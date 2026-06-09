package com.securechat.server.dao;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.InputStream;
import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.SQLException;
import java.util.Properties;

/**
 * DatabaseConnection – Quản lý kết nối JDBC tới MySQL (XAMPP).
 *
 * Đọc cấu hình từ src/main/resources/db.properties
 */
public class DatabaseConnection {

    private static final Logger log = LoggerFactory.getLogger(DatabaseConnection.class);

    private static String url;
    private static String username;
    private static String password;

    static {
        try (InputStream is = DatabaseConnection.class
                .getResourceAsStream("/db.properties")) {
            if (is == null) {
                throw new RuntimeException("Không tìm thấy db.properties!");
            }
            Properties props = new Properties();
            props.load(is);
            url      = props.getProperty("db.url");
            username = props.getProperty("db.username");
            password = props.getProperty("db.password");
            // Đăng ký driver
            Class.forName("com.mysql.cj.jdbc.Driver");
            log.info("Database config loaded: {}", url);
        } catch (Exception e) {
            throw new RuntimeException("Lỗi khởi tạo DB config: " + e.getMessage(), e);
        }
    }

    /**
     * Lấy một Connection mới từ DriverManager.
     * Lưu ý: Đây là simple connection (không dùng pool).
     * Trong production, hãy dùng HikariCP hoặc c3p0.
     */
    public static Connection getConnection() throws SQLException {
        return DriverManager.getConnection(url, username, password);
    }

    /**
     * Kiểm tra kết nối khi khởi động server.
     */
    public static void testConnection() {
        try (Connection conn = getConnection()) {
            log.info("✅ Kết nối MySQL thành công! URL={}", url);
        } catch (SQLException e) {
            log.error("❌ Không thể kết nối MySQL: {}", e.getMessage());
            log.error("Hãy đảm bảo XAMPP đang chạy và database 'secure_chat' đã được tạo.");
            System.exit(1);
        }
    }
}
