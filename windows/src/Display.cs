// Popup67 - screen detection.
//
// Goal: on every device the show fills the whole screen. That needs three
// layers of fallback, because a program can be given a "virtualised" (smaller)
// desktop by Windows, by a remote session or by a scaled display:
//
//   1. per-monitor-v2 DPI awareness (see Dpi.cs) so Screen.Bounds reports
//      real pixels on modern Windows;
//   2. a cross-check against the real video mode via EnumDisplaySettings,
//      which reports the true resolution even for a DPI unaware process;
//   3. a manual --area WxH escape hatch.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Popup67
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        public short SpecVersion;
        public short DriverVersion;
        public short Size;
        public short DriverExtra;
        public int Fields;
        public int PositionX;
        public int PositionY;
        public int DisplayOrientation;
        public int DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FormName;
        public short LogPixels;
        public int BitsPerPel;
        public int PelsWidth;
        public int PelsHeight;
        public int DisplayFlags;
        public int DisplayFrequency;
        public int IcmMethod;
        public int IcmIntent;
        public int MediaType;
        public int DitherType;
        public int Reserved1;
        public int Reserved2;
        public int PanningWidth;
        public int PanningHeight;
    }

    internal static class Display
    {
        private const int ENUM_CURRENT_SETTINGS = -1;

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DevMode devMode);

        public static int PhysicalWidth;
        public static int PhysicalHeight;
        public static bool Corrected;

        // Full screen rectangle in real pixels.
        public static Rectangle FullScreen(Options opt)
        {
            Rectangle bounds = opt.AllScreens ? SystemInformation.VirtualScreen : Screen.PrimaryScreen.Bounds;

            if (opt.AreaWidth >= 200 && opt.AreaHeight >= 200)
            {
                Corrected = true;
                PhysicalWidth = opt.AreaWidth;
                PhysicalHeight = opt.AreaHeight;
                return new Rectangle(bounds.Left, bounds.Top, opt.AreaWidth, opt.AreaHeight);
            }

            if (!opt.AllScreens && ReadVideoMode())
            {
                if (Math.Abs(bounds.Width - PhysicalWidth) > 2 || Math.Abs(bounds.Height - PhysicalHeight) > 2)
                {
                    // the process sees a virtualised desktop - use the real mode
                    Corrected = true;
                    return new Rectangle(bounds.Left, bounds.Top, PhysicalWidth, PhysicalHeight);
                }
            }

            return bounds;
        }

        // Where the popup windows are laid out. Full screen by default so the
        // burst covers everything, taskbar strip included.
        public static Rectangle LayoutArea(Options opt)
        {
            if (opt.LayoutWorkArea)
            {
                Rectangle work = opt.AllScreens ? SystemInformation.VirtualScreen : Screen.PrimaryScreen.WorkingArea;
                if (opt.AreaWidth >= 200 && opt.AreaHeight >= 200)
                {
                    work = new Rectangle(work.Left, work.Top, opt.AreaWidth, opt.AreaHeight);
                }
                return work;
            }

            return FullScreen(opt);
        }

        private static bool ReadVideoMode()
        {
            try
            {
                DevMode mode = new DevMode();
                mode.Size = (short)Marshal.SizeOf(typeof(DevMode));
                if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref mode))
                {
                    if (mode.PelsWidth >= 200 && mode.PelsHeight >= 200)
                    {
                        PhysicalWidth = mode.PelsWidth;
                        PhysicalHeight = mode.PelsHeight;
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        public static string Describe()
        {
            return Util.Fmt(
                "detected {0}x{1}{2}",
                PhysicalWidth,
                PhysicalHeight,
                Corrected ? " (corrected from virtualised metrics)" : "");
        }
    }
}
