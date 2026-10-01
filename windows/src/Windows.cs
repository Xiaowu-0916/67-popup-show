// Popup67 - the individual popup windows and their variety.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace Popup67
{
    internal sealed class SpamWindow
    {
        public Form Form;
        public Point Origin;
        public bool Wobble;
        public float Amp;
        public float Speed;
        public float Phase;
        public bool Vertical;
        public NumberCanvas Canvas;
        public double HueOffset;
        public DateTime PopStart;
        public bool Popping;

        public void ApplyKinetic(double t)
        {
            if (!Wobble || Form == null || Form.IsDisposed || !Form.Visible)
            {
                return;
            }

            double v = Amp * Math.Sin(Phase + t * Speed);
            int dx = Vertical ? 0 : (int)Math.Round(v);
            int dy = Vertical ? (int)Math.Round(v) : 0;
            Form.Location = new Point(Origin.X + dx, Origin.Y + dy);
        }
    }

    internal static class WindowFactory
    {
        private static readonly string[] Titles =
        {
            "67",
            "67!",
            "67 67",
            "6767",
            "6 7",
            "67 (1)",
            "67 (2)",
            "67 (3)",
            "67 - Copy",
            "67 - Copy (2)",
            "67.exe",
            "SIXTY SEVEN",
            "six seven",
            "sixty-seven",
            "67 everywhere",
            "67 is coming",
            "67 67 67",
            "happy 67"
        };

        public static SpamWindow Create(Rectangle bounds, Random rng, Palette palette, Director director)
        {
            bool tool;
            FormBorderStyle border = StyleFor(rng, out tool);

            Form form = new Form();
            form.SuspendLayout();
            form.Text = Titles[rng.Next(Titles.Length)];
            form.StartPosition = FormStartPosition.Manual;
            form.FormBorderStyle = border;
            form.ShowInTaskbar = !tool && rng.Next(100) < 28;
            form.TopMost = true;
            form.KeyPreview = true;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowIcon = rng.Next(100) < 26;
            form.ControlBox = rng.Next(100) < 80;
            form.BackColor = palette.Back;
            form.ForeColor = palette.Ink;
            form.Padding = new Padding(2);
            form.Bounds = bounds;

            if (!form.ControlBox)
            {
                form.Text = "";
            }

            FontChoice font = FontPicker.Next(rng);
            ArtStyle art = (ArtStyle)rng.Next(5);
            int pattern = rng.Next(100) < 45 ? 1 + rng.Next(3) : 0;

            int face = rng.Next(100) < 22 ? (rng.Next(2) == 0 ? 1 : 2) : 0;
            int chaos = rng.Next(100) < 15 ? 1 : 0;
            float tilt = rng.Next(100) < 30
                ? (float)((rng.NextDouble() - 0.5) * 20.0)
                : 0f;

            NumberCanvas canvas = new NumberCanvas(
                palette,
                font,
                art,
                pattern,
                face,
                chaos,
                tilt,
                rng.Next());
            canvas.Dock = DockStyle.Fill;
            form.Controls.Add(canvas);

            form.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    director.AbortAll("escape (popup)");
                }
            };

            canvas.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right)
                {
                    try
                    {
                        form.Close();
                    }
                    catch
                    {
                    }
                }
            };

            form.ResumeLayout(false);

            SpamWindow window = new SpamWindow();
            window.Form = form;
            window.Canvas = canvas;
            window.HueOffset = rng.Next(360);
            window.Origin = bounds.Location;
            return window;
        }

        private static FormBorderStyle StyleFor(Random rng, out bool tool)
        {
            tool = false;
            int k = rng.Next(100);
            if (k < 20)
            {
                return FormBorderStyle.None;
            }
            if (k < 40)
            {
                return FormBorderStyle.FixedSingle;
            }
            if (k < 56)
            {
                return FormBorderStyle.Sizable;
            }
            if (k < 69)
            {
                return FormBorderStyle.Fixed3D;
            }
            if (k < 81)
            {
                return FormBorderStyle.FixedDialog;
            }
            tool = true;
            return rng.Next(2) == 0
                ? FormBorderStyle.FixedToolWindow
                : FormBorderStyle.SizableToolWindow;
        }
    }
}
