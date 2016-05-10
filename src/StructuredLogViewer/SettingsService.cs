using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StructuredLogViewer
{
    public class SettingsService
    {
        private const int maxCount = 10;

        // TODO: protect access to these with a Mutex (I know I'll never have time for this, but, hey)
        private static readonly string recentLogsFilePath = Path.Combine(GetRootPath(), "RecentLogs.txt");
        private static readonly string recentProjectsFilePath = Path.Combine(GetRootPath(), "RecentProjects.txt");

        public static void AddRecentLogFile(string filePath)
        {
            AddRecentItem(filePath, recentLogsFilePath);
        }

        public static void AddRecentProject(string filePath)
        {
            AddRecentItem(filePath, recentProjectsFilePath);
        }

        public static IEnumerable<string> GetRecentLogFiles()
        {
            return GetRecentItems(recentLogsFilePath);
        }

        public static IEnumerable<string> GetRecentProjects()
        {
            return GetRecentItems(recentProjectsFilePath);
        }

        private static void AddRecentItem(string item, string storageFilePath)
        {
            var list = GetRecentItems(storageFilePath).ToList();
            if (AddOrPromote(list, item))
            {
                SaveText(storageFilePath, list);
            }
        }

        private static IEnumerable<string> GetRecentItems(string storageFilePath)
        {
            if (!File.Exists(storageFilePath))
            {
                return Array.Empty<string>();
            }

            var lines = File.ReadAllLines(storageFilePath);
            return lines;
        }

        private static string GetRootPath()
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            path = Path.Combine(path, "Microsoft", "MSBuildStructuredLog");
            return path;
        }

        private static void SaveText(string storageFilePath, IEnumerable<string> lines)
        {
            string directoryName = Path.GetDirectoryName(storageFilePath);
            Directory.CreateDirectory(directoryName);
            File.WriteAllLines(storageFilePath, lines);
        }

        private static bool AddOrPromote(List<string> list, string item)
        {
            if (list.Count > 0 && list[0] == item)
            {
                // if the first item is exact match, don't do anything
                return false;
            }

            int index = list.FindIndex(i => StringComparer.OrdinalIgnoreCase.Compare(i, item) == 0);
            if (index >= 0)
            {
                list.RemoveAt(index);
            }
            else if (list.Count >= maxCount)
            {
                list.RemoveAt(list.Count - 1);
            }

            list.Insert(0, item);
            return true;
        }
    }
}
