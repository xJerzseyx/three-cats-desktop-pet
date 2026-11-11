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

    public sealed class CatPlacement
    {
        public int Id;
        public int X;
        public int Y;
    }

    public sealed class CatLayout
    {
        private List<int> order = new List<int> { 0, 1, 2 };
        private readonly Point[] positions = new Point[3];
        public Point Anchor { get; private set; }
        public int Width { get; private set; }
        public int Spacing { get { return Math.Max(1, (int)Math.Round(Width * 520.0 / 1774)); } }
        public IList<int> Queue { get { return order.AsReadOnly(); } }
        public Point TailPosition { get { return new Point(Anchor.X + order.Count * Spacing, Anchor.Y); } }

        public CatLayout(Point anchor, int width)
        {
            Anchor = anchor;
            Resize(width);
        }

        public Point PositionOf(int id) { return positions[id]; }
        public bool IsJoined(int id) { return order.Contains(id); }

        public void Detach(int id)
        {
            if (order.Remove(id)) Reflow();
        }

        public void MoveCat(int id, Point position)
        {
            Detach(id);
            positions[id] = position;
        }

        public void Join(int id)
        {
            if (order.Contains(id)) return;
            order.Add(id);
            Reflow();
        }

        public void MoveQueue(Point anchor)
        {
            Anchor = anchor;
            Reflow();
        }

        public void Resize(int width)
        {
            Width = Math.Max(240, Math.Min(1600, width));
            Reflow();
        }

        public bool CanJoin(Point position)
        {
            int halfHeight = Math.Max(24, Width / 5);
            var target = new Rectangle(Anchor.X - Spacing / 2, Anchor.Y - halfHeight,
                Spacing * Math.Max(1, order.Count + 1), halfHeight * 2);
            return target.Contains(position);
        }

        private void Reflow()
        {
            for (int slot = 0; slot < order.Count; slot++)
                positions[order[slot]] = new Point(Anchor.X + slot * Spacing, Anchor.Y);
        }

        public CatLayout Copy()
        {
            var result = new CatLayout(Anchor, Width);
            result.Restore(this);
            return result;
        }

        public void Restore(CatLayout snapshot)
        {
            Anchor = snapshot.Anchor;
            Width = snapshot.Width;
            order = new List<int>(snapshot.order);
            Array.Copy(snapshot.positions, positions, positions.Length);
        }

        public static CatLayout FromSettings(Settings settings, Point defaultAnchor)
        {
            var result = new CatLayout(defaultAnchor, settings.Width);
            if (settings.LayoutVersion < 1) return result;
            if (settings.QueueX != int.MinValue && settings.QueueY != int.MinValue)
                result.Anchor = new Point(settings.QueueX, settings.QueueY);
            result.order = (settings.Queue ?? new[] { 0, 1, 2 }).Where(id => id >= 0 && id < 3).Distinct().ToList();
            if (settings.Cats != null)
                foreach (var cat in settings.Cats.Where(cat => cat != null && cat.Id >= 0 && cat.Id < 3))
                    result.positions[cat.Id] = new Point(cat.X, cat.Y);
            result.Reflow();
            return result;
        }

        public void WriteTo(Settings settings)
        {
            settings.LayoutVersion = 1;
            settings.Width = Width;
            settings.QueueX = Anchor.X;
            settings.QueueY = Anchor.Y;
            settings.Queue = order.ToArray();
            settings.Cats = Enumerable.Range(0, 3).Select(id => new CatPlacement
            {
                Id = id,
                X = positions[id].X,
                Y = positions[id].Y
            }).ToArray();
        }
    }

    public sealed class Settings
    {
        public int Width = 300;
        public int X = int.MinValue;
        public int Y = int.MinValue;
        public string Players = "cloudmusic,qqmusic";
        public string CursorTheme = "wand";
        public string[] SeenGreetingDates;
        public int LayoutVersion;
        public int QueueX = int.MinValue;
        public int QueueY = int.MinValue;
        public int[] Queue;
        public CatPlacement[] Cats;

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
