using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ThreeCats
{
    internal sealed class SpineDragonRenderer : IDisposable
    {
        private const int CellWidth = 384;
        private const int CellHeight = 340;
        private const int Baseline = 310;
        private readonly Bitmap atlas;
        private Bitmap output;

        internal SpineDragonRenderer()
        {
            using (var stream = typeof(SpineDragonRenderer).Assembly.GetManifestResourceStream("ThreeCats.SpineDragon.png"))
            using (var image = new Bitmap(stream))
                atlas = new Bitmap(image);
        }

        private static float RecoveryScale(SpineDragon action) { return .78f + .22f * (float)action.Recovery; }

        private static RectangleF ArtworkTarget(int groupWidth, Point foot)
        {
            float scale = groupWidth / 720f;
            return new RectangleF(foot.X - CellWidth * scale / 2, foot.Y - Baseline * scale,
                CellWidth * scale, CellHeight * scale);
        }

        internal CostumePose GetCostumePose(int cat, int groupWidth, SpineDragon action, CostumePose idle)
        {
            Point foot = DailyRenderer.FootOffset(groupWidth);
            if (action.Recovery <= 0)
                return CostumeLandmarks.Dragon[cat, action.Frame].Project(ArtworkTarget(groupWidth, foot),
                    240, 240, groupWidth, action.Mirrored);
            Point idleFoot = SpriteRenderer.CatFootOffset(groupWidth);
            float stretch = RecoveryScale(action);
            return idle.InFrame(foot.X - idleFoot.X, foot.Y - idleFoot.Y * stretch, stretch);
        }

        internal Bitmap Render(int cat, int groupWidth, SpineDragon action, Bitmap idle)
        {
            Size size = DailyRenderer.FrameSize(groupWidth);
            if (output == null || output.Size != size)
            {
                if (output != null) output.Dispose();
                output = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            }

            Point foot = DailyRenderer.FootOffset(groupWidth);
            using (var graphics = Graphics.FromImage(output))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                if (idle != null)
                {
                    float stretch = RecoveryScale(action);
                    Point idleFoot = SpriteRenderer.CatFootOffset(groupWidth);
                    var target = new RectangleF(foot.X - idleFoot.X, foot.Y - idleFoot.Y * stretch, idle.Width, idle.Height * stretch);
                    graphics.DrawImage(idle, target);
                }
                else
                {
                    RectangleF artwork = ArtworkTarget(groupWidth, foot);
                    var target = new[]
                    {
                        new PointF(action.Mirrored ? artwork.Right : artwork.Left, artwork.Top),
                        new PointF(action.Mirrored ? artwork.Left : artwork.Right, artwork.Top),
                        new PointF(action.Mirrored ? artwork.Right : artwork.Left, artwork.Bottom)
                    };
                    var source = new RectangleF(action.Frame * CellWidth, cat * CellHeight, CellWidth, CellHeight);
                    graphics.DrawImage(atlas, target, source, GraphicsUnit.Pixel);
                }
            }
            return output;
        }

        public void Dispose()
        {
            if (output != null) output.Dispose();
            atlas.Dispose();
        }
    }
}
