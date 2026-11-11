using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using ThreeCats;

internal static class DailyIntegrationTests
{
    private static int checks;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    private static bool IsAbove(IntPtr upper, IntPtr lower)
    {
        for (IntPtr window = GetWindow(lower, 3); window != IntPtr.Zero; window = GetWindow(window, 3))
            if (window == upper) return true;
        return false;
    }

    private sealed class FixedRandom : Random
    {
        private readonly int cat;
        private readonly int kind;

        internal FixedRandom(int cat, RoutineKind kind)
        {
            this.cat = cat;
            this.kind = (int)kind - 1;
        }

        public override double NextDouble() { return 0; }
        public override int Next(int maximum) { return maximum == 3 ? cat : kind; }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        checks++;
        Console.WriteLine("PASS " + label);
    }

    private static T Field<T>(PetForm pet, string name)
    {
        return (T)typeof(PetForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pet);
    }

    private static object Invoke(object target, string name, params object[] arguments)
    {
        return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }

    private static void Mouse(CatWindow cat, string method, Point screenPoint)
    {
        Point local = cat.PointToClient(screenPoint);
        Invoke(cat, method, new MouseEventArgs(MouseButtons.Left, 1, local.X, local.Y, 0));
    }

    private static bool SameLayout(CatLayout first, CatLayout second)
    {
        return first.Anchor == second.Anchor && first.Queue.SequenceEqual(second.Queue)
            && Enumerable.Range(0, 3).All(id => first.PositionOf(id) == second.PositionOf(id));
    }

