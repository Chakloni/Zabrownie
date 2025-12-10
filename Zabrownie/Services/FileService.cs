using System;
using System.IO;
using System.Threading.Tasks;

namespace Zabrownie.Services
{
    public static class FileService
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zabrownie");

        public static string GetAppDataPath() => AppDataPath;

        public static async Task<string[]> LoadTextFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return Array.Empty<string>();

                return await File.ReadAllLinesAsync(filePath);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to load file: {filePath}", ex);
                return Array.Empty<string>();
            }
        }

        public static async Task SaveTextFileAsync(string filePath, string[] lines)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    await File.WriteAllLinesAsync(filePath, lines);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to save file: {filePath}", ex);
            }
        }

        public static string GetDefaultFiltersPath()
        {
            return Path.Combine(AppDataPath, "default_filters.txt");
        }
    }
}