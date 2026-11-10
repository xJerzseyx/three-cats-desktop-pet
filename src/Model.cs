using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace ThreeCats
{
    public static class PlayerNames
    {
        public static string Normalize(string value)
        {
            value = (value ?? "").Trim().Trim('"', '\'');
            try { value = Path.GetFileName(value).ToLowerInvariant(); }
            catch (ArgumentException) { return ""; }
            return value.EndsWith(".exe") ? value.Substring(0, value.Length - 4) : value;
        }

        public static HashSet<string> Parse(string value)
        {
            return new HashSet<string>((value ?? "").Split(new[] { ',', ';', '，', '；', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalize).Where(name => name.Length > 0), StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed class MotionState
    {
        private double silence = 1;
        public bool Playing { get; private set; }
        public double Energy { get; private set; }

        public void Update(float peak, double seconds)
        {
            seconds = Math.Max(0, Math.Min(seconds, 1));
            silence = peak > 0.0005f ? 0 : silence + seconds;
            Playing = silence < 0.55;
            double target = Playing ? Math.Min(1, Math.Sqrt(Math.Max(0, peak)) * 1.7) : 0;
            double speed = target > Energy ? 18 : 5;
            Energy += (target - Energy) * (1 - Math.Exp(-speed * seconds));
        }
    }

    public static class Placement
    {
        public static Rectangle Clamp(Rectangle bounds, Rectangle[] screens)
        {
            Rectangle screen = screens.OrderByDescending(area =>
            {
                Rectangle overlap = Rectangle.Intersect(bounds, area);
                return (long)overlap.Width * overlap.Height;
            }).First();
            int width = Math.Min(bounds.Width, screen.Width);
            int height = Math.Min(bounds.Height, screen.Height);
            return new Rectangle(Math.Max(screen.Left, Math.Min(bounds.Left, screen.Right - width)),
                Math.Max(screen.Top, Math.Min(bounds.Top, screen.Bottom - height)), width, height);
        }
    }

    public sealed class Settings
    {
        public int Width = 300;
        public int X = int.MinValue;
        public int Y = int.MinValue;
        public string Players = "cloudmusic,qqmusic";

        public static Settings Load(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var result = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);
                    result.Width = Math.Max(240, Math.Min(1600, result.Width));
                    if (result.Players == null) result.Players = "cloudmusic,qqmusic";
                    return result;
                }
            }
            catch (IOException) { return new Settings(); }
            catch (UnauthorizedAccessException) { return new Settings(); }
            catch (InvalidOperationException) { return new Settings(); }
        }

        public void Save(string path)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    new XmlSerializer(typeof(Settings)).Serialize(stream, this);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
