package com.securechat.server;

import com.securechat.server.dao.DatabaseConnection;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.net.ssl.*;
import java.io.InputStream;
import java.net.InetAddress;
import java.security.KeyStore;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Server – Entry point của Secure Chat Server.
 *
 * Khởi động SSLServerSocket trên port 8443 sử dụng:
 *   - server.keystore  (chứa private key + CA-signed certificate)
 *   - TLS 1.3
 *
 * Mỗi client được xử lý trong một Thread riêng (ThreadPool).
 */
public class Server {

    private static final Logger log = LoggerFactory.getLogger(Server.class);

    // ── Cấu hình ────────────────────────────────────────────────
    public static final int    CHAT_PORT    = 8443;
    public static final int    FILE_PORT    = 8444;
    public static final int    MAX_THREADS  = 50;

    private static final String KEYSTORE_PATH = "/server.keystore";
    private static final String KEYSTORE_PASS = "server_password_2024";

    private final RoomManager     roomManager;
    private final ExecutorService threadPool;

    private volatile boolean running = true;

    // ────────────────────────────────────────────────────────────

    public Server() {
        this.roomManager = new RoomManager();
        this.threadPool  = Executors.newFixedThreadPool(MAX_THREADS);
    }

    public static void main(String[] args) throws Exception {
        log.info("===========================================");
        log.info("   Secure Chat Server v1.0 – VHU LTM");
        log.info("===========================================");

        // Kiểm tra DB
        DatabaseConnection.testConnection();

        Server server = new Server();
        server.start();
    }

    public void start() throws Exception {
        // Khởi tạo SSLContext
        SSLServerSocket serverSocket = createSSLServerSocket();

        log.info("Server đang lắng nghe tại port {} (TLS 1.3)", CHAT_PORT);
        log.info("File transfer port: {}", FILE_PORT);
        log.info("Địa chỉ IP: {}", InetAddress.getLocalHost().getHostAddress());

        // Khởi động FileTransferHandler trong thread riêng
        threadPool.submit(new FileTransferHandler(FILE_PORT));

        // Vòng lặp chấp nhận kết nối
        Runtime.getRuntime().addShutdownHook(new Thread(() -> {
            log.info("Server đang dừng...");
            running = false;
            threadPool.shutdown();
        }));

        while (running) {
            try {
                SSLSocket clientSocket = (SSLSocket) serverSocket.accept();
                log.info("Client kết nối từ: {}", clientSocket.getInetAddress());

                // Ép dùng TLS 1.3
                clientSocket.setEnabledProtocols(new String[]{"TLSv1.3", "TLSv1.2"});

                // Tạo handler cho client mới
                ClientHandler handler = new ClientHandler(clientSocket, roomManager);
                threadPool.submit(handler);

            } catch (Exception e) {
                if (running) {
                    log.error("Lỗi chấp nhận kết nối: {}", e.getMessage());
                }
            }
        }
    }

    // ────────────────────────────────────────────────────────────
    // Tạo SSLServerSocket với server.keystore
    // ────────────────────────────────────────────────────────────

    private SSLServerSocket createSSLServerSocket() throws Exception {
        // Load keystore từ classpath
        KeyStore ks = KeyStore.getInstance("JKS");
        try (InputStream is = Server.class.getResourceAsStream(KEYSTORE_PATH)) {
            if (is == null) {
                throw new RuntimeException(
                    "Không tìm thấy server.keystore! Hãy chạy certs/generate_certs.bat trước.");
            }
            ks.load(is, KEYSTORE_PASS.toCharArray());
        }

        // Khởi tạo KeyManagerFactory
        KeyManagerFactory kmf = KeyManagerFactory.getInstance(
                KeyManagerFactory.getDefaultAlgorithm());
        kmf.init(ks, KEYSTORE_PASS.toCharArray());

        // Tạo SSLContext TLS
        SSLContext sslContext = SSLContext.getInstance("TLS");
        sslContext.init(kmf.getKeyManagers(), null, null);

        // Tạo server socket
        SSLServerSocketFactory ssf = sslContext.getServerSocketFactory();
        SSLServerSocket socket = (SSLServerSocket) ssf.createServerSocket(CHAT_PORT);
        socket.setNeedClientAuth(false);  // Server không yêu cầu cert từ client

        return socket;
    }
}
