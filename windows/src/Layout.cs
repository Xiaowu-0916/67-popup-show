// Popup67 - window placement: keeps adding popup rectangles until the union of
// their areas covers the requested share of the screen.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Popup67
{
    internal sealed class LayoutStats
    {
        public int Windows;
        public int CoveredCells;
        public int TotalCells;
        public double Coverage;
    }

    internal static class LayoutBuilder
    {
        public const int Cell = 8;

        public static List<Rectangle> Build(Rectangle area, Options opt, Random rng, out LayoutStats stats)
        {
            if (string.Equals(opt.Layout, "chaos", StringComparison.OrdinalIgnoreCase))
            {
                return BuildChaos(area, opt, rng, out stats);
            }
            if (string.Equals(opt.Layout, "grid", StringComparison.OrdinalIgnoreCase))
            {
                return BuildGrid(area, opt, rng, out stats);
            }
            return BuildScatter(area, opt, rng, out stats);
        }

        // Default layout: windows are thrown at fully random positions across
        // the whole screen (random size and random overlap), and then every one
        // of a 4x4 set of screen regions is checked so that each area really
        // does get a window. Random look, no empty corner.
        private static List<Rectangle> BuildScatter(Rectangle area, Options opt, Random rng, out LayoutStats stats)
        {
            double coverage = Util.Clamp(opt.Coverage, 0.20, 0.95);
            int goal = Util.Clamp(
                (int)Math.Round(coverage * 26.0),
                opt.MinWindows,
                opt.MaxWindows);

            double scaleFactor = (0.85 + 0.15 * Util.Clamp(opt.WindowScale, 0.5, 2.5))
                * (coverage / 0.75);
            scaleFactor = Util.Clamp(scaleFactor, 0.45, 2.2);

            int gw = Math.Max(1, (area.Width + Cell - 1) / Cell);
            int gh = Math.Max(1, (area.Height + Cell - 1) / Cell);
            int total = gw * gh;
            bool[] covered = new bool[total];
            int coveredCells = 0;
            List<Rectangle> placed = new List<Rectangle>();

            for (int i = 0; i < goal; i++)
            {
                Rectangle rect = RandomWindow(area, rng, scaleFactor, 1.0, 1.0);
                coveredCells += CountFresh(rect, area, covered, gw, gh);
                MarkCovered(rect, area, covered, gw, gh);
                placed.Add(rect);
            }

            // region guarantee: 4x4 areas must each contain a window centre
            const int regions = 4;
            bool[,] filled = new bool[regions, regions];
            for (int i = 0; i < placed.Count; i++)
            {
                int cx = placed[i].Left + placed[i].Width / 2;
                int cy = placed[i].Top + placed[i].Height / 2;
                int rx = (int)Math.Min(regions - 1, Math.Max(0, (cx - area.Left) * regions / Math.Max(1, area.Width)));
                int ry = (int)Math.Min(regions - 1, Math.Max(0, (cy - area.Top) * regions / Math.Max(1, area.Height)));
                filled[rx, ry] = true;
            }

            for (int ry = 0; ry < regions; ry++)
            {
                for (int rx = 0; rx < regions; rx++)
                {
                    if (filled[rx, ry] || placed.Count >= opt.MaxWindows)
                    {
                        continue;
                    }

                    double tx = (rx + 0.2 + rng.NextDouble() * 0.6) / regions;
                    double ty = (ry + 0.2 + rng.NextDouble() * 0.6) / regions;
                    Rectangle rect = RandomWindow(area, rng, scaleFactor, tx, ty);
                    coveredCells += CountFresh(rect, area, covered, gw, gh);
                    MarkCovered(rect, area, covered, gw, gh);
                    placed.Add(rect);
                    filled[rx, ry] = true;
                }
            }

            stats = new LayoutStats();
            stats.Windows = placed.Count;
            stats.CoveredCells = coveredCells;
            stats.TotalCells = total;
            stats.Coverage = coveredCells / (double)total;
            return placed;
        }

        // One random window: mixed size classes, random aspect, random centre
        // (tx/ty 0..1 pin the centre, 1.0 means "anywhere").
        private static Rectangle RandomWindow(Rectangle area, Random rng, double scaleFactor, double tx, double ty)
        {
            double roll = rng.NextDouble();
            double areaFraction;
            if (roll < 0.30)
            {
                areaFraction = 0.060 + rng.NextDouble() * 0.055;   // big
            }
            else if (roll < 0.72)
            {
                areaFraction = 0.028 + rng.NextDouble() * 0.022;   // medium
            }
            else
            {
                areaFraction = 0.012 + rng.NextDouble() * 0.012;   // small
            }
            areaFraction *= scaleFactor;

            double aspect = 1.10 + rng.NextDouble() * 1.50;
            double widthFactor = Math.Sqrt(areaFraction * aspect);
            double heightFactor = areaFraction / Math.Max(0.02, widthFactor);

            int w = Util.Clamp(
                (int)Math.Round(widthFactor * area.Width),
                Dpi.Scaled(130),
                Dpi.Scaled(1100));
            int h = Util.Clamp(
                (int)Math.Round(heightFactor * area.Height),
                Dpi.Scaled(85),
                Dpi.Scaled(700));

            double cxRatio = tx >= 1.0 ? rng.NextDouble() : tx + (rng.NextDouble() - 0.5) * 0.16;
            double cyRatio = ty >= 1.0 ? rng.NextDouble() : ty + (rng.NextDouble() - 0.5) * 0.16;
            int cx = area.Left + (int)Math.Round(Util.Clamp(cxRatio, 0.0, 1.0) * area.Width);
            int cy = area.Top + (int)Math.Round(Util.Clamp(cyRatio, 0.0, 1.0) * area.Height);

            int maxOutX = (int)(w * 0.20);
            int maxOutY = (int)(h * 0.20);
            return new Rectangle(
                Util.Clamp(cx - w / 2, area.Left - maxOutX, area.Right - w + maxOutX),
                Util.Clamp(cy - h / 2, area.Top - maxOutY, area.Bottom - h + maxOutY),
                w,
                h);
        }

        // Default: stratified random. The screen is cut into regions and every
        // region gets one window at a random spot with a random size, so the
        // burst reaches every corner of the screen while still looking random.
        // Sizes are derived from the coverage target, so overlaps are just as
        // random as the positions.
        private static List<Rectangle> BuildScatterOld(Rectangle area, Options opt, Random rng, out LayoutStats stats)
        {
            double coverage = Util.Clamp(opt.Coverage, 0.20, 0.95);
            int goal = Util.Clamp(
                (int)Math.Round(coverage * 26.0),
                opt.MinWindows,
                opt.MaxWindows);

            int cols = 4;
            int rows = 4;
            if (goal >= 30)
            {
                cols = 4;
                rows = 2;
            }
            else
            {
                cols = 3;
                rows = 2;
            }
            if (goal >= 45)
            {
                cols = 4;
                rows = 3;
            }
            if (area.Width < area.Height)
            {
                int swap = cols;
                cols = rows;
                rows = swap;
            }

            int windows = Util.Clamp(goal, opt.MinWindows, opt.MaxWindows);
            int regions = cols * rows;
            double scaleFactor = (0.85 + 0.15 * Util.Clamp(opt.WindowScale, 0.5, 2.5))
                * (coverage / 0.75);
            scaleFactor = Util.Clamp(scaleFactor, 0.45, 2.2);

            int gw = Math.Max(1, (area.Width + Cell - 1) / Cell);
            int gh = Math.Max(1, (area.Height + Cell - 1) / Cell);
            int total = gw * gh;
            bool[] covered = new bool[total];
            int coveredCells = 0;
            List<Rectangle> placed = new List<Rectangle>();

            for (int row = 0; row < rows; row++)
            {
                int y0 = area.Top + (int)((long)row * area.Height / rows);
                int y1 = area.Top + (int)((long)(row + 1) * area.Height / rows);
                int regionH = Math.Max(1, y1 - y0);

                for (int col = 0; col < cols; col++)
                {
                    int x0 = area.Left + (int)((long)col * area.Width / cols);
                    int x1 = area.Left + (int)((long)(col + 1) * area.Width / cols);
                    int regionW = Math.Max(1, x1 - x0);

                    int perRegion = windows / regions;
                    if ((row * cols + col) < (windows % regions))
                    {
                        perRegion++;
                    }

                    for (int k = 0; k < perRegion; k++)
                    {
                        double roll = rng.NextDouble();
                        double areaFraction;
                        if (roll < 0.34)
                        {
                            areaFraction = 0.060 + rng.NextDouble() * 0.050;   // big
                        }
                        else if (roll < 0.74)
                        {
                            areaFraction = 0.030 + rng.NextDouble() * 0.024;   // medium
                        }
                        else
                        {
                            areaFraction = 0.014 + rng.NextDouble() * 0.013;   // small
                        }
                        areaFraction *= scaleFactor;

                        double aspect = 1.10 + rng.NextDouble() * 1.45;
                        double widthFactor = Math.Sqrt(areaFraction * aspect);
                        double heightFactor = areaFraction / Math.Max(0.02, widthFactor);

                        int w = Util.Clamp(
                            (int)Math.Round(widthFactor * area.Width),
                            Dpi.Scaled(140),
                            Dpi.Scaled(1100));
                        int h = Util.Clamp(
                            (int)Math.Round(heightFactor * area.Height),
                            Dpi.Scaled(90),
                            Dpi.Scaled(700));

                        // centres may leave their region, which is what makes the
                        // burst look random instead of gridded
                        int cx = x0 + (int)Math.Round((rng.NextDouble() * 1.5 - 0.25) * regionW);
                        int cy = y0 + (int)Math.Round((rng.NextDouble() * 1.5 - 0.25) * regionH);

                        int maxOutX = (int)(w * 0.18);
                        int maxOutY = (int)(h * 0.18);
                        Rectangle rect = new Rectangle(
                            Util.Clamp(cx - w / 2, area.Left - maxOutX, area.Right - w + maxOutX),
                            Util.Clamp(cy - h / 2, area.Top - maxOutY, area.Bottom - h + maxOutY),
                            w,
                            h);

                        coveredCells += CountFresh(rect, area, covered, gw, gh);
                        MarkCovered(rect, area, covered, gw, gh);
                        placed.Add(rect);
                    }
                }
            }

            stats = new LayoutStats();
            stats.Windows = placed.Count;
            stats.CoveredCells = coveredCells;
            stats.TotalCells = total;
            stats.Coverage = coveredCells / (double)total;
            return placed;
        }

        // Even coverage: the screen is cut into cells, every cell gets one
        // window that is jittered inside the cell, so the burst spreads over
        // the whole screen with a visible gap between neighbours.
        private static List<Rectangle> BuildGrid(Rectangle area, Options opt, Random rng, out LayoutStats stats)
        {
            double coverage = Util.Clamp(opt.Coverage, 0.20, 0.95);
            int goal = Util.Clamp(
                (int)Math.Round(coverage * 30.0),
                opt.MinWindows,
                opt.MaxWindows);

            int bestCols = 4;
            int bestRows = 4;
            double bestScore = double.MaxValue;
            for (int cols = 2; cols <= 12; cols++)
            {
                int rows = (int)Math.Round(goal / (double)cols);
                if (rows < 2)
                {
                    rows = 2;
                }
                if (rows > 12)
                {
                    continue;
                }

                int count = cols * rows;
                if (count < opt.MinWindows || count > opt.MaxWindows)
                {
                    continue;
                }

                double cellW = area.Width / (double)cols;
                double cellH = area.Height / (double)rows;
                double ratio = cellW / cellH;
                double score = Math.Abs(ratio - 1.6)
                    + (ratio < 1.0 ? 1.0 : 0.0)
                    + Math.Abs(count - goal) * 0.02;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestCols = cols;
                    bestRows = rows;
                }
            }

            // per-axis fill ratio: sqrt turns an area ratio into a side ratio
            double fill = Math.Sqrt(coverage) * (0.92 + 0.08 * Util.Clamp(opt.WindowScale, 0.5, 2.5));
            fill = Util.Clamp(fill, 0.42, 0.93);

            int gw = Math.Max(1, (area.Width + Cell - 1) / Cell);
            int gh = Math.Max(1, (area.Height + Cell - 1) / Cell);
            int total = gw * gh;
            bool[] covered = new bool[total];
            int coveredCells = 0;

            List<Rectangle> placed = new List<Rectangle>();
            for (int row = 0; row < bestRows; row++)
            {
                int y0 = area.Top + (int)((long)row * area.Height / bestRows);
                int y1 = area.Top + (int)((long)(row + 1) * area.Height / bestRows);
                int cellH = y1 - y0;

                for (int col = 0; col < bestCols; col++)
                {
                    int x0 = area.Left + (int)((long)col * area.Width / bestCols);
                    int x1 = area.Left + (int)((long)(col + 1) * area.Width / bestCols);
                    int cellW = x1 - x0;

                    double fillW = Util.Clamp(fill + (rng.NextDouble() - 0.5) * 0.12, 0.40, 0.94);
                    double fillH = Util.Clamp(fill + (rng.NextDouble() - 0.5) * 0.12, 0.40, 0.94);

                    int w = (int)Math.Round(cellW * fillW);
                    int h = (int)Math.Round(cellH * fillH);

                    int minW = Math.Min(Dpi.Scaled(150), Math.Max(60, cellW - Dpi.Scaled(6)));
                    int minH = Math.Min(Dpi.Scaled(100), Math.Max(50, cellH - Dpi.Scaled(6)));
                    w = Util.Clamp(w, minW, Math.Max(minW, cellW - Dpi.Scaled(6)));
                    h = Util.Clamp(h, minH, Math.Max(minH, cellH - Dpi.Scaled(6)));

                    int slackX = Math.Max(0, cellW - w);
                    int slackY = Math.Max(0, cellH - h);
                    Rectangle rect = new Rectangle(
                        x0 + (int)Math.Round(rng.NextDouble() * slackX),
                        y0 + (int)Math.Round(rng.NextDouble() * slackY),
                        w,
                        h);

                    coveredCells += CountFresh(rect, area, covered, gw, gh);
                    MarkCovered(rect, area, covered, gw, gh);
                    placed.Add(rect);
                }
            }

            stats = new LayoutStats();
            stats.Windows = placed.Count;
            stats.CoveredCells = coveredCells;
            stats.TotalCells = total;
            stats.Coverage = coveredCells / (double)total;
            return placed;
        }

        // The older, deliberately messy placement (--layout chaos).
        private static List<Rectangle> BuildChaos(Rectangle area, Options opt, Random rng, out LayoutStats stats)
        {
            List<Rectangle> placed = new List<Rectangle>();
            int gw = Math.Max(1, (area.Width + Cell - 1) / Cell);
            int gh = Math.Max(1, (area.Height + Cell - 1) / Cell);
            int total = gw * gh;
            bool[] covered = new bool[total];
            int coveredCells = 0;

            while (placed.Count < opt.MaxWindows)
            {
                double coverage = coveredCells / (double)total;
                if (coverage >= opt.Coverage && placed.Count >= opt.MinWindows)
                {
                    break;
                }

                double remaining = opt.Coverage - coverage;
                double desired = Util.Clamp(remaining / 9.0, 0.028, 0.100);

                Rectangle best = Rectangle.Empty;
                double bestScore = double.MaxValue;
                int bestFresh = 0;

                for (int k = 0; k < 30; k++)
                {
                    Rectangle candidate = RandomRect(area, rng, opt.WindowScale);
                    int fresh = CountFresh(candidate, area, covered, gw, gh);
                    double gain = fresh / (double)total;
                    double score = Math.Abs(gain - desired) + rng.NextDouble() * 0.0045;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                        bestFresh = fresh;
                    }
                }

                if (best.Width <= 0 || bestFresh <= 0)
                {
                    if (placed.Count >= opt.MinWindows)
                    {
                        break;
                    }
                    best = new Rectangle(
                        area.Left + area.Width / 2 - Dpi.Scaled(170),
                        area.Top + area.Height / 2 - Dpi.Scaled(95),
                        Dpi.Scaled(340),
                        Dpi.Scaled(190));
                    bestFresh = CountFresh(best, area, covered, gw, gh);
                    if (bestFresh <= 0)
                    {
                        placed.Add(best);
                        break;
                    }
                }

                MarkCovered(best, area, covered, gw, gh);
                coveredCells += bestFresh;
                placed.Add(best);
            }

            stats = new LayoutStats();
            stats.Windows = placed.Count;
            stats.CoveredCells = coveredCells;
            stats.TotalCells = total;
            stats.Coverage = coveredCells / (double)total;
            return placed;
        }

        private static Rectangle RandomRect(Rectangle area, Random rng, double scale)
        {
            int kind = rng.Next(100);
            double widthFactor;
            double heightFactor;

            if (kind < 12)
            {
                widthFactor = 0.15 + rng.NextDouble() * 0.08;
                heightFactor = 0.38 + rng.NextDouble() * 0.16;
            }
            else if (kind < 25)
            {
                widthFactor = 0.42 + rng.NextDouble() * 0.20;
                heightFactor = 0.15 + rng.NextDouble() * 0.08;
            }
            else
            {
                widthFactor = 0.21 + rng.NextDouble() * 0.17;
                heightFactor = 0.19 + rng.NextDouble() * 0.16;
            }

            widthFactor *= scale;
            heightFactor *= scale;

            int w = Util.Clamp(
                (int)Math.Round(area.Width * widthFactor),
                Dpi.Scaled(240),
                Math.Max(Dpi.Scaled(240), (int)(area.Width * 0.78)));
            int h = Util.Clamp(
                (int)Math.Round(area.Height * heightFactor),
                Dpi.Scaled(160),
                Math.Max(Dpi.Scaled(160), (int)(area.Height * 0.72)));
            w = Math.Min(w, area.Width);
            h = Math.Min(h, area.Height);

            int maxX = Math.Max(area.Left, area.Right - w);
            int maxY = Math.Max(area.Top, area.Bottom - h);
            int spanX = Math.Max(1, area.Width - w + 1);
            int spanY = Math.Max(1, area.Height - h + 1);
            int edge = Math.Min(90, Math.Max(1, Math.Min(spanX, spanY)));
            int x;
            int y;
            int mode = rng.Next(100);

            if (mode < 20)
            {
                x = area.Left + rng.Next(0, Math.Min(edge + 1, spanX));
                y = area.Top + rng.Next(0, Math.Min(edge + 1, spanY));
            }
            else if (mode < 40)
            {
                x = maxX - rng.Next(0, Math.Min(edge + 1, spanX));
                y = maxY - rng.Next(0, Math.Min(edge + 1, spanY));
            }
            else if (mode < 52)
            {
                x = area.Left + rng.Next(0, Math.Min(edge + 1, spanX));
                y = area.Top + rng.Next(0, spanY);
            }
            else if (mode < 64)
            {
                x = maxX - rng.Next(0, Math.Min(edge + 1, spanX));
                y = area.Top + rng.Next(0, spanY);
            }
            else
            {
                x = area.Left + rng.Next(0, spanX);
                y = area.Top + rng.Next(0, spanY);
            }

            // A fifth of the windows are allowed to hang over the screen edge so
            // the burst looks messier than a tidy grid.
            if (rng.Next(100) < 20)
            {
                x += (int)Math.Round((rng.NextDouble() - 0.5) * w * 0.30);
                y += (int)Math.Round((rng.NextDouble() - 0.5) * h * 0.30);
            }

            return new Rectangle(x, y, w, h);
        }

        private static int CountFresh(Rectangle r, Rectangle area, bool[] covered, int gw, int gh)
        {
            int x0 = Util.Clamp((r.Left - area.Left) / Cell, 0, gw - 1);
            int x1 = Util.Clamp((r.Right - 1 - area.Left) / Cell, 0, gw - 1);
            int y0 = Util.Clamp((r.Top - area.Top) / Cell, 0, gh - 1);
            int y1 = Util.Clamp((r.Bottom - 1 - area.Top) / Cell, 0, gh - 1);
            int fresh = 0;

            for (int y = y0; y <= y1; y++)
            {
                int row = y * gw;
                for (int x = x0; x <= x1; x++)
                {
                    if (!covered[row + x])
                    {
                        fresh++;
                    }
                }
            }
            return fresh;
        }

        private static void MarkCovered(Rectangle r, Rectangle area, bool[] covered, int gw, int gh)
        {
            int x0 = Util.Clamp((r.Left - area.Left) / Cell, 0, gw - 1);
            int x1 = Util.Clamp((r.Right - 1 - area.Left) / Cell, 0, gw - 1);
            int y0 = Util.Clamp((r.Top - area.Top) / Cell, 0, gh - 1);
            int y1 = Util.Clamp((r.Bottom - 1 - area.Top) / Cell, 0, gh - 1);

            for (int y = y0; y <= y1; y++)
            {
                int row = y * gw;
                for (int x = x0; x <= x1; x++)
                {
                    covered[row + x] = true;
                }
            }
        }
    }

    // Renders the computed layout to a PNG. Used for verification/QA.
    internal static class LayoutDump
    {
        public static void Write(Options opt, string path)
        {
            Rectangle area = Display.LayoutArea(opt);
            Random rng = new Random(opt.Seed == 0 ? 20261001 : opt.Seed);

            LayoutStats stats;
            List<Rectangle> rects = LayoutBuilder.Build(area, opt, rng, out stats);

            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            int footer = 44;
            using (Bitmap bmp = new Bitmap(area.Width, area.Height + footer, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(255, 14, 16, 22));

                    for (int i = 0; i < rects.Count; i++)
                    {
                        Rectangle r = rects[i];
                        r.Offset(-area.Left, -area.Top);
                        Color baseColor = Palette.BackAt(i);
                        int bar = Math.Max(12, r.Height / 9);

                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(220, baseColor)))
                        {
                            g.FillRectangle(brush, r);
                        }
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(235, Util.Darken(baseColor, 0.42))))
                        {
                            g.FillRectangle(brush, new Rectangle(r.Left, r.Top, r.Width, bar));
                        }
                        using (Pen pen = new Pen(Color.FromArgb(235, 255, 255, 255), 1f))
                        {
                            g.DrawRectangle(pen, r.Left, r.Top, r.Width - 1, r.Height - 1);
                        }
                        using (Font f = new Font(
                            "Arial Black",
                            Math.Max(10f, Math.Min(r.Height * 0.34f, r.Width * 0.40f)),
                            FontStyle.Bold,
                            GraphicsUnit.Pixel))
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                        using (StringFormat sf = new StringFormat())
                        {
                            sf.Alignment = StringAlignment.Center;
                            sf.LineAlignment = StringAlignment.Center;
                            g.DrawString(
                                "67",
                                f,
                                brush,
                                new RectangleF(r.Left, r.Top + bar, r.Width, Math.Max(6, r.Height - bar)),
                                sf);
                        }
                        using (Font f = new Font("Consolas", 11f, GraphicsUnit.Pixel))
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(230, 20, 20, 26)))
                        {
                            g.DrawString(
                                (i + 1).ToString(CultureInfo.InvariantCulture),
                                f,
                                brush,
                                r.Left + 4,
                                r.Top + 3);
                        }
                    }

                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, 9, 11, 15)))
                    {
                        g.FillRectangle(brush, 0, area.Height, area.Width, footer);
                    }

                    string line = Util.Fmt(
                        "windows={0}   union coverage={1:0.0}%   target={2:0}%   screen={3}x{4}   seed={5}",
                        stats.Windows,
                        stats.Coverage * 100.0,
                        opt.Coverage * 100.0,
                        area.Width,
                        area.Height,
                        opt.Seed == 0 ? 20261001 : opt.Seed);

                    using (Font f = new Font("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, 226, 232, 240)))
                    {
                        g.DrawString(line, f, brush, 14f, area.Height + 13f);
                    }
                }
                bmp.Save(full, ImageFormat.Png);
            }

            File.WriteAllText(
                full + ".txt",
                Util.Fmt(
                    "windows={0}{1}coverage={2:0.00}%{1}target={3:0.00}%{1}coveredCells={4}/{5}{1}screen={6}x{7}{1}cell={8}px{1}",
                    stats.Windows,
                    Environment.NewLine,
                    stats.Coverage * 100.0,
                    opt.Coverage * 100.0,
                    stats.CoveredCells,
                    stats.TotalCells,
                    area.Width,
                    area.Height,
                    LayoutBuilder.Cell),
                new UTF8Encoding(false));
        }
    }
}
