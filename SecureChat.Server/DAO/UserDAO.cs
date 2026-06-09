using System;
using System.Collections.Generic;
using MySqlConnector;
using BCrypt.Net;
using SecureChat.Common.DTO;

namespace SecureChat.Server.DAO
{
    public static class UserDAO
    {
        /// <summary>
        /// Xác thực thông tin đăng nhập của người dùng.
        /// </summary>
        public static UserDTO? Authenticate(string username, string rawPassword)
        {
            string sql = "SELECT id, username, password, display_name, public_key, status " +
                         "FROM users WHERE username = @username AND is_active = TRUE";

            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string hashedPassword = reader.GetString("password");
                            // Xác thực mật khẩu sử dụng BCrypt
                            if (BCrypt.Net.BCrypt.Verify(rawPassword, hashedPassword))
                            {
                                return MapToDTO(reader);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserDAO] Lỗi xác thực user '{username}': {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Tạo một người dùng mới (được hash mật khẩu bằng BCrypt).
        /// </summary>
        public static int CreateUser(string username, string rawPassword, string displayName)
        {
            string hashedPwd = BCrypt.Net.BCrypt.HashPassword(rawPassword, 12);
            string sql = "INSERT INTO users (username, password, display_name, public_key) " +
                         "VALUES (@username, @password, @displayName, 'PENDING')";

            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@password", hashedPwd);
                    cmd.Parameters.AddWithValue("@displayName", displayName);
                    cmd.ExecuteNonQuery();
                    return (int)cmd.LastInsertedId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserDAO] Lỗi tạo user '{username}': {ex.Message}");
            }
            return -1;
        }

        /// <summary>
        /// Cập nhật khóa công khai RSA của người dùng.
        /// </summary>
        public static void UpdatePublicKey(int userId, string publicKeyBase64)
        {
            string sql = "UPDATE users SET public_key = @publicKey WHERE id = @id";
            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@publicKey", publicKeyBase64);
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserDAO] Lỗi cập nhật public key cho user {userId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Cập nhật trạng thái online/offline/away của người dùng.
        /// </summary>
        public static void UpdateStatus(int userId, string status)
        {
            string sql = "UPDATE users SET status = @status WHERE id = @id";
            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserDAO] Lỗi cập nhật status cho user {userId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy toàn bộ danh sách tài khoản active.
        /// </summary>
        public static List<UserDTO> GetAllActiveUsers()
        {
            var list = new List<UserDTO>();
            string sql = "SELECT id, username, display_name, public_key, status " +
                         "FROM users WHERE is_active = TRUE ORDER BY display_name";
            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapToDTO(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserDAO] Lỗi lấy danh sách user: {ex.Message}");
            }
            return list;
        }

        /// <summary>
        /// Lấy thông tin user theo ID.
        /// </summary>
        public static UserDTO? GetUserById(int id)
        {
            string sql = "SELECT id, username, display_name, public_key, status " +
                         "FROM users WHERE id = @id";
            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return MapToDTO(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserDAO] Lỗi lấy user by id {id}: {ex.Message}");
            }
            return null;
        }

        private static UserDTO MapToDTO(MySqlDataReader reader)
        {
            return new UserDTO
            {
                Id = reader.GetInt32("id"),
                Username = reader.GetString("username"),
                DisplayName = reader.GetString("display_name"),
                PublicKey = reader.GetString("public_key"),
                Status = reader.GetString("status")
            };
        }
    }
}
