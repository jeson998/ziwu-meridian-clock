using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace ZiwuMeridianClock
{
    internal static class Program
    {
        private const string WebView2Download = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

        [STAThread]
        private static void Main(string[] args)
        {
            using var mutex = new Mutex(true, @"Local\ZiwuMeridianClock_SingleInstance_7f3c", out bool created);
            if (!created)
            {
                MessageBox.Show("子午流注·经络全息钟 已在运行（见任务栏右下角托盘图标）。", TrayApp.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Log.Error("UI 线程异常", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Error("未处理异常", e.ExceptionObject as Exception);
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) => { Log.Error("Task 异常", e.Exception); e.SetObserved(); };

            Log.Info($"==== 启动 {TrayApp.AppName} v{typeof(Program).Assembly.GetName().Version} | {Environment.OSVersion} | 64位={Environment.Is64BitProcess} ====");

            // WebView2 运行时检查
            string ver = null;
            try { ver = CoreWebView2Environment.GetAvailableBrowserVersionString(); }
            catch (WebView2RuntimeNotFoundException) { ver = null; }
            catch (Exception ex) { Log.Error("检查 WebView2 失败", ex); }
            if (string.IsNullOrEmpty(ver))
            {
                Log.Warn("未检测到 WebView2 运行时");
                var r = MessageBox.Show(
                    "本程序需要 Microsoft Edge WebView2 运行时（Windows 11 通常已自带）。\n\n点击“确定”打开下载页面，安装后重新运行本程序。\n\n" + WebView2Download,
                    TrayApp.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (r == DialogResult.OK)
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(WebView2Download) { UseShellExecute = true }); }
                    catch (Exception ex) { Log.Error("打开下载链接失败", ex); }
                }
                return;
            }
            Log.Info("WebView2 运行时 " + ver);

            // 网页资源：exe 同目录的 web\ 文件夹
            string webDir = Path.Combine(AppContext.BaseDirectory, "web");
            if (!File.Exists(Path.Combine(webDir, "index.html")))
            {
                Log.Error("缺少网页资源: " + webDir);
                MessageBox.Show("找不到网页资源文件夹：\n" + webDir + "\n\n请把压缩包完整解压（exe 与 web 文件夹需放在同一目录）。",
                    TrayApp.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var st = Settings.Load();
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            try { Application.Run(new TrayApp(st, webDir)); }
            catch (Exception ex)
            {
                Log.Error("运行失败", ex);
                MessageBox.Show("程序异常退出：" + ex.Message + "\n\n日志：" + Log.FilePath, TrayApp.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
