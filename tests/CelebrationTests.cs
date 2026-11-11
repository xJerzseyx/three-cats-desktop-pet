using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ThreeCats;

internal static class CelebrationTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Birthday = "珣珣酱生日快乐喵";
    private static DateTime today;

    [STAThread]
    private static int Main()
    {
        string path = Path.GetFullPath("work/celebrations-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            new Settings { CursorTheme = "off" }.Save(path);
            using (var pet = new PetForm(path))
            {
                var tray = Field<NotifyIcon>(pet, "tray");
                Check(!HasRemovedOption(tray.ContextMenuStrip.Items), "tray hides the celebration and date preview options");
            }
            CheckDates();
            CheckApplication(path);
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine("FAIL celebrations " + exception);
            return 1;
        }
        finally { File.Delete(path); }
    }

    private static void CheckDates()
    {
        Expect(new DateTime(2026, 10, 31), "Halloween", null);
        Expect(new DateTime(2026, 12, 25), "Christmas", null);
        Expect(new DateTime(2026, 12, 26), "None", null);
        Expect(new DateTime(2027, 1, 1), "None", null);
        Expect(new DateTime(2026, 2, 9), "None", null);
        Expect(new DateTime(2026, 2, 10), "SpringFestival", null);
        Expect(new DateTime(2026, 2, 17), "SpringFestival", null);
        Expect(new DateTime(2026, 2, 23), "SpringFestival", null);
        Expect(new DateTime(2026, 2, 24), "None", null);
        Expect(new DateTime(2026, 4, 6), "None", "伊伊酱生日快乐喵");
        Expect(new DateTime(2026, 1, 18), "None", Birthday);
        Expect(new DateTime(2031, 1, 18), "SpringFestival", Birthday);
        Expect(new DateTime(2025, 12, 21), "None", null);
        Expect(new DateTime(2026, 12, 21), "None", null);
        Expect(new DateTime(2026, 12, 22), "None", "主人们1周年快乐！");
        Expect(new DateTime(2027, 12, 22), "None", "主人们2周年快乐！");
        Expect(new DateTime(2028, 12, 21), "None", "主人们3周年快乐！");
        Expect(new DateTime(2100, 12, 22), "None", "主人们75周年快乐！");
    }

    private static void CheckApplication(string path)
    {
        today = new DateTime(2030, 12, 25);
        using (var pet = Open(path))
        {
            try
            {
                Check(Costume(pet) == "Christmas", "live date equips all cats");
                today = new DateTime(2031, 1, 1);
                Tick(pet);
                Check(Costume(pet) == "None", "live year rollover removes the old costume");
                Check(!Greeting(pet).Visible && Seen(path).Length == 0, "ordinary days do not consume any automatic greeting");

                today = new DateTime(2031, 1, 18);
                Tick(pet);
                Check(Costume(pet) == "SpringFestival" && Greeting(pet).Visible && Greeting(pet).Text == Birthday,
                    "live birthday shows its greeting and keeps the festive outfit");
                Check(Seen(path).SequenceEqual(new[] { "2031-01-18" }), "automatic greeting is saved immediately");
                Greeting(pet).GetType().GetMethod("Present", Instance)
                    .Invoke(Greeting(pet), new object[] { double.MaxValue, Rectangle.Empty });
                Tick(pet);
                Check(!Greeting(pet).Visible, "a later tick cannot replay the same day's greeting");
                CheckCostumedDrag(pet);
            }
            finally { pet.Close(); }
        }

        using (var pet = Open(path))
        {
            try
            {
                Check(Costume(pet) == "SpringFestival" && !Greeting(pet).Visible,
                    "same-day restart restores the costume and suppresses the automatic greeting");
                today = new DateTime(2031, 4, 6);
                Tick(pet);
                Check(Greeting(pet).Visible && Greeting(pet).Text == "伊伊酱生日快乐喵" && Seen(path).Length == 2,
                    "a different birthday still plays after a same-day restart");
            }
            finally { pet.Close(); }
        }
    }

    private static void CheckCostumedDrag(PetForm pet)
    {
        var cats = Field<CatWindow[]>(pet, "cats");
        var layout = Field<CatLayout>(pet, "layout");
        CatWindow cat = cats[0];
        Size idle = SpriteRenderer.CatFrameSize(layout.Width);
        int padding = (cat.Width - idle.Width) / 2;
        Check(padding > 0 && cats.All(window => window.Size == idle + new Size(padding * 2, padding * 2)),
            "all costumes have room for their accessories");
        Point pointer = cat.PointToScreen(new Point(cat.Width / 2, cat.Height / 2));
        Point destination = new Point(pointer.X - 150, pointer.Y - 100);
        typeof(PetForm).GetMethod("BeginCatDrag", Instance).Invoke(pet, new object[] { cat, pointer, false });
        typeof(PetForm).GetMethod("UpdateDrag", Instance).Invoke(pet, new object[] { destination });
        Point grip = LiftRenderer.GripOffset(cat.CatId, layout.Width);
        grip.Offset(padding, padding);
        Check(cat.PointToScreen(grip) == destination && cat.Height > idle.Height + padding * 2,
            "the dressed cat keeps its scruff under the pointer when lifted");
        typeof(PetForm).GetMethod("CancelDrag", Instance).Invoke(pet, null);
        Check(cat.Size == idle + new Size(padding * 2, padding * 2),
            "cancelled drag returns to a dressed standing pose");
    }

    private static PetForm Open(string path)
    {
        var constructor = typeof(PetForm).GetConstructor(Instance, null,
            new[] { typeof(string), typeof(Random), typeof(Func<DateTime>) }, null);
        var pet = (PetForm)constructor.Invoke(new object[] { path, new Random(42), (Func<DateTime>)(() => today) });
        pet.Show();
        Application.DoEvents();
        return pet;
    }

    private static bool HasRemovedOption(ToolStripItemCollection items)
    {
        return items.OfType<ToolStripMenuItem>().Any(item => item.Text == "节日换装与祝福"
            || item.Text.StartsWith("按日期预览") || HasRemovedOption(item.DropDownItems));
    }

    private static Form Greeting(PetForm pet) { return Field<Form>(pet, "greeting"); }
    private static string Costume(PetForm pet) { return Property(Field<object>(pet, "celebration"), "Costume").ToString(); }
    private static T Field<T>(PetForm pet, string name) { return (T)typeof(PetForm).GetField(name, Instance).GetValue(pet); }
    private static object Property(object target, string name) { return target.GetType().GetProperty(name, Instance).GetValue(target, null); }
    private static void Tick(PetForm pet) { typeof(PetForm).GetMethod("TickAnimation", Instance).Invoke(pet, null); }
    private static string[] Seen(string path)
    {
        return (string[])typeof(Settings).GetField("SeenGreetingDates").GetValue(Settings.Load(path)) ?? new string[0];
    }

    private static void Expect(DateTime date, string costume, string message)
    {
        var calendar = typeof(PetForm).Assembly.GetType("ThreeCats.CelebrationCalendar", true);
        var day = calendar.GetMethod("Describe", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { date });
        Check(Property(day, "Costume").ToString() == costume && (string)Property(day, "Message") == message,
            date.ToString("yyyy-MM-dd") + " selects " + costume + " and " + (message ?? "no greeting"));
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Console.WriteLine("PASS " + description);
    }
}
