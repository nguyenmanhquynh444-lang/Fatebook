using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using SecureChat.Common.DTO;
using SecureChat.Common.Crypto;
using SecureChat.Client.Storage;

namespace SecureChat.Client.Forms
{
    public partial class MainChatForm : Form
    {
        private readonly SecureConnection _connection;
        private LocalHistoryDB? _historyDB;
        private readonly Dictionary<int, UserDTO> _allUsers = new(); // userId -> UserDTO
        private UserDTO? _activeChatUser;
        private int _activeRoomId = -1;
        private VideoCallForm? _activeVideoCallForm;
        private Panel? _composerPanel;
        private Label? _chatAvatar;
        private Panel? _incomingCallOverlay;
        private Panel? _incomingCallCard;
        private Label? _incomingCallInitials;
        private Label? _incomingCallName;
        private MessageDTO? _pendingVideoInvite;
        private Button? btnAudioCall;
        private Label? _incomingCallType;
        private Label? _incomingCallUsername;
        private Label? _myAvatar;
        private readonly ToolTip _toolTip = new();

        public MainChatForm(SecureConnection connection)
        {
            InitializeComponent();
            _connection = connection;
            SetupFormStyles();
            InitializeEvents();

            // Khởi chạy nhận tin nhắn sau khi các sự kiện callback đã được đăng ký
            _connection.StartReceiveLoop();
        }

