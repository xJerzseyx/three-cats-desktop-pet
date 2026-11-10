using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ThreeCats
{
    public sealed class SpriteRenderer : IDisposable
    {
        private readonly int[] source;
        private readonly int sourceWidth;
        private readonly int sourceHeight;
        private readonly double[] centers = { 393, 904, 1441 };
        private readonly int[] edges = { 0, 610, 1220, 1774 };
        private readonly double[,] eyeCentersX = { { 333, 453 }, { 845, 955 }, { 1375, 1494 } };
        private readonly double[] eyeCentersY = { 243, 245, 249 };
        private Bitmap frame;
        private int[] pixels;
        public const double Aspect = 887.0 / 1774.0;

        public SpriteRenderer()
        {
            using (var stream = typeof(SpriteRenderer).Assembly.GetManifestResourceStream("ThreeCats.Cats.png"))
            using (var original = new Bitmap(stream))
            using (var converted = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppPArgb))
            {
                using (var graphics = Graphics.FromImage(converted)) graphics.DrawImageUnscaled(original, 0, 0);
                sourceWidth = converted.Width;
                sourceHeight = converted.Height;
                source = new int[sourceWidth * sourceHeight];
                var data = converted.LockBits(new Rectangle(0, 0, sourceWidth, sourceHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try { Marshal.Copy(data.Scan0, source, 0, source.Length); }
                finally { converted.UnlockBits(data); }
            }
        }

        public Bitmap Render(int width, double[] horizontal, double[] vertical, double musicMix, double energy, double time)
        {
            int height = (int)Math.Round(width * Aspect);
            if (frame == null || frame.Width != width)
            {
                if (frame != null) frame.Dispose();
                frame = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                pixels = new int[width * height];
            }
            Array.Clear(pixels, 0, pixels.Length);
            double scale = (double)width / sourceWidth;
            // A continuous inverse warp bends the neck without exposing a layer seam.
            double sway = Math.Sin(time * 3.5) * (0.008 + energy * 0.014) * musicMix;
            double nod = Math.Sin(time * 7.0) * (3 + energy * 13) * musicMix;
            for (int cat = 0; cat < 3; cat++)
            {
                double center = centers[cat];
                double headAngle = horizontal[cat] * 0.065 * (1 - musicMix)
                    + Math.Sin(time * 3.5) * (0.025 + energy * 0.055) * musicMix;
                double offsetX = horizontal[cat] * 13 * (1 - musicMix);
                double offsetY = vertical[cat] * 9 * (1 - musicMix) + nod;
                double bodyCos = Math.Cos(sway);
                double bodySin = Math.Sin(sway);
                int fromX = Math.Max(0, (int)((edges[cat] - 28) * scale));
                int toX = Math.Min(width, (int)Math.Ceiling((edges[cat + 1] + 28) * scale));
                for (int y = 0; y < height; y++)
                {
                    double sourceY = (y + 0.5) / scale;
                    double blend = Math.Max(0, Math.Min(1, (480 - sourceY) / 155.0));
                    blend = blend * blend * (3 - 2 * blend);
                    double cosine = Math.Cos(headAngle * blend);
                    double sine = Math.Sin(headAngle * blend);
                    for (int x = fromX; x < toX; x++)
                    {
                        double dx = (x + 0.5) / scale - center;
                        double dy = sourceY - 825;
                        double bodyX = center + bodyCos * dx + bodySin * dy;
                        double bodyY = 825 - bodySin * dx + bodyCos * dy;
                        double headX = bodyX - center - offsetX * blend;
                        double headY = bodyY - 335 - offsetY * blend;
                        int sampleX = (int)Math.Floor(center + cosine * headX + sine * headY);
                        int sampleY = (int)Math.Floor(335 - sine * headX + cosine * headY);
                        if (sampleX < edges[cat] || sampleX >= edges[cat + 1] || sampleY < 0 || sampleY >= sourceHeight) continue;
                        // Deform only the iris interior. Eye outlines and original eye colors stay anchored.
                        if (sampleY > 222 && sampleY < 273)
                        {
                            for (int eye = 0; eye < 2; eye++)
                            {
                                double eyeX = sampleX - eyeCentersX[cat, eye];
                                double eyeY = sampleY - eyeCentersY[cat];
                                double distance = eyeX * eyeX / 400.0 + eyeY * eyeY / 289.0;
                                if (distance >= 1) continue;
                                double weight = Math.Min(1, (1 - distance) * 2);
                                sampleX = (int)Math.Floor(sampleX - horizontal[cat] * 7 * weight + 0.5);
                                sampleY = (int)Math.Floor(sampleY - vertical[cat] * 5 * weight + 0.5);
                                break;
                            }
                        }
                        int color = source[sampleY * sourceWidth + sampleX];
                        if ((color & unchecked((int)0xff000000)) != 0) pixels[y * width + x] = color;
                    }
                }
            }
            var locked = frame.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try { Marshal.Copy(pixels, 0, locked.Scan0, pixels.Length); }
            finally { frame.UnlockBits(locked); }
            return frame;
        }

        public void Dispose() { if (frame != null) frame.Dispose(); }
    }
}
