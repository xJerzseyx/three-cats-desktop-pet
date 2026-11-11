using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ThreeCats
{
    internal sealed class BowlWindow : Form
    {
        private readonly Bitmap source;
        private readonly Rectangle[] bowls = new Rectangle[2];
        private Bitmap frame;
        private RoutineKind renderedKind;
        private int renderedWidth;

        internal BowlWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            using (var stream = typeof(BowlWindow).Assembly.GetManifestResourceStream("ThreeCats.Daily.Bowls.png"))
            using (var image = new Bitmap(stream))
            {
                source = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(source)) graphics.DrawImageUnscaled(image, 0, 0);
            }
            var data = source.LockBits(new Rectangle(Point.Empty, source.Size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var pixels = new int[source.Width * source.Height];
            try
            {
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                for (int index = 0; index < pixels.Length; index++)
                    if ((uint)pixels[index] >> 24 <= 32) pixels[index] = 0;
                for (int bowl = 0; bowl < 2; bowl++)
                {
                    int start = bowl * source.Width / 2;
                    int end = (bowl + 1) * source.Width / 2;
                    int left = end;
                    int right = start;
                    int top = source.Height;
                    int bottom = 0;
                    for (int y = 0; y < source.Height; y++)
                        for (int x = start; x < end; x++)
                        {
                            if (pixels[y * source.Width + x] == 0) continue;
                            left = Math.Min(left, x);
                            right = Math.Max(right, x + 1);
                            top = Math.Min(top, y);
                            bottom = Math.Max(bottom, y + 1);
                        }
                    if (right <= left || bottom <= top) throw new InvalidOperationException("Missing bowl artwork.");
                    bowls[bowl] = Rectangle.FromLTRB(left, top, right, bottom);
                }
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally { source.UnlockBits(data); }
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

        internal void ShowMeal(RoutineKind kind, Point foot, int groupWidth)
        {
            if (kind != RoutineKind.Drink && kind != RoutineKind.Eat) { Hide(); return; }
            if (frame == null || kind != renderedKind || groupWidth != renderedWidth)
            {
                if (frame != null) frame.Dispose();
                Rectangle crop = bowls[kind == RoutineKind.Drink ? 0 : 1];
                int width = Math.Max(20, (int)Math.Round(groupWidth * .14));
                int height = Math.Max(8, (int)Math.Round(width * (double)crop.Height / crop.Width));
                frame = new Bitmap(width + 4, height + 4, PixelFormat.Format32bppPArgb);
                using (var graphics = Graphics.FromImage(frame))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(source, new Rectangle(2, 2, width, height), crop, GraphicsUnit.Pixel);
                }
                renderedKind = kind;
                renderedWidth = groupWidth;
            }
            Size = frame.Size;
            int groundOffset = Math.Max(5, (int)Math.Round(groupWidth * .025));
            Location = new Point(foot.X - Width / 2, foot.Y + groundOffset - Height + 2);
            if (!Visible) Show();
            Native.Present(Handle, frame, Location);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                source.Dispose();
                if (frame != null) frame.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
