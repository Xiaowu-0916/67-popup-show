// Popup67 - full screen effects.
//
// A click-through, never-activated, per-pixel-alpha overlay drawn straight onto
// a 32bpp bitmap and pushed with UpdateLayeredWindow. No controls, no focus,
// no interference with the popup windows underneath.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Popup67
{
    // Renders one overlay frame over a mock desktop so the effect can be checked
    // without covering the screen (--dump-fx).
    internal static class FxDump
    {
        public static void Write(Options opt, string path)
        {
            Rectangle area = Display.FullScreen(opt);

            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (Bitmap bmp = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    using (LinearGradientBrush bg = new LinearGradientBrush(
                        new RectangleF(0f, 0f, area.Width, area.Height),
                        Color.FromArgb(255, 44, 52, 72),
                        Color.FromArgb(255, 12, 14, 20),
                        62f))
                    {
                        g.FillRectangle(bg, 0, 0, area.Width, area.Height);
                    }

                    FxOverlay fx = new FxOverlay(
                        new Rectangle(0, 0, area.Width, area.Height),
                        false,
                        new Random(opt.Seed == 0 ? 20261001 : opt.Seed),
                        new RunLog(null));
                    try
                    {
                        fx.PreviewInto(g, 2.4);
                    }
                    finally
                    {
                        fx.Dispose();
                    }
                }
                bmp.Save(full, ImageFormat.Png);
            }
        }
    }

    internal sealed class FxOverlay : Form
    {
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int ULW_ALPHA = 0x00000002;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        private struct pointapi
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct sizeapi
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct blendfunctionapi
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hwnd,
            IntPtr hdcDst,
            ref pointapi pptDst,
            ref sizeapi psize,
            IntPtr hdcSrc,
            ref pointapi pptSrc,
            int crKey,
            ref blendfunctionapi pblend,
            int dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint flags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        private sealed class Particle
        {
            public double X;
            public double Y;
            public double VX;
            public double VY;
            public double Rot;
            public double Spin;
            public double Size;
            public double Hue;
            public int Shape;
        }

        private sealed class Column
        {
            public double X;
            public double Y;
            public double Speed;
            public double Hue;
            public string Text;
        }

        private sealed class Ring
        {
            public double Age;
        }

        private readonly Rectangle area;
        private readonly bool finale;
        private readonly Random rng;
        private readonly RunLog log;
        private readonly List<Particle> particles = new List<Particle>();
        private readonly List<Column> columns = new List<Column>();
        private readonly List<Ring> rings = new List<Ring>();

        private System.Windows.Forms.Timer timer;
        private Bitmap canvas;
        private Font rainFont;
        private DateTime started;
        private DateTime lastFrame;
        private double alpha;
        private double alphaTarget = 1.0;
        private bool fadingOut;
        private bool dead;
        private bool failed;
        private int raiseCounter;
        private byte[] pixelBuffer;

        public FxOverlay(Rectangle area, bool finale, Random rng, RunLog log)
        {
            this.area = area;
            this.finale = finale;
            this.rng = rng;
            this.log = log;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Black;
            Bounds = area;
            SetStyle(
                ControlStyles.Opaque |
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint,
                true);

            Build();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        private void Build()
        {
            int pCount = finale ? 54 : 130;
            for (int i = 0; i < pCount; i++)
            {
                particles.Add(NewParticle(true));
            }

            int cCount = finale ? 16 : 46;
            for (int i = 0; i < cCount; i++)
            {
                Column c = new Column();
                c.X = rng.Next(0, Math.Max(2, area.Width));
                c.Y = rng.Next(-area.Height, area.Height);
                c.Speed = (finale ? 40.0 : 90.0) + rng.NextDouble() * (finale ? 60.0 : 220.0);
                c.Hue = rng.NextDouble();
                c.Text = rng.Next(4) == 0 ? "6767" : "67";
                columns.Add(c);
            }

            rainFont = new Font(
                "Impact",
                Math.Max(16f, area.Height * (finale ? 0.030f : 0.042f)),
                FontStyle.Bold,
                GraphicsUnit.Pixel);

            canvas = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
        }

        private Particle NewParticle(bool spread)
        {
            Particle p = new Particle();
            p.X = rng.Next(0, Math.Max(2, area.Width));
            p.Y = spread ? rng.Next(0, Math.Max(2, area.Height)) : -20.0;
            p.VX = (rng.NextDouble() - 0.5) * (finale ? 60.0 : 140.0);
            p.VY = (finale ? 50.0 : 120.0) + rng.NextDouble() * (finale ? 110.0 : 320.0);
            p.Rot = rng.NextDouble() * Math.PI * 2.0;
            p.Spin = (rng.NextDouble() - 0.5) * 9.0;
            p.Size = (5.0 + rng.NextDouble() * (finale ? 9.0 : 15.0)) * Dpi.Scale;
            p.Hue = rng.NextDouble();
            p.Shape = rng.Next(3);
            return p;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            started = DateTime.Now;
            lastFrame = started;

            RaiseToTop();

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 33;
            timer.Tick += Tick;
            timer.Start();
        }

        // A window that never activates would otherwise stay underneath the
        // foreground (giant) window, so push it up inside the topmost band.
        public void RaiseToTop()
        {
            try
            {
                SetWindowPos(
                    Handle,
                    HWND_TOPMOST,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            catch
            {
            }
        }

        // The layer never takes focus, but if it ever does, ESC still cancels
        // the whole show.
        public Action EscapeAction;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && EscapeAction != null)
            {
                EscapeAction();
            }
            base.OnKeyDown(e);
        }

        public void FadeOutAndClose()
        {
            if (dead)
            {
                return;
            }
            fadingOut = true;
            alphaTarget = 0.0;
        }

        public void CloseNow()
        {
            if (dead)
            {
                return;
            }
            dead = true;
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
            }
            try
            {
                Close();
            }
            catch
            {
            }
        }

        private void Tick(object sender, EventArgs e)
        {
            if (dead)
            {
                return;
            }

            DateTime now = DateTime.Now;
            double dt = Math.Min(0.1, (now - lastFrame).TotalSeconds);
            lastFrame = now;
            double t = (now - started).TotalSeconds;

            double target = fadingOut ? 0.0 : (finale ? 0.80 : 0.92);
            alphaTarget = target;
            alpha += (alphaTarget - alpha) * Math.Min(1.0, dt * (fadingOut ? 6.5 : 7.5));

            if (fadingOut && alpha < 0.02)
            {
                CloseNow();
                return;
            }

            // safety: never outlive the show by much
            if (!finale && !fadingOut && t > 12.0)
            {
                FadeOutAndClose();
            }

            // keep the layer above the (topmost, activated) windows
            raiseCounter++;
            if (raiseCounter >= 30)
            {
                raiseCounter = 0;
                RaiseToTop();
            }

            Step(dt, t);

            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingMode = CompositingMode.SourceOver;
                g.Clear(Color.Transparent);
                PaintInto(g);
            }
            Push();
        }

        // Renders one frame onto an arbitrary surface (used by --dump-fx).
        public void PreviewInto(Graphics g, double seconds)
        {
            started = DateTime.Now - TimeSpan.FromSeconds(seconds);
            lastFrame = started;
            alpha = 1.0;

            int steps = (int)Math.Min(600.0, seconds / 0.033);
            for (int i = 0; i < steps; i++)
            {
                Step(0.033, (i + 1) * 0.033);
            }
            PaintInto(g);
        }

        private void Step(double dt, double t)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                Particle p = particles[i];
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                p.VY += (finale ? 30.0 : 90.0) * dt;
                p.Rot += p.Spin * dt;

                if (p.Y > area.Height + 40 || p.X < -60 || p.X > area.Width + 60)
                {
                    Particle fresh = NewParticle(false);
                    p.X = fresh.X;
                    p.Y = fresh.Y;
                    p.VX = fresh.VX;
                    p.VY = fresh.VY;
                    p.Rot = fresh.Rot;
                    p.Spin = fresh.Spin;
                    p.Size = fresh.Size;
                    p.Hue = fresh.Hue;
                    p.Shape = fresh.Shape;
                }
            }

            for (int i = 0; i < columns.Count; i++)
            {
                Column c = columns[i];
                c.Y += c.Speed * dt;
                if (c.Y > area.Height + 80)
                {
                    c.Y = -rng.Next(120, 800);
                    c.X = rng.Next(0, Math.Max(2, area.Width));
                    c.Hue = rng.NextDouble();
                    c.Speed = (finale ? 40.0 : 90.0) + rng.NextDouble() * (finale ? 60.0 : 220.0);
                }
            }

            for (int i = rings.Count - 1; i >= 0; i--)
            {
                rings[i].Age += dt;
                if (rings[i].Age > 1.6)
                {
                    rings.RemoveAt(i);
                }
            }

            double period = finale ? 1.7 : 0.85;
            if (rings.Count == 0 || t > (rings.Count * period))
            {
                rings.Add(new Ring());
            }
        }

        private void PaintInto(Graphics g)
        {
            DrawRays(g);
            DrawRain(g);
            DrawRings(g);
            DrawConfetti(g);
            DrawBorder(g);
            DrawFlash(g);
        }

        private void DrawRays(Graphics g)
        {
            double cx = area.Width / 2.0;
            double cy = area.Height / 2.0;
            double maxR = Math.Sqrt(cx * cx + cy * cy) * 1.06;
            int wedges = finale ? 14 : 20;
            double spin = (finale ? 7.0 : 30.0) * (DateTime.Now - started).TotalSeconds;
            int baseAlpha = finale ? 30 : 58;

            for (int i = 0; i < wedges; i++)
            {
                double step = 360.0 / wedges;
                double a0 = ((spin + i * step) % 360.0) * Math.PI / 180.0;
                double a1 = a0 + step * 0.55 * Math.PI / 180.0;
                double hue = ((i / (double)wedges) + spin / 720.0) % 1.0;
                Color c = Util.FromHsl(hue, 0.95, 0.58);

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(baseAlpha, c)))
                {
                    PointF[] pts = new PointF[3];
                    pts[0] = new PointF((float)cx, (float)cy);
                    pts[1] = new PointF(
                        (float)(cx + Math.Cos(a0) * maxR),
                        (float)(cy + Math.Sin(a0) * maxR));
                    pts[2] = new PointF(
                        (float)(cx + Math.Cos(a1) * maxR),
                        (float)(cy + Math.Sin(a1) * maxR));
                    g.FillPolygon(brush, pts);
                }
            }
        }

        private void DrawRain(Graphics g)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                Column c = columns[i];
                Color hue = Util.FromHsl((c.Hue + i * 0.013) % 1.0, 0.95, 0.62);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(finale ? 85 : 155, hue)))
                {
                    g.DrawString(c.Text, rainFont, brush, (float)c.X, (float)c.Y);
                }
            }
        }

        private void DrawRings(Graphics g)
        {
            double cx = area.Width / 2.0;
            double cy = area.Height / 2.0;
            double maxR = Math.Sqrt(cx * cx + cy * cy);

            for (int i = 0; i < rings.Count; i++)
            {
                double p = rings[i].Age / 1.6;
                if (p > 1.0)
                {
                    continue;
                }
                double r = p * maxR;
                int a = (int)((1.0 - p) * (finale ? 85 : 135));
                float width = 4f + (float)(1.0 - p) * (finale ? 10f : 18f);
                using (Pen pen = new Pen(Color.FromArgb(a, Util.FromHsl((p * 0.6) % 1.0, 0.9, 0.65)), width))
                {
                    g.DrawEllipse(
                        pen,
                        (float)(cx - r),
                        (float)(cy - r * 0.75),
                        (float)(r * 2.0),
                        (float)(r * 1.5));
                }
            }
        }

        private void DrawConfetti(Graphics g)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                Particle p = particles[i];
                Color c = Util.FromHsl(p.Hue, 0.95, 0.6);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(finale ? 170 : 235, c)))
                {
                    GraphicsState state = g.Save();
                    try
                    {
                        g.TranslateTransform((float)p.X, (float)p.Y);
                        g.RotateTransform((float)(p.Rot * 180.0 / Math.PI));

                        if (p.Shape == 0)
                        {
                            g.FillRectangle(
                                brush,
                                (float)(-p.Size / 2.0),
                                (float)(-p.Size / 4.0),
                                (float)p.Size,
                                (float)(p.Size / 2.0));
                        }
                        else if (p.Shape == 1)
                        {
                            g.FillEllipse(
                                brush,
                                (float)(-p.Size / 2.0),
                                (float)(-p.Size / 2.0),
                                (float)p.Size,
                                (float)p.Size);
                        }
                        else
                        {
                            PointF[] tri = new PointF[3];
                            tri[0] = new PointF(0f, (float)(-p.Size / 2.0));
                            tri[1] = new PointF((float)(p.Size / 2.0), (float)(p.Size / 2.0));
                            tri[2] = new PointF((float)(-p.Size / 2.0), (float)(p.Size / 2.0));
                            g.FillPolygon(brush, tri);
                        }
                    }
                    finally
                    {
                        g.Restore(state);
                    }
                }
            }
        }

        private void DrawBorder(Graphics g)
        {
            double t = (DateTime.Now - started).TotalSeconds;
            int thickness = Dpi.Scaled(finale ? 6 : 11);
            double pulse = 0.65 + 0.35 * Math.Sin(t * 6.0);
            int alpha = (int)((finale ? 110 : 170) * pulse);

            for (int i = 0; i < 4; i++)
            {
                Rectangle edge;
                if (i == 0)
                {
                    edge = new Rectangle(0, 0, area.Width, thickness);
                }
                else if (i == 1)
                {
                    edge = new Rectangle(0, area.Height - thickness, area.Width, thickness);
                }
                else if (i == 2)
                {
                    edge = new Rectangle(0, 0, thickness, area.Height);
                }
                else
                {
                    edge = new Rectangle(area.Width - thickness, 0, thickness, area.Height);
                }

                Color c = Util.FromHsl((t * 0.25 + i * 0.22) % 1.0, 0.95, 0.6);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, c)))
                {
                    g.FillRectangle(brush, edge);
                }
            }
        }

        private void DrawFlash(Graphics g)
        {
            double t = (DateTime.Now - started).TotalSeconds;
            if (t > 0.55)
            {
                return;
            }
            int a = (int)((1.0 - t / 0.55) * 120.0);
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(a, Color.White)))
            {
                g.FillRectangle(brush, 0, 0, area.Width, area.Height);
            }
        }

        private void Push()
        {
            if (failed || dead || canvas == null)
            {
                return;
            }

            // UpdateLayeredWindow expects premultiplied alpha; without this the
            // overlay looks washed out and overly bright.
            Premultiply();

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = canvas.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);

                sizeapi size = new sizeapi();
                size.cx = area.Width;
                size.cy = area.Height;

                pointapi src = new pointapi();
                src.x = 0;
                src.y = 0;

                pointapi dst = new pointapi();
                dst.x = area.Left;
                dst.y = area.Top;

                blendfunctionapi blend = new blendfunctionapi();
                blend.BlendOp = AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = (byte)Math.Round(Util.Clamp(alpha, 0.0, 1.0) * 255.0);
                blend.AlphaFormat = AC_SRC_ALPHA;

                if (!UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA))
                {
                    failed = true;
                    log.Write("fx: UpdateLayeredWindow failed");
                }
            }
            catch (Exception ex)
            {
                failed = true;
                log.Write("fx: " + ex.Message);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero)
                {
                    SelectObject(memDc, oldBitmap);
                }
                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                }
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private void Premultiply()
        {
            int height = canvas.Height;
            int width = canvas.Width;
            BitmapData data = canvas.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite,
                PixelFormat.Format32bppArgb);
            try
            {
                int bytes = data.Stride * height;
                if (pixelBuffer == null || pixelBuffer.Length < bytes)
                {
                    pixelBuffer = new byte[bytes];
                }

                Marshal.Copy(data.Scan0, pixelBuffer, 0, bytes);

                for (int i = 0; i + 3 < bytes; i += 4)
                {
                    byte alpha = pixelBuffer[i + 3];
                    if (alpha == 255)
                    {
                        continue;
                    }
                    if (alpha == 0)
                    {
                        pixelBuffer[i] = 0;
                        pixelBuffer[i + 1] = 0;
                        pixelBuffer[i + 2] = 0;
                        continue;
                    }
                    pixelBuffer[i] = (byte)(pixelBuffer[i] * alpha / 255);
                    pixelBuffer[i + 1] = (byte)(pixelBuffer[i + 1] * alpha / 255);
                    pixelBuffer[i + 2] = (byte)(pixelBuffer[i + 2] * alpha / 255);
                }

                Marshal.Copy(pixelBuffer, 0, data.Scan0, bytes);
            }
            finally
            {
                canvas.UnlockBits(data);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // The layer is pushed with UpdateLayeredWindow; GDI painting is unused.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (timer != null)
                {
                    timer.Stop();
                    timer.Dispose();
                    timer = null;
                }
                if (rainFont != null)
                {
                    rainFont.Dispose();
                    rainFont = null;
                }
                if (canvas != null)
                {
                    canvas.Dispose();
                    canvas = null;
                }
            }
            base.Dispose(disposing);
        }

    }
}
