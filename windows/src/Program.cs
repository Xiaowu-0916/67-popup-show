// Popup67 - main entry point.

using System;
using System.Windows.Forms;

namespace Popup67
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // settings file first, command line on top
            string config = null;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], "--config", StringComparison.OrdinalIgnoreCase))
                {
                    config = args[i + 1];
                }
            }

            Options opt = new Options();
            opt.LoadFile(string.IsNullOrEmpty(config) ? Options.DefaultConfigPath() : config);
            Options.ApplyArgs(opt, args);
            opt.ConfigPath = config;

            // has to run before any window (or handle) exists
            Dpi.Enable(opt.DpiAware);

            if (opt.Help)
            {
                MessageBox.Show(Options.Usage(), "67", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!string.IsNullOrEmpty(opt.DumpLayout))
            {
                LayoutDump.Write(opt, opt.DumpLayout);
                return;
            }

            if (!string.IsNullOrEmpty(opt.DumpFinale))
            {
                FinaleDump.Write(opt, opt.DumpFinale);
                return;
            }

            if (!string.IsNullOrEmpty(opt.DumpFx))
            {
                FxDump.Write(opt, opt.DumpFx);
                return;
            }

            Director director = new Director(opt);
            Application.Run(director);
        }
    }
}
