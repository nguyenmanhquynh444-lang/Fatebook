using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SecureChat.Client.Storage;

namespace SecureChat.Client.Forms
{
    public partial class HistoryForm : Form
    {
        private readonly LocalHistoryDB _historyDB;

        public HistoryForm(LocalHistoryDB historyDB)
        {
            InitializeComponent();
            _historyDB = historyDB;
            SetupStyles();
        }

        private void SetupStyles()
        {
            this.BackColor = Color.FromArgb(30, 30, 36); // #1E1E24
            this.ForeColor = Color.FromArgb(226, 226, 226);
            this.Font = new Font("Segoe UI", 9F);
            this.StartPosition = FormStartPosition.CenterParent;

            // Phong cách tối cho ListView
            lstResults.BackColor = Color.FromArgb(42, 43, 54); // #2A2B36
            lstResults.ForeColor = Color.FromArgb(226, 226, 226);
            lstResults.BorderStyle = BorderStyle.None;
            lstResults.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            
            txtKeyword.BackColor = Color.FromArgb(42, 43, 54);
            txtKeyword.ForeColor = Color.FromArgb(226, 226, 226);
            txtKeyword.BorderStyle = BorderStyle.FixedSingle;

            btnSearch.BackColor = Color.FromArgb(108, 92, 231); // #6C5CE7
            btnSearch.FlatStyle = FlatStyle.Flat;
            btnSearch.FlatAppearance.BorderSize = 0;
            btnSearch.ForeColor = Color.White;
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            string keyword = txtKeyword.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                MessageBox.Show("Vui lòng nhập từ khóa tìm kiếm!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            lstResults.Items.Clear();
            List<object[]> results = _historyDB.SearchMessages(keyword);

            if (results.Count == 0)
            {
                ListViewItem emptyItem = new ListViewItem("Không tìm thấy tin nhắn nào khớp.");
                emptyItem.SubItems.Add("");
                emptyItem.SubItems.Add("");
                lstResults.Items.Add(emptyItem);
                return;
            }

            foreach (var row in results)
            {
                int roomId = (int)row[0];
                string senderName = (string)row[1];
                string content = (string)row[2];
                long ts = (long)row[3];
                string timeStr = DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime.ToString("dd/MM/yyyy HH:mm");

                ListViewItem item = new ListViewItem(senderName);
                item.SubItems.Add(content);
                item.SubItems.Add(timeStr);
                item.SubItems.Add(roomId.ToString());
                lstResults.Items.Add(item);
            }
        }
    }
}
