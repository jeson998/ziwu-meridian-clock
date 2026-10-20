using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace ZiwuMeridianClock
{
    internal sealed class Settings
    {
        /// <summary>high / mid / low</summary>
        public string Quality { get; set; } = "high";
        /// <summary>true = 所有显示器；false = 仅主显示器</summary>
        public bool AllMonitors { get; set; } = false;
        /// <summary>auto = 自动嵌入桌面图标之后；bottom = 兼容模式（置底、鼠标穿透的窗口）</summary>
        public string EmbedMode { get; set; } = "auto";
        /// <summary>前台全屏程序时暂停渲染</summary>
        public bool PauseOnFullscreen { get; set; } = true;
        /// <summary>电池供电 / 省电模式时降低帧率</summary>
        public bool ThrottleOnBattery { get; set; } = true;
        public bool Visible { get; set; } = true;
        /// <summary>人体旋转一周的秒数：90 / 60 / 30；0 = 停止（正面）</summary>
        public int Spin { get; set; } = 60;
        /// <summary>安全模式（排查显卡问题）：页面加 safe=1，关闭 AO/褶皱暗化、MSAA、Bloom</summary>
        public bool SafeMode { get; set; } = false;
        /// <summary>附加到页面地址的调试参数，例如 "safe=1" 或 "q=mid"</summary>
        public string ExtraParams { get; set; } = "";

        private static string PathFile => System.IO.Path.Combine(Log.Dir, "settings.json");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(PathFile))
                {
                    var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathFile));
                    if (s != null) { s.Normalize(); return s; }
                }
            }
            catch (Exception ex) { Log.Error("读取设置失败，使用默认值", ex); }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Log.Dir);
                File.WriteAllText(PathFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { Log.Error("保存设置失败", ex); }
        }

        private void Normalize()
        {
            if (Quality != "high" && Quality != "mid" && Quality != "low") Quality = "high";
            if (EmbedMode != "auto" && EmbedMode != "bottom") EmbedMode = "auto";
            if (Spin != 0 && Spin != 30 && Spin != 60 && Spin != 90) Spin = 60;
        }

        // ---------- 开机自启（HKCU Run） ----------
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "ZiwuMeridianClock";

        public static bool GetAutoStart()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey, false);
                return k?.GetValue(RunName) is string v && v.Length > 0;
            }
            catch (Exception ex) { Log.Error("读取开机自启失败", ex); return false; }
        }

        public static bool SetAutoStart(bool on)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(RunKey, true);
                if (k == null) return false;
                if (on)
                {
                    string exe = Environment.ProcessPath ?? System.Windows.Forms.Application.ExecutablePath;
                    k.SetValue(RunName, "\"" + exe + "\"", RegistryValueKind.String);
                }
                else if (k.GetValue(RunName) != null) k.DeleteValue(RunName, false);
                Log.Info("开机自启 = " + on);
                return true;
            }
            catch (Exception ex) { Log.Error("设置开机自启失败", ex); return false; }
        }
    }
}
