using System.Windows.Forms;

namespace SecureChat.Client.Forms
{
    partial class HistoryForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblKeyword = new System.Windows.Forms.Label();
            this.txtKeyword = new System.Windows.Forms.TextBox();
            this.btnSearch = new System.Windows.Forms.Button();
            this.lstResults = new System.Windows.Forms.ListView();
            this.colSender = new System.Windows.Forms.ColumnHeader();
            this.colContent = new System.Windows.Forms.ColumnHeader();
            this.colTime = new System.Windows.Forms.ColumnHeader();
            this.colRoom = new System.Windows.Forms.ColumnHeader();
            this.SuspendLayout();
            // 
            // lblKeyword
            // 
            this.lblKeyword.AutoSize = true;
            this.lblKeyword.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblKeyword.Location = new System.Drawing.Point(12, 17);
            this.lblKeyword.Name = "lblKeyword";
            this.lblKeyword.Size = new System.Drawing.Size(117, 15);
            this.lblKeyword.TabIndex = 0;
            this.lblKeyword.Text = "Nhập từ khóa tìm kiếm";
            // 
            // txtKeyword
            // 
            this.txtKeyword.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.txtKeyword.Location = new System.Drawing.Point(135, 12);
            this.txtKeyword.Name = "txtKeyword";
            this.txtKeyword.Size = new System.Drawing.Size(320, 25);
            this.txtKeyword.TabIndex = 1;
            // 
            // btnSearch
            // 
            this.btnSearch.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSearch.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnSearch.Location = new System.Drawing.Point(465, 10);
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(100, 28);
            this.btnSearch.TabIndex = 2;
            this.btnSearch.Text = "TÌM KIẾM";
            this.btnSearch.UseVisualStyleBackColor = true;
            this.btnSearch.Click += new System.EventHandler(this.btnSearch_Click);
            // 
            // lstResults
            // 
            this.lstResults.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colSender,
            this.colContent,
            this.colTime,
            this.colRoom});
            this.lstResults.FullRowSelect = true;
            this.lstResults.HideSelection = false;
            this.lstResults.Location = new System.Drawing.Point(12, 55);
            this.lstResults.Name = "lstResults";
            this.lstResults.Size = new System.Drawing.Size(560, 295);
            this.lstResults.TabIndex = 3;
            this.lstResults.UseCompatibleStateImageBehavior = false;
            this.lstResults.View = System.Windows.Forms.View.Details;
            // 
            // colSender
            // 
            this.colSender.Text = "Người gửi";
            this.colSender.Width = 100;
            // 
            // colContent
            // 
            this.colContent.Text = "Nội dung tin nhắn";
            this.colContent.Width = 280;
            // 
            // colTime
            // 
            this.colTime.Text = "Thời gian";
            this.colTime.Width = 110;
            // 
            // colRoom
            // 
            this.colRoom.Text = "Mã Phòng";
            this.colRoom.Width = 60;
            // 
            // HistoryForm
            // 
            this.AcceptButton = this.btnSearch;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 361);
            this.Controls.Add(this.lstResults);
            this.Controls.Add(this.btnSearch);
            this.Controls.Add(this.txtKeyword);
            this.Controls.Add(this.lblKeyword);
            this.Name = "HistoryForm";
            this.Text = "Lịch sử Chat Cục Bộ";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label lblKeyword;
        private System.Windows.Forms.TextBox txtKeyword;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.ListView lstResults;
        private System.Windows.Forms.ColumnHeader colSender;
        private System.Windows.Forms.ColumnHeader colContent;
        private System.Windows.Forms.ColumnHeader colTime;
        private System.Windows.Forms.ColumnHeader colRoom;
    }
}
