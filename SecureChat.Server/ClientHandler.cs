using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SecureChat.Common.DTO;
using SecureChat.Common.Crypto;
using SecureChat.Server.DAO;

namespace SecureChat.Server
{
    public class ClientHandler
    {
        private readonly TcpClient _clientSocket;
        private readonly RoomManager _roomManager;
        private readonly X509Certificate2 _serverCertificate;

        private SslStream? _sslStream;
        private StreamReader? _reader;
        private StreamWriter? _writer;

        private UserDTO? _currentUser;
        private byte[]? _aesKey; // Khóa phiên AES-256
        private RSAUtil.RsaKeyPair? _sessionRsaKeyPair; // RSA KeyPair của server sinh riêng cho phiên này

        private volatile bool _connected = true;

        public UserDTO? CurrentUser => _currentUser;
        public byte[]? AesKey => _aesKey;

        public ClientHandler(TcpClient clientSocket, RoomManager roomManager, X509Certificate2 serverCertificate)
        {
            _clientSocket = clientSocket;
            _roomManager = roomManager;
            _serverCertificate = serverCertificate;
        }

        public void Start()
        {
            // Xử lý client trong một Task chạy ngầm
            Task.Run(async () =>
            {
                try
                {
                    await SetupSslStreamAsync();
                    await PerformHandshakeAsync();
                    await MessageLoopAsync();
                }
                catch (IOException)
                {
                    Console.WriteLine($"[ClientHandler] Client ngắt kết nối: {_currentUser?.Username ?? "unknown"}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ClientHandler] Lỗi xử lý client: {ex.Message}");
                }
                finally
                {
                    Cleanup();
                }
            });
        }

        private async Task SetupSslStreamAsync()
        {
            // Thiết lập SSL Stream
            _sslStream = new SslStream(_clientSocket.GetStream(), false);
            
            // Xác thực Server TLS 1.3 / 1.2
            await _sslStream.AuthenticateAsServerAsync(
                _serverCertificate,
                clientCertificateRequired: false,
                enabledSslProtocols: SslProtocols.Tls12 | SslProtocols.Tls13,
                checkCertificateRevocation: false);

            _reader = new StreamReader(_sslStream, Encoding.UTF8);
            _writer = new StreamWriter(_sslStream, Encoding.UTF8) { AutoFlush = true };
        }

        private async Task PerformHandshakeAsync()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // ── Bước 1: Đăng nhập ──────────────────────────────────
            string? loginJson = await _reader!.ReadLineAsync();
            if (string.IsNullOrEmpty(loginJson)) throw new IOException("Client không gửi thông tin đăng nhập.");

            var loginMsg = JsonSerializer.Deserialize<MessageDTO>(loginJson, options);
            if (loginMsg == null || loginMsg.Type != MessageDTO.MessageType.LOGIN)
            {
                SendSystemMessage("ERR:Yêu cầu đăng nhập trước.");
                throw new IOException("Đăng nhập không hợp lệ.");
            }

            // Parse username:password
            string[] creds = loginMsg.PlainContent.Split(':', 2);
            string username = creds[0];
            string password = creds.Length > 1 ? creds[1] : "";

            // Kiểm tra DB
            var user = UserDAO.Authenticate(username, password);
            if (user == null)
            {
                SendLoginResponse(false, "Sai tài khoản hoặc mật khẩu!", null);
                throw new IOException($"Đăng nhập thất bại: {username}");
            }

            _currentUser = user;
            Console.WriteLine($"[ClientHandler] Đăng nhập thành công: {username} (id={user.Id})");

            // ── Bước 2: Sinh cặp khóa RSA cho phiên này ─────────────
            _sessionRsaKeyPair = RSAUtil.GenerateKeyPair();

            // Phản hồi LOGIN_RESPONSE chứa RSA Public Key của Server
            SendLoginResponse(true, "Đăng nhập thành công!", _sessionRsaKeyPair.PublicKey);

            // ── Bước 3: Nhận RSA Public Key của Client ─────────────
            string? clientKeyJson = await _reader.ReadLineAsync();
            if (string.IsNullOrEmpty(clientKeyJson)) throw new IOException("Không nhận được Client Public Key.");

            var clientKeyMsg = JsonSerializer.Deserialize<MessageDTO>(clientKeyJson, options);
            if (clientKeyMsg == null || clientKeyMsg.Type != MessageDTO.MessageType.KEY_EXCHANGE)
            {
                throw new IOException("Giao thức trao đổi khóa công khai không hợp lệ.");
            }

            string clientPubKeyBase64 = clientKeyMsg.PlainContent;
            UserDAO.UpdatePublicKey(user.Id, clientPubKeyBase64);
            _currentUser.PublicKey = clientPubKeyBase64;

            // ── Bước 4: Nhận AES Session Key (đã được Client mã hóa bằng RSA của Server) ──
            string? keyExchangeJson = await _reader.ReadLineAsync();
            if (string.IsNullOrEmpty(keyExchangeJson)) throw new IOException("Không nhận được AES Key.");

            var keyMsg = JsonSerializer.Deserialize<MessageDTO>(keyExchangeJson, options);
            if (keyMsg == null || keyMsg.Type != MessageDTO.MessageType.KEY_EXCHANGE)
            {
                throw new IOException("Giao thức trao đổi AES Key không hợp lệ.");
            }