        private void SetupFormStyles()
        {
            this.BackColor = ChatTheme.Bg0;
            this.ForeColor = ChatTheme.Text0;
            this.Font = ChatTheme.Font(9F);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(960, 600);

            pnlSidebar.Width = 286;
            pnlSidebar.BackColor = ChatTheme.Bg1;
            pnlSidebar.Padding = new Padding(0);
            lblCurrentUser.Text = $"{_connection.LoggedInUser?.DisplayName ?? _connection.LoggedInUser?.Username}";
            lblCurrentUser.ForeColor = ChatTheme.Text0;
            lblCurrentUser.Font = ChatTheme.Font(11.5F, FontStyle.Bold);
            lblCurrentUser.Location = new Point(56, 18);
            lblCurrentUser.Size = new Size(pnlSidebar.Width - 68, 25);

            // Khởi tạo _myAvatar
            _myAvatar = new Label
            {
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(12, 11),
                Size = new Size(36, 36),
                BackColor = ChatTheme.Accent,
                ForeColor = Color.White,
                Font = ChatTheme.Font(9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _myAvatar.Click += _myAvatar_Click;
            _toolTip.SetToolTip(_myAvatar, "Nhấp để đổi ảnh đại diện");
            pnlSidebar.Controls.Add(_myAvatar);
            UpdateMyAvatarVisuals();

            pnlUserList.BackColor = ChatTheme.Bg1;
            pnlUserList.Location = new Point(0, 60);
            pnlUserList.Size = new Size(pnlSidebar.Width, pnlSidebar.Height - 60);
            pnlUserList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlUserList.Padding = new Padding(0, 4, 0, 12);
            pnlUserList.FlowDirection = FlowDirection.TopDown;
            pnlUserList.WrapContents = false;

            pnlChatArea.BackColor = ChatTheme.Bg2;
            pnlChatHeader.Height = 64;
            pnlChatHeader.BackColor = ChatTheme.Bg2;
            pnlChatHeader.Padding = new Padding(16, 0, 16, 0);
            pnlChatHeader.Paint += (_, e) =>
            {
                using Pen pen = new Pen(ChatTheme.Border, 1);
                e.Graphics.DrawLine(pen, 0, pnlChatHeader.Height - 1, pnlChatHeader.Width, pnlChatHeader.Height - 1);
            };

            _chatAvatar = new Label
            {
                Text = "?",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                BackColor = ChatTheme.Bg4,
                Font = ChatTheme.Font(10F, FontStyle.Bold),
                Size = new Size(40, 40),
                Location = new Point(18, 12)
            };
            ChatTheme.ApplyRoundedRegion(_chatAvatar, 20);
            pnlChatHeader.Controls.Add(_chatAvatar);
            _chatAvatar.BringToFront();

            lblChatTarget.Location = new Point(70, 11);
            lblChatTarget.Font = ChatTheme.Font(12F, FontStyle.Bold);
            lblChatTarget.ForeColor = ChatTheme.Text0;
            lblChatTarget.AutoSize = false;
            lblChatTarget.Size = new Size(260, 24);
            lblChatTargetStatus.Location = new Point(71, 36);
            lblChatTargetStatus.Font = ChatTheme.Font(8.5F);
            lblChatTargetStatus.ForeColor = ChatTheme.Text2;
            lblChatTargetStatus.AutoSize = false;
            lblChatTargetStatus.Size = new Size(280, 18);

            lblSecureIndicator.AutoSize = false;
            lblSecureIndicator.TextAlign = ContentAlignment.MiddleCenter;
            lblSecureIndicator.BackColor = ChatTheme.Bg3;
            lblSecureIndicator.ForeColor = ChatTheme.Green;
            lblSecureIndicator.Font = ChatTheme.Font(8F, FontStyle.Bold);
            lblSecureIndicator.Size = new Size(178, 30);
            ChatTheme.ApplyRoundedRegion(lblSecureIndicator, 10);
            btnVideoCall.Text = "";
            btnHistory.Text = "🕘";
            btnVideoCall.Font = ChatTheme.Font(11F, FontStyle.Bold);
            btnHistory.Font = ChatTheme.Font(11F, FontStyle.Bold);
            
            // Custom setup for transparent buttons with hover styles (matches the user's uploaded icons)
            btnVideoCall.BackColor = Color.Transparent;
            btnVideoCall.FlatStyle = FlatStyle.Flat;
            btnVideoCall.FlatAppearance.BorderSize = 0;
            btnVideoCall.FlatAppearance.MouseOverBackColor = ChatTheme.Bg4;
            btnVideoCall.FlatAppearance.MouseDownBackColor = ChatTheme.Hover;
            btnVideoCall.UseVisualStyleBackColor = false;
            btnVideoCall.Cursor = Cursors.Hand;
            ChatTheme.ApplyRoundedRegion(btnVideoCall, 16);
            btnVideoCall.Resize += (_, _) => ChatTheme.ApplyRoundedRegion(btnVideoCall, 16);

            ChatTheme.ApplyNeutralButton(btnHistory, 16);
            _toolTip.SetToolTip(btnVideoCall, "Gọi video");
            _toolTip.SetToolTip(btnHistory, "Lịch sử chat");

            // Tạo nút Gọi Thoại
            btnAudioCall = new Button();
            btnAudioCall.Enabled = false;
            btnAudioCall.BackColor = Color.Transparent;
            btnAudioCall.FlatStyle = FlatStyle.Flat;
            btnAudioCall.FlatAppearance.BorderSize = 0;
            btnAudioCall.FlatAppearance.MouseOverBackColor = ChatTheme.Bg4;
            btnAudioCall.FlatAppearance.MouseDownBackColor = ChatTheme.Hover;
            btnAudioCall.UseVisualStyleBackColor = false;
            btnAudioCall.Cursor = Cursors.Hand;
            ChatTheme.ApplyRoundedRegion(btnAudioCall, 16);
            btnAudioCall.Resize += (_, _) => ChatTheme.ApplyRoundedRegion(btnAudioCall, 16);

            _toolTip.SetToolTip(btnAudioCall, "Gọi thoại");
            btnAudioCall.Click += btnAudioCall_Click;
            pnlChatHeader.Controls.Add(btnAudioCall);

            // Custom paint vẽ icon Gọi Video màu tím cực sắc nét
            btnVideoCall.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color iconColor = btnVideoCall.Enabled ? Color.FromArgb(157, 0, 255) : ChatTheme.Text3;
                int cx = btnVideoCall.Width;
                int cy = btnVideoCall.Height;
                using (var brush = new SolidBrush(iconColor))
                {
                    // Thân máy quay
                    using (var path = ChatTheme.RoundedRect(new Rectangle(cx / 2 - 9, cy / 2 - 6, 12, 12), 3))
                    {
                        pe.Graphics.FillPath(brush, path);
                    }
                    // Ống kính máy quay
                    PointF[] points = {
                        new PointF(cx / 2 + 3, cy / 2 - 3),
                        new PointF(cx / 2 + 9, cy / 2 - 7),
                        new PointF(cx / 2 + 9, cy / 2 + 7),
                        new PointF(cx / 2 + 3, cy / 2 + 3)
                    };
                    pe.Graphics.FillPolygon(brush, points);
                }
            };

            // Custom paint vẽ icon Gọi Thoại màu tím cực sắc nét
            btnAudioCall.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color iconColor = btnAudioCall!.Enabled ? Color.FromArgb(157, 0, 255) : ChatTheme.Text3;
                int cx = btnAudioCall.Width;
                int cy = btnAudioCall.Height;
                using (var pen = new Pen(iconColor, 4.5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    pe.Graphics.DrawBezier(pen,
                        new PointF(cx / 2 - 6, cy / 2 - 6),
                        new PointF(cx / 2 - 12, cy / 2 - 2),
                        new PointF(cx / 2 - 2, cy / 2 + 12),
                        new PointF(cx / 2 + 6, cy / 2 + 6)
                    );
                    using (var brush = new SolidBrush(iconColor))
                    {
                        pe.Graphics.FillEllipse(brush, cx / 2 - 9, cy / 2 - 9, 6, 6);
                        pe.Graphics.FillEllipse(brush, cx / 2 + 3, cy / 2 + 3, 6, 6);
                    }
                }
            };

            LayoutHeaderActions();
            pnlChatHeader.Resize += (_, _) => LayoutHeaderActions();
            pnlMessages.BackColor = ChatTheme.Bg2;
            pnlMessages.Padding = new Padding(16, 18, 16, 24);

            pnlInput.Height = 76;
            pnlInput.BackColor = ChatTheme.Bg2;
            BuildComposer();
            pnlInput.Resize += (_, _) => LayoutComposer();

            BuildIncomingCallPopup();
            this.Resize += (_, _) => LayoutIncomingCallPopup();

            pnlMessages.SizeChanged += (s, e) => ResizeMessages();

            ChatTheme.EnableDoubleBuffer(pnlMessages);
            ChatTheme.EnableDoubleBuffer(pnlUserList);
        }

        private void LayoutHeaderActions()
        {
            int right = pnlChatHeader.ClientSize.Width - 16;
            btnHistory.SetBounds(right - 40, 16, 32, 32);
            btnVideoCall.SetBounds(btnHistory.Left - 44, 16, 32, 32);
            if (btnAudioCall != null)
            {
                btnAudioCall.SetBounds(btnVideoCall.Left - 44, 16, 32, 32);
                lblSecureIndicator.SetBounds(Math.Max(360, btnAudioCall.Left - 190), 17, 178, 30);
            }
            else
            {
                lblSecureIndicator.SetBounds(Math.Max(360, btnVideoCall.Left - 190), 17, 178, 30);
            }
            ChatTheme.ApplyRoundedRegion(lblSecureIndicator, 10);
        }

        private void BuildComposer()
        {
            if (_composerPanel == null)
            {
                _composerPanel = new Panel
                {
                    BackColor = ChatTheme.Bg4,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom
                };
                _composerPanel.Paint += (_, e) => ChatTheme.PaintRoundedBorder(_composerPanel, e, ChatTheme.Border, 12);
                pnlInput.Controls.Add(_composerPanel);

                pnlInput.Controls.Remove(btnAttach);
                pnlInput.Controls.Remove(txtMessage);
                pnlInput.Controls.Remove(btnSend);
                _composerPanel.Controls.Add(btnAttach);
                _composerPanel.Controls.Add(txtMessage);
                _composerPanel.Controls.Add(btnSend);
            }

            txtMessage.Multiline = true;
            txtMessage.ScrollBars = ScrollBars.None;
            txtMessage.PlaceholderText = "Nhắn tin bảo mật...";
            ChatTheme.ApplyTextBox(txtMessage);

            btnAttach.Text = "📎";
            btnSend.Text = "➤";
            btnAttach.Font = ChatTheme.Font(12F, FontStyle.Bold);
            btnSend.Font = ChatTheme.Font(12F, FontStyle.Bold);
            ChatTheme.ApplyNeutralButton(btnAttach, 8);
            ChatTheme.ApplyPrimaryButton(btnSend, 8);
            _toolTip.SetToolTip(btnAttach, "Đính kèm file");
            _toolTip.SetToolTip(btnSend, "Gửi tin nhắn");

            LayoutComposer();
        }

        private void LayoutComposer()
        {
            if (_composerPanel == null) return;

            int width = Math.Max(220, pnlInput.ClientSize.Width - 32);
            _composerPanel.SetBounds(16, 12, width, 48);
            ChatTheme.ApplyRoundedRegion(_composerPanel, 12);

            btnAttach.SetBounds(8, 6, 36, 36);
            btnSend.SetBounds(_composerPanel.Width - 44, 6, 36, 36);
            txtMessage.SetBounds(54, 13, Math.Max(80, _composerPanel.Width - 110), 24);
        }

        private void UpdateChatHeaderVisuals()
        {
            if (_chatAvatar == null) return;

            if (_activeChatUser != null && !string.IsNullOrEmpty(_activeChatUser.AvatarBase64))
            {
                try
                {
                    byte[] imgBytes = Convert.FromBase64String(_activeChatUser.AvatarBase64);
                    using (var ms = new MemoryStream(imgBytes))
                    {
                        using (var tempImg = Image.FromStream(ms))
                        {
                            _chatAvatar.Image = new Bitmap(tempImg, _chatAvatar.Size);
                        }
                    }
                    _chatAvatar.Text = "";
                }
                catch
                {
                    _chatAvatar.Text = ChatTheme.Initials(_activeChatUser.DisplayName);
                    _chatAvatar.Image = null;
                    _chatAvatar.BackColor = ChatTheme.Accent;
                }
            }
            else
            {
                string name = _activeChatUser?.DisplayName ?? "?";
                _chatAvatar.Text = ChatTheme.Initials(name);
                _chatAvatar.Image = null;
                _chatAvatar.BackColor = _activeChatUser == null ? ChatTheme.Bg4 : ChatTheme.Accent;
            }
            ChatTheme.ApplyRoundedRegion(_chatAvatar, 20);
        }

        private void UpdateMyAvatarVisuals()
        {
            if (_myAvatar == null) return;

            var user = _connection.LoggedInUser;
            if (user != null && !string.IsNullOrEmpty(user.AvatarBase64))
            {
                try
                {
                    byte[] imgBytes = Convert.FromBase64String(user.AvatarBase64);
                    using (var ms = new MemoryStream(imgBytes))
                    {
                        using (var tempImg = Image.FromStream(ms))
                        {
                            _myAvatar.Image = new Bitmap(tempImg, _myAvatar.Size);
                        }
                    }
                    _myAvatar.Text = "";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MainUI] Lỗi hiển thị avatar của tôi: {ex.Message}");
                    _myAvatar.Text = ChatTheme.Initials(user.DisplayName);
                    _myAvatar.Image = null;
                }
            }
            else
            {
                string name = user?.DisplayName ?? "?";
                _myAvatar.Text = ChatTheme.Initials(name);
                _myAvatar.Image = null;
            }
            ChatTheme.ApplyRoundedRegion(_myAvatar, 18);
        }

