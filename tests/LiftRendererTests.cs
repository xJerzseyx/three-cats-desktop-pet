using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using ThreeCats;

internal static class LiftRendererTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static string Signature(Bitmap frame)
    {
        using (var stream = new MemoryStream())
        using (var hash = SHA256.Create())
        {
            frame.Save(stream, ImageFormat.Png);
            return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
        }
    }

    private static int Main()
    {
        try
        {
            using (var renderer = new LiftRenderer())
            {
                foreach (int width in new[] { 300, 600, 1200, 300 })
                {
                    var identities = new System.Collections.Generic.HashSet<string>();
                    for (int cat = 0; cat < 3; cat++)
                    {
                        Bitmap frame = renderer.Render(cat, width);
                        Check(frame.Size == LiftRenderer.FrameSize(width), "lift frame dimensions");
                        Check(object.ReferenceEquals(frame, renderer.Render(cat, width)), "lift frame is cached per cat");
                        identities.Add(Signature(frame));
                        Point grip = LiftRenderer.GripOffset(cat, width);
                        Point foot = LiftRenderer.FootOffset(width);
                        Check(foot.Y - grip.Y > width * .5, "body hangs below the scruff");
                        bool scruffVisible = false;
                        for (int y = grip.Y - 3; y <= grip.Y + 3; y++)
                            for (int x = grip.X - 3; x <= grip.X + 3; x++)
                                scruffVisible |= frame.GetPixel(x, y).A > 32;
                        Check(scruffVisible, "grip corresponds to visible scruff fur");
                        for (int x = 0; x < frame.Width; x++)
                            Check(frame.GetPixel(x, 0).A == 0 && frame.GetPixel(x, frame.Height - 1).A == 0, "transparent vertical margin");
                        for (int y = 0; y < frame.Height; y++)
                            Check(frame.GetPixel(0, y).A == 0 && frame.GetPixel(frame.Width - 1, y).A == 0, "transparent horizontal margin");
                    }
                    Check(identities.Count == 3, "all three lifted identities remain distinct");
                }
                using (var preview = new Bitmap(900, 550))
                using (var graphics = Graphics.FromImage(preview))
                {
                    graphics.Clear(Color.FromArgb(245, 242, 236));
                    Point foot = LiftRenderer.FootOffset(600);
                    for (int cat = 0; cat < 3; cat++)
                        graphics.DrawImageUnscaled(renderer.Render(cat, 600), 150 + 300 * cat - foot.X, 0);
                    preview.Save("work/lift-preview.png", ImageFormat.Png);
                }
            }
            Console.WriteLine("PASS lifted identities, neck anchors, transparent edges, scale and cache");
            return 0;
        }
        catch (Exception error) { Console.WriteLine("FAIL lifted renderer " + error); return 1; }
    }
}
