using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ThreeCats;

class DailyRendererTests
{
    sealed class FixedRandom : Random
    {
        readonly int cat;
        readonly int kind;

        public FixedRandom(int cat, int kind)
        {
            this.cat = cat;
            this.kind = kind;
        }

        public override double NextDouble() { return 0; }
        public override int Next(int max) { return max == 3 ? cat : kind; }
    }

    static void CheckBorders(Bitmap bitmap)
    {
        for (int x = 0; x < bitmap.Width; x++)
        {
            if (bitmap.GetPixel(x, 0).A != 0 || bitmap.GetPixel(x, bitmap.Height - 1).A != 0) throw new Exception("Vertical clipping");
        }
        for (int y = 0; y < bitmap.Height; y++)
        {
            if (bitmap.GetPixel(0, y).A != 0 || bitmap.GetPixel(bitmap.Width - 1, y).A != 0) throw new Exception("Horizontal clipping");
        }
    }

    static void CheckOriginal(SpriteRenderer idle, Bitmap rendered, int cat, double time)
    {
        Bitmap original = idle.RenderCat(cat, 300, .2, .1, .3, .2, time);
        Point offset = DailyRenderer.FootOffset(300);
        Point old = SpriteRenderer.CatFootOffset(300);
        for (int y = 0; y < original.Height; y++)
        {
            for (int x = 0; x < original.Width; x++)
            {
                if (original.GetPixel(x, y) != rendered.GetPixel(x + offset.X - old.X, y + offset.Y - old.Y)) throw new Exception("Endpoint identity or pixels");
            }
        }
    }

    static void CheckMirror(SpriteRenderer idle, DailyRenderer renderer)
    {
        var layout = new CatLayout(Point.Empty, 300);
        var left = new DailyRoutine(new FixedRandom(1, 2));
        var right = new DailyRoutine(new FixedRandom(1, 2));
        left.Update(20.8, layout, new Point(-900, 0), true);
        right.Update(20.8, layout, new Point(900, 0), true);
        using (var first = (Bitmap)renderer.Render(idle, 1, 300, 0, 0, 0, 0, 0, left).Clone())
        {
            Bitmap second = renderer.Render(idle, 1, 300, 0, 0, 0, 0, 0, right);
            int axis = DailyRenderer.FootOffset(300).X * 2 - 1;
            for (int y = 0; y < first.Height; y++)
            {
                for (int x = 0; x < first.Width; x++)
                {
                    int other = axis - x;
                    if (other >= 0 && other < second.Width && first.GetPixel(x, y) != second.GetPixel(other, y)) throw new Exception("Walk mirror");
                }
            }
        }
    }

    static int Main()
    {
        try
        {
            Run();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    static void CheckDetachedFragments()
    {
        int[] source = new int[1536 * 1024];
        for (int y = 40; y < 180; y++)
        {
            for (int x = 50; x < 170; x++) source[y * 1536 + x] = unchecked((int)0xff808080);
        }
        source[60 * 1536 + 174] = unchecked((int)0xff808080);
        source[80 * 1536 + 220] = unchecked((int)0xff808080);
        var method = typeof(DailyRenderer).GetMethod("CleanCell", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        method.Invoke(null, new object[] { source, 0 });
        if (source[80 * 1536 + 220] != 0) throw new Exception("Detached atlas fragment retained");
        if (source[60 * 1536 + 174] == 0) throw new Exception("Nearby whisker removed");
        if (source[100 * 1536 + 100] == 0) throw new Exception("Main body removed");
    }

    static void Run()
    {
        CheckDetachedFragments();
        Directory.CreateDirectory("work/v14-preview");
        using (var idle = new SpriteRenderer())
        using (var renderer = new DailyRenderer())
        {
            if (DailyRenderer.FrameSize(1774).Width != 1600) throw new Exception("Size");
            CheckMirror(idle, renderer);
            for (int kind = 0; kind < 4; kind++)
            {
                var routines = new DailyRoutine[3];
                var layout = new CatLayout(Point.Empty, 300);
                for (int cat = 0; cat < 3; cat++)
                {
                    routines[cat] = new DailyRoutine(new FixedRandom(cat, kind));
                    routines[cat].Update(20, layout, new Point(200, 0), true);
                }
                int count = kind < 2 ? 70 : 160;
                for (int index = 0; index < count; index++)
                {
                    using (var preview = new Bitmap(840, 280))
                    using (var graphics = Graphics.FromImage(preview))
                    {
                        graphics.Clear(Color.FromArgb(245, 242, 236));
                        for (int cat = 0; cat < 3; cat++)
                        {
                            var routine = routines[cat];
                            Bitmap rendered = renderer.Render(idle, cat, 300, .2, .1, .3, .2, index * .08, routine);
                            CheckBorders(rendered);
                            if (routine.Blend == 0) CheckOriginal(idle, rendered, cat, index * .08);
                            graphics.DrawImageUnscaled(rendered, cat * 280 + 5, 5);
                            routine.Update(.08, layout, new Point(200, 0), false);
                        }
                        if (kind < 2)
                        {
                            string name = kind == 0 ? "stretch" : "roll";
                            preview.Save("work/v14-preview/" + name + index.ToString("000") + ".png", ImageFormat.Png);
                        }
                    }
                }
            }
            var waiting = new DailyRoutine(new FixedRandom(0, 0));
            Bitmap cached = renderer.Render(idle, 0, 1774, 0, 0, 0, 0, 0, waiting);
            if (!Object.ReferenceEquals(cached, renderer.Render(idle, 0, 1774, 0, 0, 0, 0, 0, waiting))) throw new Exception("Cache");
        }
        Console.WriteLine("PASS identity endpoints, transparency, borders, mirror, cache, resize, all routine timelines");
    }
}
