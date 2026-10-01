// Popup67 - colours, fonts and the GDI+ "67" artwork.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Popup67
{
    internal sealed class Palette
    {
        private static readonly Color[] Backs =
        {
            Color.FromArgb(255, 255, 59, 48),
            Color.FromArgb(255, 255, 149, 0),
            Color.FromArgb(255, 255, 214, 10),
            Color.FromArgb(255, 52, 199, 89),
            Color.FromArgb(255, 0, 199, 190),
            Color.FromArgb(255, 48, 176, 199),
            Color.FromArgb(255, 10, 132, 255),
            Color.FromArgb(255, 94, 92, 230),
            Color.FromArgb(255, 191, 90, 242),
            Color.FromArgb(255, 255, 55, 95),
            Color.FromArgb(255, 242, 242, 247),
            Color.FromArgb(255, 28, 28, 30),
            Color.FromArgb(255, 142, 142, 147),
            Color.FromArgb(255, 11, 61, 145),
            Color.FromArgb(255, 0, 122, 90),
            Color.FromArgb(255, 140, 90, 40)
        };

        public readonly Color Back;
        public readonly Color Ink;
        public readonly Color Ink2;

        public Palette(Color back)
        {
            Back = back;
            if (Util.Luminance(back) > 0.62)
            {
                Ink = Color.FromArgb(255, 12, 14, 20);
                Ink2 = Util.Darken(back, 0.55);
            }
            else
            {
                Ink = Color.White;
                Ink2 = Util.Mix(back, Color.White, 0.62);
            }
        }

        public static Color BackAt(int index)
        {
            return Backs[((index % Backs.Length) + Backs.Length) % Backs.Length];
        }

        public static Palette[] Deck(Random rng)
        {
            List<Color> colors = new List<Color>(Backs);
            Util.Shuffle(colors, rng);
            Palette[] deck = new Palette[colors.Count];
            for (int i = 0; i < colors.Count; i++)
            {
                deck[i] = new Palette(colors[i]);
            }
            return deck;
        }
    }

    internal sealed class FontChoice
    {
        public readonly FontFamily Family;
        public readonly FontStyle Style;

        public FontChoice(FontFamily family, FontStyle style)
        {
            Family = family;
            Style = style;
        }
    }

    internal static class FontPicker
    {
        private static readonly string[] Chunky =
        {
            "Impact",
            "Arial Black",
            "Haettenschweiler",
            "Franklin Gothic Heavy",
            "Segoe UI Black",
            "Bahnschrift",
            "Franklin Gothic Medium"
        };

        private static readonly string[] Others =
        {
            "Segoe UI",
            "Verdana",
            "Tahoma",
            "Trebuchet MS",
            "Georgia",
            "Consolas",
            "Comic Sans MS",
            "Segoe Print",
            "Calibri",
            "Cambria"
        };

        private static List<FontChoice> chunky;
        private static List<FontChoice> others;

        private static List<FontChoice> Resolve(string[] names)
        {
            List<FontChoice> found = new List<FontChoice>();
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    FontFamily family = new FontFamily(names[i]);
                    FontStyle style = family.IsStyleAvailable(FontStyle.Bold)
                        ? FontStyle.Bold
                        : (family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold);
                    found.Add(new FontChoice(family, style));
                }
                catch
                {
                }
            }
            return found;
        }

        private static void Ensure()
        {
            if (chunky == null)
            {
                chunky = Resolve(Chunky);
                others = Resolve(Others);
            }
        }

        public static FontChoice Next(Random rng)
        {
            Ensure();
            List<FontChoice> pool = (rng.Next(100) < 62) ? chunky : others;
            if (pool == null || pool.Count == 0)
            {
                pool = (pool == chunky) ? others : chunky;
            }
            if (pool != null && pool.Count > 0)
            {
                return pool[rng.Next(pool.Count)];
            }
            return new FontChoice(FontFamily.GenericSansSerif, FontStyle.Bold);
        }

        public static FontChoice Best()
        {
            Ensure();
            if (chunky != null && chunky.Count > 0)
            {
                return chunky[0];
            }
            if (others != null && others.Count > 0)
            {
                return others[0];
            }
            return new FontChoice(FontFamily.GenericSansSerif, FontStyle.Bold);
        }
    }

    internal enum ArtStyle
    {
        Solid = 0,
        Gradient = 1,
        Outline = 2,
        Shadow = 3,
        Neon = 4
    }

    internal static class TextArt
    {
        public static GraphicsPath BuildFittedPath(
            string text,
            Rectangle bounds,
            FontFamily family,
            FontStyle style,
            float extraScale,
            float widthRatio,
            float heightRatio)
        {
            if (bounds.Width < 12 || bounds.Height < 12)
            {
                return null;
            }

            GraphicsPath path = new GraphicsPath();
            try
            {
                using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    format.Trimming = StringTrimming.None;
                    path.AddString(text, family, (int)style, 200f, new PointF(0f, 0f), format);
                }

                RectangleF b = path.GetBounds();
                if (b.Width <= 0.01f || b.Height <= 0.01f)
                {
                    path.Dispose();
                    return null;
                }

                float targetW = bounds.Width * widthRatio * extraScale;
                float targetH = bounds.Height * heightRatio * extraScale;
                float scale = Math.Min(targetW / b.Width, targetH / b.Height);

                using (Matrix m = new Matrix())
                {
                    // GDI+ prepends: the last call is applied first, so this
                    // order reads bottom-up as "move glyph to origin -> scale
                    // -> move to the centre of the box".
                    m.Translate(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);
                    m.Scale(scale, scale);
                    m.Translate(-(b.Left + b.Width / 2f), -(b.Top + b.Height / 2f));
                    path.Transform(m);
                }
                return path;
            }
            catch
            {
                path.Dispose();
                return null;
            }
        }

        public static void Draw(
            Graphics g,
            string text,
            Rectangle bounds,
            FontFamily family,
            FontStyle style,
            ArtStyle art,
            Color ink,
            Color ink2,
            float extraScale)
        {
            // 0.94 / 0.88 makes the number fill most of the window
            using (GraphicsPath path = BuildFittedPath(text, bounds, family, style, extraScale, 0.94f, 0.88f))
            {
                if (path == null)
                {
                    return;
                }

                RectangleF pb = path.GetBounds();
                switch (art)
                {
                    case ArtStyle.Solid:
                        using (SolidBrush brush = new SolidBrush(ink))
                        {
                            g.FillPath(brush, path);
                        }
                        break;

                    case ArtStyle.Gradient:
                        using (LinearGradientBrush brush = new LinearGradientBrush(pb, ink, ink2, 22f))
                        {
                            g.FillPath(brush, path);
                        }
                        break;

                    case ArtStyle.Shadow:
                        using (Matrix m = new Matrix())
                        {
                            m.Translate(pb.Width * 0.030f, pb.Height * 0.045f);
                            using (GraphicsPath shadow = (GraphicsPath)path.Clone())
                            {
                                shadow.Transform(m);
                                using (SolidBrush brush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                                {
                                    g.FillPath(brush, shadow);
                                }
                            }
                        }
                        using (LinearGradientBrush brush = new LinearGradientBrush(pb, ink, ink2, 22f))
                        {
                            g.FillPath(brush, path);
                        }
                        break;

                    case ArtStyle.Outline:
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(42, ink)))
                        {
                            g.FillPath(brush, path);
                        }
                        using (Pen pen = new Pen(ink, Math.Max(2f, pb.Height * 0.055f)))
                        {
                            pen.LineJoin = LineJoin.Round;
                            g.DrawPath(pen, path);
                        }
                        using (Pen pen = new Pen(ink2, Math.Max(1f, pb.Height * 0.020f)))
                        {
                            pen.LineJoin = LineJoin.Round;
                            g.DrawPath(pen, path);
                        }
                        break;

                    case ArtStyle.Neon:
                        float glow = Math.Max(3f, pb.Height * 0.070f);
                        for (int i = 4; i >= 1; i--)
                        {
                            using (Pen pen = new Pen(Color.FromArgb(10 + (4 - i) * 6, ink), glow * i))
                            {
                                pen.LineJoin = LineJoin.Round;
                                g.DrawPath(pen, path);
                            }
                        }
                        using (LinearGradientBrush brush = new LinearGradientBrush(pb, ink, ink2, 22f))
                        {
                            g.FillPath(brush, path);
                        }
                        break;
                }
            }
        }
    }

    internal sealed class NumberCanvas : Control
    {
        private readonly Palette palette;
        private readonly FontChoice font;
        private readonly ArtStyle art;
        private readonly int pattern;
        private readonly int face;
        private readonly int chaos;
        private readonly float tilt;
        private readonly float[] decorX;
        private readonly float[] decorY;
        private readonly float[] decorSize;
        private readonly float[] decorRot;
        private readonly int[] decorKind;
        private readonly Color[] decorColor;
        private float extraScale = 1f;
        private float hueShift;

        public NumberCanvas(
            Palette palette,
            FontChoice font,
            ArtStyle art,
            int pattern,
            int face,
            int chaos,
            float tilt,
            int decorSeed)
        {
            this.palette = palette;
            this.font = font;
            this.art = art;
            this.pattern = pattern;
            this.face = face;
            this.chaos = chaos;
            this.tilt = tilt;

            BackColor = palette.Back;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Opaque,
                true);

            Random rng = new Random(decorSeed);
            int count = rng.Next(3, 8);
            decorX = new float[count];
            decorY = new float[count];
            decorSize = new float[count];
            decorRot = new float[count];
            decorKind = new int[count];
            decorColor = new Color[count];

            for (int i = 0; i < count; i++)
            {
                decorX[i] = (float)(0.06 + rng.NextDouble() * 0.88);
                decorY[i] = (float)(0.06 + rng.NextDouble() * 0.88);
                decorSize[i] = (float)(0.06 + rng.NextDouble() * 0.16);
                decorRot[i] = (float)(rng.NextDouble() * Math.PI * 2.0);
                decorKind[i] = rng.Next(5);

                if (rng.Next(100) < 62)
                {
                    decorColor[i] = Util.Luminance(palette.Back) > 0.62
                        ? Color.FromArgb(150, 0, 0, 0)
                        : Color.FromArgb(190, 255, 255, 255);
                }
                else
                {
                    decorColor[i] = Color.FromArgb(220, Util.FromHsl(rng.NextDouble(), 0.95, 0.62));
                }
            }
        }

        public float ExtraScale
        {
            get { return extraScale; }
            set
            {
                if (Math.Abs(extraScale - value) > 0.0005f)
                {
                    extraScale = value;
                    Invalidate();
                }
            }
        }

        public float HueShift
        {
            get { return hueShift; }
            set
            {
                if (Math.Abs(hueShift - value) > 0.5f)
                {
                    hueShift = value;
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            Color back = Util.HueShift(palette.Back, hueShift);
            Color ink = Util.HueShift(palette.Ink, hueShift * 0.35f);
            Color ink2 = Util.HueShift(palette.Ink2, hueShift * 0.35f);
            g.Clear(back);

            GraphicsState state = g.Save();
            try
            {
                if (Math.Abs(tilt) > 0.5f)
                {
                    g.TranslateTransform(ClientSize.Width / 2f, ClientSize.Height / 2f);
                    g.RotateTransform(tilt);
                    g.TranslateTransform(-ClientSize.Width / 2f, -ClientSize.Height / 2f);
                }

                DrawPattern(g, back);
                DrawNumber(g, ink, ink2);
            }
            finally
            {
                g.Restore(state);
            }

            DrawFace(g);
            DrawDecor(g);
        }

        private void DrawNumber(Graphics g, Color ink, Color ink2)
        {
            if (chaos == 1)
            {
                int w = ClientSize.Width;
                int h = ClientSize.Height;
                Rectangle[] spots = new Rectangle[3];
                spots[0] = new Rectangle((int)(w * 0.02), (int)(h * 0.04), (int)(w * 0.58), (int)(h * 0.56));
                spots[1] = new Rectangle((int)(w * 0.40), (int)(h * 0.42), (int)(w * 0.58), (int)(h * 0.56));
                spots[2] = new Rectangle((int)(w * 0.20), (int)(h * 0.24), (int)(w * 0.60), (int)(h * 0.52));

                for (int i = 0; i < spots.Length; i++)
                {
                    float scale = i == 2 ? extraScale : extraScale * 0.86f;
                    Color a = i == 0 ? ink2 : ink;
                    TextArt.Draw(g, "67", spots[i], font.Family, font.Style, art, Color.FromArgb(210, a), ink2, scale);
                }
                return;
            }

            TextArt.Draw(g, "67", ClientRectangle, font.Family, font.Style, art, ink, ink2, extraScale);
        }

        private void DrawPattern(Graphics g, Color back)
        {
            if (pattern <= 0)
            {
                return;
            }

            bool light = Util.Luminance(back) > 0.62;
            Color soft = light
                ? Color.FromArgb(30, 0, 0, 0)
                : Color.FromArgb(34, 255, 255, 255);
            Color softer = light
                ? Color.FromArgb(14, 0, 0, 0)
                : Color.FromArgb(16, 255, 255, 255);

            if (pattern == 1)
            {
                int step = Math.Max(14, ClientSize.Height / 6);
                using (Pen pen = new Pen(soft, Math.Max(3f, ClientSize.Height / 34f)))
                {
                    for (int x = -ClientSize.Height; x < ClientSize.Width + ClientSize.Height; x += step)
                    {
                        g.DrawLine(pen, x, ClientSize.Height, x + ClientSize.Height, 0);
                    }
                }
            }
            else if (pattern == 2)
            {
                int step = Math.Max(18, ClientSize.Height / 4);
                using (SolidBrush brush = new SolidBrush(soft))
                {
                    for (int y = step / 2; y < ClientSize.Height; y += step)
                    {
                        for (int x = step / 2; x < ClientSize.Width; x += step)
                        {
                            int d = Math.Max(3, step / 7);
                            g.FillEllipse(brush, x - d / 2, y - d / 2, d, d);
                        }
                    }
                }
            }
            else
            {
                int size = (int)(Math.Min(ClientSize.Width, ClientSize.Height) * 0.92f);
                using (SolidBrush brush = new SolidBrush(softer))
                {
                    g.FillEllipse(
                        brush,
                        (ClientSize.Width - size) / 2,
                        (ClientSize.Height - size) / 2,
                        size,
                        size);
                }
                using (Pen pen = new Pen(soft, Math.Max(2f, ClientSize.Height / 60f)))
                {
                    g.DrawEllipse(
                        pen,
                        (ClientSize.Width - size) / 2,
                        (ClientSize.Height - size) / 2,
                        size,
                        size);
                }
            }
        }

        private void DrawDecor(Graphics g)
        {
            for (int i = 0; i < decorKind.Length; i++)
            {
                float cx = decorX[i] * ClientSize.Width;
                float cy = decorY[i] * ClientSize.Height;
                float r = decorSize[i] * Math.Min(ClientSize.Width, ClientSize.Height);
                if (r < 3f)
                {
                    continue;
                }

                GraphicsState state = g.Save();
                try
                {
                    g.TranslateTransform(cx, cy);
                    g.RotateTransform((float)(decorRot[i] * 180.0 / Math.PI));
                    using (SolidBrush brush = new SolidBrush(decorColor[i]))
                    {
                        switch (decorKind[i])
                        {
                            case 0:
                                g.FillPolygon(brush, Sparkle(r));
                                break;
                            case 1:
                                g.FillPolygon(brush, Star(r, 5));
                                break;
                            case 2:
                                g.FillPolygon(brush, Bolt(r));
                                break;
                            case 3:
                                g.FillEllipse(brush, -r * 0.4f, -r * 0.45f, r * 0.8f, r * 0.8f);
                                g.FillEllipse(brush, r * 0.0f, -r * 0.45f, r * 0.8f, r * 0.8f);
                                g.FillPolygon(
                                    brush,
                                    new PointF[]
                                    {
                                        new PointF(-r * 0.42f, -r * 0.12f),
                                        new PointF(r * 0.42f, -r * 0.12f),
                                        new PointF(0f, r * 0.62f)
                                    });
                                break;
                            case 4:
                                using (Pen pen = new Pen(decorColor[i], Math.Max(2f, r * 0.26f)))
                                {
                                    g.DrawEllipse(pen, -r * 0.6f, -r * 0.6f, r * 1.2f, r * 1.2f);
                                }
                                break;
                        }
                    }
                }
                finally
                {
                    g.Restore(state);
                }
            }
        }

        private void DrawFace(Graphics g)
        {
            if (face <= 0)
            {
                return;
            }

            float w = ClientSize.Width;
            float h = ClientSize.Height;
            float eyeR = Math.Max(4f, h * 0.085f);
            float eyeY = h * 0.34f;
            float leftX = w * 0.37f;
            float rightX = w * 0.63f;

            if (face == 2)
            {
                // sunglasses
                using (SolidBrush frame = new SolidBrush(Color.FromArgb(235, 12, 12, 16)))
                {
                    g.FillRectangle(frame, leftX - eyeR * 1.1f, eyeY - eyeR * 1.2f, (rightX - leftX) + eyeR * 2.2f, eyeR * 0.34f);
                    g.FillEllipse(frame, leftX - eyeR * 1.35f, eyeY - eyeR * 0.95f, eyeR * 2.7f, eyeR * 1.9f);
                    g.FillEllipse(frame, rightX - eyeR * 1.35f, eyeY - eyeR * 0.95f, eyeR * 2.7f, eyeR * 1.9f);
                }
                using (SolidBrush shine = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
                {
                    g.FillEllipse(shine, leftX - eyeR * 0.9f, eyeY - eyeR * 0.6f, eyeR * 0.7f, eyeR * 0.5f);
                    g.FillEllipse(shine, rightX - eyeR * 0.9f, eyeY - eyeR * 0.6f, eyeR * 0.7f, eyeR * 0.5f);
                }
                return;
            }

            using (SolidBrush white = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
            using (SolidBrush pupil = new SolidBrush(Color.FromArgb(245, 16, 16, 20)))
            {
                g.FillEllipse(white, leftX - eyeR, eyeY - eyeR, eyeR * 2f, eyeR * 2f);
                g.FillEllipse(white, rightX - eyeR, eyeY - eyeR, eyeR * 2f, eyeR * 2f);
                g.FillEllipse(pupil, leftX - eyeR * 0.42f, eyeY - eyeR * 0.42f, eyeR * 0.84f, eyeR * 0.84f);
                g.FillEllipse(pupil, rightX - eyeR * 0.42f, eyeY - eyeR * 0.42f, eyeR * 0.84f, eyeR * 0.84f);
            }

            using (Pen pen = new Pen(Color.FromArgb(220, 16, 16, 20), Math.Max(2f, h * 0.028f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, w * 0.34f, h * 0.44f, w * 0.32f, h * 0.30f, 20f, 140f);
            }

            using (SolidBrush blush = new SolidBrush(Color.FromArgb(90, 255, 80, 130)))
            {
                g.FillEllipse(blush, w * 0.24f, h * 0.55f, w * 0.12f, h * 0.09f);
                g.FillEllipse(blush, w * 0.64f, h * 0.55f, w * 0.12f, h * 0.09f);
            }
        }

        private static PointF[] Sparkle(float r)
        {
            float s = r * 0.26f;
            return new PointF[]
            {
                new PointF(0f, -r),
                new PointF(s, -s),
                new PointF(r, 0f),
                new PointF(s, s),
                new PointF(0f, r),
                new PointF(-s, s),
                new PointF(-r, 0f),
                new PointF(-s, -s)
            };
        }

        private static PointF[] Star(float r, int points)
        {
            PointF[] pts = new PointF[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                double angle = (Math.PI / points) * i - Math.PI / 2.0;
                float radius = (i % 2 == 0) ? r : r * 0.45f;
                pts[i] = new PointF(
                    (float)(Math.Cos(angle) * radius),
                    (float)(Math.Sin(angle) * radius));
            }
            return pts;
        }

        private static PointF[] Bolt(float r)
        {
            return new PointF[]
            {
                new PointF(r * 0.20f, -r),
                new PointF(r * 0.62f, -r * 0.18f),
                new PointF(r * 0.16f, -r * 0.10f),
                new PointF(r * 0.52f, r),
                new PointF(-r * 0.20f, r * 0.08f),
                new PointF(r * 0.12f, -r * 0.02f),
                new PointF(-r * 0.34f, -r * 0.12f)
            };
        }
    }
}
