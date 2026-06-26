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
            string sql = "SELECT id, username, password, display_name, public_key, status, avatar_base64, role, is_active " +
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
            string sql = "INSERT INTO users (username, password, display_name, public_key, role, is_active) " +
                         "VALUES (@username, @password, @displayName, 'PENDING', 'USER', TRUE)";

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

        public static List<UserDTO> GetAllUsersForAdmin()
        {
            var list = new List<UserDTO>();
            string sql = "SELECT id, username, display_name, public_key, status, avatar_base64, role, is_active " +
                         "FROM users ORDER BY role, username";

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
                Console.WriteLine($"[UserDAO] Lỗi lấy danh sách quản trị: {ex.Message}");
            }

            return list;
        }

        public static bool DeleteUser(int userId, out string error)
        {
            error = string.Empty;

            try
            {
                using var conn = DatabaseConnection.GetConnection();
                using var transaction = conn.BeginTransaction();

                using (var deleteMessages = new MySqlCommand(
                    "DELETE FROM messages WHERE sender_id = @id", conn, transaction))
                {
                    deleteMessages.Parameters.AddWithValue("@id", userId);
                    deleteMessages.ExecuteNonQuery();
                }

                using (var deleteUser = new MySqlCommand(
                    "DELETE FROM users WHERE id = @id AND role <> 'ADMIN'", conn, transaction))
                {
                    deleteUser.Parameters.AddWithValue("@id", userId);
                    int affected = deleteUser.ExecuteNonQuery();
                    if (affected == 0)
                    {
                        transaction.Rollback();
                        error = "Không tìm thấy user hoặc không được phép xóa tài khoản admin.";
                        return false;
                    }
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Console.WriteLine($"[UserDAO] Lỗi xóa user {userId}: {ex.Message}");
                return false;
            }
        }

        public static bool UpdateUserAdmin(int userId, string username, string? rawPassword, string displayName, out string error)
        {
            error = string.Empty;
            try
            {
                using var conn = DatabaseConnection.GetConnection();

                string checkSql = "SELECT COUNT(*) FROM users WHERE username = @username AND id <> @id";
                using (var checkCmd = new MySqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@username", username);
                    checkCmd.Parameters.AddWithValue("@id", userId);
                    long count = Convert.ToInt64(checkCmd.ExecuteScalar());
                    if (count > 0)
                    {
                        error = "Tên đăng nhập đã tồn tại.";
                        return false;
                    }
                }

                string sql;
                if (!string.IsNullOrEmpty(rawPassword))
                {
                    string hashedPwd = BCrypt.Net.BCrypt.HashPassword(rawPassword, 12);
                    sql = "UPDATE users SET username = @username, password = @password, display_name = @displayName " +
                          "WHERE id = @id AND role <> 'ADMIN'";
                    using var cmd = new MySqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@password", hashedPwd);
                    cmd.Parameters.AddWithValue("@displayName", displayName);
                    cmd.Parameters.AddWithValue("@id", userId);
                    int affected = cmd.ExecuteNonQuery();
                    if (affected == 0)
                    {
                        error = "Không tìm thấy user hoặc không được phép sửa tài khoản admin.";
                        return false;
                    }
                    return true;
                }
                else
                {
                    sql = "UPDATE users SET username = @username, display_name = @displayName " +
                          "WHERE id = @id AND role <> 'ADMIN'";
                    using var cmd = new MySqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@displayName", displayName);
                    cmd.Parameters.AddWithValue("@id", userId);
                    int affected = cmd.ExecuteNonQuery();
                    if (affected == 0)
                    {
                        error = "Không tìm thấy user hoặc không được phép sửa tài khoản admin.";
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Console.WriteLine($"[UserDAO] Lỗi cập nhật user {userId}: {ex.Message}");
                return false;
            }
        }

        public static bool SetUserActive(int userId, bool isActive, out string error)
        {
            error = string.Empty;
            string sql = "UPDATE users SET is_active = @isActive, " +
                         "status = CASE WHEN @isActive = FALSE THEN 'OFFLINE' ELSE status END " +
                         "WHERE id = @id AND role <> 'ADMIN'";

            try
            {
                using var conn = DatabaseConnection.GetConnection();
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@isActive", isActive);
                cmd.Parameters.AddWithValue("@id", userId);

                if (cmd.ExecuteNonQuery() == 0)
                {
                    error = "Không tìm thấy user hoặc không được phép vô hiệu hóa tài khoản admin.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Console.WriteLine($"[UserDAO] Lỗi cập nhật trạng thái hoạt động user {userId}: {ex.Message}");
                return false;
            }
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
        /// Cập nhật Avatar dạng Base64 của người dùng.
        /// </summary>
        public static void UpdateAvatar(int userId, string avatarBase64)
        {
            string sql = "UPDATE users SET avatar_base64 = @avatar WHERE id = @id";
            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@avatar", string.IsNullOrEmpty(avatarBase64) ? DBNull.Value : avatarBase64);
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserDAO] Lỗi cập nhật avatar cho user {userId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy toàn bộ danh sách tài khoản active.
        /// </summary>
        public static List<UserDTO> GetAllActiveUsers(int currentUserId = 0)
        {
            var list = new List<UserDTO>();
            string sql;
            if (currentUserId > 0)
            {
                sql = @"
                    SELECT u.id, u.username, u.display_name, u.public_key, u.status, u.avatar_base64, u.role, u.is_active,
                           f.status AS friendship_status, f.sender_id AS friendship_sender
                    FROM users u
                    LEFT JOIN friendships f ON (f.sender_id = @currentUserId AND f.receiver_id = u.id) OR (f.sender_id = u.id AND f.receiver_id = @currentUserId)
                    WHERE u.is_active = TRUE AND u.id <> @currentUserId
                    ORDER BY u.display_name";
            }
            else
            {
                sql = "SELECT id, username, display_name, public_key, status, avatar_base64, role, is_active " +
                      "FROM users WHERE is_active = TRUE ORDER BY display_name";
            }

            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    if (currentUserId > 0)
                    {
                        cmd.Parameters.AddWithValue("@currentUserId", currentUserId);
                    }
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(MapToDTO(reader, currentUserId));
                        }
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
            string sql = "SELECT id, username, display_name, public_key, status, avatar_base64, role, is_active " +
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

        private static UserDTO MapToDTO(MySqlDataReader reader, int currentUserId = 0)
        {
            int avatarCol = reader.GetOrdinal("avatar_base64");
            var dto = new UserDTO
            {
                Id = reader.GetInt32("id"),
                Username = reader.GetString("username"),
                DisplayName = reader.GetString("display_name"),
                PublicKey = reader.GetString("public_key"),
                Status = reader.GetString("status"),
                AvatarBase64 = (avatarCol >= 0 && !reader.IsDBNull(avatarCol)) ? reader.GetString(avatarCol) : string.Empty,
                Role = reader.GetString("role"),
                IsActive = reader.GetBoolean("is_active"),
                FriendshipStatus = "NONE"
            };

            if (currentUserId > 0)
            {
                int statusCol = -1;
                int senderCol = -1;
                try { statusCol = reader.GetOrdinal("friendship_status"); } catch { }
                try { senderCol = reader.GetOrdinal("friendship_sender"); } catch { }

                if (statusCol >= 0 && !reader.IsDBNull(statusCol))
                {
                    string rawStatus = reader.GetString(statusCol); // "PENDING" or "ACCEPTED"
                    if (rawStatus == "ACCEPTED")
                    {
                        dto.FriendshipStatus = "ACCEPTED";
                    }
                    else if (rawStatus == "PENDING" && senderCol >= 0 && !reader.IsDBNull(senderCol))
                    {
                        int senderId = reader.GetInt32(senderCol);
                        dto.FriendshipStatus = (senderId == currentUserId) ? "PENDING_SENT" : "PENDING_RECEIVED";
                    }
                }
            }

            return dto;
        }
    }
}
