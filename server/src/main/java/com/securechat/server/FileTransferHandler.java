package com.securechat.server;

import com.google.gson.Gson;
import com.securechat.common.dto.MessageDTO;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.net.ssl.*;
import java.io.*;
import java.security.KeyStore;

/**
 * FileTransferHandler – Xử lý truyền file qua port riêng (8444).
 *
 * Luồng:
 * 1. Client gửi FileDTO: roomId, fileName, fileSize, encryptedData (Base64)
 * 2. Server nhận, route đến các thành viên trong room
 * 3. Receiver tải file (đã mã hoá) → giải mã bằng AES local key
 *
 * File lớn được chia thành chunks 64KB.
 */
public class FileTransferHandler implements Runnable {

    private static final Logger log = LoggerFactory.getLogger(FileTransferHandler.class);
    private static final int    CHUNK_SIZE   = 64 * 1024; // 64 KB
    private static final String KEYSTORE_PASS = "server_password_2024";

    private final int         port;
    private final RoomManager roomManager;

    private static FileTransferHandler instance;

    public FileTransferHandler(int port) {
        this.port        = port;
        this.roomManager = null; // Sẽ được inject sau
        instance         = this;
    }

    public FileTransferHandler(int port, RoomManager roomManager) {
        this.port        = port;
        this.roomManager = roomManager;
        instance         = this;
    }

    @Override
    public void run() {
        try {
            SSLServerSocket serverSocket = createSSLServerSocket();
            log.info("FileTransferHandler lắng nghe port {}", port);

            while (true) {
                SSLSocket clientSocket = (SSLSocket) serverSocket.accept();
                new Thread(() -> handleFileClient(clientSocket)).start();
            }
        } catch (Exception e) {
            log.error("FileTransferHandler lỗi: {}", e.getMessage(), e);
        }
    }

    private void handleFileClient(SSLSocket socket) {
        try (DataInputStream dis = new DataInputStream(
                     new BufferedInputStream(socket.getInputStream()));
             DataOutputStream dos = new DataOutputStream(socket.getOutputStream())) {

            // Đọc metadata
            int    roomId   = dis.readInt();
            int    senderId = dis.readInt();
            String fileName = dis.readUTF();
            long   fileSize = dis.readLong();

            log.info("Nhận file '{}' ({} bytes) từ userId={} → room={}",
                    fileName, fileSize, senderId, roomId);

            // Đọc toàn bộ dữ liệu mã hoá
            byte[] encryptedData = dis.readAllBytes();

            // Tạo MessageDTO loại FILE để broadcast
            MessageDTO fileMsg = MessageDTO.fileMessage(
                    senderId, "unknown", roomId,
                    java.util.Base64.getEncoder().encodeToString(encryptedData),
                    fileName, fileSize);

            // Broadcast đến các thành viên (nếu roomManager available)
            if (roomManager != null) {
                roomManager.broadcastToRoom(roomId, fileMsg, senderId);
            }

            // Phản hồi thành công
            dos.writeUTF("OK");
            dos.flush();

        } catch (Exception e) {
            log.error("Lỗi xử lý file: {}", e.getMessage());
        } finally {
            try { socket.close(); } catch (IOException ignored) {}
        }
    }

    private SSLServerSocket createSSLServerSocket() throws Exception {
        KeyStore ks = KeyStore.getInstance("JKS");
        try (InputStream is = FileTransferHandler.class.getResourceAsStream("/server.keystore")) {
            ks.load(is, KEYSTORE_PASS.toCharArray());
        }
        KeyManagerFactory kmf = KeyManagerFactory.getInstance("SunX509");
        kmf.init(ks, KEYSTORE_PASS.toCharArray());

        SSLContext ctx = SSLContext.getInstance("TLS");
        ctx.init(kmf.getKeyManagers(), null, null);

        SSLServerSocketFactory ssf = ctx.getServerSocketFactory();
        return (SSLServerSocket) ssf.createServerSocket(port);
    }
}
