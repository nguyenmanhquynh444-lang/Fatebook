using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using SecureChat.Common.DTO;
using SecureChat.Common.Crypto;

namespace SecureChat.Client.Forms
{
    public partial class VideoCallForm : Form
    {
        private readonly SecureConnection _connection;
        private readonly int _roomId;
        private readonly int _targetUserId;
        private readonly string _targetUserDisplayName;
        private readonly bool _isCaller;
        private readonly string _myUsername;
        private readonly int _myId;
        private readonly byte[] _aesKey;

        private FilterInfoCollection? _videoDevices;
        private VideoCaptureDevice? _videoSource;
        private System.Windows.Forms.Timer? _simulatedCameraTimer;
        private long _lastFrameSentTime = 0;
        private bool _isCallActive = false;

        public VideoCallForm(SecureConnection connection, int roomId, int targetUserId, string targetUserDisplayName, bool isCaller)
        {
            InitializeComponent();
            _connection = connection;
            _roomId = roomId;
            _targetUserId = targetUserId;
            _targetUserDisplayName = targetUserDisplayName;
            _isCaller = isCaller;
            _myUsername = connection.LoggedInUser?.Username ?? "User";
            _myId = connection.LoggedInUser?.Id ?? 0;
            _aesKey = connection.AesKey!;

            SetupStyles();
        }

        private void SetupStyles()
        {
            this.Text = $"Cuộc gọi video với {_targetUserDisplayName}";
            this.BackColor = Color.FromArgb(20, 20, 25);
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.Sizable;

            lblStatus.Text = _isCaller ? "Đang đổ chuông đối phương..." : "Đang kết nối cuộc gọi...";
            lblStatus.ForeColor = Color.FromArgb(241, 196, 15); // Vàng
        }

        private void VideoCallForm_Load(object sender, EventArgs e)
        {
            if (!_isCaller)
            {
                // Nếu là người nhận, cuộc gọi đã kết nối ngay khi mở form
                StartCall();
            }
        }

        public void StartCall()
        {
            if (_isCallActive) return;
            _isCallActive = true;

            this.BeginInvoke(new Action(() =>
            {
                lblStatus.Text = "Đang đàm thoại (Bảo mật đầu cuối)";
                lblStatus.ForeColor = Color.FromArgb(46, 204, 113); // Xanh lá
            }));

            // Bắt đầu camera hoặc camera giả lập
            InitializeCamera();
        }

        private void InitializeCamera()
        {
            try
            {
                _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (_videoDevices.Count > 0)
                {
                    // Sử dụng camera đầu tiên tìm thấy
                    _videoSource = new VideoCaptureDevice(_videoDevices[0].MonikerString);
                    _videoSource.NewFrame += VideoSource_NewFrame;
                    _videoSource.Start();
                }
                else
                {
                    Console.WriteLine("[VideoCall] Không tìm thấy camera thật. Kích hoạt camera giả lập.");
                    // Không có camera thật, chạy camera giả lập bằng Timer
                    _simulatedCameraTimer = new System.Windows.Forms.Timer();
                    _simulatedCameraTimer.Interval = 200; // ~5 FPS
                    _simulatedCameraTimer.Tick += SimulatedCameraTimer_Tick;
                    _simulatedCameraTimer.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VideoCall] Lỗi khởi động camera: {ex.Message}. Chuyển sang giả lập.");
                _simulatedCameraTimer = new System.Windows.Forms.Timer();
                _simulatedCameraTimer.Interval = 200;
                _simulatedCameraTimer.Tick += SimulatedCameraTimer_Tick;
                _simulatedCameraTimer.Start();
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            // Clone khung hình để tránh xung đột luồng của DirectShow
            using (Bitmap frame = (Bitmap)eventArgs.Frame.Clone())
            {
                // Hiển thị preview local
                UpdateLocalPreview(frame);

                // Gửi khung hình đi nếu đủ thời gian trễ (rate limit ~5 FPS)
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now - _lastFrameSentTime >= 200)
                {
                    _lastFrameSentTime = now;
                    SendFrame(frame);
                }
            }
        }

        private void SimulatedCameraTimer_Tick(object? sender, EventArgs e)
        {
            using (Bitmap frame = GenerateSimulatedFrame())
            {
                UpdateLocalPreview(frame);
                SendFrame(frame);
            }
        }

