using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public interface IProjectOrEvaluation
    {
        string TargetFramework { get; set; }

        string Platform { get; set; }

        string Configuration { get; set; }
    }

    public static class ProjectOrEvaluationHelper
    {
        private class IProjectOrEvaluationComparer : IEqualityComparer<IProjectOrEvaluation>
        {
            public bool Equals(IProjectOrEvaluation x, IProjectOrEvaluation y)
            {
                return x.TargetFramework == y.TargetFramework
                    && x.Platform == y.Platform
                    && x.Configuration == y.Configuration;
            }

            public int GetHashCode(IProjectOrEvaluation obj) => (obj.TargetFramework, obj.Platform, obj.Configuration).GetHashCode();
        }

        private const string separator = ",";

        private static IProjectOrEvaluationComparer comparer = new();

        public static void ClearCache()
        {
            AdornmentStringCache.Clear();
        }

        private static Dictionary<IProjectOrEvaluation, string> AdornmentStringCache = new(comparer);

        public static bool ShowConfigurationAndPlatform;

        public static string GetAdornmentString(this IProjectOrEvaluation proj)
        {
            if (AdornmentStringCache.TryGetValue(proj, out string value))
            {
                return value;
            }

            string adornment = CreateAdornment(proj);
            AdornmentStringCache.Add(proj, adornment);

            return adornment;
        }

        [ThreadStatic]
        private static List<string> strings;

        private static string CreateAdornment(IProjectOrEvaluation proj)
        {
            if (strings == null)
            {
                strings = new List<string>(3);
            }

            if (proj.TargetFramework is { Length: > 0 } targetFramework)
            {
                strings.Add(targetFramework);
            }

            if (ShowConfigurationAndPlatform)
            {
                if (proj.Configuration is { Length: > 0 } configuration)
                {
                    strings.Add(configuration);
                }

                if (proj.Platform is { Length: > 0 } platform)
                {
                    strings.Add(platform);
                }
            }

            var result = string.Join(separator, strings);

            strings.Clear();

            return result;
        }
    }
}
