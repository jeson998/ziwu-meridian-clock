using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ZiwuMeridianClock
{
    internal static class Native
    {
        public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        public const long WS_CHILD = 0x40000000L, WS_POPUP = 0x80000000L, WS_VISIBLE = 0x10000000L;
        public const long WS_EX_TRANSPARENT = 0x20L, WS_EX_LAYERED = 0x80000L, WS_EX_NOACTIVATE = 0x08000000L,
                          WS_EX_TOOLWINDOW = 0x80L, WS_EX_APPWINDOW = 0x40000L;
        public const uint LWA_ALPHA = 0x2;
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10,
                          SWP_SHOWWINDOW = 0x40, SWP_FRAMECHANGED = 0x20, SWP_NOOWNERZORDER = 0x200;
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        public const uint SMTO_NORMAL = 0x0;
        public const int WM_WINDOWPOSCHANGING = 0x0046, WM_MOUSEACTIVATE = 0x0021, WM_DISPLAYCHANGE = 0x007E,
                         WM_SETTINGCHANGE = 0x001A, WM_NCHITTEST = 0x0084, WM_DPICHANGED = 0x02E0;
        public const int MA_NOACTIVATE = 3, HTTRANSPARENT = -1;
        public const uint MONITOR_DEFAULTTONEAREST = 2;
        public const uint SPI_GETDESKWALLPAPER = 0x0073, SPI_SETDESKWALLPAPER = 0x0014;

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPOS { public IntPtr hwnd, hwndInsertAfter; public int x, y, cx, cy; public uint flags; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
            public int BatteryLifeTime, BatteryFullLifeTime;
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowTitle);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
        [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int idx);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int idx, IntPtr val);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte alpha, uint flags);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFO mi);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern int MapWindowPoints(IntPtr from, IntPtr to, ref RECT r, int cPoints);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string msg);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint action, uint param, StringBuilder pv, uint winIni);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint action, uint param, string pv, uint winIni);
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);
        [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hMon, int dpiType, out uint dpiX, out uint dpiY);

        public static string ClassOf(IntPtr h)
        {
            if (h == IntPtr.Zero) return "";
            var sb = new StringBuilder(256);
            try { GetClassName(h, sb, sb.Capacity); } catch { }
            return sb.ToString();
        }

        public static long GetExStyle(IntPtr h) => GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
        public static void SetExStyle(IntPtr h, long v) => SetWindowLongPtr(h, GWL_EXSTYLE, new IntPtr(v));

        /// <summary>显示器 DPI 缩放（1.0 = 100%）。</summary>
        public static double ScaleForRect(System.Drawing.Rectangle r)
        {
            try
            {
                var mon = MonitorFromPoint(new POINT { X = r.Left + r.Width / 2, Y = r.Top + r.Height / 2 }, MONITOR_DEFAULTTONEAREST);
                if (mon != IntPtr.Zero && GetDpiForMonitor(mon, 0, out uint dx, out _) == 0 && dx > 0) return dx / 96.0;
            }
            catch (Exception ex) { Log.Warn("GetDpiForMonitor 失败: " + ex.Message); }
            return 1.0;
        }

        /// <summary>重新设置当前壁纸，清除嵌入窗口退出后残留的画面。</summary>
        public static void RefreshWallpaper()
        {
            try
            {
                var sb = new StringBuilder(520);
                if (SystemParametersInfo(SPI_GETDESKWALLPAPER, (uint)sb.Capacity, sb, 0))
                    SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, sb.ToString(), 0);
            }
            catch (Exception ex) { Log.Warn("刷新壁纸失败: " + ex.Message); }
        }
    }
}
