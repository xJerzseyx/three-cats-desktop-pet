using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using ThreeCats;

class IntegrationTests
{
    static int checks;
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL " + label);
        checks++;
        Console.WriteLine("PASS " + label);
    }
    static void Pump(int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(15); }
    }
    static Process Tone(string exe)
    {
        return Process.Start(new ProcessStartInfo(Path.GetFullPath("work/" + exe), "\"" + Path.GetFullPath("work/test-tone.wav") + "\"") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
    }
    static bool RegionEqual(Bitmap first, Bitmap second, Rectangle area)
    {
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
                if (first.GetPixel(x, y) != second.GetPixel(x, y)) return false;
        return true;
    }
    [STAThread]
    static void Main()
    {
        try { Run(); }
        catch (Exception exception)
        {
            Console.WriteLine("TEST FAILURE: " + exception.GetType().Name + " " + exception.Message);
            Environment.Exit(1);
        }
    }

    static void Run()
    {
        using (var renderer = new SpriteRenderer())
        {
            var watch = Stopwatch.StartNew();
            renderer.Render(600, new double[3], new double[3], 0, 0, 0).Save("work/render-neutral.png", ImageFormat.Png);
            renderer.Render(600, new[] { -1.0, 0.0, 1.0 }, new[] { -1.0, 0.0, 1.0 }, 0, 0, 0).Save("work/render-follow.png", ImageFormat.Png);
            renderer.Render(600, new double[3], new double[3], 1, 0.8, 1.0).Save("work/render-music.png", ImageFormat.Png);
            Check(!File.ReadAllBytes("work/render-neutral.png").SequenceEqual(File.ReadAllBytes("work/render-follow.png")), "mouse direction changes head rendering");
            byte[] musicA = File.ReadAllBytes("work/render-music.png");
            renderer.Render(600, new[] { -1.0, 1.0, -0.5 }, new[] { 1.0, -1.0, 0.5 }, 1, 0.8, 1.0).Save("work/render-music-other-cursor.png", ImageFormat.Png);
            Check(!musicA.SequenceEqual(File.ReadAllBytes("work/render-music-other-cursor.png")), "pupils follow cursor during music");
            using (var first = new Bitmap("work/render-music.png"))
            using (var second = new Bitmap("work/render-music-other-cursor.png"))
            {
                Check(RegionEqual(first, second, new Rectangle(0, 0, 600, 60)), "head upper silhouette ignores cursor during music");
                Check(RegionEqual(first, second, new Rectangle(0, 120, 600, 180)), "body and lower face ignore cursor during music");
            }
            for (int i = 0; i < 30; i++) renderer.Render(600, new double[3], new double[3], 1, 0.6, i / 30.0);
            Console.WriteLine("Renderer average: " + (watch.ElapsedMilliseconds / 34.0).ToString("F2") + " ms per 600px frame");
        }
        string configPath = Path.GetFullPath("work/integration-settings-" + Guid.NewGuid().ToString("N") + ".xml");
        using (var pet = new PetForm(configPath))
        {
            pet.Show();
            Pump(900);
            Check(pet.Visible && pet.TopMost && !pet.ShowInTaskbar, "transparent pet starts visible, topmost, without taskbar button");
            Check(pet.FormBorderStyle == FormBorderStyle.None, "pet has no window border");
            Check(pet.Width == 300 && pet.Height == 150, "first launch window uses 50 percent size");
            var menu = (ContextMenuStrip)typeof(PetForm).GetField("menu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pet);
            Check(menu.Items.OfType<ToolStripMenuItem>().Any(item => item.Text == "开机自启动"), "tray provides optional startup toggle");
            pet.ResizePet(720);
            Pump(100);
            Check(pet.Width == 720 && pet.Height == 360, "resize retains group aspect ratio");
            pet.ToggleVisibility();
            Pump(100);
            Check(!pet.Visible, "hide via common tray handler");
            pet.ToggleVisibility();
            Pump(100);
            Check(pet.Visible, "reshow via common tray handler");
            bool ownedDialog = false;
            using (var closeDialog = new System.Windows.Forms.Timer { Interval = 100 })
            {
                closeDialog.Tick += delegate
                {
                    var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "伊二三万 · 音乐来源");
                    if (dialog == null) return;
                    ownedDialog = object.ReferenceEquals(dialog.Owner, pet);
                    dialog.DialogResult = DialogResult.Cancel;
                    closeDialog.Stop();
                };
                closeDialog.Start();
                typeof(PetForm).GetMethod("ChoosePlayers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pet, null);
            }
            Check(ownedDialog, "settings dialog stays owned above topmost pet");
            pet.Close();
            Pump(50);
        }
        Check(Settings.Load(configPath).Width == 720, "window size persisted on exit");
        File.Delete(configPath);
        using (var monitor = new AudioMonitor())
        {
            monitor.Refresh();
            Console.WriteLine("Audio available=" + monitor.Available + "; error=" + monitor.Error);
            Check(monitor.Error == null, "audio session API initializes without an interface error");
            if (monitor.Available)
            {
                monitor.Selected = PlayerNames.Parse("allowaudio");
                using (var ignored = Tone("IgnoreAudio.exe"))
                {
                    Thread.Sleep(1300);
                    monitor.Refresh();
                    for (int i = 0; i < 8; i++)
                    {
                        Check(monitor.ReadPeak() == 0, "unselected live audio excluded " + i);
                        Thread.Sleep(50);
                    }
                    Console.WriteLine("Sessions: " + string.Join(", ", monitor.ReadSources().Select(s => s.ProcessName + "=" + s.Peak.ToString("F3")).ToArray()));
                    using (var allowed = Tone("AllowAudio.exe"))
                    {
                        Thread.Sleep(1300);
                        monitor.Refresh();
                        float maximum = 0;
                        for (int i = 0; i < 12; i++) { maximum = Math.Max(maximum, monitor.ReadPeak()); Thread.Sleep(50); }
                        Console.WriteLine("Selected live audio maximum=" + maximum);
                        Check(maximum > 0.001, "selected live audio detected while other process also plays");
                        allowed.Kill();
                        allowed.WaitForExit();
                    }
                    Thread.Sleep(650);
                    monitor.Refresh();
                    Check(monitor.ReadPeak() == 0, "selected source stops while unrelated source continues");
                    ignored.Kill();
                    ignored.WaitForExit();
                }
            }
            else Console.WriteLine("SKIP live playback tests: no audio endpoint");
        }
        Console.WriteLine(checks + " integration checks passed");
    }
}
