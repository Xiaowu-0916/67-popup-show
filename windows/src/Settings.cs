// Popup67 - the visual settings app (67-Settings.exe).
//
// Sliders for every timing option plus a live preview: the left half renders
// the real layout engine at miniature scale, the bottom strip draws the actual
// time line (delay -> burst -> hold -> close -> giant) so the duration of each
// phase is visible instead of guessed.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Popup67
{
    internal sealed class PreviewPanel : Control
    {
        private readonly Options opt;
        private readonly Random rng = new Random(20261001);

        public PreviewPanel(Options opt)
        {
            this.opt = opt;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Opaque,
                true);
            BackColor = Color.FromArgb(18, 20, 26);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle full = ClientRectangle;
            int timelineHeight = 108;
            Rectangle stage = new Rectangle(0, 0, full.Width, Math.Max(60, full.Height - timelineHeight));

            DrawStage(g, stage);
            DrawTimeline(g, new Rectangle(0, stage.Bottom, full.Width, full.Height - stage.Bottom));
        }

        private void DrawStage(Graphics g, Rectangle stage)
        {
            using (LinearGradientBrush bg = new LinearGradientBrush(
                new RectangleF(0, 0, stage.Width, stage.Height),
                Color.FromArgb(255, 40, 46, 62),
                Color.FromArgb(255, 12, 14, 20),
                70f))
            {
                g.FillRectangle(bg, stage);
            }

            Random random = new Random(7);
            LayoutStats stats;
            List<Rectangle> rects = LayoutBuilder.Build(stage, opt, random, out stats);

            for (int i = 0; i < rects.Count; i++)
            {
                Rectangle r = rects[i];
                Palette palette = new Palette(Palette.BackAt(i));
                int bar = Math.Max(6, r.Height / 9);

                using (SolidBrush brush = new SolidBrush(palette.Back))
                {
                    g.FillRectangle(brush, r);
                }
                using (SolidBrush brush = new SolidBrush(Util.Darken(palette.Back, 0.45)))
                {
                    g.FillRectangle(brush, new Rectangle(r.Left, r.Top, r.Width, bar));
                }
                using (Pen pen = new Pen(Color.FromArgb(210, 255, 255, 255), 1f))
                {
                    g.DrawRectangle(pen, r.Left, r.Top, r.Width - 1, r.Height - 1);
                }

                Rectangle inner = new Rectangle(r.Left + 2, r.Top + bar, r.Width - 4, r.Height - bar - 2);
                FontChoice font = FontPicker.Next(random);
                TextArt.Draw(
                    g,
                    "67",
                    inner,
                    font.Family,
                    font.Style,
                    (ArtStyle)(i % 5),
                    palette.Ink,
                    palette.Ink2,
                    1f);
            }

            string head = Util.Fmt(
                "{0} 个窗口   ·   覆盖桌面 {1:0.0}%   ·   窗口尺寸 x{2:0.0}",
                stats.Windows,
                stats.Coverage * 100.0,
                opt.WindowScale);

            using (SolidBrush strip = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
            {
                g.FillRectangle(strip, 0, 0, stage.Width, 30);
            }
            using (Font f = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold))
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
            using (SolidBrush ink = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
            {
                g.DrawString(head, f, shadow, 9f, 9f);
                g.DrawString(head, f, ink, 8f, 8f);
            }
        }

        private void DrawTimeline(Graphics g, Rectangle bar)
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(26, 28, 36)))
            {
                g.FillRectangle(bg, bar);
            }
            using (Pen top = new Pen(Color.FromArgb(70, 255, 255, 255), 1f))
            {
                g.DrawLine(top, bar.Left, bar.Top, bar.Right, bar.Top);
            }

            // weights keep the (possibly 300s long) finale readable
            double[] weights =
            {
                1.0,
                2.0,
                Math.Max(0.6, opt.Hold),
                1.6,
                Math.Max(0.8, Math.Min(8.0, opt.FinalSeconds / 12.0))
            };
            string[] names = { "延迟", "弹出", "停留", "关闭", "大 67" };
            double[] seconds =
            {
                opt.Delay,
                0.6,
                opt.Hold,
                1.0,
                opt.FinalSeconds
            };
            Color[] colors =
            {
                Color.FromArgb(255, 80, 90, 110),
                Color.FromArgb(255, 255, 149, 0),
                Color.FromArgb(255, 48, 209, 88),
                Color.FromArgb(255, 255, 55, 95),
                Color.FromArgb(255, 10, 132, 255)
            };

            double total = 0.0;
            for (int i = 0; i < weights.Length; i++)
            {
                total += weights[i];
            }

            int x = bar.Left + 10;
            int usable = bar.Width - 20;
            int y = bar.Top + 34;
            int h = 22;
            int burstLeft = 0;
            int endRight = 0;

            for (int i = 0; i < weights.Length; i++)
            {
                int w = (int)Math.Round(usable * (weights[i] / total));
                Rectangle seg = new Rectangle(x, y, Math.Max(8, w - 2), h);
                using (SolidBrush brush = new SolidBrush(colors[i]))
                {
                    g.FillRectangle(brush, seg);
                }

                string label;
                if (i == 4 && opt.FinalSeconds <= 0.0)
                {
                    label = names[i] + " 手动关";
                }
                else
                {
                    label = Util.Fmt("{0} {1:0.0}s", names[i], seconds[i]);
                }

                bool narrow = seg.Width < 78;
                using (Font f = new Font("Microsoft YaHei UI", narrow ? 7.5f : 8f))
                using (SolidBrush ink = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                {
                    if (narrow)
                    {
                        string only = names[i];
                        SizeF size = g.MeasureString(only, f);
                        g.DrawString(only, f, ink, seg.Left + (seg.Width - size.Width) / 2f, seg.Top - 15f);
                    }
                    else
                    {
                        g.DrawString(label, f, ink, seg.Left + 3, seg.Top + 4);
                    }
                }

                if (i == 1)
                {
                    burstLeft = seg.Left;
                }
                endRight = seg.Right;
                x += w;
            }

            using (Font f = new Font("Microsoft YaHei UI", 8f))
            using (SolidBrush ink = new SolidBrush(Color.FromArgb(210, 255, 255, 255)))
            {
                g.DrawString("总时长（弹窗阶段）", f, ink, bar.Left + 10, bar.Top + 10);
                string totalText = Util.Fmt(
                    "≈ {0:0.0} 秒后进入大 67    ·    音乐：{1} 秒开始播，关闭大 67 时停止",
                    opt.Delay + 0.6 + opt.Hold + 1.0,
                    opt.AudioStartSeconds);
                g.DrawString(totalText, f, ink, bar.Left + 10, bar.Top + 66);
            }

            using (Pen marker = new Pen(Color.FromArgb(255, 255, 214, 10), 2f))
            {
                g.DrawLine(marker, burstLeft, y - 8, burstLeft, y + h + 8);
            }
            using (Pen marker = new Pen(Color.FromArgb(255, 52, 199, 89), 2f))
            {
                g.DrawLine(marker, endRight, y - 8, endRight, y + h + 8);
            }
        }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly Options opt;
        private readonly string configPath;
        private readonly string exePath;

        private TrackBar coverageBar;
        private Label coverageValue;
        private NumericUpDown holdBox;
        private NumericUpDown maxBox;
        private NumericUpDown scaleBox;
        private NumericUpDown finalBox;
        private TrackBar volumeBar;
        private Label volumeValue;
        private NumericUpDown audioStartBox;
        private CheckBox fxCheck;
        private CheckBox wobbleCheck;
        private CheckBox audioCheck;
        private CheckBox fullscreenCheck;
        private Label status;
        private PreviewPanel preview;
        private bool loading;

        public SettingsForm()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            configPath = Path.Combine(dir, "67.ini");
            exePath = Path.Combine(dir, "67.exe");

            opt = new Options();
            opt.LoadFile(configPath);

            Text = "67 设置 - 时间 / 数量 / 音量";
            ClientSize = new Size(900, 580);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(28, 30, 38);
            ForeColor = Color.FromArgb(235, 238, 245);
            Font = new Font("Microsoft YaHei UI", 9f);

            BuildUi();
            LoadValues();
        }

        private void BuildUi()
        {
            Label title = new Label();
            title.Text = "67  播放设置";
            title.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(255, 226, 232, 240);
            title.AutoSize = true;
            title.Location = new Point(16, 12);
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = "调整后点「保存」，双击文件夹里的 67.exe 生效。右侧为实时预览与时间轴。  build " + BuildInfo.Stamp;
            sub.ForeColor = Color.FromArgb(200, 170, 178, 195);
            sub.AutoSize = true;
            sub.Location = new Point(18, 44);
            Controls.Add(sub);

            int y = 86;
            AddTrackRow("覆盖屏幕比例", 40, 95, 5, ref y, ref coverageBar, ref coverageValue, "%");
            AddNumberRow("弹窗停留时间", 0.5m, 60m, 0.5m, 1, ref y, ref holdBox, "秒");
            AddNumberRow("窗口数量上限", 5m, 120m, 5m, 0, ref y, ref maxBox, "个");
            AddNumberRow("窗口尺寸倍率", 0.5m, 2.5m, 0.1m, 1, ref y, ref scaleBox, "倍");
            AddNumberRow("大 67 停留时间", 0m, 3600m, 30m, 0, ref y, ref finalBox, "秒 (0=手动关)");
            AddTrackRow("音量", 0, 100, 5, ref y, ref volumeBar, ref volumeValue, "%");
            AddNumberRow("音乐起始位置", 0m, 300m, 1m, 1, ref y, ref audioStartBox, "秒");

            y += 12;
            fxCheck = AddCheck("全屏特效", 18, y);
            wobbleCheck = AddCheck("窗口漂移", 158, y);
            y += 32;
            audioCheck = AddCheck("播放音乐", 18, y);
            fullscreenCheck = AddCheck("大 67 全屏", 158, y);

            Button save = AddButton("保存", 18, y + 44, 96, 34, SaveOnly);
            AddButton("保存并试一次", 122, y + 44, 150, 34, SaveAndRun);
            AddButton("恢复默认", 280, y + 44, 96, 34, RestoreDefaults);
            save.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);

            status = new Label();
            status.AutoSize = false;
            status.Size = new Size(360, 24);
            status.Location = new Point(18, y + 86);
            status.ForeColor = Color.FromArgb(220, 120, 210, 150);
            Controls.Add(status);

            preview = new PreviewPanel(opt);
            preview.Location = new Point(392, 86);
            preview.Size = new Size(492, ClientSize.Height - 104);
            Controls.Add(preview);

            coverageBar.ValueChanged += delegate { ReadUi(); };
            volumeBar.ValueChanged += delegate { ReadUi(); };
            holdBox.ValueChanged += delegate { ReadUi(); };
            maxBox.ValueChanged += delegate { ReadUi(); };
            scaleBox.ValueChanged += delegate { ReadUi(); };
            finalBox.ValueChanged += delegate { ReadUi(); };
            audioStartBox.ValueChanged += delegate { ReadUi(); };
            fxCheck.CheckedChanged += delegate { ReadUi(); };
            wobbleCheck.CheckedChanged += delegate { ReadUi(); };
            audioCheck.CheckedChanged += delegate { ReadUi(); };
            fullscreenCheck.CheckedChanged += delegate { ReadUi(); };
        }

        private void AddTrackRow(
            string caption,
            int min,
            int max,
            int step,
            ref int y,
            ref TrackBar bar,
            ref Label value,
            string unit)
        {
            Label label = new Label();
            label.Text = caption;
            label.AutoSize = true;
            label.Location = new Point(18, y + 6);
            Controls.Add(label);

            TrackBar track = new TrackBar();
            track.Minimum = min;
            track.Maximum = max;
            track.TickFrequency = step * 2;
            track.SmallChange = step;
            track.LargeChange = step * 2;
            track.AutoSize = false;
            track.Size = new Size(196, 32);
            track.Location = new Point(150, y);
            Controls.Add(track);

            Label read = new Label();
            read.AutoSize = true;
            read.Location = new Point(354, y + 6);
            read.ForeColor = Color.FromArgb(255, 255, 214, 10);
            Controls.Add(read);

            bar = track;
            value = read;
            value.Tag = unit;
            y += 44;
        }

        private void AddNumberRow(
            string caption,
            decimal min,
            decimal max,
            decimal step,
            int decimals,
            ref int y,
            ref NumericUpDown box,
            string unit)
        {
            Label label = new Label();
            label.Text = caption;
            label.AutoSize = true;
            label.Location = new Point(18, y + 6);
            Controls.Add(label);

            NumericUpDown up = new NumericUpDown();
            up.Minimum = min;
            up.Maximum = max;
            up.Increment = step;
            up.DecimalPlaces = decimals;
            up.Size = new Size(90, 26);
            up.Location = new Point(150, y + 2);
            up.BackColor = Color.FromArgb(44, 47, 58);
            up.ForeColor = Color.FromArgb(240, 243, 250);
            up.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(up);

            Label read = new Label();
            read.Text = unit;
            read.AutoSize = true;
            read.Location = new Point(248, y + 6);
            read.ForeColor = Color.FromArgb(190, 170, 178, 195);
            Controls.Add(read);

            box = up;
            y += 44;
        }

        private CheckBox AddCheck(string caption, int x, int y)
        {
            CheckBox check = new CheckBox();
            check.Text = caption;
            check.AutoSize = true;
            check.Location = new Point(x, y);
            check.ForeColor = Color.FromArgb(235, 238, 245);
            Controls.Add(check);
            return check;
        }

        private Button AddButton(string caption, int x, int y, int w, int h, EventHandler handler)
        {
            Button button = new Button();
            button.Text = caption;
            button.Size = new Size(w, h);
            button.Location = new Point(x, y);
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Color.FromArgb(52, 56, 70);
            button.ForeColor = Color.FromArgb(240, 243, 250);
            button.FlatAppearance.BorderColor = Color.FromArgb(90, 96, 116);
            button.Click += handler;
            Controls.Add(button);
            return button;
        }

        private void LoadValues()
        {
            loading = true;
            coverageBar.Value = Util.Clamp((int)Math.Round(opt.Coverage * 100.0), 40, 95);
            holdBox.Value = ClampDec((decimal)opt.Hold, holdBox.Minimum, holdBox.Maximum);
            maxBox.Value = ClampDec((decimal)opt.MaxWindows, maxBox.Minimum, maxBox.Maximum);
            scaleBox.Value = ClampDec((decimal)opt.WindowScale, scaleBox.Minimum, scaleBox.Maximum);
            finalBox.Value = ClampDec((decimal)opt.FinalSeconds, finalBox.Minimum, finalBox.Maximum);
            volumeBar.Value = Util.Clamp((int)Math.Round(opt.Volume * 100.0), 0, 100);
            audioStartBox.Value = ClampDec((decimal)opt.AudioStartSeconds, audioStartBox.Minimum, audioStartBox.Maximum);
            fxCheck.Checked = opt.Fx;
            wobbleCheck.Checked = opt.Wobble;
            audioCheck.Checked = opt.Audio;
            fullscreenCheck.Checked = opt.FinalFullscreen;
            loading = false;
            ReadUi();
        }

        private static decimal ClampDec(decimal value, decimal min, decimal max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        private void ReadUi()
        {
            if (loading)
            {
                return;
            }

            opt.Coverage = coverageBar.Value / 100.0;
            opt.Hold = (double)holdBox.Value;
            opt.MaxWindows = (int)maxBox.Value;
            opt.MinWindows = Math.Min(opt.MinWindows, opt.MaxWindows);
            opt.WindowScale = (double)scaleBox.Value;
            opt.FinalSeconds = (double)finalBox.Value;
            opt.Volume = volumeBar.Value / 100.0;
            opt.AudioStartSeconds = (double)audioStartBox.Value;
            opt.Fx = fxCheck.Checked;
            opt.Wobble = wobbleCheck.Checked;
            opt.Audio = audioCheck.Checked;
            opt.FinalFullscreen = fullscreenCheck.Checked;

            coverageValue.Text = coverageBar.Value + "%";
            volumeValue.Text = volumeBar.Value + "%";

            if (preview != null)
            {
                preview.Invalidate();
            }
        }

        private void SaveOnly(object sender, EventArgs e)
        {
            try
            {
                opt.SaveFile(configPath);
                status.Text = "已保存到 67.ini";
            }
            catch (Exception ex)
            {
                status.Text = "保存失败: " + ex.Message;
            }
        }

        private void SaveAndRun(object sender, EventArgs e)
        {
            SaveOnly(sender, e);
            try
            {
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = Path.GetDirectoryName(exePath) });
                    status.Text = "已保存并启动 67.exe（按 ESC 可随时中止）";
                }
                else
                {
                    status.Text = "找不到 67.exe，请与设置程序放在同一文件夹";
                }
            }
            catch (Exception ex)
            {
                status.Text = "启动失败: " + ex.Message;
            }
        }

        private void RestoreDefaults(object sender, EventArgs e)
        {
            Options fresh = new Options();
            opt.Coverage = fresh.Coverage;
            opt.Hold = fresh.Hold;
            opt.MaxWindows = fresh.MaxWindows;
            opt.WindowScale = fresh.WindowScale;
            opt.FinalSeconds = fresh.FinalSeconds;
            opt.Volume = fresh.Volume;
            opt.AudioStartSeconds = fresh.AudioStartSeconds;
            opt.Fx = fresh.Fx;
            opt.Wobble = fresh.Wobble;
            opt.Audio = fresh.Audio;
            opt.FinalFullscreen = fresh.FinalFullscreen;
            LoadValues();
            status.Text = "已恢复默认值（记得点保存）";
        }
    }

    internal static class SettingsProgram
    {
        [STAThread]
        private static void Main()
        {
            Dpi.Enable(true);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SettingsForm());
        }
    }
}