    private static Point Foot(CatWindow cat, int width, bool active)
    {
        Point offset = active ? DailyRenderer.FootOffset(width) : SpriteRenderer.CatFootOffset(width);
        return new Point(cat.Left + offset.X, cat.Top + offset.Y);
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            RunAutomaticTimer();
            for (int value = 1; value <= 4; value++) Run((RoutineKind)value, (value - 1) % 3);
            Console.WriteLine(checks + " daily integration checks passed");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine("FAIL daily integration " + exception);
            return 1;
        }
    }

    private static void RunAutomaticTimer()
    {
        string path = Path.GetFullPath("work/daily-timer-" + Guid.NewGuid().ToString("N") + ".xml");
        new Settings { CursorTheme = "off", Width = 300 }.Save(path);
        var constructor = typeof(PetForm).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(string), typeof(Random) }, null);
        bool started = false;
        bool completed = false;
        try
        {
            using (var pet = (PetForm)constructor.Invoke(new object[] { path, new FixedRandom(0, RoutineKind.Stretch) }))
            using (var observer = new System.Windows.Forms.Timer { Interval = 50 })
            {
                var elapsed = new Stopwatch();
                var daily = Field<DailyRoutine>(pet, "daily");
                observer.Tick += delegate
                {
                    started |= daily.ActiveCat == 0 && daily.Kind == RoutineKind.Stretch;
                    completed = started && daily.ActiveCat == -1 && daily.CooldownRemaining > 0;
                    if (completed || elapsed.Elapsed.TotalSeconds >= 30)
                    {
                        observer.Stop();
                        pet.Close();
                    }
                };
                pet.Shown += delegate { elapsed.Start(); observer.Start(); };
                // Exercise the application's timer and message loop without advancing the routine directly.
                Application.Run(pet);
            }
            Check(started, "real animation timer automatically starts a daily routine after the initial cooldown");
            Check(completed, "real animation timer completes the routine and enters the shared cooldown");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static void Run(RoutineKind kind, int catId)
    {
        string path = Path.GetFullPath("work/daily-integration-" + Guid.NewGuid().ToString("N") + ".xml");
        new Settings { CursorTheme = "off" }.Save(path);
        var constructor = typeof(PetForm).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(string), typeof(Random) }, null);
        try
        {
            using (var pet = (PetForm)constructor.Invoke(new object[] { path, new FixedRandom(catId, kind) }))
            {
                try
                {
                    pet.Show();
                    var layout = Field<CatLayout>(pet, "layout");
                    var daily = Field<DailyRoutine>(pet, "daily");
                    var cats = Field<CatWindow[]>(pet, "cats");
                    var bowl = Field<Form>(pet, "bowl");
                    CatLayout confirmed = layout.Copy();
                    Point bowlPosition = (Point)Invoke(pet, "GetBowlPosition");
                    daily.Update(20, layout, bowlPosition, true);
                    Invoke(pet, "TickAnimation");
                    Check(daily.ActiveCat == catId && daily.Kind == kind, kind + " selects its independent cat through the shared scheduler");
                    Check(cats.Where(cat => cat.CatId != catId).All(cat => Foot(cat, layout.Width, false) == layout.PositionOf(cat.CatId)),
                        kind + " leaves other cats at their confirmed standing positions");
                    for (int step = 0; step < 10000 && daily.ActiveCat >= 0; step++)
                        daily.Update(.1, layout, bowlPosition, false);
                    Invoke(pet, "TickAnimation");
                    Check(daily.ActiveCat == -1 && SameLayout(layout, confirmed)
                        && cats.All(cat => Foot(cat, layout.Width, false) == confirmed.PositionOf(cat.CatId)),
                        kind + " returns to its original place and preserves queue order");
                    Check(!bowl.Visible, kind + " keeps the bowl hidden after autonomous completion");

                    daily.Update(daily.CooldownRemaining + .01, layout, bowlPosition, true);
                    for (int step = 0; step < 10000 && daily.Phase != RoutinePhase.Acting; step++)
                        daily.Update(.01, layout, bowlPosition, false);
                    Invoke(pet, "TickAnimation");
                    CatWindow groupSource = cats[(catId + 1) % 3];
                    Point groupPointer = new Point(groupSource.Left + groupSource.Width / 2, groupSource.Top + groupSource.Height / 2);
                    Invoke(pet, "BeginCatDrag", groupSource, groupPointer, true);
                    Invoke(pet, "UpdateDrag", new Point(groupPointer.X - 20, groupPointer.Y - 20));
                    daily.Update(200, layout, bowlPosition, false);
                    Invoke(pet, "FinishDrag");
                    Check(daily.ActiveCat == -1 && daily.CooldownRemaining >= 89 && !bowl.Visible && layout.Queue.SequenceEqual(confirmed.Queue)
                        && Enumerable.Range(0, 3).All(id => layout.PositionOf(id) == new Point(confirmed.PositionOf(id).X - 20, confirmed.PositionOf(id).Y - 20)),
                        kind + " safely yields to manual group movement and retains the formation");
                    confirmed = layout.Copy();
                    bowlPosition = (Point)Invoke(pet, "GetBowlPosition");
                    daily.Update(daily.CooldownRemaining + .01, layout, bowlPosition, true);
                    for (int step = 0; step < 10000 && daily.Phase != RoutinePhase.Acting; step++)
                        daily.Update(.01, layout, bowlPosition, false);
                    Invoke(pet, "TickAnimation");
                    Check(daily.Phase == RoutinePhase.Acting && daily.ActiveCat == catId, kind + " reaches its active pose");
                    Check(bowl.Visible == (kind == RoutineKind.Drink || kind == RoutineKind.Eat),
                        kind + " displays the matching bowl only during meals");
                    if (bowl.Visible)
                    {
                        Check(IsAbove(cats[catId].Handle, bowl.Handle), kind + " keeps the eating cat in front of the bowl immediately");
                        typeof(PetForm).GetField("nextStatus", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pet, 0d);
                        Invoke(pet, "TickAnimation");
                        Check(cats.All(cat => IsAbove(cat.Handle, bowl.Handle)), kind + " keeps every cat above the bowl after the periodic refresh");
                        Check(bowl.Width == Math.Max(20, (int)Math.Round(layout.Width * .14)) + 4 && bowl.Bottom >= daily.BowlPosition.Y + 5,
                            kind + " keeps the bowl small and below the lowered muzzle");
                    }
                    Invoke(pet, "SaveSettings");
                    Check(SameLayout(CatLayout.FromSettings(Settings.Load(path), Point.Empty), confirmed),
                        kind + " saves confirmed standing positions during temporary daily movement");

                    double progress = daily.PhaseSeconds;
                    double cooldown = daily.CooldownRemaining;
                    pet.ToggleVisibility();
                    Thread.Sleep(100);
                    Application.DoEvents();
                    daily.Update(10, layout, bowlPosition, true);
                    if (!daily.Paused || daily.PhaseSeconds != progress || daily.CooldownRemaining != cooldown
                        || bowl.Visible || cats.Any(cat => cat.Visible))
                        Console.WriteLine("Pause state paused=" + daily.Paused + " phase=" + daily.Phase
                            + " seconds=" + progress + " -> " + daily.PhaseSeconds
                            + " cooldown=" + cooldown + " -> " + daily.CooldownRemaining
                            + " bowl=" + bowl.Visible + " cats=" + string.Join(",", cats.Select(cat => cat.Visible)));
                    Check(daily.Paused && daily.PhaseSeconds == progress && daily.CooldownRemaining == cooldown
                        && !bowl.Visible && cats.All(cat => !cat.Visible), kind + " pauses action and cooldown while hidden");
                    pet.ShowPet();
                    Check(!daily.Paused && daily.Phase == RoutinePhase.Acting && daily.PhaseSeconds >= progress
                        && daily.PhaseSeconds < progress + .1, kind + " resumes the paused phase without a time jump");
                    var motion = Field<MotionState>(pet, "motion");
                    motion.Update(.5f, .1);
                    Invoke(pet, "TickAnimation");
                    Check(motion.Playing && Field<double>(pet, "musicMix") > 0 && daily.ActiveCat == catId
                        && cats.Where(cat => cat.CatId != catId).All(cat => cat.Size == SpriteRenderer.CatFrameSize(layout.Width)),
                        kind + " keeps music active for the other cats while the selected cat performs its routine");

                    Point visual = daily.VisualPosition;
                    var pointer = new Point(visual.X, visual.Y - 25);
                    Invoke(pet, "BeginCatDrag", cats[catId], pointer, false);
                    Check(daily.ActiveCat == -1 && Foot(cats[catId], layout.Width, false) == visual && !bowl.Visible,
                        kind + " can be picked up immediately at its current displayed position");
                    daily.Update(200, layout, bowlPosition, false);
                    Check(daily.ActiveCat == -1 && daily.CooldownRemaining == 0,
                        kind + " defers new routines throughout a long manual drag");
                    Rectangle area = Screen.FromPoint(visual).WorkingArea;
                    Point destination = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
                    Point foot = LiftRenderer.FootOffset(layout.Width);
                    Point grip = LiftRenderer.GripOffset(catId, layout.Width);
                    Point release = new Point(destination.X + grip.X - foot.X, destination.Y + grip.Y - foot.Y);
                    Mouse(cats[catId], "OnMouseMove", release);
                    Check(cats[catId].Size == DailyRenderer.FrameSize(layout.Width),
                        kind + " switches to the suspended pose canvas while dragging one cat");
                    Check(cats[catId].PointToScreen(grip) == release,
                        kind + " keeps the lifted scruff attached to the pointer");
                    Mouse(cats[catId], "OnMouseUp", release);
                    Check(layout.PositionOf(catId) == destination && !layout.IsJoined(catId)
                        && daily.CooldownRemaining >= 89 && daily.CooldownRemaining <= 180,
                        kind + " confirms the manual drop and starts a fresh shared cooldown");
                    Check(CatLayout.FromSettings(Settings.Load(path), Point.Empty).PositionOf(catId) == destination,
                        kind + " persists the manual override for restart");
                    Check(cats.All(cat => cat.Size == SpriteRenderer.CatFrameSize(layout.Width)),
                        kind + " restores standing poses after the drop");
                    confirmed = layout.Copy();
                    pointer = cats[catId].PointToScreen(new Point(cats[catId].Width / 2, cats[catId].Height / 2));
                    Invoke(pet, "BeginCatDrag", cats[catId], pointer, false);
                    Invoke(pet, "UpdateDrag", new Point(pointer.X + 30, pointer.Y - 30));
                    pet.ToggleVisibility();
                    Check(SameLayout(layout, confirmed) && cats.All(cat => !cat.Visible
                        && cat.Size == SpriteRenderer.CatFrameSize(layout.Width)),
                        kind + " hiding a lifted cat cancels its temporary placement and restores the standing canvas");
                    pet.ShowPet();
                    Check(SameLayout(layout, confirmed) && cats.All(cat => cat.Visible),
                        kind + " showing the pets resumes their confirmed standing layout");
                }
                finally { pet.Close(); }
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
