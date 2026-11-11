using System;
using System.Drawing;
using ThreeCats;

internal static class RendererTests
{
    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new Exception(label);
        }
    }

    private static int CountChangedPixels(Bitmap previous, Bitmap current)
    {
        int changedPixels = 0;
        for (int y = 0; y < previous.Height; y++)
        {
            for (int x = 0; x < previous.Width; x++)
            {
                if (previous.GetPixel(x, y) != current.GetPixel(x, y))
                {
                    changedPixels++;
                }
            }
        }
        return changedPixels;
    }

    private static void Main()
    {
        Assert(SpriteRenderer.CatFrameSize(1774) == new Size(850, 887), "Full resolution canvas");
        Assert(SpriteRenderer.CatFrameSize(1) == new Size(1, 1), "Minimum canvas dimensions");
        Assert(SpriteRenderer.CatFootOffset(1774) == new Point(425, 825), "Full resolution foot pivot");
        Assert(SpriteRenderer.CatFrameSize(600) == new Size(288, 300), "Scaled canvas rounding");
        Assert(SpriteRenderer.CatFootOffset(600) == new Point(144, 279), "Scaled foot pivot rounding");

        int[] centers = { 393, 904, 1441 };
        int[] sourceEdges = { 0, 610, 1220, 1774 };
        using (var renderer = new SpriteRenderer())
        using (var groupRenderer = new SpriteRenderer())
        {
            Bitmap group = groupRenderer.Render(1774, new double[3], new double[3], 0, 0, 0);
            for (int cat = 0; cat < 3; cat++)
            {
                Bitmap single = renderer.RenderCat(cat, 1774, 0, 0, 0, 0, 0);
                int mismatches = 0;
                for (int y = 0; y < 887; y++)
                {
                    for (int x = 0; x < 850; x++)
                    {
                        int sourceX = x + centers[cat] - 425;
                        int expectedArgb = 0;
                        if (sourceX >= sourceEdges[cat] && sourceX < sourceEdges[cat + 1])
                        {
                            expectedArgb = group.GetPixel(sourceX, y).ToArgb();
                        }
                        if (single.GetPixel(x, y).ToArgb() != expectedArgb)
                        {
                            mismatches++;
                        }
                    }
                }
                Assert(mismatches == 0, "Cat " + cat + " crop equality and transparent identity boundaries");

                using (var previous = (Bitmap)single.Clone())
                {
                    Bitmap repeated = renderer.RenderCat(cat, 1774, 0, 0, 0, 0, 0);
                    Assert(object.ReferenceEquals(single, repeated), "Cat " + cat + " output bitmap reuse");
                    Bitmap looking = renderer.RenderCat(cat, 1774, 1, 1, 0, 0, 0);
                    Assert(CountChangedPixels(previous, looking) > 0, "Cat " + cat + " gaze response");
                }

                using (var previous = (Bitmap)renderer.RenderCat(cat, 1774, 0, 0, 1, 0.5, 1).Clone())
                {
                    Bitmap looking = renderer.RenderCat(cat, 1774, 1, 1, 1, 0.5, 1);
                    Assert(CountChangedPixels(previous, looking) > 0, "Cat " + cat + " pupil response during music");
                }
                Console.WriteLine("Cat " + cat + " identity, gaze, music and cache checks passed");
            }
            Bitmap resized = renderer.RenderCat(0, 600, 0, 0, 0, 0, 0);
            Assert(resized.Size == new Size(288, 300), "Output cache resize dimensions");
        }
        Console.WriteLine("Renderer tests passed");
    }
}
