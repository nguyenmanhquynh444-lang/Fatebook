using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
            this.BackColor = Color.FromArgb(30, 30, 36); // #1E1E24
            this.ForeColor = Color.FromArgb(226, 226, 226);
            this.Font = new Font("Segoe UI", 9F);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Sidebar styling
            pnlSidebar.BackColor = Color.FromArgb(42, 43, 54); // #2A2B36
            lblCurrentUser.Text = $"Chào, {_connection.LoggedInUser?.DisplayName ?? _connection.LoggedInUser?.Username}";
            lblCurrentUser.ForeColor = Color.FromArgb(108, 92, 231); // Accent Color

            // Main chat panel styling
            pnlChatHeader.BackColor = Color.FromArgb(42, 43, 54);
            pnlInput.BackColor = Color.FromArgb(42, 43, 54);
            txtMessage.BackColor = Color.FromArgb(30, 30, 36);
            txtMessage.ForeColor = Color.FromArgb(226, 226, 226);
            txtMessage.BorderStyle = BorderStyle.FixedSingle;

            btnSend.BackColor = Color.FromArgb(108, 92, 231);
            btnSend.ForeColor = Color.White;
            btnSend.FlatStyle = FlatStyle.Flat;
            btnSend.FlatAppearance.BorderSize = 0;

            btnAttach.BackColor = Color.FromArgb(58, 59, 69);
            btnAttach.ForeColor = Color.White;
            btnAttach.FlatStyle = FlatStyle.Flat;
            btnAttach.FlatAppearance.BorderSize = 0;

            btnHistory.BackColor = Color.FromArgb(58, 59, 69);
            btnHistory.ForeColor = Color.White;
            btnHistory.FlatStyle = FlatStyle.Flat;
            btnHistory.FlatAppearance.BorderSize = 0;

            // Set MinimumSize to ensure visual components don't overlap when resized
            this.MinimumSize = new Size(800, 500);

            // Đăng ký sự kiện co dãn cửa sổ
            pnlMessages.SizeChanged += (s, e) => ResizeMessages();

            // Set DoubleBuffered on chat panel to prevent flickering
            typeof(Panel).GetProperty("DoubleBuffered", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(pnlMessages, true, null);
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
            lblSecureIndicator.Text = "🔒 Đầu cuối mã hóa AES-256-GCM + SSL/TLS 1.3";
            lblSecureIndicator.ForeColor = Color.FromArgb(46, 204, 113); // Green
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
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainUI] Lỗi xử lý tin nhắn: {ex.Message}");
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
            pnlUserList.Controls.Clear();
            int top = 5;

            // Sắp xếp online lên trước
            var sortedUsers = _allUsers.Values
                .OrderByDescending(u => u.Status == "ONLINE")
                .ThenBy(u => u.DisplayName)
                .ToList();

            foreach (var user in sortedUsers)
            {
                Panel uPanel = new Panel
                {
                    Size = new Size(pnlUserList.Width - 10, 50),
                    Location = new Point(5, top),
                    BackColor = _activeChatUser?.Id == user.Id ? Color.FromArgb(58, 59, 69) : Color.Transparent,
                    Cursor = Cursors.Hand
                };

                // Tròn trạng thái (Online/Offline)
                Panel statusDot = new Panel
                {
                    Size = new Size(10, 10),
                    Location = new Point(15, 20),
                    BackColor = user.Status == "ONLINE" ? Color.FromArgb(46, 204, 113) : Color.FromArgb(149, 165, 166)
                };

                // Bo tròn chấm status
                GraphicsPath path = new GraphicsPath();
                path.AddEllipse(0, 0, 10, 10);
                statusDot.Region = new Region(path);

                // Tên hiển thị
                Label nameLabel = new Label
                {
                    Text = user.DisplayName,
                    Location = new Point(35, 16),
                    Size = new Size(160, 20),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = user.Status == "ONLINE" ? Color.White : Color.FromArgb(180, 180, 180)
                };

                uPanel.Controls.Add(statusDot);
                uPanel.Controls.Add(nameLabel);

                // Hover transitions
                uPanel.MouseEnter += (s, e) => {
                    if (_activeChatUser?.Id != user.Id)
                        uPanel.BackColor = Color.FromArgb(50, 51, 62);
                };
                uPanel.MouseLeave += (s, e) => {
                    if (_activeChatUser?.Id != user.Id)
                        uPanel.BackColor = Color.Transparent;
                };

                // Click chọn user chat
                uPanel.Click += (s, e) => SwitchChatUser(user);
                foreach (Control control in uPanel.Controls)
                {
                    control.Click += (s, e) => SwitchChatUser(user);
                }

                pnlUserList.Controls.Add(uPanel);
                top += 55;
            }
        }

        private void SwitchChatUser(UserDTO targetUser)
        {
            _activeChatUser = targetUser;
            RenderUserList();

            lblChatTarget.Text = targetUser.DisplayName;
            lblChatTargetStatus.Text = targetUser.Status == "ONLINE" ? "Online" : "Offline";
            lblChatTargetStatus.ForeColor = targetUser.Status == "ONLINE" ? Color.FromArgb(46, 204, 113) : Color.FromArgb(149, 165, 166);

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
                    lblChatTargetStatus.ForeColor = user.Status == "ONLINE" ? Color.FromArgb(46, 204, 113) : Color.FromArgb(149, 165, 166);
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
            int targetWidth = pnlMessages.ClientSize.Width - 25;
            if (targetWidth < 200) targetWidth = pnlMessages.Width - 40;
            if (targetWidth < 200) targetWidth = 200;

            // Tạo Panel tin nhắn
            Panel msgPanel = new Panel
            {
                Width = targetWidth,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 5, 0, 5)
            };

            // Vẽ custom bong bóng chat
            Panel bubble = new Panel
            {
                BackColor = isSentByMe ? Color.FromArgb(108, 92, 231) : Color.FromArgb(58, 59, 69), // Tím vs Xám
                ForeColor = Color.White,
                Padding = new Padding(12, 10, 12, 10),
                Cursor = Cursors.Default
            };

            Label lblText = new Label
            {
                Text = text,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                MaximumSize = new Size(350, 0)
            };

            // Đo độ rộng chữ để tự chỉnh kích thước bubble
            Size proposedSize = TextRenderer.MeasureText(text, lblText.Font, new Size(350, 0), TextFormatFlags.WordBreak);
            bubble.Size = new Size(Math.Max(proposedSize.Width + 25, 60), proposedSize.Height + 20);
            bubble.Controls.Add(lblText);

            // Bo góc tròn cho bong bóng chat bằng GDI+
            GraphicsPath path = new GraphicsPath();
            int r = 12; // Bán kính bo góc
            path.AddArc(0, 0, r, r, 180, 90);
            path.AddArc(bubble.Width - r, 0, r, r, 270, 90);
            path.AddArc(bubble.Width - r, bubble.Height - r, r, r, 0, 90);
            path.AddArc(0, bubble.Height - r, r, r, 90, 90);
            path.CloseAllFigures();
            bubble.Region = new Region(path);

            Label lblMeta = new Label
            {
                Text = $"{DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime:HH:mm}",
                ForeColor = Color.FromArgb(142, 146, 151),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                AutoSize = true
            };

            msgPanel.Height = bubble.Height + 15;

            if (isSentByMe)
            {
                bubble.Location = new Point(msgPanel.Width - bubble.Width - 10, 0);
                lblMeta.Location = new Point(msgPanel.Width - 50, bubble.Height + 1);
            }
            else
            {
                bubble.Location = new Point(10, 0);
                lblMeta.Location = new Point(15, bubble.Height + 1);
            }

            msgPanel.Controls.Add(bubble);
            msgPanel.Controls.Add(lblMeta);

            pnlMessages.Controls.Add(msgPanel);
            pnlMessages.ScrollControlIntoView(msgPanel);
        }

        private void AddFileBubble(string fileName, string base64EncryptedData, string senderName, bool isSentByMe, long ts)
        {
            int targetWidth = pnlMessages.ClientSize.Width - 25;
            if (targetWidth < 200) targetWidth = pnlMessages.Width - 40;
            if (targetWidth < 200) targetWidth = 200;

            Panel msgPanel = new Panel
            {
                Width = targetWidth,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 5, 0, 5),
                Height = 85
            };

            Panel bubble = new Panel
            {
                BackColor = isSentByMe ? Color.FromArgb(39, 174, 96) : Color.FromArgb(44, 62, 80), // Xanh lá vs Xanh đen
                Size = new Size(280, 65),
                ForeColor = Color.White,
                Padding = new Padding(10)
            };

            // Icon file đính kèm
            Label picIcon = new Label
            {
                Text = "📄",
                Font = new Font("Segoe UI", 18F),
                Location = new Point(10, 15),
                Size = new Size(30, 35),
                ForeColor = Color.White
            };

            Label lblName = new Label
            {
                Text = fileName.Length > 22 ? fileName.Substring(0, 19) + "..." : fileName,
                Location = new Point(45, 12),
                Size = new Size(220, 18),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White
            };

            // Nút tải file
            Button btnDownload = new Button
            {
                Text = "TẢI VỀ",
                Location = new Point(45, 33),
                Size = new Size(80, 25),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold)
            };
            btnDownload.FlatAppearance.BorderSize = 0;
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
                    Font = new Font("Segoe UI", 8f, FontStyle.Italic),
                    ForeColor = Color.FromArgb(200, 247, 197),
                    Location = new Point(45, 35),
                    Size = new Size(100, 20)
                };
                bubble.Controls.Add(lblSent);
            }

            // Bo góc tròn bubble
            GraphicsPath path = new GraphicsPath();
            int r = 12;
            path.AddArc(0, 0, r, r, 180, 90);
            path.AddArc(bubble.Width - r, 0, r, r, 270, 90);
            path.AddArc(bubble.Width - r, bubble.Height - r, r, r, 0, 90);
            path.AddArc(0, bubble.Height - r, r, r, 90, 90);
            path.CloseAllFigures();
            bubble.Region = new Region(path);

            Label lblMeta = new Label
            {
                Text = $"{DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime:HH:mm}",
                ForeColor = Color.FromArgb(142, 146, 151),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                AutoSize = true
            };

            if (isSentByMe)
            {
                bubble.Location = new Point(msgPanel.Width - bubble.Width - 10, 0);
                lblMeta.Location = new Point(msgPanel.Width - 50, bubble.Height + 1);
            }
            else
            {
                bubble.Location = new Point(10, 0);
                lblMeta.Location = new Point(15, bubble.Height + 1);
            }

            msgPanel.Controls.Add(bubble);
            msgPanel.Controls.Add(lblMeta);

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

        private async void btnSend_Click(object sender, EventArgs e)
        {
            string text = txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(text) || _activeRoomId == -1) return;

            txtMessage.Clear();
            btnSend.Enabled = false;

            try
            {
                // Gửi tin nhắn được mã hóa
                await _connection.SendTextAsync(_activeRoomId, text);

                long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Lưu SQLite cục bộ dạng plaintext
                _historyDB?.SaveMessage(_activeRoomId, _connection.LoggedInUser!.Id, _connection.LoggedInUser.Username, text, "TEXT", "", ts, true);

                // Thêm vào UI phía mình gửi
                AddMessageBubble(text, _connection.LoggedInUser.Username, true, ts);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể gửi tin nhắn: {ex.Message}", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSend.Enabled = true;
                txtMessage.Focus();
            }
        }

        private async void btnAttach_Click(object sender, EventArgs e)
        {
            if (_activeRoomId == -1) return;

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "All Files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string filePath = ofd.FileName;
                    string fileName = Path.GetFileName(filePath);

                    try
                    {
                        byte[] fileBytes = File.ReadAllBytes(filePath);

                        // Hiển thị trạng thái gửi
                        lblChatTargetStatus.Text = "Đang gửi tệp tin...";
                        lblChatTargetStatus.ForeColor = Color.FromArgb(241, 196, 15);

                        await _connection.SendFileAsync(_activeRoomId, fileName, fileBytes);

                        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        // Mã hóa tệp tin cục bộ để hiển thị/lưu SQLite
                        byte[] encryptedFileBytes = AESUtil.EncryptBytes(fileBytes, _connection.AesKey!);
                        string base64Encrypted = Convert.ToBase64String(encryptedFileBytes);

                        _historyDB?.SaveMessage(_activeRoomId, _connection.LoggedInUser!.Id, _connection.LoggedInUser.Username, base64Encrypted, "FILE", fileName, ts, true);

                        // Thêm vào UI phía mình
                        AddFileBubble(fileName, base64Encrypted, _connection.LoggedInUser.Username, true, ts);

                        lblChatTargetStatus.Text = "Online";
                        lblChatTargetStatus.ForeColor = Color.FromArgb(46, 204, 113);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gửi file thất bại: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        
                        lblChatTargetStatus.Text = "Online";
                        lblChatTargetStatus.ForeColor = Color.FromArgb(46, 204, 113);
                    }
                }
            }
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

            // Mở Form ở chế độ OUTGOING
            _activeVideoCallForm = new VideoCallForm(_connection, _activeRoomId, _activeChatUser.Id, _activeChatUser.DisplayName, true);
            
            // Gửi tin nhắn VIDEO_INVITE
            var inviteMsg = new MessageDTO(MessageDTO.MessageType.VIDEO_INVITE)
            {
                RoomId = _activeRoomId,
                SenderId = _connection.LoggedInUser!.Id,
                SenderUsername = _connection.LoggedInUser.Username
            };
            _connection.SendMessage(inviteMsg);

            _activeVideoCallForm.Show(this);
        }

        private void HandleVideoInvite(MessageDTO msg)
        {
            if (_activeVideoCallForm != null)
            {
                // Trả về bận nếu đang trong cuộc gọi khác
                var reject = new MessageDTO(MessageDTO.MessageType.VIDEO_REJECT)
                {
                    RoomId = msg.RoomId,
                    SenderId = _connection.LoggedInUser!.Id,
                    SenderUsername = _connection.LoggedInUser.Username
                };
                _connection.SendMessage(reject);
                return;
            }

            DialogResult dr = MessageBox.Show(
                $"Người dùng {msg.SenderUsername} đang gọi video cho bạn. Đồng ý nhận cuộc gọi?",
                "Cuộc gọi video đến",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (dr == DialogResult.Yes)
            {
                var accept = new MessageDTO(MessageDTO.MessageType.VIDEO_ACCEPT)
                {
                    RoomId = msg.RoomId,
                    SenderId = _connection.LoggedInUser!.Id,
                    SenderUsername = _connection.LoggedInUser.Username
                };
                _connection.SendMessage(accept);

                // Mở Form ở chế độ CONNECTED (đáp ứng)
                _activeVideoCallForm = new VideoCallForm(_connection, msg.RoomId, msg.SenderId, msg.SenderUsername, false);
                _activeVideoCallForm.Show(this);
            }
            else
            {
                var reject = new MessageDTO(MessageDTO.MessageType.VIDEO_REJECT)
                {
                    RoomId = msg.RoomId,
                    SenderId = _connection.LoggedInUser!.Id,
                    SenderUsername = _connection.LoggedInUser.Username
                };
                _connection.SendMessage(reject);
            }
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
            int targetWidth = pnlMessages.ClientSize.Width - 25;
            if (targetWidth < 200) targetWidth = 200;

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
                        bool isSentByMe = bubble.BackColor == Color.FromArgb(108, 92, 231) || bubble.BackColor == Color.FromArgb(39, 174, 96);
                        if (isSentByMe)
                        {
                            bubble.Location = new Point(msgPanel.Width - bubble.Width - 10, bubble.Location.Y);
                            lblMeta.Location = new Point(msgPanel.Width - 50, lblMeta.Location.Y);
                        }
                        else
                        {
                            bubble.Location = new Point(10, bubble.Location.Y);
                            lblMeta.Location = new Point(15, lblMeta.Location.Y);
                        }
                    }
                }
            }
            pnlMessages.ResumeLayout();
        }
    }
}
