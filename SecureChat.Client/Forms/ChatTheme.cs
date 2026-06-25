using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace SecureChat.Client.Forms
{
    internal static class ChatTheme
    {
        public static readonly Color Bg0 = Color.FromArgb(14, 15, 17);
        public static readonly Color Bg1 = Color.FromArgb(20, 21, 24);
        public static readonly Color Bg2 = Color.FromArgb(26, 28, 32);
        public static readonly Color Bg3 = Color.FromArgb(32, 35, 40);
        public static readonly Color Bg4 = Color.FromArgb(39, 43, 49);
        public static readonly Color Hover = Color.FromArgb(46, 51, 59);
        public static readonly Color Accent = Color.FromArgb(91, 106, 240);
        public static readonly Color AccentDim = Color.FromArgb(61, 74, 191);
        public static readonly Color Green = Color.FromArgb(59, 165, 93);
        public static readonly Color Red = Color.FromArgb(237, 66, 69);
        public static readonly Color Yellow = Color.FromArgb(250, 166, 26);
        public static readonly Color Text0 = Color.FromArgb(242, 243, 245);
        public static readonly Color Text1 = Color.FromArgb(181, 186, 193);
        public static readonly Color Text2 = Color.FromArgb(114, 118, 125);
        public static readonly Color Text3 = Color.FromArgb(78, 80, 88);
        public static readonly Color Border = Color.FromArgb(48, 51, 58);

        public static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", size, style);
        }

        public static void ApplyPrimaryButton(Button button, int radius = 8)
        {
            ApplyFlatButton(button, Accent, AccentDim, Color.White, radius);
        }

        public static void ApplyNeutralButton(Button button, int radius = 8)
        {
            ApplyFlatButton(button, Bg4, Hover, Text0, radius);
        }

        public static void ApplyDangerButton(Button button, int radius = 20)
        {
            ApplyFlatButton(button, Red, Color.FromArgb(194, 52, 61), Color.White, radius);
        }

        public static void ApplyFlatButton(Button button, Color backColor, Color hoverColor, Color foreColor, int radius)
        {
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = hoverColor;
            button.FlatAppearance.MouseDownBackColor = ColorBlend(backColor, Color.Black, 0.18f);
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            ApplyRoundedRegion(button, radius);
            button.Resize += (_, _) => ApplyRoundedRegion(button, radius);
        }

        public static void ApplyTextBox(TextBox textBox)
        {
            textBox.BackColor = Bg4;
            textBox.ForeColor = Text0;
            textBox.BorderStyle = BorderStyle.None;
            textBox.Font = Font(10.5f);
        }

        public static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control.Width <= 0 || control.Height <= 0) return;

            Region? oldRegion = control.Region;
            using GraphicsPath path = RoundedRect(new Rectangle(0, 0, control.Width, control.Height), radius);
            control.Region = new Region(path);
            oldRegion?.Dispose();
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
            GraphicsPath path = new GraphicsPath();

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();

            return path;
        }

        public static void PaintRoundedBorder(Control control, PaintEventArgs e, Color borderColor, int radius)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using GraphicsPath path = RoundedRect(new Rectangle(0, 0, control.Width - 1, control.Height - 1), radius);
            using Pen pen = new Pen(borderColor, 1);
            e.Graphics.DrawPath(pen, path);
        }

        public static void EnableDoubleBuffer(Control control)
        {
            typeof(Control)
                .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(control, true, null);
        }

        public static string Initials(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "?";

            string[] parts = value
                .Trim()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            string initials = string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
            return initials.Length > 0 ? initials : "?";
        }

        public static Color ColorBlend(Color baseColor, Color overlay, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            int r = (int)(baseColor.R + (overlay.R - baseColor.R) * amount);
            int g = (int)(baseColor.G + (overlay.G - baseColor.G) * amount);
            int b = (int)(baseColor.B + (overlay.B - baseColor.B) * amount);
            return Color.FromArgb(r, g, b);
        }
    }
}
