-- =============================================================
-- Secure Chat System - MySQL Schema
-- XAMPP MySQL 8.0
-- =============================================================

CREATE DATABASE IF NOT EXISTS secure_chat CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE secure_chat;

-- ---------------------------------------------------------------
-- Bảng người dùng
-- ---------------------------------------------------------------
CREATE TABLE IF NOT EXISTS users (
    id           INT          PRIMARY KEY AUTO_INCREMENT,
    username     VARCHAR(50)  UNIQUE NOT NULL,
    password     VARCHAR(255) NOT NULL,          -- BCrypt hash
    display_name VARCHAR(100) NOT NULL,
    public_key   TEXT         NOT NULL,          -- RSA Public Key (Base64 DER)
    status       ENUM('ONLINE','OFFLINE','AWAY') DEFAULT 'OFFLINE',
    created_at   TIMESTAMP    DEFAULT CURRENT_TIMESTAMP,
    is_active    BOOLEAN      DEFAULT TRUE
) ENGINE=InnoDB;

-- ---------------------------------------------------------------
-- Bảng phòng chat
-- ---------------------------------------------------------------
CREATE TABLE IF NOT EXISTS chat_rooms (
    id          INT          PRIMARY KEY AUTO_INCREMENT,
    room_name   VARCHAR(100),
    room_type   ENUM('PRIVATE','GROUP') DEFAULT 'PRIVATE',
    created_by  INT,
    created_at  TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL
) ENGINE=InnoDB;

-- ---------------------------------------------------------------
-- Bảng thành viên phòng chat
-- ---------------------------------------------------------------
CREATE TABLE IF NOT EXISTS room_members (
    room_id    INT,
    user_id    INT,
    joined_at  TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (room_id, user_id),
    FOREIGN KEY (room_id) REFERENCES chat_rooms(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id)  ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------------
-- Bảng lịch sử tin nhắn (lưu bản đã mã hoá AES trên server)
-- ---------------------------------------------------------------
CREATE TABLE IF NOT EXISTS messages (
    id                INT          PRIMARY KEY AUTO_INCREMENT,
    room_id           INT          NOT NULL,
    sender_id         INT          NOT NULL,
    encrypted_content LONGTEXT     NOT NULL,     -- AES-GCM encrypted, Base64
    iv                VARCHAR(64)  NOT NULL,     -- IV (Base64) dùng để decrypt
    message_type      ENUM('TEXT','FILE','SYSTEM') DEFAULT 'TEXT',
    file_name         VARCHAR(512),              -- nếu là file
    file_size         BIGINT       DEFAULT 0,
    sent_at           TIMESTAMP    DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (room_id)   REFERENCES chat_rooms(id) ON DELETE CASCADE,
    FOREIGN KEY (sender_id) REFERENCES users(id)
) ENGINE=InnoDB;

-- ---------------------------------------------------------------
-- Bảng phiên đăng nhập (Session Token)
-- ---------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sessions (
    id         INT          PRIMARY KEY AUTO_INCREMENT,
    user_id    INT          UNIQUE NOT NULL,
    token      VARCHAR(512) NOT NULL,
    created_at TIMESTAMP    DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMP    NOT NULL,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------------
-- Tài khoản mẫu (password = "admin123" BCrypt)
-- ---------------------------------------------------------------
-- Lưu ý: public_key sẽ được cập nhật sau khi client đăng nhập lần đầu
INSERT IGNORE INTO users (username, password, display_name, public_key) VALUES
('admin',  '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj3bp.HQrn2e', 'Administrator', 'PENDING'),
('kemchui',  '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj3bp.HQrn2e', 'kemchui',  'PENDING'),
('shinichi',    '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj3bp.HQrn2e', 'shinichi',      'PENDING'),
('Quynh','$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj3bp.HQrn2e', 'Quynh',    'PENDING');
-- password cho tất cả tài khoản mẫu là: "admin123"
