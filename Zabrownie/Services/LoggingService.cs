using System;
using System.IO;

namespace Zabrownie.Services
{
    public static class LoggingService
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zabrownie", "logs.txt");

        public static void Log(string message)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
                }
            }
            catch { }
        }

        public static void LogError(string message, Exception ex)
        {
            Log($"ERROR: {message} | {ex.Message}\n{ex.StackTrace}");
        }
    }
}