using System;
using System.Media;
using System.Threading;

class AudioTone
{
    static void Main(string[] args)
    {
        using (var player = new SoundPlayer(args[0]))
        {
            player.Load();
            player.PlayLooping();
            Thread.Sleep(9000);
            player.Stop();
        }
    }
}
