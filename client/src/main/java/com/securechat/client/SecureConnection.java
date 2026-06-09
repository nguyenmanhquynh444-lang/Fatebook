package com.securechat.client;

import com.google.gson.Gson;
import com.securechat.common.crypto.AESUtil;
import com.securechat.common.crypto.RSAUtil;
import com.securechat.common.dto.MessageDTO;
import com.securechat.common.dto.UserDTO;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.crypto.SecretKey;
import javax.net.ssl.*;
import java.io.*;
import java.security.KeyPair;
import java.security.KeyStore;
import java.util.Base64;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.function.Consumer;

/**
 * SecureConnection – Quản lý kết nối SSL/TLS tới server và mã hoá AES.
 *
 * Luồng kết nối:
 * 1. Load truststore (CA cert) → Verify server certificate
 * 2. Thiết lập SSLSocket → server:8443
 * 3. Gửi thông tin đăng nhập
 * 4. Nhận RSA Public Key của server
 * 5. Sinh AES Key 256-bit → mã hoá bằng RSA → gửi lên server
 * 6. Bắt đầu vòng lặp nhận/gửi tin nhắn mã hoá AES
 */
public class SecureConnection {

    private static final Logger log = LoggerFactory.getLogger(SecureConnection.class);

    private static final String TRUSTSTORE_PATH = "/client.truststore";
    private static final String TRUSTSTORE_PASS = "trust_password_2024";

    private final String  serverHost;
    private final int     serverPort;
    private final Gson    gson = new Gson();

    // Trạng thái kết nối
    private SSLSocket     socket;
    private PrintWriter   out;
    private BufferedReader in;

    private SecretKey aesKey;       // Khoá AES phiên chat
    private KeyPair   clientKeyPair; // RSA keypair của client
    private UserDTO   loggedInUser;

    // Callbacks
    private Consumer<MessageDTO> onMessageReceived;
    private Consumer<String>     onConnectionError;

    // Queue để gửi tin nhắn (thread-safe)
    private final BlockingQueue<MessageDTO> sendQueue = new LinkedBlockingQueue<>();

    private volatile boolean connected = false;

    // ────────────────────────────────────────────────────────────

    public SecureConnection(String serverHost, int serverPort) {
        this.serverHost = serverHost;
        this.serverPort = serverPort;
    }

    // ────────────────────────────────────────────────────────────
    // Kết nối và đăng nhập
    // ────────────────────────────────────────────────────────────

    /**
     * Kết nối tới server và thực hiện login + key exchange.
     * @return UserDTO nếu thành công, null nếu thất bại
     */
    public UserDTO connect(String username, String password) throws Exception {
        // Bước 1: Tạo SSLSocket với truststore
        SSLSocket sslSocket = createSSLSocket();
        this.socket = sslSocket;
        sslSocket.startHandshake();
        log.info("SSL Handshake thành công với {}", sslSocket.getInetAddress());

        // Bước 2: Khởi tạo streams
        this.out = new PrintWriter(new BufferedWriter(
                new OutputStreamWriter(sslSocket.getOutputStream())), true);
        this.in  = new BufferedReader(
                new InputStreamReader(sslSocket.getInputStream()));

        // Bước 3: Sinh RSA Keypair cho client
        this.clientKeyPair = RSAUtil.generateKeyPair();
        log.info("Đã sinh RSA Keypair cho client");

        // Bước 4: Gửi thông tin đăng nhập
        MessageDTO loginMsg = MessageDTO.loginRequest(username, password);
        sendRaw(loginMsg);

        // Bước 5: Nhận phản hồi đăng nhập + RSA Public Key của server
        String responseJson = in.readLine();
        MessageDTO loginResponse = gson.fromJson(responseJson, MessageDTO.class);

        if (loginResponse.getType() != MessageDTO.Type.LOGIN_RESPONSE) {
            throw new IOException("Server không phản hồi LOGIN_RESPONSE");
        }

        String content = loginResponse.getPlainContent();
        if (content.startsWith("FAIL:")) {
            log.warn("Đăng nhập thất bại: {}", content.substring(5));
            disconnect();
            return null;
        }

        // Lấy server RSA Public Key
        String serverPublicKeyBase64 = loginResponse.getEncryptedContent();
        java.security.PublicKey serverPublicKey = RSAUtil.base64ToPublicKey(serverPublicKeyBase64);

        // Điền thông tin user
        this.loggedInUser = new UserDTO();
        loggedInUser.setId(loginResponse.getSenderId());
        loggedInUser.setUsername(username);

        log.info("Đăng nhập thành công! UserId={}", loggedInUser.getId());

        // Bước 6: Gửi RSA Public Key của client lên server
        MessageDTO clientKeyMsg = new MessageDTO(MessageDTO.Type.KEY_EXCHANGE);
        clientKeyMsg.setPlainContent(RSAUtil.publicKeyToBase64(clientKeyPair.getPublic()));
        sendRaw(clientKeyMsg);

        // Bước 7: Sinh AES Key và mã hoá bằng RSA Public Key của server
        this.aesKey = AESUtil.generateKey();
        byte[] encryptedAesKey = RSAUtil.encrypt(aesKey.getEncoded(), serverPublicKey);

        MessageDTO aesKeyMsg = new MessageDTO(MessageDTO.Type.KEY_EXCHANGE);
        aesKeyMsg.setPlainContent(Base64.getEncoder().encodeToString(encryptedAesKey));
        sendRaw(aesKeyMsg);

        log.info("Key exchange hoàn thành! AES-256-GCM session key đã thiết lập.");

        // Bước 8: Khởi động vòng lặp nhận tin nhắn
        this.connected = true;
        startReceiveLoop();
        startSendLoop();

        return loggedInUser;
    }

