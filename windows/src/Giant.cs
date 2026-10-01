// Popup67 - the giant finale window.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Popup67
{
    internal sealed class GiantTheme
    {
        public Color Back1 = Color.FromArgb(255, 11, 14, 20);
        public Color Back2 = Color.FromArgb(255, 26, 32, 44);
        public Color Ink1 = Color.FromArgb(255, 253, 224, 71);
        public Color Ink2 = Color.FromArgb(255, 34, 211, 238);
    }

    // All of the giant window artwork lives here so it can also be rendered
    // off screen (--dump-finale) without showing a window.
    internal static class GiantArt
    {
        public static void Render(
            Graphics g,
            Rectangle client,
            FontChoice font,
            float pulse,
            GiantTheme theme,
            Rectangle closeBox,
            bool hoverClose)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            using (LinearGradientBrush bg = new LinearGradientBrush(
                new RectangleF(0f, 0f, client.Width, client.Height),
                theme.Back2,
                theme.Back1,
                78f))
            {
                g.FillRectangle(bg, client);
            }

            Rectangle stage = new Rectangle(
                client.Left,
                client.Top,
                client.Width,
                client.Height - (int)(client.Height * 0.02f));

            using (GraphicsPath path = TextArt.BuildFittedPath(
                "67",
                stage,
                font.Family,
                font.Style,
                pulse,
                0.88f,
                0.82f))
            {
                if (path != null)
                {
                    RectangleF pb = path.GetBounds();
                    DrawGlow(g, path, pb, theme);
                    using (LinearGradientBrush fill = new LinearGradientBrush(pb, theme.Ink1, theme.Ink2, 26f))
                    {
                        g.FillPath(fill, path);
                    }
                    using (Pen edge = new Pen(
                        Color.FromArgb(230, 255, 255, 255),
                        Math.Max(2f, pb.Height * 0.006f)))
                    {
                        edge.LineJoin = LineJoin.Round;
                        g.DrawPath(edge, path);
                    }
                }
            }

            DrawClose(g, closeBox, hoverClose);

            using (Pen framePen = new Pen(Color.FromArgb(70, 255, 255, 255), 1.4f))
            {
                g.DrawRectangle(
                    framePen,
                    client.Left,
                    client.Top,
                    client.Width - 1,
                    client.Height - 1);
            }
        }

        private static void DrawGlow(Graphics g, GraphicsPath path, RectangleF bounds, GiantTheme theme)
        {
            float glow = Math.Max(3f, bounds.Height * 0.030f);
            for (int i = 6; i >= 1; i--)
            {
                int alpha = 7 + (6 - i) * 6;
                float width = Math.Min(glow * i, Math.Max(3f, bounds.Height * 0.16f));
                try
                {
                    using (Pen pen = new Pen(Color.FromArgb(alpha, theme.Ink1), width))
                    {
                        pen.LineJoin = LineJoin.Round;
                        g.DrawPath(pen, path);
                    }
                }
                catch
                {
                    return; // glow is decoration only; never let it break the art
                }
            }
        }

        private static void DrawClose(Graphics g, Rectangle closeBox, bool hoverClose)
        {
            if (closeBox.Width <= 0)
            {
                return;
            }

            using (SolidBrush brush = new SolidBrush(Color.FromArgb(hoverClose ? 70 : 26, 255, 255, 255)))
            {
                g.FillEllipse(brush, closeBox);
            }

            int pad = (int)(closeBox.Width * 0.32f);
            using (Pen pen = new Pen(
                Color.FromArgb(hoverClose ? 255 : 170, 255, 255, 255),
                Math.Max(2.5f, closeBox.Width * 0.085f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, closeBox.Left + pad, closeBox.Top + pad, closeBox.Right - pad, closeBox.Bottom - pad);
                g.DrawLine(pen, closeBox.Right - pad, closeBox.Top + pad, closeBox.Left + pad, closeBox.Bottom - pad);
            }
        }
    }

    // Renders the finale artwork to a PNG for verification (no window shown).
    internal static class FinaleDump
    {
        public static void Write(Options opt, string path)
        {
            Rectangle area = Display.LayoutArea(opt);
            Rectangle screen = Display.FullScreen(opt);
            Rectangle frame;
            if (opt.FinalFullscreen)
            {
                frame = screen;
            }
            else
            {
                int fw = Math.Min((int)Math.Round(area.Width * 0.84), Math.Max(120, area.Width - 20));
                int fh = Math.Min((int)Math.Round(area.Height * 0.80), Math.Max(90, area.Height - 20));
                frame = new Rectangle(
                    area.Left + (area.Width - fw) / 2,
                    area.Top + (area.Height - fh) / 2,
                    fw,
                    fh);
            }

            int w = frame.Width;
            int h = frame.Height;

            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            try
            {
                FontChoice font = FontPicker.Best();
                int size = Util.Clamp((int)(h * 0.085), 44, 96);
                int margin = Util.Clamp((int)(h * 0.035), 16, 48);
                Rectangle closeBox = new Rectangle(w - margin - size, margin, size, size);

                using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        GiantArt.Render(
                            g,
                            new Rectangle(0, 0, w, h),
                            font,
                            1f,
                            new GiantTheme(),
                            closeBox,
                            false);
                    }
                    bmp.Save(full, ImageFormat.Png);
                }

                File.WriteAllText(
                    full + ".txt",
                    Util.Fmt(
                        "finale={0}x{1}{2}font={3}/{4}{2}screen={5}x{6}{2}path={7}{2}",
                        w,
                        h,
                        Environment.NewLine,
                        font.Family.Name,
                        font.Style,
                        area.Width,
                        area.Height,
                        DescribePath(font, w, h)),
                    new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                File.WriteAllText(full + ".error.txt", ex.ToString(), new UTF8Encoding(false));
                Environment.ExitCode = 3;
            }
        }

        private static string DescribePath(FontChoice font, int w, int h)
        {
            Rectangle stage = new Rectangle(0, 0, w, h - (int)(h * 0.02f));
            string raw;
            using (GraphicsPath probe = new GraphicsPath())
            {
                using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    probe.AddString("67", font.Family, (int)font.Style, 200f, new PointF(0f, 0f), format);
                }
                RectangleF rawBounds = probe.GetBounds();
                raw = Util.Fmt(
                    "raw={0:0.0},{1:0.0} {2:0.0}x{3:0.0}",
                    rawBounds.Left,
                    rawBounds.Top,
                    rawBounds.Width,
                    rawBounds.Height);
            }

            using (GraphicsPath path = TextArt.BuildFittedPath("67", stage, font.Family, font.Style, 1f, 0.88f, 0.82f))
            {
                if (path == null)
                {
                    return raw + " fitted=null";
                }
                RectangleF b = path.GetBounds();
                return raw + Util.Fmt(
                    " fitted={0:0.0},{1:0.0} {2:0.0}x{3:0.0} targetCenter={4:0.0},{5:0.0}",
                    b.Left,
                    b.Top,
                    b.Width,
                    b.Height,
                    stage.Left + stage.Width / 2.0,
                    stage.Top + stage.Height / 2.0);
            }
        }
    }

    internal sealed class GiantForm : Form
    {
        private const int CS_DROPSHADOW = 0x00020000;

        private readonly Options opt;
        private readonly FontChoice font;
        private readonly RunLog log;
        private readonly GiantTheme theme = new GiantTheme();

        private readonly Rectangle homeBounds;
        private readonly DateTime born;
        private System.Windows.Forms.Timer anim;
        private System.Windows.Forms.Timer life;
        private double phase;
        private Rectangle closeBox;
        private bool hoverClose;
        private bool dragging;
        private Point dragCursor;
        private Point dragOrigin;
        private bool closing;

        public GiantForm(Options opt, Rectangle frame, FontChoice font, RunLog log)
        {
            this.opt = opt;
            this.font = font;
            this.log = log;
            this.homeBounds = frame;
            this.born = DateTime.Now;

            Text = "67";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            TopMost = true;
            KeyPreview = true;
            BackColor = theme.Back1;
            DoubleBuffered = true;
            Bounds = Scale(frame, 0.58);
            Opacity = 0.05;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    CloseNow("escape");
                }
            };
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            DoubleClick += delegate { CloseNow("double-click"); };
            Resize += delegate { closeBox = CloseRectangle(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            BringToFront();
            closeBox = CloseRectangle();

            anim = new System.Windows.Forms.Timer();
            anim.Interval = 33;
            anim.Tick += AnimTick;
            anim.Start();

            log.Write(Util.Fmt(
                "giant: shown {0}x{1}; auto close timer {2}ms",
                ClientSize.Width,
                ClientSize.Height,
                opt.FinalSeconds > 0.0 ? (int)Math.Max(1.0, opt.FinalSeconds * 1000.0) : 0));

            if (opt.FinalSeconds > 0.0)
            {
                life = new System.Windows.Forms.Timer();
                life.Interval = (int)Math.Max(1.0, opt.FinalSeconds * 1000.0);
                life.Tick += delegate
                {
                    life.Stop();
                    life.Dispose();
                    life = null;
                    CloseNow("auto-close-timer");
                };
                life.Start();
            }
        }

        private void AnimTick(object sender, EventArgs e)
        {
            double ms = (DateTime.Now - born).TotalMilliseconds;

            if (ms < 420.0)
            {
                double t = ms / 420.0;
                double ease = 1.0 - Math.Pow(1.0 - t, 3.0);
                Bounds = Scale(homeBounds, 0.58 + 0.42 * ease);
                try
                {
                    Opacity = Math.Min(1.0, 0.05 + t * 1.25);
                }
                catch
                {
                }
                closeBox = CloseRectangle();
                return;
            }

            if (Bounds != homeBounds || Opacity < 1.0)
            {
                Bounds = homeBounds;
                try
                {
                    Opacity = 1.0;
                }
                catch
                {
                }
                closeBox = CloseRectangle();
            }

            // Pulse for a while, then settle into a static giant 67.
            if (ms < 13000.0)
            {
                phase += 0.042;
                Invalidate();
            }
            else if (anim != null)
            {
                anim.Stop();
                anim.Dispose();
                anim = null;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            float pulse = 1f + 0.030f * (float)Math.Sin(phase);
            GiantArt.Render(e.Graphics, ClientRectangle, font, pulse, theme, closeBox, hoverClose);
        }

        private Rectangle CloseRectangle()
        {
            int size = Util.Clamp((int)(ClientSize.Height * 0.085), 44, 96);
            int margin = Util.Clamp((int)(ClientSize.Height * 0.035), 16, 48);
            return new Rectangle(ClientSize.Width - margin - size, margin, size, size);
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.Add("Close (ESC)", null, delegate
                {
                    menu.Close();
                    CloseNow("context-menu");
                });
                menu.Show(this, e.Location);
                return;
            }

            if (closeBox.Contains(e.Location))
            {
                CloseNow("close-box");
                return;
            }

            dragging = true;
            dragCursor = Cursor.Position;
            dragOrigin = Location;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            bool hover = closeBox.Contains(e.Location);
            if (hover != hoverClose)
            {
                hoverClose = hover;
                Invalidate(closeBox);
            }

            if (dragging)
            {
                Point now = Cursor.Position;
                Location = new Point(
                    dragOrigin.X + (now.X - dragCursor.X),
                    dragOrigin.Y + (now.Y - dragCursor.Y));
            }
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        public void CloseNow()
        {
            CloseNow("direct");
        }

        public void CloseNow(string reason)
        {
            if (closing)
            {
                return;
            }
            closing = true;
            log.Write(Util.Fmt(
                "giant: closing after {0:0.0}s ({1})",
                (DateTime.Now - born).TotalSeconds,
                reason));

            if (anim != null)
            {
                anim.Stop();
                anim.Dispose();
                anim = null;
            }
            if (life != null)
            {
                life.Stop();
                life.Dispose();
                life = null;
            }

            Rectangle start = Bounds;
            DateTime startAt = DateTime.Now;
            System.Windows.Forms.Timer fade = new System.Windows.Forms.Timer();
            fade.Interval = 16;
            fade.Tick += delegate(object sender, EventArgs e)
            {
                double p = (DateTime.Now - startAt).TotalMilliseconds / 180.0;
                if (p > 1.0)
                {
                    p = 1.0;
                }
                try
                {
                    Opacity = 1.0 - p;
                    Bounds = Scale(start, 1.0 - 0.06 * p);
                }
                catch
                {
                }
                if (p >= 1.0)
                {
                    fade.Stop();
                    fade.Dispose();
                    Bounds = start;
                    try
                    {
                        Close();
                    }
                    catch
                    {
                    }
                }
            };
            fade.Start();
        }

        private static Rectangle Scale(Rectangle source, double factor)
        {
            int w = Math.Max(40, (int)Math.Round(source.Width * factor));
            int h = Math.Max(30, (int)Math.Round(source.Height * factor));
            return new Rectangle(
                source.Left + (source.Width - w) / 2,
                source.Top + (source.Height - h) / 2,
                w,
                h);
        }
    }
}
