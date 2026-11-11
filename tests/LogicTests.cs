using System;
using System.Drawing;
using System.Linq;
using ThreeCats;

class LogicTests
{
    static int count;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL " + name);
        count++;
        Console.WriteLine("PASS " + name);
    }
    static void Main()
    {
        try { Run(); }
        catch (Exception exception) { Console.WriteLine("FAIL " + exception.Message); Environment.Exit(1); }
    }
    static void Run()
    {
        Check(PlayerNames.Normalize(" QQMusic.EXE ") == "qqmusic", "process names ignore case and extension");
        var names = PlayerNames.Parse("QQMusic.exe, cloudmusic；foobar2000\nQQMusic.exe");
        Check(new Settings().CursorTheme == "wand", "legacy and new settings default to the wand cursor theme");
        Check(names.Count == 3 && names.Contains("foobar2000"), "player list handles Chinese punctuation and duplicates");
        Check(!names.Contains("chrome"), "unselected process excluded");
        Check(PlayerNames.Normalize("\"C:\\Music\\foobar2000.exe\"") == "foobar2000", "quoted copied EXE path accepted");
        Check(PlayerNames.Normalize("bad|player.exe") == "", "invalid process token rejected safely");
        Check(PlayerNames.Normalize(null) == "", "empty process token handled safely");
        var motion = new MotionState();
        motion.Update(0, 0.033);
        Check(!motion.Playing, "silence starts in mouse mode");
        motion.Update(0.25f, 0.033);
        Check(motion.Playing && motion.Energy > 0, "selected music activates motion");
        motion.Update(0, 0.2);
        Check(motion.Playing, "short silent gap keeps music mode");
        motion.Update(0, 0.6);
        Check(!motion.Playing, "stopped music restores mouse mode");
        var placement = Placement.Clamp(new Rectangle(-5000, -5000, 600, 320), new[] { new Rectangle(0, 0, 1920, 1040) });
        Check(placement.X >= 0 && placement.Y >= 0, "offscreen position recovered");
        var secondary = Placement.Clamp(new Rectangle(-1200, 100, 600, 320), new[] { new Rectangle(-1920, 0, 1920, 1080), new Rectangle(0,0,1920,1080) });
        Check(secondary.X == -1200, "negative-coordinate second monitor retained");
        var settings = new Settings();
        Check(settings.Width == 300, "new settings default to 50 percent");
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "threecats-test-" + Guid.NewGuid().ToString("N") + ".xml");
        Check(Settings.Load(path).Width == 300, "first launch defaults to 50 percent");
        settings.Width = 720;
        settings.Players = "qqmusic,foobar2000";
        settings.Save(path);
        var restored = Settings.Load(path);
        Check(restored.Width == 720 && restored.Players == settings.Players, "portable settings round trip");
        settings.Width = 800;
        settings.Save(path);
        Check(Settings.Load(path).Width == 800, "atomic settings replacement");
        var layout = new CatLayout(new Point(100, 200), 300);
        Point lastCat = layout.PositionOf(2);
        layout.Detach(1);
        Check(layout.Queue.SequenceEqual(new[] { 0, 2 }) && layout.PositionOf(2).X < lastCat.X,
            "detaching a cat compacts the remaining queue");
        layout.MoveCat(1, new Point(-400, 300));
        layout.MoveQueue(new Point(200, 250));
        Check(layout.PositionOf(1) == new Point(-400, 300) && layout.PositionOf(0) == new Point(200, 250),
            "group dragging moves only queued cats");
        layout.Detach(0);
        layout.Detach(2);
        Check(layout.Queue.Count == 0 && layout.Anchor == new Point(200, 250)
            && layout.CanJoin(layout.Anchor), "empty queue retains its original return target");
        layout.Join(2);
        layout.Join(1);
        layout.Join(0);
        Check(layout.Queue.SequenceEqual(new[] { 2, 1, 0 }) && layout.PositionOf(2) == layout.Anchor,
            "cats join the tail in return order with stable identities");
        var confirmed = layout.Copy();
        layout.MoveCat(1, new Point(900, 900));
        layout.Restore(confirmed);
        Check(layout.Queue.SequenceEqual(new[] { 2, 1, 0 }) && layout.PositionOf(1) == confirmed.PositionOf(1),
            "cancelled drags restore the confirmed layout");
        layout.MoveCat(1, new Point(-800, 450));
        layout.Resize(600);
        Check(layout.PositionOf(1) == new Point(-800, 450) && layout.PositionOf(0).X - layout.PositionOf(2).X == layout.Spacing,
            "resizing retains detached positions and reflows joined cats");
        layout.WriteTo(settings);
        settings.Save(path);
        var savedLayout = CatLayout.FromSettings(Settings.Load(path), Point.Empty);
        Check(savedLayout.Queue.SequenceEqual(new[] { 2, 0 }) && savedLayout.PositionOf(1) == new Point(-800, 450),
            "confirmed queue order and detached positions survive restart");
        foreach (int id in savedLayout.Queue.ToArray()) savedLayout.Detach(id);
        savedLayout.WriteTo(settings);
        settings.Save(path);
        var emptyLayout = CatLayout.FromSettings(Settings.Load(path), Point.Empty);
        Check(emptyLayout.Queue.Count == 0 && emptyLayout.Anchor == new Point(200, 250),
            "an empty saved queue stays empty after restart");
        var legacyLayout = CatLayout.FromSettings(new Settings(), new Point(75, 80));
        Check(legacyLayout.Queue.SequenceEqual(new[] { 0, 1, 2 }) && legacyLayout.Anchor == new Point(75, 80),
            "legacy settings migrate to the original three cat order");
        var damagedLayout = CatLayout.FromSettings(new Settings
        {
            LayoutVersion = 1,
            Queue = new[] { 2, 2, -1, 8, 0 },
            Cats = new[] { null, new CatPlacement { Id = 1, X = -500, Y = 200 }, new CatPlacement { Id = 9 } }
        }, new Point(75, 80));
        Check(damagedLayout.Queue.SequenceEqual(new[] { 2, 0 }) && damagedLayout.PositionOf(1) == new Point(-500, 200),
            "saved invalid and duplicate identities recover without losing valid detached positions");
        System.IO.File.WriteAllText(path, "broken");
        Check(Settings.Load(path).Width == 300, "damaged settings use 50 percent default");
        System.IO.File.Delete(path);
        Console.WriteLine(count + " tests passed");
    }
}
