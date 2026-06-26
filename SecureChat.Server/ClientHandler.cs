using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
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
        private Thread? _clientThread;

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
            // Mỗi client được xử lý bởi một Thread nền tạo thủ công.
            _clientThread = new Thread(() =>
            {
                try
                {
                    SetupSslStream();
                    PerformHandshake();
                    MessageLoop();
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
            })
            {
                IsBackground = true,
                Name = $"ChatClient-{_clientSocket.Client.RemoteEndPoint}"
            };
            _clientThread.Start();
        }

        private void SetupSslStream()
        {
            // Thiết lập SSL Stream
            _sslStream = new SslStream(_clientSocket.GetStream(), false);
            
            // Xác thực Server TLS 1.3 / 1.2
            _sslStream.AuthenticateAsServer(
                _serverCertificate,
                clientCertificateRequired: false,
                enabledSslProtocols: SslProtocols.Tls12 | SslProtocols.Tls13,
                checkCertificateRevocation: false);

            _reader = new StreamReader(_sslStream, Encoding.UTF8);
            _writer = new StreamWriter(_sslStream, Encoding.UTF8) { AutoFlush = true };
        }

        private void PerformHandshake()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // ── Bước 1: Đăng nhập ──────────────────────────────────
            string? loginJson = _reader!.ReadLine();
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
            string? clientKeyJson = _reader.ReadLine();
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
            string? keyExchangeJson = _reader.ReadLine();
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

        private void MessageLoop()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string? line;
            while (_connected && (line = _reader!.ReadLine()) != null)
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
                case MessageDTO.MessageType.VIDEO_INVITE:
                case MessageDTO.MessageType.VIDEO_ACCEPT:
                case MessageDTO.MessageType.VIDEO_REJECT:
                case MessageDTO.MessageType.VIDEO_HANGUP:
                case MessageDTO.MessageType.VIDEO_FRAME:
                    HandleVideoMessage(msg);
                    break;
                case MessageDTO.MessageType.FRIEND_REQUEST:
                    HandleFriendRequest(msg);
                    break;
                case MessageDTO.MessageType.FRIEND_ACCEPT:
                    HandleFriendAccept(msg);
                    break;
                case MessageDTO.MessageType.FRIEND_DECLINE:
                    HandleFriendDecline(msg);
                    break;
                case MessageDTO.MessageType.AVATAR_UPDATE:
                    HandleAvatarUpdate(msg);
                    break;
                case MessageDTO.MessageType.ADMIN_LIST_USERS:
                    HandleAdminListUsers();
                    break;
                case MessageDTO.MessageType.ADMIN_CREATE_USER:
                    HandleAdminCreateUser(msg);
                    break;
                case MessageDTO.MessageType.ADMIN_DELETE_USER:
                    HandleAdminDeleteUser(msg);
                    break;
                case MessageDTO.MessageType.ADMIN_SET_ACTIVE:
                    HandleAdminSetActive(msg);
                    break;
                case MessageDTO.MessageType.ADMIN_UPDATE_USER:
                    HandleAdminUpdateUser(msg);
                    break;
            }
        }

        private bool RequireAdmin()
        {
            if (_currentUser != null &&
                string.Equals(_currentUser.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            SendAdminResult(false, "Bạn không có quyền thực hiện thao tác quản trị.");
            return false;
        }

        private void HandleAdminListUsers()
        {
            if (!RequireAdmin()) return;
            SendAdminUserList();
        }

        private void HandleAdminCreateUser(MessageDTO msg)
        {
            if (!RequireAdmin()) return;

            try
            {
                using JsonDocument document = JsonDocument.Parse(msg.PlainContent);
                JsonElement root = document.RootElement;
                string username = root.GetProperty("Username").GetString()?.Trim() ?? string.Empty;
                string password = root.GetProperty("Password").GetString() ?? string.Empty;
                string displayName = root.GetProperty("DisplayName").GetString()?.Trim() ?? string.Empty;

                if (username.Length < 3 || username.Length > 50)
                {
                    SendAdminResult(false, "Tên đăng nhập phải có từ 3 đến 50 ký tự.");
                    return;
                }

                if (username.IndexOfAny(new[] { ':', ' ', '\t', '\r', '\n' }) >= 0)
                {
                    SendAdminResult(false, "Tên đăng nhập không được chứa khoảng trắng hoặc dấu hai chấm.");
                    return;
                }

                if (password.Length < 6)
                {
                    SendAdminResult(false, "Mật khẩu phải có ít nhất 6 ký tự.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = username;
                }

                if (displayName.Length > 100)
                {
                    SendAdminResult(false, "Tên hiển thị không được vượt quá 100 ký tự.");
                    return;
                }

                int userId = UserDAO.CreateUser(username, password, displayName);
                if (userId <= 0)
                {
                    SendAdminResult(false, "Không thể tạo user. Tên đăng nhập có thể đã tồn tại.");
                    return;
                }

                SendAdminResult(true, $"Đã tạo user '{username}'.");
                SendAdminUserList();
                _roomManager.BroadcastUserLists();
            }
            catch (Exception ex)
            {
                SendAdminResult(false, $"Dữ liệu tạo user không hợp lệ: {ex.Message}");
            }
        }

        private void HandleAdminDeleteUser(MessageDTO msg)
        {
            if (!RequireAdmin()) return;
            if (_currentUser == null || msg.TargetUserId == _currentUser.Id)
            {
                SendAdminResult(false, "Không thể xóa tài khoản đang đăng nhập.");
                return;
            }

            if (!UserDAO.DeleteUser(msg.TargetUserId, out string error))
            {
                SendAdminResult(false, error);
                return;
            }

            _roomManager.DisconnectUser(msg.TargetUserId, "Tài khoản của bạn đã bị quản trị viên xóa.");
            SendAdminResult(true, "Đã xóa user.");
            SendAdminUserList();
            _roomManager.BroadcastUserLists();
        }

        private void HandleAdminSetActive(MessageDTO msg)
        {
            if (!RequireAdmin()) return;
            if (_currentUser == null || msg.TargetUserId == _currentUser.Id)
            {
                SendAdminResult(false, "Không thể vô hiệu hóa tài khoản đang đăng nhập.");
                return;
            }

            if (!bool.TryParse(msg.PlainContent, out bool isActive))
            {
                SendAdminResult(false, "Trạng thái tài khoản không hợp lệ.");
                return;
            }

            if (!UserDAO.SetUserActive(msg.TargetUserId, isActive, out string error))
            {
                SendAdminResult(false, error);
                return;
            }

            if (!isActive)
            {
                _roomManager.DisconnectUser(
                    msg.TargetUserId,
                    "Tài khoản của bạn đã bị quản trị viên vô hiệu hóa.");
            }

            SendAdminResult(true, isActive ? "Đã kích hoạt user." : "Đã vô hiệu hóa user.");
            SendAdminUserList();
            _roomManager.BroadcastUserLists();
        }

        private void HandleAdminUpdateUser(MessageDTO msg)
        {
            if (!RequireAdmin()) return;
            if (_currentUser == null || msg.TargetUserId == _currentUser.Id)
            {
                SendAdminResult(false, "Không thể chỉnh sửa tài khoản đang đăng nhập.");
                return;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(msg.PlainContent);
                JsonElement root = document.RootElement;
                int targetUserId = root.GetProperty("UserId").GetInt32();
                string username = root.GetProperty("Username").GetString()?.Trim() ?? string.Empty;
                string password = root.GetProperty("Password").GetString() ?? string.Empty;
                string displayName = root.GetProperty("DisplayName").GetString()?.Trim() ?? string.Empty;

                if (targetUserId != msg.TargetUserId)
                {
                    SendAdminResult(false, "Yêu cầu không đồng nhất.");
                    return;
                }

                if (username.Length < 3 || username.Length > 50)
                {
                    SendAdminResult(false, "Tên đăng nhập phải có từ 3 đến 50 ký tự.");
                    return;
                }

                if (username.IndexOfAny(new[] { ':', ' ', '\t', '\r', '\n' }) >= 0)
                {
                    SendAdminResult(false, "Tên đăng nhập không được chứa khoảng trắng hoặc dấu hai chấm.");
                    return;
                }

                if (!string.IsNullOrEmpty(password) && password.Length < 6)
                {
                    SendAdminResult(false, "Mật khẩu phải có ít nhất 6 ký tự.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = username;
                }

                if (displayName.Length > 100)
                {
                    SendAdminResult(false, "Tên hiển thị không được vượt quá 100 ký tự.");
                    return;
                }

                if (!UserDAO.UpdateUserAdmin(targetUserId, username, string.IsNullOrEmpty(password) ? null : password, displayName, out string error))
                {
                    SendAdminResult(false, error);
                    return;
                }

                _roomManager.DisconnectUser(targetUserId, "Thông tin tài khoản của bạn đã được thay đổi bởi quản trị viên. Vui lòng đăng nhập lại.");

                SendAdminResult(true, $"Đã cập nhật user '{username}'.");
                SendAdminUserList();
                _roomManager.BroadcastUserLists();
            }
            catch (Exception ex)
            {
                SendAdminResult(false, $"Dữ liệu cập nhật user không hợp lệ: {ex.Message}");
            }
        }

        private void SendAdminUserList()
        {
            var users = UserDAO.GetAllUsersForAdmin();
            SendMessage(new MessageDTO(MessageDTO.MessageType.ADMIN_USER_LIST)
            {
                PlainContent = JsonSerializer.Serialize(users)
            });
        }

        private void SendAdminResult(bool success, string message)
        {
            SendMessage(new MessageDTO(MessageDTO.MessageType.ADMIN_RESULT)
            {
                PlainContent = JsonSerializer.Serialize(new { Success = success, Message = message })
            });
        }

        private void HandleAvatarUpdate(MessageDTO msg)
        {
            if (_currentUser == null) return;

            string newAvatarBase64 = msg.PlainContent;
            UserDAO.UpdateAvatar(_currentUser.Id, newAvatarBase64);
            _currentUser.AvatarBase64 = newAvatarBase64;

            // Broadcast to other online users
            var broadcastMsg = new MessageDTO(MessageDTO.MessageType.AVATAR_UPDATE)
            {
                SenderId = _currentUser.Id,
                SenderUsername = _currentUser.Username,
                PlainContent = newAvatarBase64
            };
            _roomManager.BroadcastToAll(broadcastMsg, _currentUser.Id);
            Console.WriteLine($"[ClientHandler] {_currentUser.Username} đã cập nhật avatar mới.");
        }

        private void HandleTextMessage(MessageDTO msg)
        {
            // Kiểm tra tình trạng bạn bè trước khi cho phép gửi tin nhắn thường
            int peerId = InferPrivateRoomPeer(msg.RoomId, _currentUser!.Id);
            if (peerId > 0)
            {
                string status = FriendshipDAO.GetFriendshipStatus(_currentUser.Id, peerId);
                if (status != "ACCEPTED")
                {
                    Console.WriteLine($"[ClientHandler] Từ chối gửi tin nhắn TEXT từ {_currentUser.Id} tới {peerId} vì chưa kết bạn.");
                    return;
                }
            }

            // Lưu tin nhắn đã mã hóa vào MySQL DB
            MessageDAO.SaveMessage(msg.RoomId, _currentUser.Id, msg.EncryptedContent, "TEXT", null!, 0);

            // Chuyển tiếp tới những thành viên khác trong phòng
            msg.SenderId = _currentUser.Id;
            msg.SenderUsername = _currentUser.Username;
            _roomManager.BroadcastToRoom(msg.RoomId, msg, _currentUser.Id);
        }

        private void HandleFileMessage(MessageDTO msg)
        {
            // Kiểm tra tình trạng bạn bè trước khi cho phép gửi file
            int peerId = InferPrivateRoomPeer(msg.RoomId, _currentUser!.Id);
            if (peerId > 0)
            {
                string status = FriendshipDAO.GetFriendshipStatus(_currentUser.Id, peerId);
                if (status != "ACCEPTED")
                {
                    Console.WriteLine($"[ClientHandler] Từ chối gửi file từ {_currentUser.Id} tới {peerId} vì chưa kết bạn.");
                    return;
                }
            }

            // Lưu tin nhắn tệp tin vào MySQL DB
            MessageDAO.SaveMessage(msg.RoomId, _currentUser.Id, msg.EncryptedContent, "FILE", msg.FileName, msg.FileSize);

            msg.SenderId = _currentUser.Id;
            msg.SenderUsername = _currentUser.Username;
            _roomManager.BroadcastToRoom(msg.RoomId, msg, _currentUser.Id);
        }

        private void HandleVideoMessage(MessageDTO msg)
        {
            if (_currentUser == null) return;

            msg.SenderId = _currentUser.Id;
            msg.SenderUsername = _currentUser.Username;

            if (msg.TargetUserId <= 0)
            {
                msg.TargetUserId = InferPrivateRoomPeer(msg.RoomId, _currentUser.Id);
            }

            // Kiểm tra tình trạng bạn bè trước khi cho phép gọi video
            if (msg.TargetUserId > 0)
            {
                string status = FriendshipDAO.GetFriendshipStatus(_currentUser.Id, msg.TargetUserId);
                if (status != "ACCEPTED")
                {
                    Console.WriteLine($"[ClientHandler] Từ chối gửi video message {msg.Type} từ {_currentUser.Id} tới {msg.TargetUserId} vì chưa kết bạn.");
                    return;
                }
            }

            _roomManager.JoinRoom(msg.RoomId, _currentUser.Id, this);
            _roomManager.BroadcastToRoom(msg.RoomId, msg, _currentUser.Id);

            Console.WriteLine($"[ClientHandler] Video {msg.Type}: from={_currentUser.Id}, target={msg.TargetUserId}, room={msg.RoomId}");
        }

        private static int InferPrivateRoomPeer(int roomId, int currentUserId)
        {
            int firstUserId = roomId / 100_000;
            int secondUserId = roomId % 100_000;

            if (firstUserId == currentUserId) return secondUserId;
            if (secondUserId == currentUserId) return firstUserId;
            return 0;
        }

        private void HandleFriendRequest(MessageDTO msg)
        {
            if (_currentUser == null) return;

            int targetUserId = msg.TargetUserId;
            string? plaintext = null;

            if (!string.IsNullOrEmpty(msg.EncryptedContent))
            {
                try
                {
                    if (_aesKey != null)
                    {
                        plaintext = AESUtil.Decrypt(msg.EncryptedContent, _aesKey);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ClientHandler] Lỗi giải mã tin nhắn kết bạn đầu tiên: {ex.Message}");
                }
            }

            // 1. Lưu vào MySQL DB
            FriendshipDAO.AddFriendRequest(_currentUser.Id, targetUserId);

            // 2. Lưu tin nhắn đầu tiên (nếu có) vào MySQL DB
            if (plaintext != null)
            {
                MessageDAO.SaveMessage(msg.RoomId, _currentUser.Id, msg.EncryptedContent, "TEXT", null!, 0);
            }

            // 3. Gửi danh sách cập nhật cho chính người gửi
            SendCurrentUserList();

            // 4. Gửi danh sách cập nhật + chuyển tiếp tin nhắn kết bạn cho người nhận nếu đang online
            var receiverHandler = _roomManager.GetOnlineClients().FirstOrDefault(c => c._currentUser?.Id == targetUserId);
            if (receiverHandler != null)
            {
                receiverHandler.SendCurrentUserList();

                if (plaintext != null && receiverHandler.AesKey != null)
                {
                    try
                    {
                        var forwardedMsg = new MessageDTO(MessageDTO.MessageType.FRIEND_REQUEST)
                        {
                            SenderId = _currentUser.Id,
                            SenderUsername = _currentUser.Username,
                            RoomId = msg.RoomId,
                            TargetUserId = targetUserId,
                            EncryptedContent = AESUtil.Encrypt(plaintext, receiverHandler.AesKey),
                            Timestamp = msg.Timestamp
                        };
                        receiverHandler.SendMessage(forwardedMsg);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ClientHandler] Lỗi dịch khóa tin nhắn kết bạn: {ex.Message}");
                    }
                }
            }
        }

        private void HandleFriendAccept(MessageDTO msg)
        {
            if (_currentUser == null) return;

            int targetUserId = msg.TargetUserId;

            // 1. Cập nhật MySQL DB thành ACCEPTED
            FriendshipDAO.AcceptFriendRequest(targetUserId, _currentUser.Id);

            // 2. Gửi danh sách cập nhật cho chính người nhận (người click accept)
            SendCurrentUserList();

            // 3. Thông báo cho người gửi lời mời
            var senderHandler = _roomManager.GetOnlineClients().FirstOrDefault(c => c._currentUser?.Id == targetUserId);
            if (senderHandler != null)
            {
                senderHandler.SendCurrentUserList();

                var notifyMsg = new MessageDTO(MessageDTO.MessageType.FRIEND_ACCEPT)
                {
                    SenderId = _currentUser.Id,
                    SenderUsername = _currentUser.Username,
                    RoomId = msg.RoomId,
                    TargetUserId = targetUserId
                };
                senderHandler.SendMessage(notifyMsg);
            }
        }

        private void HandleFriendDecline(MessageDTO msg)
        {
            if (_currentUser == null) return;

            int targetUserId = msg.TargetUserId;

            // 1. Xóa trong MySQL DB
            FriendshipDAO.RemoveFriendship(targetUserId, _currentUser.Id);

            // 2. Gửi danh sách cập nhật cho chính mình
            SendCurrentUserList();

            // 3. Thông báo cho người gửi lời mời
            var senderHandler = _roomManager.GetOnlineClients().FirstOrDefault(c => c._currentUser?.Id == targetUserId);
            if (senderHandler != null)
            {
                senderHandler.SendCurrentUserList();

                var notifyMsg = new MessageDTO(MessageDTO.MessageType.FRIEND_DECLINE)
                {
                    SenderId = _currentUser.Id,
                    SenderUsername = _currentUser.Username,
                    RoomId = msg.RoomId,
                    TargetUserId = targetUserId
                };
                senderHandler.SendMessage(notifyMsg);
            }
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
                response.SenderUsername = _currentUser.DisplayName; // client expects DisplayName in SenderUsername
                response.FileName = _currentUser.AvatarBase64;     // client expects AvatarBase64 in FileName
                response.UserRole = _currentUser.Role;
                response.UserIsActive = _currentUser.IsActive;
            }

            SendMessage(response);
        }

        private void SendSystemMessage(string text)
        {
            SendMessage(MessageDTO.SystemMessage(text));
        }

        private void SendUserList()
        {
            var users = UserDAO.GetAllActiveUsers(_currentUser?.Id ?? 0);
            var msg = new MessageDTO(MessageDTO.MessageType.USER_LIST)
            {
                PlainContent = JsonSerializer.Serialize(users)
            };
            SendMessage(msg);
        }

        public void SendCurrentUserList()
        {
            SendUserList();
        }

        public void ForceDisconnect(string reason)
        {
            SendSystemMessage(reason);
            _connected = false;
            try { _clientSocket.Close(); } catch { }
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
