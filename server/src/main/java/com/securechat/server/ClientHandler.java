package com.securechat.server;

import com.google.gson.Gson;
import com.securechat.common.crypto.AESUtil;
import com.securechat.common.crypto.RSAUtil;
import com.securechat.common.dto.MessageDTO;
import com.securechat.common.dto.UserDTO;
import com.securechat.server.dao.MessageDAO;
import com.securechat.server.dao.UserDAO;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.crypto.SecretKey;
import javax.net.ssl.SSLSocket;
import java.io.*;
import java.security.KeyPair;
import java.security.PublicKey;
import java.util.Base64;
import java.util.List;

/**
 * ClientHandler – Xử lý một client trong một Thread riêng biệt.
 *
 * Luồng xử lý:
 * 1. SSL Handshake (tự động bởi SSLSocket)
 * 2. Nhận thông tin đăng nhập → xác thực với DB
 * 3. Trao đổi khoá RSA → AES Key
 * 4. Vòng lặp nhận/gửi tin nhắn mã hoá AES
 */
public class ClientHandler implements Runnable {

    private static final Logger log = LoggerFactory.getLogger(ClientHandler.class);

    private final SSLSocket    socket;
    private final RoomManager  roomManager;
    private final Gson         gson = new Gson();

    // Trạng thái client
    private UserDTO    currentUser;
    private SecretKey  aesKey;          // Khoá AES cho phiên này
    private KeyPair    serverKeyPair;   // RSA key pair của server (mỗi phiên tạo mới)

    private PrintWriter   out;
    private BufferedReader in;

    private volatile boolean connected = true;

    // ────────────────────────────────────────────────────────────

    public ClientHandler(SSLSocket socket, RoomManager roomManager) {
        this.socket      = socket;
        this.roomManager = roomManager;
    }

    @Override
    public void run() {
        try {
            setupStreams();
            performHandshake();
            messageLoop();
        } catch (EOFException | java.net.SocketException e) {
            log.info("Client ngắt kết nối: {}",
                    currentUser != null ? currentUser.getUsername() : "unknown");
        } catch (Exception e) {
            log.error("Lỗi ClientHandler: {}", e.getMessage(), e);
        } finally {
            cleanup();
        }
    }

    // ────────────────────────────────────────────────────────────
    // Khởi tạo streams
    // ────────────────────────────────────────────────────────────

    private void setupStreams() throws IOException {
        out = new PrintWriter(new BufferedWriter(
                new OutputStreamWriter(socket.getOutputStream())), true);
        in  = new BufferedReader(
                new InputStreamReader(socket.getInputStream()));
    }

    // ────────────────────────────────────────────────────────────
    // Handshake: Login + RSA Key Exchange
    // ────────────────────────────────────────────────────────────

    private void performHandshake() throws Exception {
        // ── Bước 1: Đăng nhập ──────────────────────────────────
        String loginJson = in.readLine();
        MessageDTO loginMsg = gson.fromJson(loginJson, MessageDTO.class);

        if (loginMsg.getType() != MessageDTO.Type.LOGIN) {
            sendSystemMessage("ERR:Yêu cầu đăng nhập trước.");
            throw new IOException("Client không gửi LOGIN message");
        }

        // Parse username:password
        String[] creds = loginMsg.getPlainContent().split(":", 2);
        String username = creds[0];
        String password = creds.length > 1 ? creds[1] : "";

        // Xác thực với DB
        UserDTO user = UserDAO.authenticate(username, password);
        if (user == null) {
            sendLoginResponse(false, "Sai tài khoản hoặc mật khẩu!", null);
            throw new IOException("Đăng nhập thất bại: " + username);
        }

        this.currentUser = user;
        log.info("Đăng nhập thành công: {} (id={})", username, user.getId());

        // ── Bước 2: Sinh RSA Keypair cho phiên này ─────────────
        this.serverKeyPair = RSAUtil.generateKeyPair();
        String serverPublicKeyBase64 = RSAUtil.publicKeyToBase64(serverKeyPair.getPublic());

        // Cập nhật public key của client vào DB (client sẽ gửi trong message tiếp theo)
        // Trả về thông tin đăng nhập thành công + RSA Public Key
        sendLoginResponse(true, "Đăng nhập thành công!", serverPublicKeyBase64);

        // ── Bước 3: Nhận Client RSA Public Key ─────────────────
        String clientKeyJson = in.readLine();
        MessageDTO clientKeyMsg = gson.fromJson(clientKeyJson, MessageDTO.class);
        if (clientKeyMsg.getType() == MessageDTO.Type.KEY_EXCHANGE) {
            String clientPubKeyBase64 = clientKeyMsg.getPlainContent();
            UserDAO.updatePublicKey(user.getId(), clientPubKeyBase64);
            currentUser.setPublicKey(clientPubKeyBase64);
        }

        // ── Bước 4: Nhận AES Key (đã mã hoá bằng RSA) ─────────
        String keyExchangeJson = in.readLine();
        MessageDTO keyMsg = gson.fromJson(keyExchangeJson, MessageDTO.class);

        if (keyMsg.getType() != MessageDTO.Type.KEY_EXCHANGE) {
            throw new IOException("Không nhận được AES Key");
        }

        // Giải mã AES Key bằng RSA Private Key của server
        byte[] encryptedAesKey = Base64.getDecoder().decode(keyMsg.getPlainContent());
        byte[] aesKeyBytes     = RSAUtil.decrypt(encryptedAesKey, serverKeyPair.getPrivate());
        this.aesKey = AESUtil.bytesToKey(aesKeyBytes);

        log.info("Key exchange hoàn thành với: {}", username);

        // ── Bước 5: Gửi danh sách user và room cho client ──────
        roomManager.registerClient(user.getId(), this);
        UserDAO.updateStatus(user.getId(), "ONLINE");

        sendUserList();
        broadcastUserJoined(user);
    }

