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

        public static void EnsureSchemaAndAdmin()
        {
            using var conn = GetConnection();

            EnsureColumn(
                conn,
                "users",
                "avatar_base64",
                "ALTER TABLE users ADD COLUMN avatar_base64 LONGTEXT NULL AFTER status");
            EnsureColumn(
                conn,
                "users",
                "role",
                "ALTER TABLE users ADD COLUMN role ENUM('ADMIN','USER') NOT NULL DEFAULT 'USER' AFTER avatar_base64");
            EnsureColumn(
                conn,
                "users",
                "is_active",
                "ALTER TABLE users ADD COLUMN is_active BOOLEAN NOT NULL DEFAULT TRUE");

            string adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123", 12);
            const string ensureAdminSql =
                "INSERT INTO users (username, password, display_name, public_key, status, role, is_active) " +
                "VALUES ('admin', @password, 'Administrator', 'PENDING', 'OFFLINE', 'ADMIN', TRUE) " +
                "ON DUPLICATE KEY UPDATE password = @password, display_name = 'Administrator', " +
                "role = 'ADMIN', is_active = TRUE";

            using var cmd = new MySqlCommand(ensureAdminSql, conn);
            cmd.Parameters.AddWithValue("@password", adminPasswordHash);
            cmd.ExecuteNonQuery();

            Console.WriteLine("[Database] Đã bảo đảm tài khoản quản trị admin/admin123.");

            EnsureFriendshipTable(conn);
        }

        private static void EnsureFriendshipTable(MySqlConnection conn)
        {
            string sql = @"
                CREATE TABLE IF NOT EXISTS friendships (
                    sender_id INT NOT NULL,
                    receiver_id INT NOT NULL,
                    status ENUM('PENDING', 'ACCEPTED') NOT NULL DEFAULT 'PENDING',
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (sender_id, receiver_id),
                    FOREIGN KEY (sender_id) REFERENCES users(id) ON DELETE CASCADE,
                    FOREIGN KEY (receiver_id) REFERENCES users(id) ON DELETE CASCADE
                ) ENGINE=InnoDB;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
            Console.WriteLine("[Database] Đã bảo đảm bảng friendships.");
        }

        private static void EnsureColumn(
            MySqlConnection conn,
            string tableName,
            string columnName,
            string alterSql)
        {
            const string existsSql =
                "SELECT COUNT(*) FROM information_schema.COLUMNS " +
                "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName";

            using var existsCmd = new MySqlCommand(existsSql, conn);
            existsCmd.Parameters.AddWithValue("@tableName", tableName);
            existsCmd.Parameters.AddWithValue("@columnName", columnName);
            bool exists = Convert.ToInt32(existsCmd.ExecuteScalar()) > 0;

            if (!exists)
            {
                using var alterCmd = new MySqlCommand(alterSql, conn);
                alterCmd.ExecuteNonQuery();
            }
        }
    }
}
