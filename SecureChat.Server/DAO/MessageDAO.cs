using System;
using System.Collections.Generic;
using MySqlConnector;

namespace SecureChat.Server.DAO
{
    public static class MessageDAO
    {
        /// <summary>
        /// Lưu tin nhắn mã hoá vào MySQL database.
        /// </summary>
        public static int SaveMessage(int roomId, int senderId, string encryptedContent, string type, string fileName, long fileSize)
        {
            // Đảm bảo phòng tồn tại trước khi lưu tin nhắn để tránh lỗi Foreign Key
            CreateRoomIfNotExist(roomId, $"Room_{roomId}");

            string sql = "INSERT INTO messages " +
                         "(room_id, sender_id, encrypted_content, iv, message_type, file_name, file_size) " +
                         "VALUES (@roomId, @senderId, @encryptedContent, '', @messageType, @fileName, @fileSize)";

            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@roomId", roomId);
                    cmd.Parameters.AddWithValue("@senderId", senderId);
                    cmd.Parameters.AddWithValue("@encryptedContent", encryptedContent);
                    cmd.Parameters.AddWithValue("@messageType", type);
                    cmd.Parameters.AddWithValue("@fileName", string.IsNullOrEmpty(fileName) ? DBNull.Value : fileName);
                    cmd.Parameters.AddWithValue("@fileSize", fileSize);
                    cmd.ExecuteNonQuery();
                    return (int)cmd.LastInsertedId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MessageDAO] Lỗi lưu message: {ex.Message}");
            }
            return -1;
        }

        /// <summary>
        /// Tạo phòng chat trong database nếu chưa tồn tại (tránh lỗi Foreign Key).
        /// </summary>
        public static void CreateRoomIfNotExist(int roomId, string roomName)
        {
            string sql = "INSERT IGNORE INTO chat_rooms (id, room_name, room_type) VALUES (@id, @name, 'PRIVATE')";
            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", roomId);
                    cmd.Parameters.AddWithValue("@name", roomName);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MessageDAO] Lỗi tạo phòng chat {roomId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy lịch sử tin nhắn trong phòng (giới hạn số lượng).
        /// </summary>
        public static List<object[]> GetMessagesByRoom(int roomId, int limit)
        {
            var results = new List<object[]>();
            string sql = "SELECT m.id, m.sender_id, u.username, u.display_name, " +
                         "       m.encrypted_content, m.message_type, m.file_name, m.sent_at " +
                         "FROM messages m " +
                         "JOIN users u ON m.sender_id = u.id " +
                         "WHERE m.room_id = @roomId " +
                         "ORDER BY m.sent_at DESC LIMIT @limit";

            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@roomId", roomId);
                    cmd.Parameters.AddWithValue("@limit", limit);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new object[]
                            {
                                reader.GetInt32("id"),
                                reader.GetInt32("sender_id"),
                                reader.GetString("username"),
                                reader.GetString("display_name"),
                                reader.GetString("encrypted_content"),
                                reader.GetString("message_type"),
                                reader.IsDBNull(reader.GetOrdinal("file_name")) ? "" : reader.GetString("file_name"),
                                reader.GetDateTime("sent_at")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MessageDAO] Lỗi lấy messages: {ex.Message}");
            }
            return results;
        }

        /// <summary>
        /// Xóa lịch sử phòng chat.
        /// </summary>
        public static bool ClearRoomHistory(int roomId)
        {
            string sql = "DELETE FROM messages WHERE room_id = @roomId";
            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@roomId", roomId);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MessageDAO] Lỗi xóa lịch sử phòng {roomId}: {ex.Message}");
                return false;
            }
        }
    }
}
