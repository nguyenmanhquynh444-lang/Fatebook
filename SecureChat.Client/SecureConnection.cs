using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using SecureChat.Common.DTO;
using SecureChat.Common.Crypto;

namespace SecureChat.Client
{
    public class SecureConnection
    {
        private readonly string _serverHost;
        private readonly int _serverPort;

        private TcpClient? _socket;
        private SslStream? _sslStream;
        private StreamReader? _reader;
        private StreamWriter? _writer;

        private byte[]? _aesKey; // Khóa phiên AES-256
        private RSAUtil.RsaKeyPair? _clientKeyPair; // Cặp khóa RSA của client
        private UserDTO? _loggedInUser;
        private readonly object _sendLock = new();
        private Thread? _receiveThread;

        private volatile bool _connected = false;

        public bool IsConnected => _connected;
        public UserDTO? LoggedInUser => _loggedInUser;
        public byte[]? AesKey => _aesKey;

        // Sự kiện callback nhận tin nhắn
        public event Action<MessageDTO>? MessageReceived;
        public event Action<string>? ConnectionError;

        public SecureConnection(string serverHost, int serverPort)
        {
            _serverHost = serverHost;
            _serverPort = serverPort;
        }

        /// <summary>
        /// Kết nối đến Server và thực hiện Handshake (Đăng nhập -> Trao đổi khóa).
        /// </summary>
        public UserDTO? Connect(string username, string password)
        {
            try
            {
                _socket = new TcpClient();
                _socket.Connect(_serverHost, _serverPort);

                // Khởi tạo SslStream với bộ lọc xác thực chứng chỉ CA tùy chỉnh
                _sslStream = new SslStream(
                    _socket.GetStream(),
                    false,
                    new RemoteCertificateValidationCallback(ValidateServerCertificate),
                    null
                );

                // Thực hiện SSL Handshake
                _sslStream.AuthenticateAsClient(_serverHost);

                _reader = new StreamReader(_sslStream, Encoding.UTF8);
                _writer = new StreamWriter(_sslStream, Encoding.UTF8) { AutoFlush = true };

                // 1. Sinh cặp khóa RSA của Client
                _clientKeyPair = RSAUtil.GenerateKeyPair();

                // 2. Gửi thông tin đăng nhập dạng plaintext DTO qua TLS
                var loginMsg = MessageDTO.LoginRequest(username, password);
                SendRaw(loginMsg);

                // 3. Nhận phản hồi đăng nhập chứa RSA Public Key của Server
                string? responseJson = _reader.ReadLine();
                if (string.IsNullOrEmpty(responseJson)) throw new IOException("Server đóng kết nối đột ngột.");

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var loginResponse = JsonSerializer.Deserialize<MessageDTO>(responseJson, options);
                if (loginResponse == null || loginResponse.Type != MessageDTO.MessageType.LOGIN_RESPONSE)
                {
                    throw new IOException("Phản hồi từ server không đúng giao thức đăng nhập.");
                }

                string content = loginResponse.PlainContent;
                if (content.StartsWith("FAIL:"))
                {
                    Disconnect();
                    throw new Exception(content.Substring(5));
                }

                string serverPublicKey = loginResponse.EncryptedContent;
                
                _loggedInUser = new UserDTO
                {
                    Id = loginResponse.SenderId,
                    Username = username,
                    DisplayName = loginResponse.SenderUsername,
                    AvatarBase64 = loginResponse.FileName ?? string.Empty,
                    Role = string.IsNullOrWhiteSpace(loginResponse.UserRole) ? "USER" : loginResponse.UserRole,
                    IsActive = loginResponse.UserIsActive
                };

                // 4. Gửi RSA Public Key của Client lên Server
                var clientKeyMsg = new MessageDTO(MessageDTO.MessageType.KEY_EXCHANGE)
                {
                    PlainContent = _clientKeyPair.PublicKey
                };
                SendRaw(clientKeyMsg);

                // 5. Sinh khóa AES-256 ngẫu nhiên
                _aesKey = AESUtil.GenerateKey();

                // Mã hóa khóa AES bằng RSA Public Key của Server và gửi đi
                byte[] encryptedAesKey = RSAUtil.Encrypt(_aesKey, serverPublicKey);
                var aesKeyMsg = new MessageDTO(MessageDTO.MessageType.KEY_EXCHANGE)
                {
                    PlainContent = Convert.ToBase64String(encryptedAesKey)
                };
                SendRaw(aesKeyMsg);

                _connected = true;

                return _loggedInUser;
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new Exception($"Lỗi kết nối: {ex.Message}");
            }
        }