        private void _myAvatar_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
                ofd.Title = "Chọn ảnh đại diện";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string filePath = ofd.FileName;
                        string base64Str = "";

                        // Load and resize image to 128x128
                        using (var original = Image.FromFile(filePath))
                        {
                            using (var resized = new Bitmap(128, 128))
                            {
                                using (var g = Graphics.FromImage(resized))
                                {
                                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                                    
                                    g.DrawImage(original, 0, 0, 128, 128);
                                }

                                using (var ms = new MemoryStream())
                                {
                                    resized.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                    byte[] imgBytes = ms.ToArray();
                                    base64Str = Convert.ToBase64String(imgBytes);
                                }
                            }
                        }

                        if (_connection.LoggedInUser != null)
                        {
                            _connection.LoggedInUser.AvatarBase64 = base64Str;
                            UpdateMyAvatarVisuals();

                            // Gửi lên server để cập nhật và broadcast
                            var updateMsg = new MessageDTO(MessageDTO.MessageType.AVATAR_UPDATE)
                            {
                                SenderId = _connection.LoggedInUser.Id,
                                SenderUsername = _connection.LoggedInUser.Username,
                                PlainContent = base64Str
                            };
                            _connection.SendMessage(updateMsg);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi cập nhật ảnh đại diện: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BuildIncomingCallPopup()
        {
            if (_incomingCallOverlay != null) return;

            _incomingCallOverlay = new Panel
            {
                BackColor = Color.FromArgb(8, 9, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Visible = false
            };

            _incomingCallCard = new Panel
            {
                BackColor = ChatTheme.Bg1,
                Size = new Size(340, 300)
            };
            _incomingCallCard.Paint += (_, e) => ChatTheme.PaintRoundedBorder(_incomingCallCard, e, Color.FromArgb(157, 0, 255), 16);
            _incomingCallCard.Resize += (_, _) => ChatTheme.ApplyRoundedRegion(_incomingCallCard, 16);

            _incomingCallType = new Label
            {
                Text = "📹 CUỘC GỌI ĐẾN",
                ForeColor = Color.FromArgb(244, 117, 145),
                Font = ChatTheme.Font(9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 20),
                Size = new Size(340, 20)
            };

            Panel avatarRing = new Panel
            {
                BackColor = ChatTheme.AccentDim,
                Location = new Point(134, 52),
                Size = new Size(72, 72)
            };
            ChatTheme.ApplyRoundedRegion(avatarRing, 36);

            _incomingCallInitials = new Label
            {
                Text = "??",
                BackColor = ChatTheme.Accent,
                ForeColor = Color.White,
                Font = ChatTheme.Font(16F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(5, 5),
                Size = new Size(62, 62)
            };
            ChatTheme.ApplyRoundedRegion(_incomingCallInitials, 31);
            avatarRing.Controls.Add(_incomingCallInitials);

            _incomingCallName = new Label
            {
                Text = "Người gọi",
                ForeColor = ChatTheme.Text0,
                Font = ChatTheme.Font(13F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 136),
                Size = new Size(300, 24)
            };

            _incomingCallUsername = new Label
            {
                Text = "@username",
                ForeColor = ChatTheme.Text2,
                Font = ChatTheme.Font(9F),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 162),
                Size = new Size(300, 18)
            };

            Label status = new Label
            {
                Text = "đang gọi cho bạn...",
                ForeColor = ChatTheme.Text2,
                Font = ChatTheme.Font(9F),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 182),
                Size = new Size(300, 18)
            };

            Button declineButton = new Button
            {
                Text = "",
                Size = new Size(52, 52),
                Location = new Point(90, 222)
            };
            ChatTheme.ApplyDangerButton(declineButton, 26);
            declineButton.Click += (_, _) => DeclineIncomingVideoCall();
            declineButton.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int cx = declineButton.Width;
                int cy = declineButton.Height;
                using (var pen = new Pen(Color.White, 4f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pe.Graphics.DrawBezier(pen,
                        new PointF(cx / 2 - 10, cy / 2 - 2),
                        new PointF(cx / 2 - 5, cy / 2 + 5),
                        new PointF(cx / 2 + 5, cy / 2 + 5),
                        new PointF(cx / 2 + 10, cy / 2 - 2)
                    );
                }
            };

            Button acceptButton = new Button
            {
                Text = "",
                Size = new Size(52, 52),
                Location = new Point(198, 222)
            };
            ChatTheme.ApplyFlatButton(acceptButton, ChatTheme.Green, Color.FromArgb(46, 136, 78), Color.White, 26);
            acceptButton.Click += (_, _) => AcceptIncomingVideoCall();
            acceptButton.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int cx = acceptButton.Width;
                int cy = acceptButton.Height;
                using (var pen = new Pen(Color.White, 4f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pe.Graphics.DrawBezier(pen,
                        new PointF(cx / 2 - 5, cy / 2 - 7),
                        new PointF(cx / 2 - 11, cy / 2 - 3),
                        new PointF(cx / 2 - 3, cy / 2 + 9),
                        new PointF(cx / 2 + 5, cy / 2 + 5)
                    );
                    using (var brush = new SolidBrush(Color.White))
                    {
                        pe.Graphics.FillEllipse(brush, cx / 2 - 8, cy / 2 - 10, 5, 5);
                        pe.Graphics.FillEllipse(brush, cx / 2 + 2, cy / 2 + 2, 5, 5);
                    }
                }
            };

            _incomingCallCard.Controls.Add(_incomingCallType);
            _incomingCallCard.Controls.Add(avatarRing);
            _incomingCallCard.Controls.Add(_incomingCallName);
            _incomingCallCard.Controls.Add(_incomingCallUsername);
            _incomingCallCard.Controls.Add(status);
            _incomingCallCard.Controls.Add(declineButton);
            _incomingCallCard.Controls.Add(acceptButton);

            _incomingCallOverlay.Controls.Add(_incomingCallCard);
            Controls.Add(_incomingCallOverlay);
            LayoutIncomingCallPopup();
        }

        private void LayoutIncomingCallPopup()
        {
            if (_incomingCallOverlay == null || _incomingCallCard == null) return;

            _incomingCallOverlay.Bounds = ClientRectangle;
            _incomingCallCard.Location = new Point(
                Math.Max(0, (_incomingCallOverlay.Width - _incomingCallCard.Width) / 2),
                Math.Max(0, (_incomingCallOverlay.Height - _incomingCallCard.Height) / 2)
            );
            ChatTheme.ApplyRoundedRegion(_incomingCallCard, 16);
        }

        private void ShowIncomingVideoCallPopup(MessageDTO msg)
        {
            BuildIncomingCallPopup();
            if (_incomingCallOverlay == null || _incomingCallInitials == null || _incomingCallName == null) return;

            _pendingVideoInvite = msg;
            string callerName = GetDisplayNameForUser(msg.SenderId, msg.SenderUsername);
            bool isVideo = msg.PlainContent != "AUDIO";

            _incomingCallName.Text = callerName;
            _incomingCallInitials.Text = ChatTheme.Initials(callerName);
            
            if (_incomingCallType != null)
            {
                _incomingCallType.Text = isVideo ? "📹 CUỘC GỌI VIDEO ĐẾN" : "📞 CUỘC GỌI THOẠI ĐẾN";
                _incomingCallType.ForeColor = isVideo ? Color.FromArgb(244, 117, 145) : ChatTheme.Green;
            }

            if (_incomingCallUsername != null)
            {
                _incomingCallUsername.Text = $"@{msg.SenderUsername} (ID: {msg.SenderId})";
            }

            _incomingCallOverlay.Visible = true;
            _incomingCallOverlay.BringToFront();
            LayoutIncomingCallPopup();
        }

        private void HideIncomingVideoCallPopup()
        {
            if (_incomingCallOverlay != null)
            {
                _incomingCallOverlay.Visible = false;
            }
            _pendingVideoInvite = null;
        }

        private string GetDisplayNameForUser(int userId, string fallback)
        {
            if (_allUsers.TryGetValue(userId, out var user) && !string.IsNullOrWhiteSpace(user.DisplayName))
            {
                return user.DisplayName;
            }

            return string.IsNullOrWhiteSpace(fallback) ? "Người gọi" : fallback;
        }

        private void InitializeEvents()
        {
            _connection.MessageReceived += OnMessageReceived;
            _connection.ConnectionError += OnConnectionError;

            this.FormClosing += (s, e) =>
            {
                _connection.Disconnect();
                _historyDB?.Close();
            };
        }

        private void MainChatForm_Load(object sender, EventArgs e)
        {
            // Khởi tạo Database SQLite cục bộ
            string username = _connection.LoggedInUser?.Username ?? "anonymous";
            _historyDB = new LocalHistoryDB(username);
            _historyDB.Init();

            // Set status label secure
            lblSecureIndicator.Text = "🔒 AES-256-GCM + TLS";
            lblSecureIndicator.ForeColor = ChatTheme.Green;
            UpdateChatHeaderVisuals();
        }

        private void OnConnectionError(string errorMsg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnConnectionError(errorMsg)));
                return;
            }
            MessageBox.Show(errorMsg, "Mất kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
            this.Close();
        }

