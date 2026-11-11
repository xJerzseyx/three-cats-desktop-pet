using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ThreeCats
{
    public sealed class LiftRenderer : IDisposable
    {
        private readonly Bitmap source;
        private readonly Bitmap graySource;
        private readonly Bitmap[] frames = new Bitmap[3];
        private static readonly Point[] grips = { new Point(288, 84), new Point(267, 76), new Point(259, 80) };
        private int renderedWidth;

        public LiftRenderer()
        {
            source = Load("ThreeCats.Lifted.png");
            graySource = Load("ThreeCats.LiftedGray.png");
        }

        private static Bitmap Load(string resource)
        {
            using (var stream = typeof(LiftRenderer).Assembly.GetManifestResourceStream(resource))
            using (var image = new Bitmap(stream))
            {
                var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);
                using (var graphics = Graphics.FromImage(bitmap)) graphics.DrawImageUnscaled(image, 0, 0);
                return bitmap;
            }
        }

        public static Size FrameSize(int groupWidth) { return DailyRenderer.FrameSize(groupWidth); }
        public static Point FootOffset(int groupWidth) { return DailyRenderer.FootOffset(groupWidth); }

        public static Point GripOffset(int cat, int groupWidth)
        {
            if (cat < 0 || cat >= 3) throw new ArgumentOutOfRangeException("cat");
            Point foot = FootOffset(groupWidth);
            return new Point(foot.X, foot.Y + (int)Math.Round((grips[cat].Y - 910) * Scale(groupWidth)));
        }

        private static double Scale(int groupWidth)
        {
            if (groupWidth < 1) throw new ArgumentOutOfRangeException("groupWidth");
            return groupWidth * 1.22 / 1774;
        }

        private static RectangleF ArtworkTarget(int cat, int groupWidth, Bitmap artwork)
        {
            double scale = Scale(groupWidth);
            Point grip = GripOffset(cat, groupWidth);
            return new RectangleF((float)(grip.X - grips[cat].X * scale), (float)(grip.Y - grips[cat].Y * scale),
                (float)(artwork.Width / 3 * scale), (float)(artwork.Height * scale));
        }

        internal CostumePose GetCostumePose(int cat, int groupWidth)
        {
            return CostumeLandmarks.Lift[cat].Project(ArtworkTarget(cat, groupWidth, cat == 2 ? graySource : source),
                240, 240, groupWidth, false);
        }

        public Bitmap Render(int cat, int groupWidth)
        {
            if (cat < 0 || cat >= 3) throw new ArgumentOutOfRangeException("cat");
            if (groupWidth < 1) throw new ArgumentOutOfRangeException("groupWidth");
            if (renderedWidth != groupWidth)
            {
                foreach (var frame in frames) if (frame != null) frame.Dispose();
                Array.Clear(frames, 0, frames.Length);
                renderedWidth = groupWidth;
            }
            if (frames[cat] != null) return frames[cat];
            Size size = FrameSize(groupWidth);
            var rendered = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            Bitmap artwork = cat == 2 ? graySource : source;
            int cellWidth = artwork.Width / 3;
            var target = ArtworkTarget(cat, groupWidth, artwork);
            using (var graphics = Graphics.FromImage(rendered))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(artwork, target, new Rectangle(cat * cellWidth, 0, cellWidth, artwork.Height), GraphicsUnit.Pixel);
            }
            frames[cat] = rendered;
            return rendered;
        }

        public void Dispose()
        {
            foreach (var frame in frames) if (frame != null) frame.Dispose();
            source.Dispose();
            graySource.Dispose();
        }
    }
}