        /// <summary>
        /// Gửi tin nhắn văn bản (sẽ được tự động mã hóa AES-256-GCM).
        /// </summary>
        public void SendText(int roomId, string plaintext)
        {
            if (!_connected || _aesKey == null) throw new InvalidOperationException("Chưa kết nối tới server.");

            string encrypted = AESUtil.Encrypt(plaintext, _aesKey);
            var msg = MessageDTO.TextMessage(_loggedInUser!.Id, _loggedInUser.Username, roomId, encrypted);
            SendRaw(msg);
        }

        /// <summary>
        /// Gửi file (bytes) bằng cách mở kết nối phụ cổng 8444, mã hóa và truyền.
        /// </summary>
        public void SendFile(int roomId, string fileName, byte[] fileBytes)
        {
            if (!_connected || _aesKey == null) throw new InvalidOperationException("Chưa kết nối tới server.");

            // 1. Mã hóa file bytes bằng khóa phiên AES
            byte[] encryptedFileBytes = AESUtil.EncryptBytes(fileBytes, _aesKey);

            // 2. Kết nối tới cổng truyền file của server (8444)
            using (var fileSocket = new TcpClient())
            {
                fileSocket.Connect(_serverHost, 8444);

                using (var fileSslStream = new SslStream(
                    fileSocket.GetStream(),
                    false,
                    new RemoteCertificateValidationCallback(ValidateServerCertificate),
                    null))
                {
                    fileSslStream.AuthenticateAsClient(_serverHost);

                    using (var writer = new BinaryWriter(fileSslStream, Encoding.UTF8))
                    {
                        // Gửi metadata trước
                        writer.Write(roomId);
                        writer.Write(_loggedInUser!.Id);
                        writer.Write(fileName);
                        writer.Write(fileBytes.LongLength);
                        writer.Write(encryptedFileBytes.LongLength);

                        // Gửi toàn bộ dữ liệu file đã mã hóa
                        writer.Write(encryptedFileBytes);
                        writer.Flush();

                        // Chờ phản hồi OK từ server
                        using (var reader = new BinaryReader(fileSslStream, Encoding.UTF8))
                        {
                            string response = reader.ReadString();
                            if (response != "OK")
                            {
                                throw new Exception("Server từ chối nhận file.");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Giải mã tin nhắn văn bản nhận được bằng khóa phiên AES.
        /// </summary>
        public string DecryptMessage(string encryptedContent)
        {
            if (_aesKey == null) return "[Chưa cấu hình khóa giải mã]";
            try
            {
                return AESUtil.Decrypt(encryptedContent, _aesKey);
            }
            catch (Exception)
            {
                return "[Không thể giải mã tin nhắn]";
            }
        }

        /// <summary>
        /// Giải mã mảng bytes file nhận được bằng khóa phiên AES.
        /// </summary>
        public byte[]? DecryptFile(string base64Encrypted)
        {
            if (_aesKey == null) return null;
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(base64Encrypted);
                return AESUtil.DecryptBytes(encryptedBytes, _aesKey);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void StartReceiveLoop()
        {
            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                return;
            }

            _receiveThread = new Thread(() =>
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                try
                {
                    string? line;
                    while (_connected && _reader != null && (line = _reader.ReadLine()) != null)
                    {
                        try
                        {
                            var msg = JsonSerializer.Deserialize<MessageDTO>(line, options);
                            if (msg != null)
                            {
                                MessageReceived?.Invoke(msg);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SecureConnection] Lỗi parse tin nhắn: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_connected)
                    {
                        ConnectionError?.Invoke($"Mất kết nối server: {ex.Message}");
                    }
                }
                finally
                {
                    Disconnect();
                }
            })
            {
                IsBackground = true,
                Name = "ServerReceive"
            };
            _receiveThread.Start();
        }

        public void SendMessage(MessageDTO msg)
        {
            lock (_sendLock)
            {
                if (_writer != null)
                {
                    string json = JsonSerializer.Serialize(msg);
                    _writer.WriteLine(json);
                }
            }
        }

        public void RequestAdminUserList()
        {
            SendMessage(new MessageDTO(MessageDTO.MessageType.ADMIN_LIST_USERS));
        }

        public void CreateUser(string username, string password, string displayName)
        {
            string payload = JsonSerializer.Serialize(new
            {
                Username = username,
                Password = password,
                DisplayName = displayName
            });

            SendMessage(new MessageDTO(MessageDTO.MessageType.ADMIN_CREATE_USER)
            {
                PlainContent = payload
            });
        }

        public void UpdateUser(int userId, string username, string password, string displayName)
        {
            string payload = JsonSerializer.Serialize(new
            {
                UserId = userId,
                Username = username,
                Password = password,
                DisplayName = displayName
            });

            SendMessage(new MessageDTO(MessageDTO.MessageType.ADMIN_UPDATE_USER)
            {
                TargetUserId = userId,
                PlainContent = payload
            });
        }

        public void DeleteUser(int userId)
        {
            SendMessage(new MessageDTO(MessageDTO.MessageType.ADMIN_DELETE_USER)
            {
                TargetUserId = userId
            });
        }

        public void SetUserActive(int userId, bool isActive)
        {
            SendMessage(new MessageDTO(MessageDTO.MessageType.ADMIN_SET_ACTIVE)
            {
                TargetUserId = userId,
                PlainContent = isActive.ToString()
            });
        }

        private void SendRaw(MessageDTO msg)
        {
            lock (_sendLock)
            {
                if (_writer != null)
                {
                    string json = JsonSerializer.Serialize(msg);
                    _writer.WriteLine(json);
                }
            }
        }

        private static bool ValidateServerCertificate(
            object sender,
            X509Certificate? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            string certsDir = GetCertsDirectory();
            string caPath = Path.Combine(certsDir, "ca.crt");
            if (!File.Exists(caPath))
            {
                // Nếu chưa có file ca.crt (ví dụ lúc khởi động đồng thời), chấp nhận tạm
                return false;
            }

            try
            {
                var caCert = new X509Certificate2(caPath);

                var chain2 = new X509Chain();
                chain2.ChainPolicy.ExtraStore.Add(caCert);
                chain2.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                chain2.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                if (certificate != null)
                {
                    var serverCert2 = new X509Certificate2(certificate);
                    bool isValid = chain2.Build(serverCert2);

                    if (!isValid)
                    {
                        foreach (var element in chain2.ChainElements)
                        {
                            if (element.Certificate.Thumbprint == caCert.Thumbprint)
                            {
                                return true;
                            }
                        }
                        return false;
                    }

                    var chainRoot = chain2.ChainElements[chain2.ChainElements.Count - 1].Certificate;
                    return chainRoot.Thumbprint == caCert.Thumbprint;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecureConnection] Lỗi xác thực CA: {ex.Message}");
            }

            return false;
        }

        private static string GetCertsDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo? dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "SecureChat.Client")) && 
                    Directory.Exists(Path.Combine(dir.FullName, "SecureChat.Server")))
                {
                    string certsDir = Path.Combine(dir.FullName, "certs");
                    if (!Directory.Exists(certsDir))
                    {
                        Directory.CreateDirectory(certsDir);
                    }
                    return certsDir;
                }
                dir = dir.Parent;
            }
            return Path.Combine(baseDir, "certs");
        }

        public void Disconnect()
        {
            _connected = false;
            try { _reader?.Close(); } catch { }
            try { _writer?.Close(); } catch { }
            try { _sslStream?.Close(); } catch { }
            try { _socket?.Close(); } catch { }
        }
    }
}
