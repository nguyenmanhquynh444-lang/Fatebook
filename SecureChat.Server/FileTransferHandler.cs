using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SecureChat.Common.DTO;
using SecureChat.Server.Crypto;

namespace SecureChat.Server
{
    public class FileTransferHandler
    {
        private readonly int _port;
        private readonly RoomManager _roomManager;
        private readonly X509Certificate2 _serverCertificate;
        private TcpListener? _serverSocket;
        private volatile bool _running = true;

        public FileTransferHandler(int port, RoomManager roomManager, X509Certificate2 serverCertificate)
        {
            _port = port;
            _roomManager = roomManager;
            _serverCertificate = serverCertificate;
        }

        public void Start()
        {
            Task.Run(async () =>
            {
                try
                {
                    _serverSocket = new TcpListener(IPAddress.Any, _port);
                    _serverSocket.Start();
                    Console.WriteLine($"[FileServer] FileTransferHandler đang lắng nghe tại port {_port}...");

                    while (_running)
                    {
                        var clientSocket = await _serverSocket.AcceptTcpClientAsync();
                        _ = Task.Run(() => HandleFileClientAsync(clientSocket));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FileServer] Lỗi FileTransferHandler: {ex.Message}");
                }
            });
        }

        private async Task HandleFileClientAsync(TcpClient socket)
        {
            SslStream? sslStream = null;
            try
            {
                sslStream = new SslStream(socket.GetStream(), false);
                await sslStream.AuthenticateAsServerAsync(
                    _serverCertificate,
                    clientCertificateRequired: false,
                    enabledSslProtocols: SslProtocols.Tls12 | SslProtocols.Tls13,
                    checkCertificateRevocation: false);

                using (var reader = new BinaryReader(sslStream, Encoding.UTF8, leaveOpen: true))
                using (var writer = new BinaryWriter(sslStream, Encoding.UTF8, leaveOpen: true))
                {
                    // Đọc metadata
                    int roomId = reader.ReadInt32();
                    int senderId = reader.ReadInt32();
                    string fileName = reader.ReadString();
                    long fileSize = reader.ReadInt64();

                    Console.WriteLine($"[FileServer] Nhận file '{fileName}' ({fileSize} bytes) từ userId={senderId} -> room={roomId}");

                    // Đọc toàn bộ dữ liệu mã hoá còn lại
                    byte[] encryptedData;
                    using (var ms = new MemoryStream())
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, bytesRead);
                        }
                        encryptedData = ms.ToArray();
                    }

                    // Tạo MessageDTO loại FILE để broadcast
                    var fileMsg = MessageDTO.FileMessage(
                        senderId,
                        "unknown",
                        roomId,
                        Convert.ToBase64String(encryptedData),
                        fileName,
                        fileSize
                    );

                    // Broadcast tới các thành viên trong room (trừ người gửi)
                    _roomManager.BroadcastToRoom(roomId, fileMsg, senderId);

                    // Phản hồi thành công
                    writer.Write("OK");
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileServer] Lỗi xử lý file: {ex.Message}");
            }
            finally
            {
                try
                {
                    sslStream?.Close();
                    socket.Close();
                }
                catch { }
            }
        }

        public void Stop()
        {
            _running = false;
            _serverSocket?.Stop();
        }
    }
}