        private void UpdateLocalPreview(Bitmap bmp)
        {
            if (this.IsDisposed || picLocal.IsDisposed) return;

            try
            {
                // Thumbnail nhỏ của mình
                Bitmap preview = new Bitmap(bmp, picLocal.Size);
                
                this.BeginInvoke(new Action(() =>
                {
                    var oldImg = picLocal.Image;
                    picLocal.Image = preview;
                    oldImg?.Dispose();
                }));
            }
            catch { }
        }

        private void SendFrame(Bitmap bmp)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    // Nén JPEG để giảm băng thông truyền tải
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                    byte[] jpegBytes = ms.ToArray();
                    string base64 = Convert.ToBase64String(jpegBytes);

                    // Mã hóa AES bằng khóa đối xứng của người gửi
                    string encrypted = AESUtil.Encrypt(base64, _aesKey);

                    var frameMsg = new MessageDTO(MessageDTO.MessageType.VIDEO_FRAME)
                    {
                        RoomId = _roomId,
                        SenderId = _myId,
                        SenderUsername = _myUsername,
                        EncryptedContent = encrypted
                    };

                    _connection.SendMessage(frameMsg);
                }
            }
            catch { }
        }

        public void OnFrameReceived(string base64Decrypted)
        {
            if (this.IsDisposed || picRemote.IsDisposed) return;

            try
            {
                byte[] jpegBytes = Convert.FromBase64String(base64Decrypted);
                using (MemoryStream ms = new MemoryStream(jpegBytes))
                {
                    Image img = Image.FromStream(ms);
                    this.BeginInvoke(new Action(() =>
                    {
                        var oldImg = picRemote.Image;
                        picRemote.Image = img;
                        oldImg?.Dispose();
                    }));
                }
            }
            catch { }
        }

        private Bitmap GenerateSimulatedFrame()
        {
            Bitmap bmp = new Bitmap(320, 240);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(30, 30, 36));

                // Vẽ vòng tròn hoạt động
                double angle = (DateTime.Now.Millisecond / 1000.0) * 2 * Math.PI;
                int cx = 160 + (int)(30 * Math.Cos(angle));
                int cy = 120 + (int)(30 * Math.Sin(angle));

                using (Brush circleBrush = new SolidBrush(Color.FromArgb(108, 92, 231)))
                {
                    g.FillEllipse(circleBrush, cx - 20, cy - 20, 40, 40);
                }

                // Vẽ tiêu đề
                using (Font fTitle = new Font("Segoe UI", 10, FontStyle.Bold))
                using (Brush textBrush = new SolidBrush(Color.White))
                {
                    g.DrawString("🔒 CAMERA GIẢ LẬP", fTitle, textBrush, new PointF(15, 15));
                    g.DrawString($"User: {_myUsername}", fTitle, textBrush, new PointF(15, 35));
                    g.DrawString(DateTime.Now.ToString("HH:mm:ss.fff"), fTitle, textBrush, new PointF(15, 205));
                }

                // Vẽ khung định dạng
                using (Pen borderPen = new Pen(Color.FromArgb(108, 92, 231, 120), 2))
                {
                    g.DrawRectangle(borderPen, 8, 8, 304, 224);
                    g.DrawLine(borderPen, 0, 120, 320, 120);
                    g.DrawLine(borderPen, 160, 0, 160, 240);
                }
            }
            return bmp;
        }

        private void btnHangUp_Click(object sender, EventArgs e)
        {
            HangUp(true);
        }

        public void HangUp(bool sendNotification)
        {
            _isCallActive = false;

            if (sendNotification)
            {
                try
                {
                    var hangupMsg = new MessageDTO(MessageDTO.MessageType.VIDEO_HANGUP)
                    {
                        RoomId = _roomId,
                        SenderId = _myId,
                        SenderUsername = _myUsername
                    };
                    _connection.SendMessage(hangupMsg);
                }
                catch { }
            }

            // Tắt camera giải phóng tài nguyên
            StopCamera();

            this.BeginInvoke(new Action(() =>
            {
                this.Close();
            }));
        }

        private void StopCamera()
        {
            try
            {
                if (_videoSource != null && _videoSource.IsRunning)
                {
                    _videoSource.SignalToStop();
                    _videoSource.WaitForStop();
                    _videoSource = null;
                }

                if (_simulatedCameraTimer != null)
                {
                    _simulatedCameraTimer.Stop();
                    _simulatedCameraTimer.Dispose();
                    _simulatedCameraTimer = null;
                }
            }
            catch { }
        }

        private void VideoCallForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isCallActive)
            {
                HangUp(true);
            }
            else
            {
                StopCamera();
            }
        }
    }
}