        private void OnMessageReceived(MessageDTO msg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnMessageReceived(msg)));
                return;
            }

            try
            {
                // Xử lý các loại tin nhắn
                switch (msg.Type)
                {
                    case MessageDTO.MessageType.USER_LIST:
                        UpdateUserList(msg.PlainContent);
                        break;

                    case MessageDTO.MessageType.SYSTEM:
                        HandleSystemMessage(msg);
                        break;

                    case MessageDTO.MessageType.TEXT:
                        HandleIncomingText(msg);
                        break;

                    case MessageDTO.MessageType.FILE:
                        HandleIncomingFile(msg);
                        break;

                    case MessageDTO.MessageType.VIDEO_INVITE:
                        HandleVideoInvite(msg);
                        break;

                    case MessageDTO.MessageType.VIDEO_ACCEPT:
                        HandleVideoAccept(msg);
                        break;

                    case MessageDTO.MessageType.VIDEO_REJECT:
                        HandleVideoReject(msg);
                        break;

                    case MessageDTO.MessageType.VIDEO_HANGUP:
                        HandleVideoHangUp(msg);
                        break;

                    case MessageDTO.MessageType.VIDEO_FRAME:
                        HandleVideoFrame(msg);
                        break;

                    case MessageDTO.MessageType.AVATAR_UPDATE:
                        HandleIncomingAvatarUpdate(msg);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainUI] Lỗi xử lý tin nhắn: {ex.Message}");
            }
        }

        private void HandleIncomingAvatarUpdate(MessageDTO msg)
        {
            int senderId = msg.SenderId;
            string newAvatarBase64 = msg.PlainContent;

            if (_allUsers.ContainsKey(senderId))
            {
                _allUsers[senderId].AvatarBase64 = newAvatarBase64;
                
                // Cập nhật lại đối tác trò chuyện đang hoạt động nếu trùng khớp
                if (_activeChatUser != null && _activeChatUser.Id == senderId)
                {
                    _activeChatUser.AvatarBase64 = newAvatarBase64;
                    UpdateChatHeaderVisuals();
                }

                RenderUserList();
            }
        }

        private void UpdateUserList(string userListJson)
        {
            var list = JsonSerializer.Deserialize<List<UserDTO>>(userListJson, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (list == null) return;

            foreach (var user in list)
            {
                if (user.Id == _connection.LoggedInUser?.Id) continue;
                _allUsers[user.Id] = user;
            }

            RenderUserList();
        }

        private void RenderUserList()
        {
            pnlUserList.SuspendLayout();
            pnlUserList.Controls.Clear();

            // Sắp xếp online lên trước
            var sortedUsers = _allUsers.Values
                .OrderByDescending(u => u.Status == "ONLINE")
                .ThenBy(u => u.DisplayName)
                .ToList();

            foreach (var user in sortedUsers)
            {
                pnlUserList.Controls.Add(CreateUserItem(user));
            }

            pnlUserList.ResumeLayout();
            UpdateChatHeaderVisuals();
        }

        private Panel CreateUserItem(UserDTO user)
        {
            bool isActive = _activeChatUser?.Id == user.Id;
            bool isOnline = user.Status == "ONLINE";
            int width = Math.Max(236, pnlUserList.ClientSize.Width - 18);

            Panel item = new Panel
            {
                Size = new Size(width, 58),
                Margin = new Padding(8, 4, 8, 4),
                BackColor = isActive ? ChatTheme.Bg3 : ChatTheme.Bg1,
                Cursor = Cursors.Hand
            };

            item.Paint += (_, e) =>
            {
                if (isActive)
                {
                    using Brush accent = new SolidBrush(ChatTheme.Accent);
                    e.Graphics.FillRectangle(accent, 0, 9, 4, 40);
                }
            };

            Label avatar = new Label
            {
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(12, 11),
                Size = new Size(36, 36),
                BackColor = isActive ? ChatTheme.Accent : ChatTheme.Bg4,
                ForeColor = Color.White,
                Font = ChatTheme.Font(9F, FontStyle.Bold)
            };

            if (!string.IsNullOrEmpty(user.AvatarBase64))
            {
                try
                {
                    byte[] imgBytes = Convert.FromBase64String(user.AvatarBase64);
                    using (var ms = new MemoryStream(imgBytes))
                    {
                        using (var tempImg = Image.FromStream(ms))
                        {
                            avatar.Image = new Bitmap(tempImg, avatar.Size);
                        }
                    }
                    avatar.Text = "";
                }
                catch
                {
                    avatar.Text = ChatTheme.Initials(user.DisplayName);
                    avatar.Image = null;
                }
            }
            else
            {
                avatar.Text = ChatTheme.Initials(user.DisplayName);
                avatar.Image = null;
            }
            ChatTheme.ApplyRoundedRegion(avatar, 18);

            Panel statusDot = new Panel
            {
                Size = new Size(12, 12),
                Location = new Point(39, 36),
                BackColor = isOnline ? ChatTheme.Green : ChatTheme.Text3
            };
            ChatTheme.ApplyRoundedRegion(statusDot, 6);

            Label nameLabel = new Label
            {
                Text = user.DisplayName,
                Location = new Point(60, 10),
                Size = new Size(width - 76, 21),
                Font = ChatTheme.Font(9.5F, FontStyle.Bold),
                ForeColor = isOnline ? ChatTheme.Text0 : ChatTheme.Text2
            };

            Label statusLabel = new Label
            {
                Text = isOnline ? "Đang hoạt động" : "Offline",
                Location = new Point(60, 31),
                Size = new Size(width - 76, 18),
                Font = ChatTheme.Font(8F),
                ForeColor = isOnline ? ChatTheme.Green : ChatTheme.Text3
            };

            item.Controls.Add(avatar);
            item.Controls.Add(statusDot);
            item.Controls.Add(nameLabel);
            item.Controls.Add(statusLabel);

            item.MouseEnter += (_, _) =>
            {
                if (_activeChatUser?.Id != user.Id) item.BackColor = ChatTheme.Hover;
            };
            item.MouseLeave += (_, _) =>
            {
                if (_activeChatUser?.Id != user.Id) item.BackColor = ChatTheme.Bg1;
            };

            item.Click += (_, _) => SwitchChatUser(user);
            foreach (Control control in item.Controls)
            {
                control.Click += (_, _) => SwitchChatUser(user);
            }

            return item;
        }

        private void SwitchChatUser(UserDTO targetUser)
        {
            _activeChatUser = targetUser;
            RenderUserList();

            lblChatTarget.Text = targetUser.DisplayName;
            lblChatTargetStatus.Text = targetUser.Status == "ONLINE" ? "Online" : "Offline";
            lblChatTargetStatus.ForeColor = targetUser.Status == "ONLINE" ? ChatTheme.Green : ChatTheme.Text2;
            UpdateChatHeaderVisuals();

            // Phòng ảo
            int myId = _connection.LoggedInUser!.Id;
            _activeRoomId = Math.Min(myId, targetUser.Id) * 100_000 + Math.Max(myId, targetUser.Id);

            // Gửi tín hiệu JOIN_ROOM cho server
            var joinMsg = new MessageDTO(MessageDTO.MessageType.JOIN_ROOM)
            {
                RoomId = _activeRoomId,
                SenderId = myId
            };
            _connection.SendMessage(joinMsg);

            pnlMessages.Controls.Clear();
            txtMessage.Enabled = true;
            btnSend.Enabled = true;
            btnAttach.Enabled = true;
            btnVideoCall.Enabled = true;
            if (btnAudioCall != null) btnAudioCall.Enabled = true;

            // Load lịch sử local SQLite
            LoadLocalHistory();
        }

        private void LoadLocalHistory()
        {
            if (_historyDB == null) return;
            var history = _historyDB.GetHistory(_activeRoomId, 50);
            foreach (var row in history)
            {
                int senderId = (int)row[1];
                string senderName = (string)row[2];
                string content = (string)row[3];
                string type = (string)row[4];
                string fileName = (string)row[5];
                long ts = (long)row[6];
                bool isSentByMe = (bool)row[7];

                if (type == "TEXT")
                {
                    AddMessageBubble(content, senderName, isSentByMe, ts);
                }
                else if (type == "FILE")
                {
                    AddFileBubble(fileName, content, senderName, isSentByMe, ts);
                }
            }
        }

        private void HandleSystemMessage(MessageDTO msg)
        {
            // SYSTEM message định dạng: "DisplayName đã online!" hoặc "DisplayName đã offline."
            // Cập nhật trạng thái trong bộ nhớ
            if (msg.SenderId > 0 && msg.SenderId != _connection.LoggedInUser?.Id)
            {
                if (!_allUsers.TryGetValue(msg.SenderId, out var user))
                {
                    string displayName = msg.SenderUsername;
                    if (msg.PlainContent.EndsWith(" đã online!"))
                    {
                        displayName = msg.PlainContent.Substring(0, msg.PlainContent.Length - " đã online!".Length);
                    }
                    else if (msg.PlainContent.EndsWith(" đã offline."))
                    {
                        displayName = msg.PlainContent.Substring(0, msg.PlainContent.Length - " đã offline.".Length);
                    }

                    user = new UserDTO
                    {
                        Id = msg.SenderId,
                        Username = msg.SenderUsername,
                        DisplayName = displayName,
                        Status = "OFFLINE"
                    };
                    _allUsers[msg.SenderId] = user;
                }

                if (msg.PlainContent.Contains("online"))
                {
                    user.Status = "ONLINE";
                }
                else
                {
                    user.Status = "OFFLINE";
                }
                RenderUserList();

                // Cập nhật text trạng thái nếu đang chat
                if (_activeChatUser?.Id == user.Id)
                {
                    lblChatTargetStatus.Text = user.Status == "ONLINE" ? "Online" : "Offline";
                    lblChatTargetStatus.ForeColor = user.Status == "ONLINE" ? ChatTheme.Green : ChatTheme.Text2;
                }
            }
        }

        private void HandleIncomingText(MessageDTO msg)
        {
            string plaintext = _connection.DecryptMessage(msg.EncryptedContent);

            // Lưu SQLite cục bộ
            _historyDB?.SaveMessage(msg.RoomId, msg.SenderId, msg.SenderUsername, plaintext, "TEXT", "", msg.Timestamp, false);

            // Nếu thuộc phòng đang chat, vẽ lên UI
            if (msg.RoomId == _activeRoomId)
            {
                AddMessageBubble(plaintext, msg.SenderUsername, false, msg.Timestamp);
            }
        }

        private void HandleIncomingFile(MessageDTO msg)
        {
            // Dữ liệu file mã hóa nằm trong msg.EncryptedContent dạng Base64
            // Lưu SQLite cục bộ dạng Base64 (sau khi click tải mới giải mã để tránh dung lượng lớn trong RAM)
            _historyDB?.SaveMessage(msg.RoomId, msg.SenderId, msg.SenderUsername, msg.EncryptedContent, "FILE", msg.FileName, msg.Timestamp, false);

            if (msg.RoomId == _activeRoomId)
            {
                AddFileBubble(msg.FileName, msg.EncryptedContent, msg.SenderUsername, false, msg.Timestamp);
            }
        }

        private void AddMessageBubble(string text, string senderName, bool isSentByMe, long ts)
        {
            int targetWidth = Math.Max(240, pnlMessages.ClientSize.Width - 34);
            int maxBubbleWidth = Math.Max(190, Math.Min(460, (int)(targetWidth * 0.68)));

            Panel msgPanel = new Panel
            {
                Width = targetWidth,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 3, 0, 9)
            };

            Font messageFont = ChatTheme.Font(10F);
            Size proposedSize = TextRenderer.MeasureText(
                text,
                messageFont,
                new Size(maxBubbleWidth - 24, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl
            );

            int bubbleWidth = Math.Min(maxBubbleWidth, Math.Max(76, proposedSize.Width + 26));
            int bubbleHeight = Math.Max(38, proposedSize.Height + 20);

            Panel bubble = new Panel
            {
                BackColor = isSentByMe ? ChatTheme.Accent : ChatTheme.Bg3,
                ForeColor = Color.White,
                Size = new Size(bubbleWidth, bubbleHeight),
                Padding = new Padding(12, 8, 12, 8),
                Cursor = Cursors.Default,
                Tag = isSentByMe
            };
            if (!isSentByMe)
            {
                bubble.Paint += (_, e) => ChatTheme.PaintRoundedBorder(bubble, e, ChatTheme.Border, 18);
            }

            Label lblText = new Label
            {
                Text = text,
                ForeColor = isSentByMe ? Color.White : ChatTheme.Text0,
                Font = messageFont,
                AutoSize = false,
                Location = new Point(12, 8),
                Size = new Size(bubbleWidth - 24, bubbleHeight - 16)
            };

            bubble.Controls.Add(lblText);
            ChatTheme.ApplyRoundedRegion(bubble, 18);

            Label lblMeta = new Label
            {
                Text = isSentByMe
                    ? $"{DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime:HH:mm}  •  Bạn"
                    : $"{senderName}  •  {DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime:HH:mm}",
                ForeColor = ChatTheme.Text2,
                Font = ChatTheme.Font(7.7F),
                AutoSize = false,
                Size = new Size(bubbleWidth, 18),
                TextAlign = isSentByMe ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft
            };

            msgPanel.Height = bubble.Height + 22;
            int bubbleLeft = isSentByMe ? msgPanel.Width - bubble.Width - 12 : 12;
            lblMeta.Location = new Point(bubbleLeft + 2, 0);
            bubble.Location = new Point(bubbleLeft, 20);

            msgPanel.Controls.Add(lblMeta);
            msgPanel.Controls.Add(bubble);

            pnlMessages.Controls.Add(msgPanel);
            pnlMessages.ScrollControlIntoView(msgPanel);
        }

        private void AddFileBubble(string fileName, string base64EncryptedData, string senderName, bool isSentByMe, long ts)
        {
            int targetWidth = Math.Max(240, pnlMessages.ClientSize.Width - 34);

            Panel msgPanel = new Panel
            {
                Width = targetWidth,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 3, 0, 9),
                Height = 94
            };

            Panel bubble = new Panel
            {
                BackColor = isSentByMe ? ChatTheme.ColorBlend(ChatTheme.Accent, ChatTheme.Bg4, 0.28F) : ChatTheme.Bg3,
                Size = new Size(304, 70),
                ForeColor = Color.White,
                Padding = new Padding(10),
                Tag = isSentByMe
            };
            bubble.Paint += (_, e) => ChatTheme.PaintRoundedBorder(bubble, e, isSentByMe ? ChatTheme.AccentDim : ChatTheme.Border, 12);

            Label picIcon = new Label
            {
                Text = "📄",
                Font = ChatTheme.Font(18F),
                Location = new Point(12, 17),
                Size = new Size(34, 36),
                ForeColor = Color.White
            };

            Label lblName = new Label
            {
                Text = fileName.Length > 25 ? fileName.Substring(0, 22) + "..." : fileName,
                Location = new Point(54, 13),
                Size = new Size(230, 20),
                Font = ChatTheme.Font(9.5F, FontStyle.Bold),
                ForeColor = ChatTheme.Text0
            };

            Button btnDownload = new Button
            {
                Text = "TẢI VỀ",
                Location = new Point(54, 38),
                Size = new Size(76, 24),
                Font = ChatTheme.Font(7.5F, FontStyle.Bold)
            };
            ChatTheme.ApplyPrimaryButton(btnDownload, 8);
            btnDownload.Click += (s, e) => DownloadFile(fileName, base64EncryptedData);

            bubble.Controls.Add(picIcon);
            bubble.Controls.Add(lblName);
            if (!isSentByMe)
            {
                bubble.Controls.Add(btnDownload);
            }
            else
            {
                Label lblSent = new Label
                {
                    Text = "Đã gửi tệp",
                    Font = ChatTheme.Font(8F),
                    ForeColor = ChatTheme.Green,
                    Location = new Point(54, 39),
                    Size = new Size(120, 20)
                };
                bubble.Controls.Add(lblSent);
            }

            ChatTheme.ApplyRoundedRegion(bubble, 12);

            Label lblMeta = new Label
            {
                Text = isSentByMe
                    ? $"{DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime:HH:mm}  •  Bạn"
                    : $"{senderName}  •  {DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime:HH:mm}",
                ForeColor = ChatTheme.Text2,
                Font = ChatTheme.Font(7.7F),
                AutoSize = false,
                Size = new Size(bubble.Width, 18),
                TextAlign = isSentByMe ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft
            };

            int bubbleLeft = isSentByMe ? msgPanel.Width - bubble.Width - 12 : 12;
            lblMeta.Location = new Point(bubbleLeft + 2, 0);
            bubble.Location = new Point(bubbleLeft, 20);

            msgPanel.Controls.Add(lblMeta);
            msgPanel.Controls.Add(bubble);

            pnlMessages.Controls.Add(msgPanel);
            pnlMessages.ScrollControlIntoView(msgPanel);
        }

        private void DownloadFile(string fileName, string base64EncryptedData)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = fileName;
                sfd.Filter = "All Files (*.*)|*.*";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        byte[]? decryptedBytes = _connection.DecryptFile(base64EncryptedData);
                        if (decryptedBytes != null)
                        {
                            File.WriteAllBytes(sfd.FileName, decryptedBytes);
                            MessageBox.Show("Tải và giải mã file thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Giải mã file thất bại! Khóa phiên không khớp.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi lưu file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            string text = txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(text) || _activeRoomId == -1) return;

            var currentUser = _connection.LoggedInUser;
            if (currentUser == null) return;
            int roomId = _activeRoomId;

            txtMessage.Clear();
            btnSend.Enabled = false;

            var sendThread = new Thread(() =>
            {
                try
                {
                    _connection.SendText(roomId, text);
                    long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    RunOnUiThread(() =>
                    {
                        _historyDB?.SaveMessage(roomId, currentUser.Id, currentUser.Username, text, "TEXT", "", ts, true);
                        AddMessageBubble(text, currentUser.Username, true, ts);
                        btnSend.Enabled = true;
                        txtMessage.Focus();
                    });
                }
                catch (Exception ex)
                {
                    RunOnUiThread(() =>
                    {
                        MessageBox.Show($"Không thể gửi tin nhắn: {ex.Message}", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        btnSend.Enabled = true;
                        txtMessage.Focus();
                    });
                }
            })
            {
                IsBackground = true,
                Name = "SendMessage"
            };
            sendThread.Start();
        }

        private void btnAttach_Click(object sender, EventArgs e)
        {
            if (_activeRoomId == -1) return;

            var currentUser = _connection.LoggedInUser;
            if (currentUser == null) return;
            int roomId = _activeRoomId;

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "All Files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string filePath = ofd.FileName;
                    string fileName = Path.GetFileName(filePath);

                    lblChatTargetStatus.Text = "Đang gửi tệp tin...";
                    lblChatTargetStatus.ForeColor = ChatTheme.Yellow;
                    btnAttach.Enabled = false;

                    var fileThread = new Thread(() =>
                    {
                        try
                        {
                            byte[] fileBytes = File.ReadAllBytes(filePath);
                            _connection.SendFile(roomId, fileName, fileBytes);

                            long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            byte[] encryptedFileBytes = AESUtil.EncryptBytes(fileBytes, _connection.AesKey!);
                            string base64Encrypted = Convert.ToBase64String(encryptedFileBytes);

                            RunOnUiThread(() =>
                            {
                                _historyDB?.SaveMessage(roomId, currentUser.Id, currentUser.Username, base64Encrypted, "FILE", fileName, ts, true);
                                AddFileBubble(fileName, base64Encrypted, currentUser.Username, true, ts);
                                lblChatTargetStatus.Text = "Online";
                                lblChatTargetStatus.ForeColor = ChatTheme.Green;
                                btnAttach.Enabled = true;
                            });
                        }
                        catch (Exception ex)
                        {
                            RunOnUiThread(() =>
                            {
                                MessageBox.Show($"Gửi file thất bại: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                lblChatTargetStatus.Text = "Online";
                                lblChatTargetStatus.ForeColor = ChatTheme.Green;
                                btnAttach.Enabled = true;
                            });
                        }
                    })
                    {
                        IsBackground = true,
                        Name = "SendFile"
                    };
                    fileThread.Start();
                }
            }
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(action);
        }

        private void btnHistory_Click(object sender, EventArgs e)
        {
            if (_historyDB == null) return;
            using (HistoryForm hForm = new HistoryForm(_historyDB))
            {
                hForm.ShowDialog(this);
            }
        }

        private void btnVideoCall_Click(object sender, EventArgs e)
        {
            if (_activeChatUser == null || _activeRoomId == -1) return;

            // Mở Form ở chế độ OUTGOING với isVideo = true
            _activeVideoCallForm = new VideoCallForm(_connection, _activeRoomId, _activeChatUser.Id, _activeChatUser.DisplayName, true, true);
            
            // Gửi tin nhắn VIDEO_INVITE
            var inviteMsg = new MessageDTO(MessageDTO.MessageType.VIDEO_INVITE)
            {
                RoomId = _activeRoomId,
                TargetUserId = _activeChatUser.Id,
                SenderId = _connection.LoggedInUser!.Id,
                SenderUsername = _connection.LoggedInUser.Username,
                PlainContent = "VIDEO"
            };
            _connection.SendMessage(inviteMsg);

            _activeVideoCallForm.Show(this);
        }

        private void btnAudioCall_Click(object? sender, EventArgs e)
        {
            if (_activeChatUser == null || _activeRoomId == -1) return;

            // Mở Form ở chế độ OUTGOING với isVideo = false
            _activeVideoCallForm = new VideoCallForm(_connection, _activeRoomId, _activeChatUser.Id, _activeChatUser.DisplayName, true, false);
            
            // Gửi tin nhắn VIDEO_INVITE với PlainContent = "AUDIO"
            var inviteMsg = new MessageDTO(MessageDTO.MessageType.VIDEO_INVITE)
            {
                RoomId = _activeRoomId,
                TargetUserId = _activeChatUser.Id,
                SenderId = _connection.LoggedInUser!.Id,
                SenderUsername = _connection.LoggedInUser.Username,
                PlainContent = "AUDIO"
            };
            _connection.SendMessage(inviteMsg);

            _activeVideoCallForm.Show(this);
        }

        private void HandleVideoInvite(MessageDTO msg)
        {
            if (_activeVideoCallForm != null || _pendingVideoInvite != null)
            {
                // Trả về bận nếu đang trong cuộc gọi khác
                SendVideoResponse(MessageDTO.MessageType.VIDEO_REJECT, msg);
                return;
            }

            ShowIncomingVideoCallPopup(msg);
        }

        private void AcceptIncomingVideoCall()
        {
            if (_pendingVideoInvite == null) return;

            MessageDTO invite = _pendingVideoInvite;
            SendVideoResponse(MessageDTO.MessageType.VIDEO_ACCEPT, invite);
            HideIncomingVideoCallPopup();

            string callerName = GetDisplayNameForUser(invite.SenderId, invite.SenderUsername);
            bool isVideo = invite.PlainContent != "AUDIO";
            _activeVideoCallForm = new VideoCallForm(_connection, invite.RoomId, invite.SenderId, callerName, false, isVideo);
            _activeVideoCallForm.Show(this);
        }

        private void DeclineIncomingVideoCall()
        {
            if (_pendingVideoInvite == null) return;

            SendVideoResponse(MessageDTO.MessageType.VIDEO_REJECT, _pendingVideoInvite);
            HideIncomingVideoCallPopup();
        }

        private void SendVideoResponse(MessageDTO.MessageType responseType, MessageDTO source)
        {
            var currentUser = _connection.LoggedInUser;
            if (currentUser == null) return;

            var response = new MessageDTO(responseType)
            {
                RoomId = source.RoomId,
                TargetUserId = source.SenderId,
                SenderId = currentUser.Id,
                SenderUsername = currentUser.Username
            };
            _connection.SendMessage(response);
        }

        private void HandleVideoAccept(MessageDTO msg)
        {
            if (_activeVideoCallForm != null)
            {
                _activeVideoCallForm.StartCall();
            }
        }

        private void HandleVideoReject(MessageDTO msg)
        {
            if (_activeVideoCallForm != null)
            {
                MessageBox.Show("Cuộc gọi bị từ chối bởi đối phương.", "Từ chối cuộc gọi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _activeVideoCallForm.HangUp(false);
                _activeVideoCallForm = null;
            }
        }

        private void HandleVideoHangUp(MessageDTO msg)
        {
            HideIncomingVideoCallPopup();

            if (_activeVideoCallForm != null)
            {
                _activeVideoCallForm.HangUp(false);
                _activeVideoCallForm = null;
            }
        }

        private void HandleVideoFrame(MessageDTO msg)
        {
            if (_activeVideoCallForm != null)
            {
                // Giải mã khung hình bằng khóa AES đối xứng người nhận
                string decryptedBase64 = _connection.DecryptMessage(msg.EncryptedContent);
                _activeVideoCallForm.OnFrameReceived(decryptedBase64);
            }
        }

        private void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // Ngăn tiếng bip mặc định
                btnSend.PerformClick();
            }
        }

        private void ResizeMessages()
        {
            if (pnlMessages == null) return;
            pnlMessages.SuspendLayout();
            int targetWidth = Math.Max(240, pnlMessages.ClientSize.Width - 34);

            foreach (Control control in pnlMessages.Controls)
            {
                if (control is Panel msgPanel)
                {
                    msgPanel.Width = targetWidth;

                    Panel? bubble = null;
                    Label? lblMeta = null;
                    foreach (Control child in msgPanel.Controls)
                    {
                        if (child is Panel p) bubble = p;
                        else if (child is Label l) lblMeta = l;
                    }

                    if (bubble != null && lblMeta != null)
                    {
                        bool isSentByMe = bubble.Tag is bool own && own;
                        if (isSentByMe)
                        {
                            bubble.Location = new Point(msgPanel.Width - bubble.Width - 12, bubble.Location.Y);
                            lblMeta.Location = new Point(bubble.Left + 2, lblMeta.Location.Y);
                        }
                        else
                        {
                            bubble.Location = new Point(12, bubble.Location.Y);
                            lblMeta.Location = new Point(bubble.Left + 2, lblMeta.Location.Y);
                        }
                    }
                }
            }
            pnlMessages.ResumeLayout();
        }
    }
}
