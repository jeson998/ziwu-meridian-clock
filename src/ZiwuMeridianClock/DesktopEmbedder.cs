using System;
using System.Drawing;
using System.Windows.Forms;

namespace ZiwuMeridianClock
{
    /// <summary>
    /// 把窗口嵌入到桌面图标之后（WorkerW 技术）。
    /// 经典结构（Win10 / Win11 23H2 及更早）：给 Progman 发送 0x052C 后，SHELLDLL_DefView 移到一个顶层 WorkerW，
    ///   其后会再生成一个顶层 WorkerW（壁纸层）—— 我们 SetParent 到这个 WorkerW。
    /// Win11 24H2+ 结构：SHELLDLL_DefView 仍是 Progman 的子窗口，且 Progman 下有一个子 WorkerW（壁纸层）——
    ///   我们 SetParent 到 Progman，并把 z 序放在 DefView 之下、子 WorkerW 之上。
    /// </summary>
    internal static class DesktopEmbedder
    {
        public enum Kind { None, ClassicWorkerW, Progman24H2, ProgmanDirect }

        public sealed class Result
        {
            public bool Ok;
            public Kind Kind;
            public IntPtr Parent;
            public IntPtr DefView;
            public IntPtr ChildWorkerW;
            public override string ToString() => $"{Kind} parent=0x{Parent.ToInt64():X} defView=0x{DefView.ToInt64():X} workerW=0x{ChildWorkerW.ToInt64():X}";
        }

        /// <summary>找到壁纸层（不修改任何窗口）。</summary>
        public static Result Locate()
        {
            var r = new Result();
            IntPtr progman = Native.FindWindow("Progman", null);
            if (progman == IntPtr.Zero) { Log.Warn("未找到 Progman"); return r; }

            // 让 Progman 生成 WorkerW（两种参数都发一次，兼容不同版本）
            Native.SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(0x1), Native.SMTO_NORMAL, 1000, out _);
            Native.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, Native.SMTO_NORMAL, 1000, out _);

            // 1) 经典：找到包含 SHELLDLL_DefView 的顶层 WorkerW，其后的兄弟 WorkerW 即壁纸层
            IntPtr defViewHost = IntPtr.Zero, defView = IntPtr.Zero;
            Native.EnumWindows((top, _) =>
            {
                IntPtr dv = Native.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (dv != IntPtr.Zero) { defViewHost = top; defView = dv; return false; }
                return true;
            }, IntPtr.Zero);

            if (defViewHost != IntPtr.Zero && defViewHost != progman)
            {
                IntPtr w = Native.FindWindowEx(IntPtr.Zero, defViewHost, "WorkerW", null);
                if (w != IntPtr.Zero)
                {
                    r.Ok = true; r.Kind = Kind.ClassicWorkerW; r.Parent = w; r.DefView = defView;
                    return r;
                }
            }

            // 2) Win11 24H2+：DefView 与 WorkerW 都是 Progman 的子窗口
            IntPtr dv2 = Native.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            IntPtr cw = Native.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
            if (dv2 != IntPtr.Zero && cw != IntPtr.Zero)
            {
                r.Ok = true; r.Kind = Kind.Progman24H2; r.Parent = progman; r.DefView = dv2; r.ChildWorkerW = cw;
                return r;
            }

            // 3) 退一步：直接放进 Progman（图标之后）
            if (dv2 != IntPtr.Zero)
            {
                r.Ok = true; r.Kind = Kind.ProgmanDirect; r.Parent = progman; r.DefView = dv2;
                return r;
            }
            Log.Warn("未能识别桌面窗口结构");
            return r;
        }

        /// <summary>把 hwnd 嵌入壁纸层并铺满 screenBounds（物理像素、屏幕坐标）。</summary>
        public static Result Embed(IntPtr hwnd, Rectangle screenBounds)
        {
            Result r;
            try { r = Locate(); }
            catch (Exception ex) { Log.Error("定位桌面窗口失败", ex); return new Result(); }
            if (!r.Ok) return r;
            try
            {
                // 去掉 WS_EX_APPWINDOW，保持工具窗口/不激活
                long ex = Native.GetExStyle(hwnd);
                ex = (ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE) & ~Native.WS_EX_APPWINDOW;
                Native.SetExStyle(hwnd, ex);

                if (Native.SetParent(hwnd, r.Parent) == IntPtr.Zero)
                {
                    int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    if (err != 0) { Log.Warn("SetParent 失败, Win32 错误 " + err); r.Ok = false; return r; }
                }
                Position(hwnd, r, screenBounds);
                Log.Info("嵌入成功: " + r);
            }
            catch (Exception ex2) { Log.Error("嵌入失败", ex2); r.Ok = false; }
            return r;
        }

        /// <summary>按父窗口客户区坐标定位；24H2 下调整 z 序（DefView 之下、壁纸 WorkerW 之上）。</summary>
        public static void Position(IntPtr hwnd, Result r, Rectangle screenBounds)
        {
            var rc = new Native.RECT { Left = screenBounds.Left, Top = screenBounds.Top, Right = screenBounds.Right, Bottom = screenBounds.Bottom };
            Native.MapWindowPoints(IntPtr.Zero, r.Parent, ref rc, 2);
            int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
            if (r.Kind == Kind.Progman24H2 || r.Kind == Kind.ProgmanDirect)
            {
                // hWndInsertAfter = DefView：把窗口放在 DefView 之下（图标仍在最上面、可点击）
                Native.SetWindowPos(hwnd, r.DefView, rc.Left, rc.Top, w, h, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
                if (r.ChildWorkerW != IntPtr.Zero && Native.IsWindow(r.ChildWorkerW))
                    Native.SetWindowPos(r.ChildWorkerW, hwnd, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }
            else
            {
                Native.SetWindowPos(hwnd, IntPtr.Zero, rc.Left, rc.Top, w, h, Native.SWP_NOACTIVATE | Native.SWP_NOZORDER | Native.SWP_SHOWWINDOW);
            }
        }

        /// <summary>嵌入是否仍然有效（Explorer 重启后父窗口会消失）。</summary>
        public static bool StillValid(IntPtr hwnd, Result r)
        {
            if (r == null || !r.Ok) return false;
            if (!Native.IsWindow(hwnd) || !Native.IsWindow(r.Parent)) return false;
            if (Native.GetAncestor(hwnd, 1) != r.Parent) return false;
            if (r.DefView != IntPtr.Zero && !Native.IsWindow(r.DefView)) return false;
            return true;
        }
    }
}
