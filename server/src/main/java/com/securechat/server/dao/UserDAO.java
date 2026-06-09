package com.securechat.server.dao;

import com.securechat.common.dto.UserDTO;
import org.mindrot.jbcrypt.BCrypt;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.sql.*;
import java.util.ArrayList;
import java.util.List;

/**
 * UserDAO – CRUD operations cho bảng users trong MySQL.
 */
public class UserDAO {

    private static final Logger log = LoggerFactory.getLogger(UserDAO.class);

    // ────────────────────────────────────────────────────────────
    // Xác thực
    // ────────────────────────────────────────────────────────────

    /**
     * Xác thực tài khoản người dùng bằng BCrypt.
     * @return UserDTO nếu thành công, null nếu thất bại.
     */
    public static UserDTO authenticate(String username, String rawPassword) {
        String sql = "SELECT id, username, password, display_name, public_key, status " +
                     "FROM users WHERE username = ? AND is_active = TRUE";

        try (Connection conn = DatabaseConnection.getConnection();
             PreparedStatement ps = conn.prepareStatement(sql)) {

            ps.setString(1, username);
            ResultSet rs = ps.executeQuery();

            if (rs.next()) {
                String hashedPassword = rs.getString("password");
                if (BCrypt.checkpw(rawPassword, hashedPassword)) {
                    return mapToDTO(rs);
                }
            }
        } catch (SQLException e) {
            log.error("Lỗi xác thực user '{}': {}", username, e.getMessage());
        }
        return null;
    }

    // ────────────────────────────────────────────────────────────
    // Tạo tài khoản
    // ────────────────────────────────────────────────────────────

    /**
     * Tạo tài khoản mới (server-side, do admin tạo).
     * @param username    tên tài khoản
     * @param rawPassword mật khẩu gốc (sẽ được hash bằng BCrypt)
     * @param displayName tên hiển thị
     * @return id của user mới tạo, -1 nếu lỗi
     */
    public static int createUser(String username, String rawPassword, String displayName) {
        String hashedPwd = BCrypt.hashpw(rawPassword, BCrypt.gensalt(12));
        String sql = "INSERT INTO users (username, password, display_name, public_key) " +
                     "VALUES (?, ?, ?, 'PENDING')";

        try (Connection conn = DatabaseConnection.getConnection();
             PreparedStatement ps = conn.prepareStatement(sql, Statement.RETURN_GENERATED_KEYS)) {

            ps.setString(1, username);
            ps.setString(2, hashedPwd);
            ps.setString(3, displayName);
            ps.executeUpdate();

            ResultSet keys = ps.getGeneratedKeys();
            if (keys.next()) {
                int newId = keys.getInt(1);
                log.info("Tạo user mới: {} (id={})", username, newId);
                return newId;
            }
        } catch (SQLException e) {
            log.error("Lỗi tạo user '{}': {}", username, e.getMessage());
        }
        return -1;
    }

    // ────────────────────────────────────────────────────────────
    // Cập nhật
    // ────────────────────────────────────────────────────────────

    /** Cập nhật RSA Public Key của user sau lần đăng nhập đầu tiên. */
    public static void updatePublicKey(int userId, String publicKeyBase64) {
        String sql = "UPDATE users SET public_key = ? WHERE id = ?";
        try (Connection conn = DatabaseConnection.getConnection();
             PreparedStatement ps = conn.prepareStatement(sql)) {
            ps.setString(1, publicKeyBase64);
            ps.setInt(2, userId);
            ps.executeUpdate();
        } catch (SQLException e) {
            log.error("Lỗi cập nhật public key user {}: {}", userId, e.getMessage());
        }
    }

    /** Cập nhật trạng thái online/offline. */
    public static void updateStatus(int userId, String status) {
        String sql = "UPDATE users SET status = ? WHERE id = ?";
        try (Connection conn = DatabaseConnection.getConnection();
             PreparedStatement ps = conn.prepareStatement(sql)) {
            ps.setString(1, status);
            ps.setInt(2, userId);
            ps.executeUpdate();
        } catch (SQLException e) {
            log.error("Lỗi cập nhật status user {}: {}", userId, e.getMessage());
        }
    }

    // ────────────────────────────────────────────────────────────
    // Query
    // ────────────────────────────────────────────────────────────

    /** Lấy tất cả user đang hoạt động (để hiển thị danh sách). */
    public static List<UserDTO> getAllActiveUsers() {
        String sql = "SELECT id, username, display_name, public_key, status " +
                     "FROM users WHERE is_active = TRUE ORDER BY display_name";
        List<UserDTO> users = new ArrayList<>();

        try (Connection conn = DatabaseConnection.getConnection();
             PreparedStatement ps = conn.prepareStatement(sql);
             ResultSet rs = ps.executeQuery()) {

            while (rs.next()) {
                users.add(mapToDTO(rs));
            }
        } catch (SQLException e) {
            log.error("Lỗi lấy danh sách users: {}", e.getMessage());
        }
        return users;
    }

    /** Lấy thông tin một user theo id. */
    public static UserDTO getUserById(int id) {
        String sql = "SELECT id, username, display_name, public_key, status " +
                     "FROM users WHERE id = ?";
        try (Connection conn = DatabaseConnection.getConnection();
             PreparedStatement ps = conn.prepareStatement(sql)) {
            ps.setInt(1, id);
            ResultSet rs = ps.executeQuery();
            if (rs.next()) return mapToDTO(rs);
        } catch (SQLException e) {
            log.error("Lỗi lấy user by id {}: {}", id, e.getMessage());
        }
        return null;
    }

    // ────────────────────────────────────────────────────────────
    // Helper
    // ────────────────────────────────────────────────────────────

    private static UserDTO mapToDTO(ResultSet rs) throws SQLException {
        return new UserDTO(
            rs.getInt("id"),
            rs.getString("username"),
            rs.getString("display_name"),
            rs.getString("public_key"),
            rs.getString("status")
        );
    }
}
