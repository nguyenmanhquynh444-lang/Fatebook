namespace SecureChat.Client.Forms
{
    partial class VideoCallForm
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
            this.picRemote = new System.Windows.Forms.PictureBox();
            this.picLocal = new System.Windows.Forms.PictureBox();
            this.btnHangUp = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.picRemote)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picLocal)).BeginInit();
            this.SuspendLayout();
            // 
            // picRemote
            // 
            this.picRemote.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(15)))), ((int)(((byte)(20)))));
            this.picRemote.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picRemote.Location = new System.Drawing.Point(0, 0);
            this.picRemote.Name = "picRemote";
            this.picRemote.Size = new System.Drawing.Size(784, 561);
            this.picRemote.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picRemote.TabIndex = 0;
            this.picRemote.TabStop = false;
            // 
            // picLocal
            // 
            this.picLocal.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.picLocal.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(50)))));
            this.picLocal.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picLocal.Location = new System.Drawing.Point(608, 425);
            this.picLocal.Name = "picLocal";
            this.picLocal.Size = new System.Drawing.Size(160, 120);
            this.picLocal.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picLocal.TabIndex = 1;
            this.picLocal.TabStop = false;
            // 
            // btnHangUp
            // 
            this.btnHangUp.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnHangUp.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(231)))), ((int)(((byte)(76)))), ((int)(((byte)(60)))));
            this.btnHangUp.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnHangUp.FlatAppearance.BorderSize = 0;
            this.btnHangUp.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnHangUp.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.btnHangUp.ForeColor = System.Drawing.Color.White;
            this.btnHangUp.Location = new System.Drawing.Point(332, 501);
            this.btnHangUp.Name = "btnHangUp";
            this.btnHangUp.Size = new System.Drawing.Size(120, 40);
            this.btnHangUp.TabIndex = 2;
            this.btnHangUp.Text = "CÚP MÁY";
            this.btnHangUp.UseVisualStyleBackColor = false;
            this.btnHangUp.Click += new System.EventHandler(this.btnHangUp_Click);
            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.BackColor = System.Drawing.Color.Transparent;
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(196)))), ((int)(((byte)(15)))));
            this.lblStatus.Location = new System.Drawing.Point(12, 18);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(760, 25);
            this.lblStatus.TabIndex = 3;
            this.lblStatus.Text = "Đang kết nối...";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // VideoCallForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnHangUp);
            this.Controls.Add(this.picLocal);
            this.Controls.Add(this.picRemote);
            this.Name = "VideoCallForm";
            this.Text = "Gọi Video Bảo Mật";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.VideoCallForm_FormClosing);
            this.Load += new System.EventHandler(this.VideoCallForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.picRemote)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picLocal)).EndInit();
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.PictureBox picRemote;
        private System.Windows.Forms.PictureBox picLocal;
        private System.Windows.Forms.Button btnHangUp;
        private System.Windows.Forms.Label lblStatus;
    }
}