    // ────────────────────────────────────────────────────────────
    // Vòng lặp nhận/gửi tin nhắn
    // ────────────────────────────────────────────────────────────

    private void messageLoop() throws Exception {
        String line;
        while (connected && (line = in.readLine()) != null) {
            try {
                MessageDTO msg = gson.fromJson(line, MessageDTO.class);
                handleMessage(msg);
            } catch (Exception e) {
                log.error("Lỗi xử lý tin nhắn từ {}: {}", currentUser.getUsername(), e.getMessage());
            }
        }
    }

    private void handleMessage(MessageDTO msg) throws Exception {
        switch (msg.getType()) {
            case TEXT -> handleTextMessage(msg);
            case FILE -> handleFileMessage(msg);
            case JOIN_ROOM -> handleJoinRoom(msg);
            case LEAVE_ROOM -> handleLeaveRoom(msg);
            case TYPING -> roomManager.broadcastToRoom(msg.getRoomId(), msg, currentUser.getId());
            default -> log.warn("Loại tin nhắn không xác định: {}", msg.getType());
        }
    }

    private void handleTextMessage(MessageDTO msg) throws Exception {
        // Giải mã để kiểm tra nội dung (tùy chọn - server có thể không giải mã)
        // String plaintext = AESUtil.decrypt(msg.getEncryptedContent(), aesKey);

        // Lưu vào DB (bản mã hoá)
        MessageDAO.saveMessage(msg.getRoomId(), currentUser.getId(),
                msg.getEncryptedContent(), "TEXT", null, 0);

        // Phát tán đến tất cả thành viên trong room
        msg.setSenderId(currentUser.getId());
        msg.setSenderUsername(currentUser.getUsername());
        roomManager.broadcastToRoom(msg.getRoomId(), msg, currentUser.getId());
    }

    private void handleFileMessage(MessageDTO msg) throws Exception {
        // Lưu thông tin file vào DB
        MessageDAO.saveMessage(msg.getRoomId(), currentUser.getId(),
                msg.getEncryptedContent(), "FILE", msg.getFileName(), msg.getFileSize());

        msg.setSenderId(currentUser.getId());
        msg.setSenderUsername(currentUser.getUsername());
        roomManager.broadcastToRoom(msg.getRoomId(), msg, currentUser.getId());
    }

    private void handleJoinRoom(MessageDTO msg) {
        roomManager.joinRoom(msg.getRoomId(), currentUser.getId(), this);
        log.info("{} vào phòng {}", currentUser.getUsername(), msg.getRoomId());
    }

    private void handleLeaveRoom(MessageDTO msg) {
        roomManager.leaveRoom(msg.getRoomId(), currentUser.getId());
        log.info("{} rời phòng {}", currentUser.getUsername(), msg.getRoomId());
    }

    // ────────────────────────────────────────────────────────────
    // Gửi tin nhắn đến client này
    // ────────────────────────────────────────────────────────────

    /** Gửi một MessageDTO (đã JSON hoá) đến client này. */
    public synchronized void sendMessage(MessageDTO msg) {
        if (out != null && !socket.isClosed()) {
            out.println(gson.toJson(msg));
        }
    }

    private void sendLoginResponse(boolean success, String message, String serverPublicKey) {
        MessageDTO response = new MessageDTO(MessageDTO.Type.LOGIN_RESPONSE);
        response.setPlainContent(success ? "OK:" + message : "FAIL:" + message);
        if (serverPublicKey != null) {
            // Đính kèm RSA Public Key của server
            response.setEncryptedContent(serverPublicKey);
        }
        if (success && currentUser != null) {
            // Gửi thông tin user
            response.setSenderId(currentUser.getId());
            response.setSenderUsername(currentUser.getUsername());
        }
        sendMessage(response);
    }

    private void sendSystemMessage(String text) {
        sendMessage(MessageDTO.systemMessage(text));
    }

    private void sendUserList() {
        List<UserDTO> users = UserDAO.getAllActiveUsers();
        MessageDTO msg = new MessageDTO(MessageDTO.Type.USER_LIST);
        msg.setPlainContent(gson.toJson(users));
        sendMessage(msg);
    }

    private void broadcastUserJoined(UserDTO user) {
        MessageDTO msg = MessageDTO.systemMessage(user.getDisplayName() + " đã online!");
        msg.setSenderId(user.getId());
        msg.setSenderUsername(user.getUsername());
        roomManager.broadcastToAll(msg, user.getId());
    }

    // ────────────────────────────────────────────────────────────
    // Cleanup
    // ────────────────────────────────────────────────────────────

    private void cleanup() {
        connected = false;
        if (currentUser != null) {
            roomManager.removeClient(currentUser.getId());
            UserDAO.updateStatus(currentUser.getId(), "OFFLINE");

            // Thông báo user offline
            MessageDTO offlineMsg = MessageDTO.systemMessage(
                    currentUser.getDisplayName() + " đã offline.");
            offlineMsg.setSenderId(currentUser.getId());
            offlineMsg.setSenderUsername(currentUser.getUsername());
            roomManager.broadcastToAll(offlineMsg, currentUser.getId());
        }
        try {
            if (socket != null && !socket.isClosed()) socket.close();
        } catch (IOException e) {
            log.error("Lỗi đóng socket: {}", e.getMessage());
        }
    }

    // ── Getter ──────────────────────────────────────────────────
    public UserDTO getCurrentUser() { return currentUser; }
    public SecretKey getAesKey()    { return aesKey; }
}
