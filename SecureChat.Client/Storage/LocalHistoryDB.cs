using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace SecureChat.Client.Storage
{
    public class LocalHistoryDB
    {
        private static readonly string DbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".securechat");
        private readonly string _dbPath;
        private SqliteConnection? _connection;

        public LocalHistoryDB(string username)
        {
            _dbPath = Path.Combine(DbDir, $"{username}_history.db");
        }

        /// <summary>
        /// Khởi tạo thư mục và kết nối SQLite, tạo bảng nếu chưa có.
        /// </summary>
        public void Init()
        {
            try
            {
                if (!Directory.Exists(DbDir))
                {
                    Directory.CreateDirectory(DbDir);
                }

                _connection = new SqliteConnection($"Data Source={_dbPath}");
                _connection.Open();
                CreateTables();
                Console.WriteLine($"[LocalDB] SQLite được kết nối thành công: {_dbPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalDB] Lỗi khởi tạo SQLite: {ex.Message}");
            }
        }

        private void CreateTables()
        {
            string sql = @"
                CREATE TABLE IF NOT EXISTS local_messages (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    room_id      INTEGER NOT NULL,
                    sender_id    INTEGER NOT NULL,
                    sender_name  TEXT    NOT NULL,
                    content      TEXT    NOT NULL,
                    message_type TEXT    DEFAULT 'TEXT',
                    file_name    TEXT,
                    timestamp    INTEGER NOT NULL,
                    is_sent      INTEGER DEFAULT 1
                );
                CREATE INDEX IF NOT EXISTS idx_room_time ON local_messages(room_id, timestamp);
            ";

            using (var cmd = new SqliteCommand(sql, _connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Lưu tin nhắn đã giải mã vào SQLite.
        /// </summary>
        public void SaveMessage(int roomId, int senderId, string senderName, string content, string type, string fileName, long timestamp, bool isSent)
        {
            string sql = @"
                INSERT INTO local_messages (room_id, sender_id, sender_name, content, message_type, file_name, timestamp, is_sent)
                VALUES ($roomId, $senderId, $senderName, $content, $type, $fileName, $timestamp, $isSent)
            ";

            try
            {
                using (var cmd = new SqliteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("$roomId", roomId);
                    cmd.Parameters.AddWithValue("$senderId", senderId);
                    cmd.Parameters.AddWithValue("$senderName", senderName);
                    cmd.Parameters.AddWithValue("$content", content);
                    cmd.Parameters.AddWithValue("$type", type);
                    cmd.Parameters.AddWithValue("$fileName", string.IsNullOrEmpty(fileName) ? DBNull.Value : fileName);
                    cmd.Parameters.AddWithValue("$timestamp", timestamp);
                    cmd.Parameters.AddWithValue("$isSent", isSent ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalDB] Lỗi lưu message cục bộ: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy lịch sử tin nhắn của một phòng (sắp xếp tăng dần theo thời gian).
        /// </summary>
        public List<object[]> GetHistory(int roomId, int limit)
        {
            var results = new List<object[]>();
            string sql = @"
                SELECT room_id, sender_id, sender_name, content, message_type, file_name, timestamp, is_sent
                FROM local_messages
                WHERE room_id = $roomId
                ORDER BY timestamp DESC
                LIMIT $limit
            ";

            try
            {
                using (var cmd = new SqliteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("$roomId", roomId);
                    cmd.Parameters.AddWithValue("$limit", limit);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new object[]
                            {
                                reader.GetInt32(0),
                                reader.GetInt32(1),
                                reader.GetString(2),
                                reader.GetString(3),
                                reader.GetString(4),
                                reader.IsDBNull(5) ? "" : reader.GetString(5),
                                reader.GetInt64(6),
                                reader.GetInt32(7) == 1
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalDB] Lỗi đọc lịch sử: {ex.Message}");
            }

            // Đảo ngược danh sách để hiển thị theo thời gian tăng dần
            results.Reverse();
            return results;
        }

        /// <summary>
        /// Tìm kiếm tin nhắn theo từ khóa.
        /// </summary>
        public List<object[]> SearchMessages(string keyword)
        {
            var results = new List<object[]>();
            string sql = @"
                SELECT room_id, sender_name, content, timestamp
                FROM local_messages
                WHERE content LIKE $keyword
                ORDER BY timestamp DESC
                LIMIT 50
            ";

            try
            {
                using (var cmd = new SqliteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("$keyword", $"%{keyword}%");
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new object[]
                            {
                                reader.GetInt32(0),
                                reader.GetString(1),
                                reader.GetString(2),
                                reader.GetInt64(3)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalDB] Lỗi tìm kiếm: {ex.Message}");
            }
            return results;
        }

        /// <summary>
        /// Xóa lịch sử tin nhắn phòng.
        /// </summary>
        public void ClearRoomHistory(int roomId)
        {
            string sql = "DELETE FROM local_messages WHERE room_id = $roomId";
            try
            {
                using (var cmd = new SqliteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("$roomId", roomId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalDB] Lỗi xoá lịch sử phòng {roomId}: {ex.Message}");
            }
        }

        public void Close()
        {
            try
            {
                _connection?.Close();
            }
            catch { }
        }
    }
}
