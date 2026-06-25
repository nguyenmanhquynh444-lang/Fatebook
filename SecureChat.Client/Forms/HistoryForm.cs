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
            this.BackColor = ChatTheme.Bg0;
            this.ForeColor = ChatTheme.Text0;
            this.Font = ChatTheme.Font(9F);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(620, 420);

            lblKeyword.ForeColor = ChatTheme.Text2;
            lblKeyword.Font = ChatTheme.Font(8.5F, FontStyle.Bold);

            lstResults.BackColor = ChatTheme.Bg1;
            lstResults.ForeColor = ChatTheme.Text0;
            lstResults.BorderStyle = BorderStyle.None;
            lstResults.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lstResults.OwnerDraw = true;
            lstResults.FullRowSelect = true;
            lstResults.DrawColumnHeader += DrawHistoryHeader;
            lstResults.DrawSubItem += DrawHistorySubItem;

            ChatTheme.ApplyTextBox(txtKeyword);
            txtKeyword.BackColor = ChatTheme.Bg3;
            ChatTheme.ApplyPrimaryButton(btnSearch, 8);

            LayoutHistoryControls();
            this.Resize += (_, _) => LayoutHistoryControls();
        }

        private void LayoutHistoryControls()
        {
            int margin = 16;
            lblKeyword.SetBounds(margin, 18, 128, 20);
            btnSearch.SetBounds(ClientSize.Width - margin - 106, 12, 106, 32);
            txtKeyword.SetBounds(150, 17, Math.Max(180, btnSearch.Left - 162), 24);
            lstResults.SetBounds(margin, 58, ClientSize.Width - margin * 2, ClientSize.Height - 74);
            colContent.Width = Math.Max(220, lstResults.Width - colSender.Width - colTime.Width - colRoom.Width - 8);
        }

        private void DrawHistoryHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            using Brush headerBrush = new SolidBrush(ChatTheme.Bg3);
            using Font headerFont = ChatTheme.Font(8.5F, FontStyle.Bold);
            e.Graphics.FillRectangle(headerBrush, e.Bounds);
            TextRenderer.DrawText(
                e.Graphics,
                e.Header?.Text ?? string.Empty,
                headerFont,
                e.Bounds,
                ChatTheme.Text1,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            );
        }

        private void DrawHistorySubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            bool isSelected = e.Item?.Selected == true;
            Color rowColor = isSelected
                ? ChatTheme.Hover
                : (e.ItemIndex % 2 == 0 ? ChatTheme.Bg1 : ChatTheme.Bg2);

            using Brush rowBrush = new SolidBrush(rowColor);
            using Font rowFont = ChatTheme.Font(8.7F);
            e.Graphics.FillRectangle(rowBrush, e.Bounds);
            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem?.Text ?? string.Empty,
                rowFont,
                e.Bounds,
                isSelected ? ChatTheme.Text0 : ChatTheme.Text1,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            );
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
