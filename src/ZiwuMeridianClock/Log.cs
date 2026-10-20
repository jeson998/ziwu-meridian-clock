using System;
using System.IO;
using System.Text;

namespace ZiwuMeridianClock
{
    /// <summary>简单线程安全文件日志：%LOCALAPPDATA%\ZiwuMeridianClock\log.txt（超过 1MB 自动轮转）。</summary>
    internal static class Log
    {
        private static readonly object Gate = new object();
        public static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZiwuMeridianClock");
        public static readonly string FilePath = Path.Combine(Dir, "log.txt");

        public static void Info(string msg) => Write("INFO ", msg);
        public static void Warn(string msg) => Write("WARN ", msg);
        public static void Error(string msg, Exception ex = null) => Write("ERROR", ex == null ? msg : msg + " :: " + ex);

        private static void Write(string level, string msg)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Dir);
                    var fi = new FileInfo(FilePath);
                    if (fi.Exists && fi.Length > 1024 * 1024)
                    {
                        string old = Path.Combine(Dir, "log.old.txt");
                        try { if (File.Exists(old)) File.Delete(old); File.Move(FilePath, old); } catch { }
                    }
                    File.AppendAllText(FilePath,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}{Environment.NewLine}", Encoding.UTF8);
                }
            }
            catch { /* 日志失败不影响运行 */ }
        }
    }
}
