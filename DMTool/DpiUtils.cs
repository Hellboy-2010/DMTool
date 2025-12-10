using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;

namespace DMTool
{
    public static class DpiUtils
    {
        // P/Invoke definitions
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, Monitor_DPI_Type dpiType, out uint dpiX, out uint dpiY);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public enum Monitor_DPI_Type
        {
            MDT_Effective_DPI = 0,
            MDT_Angular_DPI = 1,
            MDT_Raw_DPI = 2,
            MDT_Default = MDT_Effective_DPI
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        /// <summary>
        /// Gets the DPI scaling factor for the specified screen.
        /// Returns 1.0 for 96 DPI (100%).
        /// </summary>
        public static double GetDpiScaleFactor(Screen screen)
        {
            try
            {
                var point = new POINT { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 };
                var hMonitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);

                if (GetDpiForMonitor(hMonitor, Monitor_DPI_Type.MDT_Effective_DPI, out uint dpiX, out uint dpiY) == 0)
                {
                    return dpiX / 96.0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting DPI: {ex.Message}");
            }

            return 1.0; // Default to 100%
        }

        /// <summary>
        /// Positions a Window using physical screen coordinates.
        /// This bypasses WPF's logical unit conversion issues during initial placement.
        /// </summary>
        public static void SetWindowPosition(Window window, int x, int y, int width, int height)
        {
            var helper = new WindowInteropHelper(window);
            SetWindowPos(helper.Handle, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }
        
        /// <summary>
        /// Converts physical pixels to logical units based on the screen's DPI.
        /// </summary>
        public static double PhysicalToLogical(int physicalPixels, double scaleFactor)
        {
            return physicalPixels / scaleFactor;
        }
    }
}