    // ────────────────────────────────────────────────────────────
    // Gửi tin nhắn
    // ────────────────────────────────────────────────────────────

    /**
     * Gửi tin nhắn văn bản (sẽ được mã hoá AES tự động).
     */
    public void sendText(int roomId, String plaintext) throws Exception {
        if (!connected || aesKey == null) throw new IllegalStateException("Chưa kết nối!");

        String encrypted = AESUtil.encrypt(plaintext, aesKey);
        MessageDTO msg = MessageDTO.textMessage(
                loggedInUser.getId(), loggedInUser.getUsername(), roomId, encrypted);

        sendQueue.offer(msg);
    }

    /**
     * Gửi file (bytes đã đọc từ disk).
     */
    public void sendFile(int roomId, String fileName, byte[] fileBytes) throws Exception {
        if (!connected || aesKey == null) throw new IllegalStateException("Chưa kết nối!");

        byte[] encryptedBytes = AESUtil.encryptBytes(fileBytes, aesKey);
        String base64Encrypted = Base64.getEncoder().encodeToString(encryptedBytes);

        MessageDTO msg = MessageDTO.fileMessage(
                loggedInUser.getId(), loggedInUser.getUsername(),
                roomId, base64Encrypted, fileName, fileBytes.length);

        sendQueue.offer(msg);
    }

    /**
     * Giải mã tin nhắn nhận được (dùng AES key của phiên).
     */
    public String decryptMessage(String encryptedContent) {
        try {
            return AESUtil.decrypt(encryptedContent, aesKey);
        } catch (Exception e) {
            log.error("Lỗi giải mã tin nhắn: {}", e.getMessage());
            return "[Không thể giải mã]";
        }
    }

    /**
     * Giải mã file nhận được.
     */
    public byte[] decryptFile(String base64Encrypted) {
        try {
            byte[] encryptedBytes = Base64.getDecoder().decode(base64Encrypted);
            return AESUtil.decryptBytes(encryptedBytes, aesKey);
        } catch (Exception e) {
            log.error("Lỗi giải mã file: {}", e.getMessage());
            return null;
        }
    }

    // ────────────────────────────────────────────────────────────
    // Vòng lặp nhận / gửi
    // ────────────────────────────────────────────────────────────

    private void startReceiveLoop() {
        Thread receiveThread = new Thread(() -> {
            try {
                String line;
                while (connected && (line = in.readLine()) != null) {
                    try {
                        MessageDTO msg = gson.fromJson(line, MessageDTO.class);
                        if (onMessageReceived != null) {
                            onMessageReceived.accept(msg);
                        }
                    } catch (Exception e) {
                        log.error("Lỗi parse tin nhắn: {}", e.getMessage());
                    }
                }
            } catch (IOException e) {
                if (connected) {
                    log.error("Mất kết nối server: {}", e.getMessage());
                    if (onConnectionError != null) {
                        onConnectionError.accept("Mất kết nối server: " + e.getMessage());
                    }
                }
            } finally {
                connected = false;
            }
        }, "ReceiveThread");
        receiveThread.setDaemon(true);
        receiveThread.start();
    }

    private void startSendLoop() {
        Thread sendThread = new Thread(() -> {
            while (connected) {
                try {
                    MessageDTO msg = sendQueue.take();
                    sendRaw(msg);
                } catch (InterruptedException e) {
                    Thread.currentThread().interrupt();
                    break;
                }
            }
        }, "SendThread");
        sendThread.setDaemon(true);
        sendThread.start();
    }

    private synchronized void sendRaw(MessageDTO msg) {
        if (out != null) {
            out.println(gson.toJson(msg));
        }
    }

    // ────────────────────────────────────────────────────────────
    // Tạo SSLSocket với truststore
    // ────────────────────────────────────────────────────────────

    private SSLSocket createSSLSocket() throws Exception {
        KeyStore trustStore = KeyStore.getInstance("JKS");
        try (InputStream is = SecureConnection.class.getResourceAsStream(TRUSTSTORE_PATH)) {
            if (is == null) {
                throw new RuntimeException(
                    "Không tìm thấy client.truststore! Hãy chạy certs/generate_certs.bat trước.");
            }
            trustStore.load(is, TRUSTSTORE_PASS.toCharArray());
        }

        TrustManagerFactory tmf = TrustManagerFactory.getInstance(
                TrustManagerFactory.getDefaultAlgorithm());
        tmf.init(trustStore);

        SSLContext sslContext = SSLContext.getInstance("TLS");
        sslContext.init(null, tmf.getTrustManagers(), null);

        SSLSocketFactory ssf = sslContext.getSocketFactory();
        SSLSocket socket = (SSLSocket) ssf.createSocket(serverHost, serverPort);
        socket.setEnabledProtocols(new String[]{"TLSv1.3", "TLSv1.2"});

        return socket;
    }

    // ────────────────────────────────────────────────────────────
    // Callbacks & Utils
    // ────────────────────────────────────────────────────────────

    public void setOnMessageReceived(Consumer<MessageDTO> callback) {
        this.onMessageReceived = callback;
    }

    public void setOnConnectionError(Consumer<String> callback) {
        this.onConnectionError = callback;
    }

    public void disconnect() {
        connected = false;
        try {
            if (socket != null && !socket.isClosed()) socket.close();
        } catch (IOException e) {
            log.error("Lỗi đóng kết nối: {}", e.getMessage());
        }
    }

    public boolean isConnected()     { return connected; }
    public UserDTO getLoggedInUser() { return loggedInUser; }
    public SecretKey getAesKey()     { return aesKey; }
}
