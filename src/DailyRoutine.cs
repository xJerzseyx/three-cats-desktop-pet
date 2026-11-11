using System;
using System.Drawing;

namespace ThreeCats
{
    public enum RoutineKind { None, Stretch, Roll, Drink, Eat }
    public enum RoutinePhase { Waiting, Entering, WalkingOut, Lowering, Acting, Rising, WalkingBack, Settling }

    public sealed class DailyRoutine
    {
        readonly Random random;
        double x;
        double y;
        Point destination;
        double walkDuration;
        public int ActiveCat { get; private set; }
        public RoutineKind Kind { get; private set; }
        public RoutinePhase Phase { get; private set; }
        public double PhaseSeconds { get; private set; }
        public double CooldownRemaining { get; private set; }
        public bool Paused { get; set; }
        public int Facing { get; private set; }
        public Point VisualPosition { get { return new Point((int)Math.Round(x), (int)Math.Round(y)); } }
        public Point BowlPosition { get; private set; }
        public bool BowlVisible { get { return IsMeal && Phase != RoutinePhase.WalkingBack && Phase != RoutinePhase.Settling; } }
        bool IsMeal { get { return Kind == RoutineKind.Drink || Kind == RoutineKind.Eat; } }
        double Duration
        {
            get
            {
                if (Phase == RoutinePhase.WalkingOut || Phase == RoutinePhase.WalkingBack) return walkDuration;
                if (Phase == RoutinePhase.Acting)
                {
                    if (Kind == RoutineKind.Stretch) return 3.2;
                    if (Kind == RoutineKind.Roll) return 4.2;
                    return 4;
                }
                return .35;
            }
        }
        public double Progress
        {
            get
            {
                if (Phase == RoutinePhase.Waiting) return 0;
                if (Duration <= 0) return 1;
                return Math.Min(1, PhaseSeconds / Duration);
            }
        }
        public double Blend
        {
            get
            {
                if (Phase == RoutinePhase.Waiting) return 0;
                if (Phase == RoutinePhase.Entering) return Progress;
                if (Phase == RoutinePhase.Settling) return 1 - Progress;
                return 1;
            }
        }
        public DailyRoutine(Random random)
        {
            if (random == null) throw new ArgumentNullException("random");
            this.random = random;
            ActiveCat = -1;
            Facing = 1;
            CooldownRemaining = 20 + random.NextDouble() * 20;
        }
        public void Interrupt()
        {
            ActiveCat = -1;
            Kind = RoutineKind.None;
            Phase = RoutinePhase.Waiting;
            PhaseSeconds = 0;
            CooldownRemaining = 90 + random.NextDouble() * 90;
        }
        void Change(RoutinePhase phase)
        {
            Phase = phase;
            PhaseSeconds = 0;
        }
        void Walk(RoutinePhase phase, Point target, CatLayout layout)
        {
            destination = target;
            double dx = target.X - x;
            double dy = target.Y - y;
            walkDuration = Math.Sqrt(dx * dx + dy * dy) / Math.Max(70, layout.Width * .45);
            if (dx != 0) Facing = dx > 0 ? 1 : -1;
            Change(phase);
        }
        public void Update(double seconds, CatLayout layout, Point bowlPosition, bool allowStart)
        {
            if (Paused || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return;
            if (layout == null) throw new ArgumentNullException("layout");
            // Each loop consumes a phase or elapsed time, including zero-distance walks.
            while (true)
            {
                if (Phase == RoutinePhase.Waiting)
                {
                    double consumed = Math.Min(seconds, CooldownRemaining);
                    CooldownRemaining -= consumed;
                    seconds -= consumed;
                    if (CooldownRemaining > 0 || !allowStart) return;
                    ActiveCat = random.Next(3);
                    Kind = (RoutineKind)(1 + random.Next(4));
                    Point start = layout.PositionOf(ActiveCat);
                    x = start.X;
                    y = start.Y;
                    BowlPosition = bowlPosition;
                    Facing = bowlPosition.X >= x ? 1 : -1;
                    Change(RoutinePhase.Entering);
                }
                if (!IsMeal || Phase == RoutinePhase.Entering || Phase == RoutinePhase.Settling)
                {
                    Point position = layout.PositionOf(ActiveCat);
                    x = position.X;
                    y = position.Y;
                }
                if (Phase == RoutinePhase.WalkingBack && destination != layout.PositionOf(ActiveCat))
                    Walk(RoutinePhase.WalkingBack, layout.PositionOf(ActiveCat), layout);
                double remaining = Math.Max(0, Duration - PhaseSeconds);
                double step = Math.Min(seconds, remaining);
                if (Phase == RoutinePhase.WalkingOut || Phase == RoutinePhase.WalkingBack)
                {
                    double fraction = remaining <= 0 ? 1 : step / remaining;
                    x += (destination.X - x) * fraction;
                    y += (destination.Y - y) * fraction;
                }
                PhaseSeconds += step;
                seconds -= step;
                if (PhaseSeconds + 1e-9 < Duration) return;
                switch (Phase)
                {
                    case RoutinePhase.Entering:
                        if (IsMeal) Walk(RoutinePhase.WalkingOut, new Point(BowlPosition.X - Facing * (int)Math.Round(layout.Width * .18), BowlPosition.Y), layout);
                        else Change(RoutinePhase.Acting);
                        break;
                    case RoutinePhase.WalkingOut:
                        Change(RoutinePhase.Lowering);
                        break;
                    case RoutinePhase.Lowering:
                        Change(RoutinePhase.Acting);
                        break;
                    case RoutinePhase.Acting:
                        Change(IsMeal ? RoutinePhase.Rising : RoutinePhase.Settling);
                        break;
                    case RoutinePhase.Rising:
                        Walk(RoutinePhase.WalkingBack, layout.PositionOf(ActiveCat), layout);
                        break;
                    case RoutinePhase.WalkingBack:
                        Change(RoutinePhase.Settling);
                        break;
                    case RoutinePhase.Settling:
                        Interrupt();
                        break;
                }
                if (seconds <= 0) return;
            }
        }
    }
}
