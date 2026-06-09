using System.Windows.Forms;

namespace SecureChat.Client.Forms
{
    partial class MainChatForm
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
            this.pnlSidebar = new System.Windows.Forms.Panel();
            this.lblCurrentUser = new System.Windows.Forms.Label();
            this.pnlUserList = new System.Windows.Forms.FlowLayoutPanel();
            this.pnlChatArea = new System.Windows.Forms.Panel();
            this.pnlMessages = new System.Windows.Forms.FlowLayoutPanel();
            this.pnlInput = new System.Windows.Forms.Panel();
            this.btnAttach = new System.Windows.Forms.Button();
            this.btnSend = new System.Windows.Forms.Button();
            this.txtMessage = new System.Windows.Forms.TextBox();
            this.pnlChatHeader = new System.Windows.Forms.Panel();
            this.btnHistory = new System.Windows.Forms.Button();
            this.btnVideoCall = new System.Windows.Forms.Button();
            this.lblSecureIndicator = new System.Windows.Forms.Label();
            this.lblChatTargetStatus = new System.Windows.Forms.Label();
            this.lblChatTarget = new System.Windows.Forms.Label();
            this.pnlSidebar.SuspendLayout();
            this.pnlChatArea.SuspendLayout();
            this.pnlInput.SuspendLayout();
            this.pnlChatHeader.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlSidebar
            // 
            this.pnlSidebar.Controls.Add(this.lblCurrentUser);
            this.pnlSidebar.Controls.Add(this.pnlUserList);
            this.pnlSidebar.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnlSidebar.Location = new System.Drawing.Point(0, 0);
            this.pnlSidebar.Name = "pnlSidebar";
            this.pnlSidebar.Size = new System.Drawing.Size(260, 661);
            this.pnlSidebar.TabIndex = 0;
            // 
            // lblCurrentUser
            // 
            this.lblCurrentUser.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblCurrentUser.Location = new System.Drawing.Point(12, 18);
            this.lblCurrentUser.Name = "lblCurrentUser";
            this.lblCurrentUser.Size = new System.Drawing.Size(236, 25);
            this.lblCurrentUser.TabIndex = 1;
            this.lblCurrentUser.Text = "Chào, Administrator";
            // 
            // pnlUserList
            // 
            this.pnlUserList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlUserList.AutoScroll = true;
            this.pnlUserList.Location = new System.Drawing.Point(0, 60);
            this.pnlUserList.Name = "pnlUserList";
            this.pnlUserList.Size = new System.Drawing.Size(260, 601);
            this.pnlUserList.TabIndex = 0;
            // 
            // pnlChatArea
            // 
            this.pnlChatArea.Controls.Add(this.pnlMessages);
            this.pnlChatArea.Controls.Add(this.pnlInput);
            this.pnlChatArea.Controls.Add(this.pnlChatHeader);
            this.pnlChatArea.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlChatArea.Location = new System.Drawing.Point(260, 0);
            this.pnlChatArea.Name = "pnlChatArea";
            this.pnlChatArea.Size = new System.Drawing.Size(724, 661);
            this.pnlChatArea.TabIndex = 1;
            // 
            // pnlMessages
            // 
            this.pnlMessages.AutoScroll = true;
            this.pnlMessages.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(36)))));
            this.pnlMessages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMessages.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.pnlMessages.Location = new System.Drawing.Point(0, 60);
            this.pnlMessages.Name = "pnlMessages";
            this.pnlMessages.Padding = new System.Windows.Forms.Padding(10, 10, 10, 30);
            this.pnlMessages.Size = new System.Drawing.Size(724, 536);
            this.pnlMessages.TabIndex = 2;
            this.pnlMessages.WrapContents = false;
            // 
            // pnlInput
            // 
            this.pnlInput.Controls.Add(this.btnAttach);
            this.pnlInput.Controls.Add(this.btnSend);
            this.pnlInput.Controls.Add(this.txtMessage);
            this.pnlInput.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlInput.Location = new System.Drawing.Point(0, 596);
            this.pnlInput.Name = "pnlInput";
            this.pnlInput.Size = new System.Drawing.Size(724, 65);
            this.pnlInput.TabIndex = 1;
            // 
            // btnAttach
            // 
            this.btnAttach.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnAttach.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnAttach.Enabled = false;
            this.btnAttach.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.btnAttach.Location = new System.Drawing.Point(15, 15);
            this.btnAttach.Name = "btnAttach";
            this.btnAttach.Size = new System.Drawing.Size(40, 35);
            this.btnAttach.TabIndex = 2;
            this.btnAttach.Text = "📎";
            this.btnAttach.UseVisualStyleBackColor = true;
            this.btnAttach.Click += new System.EventHandler(this.btnAttach_Click);
            // 
            // btnSend
            // 
            this.btnSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSend.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSend.Enabled = false;
            this.btnSend.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnSend.Location = new System.Drawing.Point(620, 15);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(90, 35);
            this.btnSend.TabIndex = 1;
            this.btnSend.Text = "GỬI TIN";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            // 
            // txtMessage
            // 
            this.txtMessage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtMessage.Enabled = false;
            this.txtMessage.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.txtMessage.Location = new System.Drawing.Point(65, 18);
            this.txtMessage.Name = "txtMessage";
            this.txtMessage.Size = new System.Drawing.Size(545, 27);
            this.txtMessage.TabIndex = 0;
            this.txtMessage.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtMessage_KeyDown);
            // 
            // pnlChatHeader
            // 
            this.pnlChatHeader.Controls.Add(this.btnVideoCall);
            this.pnlChatHeader.Controls.Add(this.btnHistory);
            this.pnlChatHeader.Controls.Add(this.lblSecureIndicator);
            this.pnlChatHeader.Controls.Add(this.lblChatTargetStatus);
            this.pnlChatHeader.Controls.Add(this.lblChatTarget);
            this.pnlChatHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlChatHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlChatHeader.Name = "pnlChatHeader";
            this.pnlChatHeader.Size = new System.Drawing.Size(724, 60);
            this.pnlChatHeader.TabIndex = 0;
            // 
            // btnVideoCall
            // 
            this.btnVideoCall.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnVideoCall.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnVideoCall.Enabled = false;
            this.btnVideoCall.Font = new System.Drawing.Font("Segoe UI Semibold", 8.25f, System.Drawing.FontStyle.Bold);
            this.btnVideoCall.Location = new System.Drawing.Point(520, 15);
            this.btnVideoCall.Name = "btnVideoCall";
            this.btnVideoCall.Size = new System.Drawing.Size(90, 28);
            this.btnVideoCall.TabIndex = 4;
            this.btnVideoCall.Text = "📹 GỌI VIDEO";
            this.btnVideoCall.UseVisualStyleBackColor = true;
            this.btnVideoCall.Click += new System.EventHandler(this.btnVideoCall_Click);
            // 
            // btnHistory
            // 
            this.btnHistory.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnHistory.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnHistory.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.btnHistory.Location = new System.Drawing.Point(620, 15);
            this.btnHistory.Name = "btnHistory";
            this.btnHistory.Size = new System.Drawing.Size(90, 28);
            this.btnHistory.TabIndex = 3;
            this.btnHistory.Text = "LỊCH SỬ";
            this.btnHistory.UseVisualStyleBackColor = true;
            this.btnHistory.Click += new System.EventHandler(this.btnHistory_Click);
            // 
            // lblSecureIndicator
            // 
            this.lblSecureIndicator.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSecureIndicator.AutoSize = true;
            this.lblSecureIndicator.Font = new System.Drawing.Font("Segoe UI Semibold", 8.25f, System.Drawing.FontStyle.Italic);
            this.lblSecureIndicator.Location = new System.Drawing.Point(340, 24);
            this.lblSecureIndicator.Name = "lblSecureIndicator";
            this.lblSecureIndicator.Size = new System.Drawing.Size(183, 13);
            this.lblSecureIndicator.TabIndex = 2;
            this.lblSecureIndicator.Text = "🔒 Đầu cuối mã hóa AES + SSL/TLS";
            // 
            // lblChatTargetStatus
            // 
            this.lblChatTargetStatus.AutoSize = true;
            this.lblChatTargetStatus.Font = new System.Drawing.Font("Segoe UI Italic", 8.25f);
            this.lblChatTargetStatus.Location = new System.Drawing.Point(20, 36);
            this.lblChatTargetStatus.Name = "lblChatTargetStatus";
            this.lblChatTargetStatus.Size = new System.Drawing.Size(126, 13);
            this.lblChatTargetStatus.TabIndex = 1;
            this.lblChatTargetStatus.Text = "Chọn người dùng để chat";
            // 
            // lblChatTarget
            // 
            this.lblChatTarget.AutoSize = true;
            this.lblChatTarget.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblChatTarget.Location = new System.Drawing.Point(18, 12);
            this.lblChatTarget.Name = "lblChatTarget";
            this.lblChatTarget.Size = new System.Drawing.Size(149, 21);
            this.lblChatTarget.TabIndex = 0;
            this.lblChatTarget.Text = "Chưa Chọn Phòng";
            // 
            // MainChatForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 661);
            this.Controls.Add(this.pnlChatArea);
            this.Controls.Add(this.pnlSidebar);
            this.Name = "MainChatForm";
            this.Text = "Fatebook - Chat Bảo Mật Đầu Cuối";
            this.Load += new System.EventHandler(this.MainChatForm_Load);
            this.pnlSidebar.ResumeLayout(false);
            this.pnlChatArea.ResumeLayout(false);
            this.pnlInput.ResumeLayout(false);
            this.pnlInput.PerformLayout();
            this.pnlChatHeader.ResumeLayout(false);
            this.pnlChatHeader.PerformLayout();
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.Panel pnlSidebar;
        private System.Windows.Forms.Label lblCurrentUser;
        private System.Windows.Forms.FlowLayoutPanel pnlUserList;
        private System.Windows.Forms.Panel pnlChatArea;
        private System.Windows.Forms.FlowLayoutPanel pnlMessages;
        private System.Windows.Forms.Panel pnlInput;
        private System.Windows.Forms.Button btnAttach;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.TextBox txtMessage;
        private System.Windows.Forms.Panel pnlChatHeader;
        private System.Windows.Forms.Label lblChatTargetStatus;
        private System.Windows.Forms.Label lblChatTarget;
        private System.Windows.Forms.Label lblSecureIndicator;
        private System.Windows.Forms.Button btnHistory;
        private System.Windows.Forms.Button btnVideoCall;
    }
}
