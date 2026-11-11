using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ThreeCats
{
    internal sealed class GreetingWindow : Form
    {
        private const double Duration = 7;
        private readonly Bitmap frame = new Bitmap(356, 104, PixelFormat.Format32bppPArgb);
        private double started;
        private bool playing;
        internal bool IsPlaying { get { return playing; } }

        internal GreetingWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            Size = frame.Size;
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x80000 | 0x80 | 0x08000000 | 0x20;
                return parameters;
            }
        }

        internal void Play(CelebrationDay day, double now)
        {
            Text = day.Message;
            started = now;
            playing = true;
            DrawCard(day);
        }

        internal void Stop()
        {
            playing = false;
            Hide();
        }

        internal void Present(double now, Rectangle catBounds)
        {
            if (!playing) return;
            double age = now - started;
            if (age >= Duration) { Stop(); return; }
            Rectangle area = Screen.FromRectangle(catBounds).WorkingArea;
            int x = catBounds.Left + (catBounds.Width - Width) / 2;
            int y = catBounds.Top - Height - 8;
            if (y < area.Top) y = catBounds.Bottom + 8;
            var target = Placement.Clamp(new Rectangle(x, y, Width, Height), new[] { area });
            if (!Visible) Show();
            double fade = Math.Min(1, Math.Min(.25 + age / .22, (Duration - age) / .65));
            Native.Present(Handle, frame, target.Location, (byte)Math.Round(fade * 255));
        }

        private void DrawCard(CelebrationDay day)
        {
            Color gold = Color.FromArgb(241, 185, 102);
            Color cream = Color.FromArgb(255, 240, 221);
            Color pink = Color.FromArgb(240, 142, 163);
            Color mint = Color.FromArgb(135, 215, 191);
            using (var graphics = Graphics.FromImage(frame))
            using (var path = new GraphicsPath())
            using (var background = new SolidBrush(Color.FromArgb(37, 27, 48)))
            using (var rim = new Pen(Color.FromArgb(216, gold), 1.25f))
            using (var ink = new SolidBrush(cream))
            using (var accent = new SolidBrush(gold))
            using (var rose = new SolidBrush(pink))
            using (var green = new SolidBrush(mint))
            using (var font = new Font("Microsoft YaHei UI", 21, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var align = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                path.AddArc(13, 16, 32, 32, 180, 90);
                path.AddArc(311, 16, 32, 32, 270, 90);
                path.AddArc(311, 58, 32, 32, 0, 90);
                path.AddArc(13, 58, 32, 32, 90, 90);
                path.CloseFigure();
                graphics.FillPath(background, path);
                graphics.DrawPath(rim, path);

                if (day.Anniversary)
                {
                    graphics.FillEllipse(accent, 29, 43, 43, 24);
                    graphics.FillEllipse(ink, 32, 39, 14, 14);
                    graphics.FillEllipse(rose, 44, 43, 13, 13);
                    graphics.FillEllipse(ink, 55, 39, 13, 13);
                    graphics.FillPie(rose, 28, 48, 45, 25, 0, 180);
                    graphics.FillEllipse(accent, 42, 71, 17, 3);
                }
                else
                {
                    graphics.FillEllipse(accent, 27, 68, 47, 6);
                    graphics.FillRectangle(rose, 32, 47, 37, 22);
                    graphics.FillEllipse(ink, 32, 43, 37, 12);
                    graphics.FillRectangle(ink, 32, 52, 37, 4);
                    graphics.FillRectangle(green, 49, 36, 4, 12);
                    graphics.FillEllipse(accent, 48, 28, 6, 9);
                }
                graphics.DrawString(day.Message, font, ink, new RectangleF(86, 16, 250, 74), align);

                // Ribbon curls and confetti around the card, leaving its copy clear.
                using (var ribbon = new Pen(pink, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    graphics.DrawBezier(ribbon, 7, 33, 24, 20, -4, 22, 12, 9);
                    ribbon.Color = mint;
                    graphics.DrawBezier(ribbon, 348, 73, 334, 77, 359, 87, 339, 98);
                }
                graphics.FillPolygon(accent, new[] { new Point(44, 2), new Point(49, 8), new Point(43, 12), new Point(39, 7) });
                graphics.FillPolygon(rose, new[] { new Point(303, 3), new Point(312, 7), new Point(306, 12) });
                graphics.FillPolygon(green, new[] { new Point(86, 90), new Point(90, 96), new Point(83, 101), new Point(80, 95) });
                graphics.FillEllipse(accent, 281, 92, 5, 5);
                graphics.FillEllipse(rose, 16, 88, 4, 4);
                graphics.FillEllipse(green, 337, 8, 4, 4);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) frame.Dispose();
            base.Dispose(disposing);
        }
    }
}
