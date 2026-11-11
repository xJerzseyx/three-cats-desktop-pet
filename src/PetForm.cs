using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ThreeCats
{
    public sealed class PetForm : CatWindow
    {
        private readonly Settings settings;
        private readonly string settingsPath;
        private readonly CatWindow[] cats;
        private readonly CatLayout layout;
        private readonly DailyRoutine daily;
        private readonly DailyRenderer dailyRenderer;
        private readonly LiftRenderer liftRenderer = new LiftRenderer();
        private readonly BowlWindow bowl = new BowlWindow();
        private readonly QueuePreview preview = new QueuePreview();
        private readonly AudioMonitor audio = new AudioMonitor();
        private readonly MotionState motion = new MotionState();
        private readonly Timer timer = new Timer { Interval = 33 };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly ToolStripMenuItem visibility;
        private readonly ToolStripMenuItem status;
        private readonly ToolStripMenuItem autoStart;
        private readonly ToolStripMenuItem cursorMenu = new ToolStripMenuItem("全局光标");
        private readonly CursorSession cursorSession = new CursorSession();
        private readonly StartupRegistration startup = new StartupRegistration();
        private double lastTime;
        private double musicMix;
        private double nextStatus;
        private CatDrag drag;
        private bool saveWarningShown;
        private static readonly string[] routineNames = { "", "伸懒腰", "打滚", "喝水", "吃饭" };
        internal static readonly uint ShowMessage = Native.RegisterWindowMessage("ThreeCatsDesktop.Show.v1");

        private sealed class CatDrag
        {
            internal CatWindow Window;
            internal Point Origin;
            internal Point CatOrigin;
            internal CatLayout Snapshot;
            internal bool GrabbedRoutine;
            internal bool InterruptedRoutine;
            internal bool Group;
            internal bool Started;
            internal bool CanReturn;
        }

        public PetForm(string configPath) : this(configPath, new Random()) { }

        internal PetForm(string configPath, Random random) : base(0)
        {
            daily = new DailyRoutine(random);
            dailyRenderer = new DailyRenderer();
            settingsPath = configPath;
            settings = Settings.Load(configPath);
            audio.Selected = PlayerNames.Parse(settings.Players);
            var area = Screen.PrimaryScreen.WorkingArea;
            var legacyLocation = settings.X == int.MinValue || settings.Y == int.MinValue
                ? new Point(area.Right - settings.Width - 24, area.Bottom - (int)Math.Round(settings.Width * SpriteRenderer.Aspect) - 12)
                : new Point(settings.X, settings.Y);
            var initialAnchor = new Point(legacyLocation.X + (int)Math.Round(settings.Width * 393.0 / 1774),
                legacyLocation.Y + SpriteRenderer.CatFootOffset(settings.Width).Y);
            layout = CatLayout.FromSettings(settings, initialAnchor);
            cats = new[] { this, new CatWindow(1), new CatWindow(2) };
            foreach (var cat in cats)
            {
                cat.MouseDown += CatMouseDown;
                cat.MouseMove += CatMouseMove;
                cat.MouseUp += CatMouseUp;
                cat.MouseCaptureChanged += CatCaptureChanged;
                cat.MouseWheel += CatMouseWheel;
                cat.ScreenChanged += delegate { CancelDrag(); StopRoutine(); FitToScreen(); };
            }
            FitToScreen();
            status = new ToolStripMenuItem("正在检测音乐…") { Enabled = false };
            menu.Items.Add(status);
            menu.Items.Add(new ToolStripSeparator());
            visibility = new ToolStripMenuItem("隐藏桌宠", null, delegate { ToggleVisibility(); });
            menu.Items.Add(visibility);
            var sizes = new ToolStripMenuItem("调整大小");
            foreach (int percent in new[] { 50, 75, 100, 125, 150, 200 })
            {
                int requestedWidth = 600 * percent / 100;
                sizes.DropDownItems.Add(percent + "%", null, delegate { ResizePet(requestedWidth); });
            }
            sizes.DropDownItems.Add(new ToolStripSeparator());
            sizes.DropDownItems.Add("放大", null, delegate { ResizePet((int)(layout.Width * 1.12)); });
            sizes.DropDownItems.Add("缩小", null, delegate { ResizePet((int)(layout.Width / 1.12)); });
            menu.Items.Add(sizes);
            AddCursorChoice("逗猫棒", "wand");
            AddCursorChoice("猫条", "treat");
            AddCursorChoice("猫薄荷", "catnip");
            cursorMenu.DropDownItems.Add(new ToolStripSeparator());
            AddCursorChoice("关闭并恢复原光标", "off");
            menu.Items.Add(cursorMenu);
            menu.Items.Add("音乐来源设置…", null, delegate { ChoosePlayers(); });
            autoStart = new ToolStripMenuItem("开机自启动", null, delegate { ToggleAutoStart(); });
            menu.Items.Add(autoStart);
            menu.Items.Add("移回主屏幕", null, delegate { MoveToPrimary(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("拖单猫 · Shift 拖队伍 · 滚轮缩放", null, delegate { }) .Enabled = false;
            menu.Items.Add("退出程序", null, delegate { Close(); });
            menu.Opening += delegate { CancelDrag(); };
            menu.Opened += RaiseMenu;
            foreach (var item in menu.Items.OfType<ToolStripDropDownItem>().Where(item => item.HasDropDownItems))
                item.DropDown.Opened += RaiseMenu;
            tray = new NotifyIcon { Text = Program.AppName, Icon = MakeIcon(), ContextMenuStrip = menu, Visible = true };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ToggleVisibility(); };
            RefreshAutoStart();
            timer.Tick += delegate { TickAnimation(); };
            Shown += delegate
            {
                SelectCursor(settings.CursorTheme == "off" || CursorScheme.IsTheme(settings.CursorTheme)
                    ? settings.CursorTheme : "wand", false);
                lastTime = clock.Elapsed.TotalSeconds;
                // Shown can be dispatched after an immediate hide request.
                if (!Visible) return;
                foreach (var cat in cats.Skip(1)) cat.Show();
                timer.Start();
                TickAnimation();
            };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == ShowMessage) { ShowPet(); return; }
            base.WndProc(ref message);
        }

        private static Icon MakeIcon()
        {
            using (var stream = typeof(PetForm).Assembly.GetManifestResourceStream("ThreeCats.Cats.png"))
            using (var cats = new Bitmap(stream))
            using (var small = new Bitmap(32, 32))
            {
                using (var graphics = Graphics.FromImage(small))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(cats, new Rectangle(0, 0, 32, 32), new Rectangle(220, 60, 355, 355), GraphicsUnit.Pixel);
                }
                IntPtr handle = small.GetHicon();
                try { using (var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }

        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);

        private static void RaiseMenu(object sender, EventArgs e)
        {
            Native.SetWindowPos(((ToolStripDropDown)sender).Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
        }

        private void TickAnimation()
        {
            double now = clock.Elapsed.TotalSeconds;
            double elapsed = Math.Max(0.001, Math.Min(1, now - lastTime));
            lastTime = now;
            // Hidden pets sleep. Reshowing refreshes the selected audio sessions.
            if (!Visible) return;
            if (drag != null && (Native.GetAsyncKeyState(0x1b) & 0x8000) != 0) CancelDrag();
            motion.Update(audio.ReadPeak(), elapsed);
            musicMix += ((motion.Playing ? 1 : 0) - musicMix) * (1 - Math.Exp(-elapsed * 9));
            daily.Update(elapsed, layout, GetBowlPosition(), drag == null && !menu.Visible && !OwnedForms.Any(form => form.Visible));
            var cursor = Cursor.Position;
            ApplyLayout();
            foreach (var cat in cats)
                cat.Animate(cursor, layout.Width, elapsed, musicMix, motion.Energy, now, daily, dailyRenderer, IsLifted(cat) ? liftRenderer : null);
            if (daily.BowlVisible)
            {
                bowl.ShowMeal(daily.Kind, daily.BowlPosition, layout.Width);
                Native.SetWindowPos(bowl.Handle, cats[daily.ActiveCat].Handle, 0, 0, 0, 0, 0x0013);
            }
            else bowl.Hide();
            if (drag != null && drag.Started && !drag.Group)
                preview.ShowTarget(layout, drag.Window.CatId, drag.CanReturn, musicMix, motion.Energy, now);
            if (now > nextStatus)
            {
                status.Text = !audio.Available ? (audio.Error ?? "等待音频设备") : motion.Playing ? "音乐播放中 · 同步摇摆" : "等待音乐 · 头部跟随鼠标";
                if (daily.ActiveCat >= 0) status.Text = "猫咪日常 · " + routineNames[(int)daily.Kind];
                if (!menu.Visible && !OwnedForms.Any(form => form.Visible))
                {
                    if (bowl.Visible) Native.SetWindowPos(bowl.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
                    foreach (var cat in cats) Native.SetWindowPos(cat.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
                    if (preview.Visible) Native.SetWindowPos(preview.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
                }
                nextStatus = now + 1;
            }
        }

        public void ToggleVisibility()
        {
            CancelDrag();
            if (Visible)
            {
                daily.Paused = true;
                bowl.Hide();
                foreach (var cat in cats) cat.Hide();
                timer.Stop();
                audio.Dispose();
                visibility.Text = "显示桌宠";
            }
            else ShowPet();
        }

        public void ShowPet()
        {
            CancelDrag();
            daily.Paused = false;
            FitToScreen();
            audio.Refresh();
            motion.Update(0, 1);
            musicMix = 0;
            foreach (var cat in cats) cat.Show();
            visibility.Text = "隐藏桌宠";
            lastTime = clock.Elapsed.TotalSeconds;
            timer.Start();
            TickAnimation();
        }

        public void ResizePet(int requestedWidth)
        {
            CancelDrag();
            StopRoutine();
            layout.Resize(requestedWidth);
            FitToScreen();
            SaveSettings();
            if (Visible) TickAnimation();
        }

        private void FitToScreen()
        {
            var areas = Screen.AllScreens.Select(screen => screen.WorkingArea).ToArray();
            var queueScreen = Screen.FromRectangle(QueueBounds()).WorkingArea;
            int maximumWidth = (int)Math.Min(queueScreen.Width * 1774.0 / 1890, areas.Min(area => area.Height) / SpriteRenderer.Aspect);
            layout.Resize(Math.Min(layout.Width, maximumWidth));
            ClampLayout(areas);
            ApplyLayout();
        }

        private Rectangle BoundsAt(Point foot)
        {
            Point offset = SpriteRenderer.CatFootOffset(layout.Width);
            return new Rectangle(new Point(foot.X - offset.X, foot.Y - offset.Y), SpriteRenderer.CatFrameSize(layout.Width));
        }

        private Rectangle QueueBounds()
        {
            var bounds = BoundsAt(layout.Anchor);
            int slots = Math.Max(1, layout.Queue.Count);
            bounds.Width += (slots - 1) * layout.Spacing;
            return bounds;
        }

        private void ClampLayout(Rectangle[] areas)
        {
            Rectangle queueBounds = QueueBounds();
            Rectangle clamped = Placement.Clamp(queueBounds, areas);
            layout.MoveQueue(new Point(layout.Anchor.X + clamped.X - queueBounds.X, layout.Anchor.Y + clamped.Y - queueBounds.Y));
            foreach (var cat in cats.Where(cat => !layout.IsJoined(cat.CatId)))
            {
                Point foot = layout.PositionOf(cat.CatId);
                Rectangle bounds = BoundsAt(foot);
                Rectangle placed = Placement.Clamp(bounds, areas);
                layout.MoveCat(cat.CatId, new Point(foot.X + placed.X - bounds.X, foot.Y + placed.Y - bounds.Y));
            }
        }

        private void ApplyLayout()
        {
            foreach (var cat in cats)
            {
                bool active = daily.ActiveCat == cat.CatId;
                Point foot = active ? daily.VisualPosition : layout.PositionOf(cat.CatId);
                if (drag != null && drag.GrabbedRoutine && !drag.Started && object.ReferenceEquals(cat, drag.Window))
                    foot = drag.CatOrigin;
                cat.Place(foot, layout.Width, active, IsLifted(cat));
            }
        }

        private bool IsLifted(CatWindow cat)
        {
            return drag != null && drag.Started && !drag.Group && object.ReferenceEquals(cat, drag.Window);
        }

        private Point GetBowlPosition()
        {
            Rectangle queue = QueueBounds();
            Rectangle area = Screen.FromRectangle(queue).WorkingArea;
            int margin = Math.Max(30, (int)Math.Round(layout.Width * .4));
            int gap = Math.Max(8, layout.Width / 30);
            int radius = Math.Max(12, (int)Math.Round(layout.Width * .07));
            int x = queue.Right + gap + radius;
            int y = layout.Anchor.Y;
            if (x + margin > area.Right) x = queue.Left - gap - radius;
            if (x - margin < area.Left)
            {
                x = queue.Left + queue.Width / 2;
                y = queue.Top - gap;
            }
            x = Math.Max(area.Left + margin, Math.Min(x, area.Right - margin));
            int headroom = SpriteRenderer.CatFrameSize(layout.Width).Height;
            y = Math.Max(area.Top + headroom, Math.Min(y, area.Bottom - Math.Max(10, layout.Width / 100)));
            return new Point(x, y);
        }

        private void StopRoutine()
        {
            if (daily.ActiveCat >= 0) daily.Interrupt();
            bowl.Hide();
        }

        private void MoveToPrimary()
        {
            CancelDrag();
            StopRoutine();
            var area = Screen.PrimaryScreen.WorkingArea;
            Point offset = SpriteRenderer.CatFootOffset(layout.Width);
            layout.MoveQueue(new Point(area.Right - QueueBounds().Width - 24 + offset.X, area.Bottom - 12 - (SpriteRenderer.CatFrameSize(layout.Width).Height - offset.Y)));
            ClampLayout(new[] { area });
            ShowPet();
            SaveSettings();
        }

        private void DisplayChanged(object sender, EventArgs args)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)delegate { CancelDrag(); StopRoutine(); FitToScreen(); SaveSettings(); }); }
            catch (InvalidOperationException) { }
        }

        private void ChoosePlayers()
        {
            using (var dialog = new PlayerDialog(audio, settings.Players))
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    settings.Players = dialog.PlayerList;
                    audio.Selected = PlayerNames.Parse(settings.Players);
                    audio.Refresh();
                    motion.Update(0, 1);
                    SaveSettings();
                }
        }

        private void RefreshAutoStart()
        {
            try
            {
                autoStart.Checked = startup.IsEnabled;
            }
            catch (Exception exception)
            {
                if (!(exception is UnauthorizedAccessException) && !(exception is System.Security.SecurityException) && !(exception is IOException)) throw;
                autoStart.Text = "开机自启动（无法访问启动项）";
                autoStart.Enabled = false;
            }
        }

        private void ToggleAutoStart()
        {
            try
            {
                startup.SetEnabled(!startup.IsEnabled, Application.ExecutablePath);
                autoStart.Checked = startup.IsEnabled;
            }
            catch (Exception exception)
            {
                if (!(exception is UnauthorizedAccessException) && !(exception is System.Security.SecurityException) && !(exception is IOException)) throw;
                tray.ShowBalloonTip(5000, "启动项设置未保存", "Windows 未允许修改当前用户的启动项。", ToolTipIcon.Info);
            }
        }

        private void AddCursorChoice(string label, string theme)
        {
            var item = new ToolStripMenuItem(label, null, delegate { SelectCursor(theme, true); }) { Tag = theme };
            cursorMenu.DropDownItems.Add(item);
        }

        private void SelectCursor(string theme, bool save)
        {
            try
            {
                cursorSession.Select(theme);
                settings.CursorTheme = theme;
                if (save) SaveSettings();
            }
            catch (Exception exception)
            {
                if (!CursorSession.IsCursorError(exception)) throw;
                theme = "off";
                if (save && settings.CursorTheme != "off")
                {
                    settings.CursorTheme = "off";
                    SaveSettings();
                }
                tray.ShowBalloonTip(5000, "无法切换全局光标", "Windows 未能完成光标操作。请在鼠标设置中检查当前方案。", ToolTipIcon.Info);
            }
            foreach (var item in cursorMenu.DropDownItems.OfType<ToolStripMenuItem>())
                item.Checked = (string)item.Tag == theme;
        }

        private void SaveSettings()
        {
            CatLayout confirmed = drag == null ? layout : drag.Snapshot;
            confirmed.WriteTo(settings);
            settings.X = confirmed.Anchor.X - (int)Math.Round(confirmed.Width * 393.0 / 1774);
            settings.Y = confirmed.Anchor.Y - SpriteRenderer.CatFootOffset(confirmed.Width).Y;
            try { settings.Save(settingsPath); }
            catch (Exception exception)
            {
                if (!(exception is IOException) && !(exception is UnauthorizedAccessException)) throw;
                if (!saveWarningShown)
                {
                    tray.ShowBalloonTip(5000, "设置暂未保存", "请将 EXE 放到可写文件夹，位置与大小会保存在程序旁的 XML 文件中。", ToolTipIcon.Info);
                    saveWarningShown = true;
                }
            }
        }

        private void CatMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var cat = (CatWindow)sender;
            BeginCatDrag(cat, cat.PointToScreen(e.Location), (ModifierKeys & Keys.Shift) != 0);
        }

        private void BeginCatDrag(CatWindow cat, Point pointer, bool shift)
        {
            CancelDrag();
            bool group = shift && layout.IsJoined(cat.CatId);
            bool grabbedRoutine = daily.ActiveCat == cat.CatId && !group;
            bool interruptedRoutine = grabbedRoutine || (group && daily.ActiveCat >= 0);
            Point origin = grabbedRoutine ? daily.VisualPosition : layout.PositionOf(cat.CatId);
            if (grabbedRoutine || group) StopRoutine();
            drag = new CatDrag
            {
                Window = cat,
                Origin = pointer,
                CatOrigin = origin,
                Snapshot = layout.Copy(),
                Group = group,
                GrabbedRoutine = grabbedRoutine,
                InterruptedRoutine = interruptedRoutine
            };
            cat.Capture = true;
            if (grabbedRoutine) { ApplyLayout(); TickAnimation(); }
        }

        private void CatMouseMove(object sender, MouseEventArgs e)
        {
            if (drag == null || !object.ReferenceEquals(sender, drag.Window)) return;
            UpdateDrag(drag.Window.PointToScreen(e.Location));
        }

        private void UpdateDrag(Point pointer)
        {
            if (drag == null) return;
            int dx = pointer.X - drag.Origin.X;
            int dy = pointer.Y - drag.Origin.Y;
            if (!drag.Started)
            {
                Size threshold = SystemInformation.DragSize;
                if (Math.Abs(dx) < threshold.Width / 2 && Math.Abs(dy) < threshold.Height / 2) return;
                drag.Started = true;
                if (!drag.Group) layout.Detach(drag.Window.CatId);
            }
            if (drag.Group)
                layout.MoveQueue(new Point(drag.Snapshot.Anchor.X + dx, drag.Snapshot.Anchor.Y + dy));
            else
            {
                Point foot = LiftRenderer.FootOffset(layout.Width);
                Point grip = LiftRenderer.GripOffset(drag.Window.CatId, layout.Width);
                var position = new Point(pointer.X + foot.X - grip.X, pointer.Y + foot.Y - grip.Y);
                layout.MoveCat(drag.Window.CatId, position);
                drag.CanReturn = layout.CanJoin(position);
            }
            ApplyLayout();
            TickAnimation();
        }

        private void CatMouseUp(object sender, MouseEventArgs e)
        {
            var cat = (CatWindow)sender;
            if (e.Button == MouseButtons.Left && drag != null && object.ReferenceEquals(cat, drag.Window))
            {
                UpdateDrag(cat.PointToScreen(e.Location));
                FinishDrag();
            }
            if (e.Button == MouseButtons.Right) menu.Show(cat, e.Location);
        }

        private void FinishDrag()
        {
            if (drag == null) return;
            CatDrag completed = drag;
            drag = null;
            if (completed.InterruptedRoutine) daily.Interrupt();
            completed.Window.Capture = false;
            preview.Hide();
            if (!completed.Started) { ApplyLayout(); TickAnimation(); return; }
            if (!completed.Group && completed.CanReturn) layout.Join(completed.Window.CatId);
            FitToScreen();
            SaveSettings();
            TickAnimation();
        }

        private void CancelDrag()
        {
            if (drag == null) return;
            CatDrag cancelled = drag;
            drag = null;
            if (cancelled.InterruptedRoutine) daily.Interrupt();
            layout.Restore(cancelled.Snapshot);
            cancelled.Window.Capture = false;
            preview.Hide();
            ApplyLayout();
            if (Visible) TickAnimation();
        }

        private void CatCaptureChanged(object sender, EventArgs e)
        {
            if (drag != null && object.ReferenceEquals(sender, drag.Window) && !drag.Window.Capture) CancelDrag();
        }

        private void CatMouseWheel(object sender, MouseEventArgs e)
        {
            ResizePet((int)(layout.Width * Math.Pow(1.12, e.Delta / 120.0)));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            CancelDrag();
            cursorSession.Dispose();
            timer.Stop();
            SaveSettings();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            tray.Visible = false;
            tray.Icon.Dispose();
            tray.Dispose();
            menu.Dispose();
            timer.Dispose();
            audio.Dispose();
            foreach (var cat in cats.Skip(1)) cat.Close();
            preview.Dispose();
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                cursorSession.Dispose();
                if (dailyRenderer != null) dailyRenderer.Dispose();
                liftRenderer.Dispose();
                bowl.Dispose();
                if (cats != null)
                    foreach (var cat in cats.Skip(1)) cat.Dispose();
                preview.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
