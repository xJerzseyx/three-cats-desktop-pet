using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ThreeCats
{
    public sealed class PetForm : Form
    {
        private readonly Settings settings;
        private readonly string settingsPath;
        private readonly SpriteRenderer renderer = new SpriteRenderer();
        private readonly AudioMonitor audio = new AudioMonitor();
        private readonly MotionState motion = new MotionState();
        private readonly Timer timer = new Timer { Interval = 33 };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly ToolStripMenuItem visibility;
        private readonly ToolStripMenuItem status;
        private readonly ToolStripMenuItem autoStart;
        private readonly StartupRegistration startup = new StartupRegistration();
        private readonly double[] lookX = new double[3];
        private readonly double[] lookY = new double[3];
        private double lastTime;
        private double musicMix;
        private double nextStatus;
        private bool dragging;
        private Point dragOrigin;
        private Point windowOrigin;
        private bool saveWarningShown;
        internal static readonly uint ShowMessage = Native.RegisterWindowMessage("ThreeCatsDesktop.Show.v1");

        public PetForm(string configPath)
        {
            settingsPath = configPath;
            settings = Settings.Load(configPath);
            audio.Selected = PlayerNames.Parse(settings.Players);
            Text = "三猫桌宠";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(settings.Width, (int)Math.Round(settings.Width * SpriteRenderer.Aspect));
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = settings.X == int.MinValue || settings.Y == int.MinValue
                ? new Point(area.Right - Width - 24, area.Bottom - Height - 12)
                : new Point(settings.X, settings.Y);
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
            sizes.DropDownItems.Add("放大", null, delegate { ResizePet((int)(Width * 1.12)); });
            sizes.DropDownItems.Add("缩小", null, delegate { ResizePet((int)(Width / 1.12)); });
            menu.Items.Add(sizes);
            menu.Items.Add("音乐来源设置…", null, delegate { ChoosePlayers(); });
            autoStart = new ToolStripMenuItem("开机自启动", null, delegate { ToggleAutoStart(); });
            menu.Items.Add(autoStart);
            menu.Items.Add("移回主屏幕", null, delegate
            {
                var primary = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(primary.Right - Width - 24, primary.Bottom - Height - 12);
                FitToScreen();
                ShowPet();
                SaveSettings();
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("左键拖动 · 滚轮缩放", null, delegate { }) .Enabled = false;
            menu.Items.Add("退出程序", null, delegate { Close(); });
            tray = new NotifyIcon { Text = "三猫桌宠", Icon = MakeIcon(), ContextMenuStrip = menu, Visible = true };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ToggleVisibility(); };
            RefreshAutoStart();
            timer.Tick += delegate { TickAnimation(); };
            Shown += delegate { lastTime = clock.Elapsed.TotalSeconds; timer.Start(); };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x80000 | 0x80 | 0x08000000;
                return parameters;
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == ShowMessage) { ShowPet(); return; }
            if (message.Msg == 0x21) { message.Result = new IntPtr(3); return; }
            if (message.Msg == 0x02E0)
            {
                base.WndProc(ref message);
                FitToScreen();
                return;
            }
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

        private void TickAnimation()
        {
            double now = clock.Elapsed.TotalSeconds;
            double elapsed = Math.Max(0.001, Math.Min(1, now - lastTime));
            lastTime = now;
            // Hidden pets sleep. Reshowing refreshes the selected audio sessions.
            if (!Visible) return;
            motion.Update(audio.ReadPeak(), elapsed);
            musicMix += ((motion.Playing ? 1 : 0) - musicMix) * (1 - Math.Exp(-elapsed * 9));
            var cursor = Cursor.Position;
            double[] fractions = { 393.0 / 1774, 904.0 / 1774, 1441.0 / 1774 };
            for (int cat = 0; cat < 3; cat++)
            {
                double desiredX = Math.Max(-1, Math.Min(1, (cursor.X - Left - Width * fractions[cat]) / Math.Max(160.0, Width * 0.55)));
                double desiredY = Math.Max(-1, Math.Min(1, (cursor.Y - Top - Height * 0.29) / Math.Max(130.0, Height * 0.8)));
                lookX[cat] += (desiredX - lookX[cat]) * (1 - Math.Exp(-elapsed * 8));
                lookY[cat] += (desiredY - lookY[cat]) * (1 - Math.Exp(-elapsed * 8));
            }
            Native.Present(Handle, renderer.Render(Width, lookX, lookY, musicMix, motion.Energy, now), Location);
            if (now > nextStatus)
            {
                status.Text = !audio.Available ? (audio.Error ?? "等待音频设备") : motion.Playing ? "音乐播放中 · 同步摇摆" : "等待音乐 · 头部跟随鼠标";
                Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
                nextStatus = now + 1;
            }
        }

        public void ToggleVisibility()
        {
            if (Visible)
            {
                Hide();
                timer.Stop();
                audio.Dispose();
                visibility.Text = "显示桌宠";
            }
            else ShowPet();
        }

        public void ShowPet()
        {
            FitToScreen();
            audio.Refresh();
            motion.Update(0, 1);
            musicMix = 0;
            Show();
            visibility.Text = "隐藏桌宠";
            lastTime = clock.Elapsed.TotalSeconds;
            timer.Start();
            TickAnimation();
        }

        public void ResizePet(int requestedWidth)
        {
            var area = Screen.FromRectangle(Bounds).WorkingArea;
            int width = Math.Max(240, Math.Min(1600, requestedWidth));
            width = Math.Min(width, Math.Min(area.Width, (int)(area.Height / SpriteRenderer.Aspect)));
            int bottom = Bottom;
            int center = Left + Width / 2;
            Size = new Size(width, (int)Math.Round(width * SpriteRenderer.Aspect));
            Location = new Point(center - Width / 2, bottom - Height);
            FitToScreen();
            SaveSettings();
            if (Visible) TickAnimation();
        }

        private void FitToScreen()
        {
            var areas = Screen.AllScreens.Select(screen => screen.WorkingArea).ToArray();
            var clamped = Placement.Clamp(Bounds, areas);
            if (clamped.Width != Width || clamped.Height != Height)
            {
                int width = Math.Min(clamped.Width, (int)(clamped.Height / SpriteRenderer.Aspect));
                clamped.Size = new Size(width, (int)Math.Round(width * SpriteRenderer.Aspect));
            }
            Bounds = clamped;
        }

        private void DisplayChanged(object sender, EventArgs args)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)delegate { FitToScreen(); SaveSettings(); }); }
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

        private void SaveSettings()
        {
            settings.Width = Width;
            settings.X = Left;
            settings.Y = Top;
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            dragOrigin = Cursor.Position;
            windowOrigin = Location;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) Location = new Point(windowOrigin.X + Cursor.Position.X - dragOrigin.X, windowOrigin.Y + Cursor.Position.Y - dragOrigin.Y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging)
            {
                dragging = false;
                Capture = false;
                FitToScreen();
                SaveSettings();
            }
            if (e.Button == MouseButtons.Right) menu.Show(Cursor.Position);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && dragging) { dragging = false; FitToScreen(); SaveSettings(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ResizePet((int)(Width * Math.Pow(1.12, e.Delta / 120.0)));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            SaveSettings();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            tray.Visible = false;
            tray.Icon.Dispose();
            tray.Dispose();
            menu.Dispose();
            timer.Dispose();
            audio.Dispose();
            renderer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
