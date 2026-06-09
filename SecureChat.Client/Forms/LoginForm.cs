using System;
using System.Drawing;
using System.Threading.Tasks;
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
            // Thiết kế Window không viền mặc định của Windows
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 36); // #1E1E24
            
            // Cho phép di chuyển form bằng cách nhấn giữ chuột ở vùng trống
            this.MouseDown += (s, e) => {
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

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            if (_isConnecting) return;

            string host = txtHost.Text.Trim();
            string portStr = txtPort.Text.Trim();
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(portStr) || 
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowStatus("Vui lòng điền đầy đủ các thông tin!", Color.FromArgb(231, 76, 60));
                return;
            }

            if (!int.TryParse(portStr, out int port))
            {
                ShowStatus("Cổng kết nối phải là số nguyên!", Color.FromArgb(231, 76, 60));
                return;
            }

            SetLoadingState(true);
            ShowStatus("Đang thiết lập kết nối SSL/TLS...", Color.FromArgb(241, 196, 15));

            try
            {
                var conn = new SecureConnection(host, port);
                UserDTO? user = await conn.ConnectAsync(username, password);

                if (user != null)
                {
                    ShowStatus("Kết nối thành công! Đang chuyển tiếp...", Color.FromArgb(46, 204, 113));
                    this.Connection = conn;
                    
                    // Mở Form Chat chính
                    await Task.Delay(800); // Tạo độ trễ nhỏ để trải nghiệm mượt mà
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    ShowStatus("Tài khoản hoặc mật khẩu không đúng!", Color.FromArgb(231, 76, 60));
                    SetLoadingState(false);
                }
            }
            catch (Exception ex)
            {
                ShowStatus(ex.Message, Color.FromArgb(231, 76, 60));
                SetLoadingState(false);
            }
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
                btnLogin.BackColor = Color.FromArgb(108, 92, 231, 150); // Mờ nút tím
            }
            else
            {
                btnLogin.Text = "ĐĂNG NHẬP CHAT";
                btnLogin.BackColor = Color.FromArgb(108, 92, 231); // #6C5CE7
            }
        }
    }
}
