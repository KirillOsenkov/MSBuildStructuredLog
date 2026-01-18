using System;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Wrapper containing Build object with identification metadata.
    /// Used for multi-binlog scenarios where each build needs unique identification.
    /// </summary>
    public class BuildInfo
    {
        /// <summary>
        /// Unique identifier for this build (e.g., "build_001").
        /// </summary>
        public string BuildId { get; }

        /// <summary>
        /// Human-friendly name for display (e.g., "CoreLib", "Tests").
        /// Derived from project folder name or user-specified.
        /// </summary>
        public string FriendlyName { get; }

        /// <summary>
        /// Full path to the binlog file. Used for uniqueness checks.
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// The actual Build object from the binlog.
        /// </summary>
        public Build Build { get; }

        /// <summary>
        /// When this build was loaded into the context.
        /// </summary>
        public DateTime LoadedAt { get; }

        /// <summary>
        /// Whether this is the primary/default build for operations.
        /// </summary>
        public bool IsPrimary { get; set; }

        // Cached summary info for quick access
        private int? cachedErrorCount;
        private int? cachedWarningCount;

        public bool Succeeded => Build.Succeeded;
        public string DurationText => Build.DurationText;

        public int ErrorCount
        {
            get
            {
                if (cachedErrorCount == null)
                {
                    CountDiagnostics();
                }
                return cachedErrorCount.Value;
            }
        }

        public int WarningCount
        {
            get
            {
                if (cachedWarningCount == null)
                {
                    CountDiagnostics();
                }
                return cachedWarningCount.Value;
            }
        }

        public BuildInfo(string buildId, string friendlyName, string fullPath, Build build)
        {
            BuildId = buildId ?? throw new ArgumentNullException(nameof(buildId));
            FriendlyName = friendlyName ?? throw new ArgumentNullException(nameof(friendlyName));
            FullPath = fullPath ?? string.Empty;
            Build = build ?? throw new ArgumentNullException(nameof(build));
            LoadedAt = DateTime.Now;
            IsPrimary = false;
        }

        private void CountDiagnostics()
        {
            int errors = 0;
            int warnings = 0;

            Build.VisitAllChildren<BaseNode>(node =>
            {
                if (node is Error)
                {
                    errors++;
                }
                else if (node is Warning)
                {
                    warnings++;
                }
            });

            cachedErrorCount = errors;
            cachedWarningCount = warnings;
        }

        public override string ToString()
        {
            var status = Succeeded ? "Succeeded" : "FAILED";
            var primary = IsPrimary ? " [PRIMARY]" : "";
            return $"[{BuildId}] {FriendlyName}{primary} - {status} ({DurationText})";
        }
    }
}
