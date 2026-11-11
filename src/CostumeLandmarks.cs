using System.Drawing;

namespace ThreeCats
{
    // Brim and collar centers measured in the unmirrored atlas cells.
    internal struct CostumeLandmark
    {
        private readonly PointF hat;
        private readonly PointF scarf;
        private readonly float angle;
        private readonly float size;

        internal CostumeLandmark(int hatX, int hatY, int scarfX, int scarfY, float angle = 0, float size = 1)
        {
            hat = new PointF(hatX, hatY);
            scarf = new PointF(scarfX, scarfY);
            this.angle = angle;
            this.size = size;
        }

        internal CostumePose Project(RectangleF target, float cellWidth, float cellHeight, int groupWidth, bool mirror)
        {
            float x = target.Width / cellWidth;
            float y = target.Height / cellHeight;
            float left = mirror ? target.Right : target.Left;
            if (mirror) x = -x;
            return new CostumePose(new PointF(left + hat.X * x, target.Top + hat.Y * y),
                new PointF(left + scarf.X * x, target.Top + scarf.Y * y),
                mirror ? -angle : angle, groupWidth / 600f * size, mirror);
        }
    }

    internal static class CostumeLandmarks
    {
        // Coordinates refer to a 216px square thumbnail of each 256px daily sprite.
        internal static readonly CostumeLandmark[,] Daily =
        {
            {
                new CostumeLandmark(119,44,119,95), new CostumeLandmark(120,55,120,111),
                new CostumeLandmark(109,115,110,167), new CostumeLandmark(92,141,96,184),
                new CostumeLandmark(93,136,95,182), new CostumeLandmark(97,113,102,166),
                new CostumeLandmark(119,44,119,95), new CostumeLandmark(103,61,121,121,-24),
                new CostumeLandmark(43,125,74,161,-42,.85f), new CostumeLandmark(52,185,89,147,-166,.82f),
                new CostumeLandmark(182,165,134,164,82,.85f), new CostumeLandmark(131,65,148,123,-23),
                new CostumeLandmark(119,44,119,95), new CostumeLandmark(155,52,152,110,8),
                new CostumeLandmark(158,52,154,110,8), new CostumeLandmark(170,54,161,112,8),
                new CostumeLandmark(170,54,165,112,8), new CostumeLandmark(160,125,143,168,18,.87f),
                new CostumeLandmark(161,134,136,171,17,.87f), new CostumeLandmark(161,134,136,173,17,.87f),
                new CostumeLandmark(158,89,143,143,8,.9f), new CostumeLandmark(165,132,142,169,17,.87f),
                new CostumeLandmark(166,136,144,172,17,.87f), new CostumeLandmark(159,89,147,145,8,.9f)
            },
            {
                new CostumeLandmark(118,43,117,93,0,1.04f), new CostumeLandmark(118,56,117,110,0,1.04f),
                new CostumeLandmark(101,119,109,169), new CostumeLandmark(87,144,94,185),
                new CostumeLandmark(88,139,95,183), new CostumeLandmark(76,113,83,165),
                new CostumeLandmark(118,43,117,93), new CostumeLandmark(96,64,119,118,-21),
                new CostumeLandmark(42,126,69,163,-40,.88f), new CostumeLandmark(49,188,83,151,-168,.86f),
                new CostumeLandmark(184,165,127,158,79,.88f), new CostumeLandmark(130,61,148,121,-20),
                new CostumeLandmark(118,43,117,93), new CostumeLandmark(151,47,151,96,8),
                new CostumeLandmark(153,52,154,103,8), new CostumeLandmark(160,53,157,105,8),
                new CostumeLandmark(166,56,164,106,8), new CostumeLandmark(167,130,146,177,15,.87f),
                new CostumeLandmark(166,136,143,179,17,.87f), new CostumeLandmark(166,138,143,180,17,.87f),
                new CostumeLandmark(155,85,145,137,8,.92f), new CostumeLandmark(166,138,146,179,17,.87f),
                new CostumeLandmark(167,137,146,179,17,.87f), new CostumeLandmark(159,85,148,139,8,.92f)
            },
            {
                new CostumeLandmark(119,44,120,102,0,.96f), new CostumeLandmark(111,56,112,112,0,.96f),
                new CostumeLandmark(107,119,110,167,0,.95f), new CostumeLandmark(91,143,92,186,0,.95f),
                new CostumeLandmark(90,138,91,182,0,.95f), new CostumeLandmark(94,116,97,165,0,.95f),
                new CostumeLandmark(119,44,120,102), new CostumeLandmark(100,63,122,122,-26,.96f),
                new CostumeLandmark(39,126,70,165,-43,.85f), new CostumeLandmark(51,187,86,145,-162,.82f),
                new CostumeLandmark(182,167,133,159,84,.85f), new CostumeLandmark(130,61,149,122,-23,.96f),
                new CostumeLandmark(119,44,120,102), new CostumeLandmark(151,51,146,108,9,.96f),
                new CostumeLandmark(155,53,150,110,9,.96f), new CostumeLandmark(168,54,158,112,9,.96f),
                new CostumeLandmark(174,57,164,113,9,.96f), new CostumeLandmark(162,130,144,175,18,.85f),
                new CostumeLandmark(164,139,141,178,18,.85f), new CostumeLandmark(163,140,141,179,18,.85f),
                new CostumeLandmark(160,91,147,143,8,.9f), new CostumeLandmark(168,139,146,177,18,.85f),
                new CostumeLandmark(164,139,144,178,18,.85f), new CostumeLandmark(158,89,145,145,8,.9f)
            }
        };

        // The lift and dragon contact sheets use 240px previews.
        internal static readonly CostumeLandmark[] Lift =
        {
            new CostumeLandmark(131,37,131,84), new CostumeLandmark(119,37,123,83,0,1.05f),
            new CostumeLandmark(118,35,117,85,0,.94f)
        };

        internal static readonly CostumeLandmark[,] Dragon =
        {
            {
                new CostumeLandmark(56,108,67,155,-8,.82f), new CostumeLandmark(48,108,67,155,-8,.82f),
                new CostumeLandmark(49,124,63,168,-8,.82f), new CostumeLandmark(57,110,74,155,-8,.82f)
            },
            {
                new CostumeLandmark(50,104,56,151,-8,.82f), new CostumeLandmark(50,102,58,148,-8,.82f),
                new CostumeLandmark(49,123,58,169,-8,.82f), new CostumeLandmark(54,102,63,150,-8,.82f)
            },
            {
                new CostumeLandmark(52,105,56,150,-8,.82f), new CostumeLandmark(50,103,57,150,-8,.82f),
                new CostumeLandmark(49,127,57,171,-8,.82f), new CostumeLandmark(55,105,57,150,-8,.82f)
            }
        };
    }
}
