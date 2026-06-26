using System;

namespace SecureChat.Common.DTO
{
    public class MessageDTO
    {
        public enum MessageType
        {
            TEXT,
            FILE,
            SYSTEM,
            LOGIN,
            LOGIN_RESPONSE,
            KEY_EXCHANGE,
            USER_LIST,
            JOIN_ROOM,
            LEAVE_ROOM,
            TYPING,
            READ_RECEIPT,
            VIDEO_INVITE,
            VIDEO_ACCEPT,
            VIDEO_REJECT,
            VIDEO_HANGUP,
            VIDEO_FRAME,
            AVATAR_UPDATE,
            ADMIN_LIST_USERS,
            ADMIN_USER_LIST,
            ADMIN_CREATE_USER,
            ADMIN_DELETE_USER,
            ADMIN_SET_ACTIVE,
            ADMIN_RESULT,
            ADMIN_UPDATE_USER,
            FRIEND_REQUEST,
            FRIEND_ACCEPT,
            FRIEND_DECLINE
        }

        public MessageType Type { get; set; }
        public int SenderId { get; set; }
        public string SenderUsername { get; set; } = string.Empty;
        public int TargetUserId { get; set; }
        public int RoomId { get; set; }
        public string EncryptedContent { get; set; } = string.Empty; // AES-GCM encrypted (Base64)
        public string PlainContent { get; set; } = string.Empty;     // Used only for SYSTEM/LOGIN (plaintext)
        public string FileName { get; set; } = string.Empty;         // For Type == FILE
        public long FileSize { get; set; }
        public long Timestamp { get; set; }
        public string UserRole { get; set; } = string.Empty;         // Used by login/admin responses
        public bool UserIsActive { get; set; } = true;

        public MessageDTO()
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public MessageDTO(MessageType type) : this()
        {
            Type = type;
        }

        // ── Static factory methods ──────────────────────────────────

        public static MessageDTO LoginRequest(string username, string password)
        {
            return new MessageDTO(MessageType.LOGIN)
            {
                PlainContent = $"{username}:{password}"
            };
        }

        public static MessageDTO TextMessage(int senderId, string senderUsername, int roomId, string encryptedContent)
        {
            return new MessageDTO(MessageType.TEXT)
            {
                SenderId = senderId,
                SenderUsername = senderUsername,
                RoomId = roomId,
                EncryptedContent = encryptedContent
            };
        }

        public static MessageDTO FileMessage(int senderId, string senderUsername, int roomId, string encryptedContent, string fileName, long fileSize)
        {
            return new MessageDTO(MessageType.FILE)
            {
                SenderId = senderId,
                SenderUsername = senderUsername,
                RoomId = roomId,
                EncryptedContent = encryptedContent,
                FileName = fileName,
                FileSize = fileSize
            };
        }

        public static MessageDTO SystemMessage(string content)
        {
            return new MessageDTO(MessageType.SYSTEM)
            {
                PlainContent = content
            };
        }
    }
}
