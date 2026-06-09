using System;
using MySqlConnector;

namespace SecureChat.Server.DAO
{
    public static class DatabaseConnection
    {
        // Cấu hình kết nối MySQL mặc định cho XAMPP
        private static readonly string ConnectionString = 
            "Server=localhost;Port=3306;Database=secure_chat;Uid=root;Pwd=;CharSet=utf8mb4;AllowPublicKeyRetrieval=True;SslMode=None;";

        /// <summary>
        /// Mở và trả về một kết nối MySQL.
        /// </summary>
        public static MySqlConnection GetConnection()
        {
            var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        /// <summary>
        /// Kiểm tra kết nối tới MySQL.
        /// </summary>
        public static void TestConnection()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    Console.WriteLine("[Database] Kết nối CSDL MySQL thành công.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] LỖI: Không thể kết nối tới MySQL XAMPP: {ex.Message}");
                Console.WriteLine("[Database] Hãy đảm bảo XAMPP Control Panel đang chạy và dịch vụ MySQL đã được bật.");
                throw;
            }
        }
    }
}