            // Giải mã AES Key bằng RSA Private Key của Server
            byte[] encryptedAesKey = Convert.FromBase64String(keyMsg.PlainContent);
            _aesKey = RSAUtil.Decrypt(encryptedAesKey, _sessionRsaKeyPair.PrivateKey);

            Console.WriteLine($"[ClientHandler] Trao đổi khóa phiên AES hoàn thành với {username}.");

            // ── Bước 5: Đăng ký online và gửi danh sách ──────────
            _roomManager.RegisterClient(user.Id, this);
            UserDAO.UpdateStatus(user.Id, "ONLINE");

            SendUserList();
            BroadcastUserJoined(user);
        }

        private async Task MessageLoopAsync()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string? line;
            while (_connected && (line = await _reader!.ReadLineAsync()) != null)
            {
                try
                {
                    var msg = JsonSerializer.Deserialize<MessageDTO>(line, options);
                    if (msg != null)
                    {
                        HandleMessage(msg);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ClientHandler] Lỗi xử lý tin nhắn từ {_currentUser?.Username}: {ex.Message}");
                }
            }
        }

        private void HandleMessage(MessageDTO msg)
        {
            switch (msg.Type)
            {
                case MessageDTO.MessageType.TEXT:
                    HandleTextMessage(msg);
                    break;
                case MessageDTO.MessageType.FILE:
                    HandleFileMessage(msg);
                    break;
                case MessageDTO.MessageType.JOIN_ROOM:
                    _roomManager.JoinRoom(msg.RoomId, _currentUser!.Id, this);
                    Console.WriteLine($"[ClientHandler] {_currentUser.Username} vào phòng {msg.RoomId}");
                    break;
                case MessageDTO.MessageType.LEAVE_ROOM:
                    _roomManager.LeaveRoom(msg.RoomId, _currentUser!.Id);
                    Console.WriteLine($"[ClientHandler] {_currentUser.Username} rời phòng {msg.RoomId}");
                    break;
                case MessageDTO.MessageType.TYPING:
                    _roomManager.BroadcastToRoom(msg.RoomId, msg, _currentUser!.Id);
                    break;
            }
        }

        private void HandleTextMessage(MessageDTO msg)
        {
            // Lưu tin nhắn đã mã hóa vào MySQL DB
            MessageDAO.SaveMessage(msg.RoomId, _currentUser!.Id, msg.EncryptedContent, "TEXT", null!, 0);

            // Chuyển tiếp tới những thành viên khác trong phòng
            msg.SenderId = _currentUser.Id;
            msg.SenderUsername = _currentUser.Username;
            _roomManager.BroadcastToRoom(msg.RoomId, msg, _currentUser.Id);
        }

        private void HandleFileMessage(MessageDTO msg)
        {
            // Lưu tin nhắn chứa file đã mã hóa vào MySQL DB
            MessageDAO.SaveMessage(msg.RoomId, _currentUser!.Id, msg.EncryptedContent, "FILE", msg.FileName, msg.FileSize);

            // Chuyển tiếp tới những thành viên khác trong phòng
            msg.SenderId = _currentUser.Id;
            msg.SenderUsername = _currentUser.Username;
            _roomManager.BroadcastToRoom(msg.RoomId, msg, _currentUser.Id);
        }

        public void SendMessage(MessageDTO msg)
        {
            lock (this)
            {
                if (_writer != null && _clientSocket.Connected)
                {
                    string json = JsonSerializer.Serialize(msg);
                    _writer.WriteLine(json);
                }
            }
        }

        private void SendLoginResponse(bool success, string message, string? serverPublicKey)
        {
            var response = new MessageDTO(MessageDTO.MessageType.LOGIN_RESPONSE)
            {
                PlainContent = success ? $"OK:{message}" : $"FAIL:{message}"
            };

            if (serverPublicKey != null)
            {
                response.EncryptedContent = serverPublicKey;
            }

            if (success && _currentUser != null)
            {
                response.SenderId = _currentUser.Id;
                response.SenderUsername = _currentUser.Username;
            }

            SendMessage(response);
        }

        private void SendSystemMessage(string text)
        {
            SendMessage(MessageDTO.SystemMessage(text));
        }

        private void SendUserList()
        {
            var users = UserDAO.GetAllActiveUsers();
            var msg = new MessageDTO(MessageDTO.MessageType.USER_LIST)
            {
                PlainContent = JsonSerializer.Serialize(users)
            };
            SendMessage(msg);
        }

        private void BroadcastUserJoined(UserDTO user)
        {
            var msg = MessageDTO.SystemMessage($"{user.DisplayName} đã online!");
            msg.SenderId = user.Id;
            msg.SenderUsername = user.Username;
            _roomManager.BroadcastToAll(msg, user.Id);
        }

        private void Cleanup()
        {
            _connected = false;
            if (_currentUser != null)
            {
                _roomManager.RemoveClient(_currentUser.Id);
                UserDAO.UpdateStatus(_currentUser.Id, "OFFLINE");

                var offlineMsg = MessageDTO.SystemMessage($"{_currentUser.DisplayName} đã offline.");
                offlineMsg.SenderId = _currentUser.Id;
                offlineMsg.SenderUsername = _currentUser.Username;
                _roomManager.BroadcastToAll(offlineMsg, _currentUser.Id);
            }

            try
            {
                _reader?.Close();
                _writer?.Close();
                _sslStream?.Close();
                _clientSocket.Close();
            }
            catch (Exception) { }
        }
    }
}
