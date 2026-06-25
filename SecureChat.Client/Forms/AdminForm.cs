using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using SecureChat.Common.DTO;

namespace SecureChat.Client.Forms
{
    public sealed class AdminForm : Form
    {
        private readonly SecureConnection _connection;
        private readonly DataGridView _userGrid = new();
        private readonly TextBox _usernameTextBox = new();
        private readonly TextBox _displayNameTextBox = new();
        private readonly TextBox _passwordTextBox = new();
        private readonly Button _createButton = new();
        private readonly Button _refreshButton = new();
        private readonly Button _toggleActiveButton = new();
        private readonly Button _deleteButton = new();
        private readonly Label _statusLabel = new();
        private readonly Label _selectedUserLabel = new();
        private readonly Button _updateButton = new();
        private readonly Button _clearFormButton = new();

        public AdminForm(SecureConnection connection)
        {
            _connection = connection;
            BuildInterface();

            _connection.MessageReceived += OnMessageReceived;
            _connection.ConnectionError += OnConnectionError;
            _connection.StartReceiveLoop();

            Shown += (_, _) => _connection.RequestAdminUserList();
            FormClosing += (_, _) =>
            {
                _connection.MessageReceived -= OnMessageReceived;
                _connection.ConnectionError -= OnConnectionError;
                _connection.Disconnect();
            };
        }

        private void BuildInterface()
        {
            Text = "Fatebook - Quản trị người dùng";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 620);
            Size = new Size(1120, 700);
            BackColor = ChatTheme.Bg0;
            ForeColor = ChatTheme.Text0;
            Font = ChatTheme.Font(9F);

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 74,
                BackColor = ChatTheme.Bg1,
                Padding = new Padding(22, 12, 22, 10)
            };

            var titleLabel = new Label
            {
                AutoSize = true,
                Text = "QUẢN TRỊ NGƯỜI DÙNG",
                ForeColor = ChatTheme.Text0,
                Font = ChatTheme.Font(17F, FontStyle.Bold),
                Location = new Point(22, 11)
            };

