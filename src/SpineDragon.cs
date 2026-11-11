using System;
using System.Drawing;

namespace ThreeCats
{
    internal sealed class SpineDragon
    {
        private const double TriggerChance = .05;
        private const double CooldownSeconds = 60;
        private const double PuffAt = .16;
        private const double TakeoffAt = .37;
        private const double LandAt = .60;
        private const double RecoverAt = .80;
        private const double EndAt = 1.10;

        private readonly Random random;
        private double seconds;
        private double cooldown;
        private int distance;
        private int height;

        internal SpineDragon(Random random)
        {
            this.random = random;
            ActiveCat = -1;
        }

        internal int ActiveCat { get; private set; }
        internal bool Mirrored { get { return distance < 0; } }
        internal int Frame { get { return seconds < PuffAt ? 0 : seconds < TakeoffAt ? 1 : seconds < LandAt ? 2 : 3; } }
        internal double Recovery { get { return Math.Max(0, (seconds - RecoverAt) / (EndAt - RecoverAt)); } }

        internal Point Offset
        {
            get
            {
                double flight = Math.Max(0, Math.Min(1, (seconds - TakeoffAt) / (LandAt - TakeoffAt)));
                double x = distance * flight * (1 - Recovery);
                double y = -height * 4 * flight * (1 - flight);
                return new Point((int)Math.Round(x), (int)Math.Round(y));
            }
        }

        internal bool TryStart(int cat, int hopDistance, int hopHeight)
        {
            if (ActiveCat >= 0 || cooldown > 0 || random.NextDouble() >= TriggerChance) return false;
            ActiveCat = cat;
            distance = hopDistance;
            height = hopHeight;
            seconds = 0;
            return true;
        }

        internal void Advance(double elapsed)
        {
            if (ActiveCat < 0)
            {
                cooldown = Math.Max(0, cooldown - elapsed);
                return;
            }
            seconds += elapsed;
            if (seconds >= EndAt) Cancel();
        }

        internal void Cancel()
        {
            if (ActiveCat < 0) return;
            ActiveCat = -1;
            cooldown = CooldownSeconds;
        }
    }
}
