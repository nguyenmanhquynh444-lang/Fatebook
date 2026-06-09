using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SecureChat.Common.DTO;
using SecureChat.Common.Crypto;

namespace SecureChat.Server
{
    public class RoomManager
    {
        // Map: userId -> ClientHandler (Tất cả user đang online)
        private readonly ConcurrentDictionary<int, ClientHandler> _onlineClients = new();

        // Map: roomId -> Set of userIds (Sử dụng ConcurrentDictionary<int, byte> làm Set)
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, byte>> _roomMembers = new();

        public void RegisterClient(int userId, ClientHandler handler)
        {
            _onlineClients[userId] = handler;
            Console.WriteLine($"[RoomManager] Đăng ký client: userId={userId}, tổng online={_onlineClients.Count}");
        }

        public void RemoveClient(int userId)
        {
            _onlineClients.TryRemove(userId, out _);
            foreach (var members in _roomMembers.Values)
            {
                members.TryRemove(userId, out _);
            }
            Console.WriteLine($"[RoomManager] Hủy đăng ký client: userId={userId}, tổng online={_onlineClients.Count}");
        }

        public bool IsOnline(int userId)
        {
            return _onlineClients.ContainsKey(userId);
        }

        public IEnumerable<ClientHandler> GetOnlineClients()
        {
            return _onlineClients.Values;
        }

        public void JoinRoom(int roomId, int userId, ClientHandler handler)
        {
            var members = _roomMembers.GetOrAdd(roomId, _ => new ConcurrentDictionary<int, byte>());
            members.TryAdd(userId, 0);
            _onlineClients[userId] = handler;
        }

        public void LeaveRoom(int roomId, int userId)
        {
            if (_roomMembers.TryGetValue(roomId, out var members))
            {
                members.TryRemove(userId, out _);
            }
        }

        public HashSet<int> GetRoomMembers(int roomId)
        {
            if (_roomMembers.TryGetValue(roomId, out var members))
            {
                return new HashSet<int>(members.Keys);
            }
            return new HashSet<int>();
        }

        /// <summary>
        /// Tạo hoặc lấy phòng chat riêng tư ảo giữa 2 người dùng.
        /// RoomId = min(a,b)*100000 + max(a,b) (Đảm bảo ID phòng là duy nhất)
        /// </summary>
        public int GetOrCreatePrivateRoom(int userId1, int userId2)
        {
            int minId = Math.Min(userId1, userId2);
            int maxId = Math.Max(userId1, userId2);
            int virtualRoomId = minId * 100_000 + maxId;

            // Tạo phòng ảo trong CSDL MySQL nếu chưa tồn tại
            DAO.MessageDAO.CreateRoomIfNotExist(virtualRoomId, $"Private_{minId}_{maxId}");

            _roomMembers.GetOrAdd(virtualRoomId, _ =>
            {
                var members = new ConcurrentDictionary<int, byte>();
                members.TryAdd(userId1, 0);
                members.TryAdd(userId2, 0);
                return members;
            });

            return virtualRoomId;
        }

        /// <summary>
        /// Gửi tin nhắn tới mọi thành viên trong phòng (trừ người gửi).
        /// Thực hiện dịch khóa đối xứng (giải mã bằng khóa người gửi, mã hóa bằng khóa từng người nhận).
        /// </summary>
        public void BroadcastToRoom(int roomId, MessageDTO msg, int excludeUserId)
        {
            var members = GetRoomMembers(roomId);
            if (members.Count == 0)
            {
                // Nếu phòng trống (chưa đăng ký member cụ thể), broadcast tới toàn server
                BroadcastToAll(msg, excludeUserId);
                return;
            }

            // Lấy ClientHandler của người gửi để lấy khóa giải mã
            _onlineClients.TryGetValue(excludeUserId, out var senderHandler);
            byte[]? senderKey = senderHandler?.AesKey;

            string? plaintext = null;
            byte[]? fileBytes = null;

            if (senderKey != null)
            {
                try
                {
                    if (msg.Type == MessageDTO.MessageType.TEXT || msg.Type == MessageDTO.MessageType.VIDEO_FRAME)
                    {
                        plaintext = AESUtil.Decrypt(msg.EncryptedContent, senderKey);
                    }
                    else if (msg.Type == MessageDTO.MessageType.FILE)
                    {
                        byte[] encryptedFileBytes = Convert.FromBase64String(msg.EncryptedContent);
                        fileBytes = AESUtil.DecryptBytes(encryptedFileBytes, senderKey);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RoomManager] Lỗi dịch khóa (giải mã) tin nhắn từ user={excludeUserId} trong phòng={roomId}: {ex.Message}");
                }
            }

            int count = 0;
            foreach (int memberId in members)
            {
                if (memberId == excludeUserId) continue;
                if (_onlineClients.TryGetValue(memberId, out var handler))
                {
                    byte[]? recipientKey = handler.AesKey;
                    if (recipientKey != null)
                    {
                        try
                        {
                            var msgCopy = new MessageDTO(msg.Type)
                            {
                                RoomId = msg.RoomId,
                                SenderId = msg.SenderId,
                                SenderUsername = msg.SenderUsername,
                                Timestamp = msg.Timestamp,
                                FileName = msg.FileName,
                                FileSize = msg.FileSize
                            };

                            if ((msg.Type == MessageDTO.MessageType.TEXT || msg.Type == MessageDTO.MessageType.VIDEO_FRAME) && plaintext != null)
                            {
                                msgCopy.EncryptedContent = AESUtil.Encrypt(plaintext, recipientKey);
                            }
                            else if (msg.Type == MessageDTO.MessageType.FILE && fileBytes != null)
                            {
                                byte[] encryptedForRecipient = AESUtil.EncryptBytes(fileBytes, recipientKey);
                                msgCopy.EncryptedContent = Convert.ToBase64String(encryptedForRecipient);
                            }
                            else
                            {
                                msgCopy.EncryptedContent = msg.EncryptedContent;
                                msgCopy.PlainContent = msg.PlainContent;
                            }

                            handler.SendMessage(msgCopy);
                            count++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[RoomManager] Lỗi dịch khóa (mã hóa) tin nhắn cho user={memberId}: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Fallback nếu người nhận chưa hoàn tất handshake khóa phiên
                        handler.SendMessage(msg);
                        count++;
                    }
                }
            }
        }

        /// <summary>
        /// Gửi tin nhắn tới tất cả client đang online (trừ người gửi).
        /// </summary>
        public void BroadcastToAll(MessageDTO msg, int excludeUserId)
        {
            int count = 0;
            foreach (var kvp in _onlineClients)
            {
                if (kvp.Key == excludeUserId) continue;
                kvp.Value.SendMessage(msg);
                count++;
            }
        }

        /// <summary>
        /// Gửi tin nhắn trực tiếp tới một user cụ thể.
        /// </summary>
        public bool SendToUser(int targetUserId, MessageDTO msg)
        {
            if (_onlineClients.TryGetValue(targetUserId, out var handler))
            {
                handler.SendMessage(msg);
                return true;
            }
            return false;
        }
    }
}
