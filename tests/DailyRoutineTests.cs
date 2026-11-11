using System;
using System.Drawing;
using ThreeCats;
class DailyRoutineTests
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
    static void Check(bool value, string behavior)
    {
        if (!value) throw new Exception(behavior);
    }
    static void MealPhases(bool zeroDistance)
    {
        var layout = new CatLayout(new Point(100, 100), 300);
        Point bowl = new Point(zeroDistance ? 154 : 400, 100);
        var routine = new DailyRoutine(new FixedRandom(0, 2));
        var seen = new System.Collections.Generic.HashSet<RoutinePhase>();
        routine.Update(20, layout, bowl, true);
        bool moved = false;
        for (int frame = 0; frame < 3000 && routine.ActiveCat >= 0; frame++)
        {
            seen.Add(routine.Phase);
            bool visible = routine.Phase == RoutinePhase.Entering ||
                routine.Phase == RoutinePhase.WalkingOut ||
                routine.Phase == RoutinePhase.Lowering ||
                routine.Phase == RoutinePhase.Acting ||
                routine.Phase == RoutinePhase.Rising;
            Check(routine.BowlVisible == visible, "bowl visibility phase");
            Check(routine.Progress >= 0 && routine.Progress <= 1, "finite phase progress");
            if (!zeroDistance && routine.Phase == RoutinePhase.WalkingBack && !moved)
            {
                layout.MoveQueue(new Point(50, 140));
                moved = true;
            }
            Point anchor = moved ? new Point(50, 140) : new Point(100, 100);
            Check(layout.Anchor == anchor && layout.Queue.Count == 3, "queue preserved");
            for (int id = 0; id < 3; id++)
                Check(layout.PositionOf(id) == new Point(anchor.X + id * layout.Spacing, anchor.Y), "confirmed cat position preserved");
            if (routine.Phase == RoutinePhase.Settling)
                Check(routine.VisualPosition == layout.PositionOf(0), "settling at confirmed position");
            routine.Update(.01, layout, bowl, false);
        }
        Check(routine.ActiveCat == -1 && !routine.BowlVisible, "zero distance or normal routine completes");
        Check(routine.VisualPosition == layout.PositionOf(0), "latest return position");
        Check(seen.Contains(RoutinePhase.Lowering) && seen.Contains(RoutinePhase.Acting), "lowering and acting phases reached");
        Check(seen.Contains(RoutinePhase.Rising) && seen.Contains(RoutinePhase.Settling), "rising and settling phases reached");
        if (!zeroDistance)
            Check(moved && seen.Contains(RoutinePhase.WalkingOut), "return tracks moved queue");
    }
    static void Run()
    {
        for (int kind = 0; kind < 4; kind++)
        {
            var layout = new CatLayout(new Point(0, 0), 300);
            layout.MoveCat(2, new Point(20, 30));
            var routine = new DailyRoutine(new FixedRandom(2, kind));
            routine.Update(19, layout, new Point(20, 30), true);
            Check(routine.ActiveCat == -1, "initial cooldown");
            routine.Paused = true;
            routine.Update(100, layout, Point.Empty, true);
            Check(routine.CooldownRemaining == 1, "paused cooldown");
            routine.Paused = false;
            routine.Update(1, layout, new Point(20, 30), false);
            Check(routine.ActiveCat == -1, "start permission deferred");
            routine.Update(0.1, layout, new Point(20, 30), true);
            Check(routine.ActiveCat == 2, "single active detached cat");
            routine.Paused = true;
            double elapsed = routine.PhaseSeconds;
            routine.Update(5, layout, Point.Empty, true);
            Check(routine.PhaseSeconds == elapsed, "paused action");
            routine.Paused = false;
            layout.MoveCat(2, new Point(40, 50));
            routine.Update(60, layout, new Point(20, 30), false);
            Check(routine.ActiveCat == -1 && routine.CooldownRemaining > 0, "completion starts shared cooldown");
            Check(layout.PositionOf(2) == new Point(40, 50) && layout.Queue.Count == 2, "detached position preserved");
            routine.Update(1000, layout, Point.Empty, true);
            routine.Interrupt();
            Check(routine.ActiveCat == -1 && routine.CooldownRemaining == 90, "interrupt resets shared cooldown");
            routine.Update(89.9, layout, Point.Empty, true);
            Check(routine.ActiveCat == -1, "subsequent cooldown boundary");
            routine.Update(.1, layout, Point.Empty, true);
            Check(routine.ActiveCat == 2, "next single active cat");
        }
        MealPhases(false);
        MealPhases(true);
        Console.WriteLine("PASS daily routine tests");
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
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

}
