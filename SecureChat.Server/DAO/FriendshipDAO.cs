using System;
using MySqlConnector;

namespace SecureChat.Server.DAO
{
    public static class FriendshipDAO
    {
        public static bool AddFriendRequest(int senderId, int receiverId)
        {
            string sql = @"
                INSERT INTO friendships (sender_id, receiver_id, status)
                VALUES (@senderId, @receiverId, 'PENDING')
                ON DUPLICATE KEY UPDATE status = 'PENDING'";

            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@senderId", senderId);
                    cmd.Parameters.AddWithValue("@receiverId", receiverId);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FriendshipDAO] Lỗi thêm yêu cầu kết bạn: {ex.Message}");
                return false;
            }
        }

        public static bool AcceptFriendRequest(int senderId, int receiverId)
        {
            string sql = @"
                UPDATE friendships 
                SET status = 'ACCEPTED'
                WHERE (sender_id = @senderId AND receiver_id = @receiverId)
                   OR (sender_id = @receiverId AND receiver_id = @senderId)";

            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@senderId", senderId);
                    cmd.Parameters.AddWithValue("@receiverId", receiverId);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FriendshipDAO] Lỗi chấp nhận kết bạn: {ex.Message}");
                return false;
            }
        }

        public static bool RemoveFriendship(int user1, int user2)
        {
            string sql = @"
                DELETE FROM friendships
                WHERE (sender_id = @u1 AND receiver_id = @u2)
                   OR (sender_id = @u2 AND receiver_id = @u1)";

            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u1", user1);
                    cmd.Parameters.AddWithValue("@u2", user2);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FriendshipDAO] Lỗi hủy kết bạn: {ex.Message}");
                return false;
            }
        }

        public static string GetFriendshipStatus(int loggedInUserId, int targetUserId)
        {
            string sql = @"
                SELECT sender_id, receiver_id, status 
                FROM friendships
                WHERE (sender_id = @loggedInUserId AND receiver_id = @targetUserId)
                   OR (sender_id = @targetUserId AND receiver_id = @loggedInUserId)";

            try
            {
                using (var conn = DatabaseConnection.GetConnection())
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@loggedInUserId", loggedInUserId);
                    cmd.Parameters.AddWithValue("@targetUserId", targetUserId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string status = reader.GetString("status");
                            if (status == "ACCEPTED")
                            {
                                return "ACCEPTED";
                            }
                            else if (status == "PENDING")
                            {
                                int senderId = reader.GetInt32("sender_id");
                                return (senderId == loggedInUserId) ? "PENDING_SENT" : "PENDING_RECEIVED";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FriendshipDAO] Lỗi lấy trạng thái bạn bè: {ex.Message}");
            }
            return "NONE";
        }
    }
}
