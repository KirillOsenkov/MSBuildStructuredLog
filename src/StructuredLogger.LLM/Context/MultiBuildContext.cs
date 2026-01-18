using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Manages multiple Build objects with unique identification.
    /// Provides methods to add, remove, and query builds by ID.
    /// </summary>
    public class MultiBuildContext
    {
        private readonly Dictionary<string, BuildInfo> builds;
        private readonly HashSet<string> usedFriendlyNames;
        private string primaryBuildId;
        private int nextBuildNumber = 1;

        /// <summary>
        /// Gets the ID of the primary (default) build.
        /// </summary>
        public string PrimaryBuildId => primaryBuildId;

        /// <summary>
        /// Gets the number of loaded builds.
        /// </summary>
        public int BuildCount => builds.Count;

        public MultiBuildContext()
        {
            builds = new Dictionary<string, BuildInfo>(StringComparer.OrdinalIgnoreCase);
            usedFriendlyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds a build to the context and returns its unique ID.
        /// The first build added becomes the primary build.
        /// </summary>
        /// <param name="build">The Build object to add.</param>
        /// <param name="friendlyName">Optional friendly name. If null, derived from path.</param>
        /// <returns>The unique build ID assigned.</returns>
        public string AddBuild(Build build, string friendlyName = null)
        {
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            var buildId = GenerateUniqueBuildId();
            var fullPath = build.LogFilePath ?? string.Empty;
            var name = friendlyName ?? GenerateFriendlyName(fullPath);

            // Ensure name uniqueness
            name = EnsureUniqueName(name);
            usedFriendlyNames.Add(name);

            var buildInfo = new BuildInfo(buildId, name, fullPath, build);

            // First build becomes primary
            if (builds.Count == 0)
            {
                buildInfo.IsPrimary = true;
                primaryBuildId = buildId;
            }

            builds[buildId] = buildInfo;
            return buildId;
        }

        /// <summary>
        /// Removes a build from the context.
        /// If the removed build was primary, a new primary is selected.
        /// </summary>
        /// <param name="buildId">The ID of the build to remove.</param>
        /// <exception cref="ArgumentException">If buildId is not found.</exception>
        /// <exception cref="InvalidOperationException">If trying to remove the last build.</exception>
        public void RemoveBuild(string buildId)
        {
            if (!builds.TryGetValue(buildId, out var buildInfo))
            {
                throw new ArgumentException($"Build '{buildId}' not found.", nameof(buildId));
            }

            if (builds.Count == 1)
            {
                throw new InvalidOperationException("Cannot remove the last build from context.");
            }

            usedFriendlyNames.Remove(buildInfo.FriendlyName);
            builds.Remove(buildId);

            // If removed build was primary, select new primary
            if (buildId.Equals(primaryBuildId, StringComparison.OrdinalIgnoreCase))
            {
                var newPrimary = builds.Values.First();
                newPrimary.IsPrimary = true;
                primaryBuildId = newPrimary.BuildId;
            }
        }

        /// <summary>
        /// Gets a build by its ID.
        /// </summary>
        /// <param name="buildId">The build ID.</param>
        /// <returns>The BuildInfo for the requested build.</returns>
        /// <exception cref="ArgumentException">If buildId is not found.</exception>
        public BuildInfo GetBuild(string buildId)
        {
            if (!builds.TryGetValue(buildId, out var buildInfo))
            {
                throw new ArgumentException($"Build '{buildId}' not found. Use ListBuilds to see available builds.", nameof(buildId));
            }
            return buildInfo;
        }

        /// <summary>
        /// Gets the primary (default) build.
        /// </summary>
        /// <returns>The BuildInfo for the primary build.</returns>
        /// <exception cref="InvalidOperationException">If no builds are loaded.</exception>
        public BuildInfo GetPrimaryBuild()
        {
            if (string.IsNullOrEmpty(primaryBuildId) || !builds.TryGetValue(primaryBuildId, out var buildInfo))
            {
                throw new InvalidOperationException("No builds loaded.");
            }
            return buildInfo;
        }

        /// <summary>
        /// Gets all loaded builds.
        /// </summary>
        /// <returns>Enumerable of all BuildInfo objects.</returns>
        public IEnumerable<BuildInfo> GetAllBuilds()
        {
            return builds.Values.OrderBy(b => b.LoadedAt);
        }

        /// <summary>
        /// Sets a specific build as the primary build.
        /// </summary>
        /// <param name="buildId">The ID of the build to set as primary.</param>
        /// <exception cref="ArgumentException">If buildId is not found.</exception>
        public void SetPrimaryBuild(string buildId)
        {
            if (!builds.TryGetValue(buildId, out var newPrimary))
            {
                throw new ArgumentException($"Build '{buildId}' not found.", nameof(buildId));
            }

            // Clear current primary
            if (!string.IsNullOrEmpty(primaryBuildId) && builds.TryGetValue(primaryBuildId, out var currentPrimary))
            {
                currentPrimary.IsPrimary = false;
            }

            newPrimary.IsPrimary = true;
            primaryBuildId = buildId;
        }

        /// <summary>
        /// Attempts to get a build by ID.
        /// </summary>
        /// <param name="buildId">The build ID.</param>
        /// <param name="buildInfo">The BuildInfo if found.</param>
        /// <returns>True if found, false otherwise.</returns>
        public bool TryGetBuild(string buildId, out BuildInfo buildInfo)
        {
            return builds.TryGetValue(buildId, out buildInfo);
        }

        /// <summary>
        /// Checks if a build with the given full path is already loaded.
        /// </summary>
        /// <param name="fullPath">The full path to check.</param>
        /// <returns>True if already loaded.</returns>
        public bool ContainsBuildByPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return false;
            }
            return builds.Values.Any(b =>
                b.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        }

        private string GenerateUniqueBuildId()
        {
            return $"build_{nextBuildNumber++:D3}";
        }

        private string GenerateFriendlyName(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return $"Build{nextBuildNumber}";
            }

            try
            {
                // Get the parent directory name (usually project/solution folder)
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    var folderName = Path.GetFileName(directory);
                    if (!string.IsNullOrEmpty(folderName))
                    {
                        // If it's a common folder like 'bin' or 'Debug', go up another level
                        if (folderName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                            folderName.Equals("Debug", StringComparison.OrdinalIgnoreCase) ||
                            folderName.Equals("Release", StringComparison.OrdinalIgnoreCase))
                        {
                            var parentDir = Path.GetDirectoryName(directory);
                            if (!string.IsNullOrEmpty(parentDir))
                            {
                                folderName = Path.GetFileName(parentDir);
                            }
                        }

                        if (!string.IsNullOrEmpty(folderName))
                        {
                            return folderName;
                        }
                    }
                }

                // Fallback to filename without extension
                var fileName = Path.GetFileNameWithoutExtension(fullPath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    return fileName;
                }
            }
            catch
            {
                // Ignore path parsing errors
            }

            return $"Build{nextBuildNumber}";
        }

        private string EnsureUniqueName(string baseName)
        {
            if (!usedFriendlyNames.Contains(baseName))
            {
                return baseName;
            }

            // Append number to make unique
            int suffix = 2;
            string uniqueName;
            do
            {
                uniqueName = $"{baseName}_{suffix++}";
            } while (usedFriendlyNames.Contains(uniqueName));

            return uniqueName;
        }
    }
}
