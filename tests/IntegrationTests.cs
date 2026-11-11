using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using ThreeCats;

class IntegrationTests
{
    [DllImport("user32.dll")]
    static extern IntPtr GetWindow(IntPtr window, uint command);

    static bool IsAbove(IntPtr upper, IntPtr lower)
    {
        for (IntPtr window = GetWindow(lower, 3); window != IntPtr.Zero; window = GetWindow(window, 3))
            if (window == upper) return true;
        return false;
    }

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

    static object Invoke(object target, string method, params object[] arguments)
    {
        return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }

    static CatLayout Layout(PetForm pet)
    {
        return (CatLayout)typeof(PetForm).GetField("layout", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pet);
    }

    static PetForm CreatePet(string configPath)
    {
        var constructor = typeof(PetForm).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(string), typeof(Random), typeof(Func<DateTime>) }, null);
        return (PetForm)constructor.Invoke(new object[] { configPath, new Random(), (Func<DateTime>)(() => new DateTime(2026, 11, 12)) });
    }

    static void Mouse(CatWindow cat, string method, Point screenPoint)
    {
        Point local = cat.PointToClient(screenPoint);
        Invoke(cat, method, new MouseEventArgs(MouseButtons.Left, 1, local.X, local.Y, 0));
    }

    static Point DragCatTo(PetForm pet, CatWindow cat, Point destination)
    {
        Point start = cat.PointToScreen(new Point(cat.Width / 2, cat.Height / 2));
        Mouse(cat, "OnMouseDown", start);
        Point foot = LiftRenderer.FootOffset(Layout(pet).Width);
        Point grip = LiftRenderer.GripOffset(cat.CatId, Layout(pet).Width);
        var finish = new Point(destination.X + grip.X - foot.X, destination.Y + grip.Y - foot.Y);
        Mouse(cat, "OnMouseMove", finish);
        Check(cat.Size == LiftRenderer.FrameSize(Layout(pet).Width) && cat.PointToScreen(grip) == finish,
            "dragging cat " + cat.CatId + " uses the suspended pose with its scruff under the pointer");
        return finish;
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
        new Settings { CursorTheme = "off" }.Save(configPath);
        CatLayout expectedLayout;
        using (var pet = CreatePet(configPath))
        {
            pet.Show();
            Pump(900);
            Check(pet.Visible && pet.TopMost && !pet.ShowInTaskbar, "transparent pet starts visible, topmost, without taskbar button");
            Check(pet.FormBorderStyle == FormBorderStyle.None, "pet has no window border");
            var cats = (CatWindow[])typeof(PetForm).GetField("cats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pet);
            var preview = (Form)typeof(PetForm).GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pet);
            Check(Layout(pet).Width == 300 && cats.All(cat => cat.Size == SpriteRenderer.CatFrameSize(300)), "first launch windows use 50 percent size");
            Check(cats.Length == 3 && cats.Select(cat => cat.CatId).SequenceEqual(new[] { 0, 1, 2 })
                && cats.All(cat => cat.Visible) && cats.Select(cat => cat.Handle).Distinct().Count() == 3,
                "each cat has a stable identity and an independent visible window");
            var menu = (ContextMenuStrip)typeof(PetForm).GetField("menu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pet);
            Check(menu.Items.OfType<ToolStripMenuItem>().Any(item => item.Text == "开机自启动"), "tray provides optional startup toggle");
            var cursorMenu = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "全局光标");
            var cursorChoices = cursorMenu.DropDownItems.OfType<ToolStripMenuItem>().ToArray();
            Check(cursorChoices.Select(item => item.Text).SequenceEqual(new[] { "逗猫棒", "猫条", "猫薄荷", "关闭并恢复原光标" }),
                "tray provides all three global themes and an off option");
            Check(cursorChoices.Single(item => item.Checked).Text == "关闭并恢复原光标", "saved off choice stays selected at startup");
            var animationTime = typeof(PetForm).GetField("lastTime", BindingFlags.Instance | BindingFlags.NonPublic);
            var sizes = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "调整大小");
            sizes.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "放大").PerformClick();
            Check(Layout(pet).Width == 336, "enlarge menu scales from the common group width");
            pet.ResizePet(600);
            sizes.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "缩小").PerformClick();
            Check(Layout(pet).Width == 535, "shrink menu scales from the common group width");
            pet.ResizePet(300);
            // Other desktop apps can take focus while these Z-order checks pump messages.
            menu.AutoClose = false;
            foreach (var child in new[] { sizes, cursorMenu }) child.DropDown.AutoClose = false;
            string menuClosed = "none", childClosed = "none";
            menu.Closed += delegate(object sender, ToolStripDropDownClosedEventArgs args) { menuClosed = args.CloseReason.ToString(); };
            foreach (var child in new[] { sizes, cursorMenu })
                child.DropDown.Closed += delegate(object sender, ToolStripDropDownClosedEventArgs args) { childClosed = args.CloseReason.ToString(); };
            using (var foreground = new Form { ShowInTaskbar = false, Size = new Size(100, 100) })
            foreach (bool fromPet in new[] { true, false })
            {
                foreground.Show();
                foreground.Activate();
                Pump(50);
                string entry = fromPet ? "pet context menu" : "tray context menu";
                if (fromPet)
                    typeof(PetForm).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(pet, new object[] { new MouseEventArgs(MouseButtons.Right, 1, 20, 20, 0) });
                else
                    menu.Show(pet.Location);
                menu.Location = pet.Location;
                double beforeMenu = (double)animationTime.GetValue(pet);
                Pump(1300);
                Check(menu.Visible && cats.All(cat => IsAbove(menu.Handle, cat.Handle)), entry + " stays above every animated cat");
                Check(pet.Visible && (double)animationTime.GetValue(pet) > beforeMenu + 1,
                    entry + " keeps the pet animation running");
                foreach (var submenu in new[] { sizes, cursorMenu })
                {
                    submenu.ShowDropDown();
                    Pump(1300);
                    if (!submenu.DropDown.Visible || !menu.Visible || cats.Any(cat => !IsAbove(submenu.DropDown.Handle, cat.Handle) || !IsAbove(menu.Handle, cat.Handle)))
                        Console.WriteLine("Layer diagnostics root=" + menu.Visible + " child=" + submenu.DropDown.Visible
                            + " rootClose=" + menuClosed + " childClose=" + childClosed
                            + " coveredCats=" + string.Join(",", cats.Where(cat => !IsAbove(submenu.DropDown.Handle, cat.Handle) || !IsAbove(menu.Handle, cat.Handle)).Select(cat => cat.CatId)));
                    Check(submenu.DropDown.Visible && cats.All(cat => IsAbove(submenu.DropDown.Handle, cat.Handle)
                        && IsAbove(menu.Handle, cat.Handle)), entry + " keeps " + submenu.Text + " above every cat");
                    submenu.HideDropDown();
                }
                menu.Close();
                Pump(1300);
                Check(pet.Visible && pet.TopMost, entry + " preserves desktop pet visibility and topmost after closing");
            }
            menu.AutoClose = true;
            foreach (var child in new[] { sizes, cursorMenu }) child.DropDown.AutoClose = true;
            Point anchor = Layout(pet).Anchor;
            Point click = cats[1].PointToScreen(new Point(cats[1].Width / 2, cats[1].Height / 2));
            Mouse(cats[1], "OnMouseDown", click);
            Mouse(cats[1], "OnMouseUp", click);
            Check(Layout(pet).Queue.SequenceEqual(new[] { 0, 1, 2 }), "a click within the drag threshold preserves the queue");
            Check(cats.All(cat => cat.Size == SpriteRenderer.CatFrameSize(Layout(pet).Width)),
                "a click within the drag threshold keeps every cat standing");
            Point originalLast = Layout(pet).PositionOf(2);
            var detachedFoot = new Point(anchor.X - 250, anchor.Y - 200);
            Point release = DragCatTo(pet, cats[1], detachedFoot);
            Check(Layout(pet).Queue.SequenceEqual(new[] { 0, 2 }) && Layout(pet).PositionOf(2).X < originalLast.X,
                "left dragging a single window detaches only that cat and compacts the queue");
            Invoke(pet, "SaveSettings");
            Check(CatLayout.FromSettings(Settings.Load(configPath), Point.Empty).Queue.SequenceEqual(new[] { 0, 1, 2 }),
                "saving during a drag persists the last confirmed layout");
            Mouse(cats[1], "OnMouseUp", release);
            Check(Layout(pet).PositionOf(1) == detachedFoot && !preview.Visible, "releasing a detached cat confirms its independent position");
            var groupOrigin = new Point(400, 400);
            Invoke(pet, "BeginCatDrag", cats[0], groupOrigin, true);
            Invoke(pet, "UpdateDrag", new Point(420, 370));
            Check(cats.All(cat => cat.Size == SpriteRenderer.CatFrameSize(Layout(pet).Width)),
                "Shift dragging keeps the group standing");
            Invoke(pet, "FinishDrag");
            Check(Layout(pet).Anchor == new Point(anchor.X + 20, anchor.Y - 30) && Layout(pet).PositionOf(1) == detachedFoot,
                "Shift dragging moves the current queue while detached cats remain in place");
            anchor = Layout(pet).Anchor;
            foreach (int id in new[] { 0, 2 })
            {
                release = DragCatTo(pet, cats[id], new Point(anchor.X - 350 - id * 60, anchor.Y - 250));
                if (id == 2) Check(preview.Visible, "empty queue marker appears while dragging the final cat away");
                Mouse(cats[id], "OnMouseUp", release);
            }
            Check(Layout(pet).Queue.Count == 0 && Layout(pet).Anchor == anchor && !preview.Visible,
                "all cats can leave while the original queue target is retained without an idle marker");
            CatLayout allDetached = Layout(pet).Copy();
            using (var emptyRestart = CreatePet(configPath))
            {
                emptyRestart.Show();
                Pump(100);
                Check(Layout(emptyRestart).Queue.Count == 0 && Layout(emptyRestart).Anchor == anchor
                    && Enumerable.Range(0, 3).All(id => Layout(emptyRestart).PositionOf(id) == allDetached.PositionOf(id)),
                    "normal startup restores an empty queue target and all detached cat positions");
                emptyRestart.Close();
            }
            foreach (int id in new[] { 2, 1, 0 })
            {
                release = DragCatTo(pet, cats[id], anchor);
                Check(preview.Visible, "returning cat " + id + " shows an insertion preview");
                Mouse(cats[id], "OnMouseUp", release);
            }
            Check(Layout(pet).Queue.SequenceEqual(new[] { 2, 1, 0 }) && !preview.Visible,
                "all three cats return to the original queue in release order");
            CatLayout confirmed = Layout(pet).Copy();
            DragCatTo(pet, cats[1], new Point(anchor.X - 300, anchor.Y - 250));
            cats[1].Capture = false;
            Check(Layout(pet).Queue.SequenceEqual(confirmed.Queue) && Layout(pet).PositionOf(1) == confirmed.PositionOf(1)
                && !preview.Visible && cats.All(cat => cat.Size == SpriteRenderer.CatFrameSize(Layout(pet).Width)),
                "capture loss cancels the drag and restores confirmed standing placement");
            pet.ResizePet(720);
            Pump(100);
            Check(Layout(pet).Width == 720 && cats.All(cat => cat.Size == SpriteRenderer.CatFrameSize(720)), "resize applies one common scale to all cat windows");
            pet.ToggleVisibility();
            Pump(100);
            Check(cats.All(cat => !cat.Visible), "hide all cats via common tray handler");
            pet.ToggleVisibility();
            Pump(100);
            Check(cats.All(cat => cat.Visible), "reshow all cats via common tray handler");
            bool ownedDialog = false;
            bool dialogAboveCats = false;
            using (var closeDialog = new System.Windows.Forms.Timer { Interval = 1300 })
            {
                closeDialog.Tick += delegate
                {
                    var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "伊二三万 · 音乐来源");
                    if (dialog == null) return;
                    ownedDialog = object.ReferenceEquals(dialog.Owner, pet);
                    dialogAboveCats = cats.All(cat => IsAbove(dialog.Handle, cat.Handle));
                    dialog.DialogResult = DialogResult.Cancel;
                    closeDialog.Stop();
                };
                closeDialog.Start();
                typeof(PetForm).GetMethod("ChoosePlayers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pet, null);
            }
            Check(ownedDialog, "settings dialog stays owned above topmost pet");
            Check(dialogAboveCats, "settings dialog remains above every cat during periodic animation");
            expectedLayout = Layout(pet).Copy();
            pet.Close();
            Pump(50);
        }
        Check(Settings.Load(configPath).Width == 720, "window size persisted on exit");
        using (var restarted = CreatePet(configPath))
        {
            restarted.Show();
            Pump(100);
            Check(Layout(restarted).Queue.SequenceEqual(new[] { 2, 1, 0 })
                && Enumerable.Range(0, 3).All(id => Layout(restarted).PositionOf(id) == expectedLayout.PositionOf(id)),
                "normal startup restores confirmed identities, queue order and positions");
            restarted.Close();
        }
        File.Delete(configPath);
        using (var monitor = new AudioMonitor())
        {
            monitor.Refresh();
            Console.WriteLine("Audio available=" + monitor.Available + "; error=" + monitor.Error);
            Check(monitor.Error == null || (!monitor.Available && monitor.Error == "没有可用的音频输出设备"),
                "audio endpoint enumeration completes without an interface error");
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
            else
            {
                Check(monitor.ReadPeak() == 0, "missing audio endpoint supplies silence");
                Console.WriteLine("SKIP live playback tests: no audio endpoint");
            }
        }
        Console.WriteLine(checks + " integration checks passed");
    }
}
