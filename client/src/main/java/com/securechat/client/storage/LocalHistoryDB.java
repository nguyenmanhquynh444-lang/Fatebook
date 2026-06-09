package com.securechat.client.storage;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.sql.*;
import java.util.ArrayList;
import java.util.List;

/**
 * LocalHistoryDB – Lưu lịch sử chat local bằng SQLite.
 *
 * Database được lưu tại: {user.home}/.securechat/history.db
 * Mỗi user có file DB riêng (theo username).
 */
public class LocalHistoryDB {

    private static final Logger log = LoggerFactory.getLogger(LocalHistoryDB.class);

    private static final String DB_DIR    = System.getProperty("user.home") + "/.securechat/";
    private String dbPath;
    private Connection connection;

    // ────────────────────────────────────────────────────────────

    public LocalHistoryDB(String username) {
        this.dbPath = DB_DIR + username + "_history.db";
    }

    /**
     * Khởi tạo kết nối và tạo bảng nếu chưa có.
     */
    public void init() {
        try {
            // Tạo thư mục nếu chưa có
            java.io.File dir = new java.io.File(DB_DIR);
            if (!dir.exists()) dir.mkdirs();

            Class.forName("org.sqlite.JDBC");
            connection = DriverManager.getConnection("jdbc:sqlite:" + dbPath);
            createTables();
            log.info("SQLite DB khởi tạo thành công: {}", dbPath);
        } catch (Exception e) {
            log.error("Lỗi khởi tạo SQLite: {}", e.getMessage(), e);
        }
    }

    private void createTables() throws SQLException {
        String sql = """
            CREATE TABLE IF NOT EXISTS local_messages (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                room_id      INTEGER NOT NULL,
                sender_id    INTEGER NOT NULL,
                sender_name  TEXT    NOT NULL,
                content      TEXT    NOT NULL,  -- plaintext (sau khi giải mã AES)
                message_type TEXT    DEFAULT 'TEXT',
                file_name    TEXT,
                timestamp    INTEGER NOT NULL,  -- epoch milliseconds
                is_sent      INTEGER DEFAULT 1  -- 1=sent by me, 0=received
            );
            CREATE INDEX IF NOT EXISTS idx_room_time
                ON local_messages(room_id, timestamp);
            """;
        try (Statement stmt = connection.createStatement()) {
            stmt.executeUpdate(sql);
        }
    }

    // ────────────────────────────────────────────────────────────
    // CRUD
    // ────────────────────────────────────────────────────────────

    /**
     * Lưu tin nhắn vào local history.
     */
    public void saveMessage(int roomId, int senderId, String senderName,
                             String content, String type, String fileName,
                             long timestamp, boolean isSent) {
        String sql = "INSERT INTO local_messages " +
                     "(room_id, sender_id, sender_name, content, message_type, file_name, timestamp, is_sent) " +
                     "VALUES (?, ?, ?, ?, ?, ?, ?, ?)";
        try (PreparedStatement ps = connection.prepareStatement(sql)) {
            ps.setInt(1, roomId);
            ps.setInt(2, senderId);
            ps.setString(3, senderName);
            ps.setString(4, content);
            ps.setString(5, type);
            ps.setString(6, fileName);
            ps.setLong(7, timestamp);
            ps.setInt(8, isSent ? 1 : 0);
            ps.executeUpdate();
        } catch (SQLException e) {
            log.error("Lỗi lưu local message: {}", e.getMessage());
        }
    }

    /**
     * Lấy lịch sử chat của một phòng (50 tin gần nhất).
     * @return danh sách [roomId, senderId, senderName, content, type, fileName, timestamp, isSent]
     */
    public List<Object[]> getHistory(int roomId, int limit) {
        String sql = "SELECT room_id, sender_id, sender_name, content, message_type, " +
                     "       file_name, timestamp, is_sent " +
                     "FROM local_messages WHERE room_id = ? " +
                     "ORDER BY timestamp DESC LIMIT ?";
        List<Object[]> results = new ArrayList<>();

        try (PreparedStatement ps = connection.prepareStatement(sql)) {
            ps.setInt(1, roomId);
            ps.setInt(2, limit);
            ResultSet rs = ps.executeQuery();

            while (rs.next()) {
                results.add(0, new Object[]{  // insert at 0 để reverse order
                    rs.getInt("room_id"),
                    rs.getInt("sender_id"),
                    rs.getString("sender_name"),
                    rs.getString("content"),
                    rs.getString("message_type"),
                    rs.getString("file_name"),
                    rs.getLong("timestamp"),
                    rs.getInt("is_sent") == 1
                });
            }
        } catch (SQLException e) {
            log.error("Lỗi lấy local history: {}", e.getMessage());
        }
        return results;
    }

    /**
     * Tìm kiếm tin nhắn theo từ khoá.
     */
    public List<Object[]> searchMessages(String keyword) {
        String sql = "SELECT room_id, sender_name, content, timestamp " +
                     "FROM local_messages WHERE content LIKE ? " +
                     "ORDER BY timestamp DESC LIMIT 50";
        List<Object[]> results = new ArrayList<>();

        try (PreparedStatement ps = connection.prepareStatement(sql)) {
            ps.setString(1, "%" + keyword + "%");
            ResultSet rs = ps.executeQuery();
            while (rs.next()) {
                results.add(new Object[]{
                    rs.getInt("room_id"),
                    rs.getString("sender_name"),
                    rs.getString("content"),
                    rs.getLong("timestamp")
                });
            }
        } catch (SQLException e) {
            log.error("Lỗi tìm kiếm: {}", e.getMessage());
        }
        return results;
    }

    /**
     * Xoá toàn bộ lịch sử của một phòng.
     */
    public void clearRoomHistory(int roomId) {
        String sql = "DELETE FROM local_messages WHERE room_id = ?";
        try (PreparedStatement ps = connection.prepareStatement(sql)) {
            ps.setInt(1, roomId);
            int deleted = ps.executeUpdate();
            log.info("Đã xoá {} tin nhắn local của room {}", deleted, roomId);
        } catch (SQLException e) {
            log.error("Lỗi xoá lịch sử: {}", e.getMessage());
        }
    }

    public void close() {
        try {
            if (connection != null && !connection.isClosed()) connection.close();
        } catch (SQLException e) {
            log.error("Lỗi đóng DB: {}", e.getMessage());
        }
    }
}
