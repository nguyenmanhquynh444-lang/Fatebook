using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using SecureChat.Server.Crypto;
using SecureChat.Server.DAO;

namespace SecureChat.Server
{
    public class Server
    {
        public const int CHAT_PORT = 8443;
        public const int FILE_PORT = 8444;

        private readonly RoomManager _roomManager;
        private FileTransferHandler? _fileTransferHandler;
        private TcpListener? _chatListener;
        private X509Certificate2? _serverCertificate;
        private volatile bool _running = true;

        public Server()
        {
            _roomManager = new RoomManager();
        }

        public void Start()
        {
            try
            {
                Console.WriteLine("===========================================");
                Console.WriteLine("   Secure Chat Server v1.0 - .NET 8.0");
                Console.WriteLine("===========================================");

                // 1. Kiểm tra kết nối CSDL MySQL
                DatabaseConnection.TestConnection();

                // 2. Kiểm tra/Tự động sinh chứng chỉ SSL
                CertGenerator.GenerateCertificatesIfNotExist();

                // 3. Load chứng chỉ SSL Server PFX
                string certsDir = CertGenerator.GetCertsDirectory();
                string pfxPath = Path.Combine(certsDir, "server.pfx");
                _serverCertificate = new X509Certificate2(pfxPath, CertGenerator.PFX_PASSWORD);

                // 4. Khởi chạy FileTransferHandler trên port 8444
                _fileTransferHandler = new FileTransferHandler(FILE_PORT, _roomManager, _serverCertificate);
                _fileTransferHandler.Start();

                // 5. Lắng nghe Chat trên port 8443
                _chatListener = new TcpListener(IPAddress.Any, CHAT_PORT);
                _chatListener.Start();

                Console.WriteLine($"[Server] Lắng nghe kết nối Chat tại port {CHAT_PORT} (SSL/TLS)...");
                Console.WriteLine($"[Server] Lắng nghe cổng truyền File tại port {FILE_PORT} (SSL/TLS)...");
                Console.WriteLine($"[Server] Địa chỉ IP máy chủ: {GetLocalIPAddress()}");

                // Đăng ký sự kiện tắt server sạch sẽ
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    Stop();
                };

                // Vòng lặp nhận kết nối chat
                while (_running)
                {
                    try
                    {
                        var clientSocket = _chatListener.AcceptTcpClient();
                        Console.WriteLine($"[Server] Client kết nối từ: {clientSocket.Client.RemoteEndPoint}");

                        // Khởi động ClientHandler cho client này
                        var handler = new ClientHandler(clientSocket, _roomManager, _serverCertificate);
                        handler.Start();
                    }
                    catch (Exception ex)
                    {
                        if (_running)
                        {
                            Console.WriteLine($"[Server] Lỗi chấp nhận kết nối: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] LỖI KHỞI ĐỘNG: {ex.Message}");
            }
        }

        public void Stop()
        {
            Console.WriteLine("[Server] Đang dừng server...");
            _running = false;
            _chatListener?.Stop();
            _fileTransferHandler?.Stop();
            Console.WriteLine("[Server] Server đã dừng.");
            Environment.Exit(0);
        }

        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "127.0.0.1";
        }
    }
}
