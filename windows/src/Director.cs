// Popup67 - runs the show: burst -> hold -> close -> giant finale.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Popup67
{
    internal sealed class Director : Form
    {
        private readonly Options opt;
        private readonly Random rng;
        private readonly Rectangle area;
        private readonly Rectangle screenArea;
        private readonly RunLog log;
        private readonly Palette[] deck;
        private readonly SoundTrack track;
        private readonly List<SpamWindow> windows = new List<SpamWindow>();
        private readonly List<SpamWindow> closeOrder = new List<SpamWindow>();

        private System.Windows.Forms.Timer burstTimer;
        private System.Windows.Forms.Timer closeTimer;
        private System.Windows.Forms.Timer wobbleTimer;
        private System.Windows.Forms.Timer hueTimer;
        private System.Windows.Forms.Timer popTimer;
        private GiantForm giant;
        private FxOverlay fx;
        private FxOverlay fxFinale;

        private int alive;
        private int burstIndex;
        private int closeIndex;
        private double huePhase;
        private int hueCursor;
        private bool aborted;
        private bool finaleScheduled;
        private bool exiting;

        public Director(Options opt)
        {
            this.opt = opt;
            this.rng = opt.Seed == 0 ? new Random() : new Random(opt.Seed);
            this.area = Display.LayoutArea(opt);
            this.screenArea = Display.FullScreen(opt);
            this.log = new RunLog(opt.LogPath);
            this.deck = Palette.Deck(this.rng);
            this.track = opt.Audio
                ? new SoundTrack(ResolveAudio(opt), opt.AudioStartSeconds, opt.Volume, this.log)
                : null;
            if (this.track != null)
            {
                this.track.Prepare();
            }

            Text = "67";
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            Size = new Size(1, 1);
            Opacity = 0.0;
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    AbortAll("escape (host)");
                }
            };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        private static string ResolveAudio(Options opt)
        {
            string wanted = string.IsNullOrEmpty(opt.AudioPath) ? @"audio\67.m4a" : opt.AudioPath;
            try
            {
                if (Path.IsPathRooted(wanted))
                {
                    return wanted;
                }

                string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
                string beside = Path.Combine(exeDir, wanted);
                if (File.Exists(beside))
                {
                    return beside;
                }

                return Path.GetFullPath(wanted);
            }
            catch
            {
                return wanted;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            log.Write(Util.Fmt(
                "host ready (build {5}, dpi {6:0} x{7:0.##}, awareness {8}); " +
                "layout {0}x{1} at {2},{3}; full screen {9}x{10}; target coverage {4:0}%",
                area.Width,
                area.Height,
                area.Left,
                area.Top,
                opt.Coverage * 100.0,
                BuildInfo.Stamp,
                Dpi.DpiValue,
                Dpi.Scale,
                Dpi.Method,
                screenArea.Width,
                screenArea.Height));
            Once(opt.Delay, delegate { StartBurst(); });
        }

        private void Once(double seconds, EventHandler action)
        {
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = Math.Max(1, (int)Math.Round(seconds * 1000.0));
            timer.Tick += delegate(object sender, EventArgs e)
            {
                timer.Stop();
                timer.Dispose();
                if (!aborted)
                {
                    action(sender, e);
                }
            };
            timer.Start();
        }

        private void StartBurst()
        {
            LayoutStats stats;
            List<Rectangle> rects = LayoutBuilder.Build(area, opt, rng, out stats);
            log.Write(Util.Fmt(
                "layout: {0} windows, union coverage {1:0.0}% (target {2:0}%)",
                stats.Windows,
                stats.Coverage * 100.0,
                opt.Coverage * 100.0));

            for (int i = 0; i < rects.Count; i++)
            {
                SpamWindow window = WindowFactory.Create(rects[i], rng, deck[i % deck.Length], this);
                window.Form.FormClosed += OnPopupClosed;
                windows.Add(window);
            }
            alive = windows.Count;

            if (opt.Wobble)
            {
                PrepareWobble();
            }

            StartHueCycle();
            StartPopAnimation();

            if (opt.Fx)
            {
                fx = new FxOverlay(screenArea, false, rng, log);
                fx.EscapeAction = delegate { AbortAll("escape (fx)"); };
                fx.Show();
                log.Write("fx: burst overlay on");
            }

            if (track != null)
            {
                track.Play();
            }

            burstTimer = new System.Windows.Forms.Timer();
            burstTimer.Interval = 18;
            burstTimer.Tick += BurstTick;
            burstTimer.Start();
            log.Write("burst started");
        }

        private void BurstTick(object sender, EventArgs e)
        {
            int batch = 1 + rng.Next(4);
            for (int i = 0; i < batch && burstIndex < windows.Count; i++)
            {
                SpamWindow window = windows[burstIndex];
                burstIndex++;
                try
                {
                    window.Form.Show();
                    window.PopStart = DateTime.Now;
                    window.Popping = true;
                    if (window.Canvas != null)
                    {
                        window.Canvas.ExtraScale = 0.35f;
                    }
                }
                catch
                {
                    alive--;
                    log.Write(Util.Fmt("warning: window {0} could not be shown", burstIndex));
                }
            }

            if (burstIndex < windows.Count)
            {
                return;
            }

            burstTimer.Stop();
            burstTimer.Dispose();
            burstTimer = null;
            log.Write(Util.Fmt("all {0} windows visible; holding {1:0.0}s", windows.Count, opt.Hold));
            Once(opt.Hold, delegate { StartClose(); });
        }

        private void StartHueCycle()
        {
            hueTimer = new System.Windows.Forms.Timer();
            hueTimer.Interval = 55;
            hueTimer.Tick += delegate
            {
                huePhase += 5.5;
                if (huePhase > 360.0)
                {
                    huePhase -= 360.0;
                }

                // repaint a rolling slice of the windows so the rainbow effect is
                // everywhere without repainting every canvas at once
                int slice = Math.Max(3, windows.Count / 5);
                for (int i = 0; i < slice; i++)
                {
                    if (windows.Count == 0)
                    {
                        break;
                    }
                    hueCursor = (hueCursor + 1) % windows.Count;
                    SpamWindow window = windows[hueCursor];
                    if (window.Canvas != null && !window.Form.IsDisposed && window.Form.Visible)
                    {
                        window.Canvas.HueShift = (float)(huePhase + window.HueOffset);
                    }
                }
            };
            hueTimer.Start();
        }

        private void StartPopAnimation()
        {
            popTimer = new System.Windows.Forms.Timer();
            popTimer.Interval = 25;
            popTimer.Tick += delegate
            {
                bool any = false;
                for (int i = 0; i < windows.Count; i++)
                {
                    SpamWindow window = windows[i];
                    if (!window.Popping || window.Canvas == null || window.Form.IsDisposed)
                    {
                        continue;
                    }

                    double p = (DateTime.Now - window.PopStart).TotalMilliseconds / 170.0;
                    if (p >= 1.0)
                    {
                        window.Popping = false;
                        window.Canvas.ExtraScale = 1f;
                        continue;
                    }

                    any = true;
                    double ease = 1.0 - Math.Pow(1.0 - p, 3.0);
                    float scale = (float)(0.35 + 0.65 * ease + 0.09 * Math.Sin(Math.PI * p));
                    window.Canvas.ExtraScale = scale;
                }

                if (!any && popTimer != null)
                {
                    popTimer.Stop();
                    popTimer.Dispose();
                    popTimer = null;
                }
            };
            popTimer.Start();
        }

        private void PrepareWobble()
        {
            for (int i = 0; i < windows.Count; i++)
            {
                if (rng.Next(100) < 36)
                {
                    windows[i].Wobble = true;
                    windows[i].Amp = 3f + (float)rng.NextDouble() * 7f;
                    windows[i].Speed = 1.4f + (float)rng.NextDouble() * 2.2f;
                    windows[i].Phase = (float)(rng.NextDouble() * Math.PI * 2.0);
                    windows[i].Vertical = rng.Next(100) < 40;
                }
            }

            DateTime start = DateTime.Now;
            wobbleTimer = new System.Windows.Forms.Timer();
            wobbleTimer.Interval = 33;
            wobbleTimer.Tick += delegate
            {
                double t = (DateTime.Now - start).TotalSeconds;
                for (int i = 0; i < windows.Count; i++)
                {
                    windows[i].ApplyKinetic(t);
                }
            };
            wobbleTimer.Start();
        }

        private void StopWobble()
        {
            if (wobbleTimer != null)
            {
                wobbleTimer.Stop();
                wobbleTimer.Dispose();
                wobbleTimer = null;
            }

            for (int i = 0; i < windows.Count; i++)
            {
                SpamWindow window = windows[i];
                if (window.Wobble && window.Form != null && !window.Form.IsDisposed)
                {
                    try
                    {
                        window.Form.Location = window.Origin;
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void StartClose()
        {
            StopWobble();
            closeOrder.Clear();
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                closeOrder.Add(windows[i]);
            }
            for (int i = 0; i + 1 < closeOrder.Count; i++)
            {
                if (rng.Next(100) < 28)
                {
                    SpamWindow tmp = closeOrder[i];
                    closeOrder[i] = closeOrder[i + 1];
                    closeOrder[i + 1] = tmp;
                }
            }

            closeTimer = new System.Windows.Forms.Timer();
            closeTimer.Interval = 30;
            closeTimer.Tick += CloseTick;
            closeTimer.Start();

            log.Write("closing phase started");
            Once((closeOrder.Count * 0.05) + 1.8, delegate { ForceFinish(); });
        }

        private void CloseTick(object sender, EventArgs e)
        {
            while (closeIndex < closeOrder.Count &&
                   (closeOrder[closeIndex].Form == null || closeOrder[closeIndex].Form.IsDisposed))
            {
                closeIndex++;
            }

            if (closeIndex >= closeOrder.Count)
            {
                closeTimer.Stop();
                closeTimer.Dispose();
                closeTimer = null;
                log.Write("close scheduling finished");
                return;
            }

            AnimateClose(closeOrder[closeIndex].Form);
            closeIndex++;
            closeTimer.Interval = 22 + rng.Next(26);
        }

        private void AnimateClose(Form form)
        {
            if (form == null || form.IsDisposed)
            {
                return;
            }

            Rectangle start = form.Bounds;
            DateTime startAt = DateTime.Now;
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 15;
            timer.Tick += delegate(object sender, EventArgs e)
            {
                if (form.IsDisposed)
                {
                    timer.Stop();
                    timer.Dispose();
                    return;
                }

                double p = (DateTime.Now - startAt).TotalMilliseconds / 150.0;
                if (p > 1.0)
                {
                    p = 1.0;
                }
                double k = 1.0 - 0.42 * (p * p);
                int w = Math.Max(24, (int)Math.Round(start.Width * k));
                int h = Math.Max(18, (int)Math.Round(start.Height * k));
                int cx = start.Left + start.Width / 2;
                int cy = start.Top + start.Height / 2;

                try
                {
                    form.Bounds = new Rectangle(cx - w / 2, cy - h / 2, w, h);
                    form.Opacity = 1.0 - 0.96 * p;
                }
                catch
                {
                }

                if (p >= 1.0)
                {
                    timer.Stop();
                    timer.Dispose();
                    try
                    {
                        form.Close();
                    }
                    catch
                    {
                    }
                }
            };
            timer.Start();
        }

        private void OnPopupClosed(object sender, FormClosedEventArgs e)
        {
            if (aborted)
            {
                return;
            }
            alive--;
            if (finaleScheduled)
            {
                return;
            }
            if (alive <= 0)
            {
                finaleScheduled = true;
                log.Write("all popup windows closed");
                StopBurstFx();
                Once(0.22, delegate { ShowFinale(); });
            }
        }

        private void ForceFinish()
        {
            if (aborted || finaleScheduled)
            {
                return;
            }
            finaleScheduled = true;

            for (int i = 0; i < windows.Count; i++)
            {
                Form form = windows[i].Form;
                try
                {
                    if (form != null && !form.IsDisposed)
                    {
                        form.Close();
                        form.Dispose();
                    }
                }
                catch
                {
                }
            }

            log.Write("safety net: popups force closed");
            StopBurstFx();
            Once(0.2, delegate { ShowFinale(); });
        }

        private void StopBurstFx()
        {
            if (fx != null)
            {
                fx.FadeOutAndClose();
                fx = null;
            }
        }

        // The clip keeps playing until the giant window is closed by the user.
        private void StopSound()
        {
            if (track != null)
            {
                track.StopFade(500.0);
            }
        }

        private void ShowFinale()
        {
            if (aborted)
            {
                return;
            }

            Rectangle frame;
            if (opt.FinalFullscreen)
            {
                // true full screen, taskbar included
                frame = screenArea;
            }
            else
            {
                int w = Math.Min((int)Math.Round(area.Width * 0.84), area.Width - 20);
                int h = Math.Min((int)Math.Round(area.Height * 0.80), area.Height - 20);
                if (w < 240)
                {
                    w = Math.Max(120, area.Width - 20);
                }
                if (h < 180)
                {
                    h = Math.Max(90, area.Height - 20);
                }
                frame = new Rectangle(
                    area.Left + (area.Width - w) / 2,
                    area.Top + (area.Height - h) / 2,
                    w,
                    h);
            }

            giant = new GiantForm(opt, frame, FontPicker.Best(), log);
            giant.FormClosed += delegate
            {
                log.Write("finale closed");
                giant = null;
                StopSound();
                if (fxFinale != null)
                {
                    fxFinale.CloseNow();
                    fxFinale = null;
                }
                CloseHost();
            };

            log.Write(Util.Fmt(
                "finale shown at {0},{1} {2}x{3}; auto close {4}",
                frame.Left,
                frame.Top,
                frame.Width,
                frame.Height,
                opt.FinalSeconds > 0.0 ? Util.Fmt("{0:0}s", opt.FinalSeconds) : "disabled"));
            giant.Show();

            // created after the giant so the effects float above it; the layer is
            // click through and never takes focus, so the giant keeps the keys
            if (opt.Fx)
            {
                fxFinale = new FxOverlay(screenArea, true, rng, log);
                fxFinale.EscapeAction = delegate { AbortAll("escape (fx)"); };
                fxFinale.Show();
                log.Write("fx: finale overlay on");
            }
        }

        public void AbortAll(string reason)
        {
            if (aborted)
            {
                return;
            }
            aborted = true;
            log.Write("abort: " + reason);
            StopAllTimers();

            for (int i = 0; i < windows.Count; i++)
            {
                Form form = windows[i].Form;
                try
                {
                    if (form != null && !form.IsDisposed)
                    {
                        form.Close();
                        form.Dispose();
                    }
                }
                catch
                {
                }
            }

            try
            {
                if (giant != null && !giant.IsDisposed)
                {
                    giant.Close();
                }
                if (fxFinale != null)
                {
                    fxFinale.CloseNow();
                    fxFinale = null;
                }
                if (fx != null)
                {
                    fx.CloseNow();
                    fx = null;
                }
                if (track != null)
                {
                    track.Dispose();
                }
            }
            catch
            {
            }

            CloseHost();
        }

        private void StopAllTimers()
        {
            StopWobble();

            if (burstTimer != null)
            {
                burstTimer.Stop();
                burstTimer.Dispose();
                burstTimer = null;
            }
            if (closeTimer != null)
            {
                closeTimer.Stop();
                closeTimer.Dispose();
                closeTimer = null;
            }
            if (hueTimer != null)
            {
                hueTimer.Stop();
                hueTimer.Dispose();
                hueTimer = null;
            }
            if (popTimer != null)
            {
                popTimer.Stop();
                popTimer.Dispose();
                popTimer = null;
            }
        }

        private void CloseHost()
        {
            if (exiting)
            {
                return;
            }
            exiting = true;
            try
            {
                Close();
            }
            catch
            {
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopAllTimers();
            if (giant != null && !giant.IsDisposed)
            {
                try
                {
                    giant.Close();
                }
                catch
                {
                }
            }
            log.Write("exit");
            if (track != null)
            {
                track.Dispose();
            }
            if (fx != null)
            {
                fx.CloseNow();
                fx = null;
            }
            if (fxFinale != null)
            {
                fxFinale.CloseNow();
                fxFinale = null;
            }
            log.Dispose();
            base.OnFormClosed(e);
        }
    }
}
