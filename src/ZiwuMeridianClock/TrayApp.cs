using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace ZiwuMeridianClock
{
    /// <summary>应用主体：托盘菜单、壁纸窗口管理、Explorer 重启/分辨率变化重嵌、全屏暂停、省电降帧。</summary>
    internal sealed class TrayApp : ApplicationContext
    {
        public const string AppName = "子午流注·经络全息钟";
        private readonly Settings _st;
        private readonly string _webDir;
        private readonly NotifyIcon _tray;
        private readonly List<WallpaperForm> _forms = new List<WallpaperForm>();
        private CoreWebView2Environment _env;
        private readonly Timer _monitorTimer = new Timer { Interval = 2000 };
        private readonly Timer _watchdog = new Timer { Interval = 5000 };
        private readonly Timer _rebuildDebounce = new Timer { Interval = 1500 };
        private readonly MessageWindow _msgWin;
        private bool _building, _exiting;
        private int _rebuildCount;
        private DateTime _rebuildWindowStart = DateTime.Now;

        private ToolStripMenuItem _miShow, _miAuto, _miAll, _miPauseFs, _miModeAuto, _miModeBottom;
        private readonly Dictionary<string, ToolStripMenuItem> _miQ = new Dictionary<string, ToolStripMenuItem>();
        private readonly Dictionary<int, ToolStripMenuItem> _miSpin = new Dictionary<int, ToolStripMenuItem>();

        public TrayApp(Settings st, string webDir)
        {
            _st = st; _webDir = webDir;
            ApplyUrlExtra();
            _tray = new NotifyIcon { Icon = LoadIcon(), Text = AppName, Visible = true, ContextMenuStrip = BuildMenu() };
            _tray.DoubleClick += (s, e) => ToggleVisible();

            _msgWin = new MessageWindow();
            _msgWin.TaskbarCreated += () => { Log.Info("收到 TaskbarCreated（Explorer 重启），稍后重新嵌入"); ScheduleRebuild(2500); };
            _msgWin.DisplayChanged += () => { Log.Info("WM_DISPLAYCHANGE"); ScheduleRebuild(1500); };
            _msgWin.WorkAreaChanged += () => { foreach (var f in _forms) { f.Reposition(); f.SendInsets(); } };

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            _rebuildDebounce.Tick += (s, e) => { _rebuildDebounce.Stop(); _ = RebuildAsync(); };
            _monitorTimer.Tick += (s, e) => CheckFullscreenAndPower();
            _watchdog.Tick += (s, e) => Watchdog();

            _ = StartAsync();
        }

        private static Icon LoadIcon()
        {
            try
            {
                using var s = typeof(TrayApp).Assembly.GetManifestResourceStream("app.ico");
                if (s != null) return new Icon(s, new Size(32, 32));
            }
            catch (Exception ex) { Log.Warn("加载托盘图标失败: " + ex.Message); }
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; } catch { return SystemIcons.Application; }
        }

        // ---------------- 菜单 ----------------
        private ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            var title = new ToolStripMenuItem(AppName) { Enabled = false };
            _miShow = new ToolStripMenuItem("显示 / 隐藏", null, (s, e) => ToggleVisible()) { Checked = _st.Visible };
            _miAuto = new ToolStripMenuItem("开机自启", null, (s, e) =>
            {
                bool want = !Settings.GetAutoStart();
                if (Settings.SetAutoStart(want)) _miAuto.Checked = want;
                else MessageBox.Show("设置开机自启失败，详见日志。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }) { Checked = Settings.GetAutoStart() };

            var q = new ToolStripMenuItem("画质");
            foreach (var (key, label) in new[] { ("high", "高（30fps，完整粒子）"), ("mid", "中（24fps，60% 粒子）"), ("low", "低（15fps，35% 粒子，省电）") })
            {
                var it = new ToolStripMenuItem(label, null, (s, e) => SetQuality(key)) { Checked = _st.Quality == key };
                _miQ[key] = it; q.DropDownItems.Add(it);
            }

            var spin = new ToolStripMenuItem("旋转速度");
            foreach (var (sec, label) in new[] { (90, "慢（90 秒/圈）"), (60, "中（60 秒/圈）"), (30, "快（30 秒/圈）"), (0, "停止（正面）") })
            {
                var it = new ToolStripMenuItem(label, null, (s, e) => SetSpin(sec)) { Checked = _st.Spin == sec };
                _miSpin[sec] = it; spin.DropDownItems.Add(it);
            }

            var adv = new ToolStripMenuItem("高级");
            _miAll = new ToolStripMenuItem("在所有显示器显示", null, (s, e) =>
            {
                _st.AllMonitors = !_st.AllMonitors; _miAll.Checked = _st.AllMonitors; _st.Save(); ScheduleRebuild(100);
            }) { Checked = _st.AllMonitors };
            _miPauseFs = new ToolStripMenuItem("全屏程序/游戏时暂停", null, (s, e) =>
            {
                _st.PauseOnFullscreen = !_st.PauseOnFullscreen; _miPauseFs.Checked = _st.PauseOnFullscreen; _st.Save();
            }) { Checked = _st.PauseOnFullscreen };
            _miModeAuto = new ToolStripMenuItem("显示模式：桌面壁纸层（推荐）", null, (s, e) => SetMode("auto")) { Checked = _st.EmbedMode == "auto" };
            _miModeBottom = new ToolStripMenuItem("显示模式：兼容模式（置底窗口）", null, (s, e) => SetMode("bottom")) { Checked = _st.EmbedMode == "bottom" };
            var miReembed = new ToolStripMenuItem("重新嵌入桌面", null, (s, e) => ScheduleRebuild(100));
            ToolStripMenuItem miSafe = null;
            miSafe = new ToolStripMenuItem("安全模式（排查黑斑/闪烁）", null, (s, e) =>
            {
                _st.SafeMode = !_st.SafeMode; miSafe.Checked = _st.SafeMode; _st.Save(); ApplyUrlExtra();
                foreach (var f in _forms) f.Navigate();
                Log.Info("安全模式 = " + _st.SafeMode);
            }) { Checked = _st.SafeMode };
            var miLog = new ToolStripMenuItem("打开日志目录", null, (s, e) =>
            {
                try { Directory.CreateDirectory(Log.Dir); System.Diagnostics.Process.Start("explorer.exe", "\"" + Log.Dir + "\""); }
                catch (Exception ex) { Log.Warn("打开日志目录失败: " + ex.Message); }
            });
            adv.DropDownItems.AddRange(new ToolStripItem[] { _miAll, _miPauseFs, new ToolStripSeparator(), _miModeAuto, _miModeBottom, miReembed, miSafe, new ToolStripSeparator(), miLog });

            var exit = new ToolStripMenuItem("退出", null, (s, e) => ExitApp());
            m.Items.AddRange(new ToolStripItem[] { title, new ToolStripSeparator(), _miShow, _miAuto, q, spin, adv, new ToolStripSeparator(), exit });
            return m;
        }

        private void SetQuality(string q)
        {
            _st.Quality = q; _st.Save();
            foreach (var kv in _miQ) kv.Value.Checked = kv.Key == q;
            foreach (var f in _forms) f.SetQuality(q);
            Log.Info("画质 = " + q);
        }

        private void ApplyUrlExtra()
        {
            string e = (_st.ExtraParams ?? "").Trim().TrimStart('?', '&');
            if (_st.SafeMode) e = string.IsNullOrEmpty(e) ? "safe=1" : e + "&safe=1";
            WallpaperForm.UrlExtra = e;
        }

        private void SetSpin(int sec)
        {
            _st.Spin = sec; _st.Save();
            foreach (var kv in _miSpin) kv.Value.Checked = kv.Key == sec;
            foreach (var f in _forms) f.SetSpin(sec);
            Log.Info("旋转速度 = " + sec + " 秒/圈");
        }

        private void SetMode(string mode)
        {
            _st.EmbedMode = mode; _st.Save();
            _miModeAuto.Checked = mode == "auto"; _miModeBottom.Checked = mode == "bottom";
            ScheduleRebuild(100);
        }

        private void ToggleVisible()
        {
            _st.Visible = !_st.Visible; _st.Save(); _miShow.Checked = _st.Visible;
            foreach (var f in _forms)
            {
                try
                {
                    if (_st.Visible) { f.Show(); f.Reposition(); f.SetPaused(false); }
                    else { f.SetPaused(true); f.Hide(); }
                }
                catch (Exception ex) { Log.Warn("显示/隐藏: " + ex.Message); }
            }
            if (!_st.Visible) Native.RefreshWallpaper();
        }

        // ---------------- 生命周期 ----------------
        private async Task StartAsync()
        {
            try
            {
                string udf = Path.Combine(Log.Dir, "WebView2");
                Directory.CreateDirectory(udf);
                // 关键：禁用 Chromium 的遮挡检测，否则窗口位于桌面图标层之后时会被当成“不可见”而停止渲染
                var opts = new CoreWebView2EnvironmentOptions(
                    "--disable-features=CalculateNativeWinOcclusion --disable-background-timer-throttling " +
                    "--disable-backgrounding-occluded-windows --disable-renderer-backgrounding --autoplay-policy=no-user-gesture-required");
                _env = await CoreWebView2Environment.CreateAsync(null, udf, opts);
                Log.Info("WebView2 版本 " + _env.BrowserVersionString);
            }
            catch (Exception ex)
            {
                Log.Error("创建 WebView2 环境失败", ex);
                MessageBox.Show("无法初始化 WebView2：\n" + ex.Message + "\n\n日志：" + Log.FilePath, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExitApp(); return;
            }
            await RebuildAsync();
            _monitorTimer.Start();
            _watchdog.Start();
            _tray.ShowBalloonTip(3000, AppName, "已在桌面运行。右键托盘图标可设置画质、开机自启或退出。", ToolTipIcon.Info);
        }

        private void ScheduleRebuild(int delayMs)
        {
            if (_exiting) return;
            _rebuildDebounce.Stop();
            _rebuildDebounce.Interval = Math.Max(50, delayMs);
            _rebuildDebounce.Start();
        }

        private async Task RebuildAsync()
        {
            if (_building || _exiting || _env == null) return;
            // 防止异常情况下无限重建：60 秒内最多 8 次
            if ((DateTime.Now - _rebuildWindowStart).TotalSeconds > 60) { _rebuildWindowStart = DateTime.Now; _rebuildCount = 0; }
            if (++_rebuildCount > 8) { Log.Warn("重建过于频繁，暂停自动重建 60 秒"); return; }

            _building = true;
            try
            {
                DisposeForms();
                var screens = _st.AllMonitors ? Screen.AllScreens : new[] { Screen.PrimaryScreen };
                bool forceFallback = _st.EmbedMode == "bottom";
                foreach (var sc in screens.Where(s => s != null))
                {
                    try
                    {
                        var f = new WallpaperForm(sc, _webDir, _st.Quality, _st.Spin);
                        f.Attach(forceFallback);
                        _forms.Add(f);
                        await f.InitWebAsync(_env);
                        if (!_st.Visible) { f.SetPaused(true); f.Hide(); }
                    }
                    catch (Exception ex) { Log.Error("创建壁纸窗口失败 " + sc.DeviceName, ex); }
                }
                Log.Info($"壁纸窗口数 = {_forms.Count}（模式 {_st.EmbedMode}）");
            }
            finally { _building = false; }
        }

        private void DisposeForms()
        {
            foreach (var f in _forms)
            {
                try { if (!f.IsDisposed) { f.Hide(); f.Close(); f.Dispose(); } }
                catch (Exception ex) { Log.Warn("释放窗口: " + ex.Message); }
            }
            _forms.Clear();
        }

        private void Watchdog()
        {
            if (_building || _exiting || !_st.Visible) return;
            if (_forms.Count == 0 || _forms.Any(f => !f.IsHealthy()))
            {
                Log.Warn("检测到壁纸窗口失效（可能 Explorer 重启），重新嵌入");
                ScheduleRebuild(500);
            }
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e) { Log.Info("DisplaySettingsChanged"); ScheduleRebuild(1500); }
        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) { Log.Info("系统唤醒"); ScheduleRebuild(3000); }
        }
        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            bool locked = e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.ConsoleDisconnect || e.Reason == SessionSwitchReason.RemoteDisconnect;
            _sessionLocked = locked;
            CheckFullscreenAndPower();
        }
        private bool _sessionLocked;

        // ---------------- 全屏暂停 / 省电降帧 ----------------
        private void CheckFullscreenAndPower()
        {
            if (_exiting || _forms.Count == 0) return;
            try
            {
                int fps = _st.Quality == "low" ? 15 : _st.Quality == "mid" ? 24 : 30;
                if (_st.ThrottleOnBattery && Native.GetSystemPowerStatus(out var ps))
                {
                    if (ps.SystemStatusFlag == 1) fps = Math.Min(fps, 10);       // 省电模式
                    else if (ps.ACLineStatus == 0) fps = Math.Min(fps, 20);      // 电池供电
                }

                bool d3dFull = false;
                try
                {
                    if (Native.SHQueryUserNotificationState(out int st) == 0)
                        d3dFull = st == 3 /*D3D 全屏*/ || st == 4 /*演示模式*/ || st == 2 /*全屏忙*/;
                }
                catch { }

                IntPtr fg = Native.GetForegroundWindow();
                Rectangle fgRect = Rectangle.Empty; bool fgIsShell = true;
                if (fg != IntPtr.Zero && Native.GetWindowRect(fg, out var r))
                {
                    string cls = Native.ClassOf(fg);
                    fgIsShell = cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd"
                                || fg == Native.GetShellWindow() || fg == Native.GetDesktopWindow() || _forms.Any(f => f.IsHandleCreated && f.Handle == fg);
                    fgRect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                }

                foreach (var f in _forms)
                {
                    bool pause = !_st.Visible || _sessionLocked;
                    if (!pause && _st.PauseOnFullscreen)
                    {
                        var b = f.TargetBounds;
                        bool covers = !fgIsShell && fgRect.Left <= b.Left && fgRect.Top <= b.Top && fgRect.Right >= b.Right && fgRect.Bottom >= b.Bottom;
                        bool onThis = !fgIsShell && b.IntersectsWith(fgRect);
                        pause = covers || (d3dFull && onThis);
                    }
                    f.SetPaused(pause);
                    f.SetFps(fps);
                }
            }
            catch (Exception ex) { Log.Warn("全屏检测: " + ex.Message); }
        }

        // ---------------- 退出 ----------------
        public void ExitApp()
        {
            if (_exiting) return;
            _exiting = true;
            Log.Info("退出");
            try
            {
                _monitorTimer.Stop(); _watchdog.Stop(); _rebuildDebounce.Stop();
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                DisposeForms();
                _msgWin.DestroyHandle();
                _tray.Visible = false; _tray.Dispose();
                Native.RefreshWallpaper();
            }
            catch (Exception ex) { Log.Warn("退出清理: " + ex.Message); }
            ExitThread();
        }
    }

    /// <summary>隐藏的顶层消息窗口：接收 TaskbarCreated / WM_DISPLAYCHANGE / WM_SETTINGCHANGE 广播。</summary>
    internal sealed class MessageWindow : NativeWindow
    {
        private readonly uint _taskbarCreated;
        public event Action TaskbarCreated, DisplayChanged, WorkAreaChanged;

        public MessageWindow()
        {
            _taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
            // 普通顶层窗口（不是 HWND_MESSAGE），这样才能收到广播消息；尺寸为 0 且不可见
            CreateHandle(new CreateParams { Caption = "ZiwuMeridianClock.Msg", ExStyle = (int)Native.WS_EX_TOOLWINDOW });
        }

        protected override void WndProc(ref Message m)
        {
            try
            {
                if (_taskbarCreated != 0 && m.Msg == (int)_taskbarCreated) TaskbarCreated?.Invoke();
                else if (m.Msg == Native.WM_DISPLAYCHANGE) DisplayChanged?.Invoke();
                else if (m.Msg == Native.WM_SETTINGCHANGE && m.WParam.ToInt64() == 0x002F /*SPI_SETWORKAREA*/) WorkAreaChanged?.Invoke();
            }
            catch (Exception ex) { Log.Warn("MessageWindow: " + ex.Message); }
            base.WndProc(ref m);
        }
    }
}
