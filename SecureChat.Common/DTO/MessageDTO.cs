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
            VIDEO_FRAME
        }

        public MessageType Type { get; set; }
        public int SenderId { get; set; }
        public string SenderUsername { get; set; } = string.Empty;
        public int RoomId { get; set; }
        public string EncryptedContent { get; set; } = string.Empty; // AES-GCM encrypted (Base64)
        public string PlainContent { get; set; } = string.Empty;     // Used only for SYSTEM/LOGIN (plaintext)
        public string FileName { get; set; } = string.Empty;         // For Type == FILE
        public long FileSize { get; set; }
        public long Timestamp { get; set; }

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
