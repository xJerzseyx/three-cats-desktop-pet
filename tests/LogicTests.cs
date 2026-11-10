using System;
using System.Drawing;
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
        System.IO.File.WriteAllText(path, "broken");
        Check(Settings.Load(path).Width == 300, "damaged settings use 50 percent default");
        System.IO.File.Delete(path);
        Console.WriteLine(count + " tests passed");
    }
}
