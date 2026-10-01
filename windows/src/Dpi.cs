// Popup67 - DPI awareness.
//
// A process that is not DPI aware sees a virtualised (smaller) desktop on a
// scaled display, so windows created from those numbers can end up as a block
// in the top-left corner of the real screen. Declaring per-monitor-v2 awareness
// (with older fallbacks) makes Screen.Bounds report real pixels, and Dpi.Scale
// keeps absolute sizes at the same physical size.

using System;
using System.Runtime.InteropServices;

namespace Popup67
{
    internal static class Dpi
    {
        private const int LOGPIXELSX = 88;
        private const int PER_MONITOR_AWARE_V2 = -4;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("shcore.dll", SetLastError = true)]
        private static extern int SetProcessDpiAwareness(int value);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int index);

        public static int DpiValue = 96;
        public static double Scale = 1.0;
        public static string Method = "none";

        public static void Enable(bool enable)
        {
            if (!enable)
            {
                Method = "disabled";
                DpiValue = ReadDpi();
                Scale = DpiValue / 96.0;
                return;
            }

            try
            {
                if (SetProcessDpiAwarenessContext(new IntPtr(PER_MONITOR_AWARE_V2)))
                {
                    Method = "per-monitor-v2";
                }
            }
            catch
            {
            }

            if (Method == "none")
            {
                try
                {
                    if (SetProcessDpiAwareness(2) == 0)
                    {
                        Method = "per-monitor";
                    }
                }
                catch
                {
                }
            }

            if (Method == "none")
            {
                try
                {
                    if (SetProcessDPIAware())
                    {
                        Method = "system";
                    }
                }
                catch
                {
                }
            }

            if (Method == "none")
            {
                Method = "failed";
            }

            DpiValue = ReadDpi();
            Scale = DpiValue / 96.0;
        }

        public static int Scaled(int pixels)
        {
            return (int)Math.Round(pixels * Scale);
        }

        private static int ReadDpi()
        {
            try
            {
                int value = (int)GetDpiForSystem();
                if (value >= 48 && value <= 768)
                {
                    return value;
                }
            }
            catch
            {
            }

            IntPtr dc = IntPtr.Zero;
            try
            {
                dc = GetDC(IntPtr.Zero);
                int value = GetDeviceCaps(dc, LOGPIXELSX);
                if (value >= 48 && value <= 768)
                {
                    return value;
                }
            }
            catch
            {
            }
            finally
            {
                if (dc != IntPtr.Zero)
                {
                    ReleaseDC(IntPtr.Zero, dc);
                }
            }

            return 96;
        }
    }
}
