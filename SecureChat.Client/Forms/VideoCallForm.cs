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
        private readonly bool _isVideo;

        private FilterInfoCollection? _videoDevices;
        private VideoCaptureDevice? _videoSource;
        private System.Windows.Forms.Timer? _simulatedCameraTimer;
        private long _lastFrameSentTime = 0;
        private bool _isCallActive = false;

        private Panel? pnlVoiceCall;
        private Label? lblVoiceAvatar;

        public VideoCallForm(SecureConnection connection, int roomId, int targetUserId, string targetUserDisplayName, bool isCaller, bool isVideo = true)
        {
            InitializeComponent();
            _connection = connection;
            _roomId = roomId;
            _targetUserId = targetUserId;
            _targetUserDisplayName = targetUserDisplayName;
            _isCaller = isCaller;
            _isVideo = isVideo;
            _myUsername = connection.LoggedInUser?.Username ?? "User";
            _myId = connection.LoggedInUser?.Id ?? 0;
            _aesKey = connection.AesKey!;

            SetupStyles();
        }

        private void SetupStyles()
        {
            this.BackColor = ChatTheme.Bg0;
            this.ForeColor = ChatTheme.Text0;
            this.Font = ChatTheme.Font(9F);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(800, 600);
            this.MinimumSize = new Size(720, 520);
            this.FormBorderStyle = FormBorderStyle.Sizable;

            if (_isVideo)
            {
                picRemote.BackColor = ChatTheme.Bg0;
                picRemote.BorderStyle = BorderStyle.None;
                picLocal.BackColor = ChatTheme.Bg3;
                picLocal.BorderStyle = BorderStyle.None;
                picLocal.Paint += (_, e) => ChatTheme.PaintRoundedBorder(picLocal, e, ChatTheme.Border, 12);
                ChatTheme.ApplyRoundedRegion(picLocal, 12);
                picLocal.Resize += (_, _) => ChatTheme.ApplyRoundedRegion(picLocal, 12);
                picRemote.SendToBack();
                picLocal.BringToFront();
            }
            else
            {
                picLocal.Visible = false;
                picRemote.Visible = false;

                pnlVoiceCall = new Panel
                {
                    BackColor = ChatTheme.Bg1,
                    Dock = DockStyle.Fill
                };

                lblVoiceAvatar = new Label
                {
                    Text = ChatTheme.Initials(_targetUserDisplayName),
                    BackColor = ChatTheme.Accent,
                    ForeColor = Color.White,
                    Font = ChatTheme.Font(24F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Size = new Size(100, 100)
                };
                ChatTheme.ApplyRoundedRegion(lblVoiceAvatar, 50);

                Label lblSecureTip = new Label
                {
                    Text = "🔒 Cuộc gọi thoại được bảo mật bằng mã hóa đầu cuối (E2EE)",
                    ForeColor = ChatTheme.Green,
                    Font = ChatTheme.Font(9.5F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Height = 24
                };

                pnlVoiceCall.Controls.Add(lblVoiceAvatar);
                pnlVoiceCall.Controls.Add(lblSecureTip);
                this.Controls.Add(pnlVoiceCall);

                pnlVoiceCall.Resize += (s, e) =>
                {
                    lblVoiceAvatar.Location = new Point((pnlVoiceCall.Width - 100) / 2, (pnlVoiceCall.Height - 100) / 2 - 40);
                    lblSecureTip.SetBounds(10, lblVoiceAvatar.Bottom + 20, pnlVoiceCall.Width - 20, 24);
                };
                pnlVoiceCall.SendToBack();
            }

            btnHangUp.Text = "CÚP MÁY";
            btnHangUp.Font = ChatTheme.Font(10.5F, FontStyle.Bold);
            ChatTheme.ApplyDangerButton(btnHangUp, 20);

            lblStatus.Font = ChatTheme.Font(12F, FontStyle.Bold);
            lblStatus.ForeColor = ChatTheme.Yellow;
            lblStatus.BackColor = Color.Transparent;
            lblStatus.Text = _isCaller ? "Đang đổ chuông đối phương..." : "Đang kết nối cuộc gọi...";

            lblStatus.BringToFront();
            btnHangUp.BringToFront();
            
            LayoutVideoControls();
            this.Resize += (_, _) => LayoutVideoControls();
        }

        private void LayoutVideoControls()
        {
            if (_isVideo)
            {
                picLocal.SetBounds(ClientSize.Width - 196, ClientSize.Height - 152, 176, 116);
            }
            btnHangUp.SetBounds((ClientSize.Width - 136) / 2, ClientSize.Height - 66, 136, 42);
            lblStatus.SetBounds(20, 18, ClientSize.Width - 40, 28);
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
                lblStatus.ForeColor = ChatTheme.Green;
            }));

            // Bắt đầu camera hoặc camera giả lập
            InitializeCamera();
        }

        private void InitializeCamera()
        {
            if (!_isVideo) return; // Không khởi tạo camera cho cuộc gọi thoại!
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
                        TargetUserId = _targetUserId,
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
                    using (Image tempImg = Image.FromStream(ms))
                    {
                        // Tạo một Bitmap mới sao chép dữ liệu từ tempImg để giải phóng MemoryStream ngay lập tức
                        Bitmap bmp = new Bitmap(tempImg);
                        this.BeginInvoke(new Action(() =>
                        {
                            var oldImg = picRemote.Image;
                            picRemote.Image = bmp;
                            oldImg?.Dispose();
                        }));
                    }
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
                g.Clear(ChatTheme.Bg2);

                // Vẽ vòng tròn hoạt động
                double angle = (DateTime.Now.Millisecond / 1000.0) * 2 * Math.PI;
                int cx = 160 + (int)(30 * Math.Cos(angle));
                int cy = 120 + (int)(30 * Math.Sin(angle));

                using (Brush circleBrush = new SolidBrush(ChatTheme.Accent))
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
                using (Pen borderPen = new Pen(Color.FromArgb(120, ChatTheme.Accent), 2))
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
                        SenderUsername = _myUsername,
                        TargetUserId = _targetUserId
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
