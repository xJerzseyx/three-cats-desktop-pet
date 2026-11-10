using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ThreeCats
{
    sealed class PlayerDialog : Form
    {
        private readonly AudioMonitor audio;
        private readonly TextBox names;
        private readonly ListView sources;
        public string PlayerList { get; private set; }

        public PlayerDialog(AudioMonitor monitor, string current)
        {
            audio = monitor;
            Text = "三猫桌宠 · 音乐来源";
            ClientSize = new Size(570, 485);
            Font = new Font("Microsoft YaHei UI", 9);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            Controls.Add(new Label { Text = "允许跟随的播放器进程（每行一个，也可用逗号分隔）", AutoSize = true, Location = new Point(18, 16) });
            names = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Bounds = new Rectangle(18, 43, 534, 95), Text = string.Join(Environment.NewLine, PlayerNames.Parse(current).OrderBy(name => name).ToArray()) };
            Controls.Add(names);
            Controls.Add(new Label { Text = "默认 cloudmusic 为网易云，qqmusic 为 QQ 音乐。\n本地播放器可选择 EXE，或从下面的音频程序中添加。", AutoSize = true, Location = new Point(18, 148) });
            var browse = new Button { Text = "选择播放器 EXE…", Bounds = new Rectangle(18, 193, 170, 32) };
            browse.Click += delegate
            {
                using (var dialog = new OpenFileDialog { Filter = "播放器程序 (*.exe)|*.exe", Title = "选择本地音乐播放器" })
                    if (dialog.ShowDialog(this) == DialogResult.OK) Append(PlayerNames.Normalize(Path.GetFileName(dialog.FileName)));
            };
            Controls.Add(browse);
            var refresh = new Button { Text = "刷新音频程序", Bounds = new Rectangle(198, 193, 154, 32) };
            refresh.Click += delegate { RefreshSources(); };
            Controls.Add(refresh);
            var add = new Button { Text = "添加勾选程序", Bounds = new Rectangle(362, 193, 190, 32) };
            add.Click += delegate
            {
                foreach (ListViewItem item in sources.CheckedItems) Append((string)item.Tag);
            };
            Controls.Add(add);
            sources = new ListView { Bounds = new Rectangle(18, 237, 534, 145), View = View.Details, CheckBoxes = true, FullRowSelect = true, HideSelection = false };
            sources.Columns.Add("音频程序", 230);
            sources.Columns.Add("进程 ID", 100);
            sources.Columns.Add("当前音量", 165);
            Controls.Add(sources);
            Controls.Add(new Label { Text = "按程序来源筛选。播放器内的广告和语音也会触发动作。", AutoSize = true, Location = new Point(18, 394) });
            var save = new Button { Text = "保存", Bounds = new Rectangle(352, 438, 95, 32) };
            save.Click += delegate
            {
                var selected = PlayerNames.Parse(names.Text);
                if (selected.Count == 0)
                {
                    MessageBox.Show(this, "请至少填写一个播放器进程名。", "音乐来源", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                PlayerList = string.Join(",", selected.OrderBy(name => name).ToArray());
                DialogResult = DialogResult.OK;
            };
            Controls.Add(save);
            var cancel = new Button { Text = "取消", Bounds = new Rectangle(457, 438, 95, 32), DialogResult = DialogResult.Cancel };
            Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;
            Shown += delegate { RefreshSources(); };
        }

        private void Append(string name)
        {
            var selected = PlayerNames.Parse(names.Text);
            selected.Add(name);
            names.Text = string.Join(Environment.NewLine, selected.OrderBy(value => value).ToArray());
        }

        private void RefreshSources()
        {
            audio.Refresh();
            sources.Items.Clear();
            foreach (var source in audio.ReadSources().GroupBy(item => item.ProcessId).Select(group => group.OrderByDescending(item => item.Peak).First()))
            {
                var item = new ListViewItem(source.ProcessName + ".exe") { Tag = source.ProcessName };
                item.SubItems.Add(source.ProcessId.ToString());
                item.SubItems.Add((source.Peak * 100).ToString("F1") + "%");
                sources.Items.Add(item);
            }
        }
    }
}
