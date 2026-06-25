using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using SecureChat.Common.DTO;

namespace SecureChat.Client.Forms
{
    public partial class LoginForm : Form
    {
        private bool _isConnecting = false;

        public SecureConnection? Connection { get; private set; }

        public LoginForm()
        {
            InitializeComponent();
            SetupCustomStyles();
        }

        private void SetupCustomStyles()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ChatTheme.Bg0;
            this.ForeColor = ChatTheme.Text0;
            this.Font = ChatTheme.Font(9F);

            lblTitle.ForeColor = ChatTheme.Accent;
            lblTitle.Font = ChatTheme.Font(24F, FontStyle.Bold);
            lblSubTitle.ForeColor = ChatTheme.Text1;
            lblStatus.ForeColor = ChatTheme.Text2;
            lblSslSecure.ForeColor = ChatTheme.Green;

            panelCard.BackColor = ChatTheme.Bg1;
            panelCard.Paint += (_, e) => ChatTheme.PaintRoundedBorder(panelCard, e, ChatTheme.Border, 14);
            ChatTheme.ApplyRoundedRegion(panelCard, 14);
            panelCard.Resize += (_, _) => ChatTheme.ApplyRoundedRegion(panelCard, 14);

            foreach (Control control in panelCard.Controls)
            {
                if (control is Label label)
                {
                    label.ForeColor = ChatTheme.Text2;
                    label.Font = ChatTheme.Font(8.5F, FontStyle.Bold);
                }
                else if (control is TextBox textBox)
                {
                    ChatTheme.ApplyTextBox(textBox);
                    textBox.BackColor = ChatTheme.Bg3;
                }
            }

            txtUsername.AutoCompleteMode = AutoCompleteMode.None;
            txtUsername.AutoCompleteSource = AutoCompleteSource.None;
            txtPassword.AutoCompleteMode = AutoCompleteMode.None;
            txtPassword.AutoCompleteSource = AutoCompleteSource.None;
            txtPassword.Clear();

            ChatTheme.ApplyPrimaryButton(btnLogin, 8);
            ChatTheme.ApplyFlatButton(btnClose, Color.Transparent, ChatTheme.Red, ChatTheme.Text2, 0);
            ChatTheme.ApplyFlatButton(btnMinimize, Color.Transparent, ChatTheme.Hover, ChatTheme.Text2, 0);

            this.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };
            panelCard.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };
        }

        // WinAPI hỗ trợ kéo di chuyển form không viền
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private void btnClose_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (_isConnecting) return;

            string host = txtHost.Text.Trim();
            string portStr = txtPort.Text.Trim();
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(portStr) || 
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowStatus("Vui lòng điền đầy đủ các thông tin!", ChatTheme.Red);
                return;
            }

            if (!int.TryParse(portStr, out int port))
            {
                ShowStatus("Cổng kết nối phải là số nguyên!", ChatTheme.Red);
                return;
            }

            SetLoadingState(true);
            ShowStatus("Đang thiết lập kết nối SSL/TLS...", ChatTheme.Yellow);

            var loginThread = new Thread(() =>
            {
                try
                {
                    var conn = new SecureConnection(host, port);
                    UserDTO? user = conn.Connect(username, password);

                    if (user != null)
                    {
                        Thread.Sleep(800);
                        RunOnUiThread(() =>
                        {
                            ShowStatus("Kết nối thành công! Đang chuyển tiếp...", ChatTheme.Green);
                            Connection = conn;
                            DialogResult = DialogResult.OK;
                            Close();
                        });
                    }
                    else
                    {
                        RunOnUiThread(() =>
                        {
                            ShowStatus("Tài khoản hoặc mật khẩu không đúng!", ChatTheme.Red);
                            SetLoadingState(false);
                        });
                    }
                }
                catch (Exception ex)
                {
                    RunOnUiThread(() =>
                    {
                        ShowStatus(ex.Message, ChatTheme.Red);
                        SetLoadingState(false);
                    });
                }
            })
            {
                IsBackground = true,
                Name = "LoginConnection"
            };
            loginThread.Start();
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(action);
        }

        private void ShowStatus(string message, Color color)
        {
            lblStatus.Text = message;
            lblStatus.ForeColor = color;
        }

        private void SetLoadingState(bool loading)
        {
            _isConnecting = loading;
            btnLogin.Enabled = !loading;
            txtHost.Enabled = !loading;
            txtPort.Enabled = !loading;
            txtUsername.Enabled = !loading;
            txtPassword.Enabled = !loading;

            if (loading)
            {
                btnLogin.Text = "ĐANG KẾT NỐI...";
                btnLogin.BackColor = ChatTheme.AccentDim;
            }
            else
            {
                btnLogin.Text = "ĐĂNG NHẬP CHAT";
                btnLogin.BackColor = ChatTheme.Accent;
            }
        }
    }
}
