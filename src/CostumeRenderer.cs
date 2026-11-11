using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ThreeCats
{
    internal struct CostumePose
    {
        internal readonly PointF Hat;
        internal readonly PointF Scarf;
        internal readonly float Angle;
        internal readonly float Scale;
        internal readonly bool Mirrored;

        internal CostumePose(PointF hat, PointF scarf, float angle, float scale, bool mirrored = false)
        {
            Hat = hat;
            Scarf = scarf;
            Angle = angle;
            Scale = scale;
            Mirrored = mirrored;
        }

        internal CostumePose InFrame(float x, float y, float verticalScale = 1)
        {
            return new CostumePose(new PointF(Hat.X + x, Hat.Y * verticalScale + y),
                new PointF(Scarf.X + x, Scarf.Y * verticalScale + y), Angle, Scale * verticalScale, Mirrored);
        }

        internal static CostumePose Blend(CostumePose from, CostumePose to, double mix)
        {
            float t = (float)mix;
            float rotation = (to.Angle - from.Angle + 540) % 360 - 180;
            return new CostumePose(Lerp(from.Hat, to.Hat, t), Lerp(from.Scarf, to.Scarf, t), from.Angle + rotation * t,
                from.Scale + (to.Scale - from.Scale) * t, t < .5f ? from.Mirrored : to.Mirrored);
        }

        private static PointF Lerp(PointF from, PointF to, float t)
        {
            return new PointF(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);
        }
    }

    internal sealed class CostumeRenderer : IDisposable
    {
        private readonly Bitmap[] hats = new Bitmap[4];
        private readonly Bitmap[] scarves = new Bitmap[4];
        private readonly Bitmap[] frames = new Bitmap[3];

        internal static int Padding(int groupWidth, Costume costume)
        {
            return costume == Costume.None ? 0 : (int)Math.Ceiling(groupWidth * .065);
        }

        internal Bitmap Render(int catId, Bitmap cat, int groupWidth, Costume costume, CostumePose pose)
        {
            int padding = Padding(groupWidth, costume);
            var size = new Size(cat.Width + padding * 2, cat.Height + padding * 2);
            Bitmap output = frames[catId];
            if (output == null || output.Size != size)
            {
                if (output != null) output.Dispose();
                output = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                frames[catId] = output;
            }
            int index = (int)costume;
            if (hats[index] == null)
            {
                hats[index] = DrawHat(costume);
                scarves[index] = DrawScarf(costume);
            }
            using (var graphics = Graphics.FromImage(output))
            {
                graphics.Clear(Color.Transparent);
                graphics.DrawImageUnscaled(cat, padding, padding);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                DrawPiece(graphics, scarves[index], pose.Scarf, new Point(62, 19), pose, padding);
                DrawPiece(graphics, hats[index], pose.Hat, new Point(62, 62), pose, padding);
            }
            return output;
        }

        private static void DrawPiece(Graphics g, Bitmap piece, PointF anchor, Point origin, CostumePose pose, int padding)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(anchor.X + padding, anchor.Y + padding);
            g.RotateTransform(pose.Angle);
            g.ScaleTransform(pose.Mirrored ? -pose.Scale : pose.Scale, pose.Scale);
            g.DrawImage(piece, new Rectangle(-origin.X, -origin.Y, piece.Width, piece.Height),
                0, 0, piece.Width, piece.Height, GraphicsUnit.Pixel);
            g.Restore(state);
        }

        private static Bitmap DrawHat(Costume costume)
        {
            var bitmap = new Bitmap(124, 76, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bitmap))
            using (var outline = new Pen(Color.FromArgb(58, 30, 42), 2.6f) { LineJoin = LineJoin.Round })
            using (var red = new SolidBrush(Color.FromArgb(196, 49, 66)))
            using (var ruby = new SolidBrush(Color.FromArgb(137, 29, 49)))
            using (var bright = new SolidBrush(Color.FromArgb(232, 92, 92)))
            using (var ivory = new SolidBrush(Color.FromArgb(255, 241, 217)))
            using (var gold = new SolidBrush(Color.FromArgb(245, 180, 69)))
            using (var violet = new SolidBrush(Color.FromArgb(89, 57, 126)))
            using (var plum = new SolidBrush(Color.FromArgb(55, 39, 76)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (costume == Costume.Christmas)
                {
                    Shape(g, red, outline, 20, 58, 27, 41, 39, 25, 54, 13, 73, 8, 92, 14, 103, 27, 105, 41, 94, 44, 83, 32, 86, 58);
                    Shape(g, ruby, null, 69, 17, 84, 26, 91, 43, 82, 33, 86, 58, 74, 59, 73, 34);
                    Shape(g, bright, null, 32, 49, 42, 33, 56, 22, 66, 20, 53, 32, 45, 49);
                    Oval(g, ivory, outline, 87, 34, 25, 25);
                    Oval(g, ivory, outline, 15, 50, 81, 21);
                }
                else if (costume == Costume.Halloween)
                {
                    Shape(g, plum, outline, 28, 57, 37, 37, 51, 19, 74, 6, 69, 24, 89, 57);
                    Shape(g, violet, null, 37, 52, 49, 32, 62, 21, 57, 42, 53, 55);
                    Oval(g, violet, outline, 9, 52, 106, 19);
                    Shape(g, red, outline, 33, 46, 48, 51, 82, 47, 89, 58, 65, 62, 28, 57);
                    g.FillRectangle(gold, 56, 48, 14, 13);
                    g.FillRectangle(plum, 60, 51, 6, 7);
                }
                else
                {
                    Shape(g, red, outline, 21, 57, 23, 43, 33, 29, 49, 22, 74, 22, 91, 29, 102, 44, 103, 57, 94, 64, 28, 64);
                    Shape(g, bright, null, 28, 49, 37, 35, 51, 28, 57, 28, 50, 37, 46, 51);
                    Shape(g, ruby, null, 75, 27, 91, 34, 99, 45, 100, 55, 84, 55);
                    Shape(g, ruby, outline, 19, 53, 104, 53, 105, 64, 97, 68, 26, 68, 19, 64);
                    g.FillRectangle(gold, 28, 56, 67, 3);
                    Shape(g, gold, outline, 62, 36, 72, 46, 62, 57, 52, 46);
                    using (var jade = new SolidBrush(Color.FromArgb(93, 187, 153))) Oval(g, jade, null, 58, 42, 8, 8);
                    Oval(g, gold, outline, 56, 15, 12, 11);
                }
            }
            return bitmap;
        }

        private static Bitmap DrawScarf(Costume costume)
        {
            var bitmap = new Bitmap(124, 66, PixelFormat.Format32bppPArgb);
            Color fabric = costume == Costume.Christmas ? Color.FromArgb(174, 39, 62)
                : costume == Costume.Halloween ? Color.FromArgb(205, 98, 41) : Color.FromArgb(173, 42, 55);
            Color trim = costume == Costume.Christmas ? Color.FromArgb(248, 207, 180)
                : costume == Costume.Halloween ? Color.FromArgb(102, 58, 132) : Color.FromArgb(248, 182, 75);
            using (var g = Graphics.FromImage(bitmap))
            using (var outline = new Pen(Color.FromArgb(63, 31, 45), 2.6f) { LineJoin = LineJoin.Round })
            using (var main = new SolidBrush(fabric))
            using (var highlight = new SolidBrush(trim))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Shape(g, main, outline, 71, 19, 96, 16, 91, 36, 94, 52, 75, 57, 72, 47, 74, 35);
                Shape(g, highlight, null, 74, 43, 92, 39, 93, 44, 74, 49);
                Shape(g, highlight, null, 77, 55, 93, 50, 94, 56, 91, 60, 88, 58, 85, 62, 82, 60, 78, 63);
                Shape(g, main, outline, 18, 12, 31, 15, 53, 21, 74, 20, 95, 14, 106, 11, 107, 23, 98, 28, 76, 34, 51, 35, 26, 29, 16, 23);
                Shape(g, highlight, null, 19, 18, 48, 27, 73, 27, 98, 20, 105, 17, 103, 22, 77, 31, 48, 32, 20, 24);
                if (costume == Costume.SpringFestival)
                {
                    Shape(g, highlight, outline, 77, 22, 87, 17, 95, 25, 90, 33, 81, 31);
                    using (var tassel = new Pen(trim, 2.5f)) g.DrawLine(tassel, 87, 32, 88, 50);
                }
            }
            return bitmap;
        }

        private static void Shape(Graphics g, Brush fill, Pen outline, params int[] coordinates)
        {
            var points = new Point[coordinates.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Point(coordinates[i * 2], coordinates[i * 2 + 1]);
            g.FillPolygon(fill, points);
            if (outline != null) g.DrawPolygon(outline, points);
        }

        private static void Oval(Graphics g, Brush fill, Pen outline, int x, int y, int width, int height)
        {
            g.FillEllipse(fill, x, y, width, height);
            if (outline != null) g.DrawEllipse(outline, x, y, width, height);
        }

        public void Dispose()
        {
            foreach (var frame in frames) if (frame != null) frame.Dispose();
            foreach (var hat in hats) if (hat != null) hat.Dispose();
            foreach (var scarf in scarves) if (scarf != null) scarf.Dispose();
        }
    }
}
