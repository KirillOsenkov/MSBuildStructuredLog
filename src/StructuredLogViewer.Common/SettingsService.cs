﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace StructuredLogViewer
{
    public class SettingsService
    {
        private const int maxCount = 10;

        // TODO: protect access to these with a Mutex
        private static readonly string recentLogsFilePath = Path.Combine(GetRootPath(), "RecentLogs.txt");
        private static readonly string recentProjectsFilePath = Path.Combine(GetRootPath(), "RecentProjects.txt");
        private static readonly string recentMSBuildLocationsFilePath = Path.Combine(GetRootPath(), "RecentMSBuildLocations.txt");
        private static readonly string recentSearchesFilePath = Path.Combine(GetRootPath(), "RecentSearches.txt");
        private static readonly string customArgumentsFilePath = Path.Combine(GetRootPath(), "CustomMSBuildArguments.txt");
        private static readonly string disableUpdatesFilePath = Path.Combine(GetRootPath(), "DisableUpdates.txt");
        private static readonly string settingsFilePath = Path.Combine(GetRootPath(), "Settings.txt");
        private static readonly string tempFolder = Path.Combine(GetRootPath(), "Temp");

        private static bool settingsRead = false;

        public static void AddRecentLogFile(string filePath)
        {
            AddRecentItem(filePath, recentLogsFilePath);
        }

        public static void AddRecentProject(string filePath)
        {
            AddRecentItem(filePath, recentProjectsFilePath);
        }

        public static void AddRecentMSBuildLocation(string filePath)
        {
            EnsureRecentMSBuildLocationsArePopulated();
            AddRecentItem(filePath, recentMSBuildLocationsFilePath);
        }

        public static void AddRecentSearchText(string searchText, bool discardPrefixes = false)
        {
            AddRecentItem(searchText, recentSearchesFilePath, discardPrefixes);
        }

        public static IEnumerable<string> GetRecentLogFiles()
        {
            return GetRecentItems(recentLogsFilePath);
        }

        public static IEnumerable<string> GetRecentProjects()
        {
            return GetRecentItems(recentProjectsFilePath);
        }

        public static void RemoveRecentLogFile(string filePath)
        {
            RemoveRecentItem(filePath, recentLogsFilePath);
        }

        public static void RemoveRecentProject(string filePath)
        {
            RemoveRecentItem(filePath, recentProjectsFilePath);
        }

        public static IEnumerable<string> GetRecentMSBuildLocations()
        {
            EnsureRecentMSBuildLocationsArePopulated();
            return GetRecentItems(recentMSBuildLocationsFilePath);
        }

        public static IEnumerable<string> GetRecentSearchStrings()
        {
            return GetRecentItems(recentSearchesFilePath);
        }

        private static void AddRecentItem(string item, string storageFilePath, bool discardPrefixes = false)
        {
            var list = GetRecentItems(storageFilePath).ToList();
            if (AddOrPromote(list, item, discardPrefixes))
            {
                SaveText(storageFilePath, list);
            }
        }

        private static void RemoveRecentItem(string item, string storageFilePath)
        {
            var list = GetRecentItems(storageFilePath).ToList();
            if (list.Remove(item))
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

        public static string GetRootPath()
        {
            string path;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                path = Environment.GetEnvironmentVariable("LocalAppData");

            }
            else
            {
                path = "~/";
            }

            if (!string.IsNullOrEmpty(path))
            {
                path = Path.Combine(path, "Microsoft", "MSBuildStructuredLog");
            }

            return path;
        }

        private static void SaveText(string storageFilePath, IEnumerable<string> lines)
        {
            string directoryName = Path.GetDirectoryName(storageFilePath);
            Directory.CreateDirectory(directoryName);
            File.WriteAllLines(storageFilePath, lines);
        }

        private static bool AddOrPromote(List<string> list, string item, bool discardPrefixes = false)
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
            else if (discardPrefixes)
            {
                index = list.FindIndex(i => item.StartsWith(i));
                if (index >= 0)
                {
                    list.RemoveAt(index);
                }
            }

            if (list.Count >= maxCount)
            {
                list.RemoveAt(list.Count - 1);
            }

            list.Insert(0, item);
            return true;
        }

        public static string GetMSBuildExe()
        {
            return GetRecentMSBuildLocations().FirstOrDefault();
        }

        private static void EnsureRecentMSBuildLocationsArePopulated()
        {
            if (!File.Exists(recentMSBuildLocationsFilePath))
            {
                SaveText(recentMSBuildLocationsFilePath, MSBuildLocator.GetMSBuildLocations());
            }
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

        /// <summary>
        /// Just an escape hatch in case some users might want it
        /// </summary>
        public static bool DisableUpdates => File.Exists(disableUpdatesFilePath);

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

        private static bool enableTreeViewVirtualization = true;
        public static bool EnableTreeViewVirtualization
        {
            get
            {
                EnsureSettingsRead();
                return enableTreeViewVirtualization;
            }

            set
            {
                if (enableTreeViewVirtualization == value)
                {
                    return;
                }

                enableTreeViewVirtualization = value;
                SaveSettings();
            }
        }

        private static void EnsureSettingsRead()
        {
            if (!settingsRead)
            {
                ReadSettings();
                settingsRead = true;
            }
        }

        const string Virtualization = "Virtualization=";

        private static void SaveSettings()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Virtualization + enableTreeViewVirtualization.ToString());
            File.WriteAllText(settingsFilePath, sb.ToString());
        }

        private static void ReadSettings()
        {
            if (!File.Exists(settingsFilePath))
            {
                return;
            }

            var lines = File.ReadAllLines(settingsFilePath);
            foreach (var line in lines)
            {
                if (line.StartsWith(Virtualization))
                {
                    var value = line.Substring(Virtualization.Length);
                    if (bool.TryParse(value, out bool boolValue))
                    {
                        enableTreeViewVirtualization = boolValue;
                    }
                }
            }
        }

        private static bool cleanedUpTempFiles = false;
        private static readonly object cleanLock = new object();

        public static string WriteContentToTempFileAndGetPath(string content, string fileExtension)
        {
            var folder = tempFolder;
            var filePath = Path.Combine(folder, Utilities.GetMD5Hash(content, 16) + fileExtension);
            if (File.Exists(filePath))
            {
                return filePath;
            }

            Directory.CreateDirectory(folder);
            File.WriteAllText(filePath, content);

            if (!cleanedUpTempFiles)
            {
                System.Threading.Tasks.Task.Run(() => CleanupTempFiles());
            }

            return filePath;
        }

        /// <summary>
        /// Delete temp files older than one month
        /// </summary>
        private static void CleanupTempFiles()
        {
            lock (cleanLock)
            {
                if (cleanedUpTempFiles)
                {
                    return;
                }

                cleanedUpTempFiles = true;

                var folder = tempFolder;
                try
                {
                    foreach (var file in Directory.GetFiles(folder))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            if (fileInfo.LastWriteTimeUtc < DateTime.UtcNow - TimeSpan.FromDays(30))
                            {
                                fileInfo.Delete();
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
