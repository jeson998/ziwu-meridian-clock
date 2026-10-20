using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ZiwuMeridianClock
{
    /// <summary>单个显示器上的壁纸窗口：无边框、不激活、不进任务栏，内嵌 WebView2。</summary>
    internal sealed class WallpaperForm : Form
    {
        public const string Host = "ziwu.local";
        public Screen TargetScreen { get; }
        public Rectangle TargetBounds { get; private set; }
        public DesktopEmbedder.Result Embed { get; private set; }
        public bool FallbackMode { get; private set; }
        public bool WebReady { get; private set; }
        public bool Paused { get; private set; }
        public int CurrentFps { get; private set; } = -1;

        private readonly WebView2 _web;
        private readonly string _webDir;
        private string _quality;
        private int _spin = 60;
        public static string UrlExtra = "";

        public WallpaperForm(Screen screen, string webDir, string quality, int spin = 60)
        {
            _spin = spin;
            TargetScreen = screen;
            TargetBounds = screen.Bounds;
            _webDir = webDir;
            _quality = quality;

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Text = "子午流注·经络全息钟 壁纸";
            Bounds = TargetBounds;

            _web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };
            Controls.Add(_web);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= (int)(Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            try
            {
                if (m.Msg == Native.WM_MOUSEACTIVATE) { m.Result = new IntPtr(Native.MA_NOACTIVATE); return; }
                if (FallbackMode && m.Msg == Native.WM_NCHITTEST) { m.Result = new IntPtr(Native.HTTRANSPARENT); return; }
                if (FallbackMode && m.Msg == Native.WM_WINDOWPOSCHANGING && m.LParam != IntPtr.Zero)
                {
                    // 兼容模式：始终保持在最底层
                    var wp = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.WINDOWPOS>(m.LParam);
                    wp.hwndInsertAfter = Native.HWND_BOTTOM;
                    wp.flags &= ~Native.SWP_NOZORDER;
                    System.Runtime.InteropServices.Marshal.StructureToPtr(wp, m.LParam, false);
                }
            }
            catch (Exception ex) { Log.Warn("WndProc: " + ex.Message); }
            base.WndProc(ref m);
        }

        /// <summary>创建句柄并尝试嵌入桌面；失败则切换到兼容模式。</summary>
        public void Attach(bool forceFallback)
        {
            IntPtr h = Handle; // 强制创建句柄
            if (!forceFallback)
            {
                Embed = DesktopEmbedder.Embed(h, TargetBounds);
                if (Embed.Ok) { FallbackMode = false; Show(); DesktopEmbedder.Position(h, Embed, TargetBounds); return; }
                Log.Warn("嵌入桌面失败，切换到兼容模式（置底 + 鼠标穿透）");
            }
            EnterFallback();
        }

        private void EnterFallback()
        {
            FallbackMode = true;
            IntPtr h = Handle;
            long ex = Native.GetExStyle(h);
            ex |= Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
            ex &= ~Native.WS_EX_APPWINDOW;
            Native.SetExStyle(h, ex);
            Native.SetLayeredWindowAttributes(h, 0, 255, Native.LWA_ALPHA);
            Show();
            Native.SetWindowPos(h, Native.HWND_BOTTOM, TargetBounds.Left, TargetBounds.Top, TargetBounds.Width, TargetBounds.Height,
                Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW | Native.SWP_FRAMECHANGED);
            Log.Info($"兼容模式窗口: {TargetBounds}");
        }

        public void Reposition()
        {
            try
            {
                TargetBounds = TargetScreen.Bounds;
                if (!FallbackMode && Embed != null && Embed.Ok) DesktopEmbedder.Position(Handle, Embed, TargetBounds);
                else Native.SetWindowPos(Handle, Native.HWND_BOTTOM, TargetBounds.Left, TargetBounds.Top, TargetBounds.Width, TargetBounds.Height, Native.SWP_NOACTIVATE);
                _web?.CoreWebView2Controller_NotifyParentMoved();
            }
            catch (Exception ex) { Log.Warn("Reposition: " + ex.Message); }
        }

        public bool IsHealthy()
        {
            try
            {
                if (IsDisposed || !IsHandleCreated || !Native.IsWindow(Handle)) return false;
                if (!FallbackMode) return DesktopEmbedder.StillValid(Handle, Embed);
                return true;
            }
            catch { return false; }
        }

        // ---------------- WebView2 ----------------
        public async Task InitWebAsync(CoreWebView2Environment env)
        {
            try
            {
                await _web.EnsureCoreWebView2Async(env);
                var core = _web.CoreWebView2;
                var s = core.Settings;
                s.AreDefaultContextMenusEnabled = false;
                s.AreDevToolsEnabled = Environment.GetEnvironmentVariable("ZIWU_DEVTOOLS") == "1";
                s.IsZoomControlEnabled = false;
                s.IsStatusBarEnabled = false;
                s.AreBrowserAcceleratorKeysEnabled = false;
                s.IsPinchZoomEnabled = false;
                s.IsSwipeNavigationEnabled = false;
                s.IsGeneralAutofillEnabled = false;
                s.IsPasswordAutosaveEnabled = false;
                core.SetVirtualHostNameToFolderMapping(Host, _webDir, CoreWebView2HostResourceAccessKind.Allow);
                // 自己提供文件并显式设置 MIME：部分电脑注册表把 .js 标成 text/plain，会导致 ES 模块被拦截
                core.AddWebResourceRequestedFilter("https://" + Host + "/*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += (o, e) =>
                {
                    try { var resp = LocalFiles.Serve(env, _webDir, e.Request.Uri); if (resp != null) e.Response = resp; }
                    catch (Exception ex) { Log.Warn("WebResourceRequested: " + ex.Message); }
                };
                core.NavigationCompleted += (o, e) =>
                {
                    if (e.IsSuccess) { WebReady = true; Log.Info("页面加载完成 " + TargetScreen.DeviceName); ReapplyState(); }
                    else Log.Warn("页面加载失败: " + e.WebErrorStatus);
                };
                core.ProcessFailed += (o, e) =>
                {
                    Log.Warn($"WebView2 进程异常: {e.ProcessFailedKind} {e.Reason}");
                    if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited ||
                        e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
                    {
                        try { BeginInvoke(new Action(() => { try { _web.CoreWebView2?.Reload(); } catch { } })); } catch { }
                    }
                };
                core.NewWindowRequested += (o, e) => { e.Handled = true; };
                Navigate();
            }
            catch (Exception ex) { Log.Error("WebView2 初始化失败", ex); }
        }

        private string BuildUrl()
        {
            // 任务栏/工作区内边距（换算成 CSS 像素），保证表盘与面板不被任务栏遮挡
            var b = TargetScreen.Bounds; var wa = TargetScreen.WorkingArea;
            double sc = Native.ScaleForRect(b);
            int it = (int)Math.Round(Math.Max(0, wa.Top - b.Top) / sc);
            int ib = (int)Math.Round(Math.Max(0, b.Bottom - wa.Bottom) / sc);
            int il = (int)Math.Round(Math.Max(0, wa.Left - b.Left) / sc);
            int ir = (int)Math.Round(Math.Max(0, b.Right - wa.Right) / sc);
            string extra = string.IsNullOrWhiteSpace(UrlExtra) ? "" : "&" + UrlExtra.Trim().TrimStart('&', '?');
            return $"https://{Host}/index.html?q={_quality}&spin={_spin}&it={it}&ib={ib}&il={il}&ir={ir}{extra}";
        }

        public void Navigate()
        {
            try
            {
                if (_web.CoreWebView2 == null) return;
                WebReady = false;
                string url = BuildUrl();
                Log.Info("加载 " + url + " @ " + TargetScreen.DeviceName + " " + TargetBounds);
                _web.CoreWebView2.Navigate(url);
            }
            catch (Exception ex) { Log.Error("Navigate 失败", ex); }
        }

        public void SetQuality(string q) { _quality = q; Navigate(); }

        /// <summary>旋转速度（秒/圈，0 = 停止并回到正面），实时生效、不重新加载页面。</summary>
        public void SetSpin(int seconds) { _spin = seconds; Post("{\"type\":\"spin\",\"v\":" + seconds + "}"); }

        public void SendInsets()
        {
            try
            {
                if (!WebReady) return;
                var b = TargetScreen.Bounds; var wa = TargetScreen.WorkingArea; double sc = Native.ScaleForRect(b);
                string json = $"{{\"type\":\"insets\",\"t\":{(int)(Math.Max(0, wa.Top - b.Top) / sc)},\"b\":{(int)(Math.Max(0, b.Bottom - wa.Bottom) / sc)},\"l\":{(int)(Math.Max(0, wa.Left - b.Left) / sc)},\"r\":{(int)(Math.Max(0, b.Right - wa.Right) / sc)}}}";
                _web.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception ex) { Log.Warn("SendInsets: " + ex.Message); }
        }

        public void SetPaused(bool p)
        {
            if (Paused == p) return;
            Paused = p;
            Post(p ? "{\"type\":\"pause\"}" : "{\"type\":\"resume\"}");
            Log.Info((p ? "暂停渲染 " : "恢复渲染 ") + TargetScreen.DeviceName);
        }

        public void SetFps(int fps)
        {
            if (CurrentFps == fps) return;
            CurrentFps = fps;
            Post("{\"type\":\"fps\",\"v\":" + fps + "}");
        }

        private void ReapplyState()
        {
            if (Paused) Post("{\"type\":\"pause\"}");
            if (CurrentFps > 0) Post("{\"type\":\"fps\",\"v\":" + CurrentFps + "}");
        }

        private void Post(string json)
        {
            try { if (WebReady && _web.CoreWebView2 != null) _web.CoreWebView2.PostWebMessageAsJson(json); }
            catch (Exception ex) { Log.Warn("PostWebMessage: " + ex.Message); }
        }

        protected override void Dispose(bool disposing)
        {
            try { if (disposing) _web?.Dispose(); } catch { }
            try { base.Dispose(disposing); } catch (Exception ex) { Log.Warn("Form.Dispose: " + ex.Message); }
        }
    }

    /// <summary>从 web 目录读取文件并返回带正确 MIME 的响应（防目录穿越）。</summary>
    internal static class LocalFiles
    {
        private static readonly System.Collections.Generic.Dictionary<string, string> Mime = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html; charset=utf-8", [".htm"] = "text/html; charset=utf-8",
            [".js"] = "text/javascript; charset=utf-8", [".mjs"] = "text/javascript; charset=utf-8",
            [".json"] = "application/json; charset=utf-8", [".css"] = "text/css; charset=utf-8",
            [".bin"] = "application/octet-stream", [".woff2"] = "font/woff2", [".woff"] = "font/woff",
            [".png"] = "image/png", [".jpg"] = "image/jpeg", [".svg"] = "image/svg+xml", [".ico"] = "image/x-icon",
        };

        public static CoreWebView2WebResourceResponse Serve(CoreWebView2Environment env, string root, string uri)
        {
            var u = new Uri(uri);
            string rel = Uri.UnescapeDataString(u.AbsolutePath).TrimStart('/');
            if (rel.Length == 0) rel = "index.html";
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, rel.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            string rootFull = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(full))
                return env.CreateWebResourceResponse(null, 404, "Not Found", "Content-Type: text/plain");
            string ext = System.IO.Path.GetExtension(full);
            string type = Mime.TryGetValue(ext, out var t) ? t : "application/octet-stream";
            var ms = new System.IO.MemoryStream(System.IO.File.ReadAllBytes(full));
            return env.CreateWebResourceResponse(ms, 200, "OK", "Content-Type: " + type + "\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-cache");
        }
    }

    internal static class WebView2Ext
    {
        public static void CoreWebView2Controller_NotifyParentMoved(this WebView2 w)
        {
            // WinForms WebView2 控件自动跟随父窗口；这里仅触发一次布局，防止嵌入后尺寸不同步
            try { w.PerformLayout(); w.Invalidate(); } catch { }
        }
    }
}