            var accountLabel = new Label
            {
                AutoSize = true,
                Text = $"Đăng nhập: {_connection.LoggedInUser?.Username}  •  ADMIN",
                ForeColor = ChatTheme.Green,
                Font = ChatTheme.Font(8.5F, FontStyle.Bold),
                Location = new Point(24, 44)
            };

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(accountLabel);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = 300,
                SplitterWidth = 1,
                BackColor = ChatTheme.Border,
                Padding = new Padding(0)
            };

            BuildCreateUserPanel(split.Panel1);
            BuildUserListPanel(split.Panel2);

            Controls.Add(headerPanel);
            Controls.Add(split);
            split.SplitterDistance = 300;
        }

        private void BuildCreateUserPanel(Control parent)
        {
            parent.BackColor = ChatTheme.Bg1;
            parent.Padding = new Padding(22);

            var heading = new Label
            {
                Text = "QUẢN LÝ THÔNG TIN USER",
                AutoSize = true,
                Font = ChatTheme.Font(11F, FontStyle.Bold),
                ForeColor = ChatTheme.Text0,
                Location = new Point(22, 25)
            };

            AddField(parent, "TÊN ĐĂNG NHẬP", _usernameTextBox, 72);
            AddField(parent, "TÊN HIỂN THỊ", _displayNameTextBox, 148);
            AddField(parent, "MẬT KHẨU", _passwordTextBox, 224);

            _passwordTextBox.UseSystemPasswordChar = true;
            _passwordTextBox.AutoCompleteMode = AutoCompleteMode.None;
            _passwordTextBox.AutoCompleteSource = AutoCompleteSource.None;

            _createButton.Text = "TẠO USER";
            _createButton.SetBounds(22, 310, 120, 42);
            ChatTheme.ApplyPrimaryButton(_createButton, 8);
            _createButton.Click += CreateButton_Click;

            _updateButton.Text = "CẬP NHẬT";
            _updateButton.SetBounds(156, 310, 120, 42);
            ChatTheme.ApplyPrimaryButton(_updateButton, 8);
            _updateButton.Click += UpdateButton_Click;
            _updateButton.Enabled = false;

            _clearFormButton.Text = "LÀM SẠCH FORM / TẠO MỚI";
            _clearFormButton.SetBounds(22, 360, 254, 34);
            ChatTheme.ApplyNeutralButton(_clearFormButton, 7);
            _clearFormButton.Click += ClearFormButton_Click;

            var note = new Label
            {
                Text = "Mật khẩu tối thiểu 6 ký tự.\r\nĐể trống mật khẩu nếu không muốn đổi.",
                ForeColor = ChatTheme.Text2,
                Font = ChatTheme.Font(8.5F),
                Location = new Point(22, 404),
                Size = new Size(254, 44)
            };

            parent.Controls.Add(heading);
            parent.Controls.Add(_createButton);
            parent.Controls.Add(_updateButton);
            parent.Controls.Add(_clearFormButton);
            parent.Controls.Add(note);
        }

        private static void AddField(Control parent, string labelText, TextBox textBox, int top)
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                ForeColor = ChatTheme.Text2,
                Font = ChatTheme.Font(8F, FontStyle.Bold),
                Location = new Point(22, top)
            };

            textBox.SetBounds(22, top + 23, 254, 30);
            ChatTheme.ApplyTextBox(textBox);
            textBox.Padding = new Padding(8, 0, 8, 0);

            var fieldPanel = new Panel
            {
                BackColor = ChatTheme.Bg4,
                Location = new Point(22, top + 21),
                Size = new Size(254, 34)
            };
            ChatTheme.ApplyRoundedRegion(fieldPanel, 7);
            fieldPanel.Controls.Add(textBox);
            textBox.SetBounds(9, 7, 236, 22);

            parent.Controls.Add(label);
            parent.Controls.Add(fieldPanel);
        }

        private void BuildUserListPanel(Control parent)
        {
            parent.BackColor = ChatTheme.Bg2;
            parent.Padding = new Padding(20);

            var toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 74,
                BackColor = ChatTheme.Bg2
            };

            var heading = new Label
            {
                Text = "DANH SÁCH TÀI KHOẢN",
                AutoSize = true,
                Font = ChatTheme.Font(11F, FontStyle.Bold),
                ForeColor = ChatTheme.Text0,
                Location = new Point(0, 4)
            };

            _selectedUserLabel.Text = "Chọn một user để quản lý";
            _selectedUserLabel.ForeColor = ChatTheme.Text2;
            _selectedUserLabel.Location = new Point(0, 32);
            _selectedUserLabel.Size = new Size(360, 22);

            _refreshButton.Text = "LÀM MỚI";
            _refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _refreshButton.Size = new Size(92, 34);
            _refreshButton.Location = new Point(parent.ClientSize.Width - 92, 7);
            ChatTheme.ApplyNeutralButton(_refreshButton, 7);
            _refreshButton.Click += (_, _) => _connection.RequestAdminUserList();
            toolbar.Resize += (_, _) => _refreshButton.Left = Math.Max(0, toolbar.ClientSize.Width - _refreshButton.Width);

            toolbar.Controls.Add(heading);
            toolbar.Controls.Add(_selectedUserLabel);
            toolbar.Controls.Add(_refreshButton);

            ConfigureUserGrid();

            var actions = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 78,
                BackColor = ChatTheme.Bg2
            };

            _toggleActiveButton.Text = "VÔ HIỆU HÓA";
            _toggleActiveButton.SetBounds(0, 12, 132, 40);
            _toggleActiveButton.Enabled = false;
            ChatTheme.ApplyNeutralButton(_toggleActiveButton, 8);
            _toggleActiveButton.Click += ToggleActiveButton_Click;

            _deleteButton.Text = "XÓA USER";
            _deleteButton.SetBounds(144, 12, 110, 40);
            _deleteButton.Enabled = false;
            ChatTheme.ApplyDangerButton(_deleteButton, 8);
            _deleteButton.Click += DeleteButton_Click;

            _statusLabel.AutoEllipsis = true;
            _statusLabel.ForeColor = ChatTheme.Text2;
            _statusLabel.TextAlign = ContentAlignment.MiddleRight;
            _statusLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            _statusLabel.SetBounds(270, 12, Math.Max(180, parent.ClientSize.Width - 270), 40);
            actions.Resize += (_, _) =>
                _statusLabel.Width = Math.Max(180, actions.ClientSize.Width - _statusLabel.Left);

            actions.Controls.Add(_toggleActiveButton);
            actions.Controls.Add(_deleteButton);
            actions.Controls.Add(_statusLabel);

            parent.Controls.Add(toolbar);
            parent.Controls.Add(actions);
            parent.Controls.Add(_userGrid);
        }

        private void ConfigureUserGrid()
        {
            _userGrid.Dock = DockStyle.Fill;
            _userGrid.AutoGenerateColumns = false;
            _userGrid.AllowUserToAddRows = false;
            _userGrid.AllowUserToDeleteRows = false;
            _userGrid.AllowUserToResizeRows = false;
            _userGrid.MultiSelect = false;
            _userGrid.ReadOnly = true;
            _userGrid.RowHeadersVisible = false;
            _userGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _userGrid.BackgroundColor = ChatTheme.Bg2;
            _userGrid.BorderStyle = BorderStyle.None;
            _userGrid.GridColor = ChatTheme.Border;
            _userGrid.EnableHeadersVisualStyles = false;
            _userGrid.ColumnHeadersHeight = 38;
            _userGrid.RowTemplate.Height = 42;
            _userGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ChatTheme.Bg3,
                ForeColor = ChatTheme.Text1,
                Font = ChatTheme.Font(8F, FontStyle.Bold),
                SelectionBackColor = ChatTheme.Bg3
            };
            _userGrid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = ChatTheme.Bg2,
                ForeColor = ChatTheme.Text0,
                SelectionBackColor = ChatTheme.AccentDim,
                SelectionForeColor = Color.White,
                Font = ChatTheme.Font(9F)
            };
            _userGrid.AlternatingRowsDefaultCellStyle.BackColor = ChatTheme.Bg1;

            _userGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(UserDTO.Id),
                HeaderText = "ID",
                Width = 55
            });
            _userGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(UserDTO.Username),
                HeaderText = "TÀI KHOẢN",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 115
            });
            _userGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PasswordColumn",
                HeaderText = "MẬT KHẨU",
                Width = 100,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _userGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(UserDTO.DisplayName),
                HeaderText = "TÊN HIỂN THỊ",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 130
            });
            _userGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(UserDTO.Role),
                HeaderText = "QUYỀN",
                Width = 76
            });
            _userGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(UserDTO.Status),
                HeaderText = "PHIÊN",
                Width = 82
            });
            _userGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                DataPropertyName = nameof(UserDTO.IsActive),
                HeaderText = "ACTIVE",
                Width = 70
            });

            _userGrid.CellFormatting += (_, e) =>
            {
                if (_userGrid.Columns[e.ColumnIndex].Name == "PasswordColumn")
                {
                    e.Value = "••••••••";
                    e.FormattingApplied = true;
                }
            };
            _userGrid.SelectionChanged += (_, _) => UpdateSelectedUserActions();
        }

        private void CreateButton_Click(object? sender, EventArgs e)
        {
            string username = _usernameTextBox.Text.Trim();
            string displayName = _displayNameTextBox.Text.Trim();
            string password = _passwordTextBox.Text;

            if (username.Length < 3 || password.Length < 6)
            {
                ShowStatus("Tên đăng nhập từ 3 ký tự, mật khẩu từ 6 ký tự.", false);
                return;
            }

            _createButton.Enabled = false;
            _connection.CreateUser(username, password, displayName);
        }

        private void UpdateButton_Click(object? sender, EventArgs e)
        {
            UserDTO? user = GetSelectedUser();
            if (user == null || IsProtectedAdmin(user)) return;

            string username = _usernameTextBox.Text.Trim();
            string displayName = _displayNameTextBox.Text.Trim();
            string password = _passwordTextBox.Text;

            if (username.Length < 3)
            {
                ShowStatus("Tên đăng nhập phải có từ 3 ký tự.", false);
                return;
            }

            if (!string.IsNullOrEmpty(password) && password.Length < 6)
            {
                ShowStatus("Mật khẩu phải có từ 6 ký tự.", false);
                return;
            }

            _updateButton.Enabled = false;
            _connection.UpdateUser(user.Id, username, password, displayName);
        }

        private void ClearFormButton_Click(object? sender, EventArgs e)
        {
            _userGrid.ClearSelection();
            _userGrid.CurrentCell = null;
            _usernameTextBox.Clear();
            _displayNameTextBox.Clear();
            _passwordTextBox.Clear();
            UpdateSelectedUserActions();
        }

        private void ToggleActiveButton_Click(object? sender, EventArgs e)
        {
            UserDTO? user = GetSelectedUser();
            if (user == null || IsProtectedAdmin(user)) return;

            bool newState = !user.IsActive;
            string action = newState ? "kích hoạt" : "vô hiệu hóa";
            if (MessageBox.Show(
                    $"Bạn có chắc muốn {action} user '{user.Username}'?",
                    "Xác nhận",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _connection.SetUserActive(user.Id, newState);
            }
        }

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            UserDTO? user = GetSelectedUser();
            if (user == null || IsProtectedAdmin(user)) return;

            if (MessageBox.Show(
                    $"Xóa vĩnh viễn user '{user.Username}' và các tin nhắn do user này gửi?",
                    "Xác nhận xóa user",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                _connection.DeleteUser(user.Id);
            }
        }

        private void OnMessageReceived(MessageDTO message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnMessageReceived(message)));
                return;
            }

            switch (message.Type)
            {
                case MessageDTO.MessageType.ADMIN_USER_LIST:
                    RenderUsers(message.PlainContent);
                    break;
                case MessageDTO.MessageType.ADMIN_RESULT:
                    HandleAdminResult(message.PlainContent);
                    break;
                case MessageDTO.MessageType.SYSTEM:
                    ShowStatus(message.PlainContent, false);
                    break;
            }
        }

        private void RenderUsers(string json)
        {
            var users = JsonSerializer.Deserialize<List<UserDTO>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<UserDTO>();

            _userGrid.DataSource = users
                .OrderByDescending(user => string.Equals(user.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
                .ThenBy(user => user.Username)
                .ToList();

            _userGrid.ClearSelection();
            _userGrid.CurrentCell = null;
            UpdateSelectedUserActions();
        }

        private void HandleAdminResult(string json)
        {
            try
            {
                var result = JsonSerializer.Deserialize<AdminResult>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null) return;
                ShowStatus(result.Message, result.Success);

                if (result.Success)
                {
                    _usernameTextBox.Clear();
                    _displayNameTextBox.Clear();
                    _passwordTextBox.Clear();
                }
            }
            catch (JsonException)
            {
                ShowStatus("Phản hồi quản trị không hợp lệ.", false);
            }
            finally
            {
                _createButton.Enabled = true;
                UpdateSelectedUserActions();
            }
        }

        private void UpdateSelectedUserActions()
        {
            UserDTO? user = GetSelectedUser();
            bool canManage = user != null && !IsProtectedAdmin(user);

            _toggleActiveButton.Enabled = canManage;
            _deleteButton.Enabled = canManage;
            _toggleActiveButton.Text = user?.IsActive == false ? "KÍCH HOẠT" : "VÔ HIỆU HÓA";
            _selectedUserLabel.Text = user == null
                ? "Chọn một user để quản lý"
                : $"{user.DisplayName}  •  @{user.Username}";

            if (canManage && user != null)
            {
                _usernameTextBox.Text = user.Username;
                _displayNameTextBox.Text = user.DisplayName;
                _passwordTextBox.Text = string.Empty;
                _updateButton.Enabled = true;
                _createButton.Enabled = false;
            }
            else
            {
                _updateButton.Enabled = false;
                _createButton.Enabled = true;
            }
        }

        private UserDTO? GetSelectedUser()
        {
            return _userGrid.CurrentRow?.DataBoundItem as UserDTO;
        }

        private static bool IsProtectedAdmin(UserDTO user)
        {
            return string.Equals(user.Role, "ADMIN", StringComparison.OrdinalIgnoreCase);
        }

        private void ShowStatus(string message, bool success)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = success ? ChatTheme.Green : ChatTheme.Red;
        }

        private void OnConnectionError(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnConnectionError(message)));
                return;
            }

            MessageBox.Show(message, "Mất kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }

        private sealed class AdminResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
        }
    }
}
