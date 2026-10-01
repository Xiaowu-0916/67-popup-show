// Popup67 - the soundtrack: plays while the popup windows are on screen and
// fades out as soon as they are closed. Uses the WPF MediaPlayer (an OS
// component of the .NET Framework), so no external player is started and no
// browser window is involved.

using System;
using System.IO;
using System.Windows.Media;

namespace Popup67
{
    internal sealed class SoundTrack : IDisposable
    {
        private readonly string path;
        private readonly double startSeconds;
        private readonly double volume;
        private readonly RunLog log;

        private MediaPlayer player;
        private System.Windows.Forms.Timer fade;
        private bool opened;
        private bool closed;

        public SoundTrack(string path, double startSeconds, double volume, RunLog log)
        {
            this.path = path;
            this.startSeconds = startSeconds;
            this.volume = volume;
            this.log = log;
            Status = "idle";
        }

        public string Status { get; private set; }

        public string PositionReport
        {
            get
            {
                try
                {
                    return player == null ? "n/a" : Util.Fmt("{0:0.0}s", player.Position.TotalSeconds);
                }
                catch
                {
                    return "n/a";
                }
            }
        }

        public bool Available
        {
            get { return !string.IsNullOrEmpty(path) && File.Exists(path); }
        }

        public bool IsOpen
        {
            get { return opened; }
        }

        // Opens (and therefore buffers) the clip before the show starts.
        public void Prepare()
        {
            if (!Available)
            {
                Status = "missing";
                log.Write("audio: file not found - " + path);
                return;
            }

            try
            {
                player = new MediaPlayer();
                player.MediaOpened += OnOpened;
                player.MediaFailed += OnFailed;
                player.Volume = volume;
                player.Open(new Uri(path, UriKind.Absolute));
                Status = "opening";
                log.Write("audio: opening " + path);
            }
            catch (Exception ex)
            {
                Status = "error";
                log.Write("audio: " + ex.Message);
            }
        }

        private void OnOpened(object sender, EventArgs e)
        {
            opened = true;
            double total = player.NaturalDuration.HasTimeSpan
                ? player.NaturalDuration.TimeSpan.TotalSeconds
                : 0.0;
            Status = Util.Fmt("opened ({0:0.0}s)", total);
            log.Write("audio: " + Status);

            try
            {
                if (startSeconds > 0.05 && total > startSeconds + 0.5)
                {
                    player.Position = TimeSpan.FromSeconds(startSeconds);
                    player.Volume = volume;
                    log.Write(Util.Fmt("audio: cued at {0:0.0}s", startSeconds));
                }
            }
            catch (Exception ex)
            {
                log.Write("audio: seek failed - " + ex.Message);
            }
        }

        private void OnFailed(object sender, ExceptionEventArgs e)
        {
            Status = "failed";
            log.Write("audio: playback failed - " +
                (e.ErrorException == null ? "unknown" : e.ErrorException.Message));
        }

        public void Play()
        {
            if (player == null)
            {
                return;
            }
            try
            {
                player.Volume = volume;
                player.Play();
                log.Write(Util.Fmt("audio: playing at volume {0:0.00}", volume));
            }
            catch (Exception ex)
            {
                log.Write("audio: play failed - " + ex.Message);
            }
        }

        // Fades the clip out and closes it - called right after the popups close.
        public void StopFade(double milliseconds)
        {
            if (player == null)
            {
                return;
            }

            log.Write("audio: stop requested at " + PositionReport);

            DateTime start = DateTime.Now;
            fade = new System.Windows.Forms.Timer();
            fade.Interval = 20;
            fade.Tick += delegate
            {
                double p = (DateTime.Now - start).TotalMilliseconds / Math.Max(50.0, milliseconds);
                if (p > 1.0)
                {
                    p = 1.0;
                }

                try
                {
                    player.Volume = volume * (1.0 - p);
                }
                catch
                {
                }

                if (p >= 1.0)
                {
                    fade.Stop();
                    fade.Dispose();
                    fade = null;
                    Shutdown();
                }
            };
            fade.Start();
        }

        private void Shutdown()
        {
            if (closed)
            {
                return;
            }
            closed = true;

            try
            {
                log.Write("audio: stopped at " + PositionReport);
                player.Stop();
                player.Close();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (fade != null)
            {
                fade.Stop();
                fade.Dispose();
                fade = null;
            }
            Shutdown();
            player = null;
        }
    }
}
