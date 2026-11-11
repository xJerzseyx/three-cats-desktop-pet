using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ThreeCats
{
    public class CatWindow : Form
    {
        private readonly SpriteRenderer renderer = new SpriteRenderer();
        private double lookX;
        private double lookY;
        public int CatId { get; private set; }
        internal event Action ScreenChanged;

        public CatWindow(int catId)
        {
            CatId = catId;
            Text = Program.AppName;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x80000 | 0x80 | 0x08000000;
                return parameters;
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x21) { message.Result = new IntPtr(3); return; }
            base.WndProc(ref message);
            if (message.Msg == 0x02E0 && ScreenChanged != null) ScreenChanged();
        }

        internal void Place(Point foot, int groupWidth, bool daily = false, bool lifted = false)
        {
            bool expanded = daily || lifted;
            Size frameSize = expanded ? DailyRenderer.FrameSize(groupWidth) : SpriteRenderer.CatFrameSize(groupWidth);
            Point offset = expanded ? DailyRenderer.FootOffset(groupWidth) : SpriteRenderer.CatFootOffset(groupWidth);
            var position = new Point(foot.X - offset.X, foot.Y - offset.Y);
            if (Size != frameSize) Size = frameSize;
            if (Location != position) Location = position;
        }

        internal void Animate(Point cursor, int groupWidth, double elapsed, double musicMix, double energy, double time,
            DailyRoutine routine = null, DailyRenderer dailyRenderer = null, LiftRenderer liftRenderer = null)
        {
            if (!Visible) return;
            bool daily = routine != null && routine.ActiveCat == CatId;
            Size idleSize = SpriteRenderer.CatFrameSize(groupWidth);
            Point idleOffset = SpriteRenderer.CatFootOffset(groupWidth);
            Point offset = daily ? DailyRenderer.FootOffset(groupWidth) : idleOffset;
            double desiredX = Math.Max(-1, Math.Min(1, (cursor.X - Left - offset.X) / Math.Max(160, groupWidth * 0.55)));
            double headY = Top + offset.Y - idleOffset.Y + idleSize.Height * 0.29;
            double desiredY = Math.Max(-1, Math.Min(1, (cursor.Y - headY) / Math.Max(130, idleSize.Height * 0.8)));
            lookX += (desiredX - lookX) * (1 - Math.Exp(-elapsed * 8));
            lookY += (desiredY - lookY) * (1 - Math.Exp(-elapsed * 8));
            Bitmap frame;
            if (liftRenderer != null) frame = liftRenderer.Render(CatId, groupWidth);
            else if (daily) frame = dailyRenderer.Render(renderer, CatId, groupWidth, lookX, lookY, musicMix, energy, time, routine);
            else frame = renderer.RenderCat(CatId, groupWidth, lookX, lookY, musicMix, energy, time);
            Native.Present(Handle, frame, Location);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) renderer.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class QueuePreview : Form
    {
        private readonly SpriteRenderer renderer = new SpriteRenderer();
        private Bitmap emptyFrame;
        private int emptyWidth;

        internal QueuePreview()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
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

        internal void ShowTarget(CatLayout layout, int catId, bool ready, double musicMix, double energy, double time)
        {
            if (!ready && layout.Queue.Count > 0) { Hide(); return; }
            Bitmap frame;
            Point foot = ready ? layout.TailPosition : layout.Anchor;
            if (ready)
                frame = renderer.RenderCat(catId, layout.Width, 0, 0, musicMix, energy, time);
            else
            {
                if (emptyFrame == null || emptyWidth != layout.Width)
                {
                    if (emptyFrame != null) emptyFrame.Dispose();
                    Size size = SpriteRenderer.CatFrameSize(layout.Width);
                    emptyFrame = new Bitmap(size.Width + layout.Spacing * 2, size.Height, PixelFormat.Format32bppPArgb);
                    using (var graphics = Graphics.FromImage(emptyFrame))
                        for (int id = 0; id < 3; id++)
                            graphics.DrawImageUnscaled(renderer.RenderCat(id, layout.Width, 0, 0, 0, 0, 0), id * layout.Spacing, 0);
                    emptyWidth = layout.Width;
                }
                frame = emptyFrame;
            }
            Point offset = SpriteRenderer.CatFootOffset(layout.Width);
            Size = frame.Size;
            Location = new Point(foot.X - offset.X, foot.Y - offset.Y);
            if (!Visible) Show();
            Native.Present(Handle, frame, Location, ready ? (byte)120 : (byte)55);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                renderer.Dispose();
                if (emptyFrame != null) emptyFrame.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
