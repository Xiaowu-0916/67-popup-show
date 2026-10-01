// Popup67 - shared helpers, options and entry point.
// Windows native (WinForms/GDI+). No HTML, no browser, no network, no files.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Popup67
{
    internal static class Util
    {
        public static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }

        public static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }

        public static double ParseDouble(string s, double fallback)
        {
            double d;
            if (!string.IsNullOrEmpty(s) &&
                double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            {
                return d;
            }
            return fallback;
        }

        public static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        public static double Luminance(Color c)
        {
            return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        }

        public static Color Mix(Color a, Color b, double t)
        {
            t = Clamp(t, 0.0, 1.0);
            return Color.FromArgb(
                255,
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static Color Lighten(Color c, double t)
        {
            return Mix(c, Color.White, t);
        }

        public static Color Darken(Color c, double t)
        {
            return Mix(c, Color.Black, t);
        }

        public static string Fmt(string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }

        // Rotates a colour around the hue wheel - used for the rainbow cycling.
        public static Color HueShift(Color c, double degrees)
        {
            double h, s, l;
            ToHsl(c, out h, out s, out l);
            h = h + degrees / 360.0;
            h = h - Math.Floor(h);
            return FromHsl(h, s, l);
        }

        public static void ToHsl(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2.0;

            if (Math.Abs(max - min) < 0.0001)
            {
                h = 0.0;
                s = 0.0;
                return;
            }

            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

            if (max == r)
            {
                h = (g - b) / d + (g < b ? 6.0 : 0.0);
            }
            else if (max == g)
            {
                h = (b - r) / d + 2.0;
            }
            else
            {
                h = (r - g) / d + 4.0;
            }
            h = h / 6.0;
        }

        public static Color FromHsl(double h, double s, double l)
        {
            if (s <= 0.0001)
            {
                int grey = (int)Math.Round(Clamp(l, 0.0, 1.0) * 255.0);
                return Color.FromArgb(255, grey, grey, grey);
            }

            double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            double p = 2.0 * l - q;
            return Color.FromArgb(
                255,
                (int)Math.Round(HueToRgb(p, q, h + 1.0 / 3.0) * 255.0),
                (int)Math.Round(HueToRgb(p, q, h) * 255.0),
                (int)Math.Round(HueToRgb(p, q, h - 1.0 / 3.0) * 255.0));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0.0)
            {
                t += 1.0;
            }
            if (t > 1.0)
            {
                t -= 1.0;
            }
            if (t < 1.0 / 6.0)
            {
                return p + (q - p) * 6.0 * t;
            }
            if (t < 1.0 / 2.0)
            {
                return q;
            }
            if (t < 2.0 / 3.0)
            {
                return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
            }
            return p;
        }
    }

    internal sealed class RunLog : IDisposable
    {
        private readonly StreamWriter writer;
        private readonly DateTime start;

        public RunLog(string path)
        {
            start = DateTime.Now;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            try
            {
                string full = Path.GetFullPath(path);
                string dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                writer = new StreamWriter(full, false, new UTF8Encoding(false));
                writer.AutoFlush = true;
            }
            catch
            {
                writer = null;
            }
        }

        public void Write(string message)
        {
            if (writer == null)
            {
                return;
            }
            try
            {
                writer.WriteLine(Util.Fmt(
                    "[+{0:0.000}s] {1}",
                    (DateTime.Now - start).TotalSeconds,
                    message));
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (writer == null)
            {
                return;
            }
            try
            {
                writer.Dispose();
            }
            catch
            {
            }
        }
    }

    internal sealed class Options
    {
        public double Coverage = 0.75;
        public int MinWindows = 10;
        public int MaxWindows = 60;
        public double Delay = 0.30;
        public double Hold = 6.00;
        public double FinalSeconds = 300.0;
        public double WindowScale = 1.20;
        public bool Wobble = true;
        public bool Fx = true;
        public bool Audio = true;
        public string AudioPath = null;
        public double AudioStartSeconds = 0.0;
        public double Volume = 0.80;
        public bool FinalFullscreen = true;
        public bool AllScreens = false;
        public bool DpiAware = true;
        public string Layout = "grid";
        public bool LayoutWorkArea = false;
        public int AreaWidth = 0;
        public int AreaHeight = 0;
        public int Seed = 0;
        public string DumpLayout = null;
        public string DumpFinale = null;
        public string DumpFx = null;
        public string LogPath = null;
        public string ConfigPath = null;
        public bool Help = false;

        public static Options Parse(string[] args)
        {
            Options o = new Options();
            ApplyArgs(o, args);
            return o;
        }

        // Command line wins over the settings file.
        public static void ApplyArgs(Options o, string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string a = (args[i] ?? "").Trim().ToLowerInvariant();
                string v = (i + 1 < args.Length) ? args[i + 1] : null;
                switch (a)
                {
                    case "--coverage":
                        o.Coverage = Util.ParseDouble(v, o.Coverage);
                        i++;
                        break;
                    case "--min":
                        o.MinWindows = (int)Util.ParseDouble(v, o.MinWindows);
                        i++;
                        break;
                    case "--max":
                        o.MaxWindows = (int)Util.ParseDouble(v, o.MaxWindows);
                        i++;
                        break;
                    case "--delay":
                        o.Delay = Util.ParseDouble(v, o.Delay);
                        i++;
                        break;
                    case "--hold":
                        o.Hold = Util.ParseDouble(v, o.Hold);
                        i++;
                        break;
                    case "--final":
                        o.FinalSeconds = Util.ParseDouble(v, o.FinalSeconds);
                        i++;
                        break;
                    case "--seed":
                        o.Seed = (int)Util.ParseDouble(v, o.Seed);
                        i++;
                        break;
                    case "--dump-layout":
                        o.DumpLayout = v;
                        i++;
                        break;
                    case "--dump-finale":
                        o.DumpFinale = v;
                        i++;
                        break;
                    case "--dump-fx":
                        o.DumpFx = v;
                        i++;
                        break;
                    case "--log":
                        o.LogPath = v;
                        i++;
                        break;
                    case "--no-wobble":
                        o.Wobble = false;
                        break;
                    case "--no-fx":
                        o.Fx = false;
                        break;
                    case "--no-audio":
                        o.Audio = false;
                        break;
                    case "--audio":
                        o.AudioPath = v;
                        o.Audio = true;
                        i++;
                        break;
                    case "--audio-start":
                        o.AudioStartSeconds = Util.ParseDouble(v, o.AudioStartSeconds);
                        i++;
                        break;
                    case "--volume":
                        o.Volume = Util.ParseDouble(v, o.Volume);
                        i++;
                        break;
                    case "--window-scale":
                        o.WindowScale = Util.ParseDouble(v, o.WindowScale);
                        i++;
                        break;
                    case "--config":
                        o.ConfigPath = v;
                        i++;
                        break;
                    case "--final-fullscreen":
                        o.FinalFullscreen = true;
                        break;
                    case "--no-final-fullscreen":
                        o.FinalFullscreen = false;
                        break;
                    case "--all-screens":
                        o.AllScreens = true;
                        break;
                    case "--no-dpi":
                        o.DpiAware = false;
                        break;
                    case "--layout":
                        if (!string.IsNullOrEmpty(v))
                        {
                            o.Layout = v.Trim().ToLowerInvariant();
                        }
                        i++;
                        break;
                    case "--layout-work-area":
                        o.LayoutWorkArea = true;
                        break;
                    case "--area":
                        ParseArea(o, v);
                        i++;
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        o.Help = true;
                        break;
                }
            }

            o.Coverage = Util.Clamp(o.Coverage, 0.20, 0.95);
            o.MinWindows = Util.Clamp(o.MinWindows, 1, 200);
            o.MaxWindows = Util.Clamp(Math.Max(o.MaxWindows, o.MinWindows), o.MinWindows, 200);
            o.Delay = Util.Clamp(o.Delay, 0.0, 15.0);
            o.Hold = Util.Clamp(o.Hold, 0.1, 120.0);
            o.FinalSeconds = Util.Clamp(o.FinalSeconds, 0.0, 86400.0);
            o.Volume = Util.Clamp(o.Volume, 0.0, 1.0);
            o.AudioStartSeconds = Util.Clamp(o.AudioStartSeconds, 0.0, 86400.0);
            o.WindowScale = Util.Clamp(o.WindowScale, 0.5, 2.5);
        }

        // --area 2560x1440 forces the layout / full screen size. Useful when a
        // remote session or a scaled display reports a wrong desktop size.
        private static void ParseArea(Options o, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            string[] parts = value.ToLowerInvariant().Split('x');
            if (parts.Length != 2)
            {
                return;
            }

            int w = (int)Util.ParseDouble(parts[0], 0);
            int h = (int)Util.ParseDouble(parts[1], 0);
            if (w >= 200 && h >= 200)
            {
                o.AreaWidth = w;
                o.AreaHeight = h;
            }
        }

        public static string DefaultConfigPath()
        {
            try
            {
                return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "67.ini");
            }
            catch
            {
                return "67.ini";
            }
        }

        // Reads the settings written by 67-Settings.exe. Missing keys keep the
        // compiled-in defaults, so the file stays optional.
        public void LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch
            {
                return;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                {
                    continue;
                }

                int split = line.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, split).Trim().ToLowerInvariant();
                string raw = line.Substring(split + 1).Trim();

                switch (key)
                {
                    case "coverage": Coverage = Util.ParseDouble(raw, Coverage); break;
                    case "min": MinWindows = (int)Util.ParseDouble(raw, MinWindows); break;
                    case "max": MaxWindows = (int)Util.ParseDouble(raw, MaxWindows); break;
                    case "delay": Delay = Util.ParseDouble(raw, Delay); break;
                    case "hold": Hold = Util.ParseDouble(raw, Hold); break;
                    case "final": FinalSeconds = Util.ParseDouble(raw, FinalSeconds); break;
                    case "window_scale": WindowScale = Util.ParseDouble(raw, WindowScale); break;
                    case "volume": Volume = Util.ParseDouble(raw, Volume); break;
                    case "audio_start": AudioStartSeconds = Util.ParseDouble(raw, AudioStartSeconds); break;
                    case "wobble": Wobble = raw != "0"; break;
                    case "fx": Fx = raw != "0"; break;
                    case "audio": Audio = raw != "0"; break;
                    case "audio_file": AudioPath = string.IsNullOrEmpty(raw) ? null : raw; break;
                    case "final_fullscreen": FinalFullscreen = raw != "0"; break;
                    case "all_screens": AllScreens = raw != "0"; break;
                    case "layout": Layout = raw.Trim().ToLowerInvariant(); break;
                    case "layout_work_area": LayoutWorkArea = raw != "0"; break;
                }
            }

            Coverage = Util.Clamp(Coverage, 0.20, 0.95);
            MinWindows = Util.Clamp(MinWindows, 1, 200);
            MaxWindows = Util.Clamp(Math.Max(MaxWindows, MinWindows), MinWindows, 200);
            Delay = Util.Clamp(Delay, 0.0, 15.0);
            Hold = Util.Clamp(Hold, 0.1, 120.0);
            FinalSeconds = Util.Clamp(FinalSeconds, 0.0, 86400.0);
            Volume = Util.Clamp(Volume, 0.0, 1.0);
            AudioStartSeconds = Util.Clamp(AudioStartSeconds, 0.0, 86400.0);
            WindowScale = Util.Clamp(WindowScale, 0.5, 2.5);
        }

        public void SaveFile(string path)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("# 67 settings - written by 67-Settings.exe");
            text.AppendLine(Util.Fmt("coverage={0:0.###}", Coverage));
            text.AppendLine(Util.Fmt("min={0}", MinWindows));
            text.AppendLine(Util.Fmt("max={0}", MaxWindows));
            text.AppendLine(Util.Fmt("delay={0:0.###}", Delay));
            text.AppendLine(Util.Fmt("hold={0:0.###}", Hold));
            text.AppendLine(Util.Fmt("final={0:0.###}", FinalSeconds));
            text.AppendLine(Util.Fmt("window_scale={0:0.###}", WindowScale));
            text.AppendLine(Util.Fmt("volume={0:0.###}", Volume));
            text.AppendLine(Util.Fmt("audio_start={0:0.###}", AudioStartSeconds));
            text.AppendLine("wobble=" + (Wobble ? "1" : "0"));
            text.AppendLine("fx=" + (Fx ? "1" : "0"));
            text.AppendLine("audio=" + (Audio ? "1" : "0"));
            text.AppendLine("audio_file=" + (AudioPath == null ? "" : AudioPath));
            text.AppendLine("final_fullscreen=" + (FinalFullscreen ? "1" : "0"));
            text.AppendLine("all_screens=" + (AllScreens ? "1" : "0"));
            text.AppendLine("layout=" + Layout);
            text.AppendLine("layout_work_area=" + (LayoutWorkArea ? "1" : "0"));

            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(full, text.ToString(), new UTF8Encoding(false));
        }

        public static string Usage()
        {
            return
                "67 - Windows native window burst" + Environment.NewLine +
                Environment.NewLine +
                "Double click 67.exe, or run it with options:" + Environment.NewLine +
                "  --coverage 0.75    target desktop coverage (0.20 - 0.95)" + Environment.NewLine +
                "  --min 10           minimum number of popup windows" + Environment.NewLine +
                "  --max 60           maximum number of popup windows" + Environment.NewLine +
                "  --delay 0.3        seconds before the burst starts" + Environment.NewLine +
                "  --hold 6.0         seconds the burst stays on screen" + Environment.NewLine +
                "  --final 300        seconds the giant window stays (0 = forever)" + Environment.NewLine +
                "  --window-scale 1.2 popup size multiplier (0.5 - 2.5)" + Environment.NewLine +
                "  --no-final-fullscreen  giant window keeps a margin instead of full screen" + Environment.NewLine +
                "  --no-wobble        disable the drifting windows" + Environment.NewLine +
                "  --no-audio         stay silent (default plays audio\\67.m4a)" + Environment.NewLine +
                "  --audio <file>     play your own clip instead of audio\\67.m4a" + Environment.NewLine +
                "  --audio-start 0    start the clip at this many seconds" + Environment.NewLine +
                "  --volume 0.80      playback volume (0.0 - 1.0)" + Environment.NewLine +
                "  --no-fx            disable the full screen effects" + Environment.NewLine +
                "  --all-screens      spread over every monitor" + Environment.NewLine +
                "  --area 2560x1440   force desktop size (fixes scaled/remote sessions)" + Environment.NewLine +
                "  --no-dpi           don't declare DPI awareness (only if the layout looks wrong)" + Environment.NewLine +
                "  --seed 12345       deterministic layout" + Environment.NewLine +
                "  --log <file.txt>   write a step by step run log" + Environment.NewLine +
                "  --config <file>    use a specific settings file (default 67.ini)" + Environment.NewLine +
                "  --dump-layout <png> render the burst layout to a PNG and exit" + Environment.NewLine +
                Environment.NewLine +
                "ESC always closes everything immediately.";
        }
    }

}
