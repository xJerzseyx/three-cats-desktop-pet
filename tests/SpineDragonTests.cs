using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ThreeCats;

internal static class SpineDragonTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private sealed class ControlledRandom : Random
    {
        internal double Roll = .95;
        public override double NextDouble() { return Roll; }
    }

    [STAThread]
    private static int Main()
    {
        string path = Path.GetFullPath("work/spine-dragon-" + Guid.NewGuid().ToString("N") + ".xml");
        new Settings { CursorTheme = "off", Width = 300 }.Save(path);
        try
        {
            var random = new ControlledRandom();
            var constructor = typeof(PetForm).GetConstructor(PrivateInstance, null,
                new[] { typeof(string), typeof(Random), typeof(Func<DateTime>) }, null);
            using (var pet = (PetForm)constructor.Invoke(new object[] { path, random, (Func<DateTime>)(() => new DateTime(2026, 11, 12)) }))
            {
                pet.Show();
                Application.DoEvents();
                try { Run(pet, random); }
                finally { pet.Close(); }
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine("FAIL spine dragon " + exception);
            return 1;
        }
        finally { File.Delete(path); }
    }

    private static void Run(PetForm pet, ControlledRandom random)
    {
        var tray = Field<NotifyIcon>(pet, "tray");
        Check(!tray.ContextMenuStrip.Items.OfType<ToolStripMenuItem>()
            .Any(item => item.Text.Contains("脊背龙")), "tray has no spine dragon preview");
        var cats = Field<CatWindow[]>(pet, "cats");
        var layout = Field<CatLayout>(pet, "layout");
        Size standingSize = SpriteRenderer.CatFrameSize(layout.Width);
        Size actionSize = DailyRenderer.FrameSize(layout.Width);
        Point standingFoot = layout.PositionOf(0);

        var settings = Field<Settings>(pet, "settings");
        var dragon = Field<object>(pet, "spineDragon");
        random.Roll = .01;
        settings.CursorTheme = "wand";
        Enter(cats[0]);
        Check(cats[0].Size == standingSize, "wand never triggers the egg");
        settings.CursorTheme = "catnip";
        random.Roll = .95;
        Enter(cats[0]);
        Check(cats[0].Size == standingSize, "catnip can miss without triggering an egg");
        random.Roll = .01;
        Enter(cats[0]);
        Check(cats[0].Size == actionSize && cats.Skip(1).All(cat => cat.Size == standingSize),
            "catnip can trigger just the hovered cat");
        Advance(pet, .46);
        Point airborne = new Point(cats[0].Left, cats[0].Top) + (Size)DailyRenderer.FootOffset(layout.Width);
        Check(airborne.X != standingFoot.X && airborne.Y < standingFoot.Y && layout.PositionOf(0) == standingFoot,
            "side hop changes the displayed position only");

        CatWindow catToDrag = cats[0];
        Point pointer = catToDrag.PointToScreen(new Point(catToDrag.Width / 2, catToDrag.Height / 2));
        Mouse(catToDrag, "OnMouseDown", pointer);
        Check(catToDrag.Size == standingSize
            && new Point(catToDrag.Left, catToDrag.Top) + (Size)SpriteRenderer.CatFootOffset(layout.Width) == airborne,
            "mouse down interrupts the egg at the visible position");
        Point destination = new Point(pointer.X - 140, pointer.Y - 90);
        Mouse(catToDrag, "OnMouseMove", destination);
        Check(catToDrag.Size == actionSize
            && catToDrag.PointToScreen(LiftRenderer.GripOffset(0, layout.Width)) == destination,
            "dragging uses the lifted pose and follows the pointer");
        Mouse(catToDrag, "OnMouseUp", destination);
        Check(cats.All(cat => cat.Size == standingSize), "release restores the normal pose immediately");

        Enter(cats[1]);
        Check(cats[1].Size == standingSize, "interrupting catnip starts a shared cooldown");
        Invoke(dragon, "Advance", 61d);
        Enter(cats[2]);
        Check(cats[2].Size == actionSize, "catnip can trigger again after the cooldown");
        Point beforeRecovery = layout.PositionOf(2);
        Advance(pet, .95);
        Advance(pet, .35);
        Check(cats.All(cat => cat.Size == standingSize)
            && new Point(cats[2].Left, cats[2].Top) + (Size)SpriteRenderer.CatFootOffset(layout.Width) == beforeRecovery,
            "natural recovery restores the confirmed standing location");

        Enter(cats[1]);
        Check(cats[1].Size == standingSize, "natural completion also starts a shared cooldown");
        settings.CursorTheme = "off";
    }

    private static T Field<T>(PetForm pet, string name)
    {
        return (T)typeof(PetForm).GetField(name, PrivateInstance).GetValue(pet);
    }

    private static object Invoke(object target, string name, params object[] arguments)
    {
        return target.GetType().GetMethod(name, PrivateInstance).Invoke(target, arguments);
    }

    private static void Advance(PetForm pet, double seconds)
    {
        typeof(PetForm).GetField("lastTime", PrivateInstance).SetValue(pet,
            Field<Stopwatch>(pet, "clock").Elapsed.TotalSeconds - seconds);
        Invoke(pet, "TickAnimation");
    }

    private static void Enter(CatWindow cat) { Invoke(cat, "OnMouseEnter", EventArgs.Empty); }

    private static void Mouse(CatWindow cat, string method, Point screenPoint)
    {
        Point local = cat.PointToClient(screenPoint);
        Invoke(cat, method, new MouseEventArgs(MouseButtons.Left, 1, local.X, local.Y, 0));
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
