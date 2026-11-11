using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ThreeCats
{
    public sealed class DailyRenderer : IDisposable
    {
        readonly int[][] atlases = new int[3][];
        readonly Rectangle[,] bounds = new Rectangle[3, 24];
        static readonly int[] sourceBottom = { 840, 838, 836 };
        readonly double[] proportions = new double[3];
        Bitmap output;
        int[] pixels;
        int[] originalPixels;

        public DailyRenderer()
        {
            string[] names = { "Black", "Ragdoll", "Gray" };
            int[] heights = { 777, 769, 771 };
            for (int cat = 0; cat < 3; cat++)
            {
                using (var stream = typeof(DailyRenderer).Assembly.GetManifestResourceStream("ThreeCats.Daily." + names[cat] + ".png"))
                using (var original = new Bitmap(stream))
                using (var atlas = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppPArgb))
                {
                    using (var graphics = Graphics.FromImage(atlas)) graphics.DrawImageUnscaled(original, 0, 0);
                    int[] source = Read(atlas, null);
                    for (int i = 0; i < source.Length; i++)
                    {
                        if ((uint)source[i] >> 24 <= 32) source[i] = 0;
                    }
                    for (int frame = 0; frame < 24; frame++)
                    {
                        CleanCell(source, frame);
                        int left = 256;
                        int top = 256;
                        int right = 0;
                        int bottom = 0;
                        for (int y = 0; y < 256; y++)
                        {
                            for (int x = 0; x < 256; x++)
                            {
                                int color = source[((frame / 6) * 256 + y) * 1536 + (frame % 6) * 256 + x];
                                if ((uint)color >> 24 <= 32) continue;
                                left = Math.Min(left, x);
                                top = Math.Min(top, y);
                                right = Math.Max(right, x + 1);
                                bottom = Math.Max(bottom, y + 1);
                            }
                        }
                        if (right <= left || bottom <= top) throw new InvalidOperationException("Empty atlas frame");
                        bounds[cat, frame] = Rectangle.FromLTRB(left, top, right, bottom);
                    }
                    atlases[cat] = source;
                    proportions[cat] = heights[cat] / (double)bounds[cat, 0].Height;
                }
            }
        }

        // Connected components keep neighboring atlas cells from contributing detached parts.
        static void CleanCell(int[] source, int frame)
        {
            int[] labels = new int[256 * 256];
            int[] queue = new int[labels.Length];
            var components = new System.Collections.Generic.List<Rectangle>();
            var counts = new System.Collections.Generic.List<int>();
            int largest = -1;
            int originX = frame % 6 * 256;
            int originY = frame / 6 * 256;
            for (int position = 0; position < labels.Length; position++)
            {
                int x = position % 256;
                int y = position / 256;
                if (labels[position] != 0 || source[(originY + y) * 1536 + originX + x] == 0) continue;
                int label = components.Count + 1;
                int head = 0;
                int tail = 1;
                queue[0] = position;
                labels[position] = label;
                int left = x;
                int top = y;
                int right = x;
                int bottom = y;
                while (head < tail)
                {
                    int item = queue[head++];
                    int currentX = item % 256;
                    int currentY = item / 256;
                    left = Math.Min(left, currentX);
                    top = Math.Min(top, currentY);
                    right = Math.Max(right, currentX);
                    bottom = Math.Max(bottom, currentY);
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nextX = currentX + dx;
                            int nextY = currentY + dy;
                            if (nextX < 0 || nextY < 0 || nextX >= 256 || nextY >= 256) continue;
                            int next = nextY * 256 + nextX;
                            if (labels[next] != 0 || source[(originY + nextY) * 1536 + originX + nextX] == 0) continue;
                            labels[next] = label;
                            queue[tail++] = next;
                        }
                    }
                }
                components.Add(Rectangle.FromLTRB(left, top, right + 1, bottom + 1));
                counts.Add(tail);
                if (largest < 0 || tail > counts[largest]) largest = components.Count - 1;
            }
            if (largest < 0) return;
            Rectangle retained = components[largest];
            retained.Inflate(10, 10);
            for (int position = 0; position < labels.Length; position++)
            {
                int label = labels[position];
                if (label == 0 || retained.IntersectsWith(components[label - 1])) continue;
                source[(originY + position / 256) * 1536 + originX + position % 256] = 0;
            }
        }
        public static Size FrameSize(int groupWidth)
        {
            int side = Math.Max(1, (int)Math.Ceiling(groupWidth * 1600.0 / 1774));
            return new Size(side, side);
        }

        public static Point FootOffset(int groupWidth)
        {
            return new Point((int)Math.Round(groupWidth * 800.0 / 1774), (int)Math.Round(groupWidth * 1450.0 / 1774));
        }

        public Bitmap Render(SpriteRenderer idle, int catId, int groupWidth, double horizontal, double vertical, double musicMix, double energy, double time, DailyRoutine routine)
        {
            if (catId < 0 || catId > 2) throw new ArgumentOutOfRangeException("catId");
            if (groupWidth < 1) throw new ArgumentOutOfRangeException("groupWidth");
            Size size = FrameSize(groupWidth);
            if (output == null || output.Size != size)
            {
                if (output != null) output.Dispose();
                output = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                pixels = new int[size.Width * size.Height];
            }
            Bitmap original = idle.RenderCat(catId, groupWidth, horizontal, vertical, musicMix, energy, time);
            originalPixels = Read(original, originalPixels);
            int previous;
            int next;
            double mix;
            Select(routine, out previous, out next, out mix);
            Point foot = FootOffset(groupWidth);
            Point oldFoot = SpriteRenderer.CatFootOffset(groupWidth);
            double scale = groupWidth / 1774.0 * proportions[catId];
            double baseline = foot.Y + Math.Round((sourceBottom[catId] - 825) * scale / proportions[catId]);
            bool mirror = routine.Facing < 0 && routine.Kind != RoutineKind.Stretch && routine.Kind != RoutineKind.Roll;
            Array.Clear(pixels, 0, pixels.Length);
            Rectangle region = TargetBounds(previous, catId, foot, oldFoot, scale, original.Size);
            if (mix == 1) region = TargetBounds(next, catId, foot, oldFoot, scale, original.Size);
            else if (mix > 0) region = Rectangle.Union(region, TargetBounds(next, catId, foot, oldFoot, scale, original.Size));
            region.Intersect(new Rectangle(Point.Empty, size));
            for (int y = region.Top; y < region.Bottom; y++)
            {
                for (int x = region.Left; x < region.Right; x++)
                {
                    if (mix == 0 || previous == next)
                    {
                        pixels[y * size.Width + x] = Sample(previous, catId, x, y, foot, oldFoot, scale, baseline, mirror, original.Size);
                        continue;
                    }
                    if (mix == 1)
                    {
                        pixels[y * size.Width + x] = Sample(next, catId, x, y, foot, oldFoot, scale, baseline, mirror, original.Size);
                        continue;
                    }
                    int first = Sample(previous, catId, x, y, foot, oldFoot, scale, baseline, mirror, original.Size);
                    int second = Sample(next, catId, x, y, foot, oldFoot, scale, baseline, mirror, original.Size);
                    int color = 0;
                    for (int shift = 0; shift < 32; shift += 8)
                    {
                        int channel = (int)Math.Round(((first >> shift) & 255) * (1 - mix) + ((second >> shift) & 255) * mix);
                        color |= channel << shift;
                    }
                    pixels[y * size.Width + x] = color;
                }
            }
            var data = output.LockBits(new Rectangle(Point.Empty, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
            finally { output.UnlockBits(data); }
            return output;
        }

        Rectangle TargetBounds(int frame, int cat, Point foot, Point oldFoot, double scale, Size originalSize)
        {
            if (frame < 0) return new Rectangle(foot.X - oldFoot.X, foot.Y - oldFoot.Y, originalSize.Width, originalSize.Height);
            Rectangle box = bounds[cat, frame];
            double baseline = foot.Y + Math.Round((sourceBottom[cat] - 825) * scale / proportions[cat]);
            int left = (int)Math.Floor(foot.X - box.Width * scale / 2) - 1;
            int top = (int)Math.Floor(baseline - box.Height * scale) - 1;
            int right = (int)Math.Ceiling(foot.X + box.Width * scale / 2) + 1;
            int bottom = (int)Math.Ceiling(baseline) + 1;
            return Rectangle.FromLTRB(left, top, right, bottom);
        }
        int Sample(int frame, int cat, int x, int y, Point foot, Point oldFoot, double scale, double baseline, bool mirror, Size originalSize)
        {
            if (frame < 0)
            {
                int sourceX = x - foot.X + oldFoot.X;
                int sourceY = y - foot.Y + oldFoot.Y;
                if (sourceX < 0 || sourceY < 0 || sourceX >= originalSize.Width || sourceY >= originalSize.Height) return 0;
                return originalPixels[sourceY * originalSize.Width + sourceX];
            }
            Rectangle box = bounds[cat, frame];
            double localX = (x + .5 - foot.X) / scale;
            if (mirror) localX = -localX;
            int atlasX = (int)Math.Floor(box.X + box.Width / 2.0 + localX);
            int atlasY = (int)Math.Floor(box.Bottom + (y + .5 - baseline) / scale);
            if (atlasX < 0 || atlasY < 0 || atlasX >= 256 || atlasY >= 256) return 0;
            return atlases[cat][((frame / 6) * 256 + atlasY) * 1536 + (frame % 6) * 256 + atlasX];
        }

        static double Ease(double progress)
        {
            progress = Math.Max(0, Math.Min(1, progress));
            return progress * progress * (3 - 2 * progress);
        }

        static void Select(DailyRoutine routine, out int previous, out int next, out double mix)
        {
            previous = -1;
            next = -1;
            mix = 0;
            if (routine.Phase == RoutinePhase.Waiting || routine.Blend <= 0) return;
            int first = 13;
            int last = 13;
            if (routine.Kind == RoutineKind.Stretch) { first = 1; last = 5; }
            if (routine.Kind == RoutineKind.Roll) { first = 7; last = 11; }
            if (routine.Phase == RoutinePhase.Entering)
            {
                next = first;
                mix = Ease(routine.Progress);
                return;
            }
            if (routine.Phase == RoutinePhase.Settling)
            {
                previous = last;
                mix = Ease(routine.Progress);
                return;
            }
            double segment = .125;
            int start = 13;
            int count = 4;
            bool loop = true;
            if (routine.Phase == RoutinePhase.Acting)
            {
                if (routine.Kind == RoutineKind.Stretch || routine.Kind == RoutineKind.Roll)
                {
                    start = first;
                    count = 5;
                    segment = (routine.Kind == RoutineKind.Stretch ? 3.2 : 4.2) / count;
                    loop = false;
                }
                else
                {
                    start = routine.Kind == RoutineKind.Drink ? 18 : 21;
                    count = 2;
                    segment = .22;
                }
            }
            if (routine.Phase == RoutinePhase.Lowering || routine.Phase == RoutinePhase.Rising)
            {
                previous = 13;
                next = routine.Kind == RoutineKind.Drink ? 17 : 21;
                if (routine.Phase == RoutinePhase.Rising)
                {
                    previous = routine.Kind == RoutineKind.Drink ? 19 : 22;
                    next = routine.Kind == RoutineKind.Drink ? 20 : 23;
                }
                mix = Ease(routine.Progress);
                return;
            }
            int step = (int)(routine.PhaseSeconds / segment);
            int index = loop ? step % count : Math.Min(count - 1, step);
            next = start + index;
            previous = next;
            if (step > 0)
            {
                int previousIndex = loop ? (step - 1) % count : Math.Min(count - 1, step - 1);
                previous = start + previousIndex;
            }
            if (step == 0)
            {
                if (routine.Phase == RoutinePhase.WalkingBack) previous = routine.Kind == RoutineKind.Drink ? 20 : 23;
                if (routine.Phase == RoutinePhase.Acting && routine.Kind == RoutineKind.Drink) previous = 17;
            }
            mix = Ease((routine.PhaseSeconds % segment) / .08);
        }

        static int[] Read(Bitmap bitmap, int[] buffer)
        {
            if (buffer == null || buffer.Length != bitmap.Width * bitmap.Height) buffer = new int[bitmap.Width * bitmap.Height];
            var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try { Marshal.Copy(data.Scan0, buffer, 0, buffer.Length); }
            finally { bitmap.UnlockBits(data); }
            return buffer;
        }

        public void Dispose()
        {
            if (output != null) output.Dispose();
        }
    }
}
