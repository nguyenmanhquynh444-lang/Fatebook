using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
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
        private Thread? _listenerThread;

        public FileTransferHandler(int port, RoomManager roomManager, X509Certificate2 serverCertificate)
        {
            _port = port;
            _roomManager = roomManager;
            _serverCertificate = serverCertificate;
        }

        public void Start()
        {
            _listenerThread = new Thread(ListenForFileClients)
            {
                IsBackground = true,
                Name = "FileListener"
            };
            _listenerThread.Start();
        }

        private void ListenForFileClients()
        {
            try
            {
                _serverSocket = new TcpListener(IPAddress.Any, _port);
                _serverSocket.Start();
                Console.WriteLine($"[FileServer] FileTransferHandler đang lắng nghe tại port {_port}...");

                while (_running)
                {
                    TcpClient clientSocket = _serverSocket.AcceptTcpClient();
                    var clientThread = new Thread(() => HandleFileClient(clientSocket))
                    {
                        IsBackground = true,
                        Name = $"FileClient-{clientSocket.Client.RemoteEndPoint}"
                    };
                    clientThread.Start();
                }
            }
            catch (SocketException) when (!_running)
            {
                // Listener was stopped during shutdown.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileServer] Lỗi FileTransferHandler: {ex.Message}");
            }
        }

        private void HandleFileClient(TcpClient socket)
        {
            SslStream? sslStream = null;
            try
            {
                sslStream = new SslStream(socket.GetStream(), false);
                sslStream.AuthenticateAsServer(
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
                    long encryptedSize = reader.ReadInt64();

                    Console.WriteLine($"[FileServer] Nhận file '{fileName}' ({fileSize} bytes) từ userId={senderId} -> room={roomId}");

                    if (encryptedSize < 0 || encryptedSize > int.MaxValue)
                    {
                        throw new IOException($"Kích thước payload mã hóa không hợp lệ: {encryptedSize}");
                    }

                    // Đọc đúng số byte dữ liệu mã hoá, sau đó phản hồi OK ngay.
                    byte[] encryptedData = new byte[encryptedSize];
                    int offset = 0;
                    while (offset < encryptedData.Length)
                    {
                        int bytesRead = sslStream.Read(encryptedData, offset, encryptedData.Length - offset);
                        if (bytesRead == 0)
                        {
                            throw new EndOfStreamException("Client đóng kết nối trước khi gửi đủ dữ liệu file.");
                        }
                        offset += bytesRead;
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
