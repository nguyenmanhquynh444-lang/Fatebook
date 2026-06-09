package com.securechat.server.dao;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.sql.*;
import java.util.ArrayList;
import java.util.List;

/**
 * MessageDAO – CRUD cho bảng messages (lịch sử chat).
 */
public class MessageDAO {

    private static final Logger log = LoggerFactory.getLogger(MessageDAO.class);

    /**
     * Lưu tin nhắn mã hoá vào database.
     *
     * @param roomId           ID phòng chat
     * @param senderId         ID người gửi
     * @param encryptedContent nội dung đã mã hoá AES (Base64)
     * @param type             TEXT hoặc FILE
     * @param fileName         tên file (null nếu TEXT)
     * @param fileSize         kích thước file (0 nếu TEXT)
     * @return ID của message vừa lưu, -1 nếu lỗi
     */
    public static int saveMessage(int roomId, int senderId,
                                   String encryptedContent, String type,
                                   String fileName, long fileSize) {
        String sql = "INSERT INTO messages " +
                     "(room_id, sender_id, encrypted_content, iv, message_type, file_name, file_size) " +
                     "VALUES (?, ?, ?, '', ?, ?, ?)";

        try (Connection conn = DatabaseConnection.getConnection();
             PreparedStatement ps = conn.prepareStatement(sql, Statement.RETURN_GENERATED_KEYS)) {

            ps.setInt(1, roomId);
            ps.setInt(2, senderId);
            ps.setString(3, encryptedContent);
            ps.setString(4, type);
            ps.setString(5, fileName);
            ps.setLong(6, fileSize);
            ps.executeUpdate();

            ResultSet keys = ps.getGeneratedKeys();
            if (keys.next()) return keys.getInt(1);

        } catch (SQLException e) {
            log.error("Lỗi lưu message: {}", e.getMessage());
        }
        return -1;
    }

    /**
     * Lấy lịch sử chat của một phòng (giới hạn 100 tin nhắn gần nhất).
     */
    public static List<Object[]> getMessagesByRoom(int roomId, int limit) {
        String sql = "SELECT m.id, m.sender_id, u.username, u.display_name, " +
                     "       m.encrypted_content, m.message_type, m.file_name, m.sent_at " +
                     "FROM messages m " +
                     "JOIN users u ON m.sender_id = u.id " +
                     "WHERE m.room_id = ? " +
                     "ORDER BY m.sent_at DESC LIMIT ?";

        List<Object[]> results = new ArrayList<>();
        try (Connection conn = DatabaseConnection.getConnection();
             PreparedStatement ps = conn.prepareStatement(sql)) {

            ps.setInt(1, roomId);
            ps.setInt(2, limit);
            ResultSet rs = ps.executeQuery();

            while (rs.next()) {
                results.add(new Object[]{
                    rs.getInt("id"),
                    rs.getInt("sender_id"),
                    rs.getString("username"),
                    rs.getString("display_name"),
                    rs.getString("encrypted_content"),
                    rs.getString("message_type"),
                    rs.getString("file_name"),
                    rs.getTimestamp("sent_at")
                });
            }
        } catch (SQLException e) {
            log.error("Lỗi lấy messages: {}", e.getMessage());
        }
        return results;
    }

    /**
     * Xoá lịch sử chat theo phòng (admin).
     */
    public static boolean clearRoomHistory(int roomId) {
        String sql = "DELETE FROM messages WHERE room_id = ?";
        try (Connection conn = DatabaseConnection.getConnection();
             PreparedStatement ps = conn.prepareStatement(sql)) {
            ps.setInt(1, roomId);
            ps.executeUpdate();
            return true;
        } catch (SQLException e) {
            log.error("Lỗi xoá lịch sử: {}", e.getMessage());
            return false;
        }
    }
}
