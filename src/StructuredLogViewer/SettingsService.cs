using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StructuredLogViewer
{
    public class SettingsService
    {
        private const int maxCount = 10;

        // TODO: protect access to these with a Mutex
        private static readonly string recentLogsFilePath = Path.Combine(GetRootPath(), "RecentLogs.txt");
        private static readonly string recentProjectsFilePath = Path.Combine(GetRootPath(), "RecentProjects.txt");
        private static readonly string customArgumentsFilePath = Path.Combine(GetRootPath(), "CustomMSBuildArguments.txt");

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

        private const string DefaultArguments = "/t:Rebuild";

        public static string GetCustomArguments(string filePath)
        {
            if (!File.Exists(customArgumentsFilePath))
            {
                return DefaultArguments;
            }

            var lines = File.ReadAllLines(customArgumentsFilePath);
            string arguments;
            int index;
            if (FindArguments(lines, filePath, out arguments, out index))
            {
                return arguments;
            }

            return DefaultArguments;
        }

        private const int MaximumProjectsInRecentArgumentsList = 100;

        private static bool FindArguments(IList<string> lines, string projectFilePath, out string existingArguments, out int index)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals == -1)
                {
                    continue;
                }

                string project = line.Substring(0, equals);
                string arguments = line.Substring(equals + 1, line.Length - equals - 1);
                if (string.Equals(projectFilePath, project, StringComparison.OrdinalIgnoreCase))
                {
                    existingArguments = arguments;
                    index = i;
                    return true;
                }
            }

            existingArguments = null;
            index = -1;
            return false;
        }

        public static void SaveCustomArguments(string projectFilePath, string newArguments)
        {
            if (!File.Exists(customArgumentsFilePath))
            {
                if (newArguments == DefaultArguments)
                {
                    return;
                }
                else
                {
                    File.WriteAllLines(customArgumentsFilePath, new[] { projectFilePath + "=" + newArguments });
                    return;
                }
            }

            var list = File.ReadAllLines(customArgumentsFilePath).ToList();

            string arguments;
            int index;
            if (FindArguments(list, projectFilePath, out arguments, out index))
            {
                list.RemoveAt(index);
            }

            list.Insert(0, projectFilePath + "=" + newArguments);
            if (list.Count >= MaximumProjectsInRecentArgumentsList)
            {
                list.RemoveAt(list.Count - 1);
            }

            File.WriteAllLines(customArgumentsFilePath, list);
        }
    }
}
