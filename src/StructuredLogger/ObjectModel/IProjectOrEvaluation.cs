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

        private static Dictionary<IProjectOrEvaluation, string> AdornmentStringCache = new(comparer);

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

        private static string CreateAdornment(IProjectOrEvaluation proj)
        {
            bool existsTF = !string.IsNullOrEmpty(proj.TargetFramework);
            bool existsPlatform = !string.IsNullOrEmpty(proj.Platform);
            bool existsConfiguration = !string.IsNullOrEmpty(proj.Configuration);

            if (existsConfiguration && existsPlatform && existsTF) { return string.Join(separator, proj.Configuration, proj.Platform, proj.TargetFramework); }
            else if (existsPlatform && existsTF) { return string.Join(separator, proj.Platform, proj.TargetFramework); }
            else if (existsConfiguration && existsPlatform) { return string.Join(separator, proj.Configuration, proj.Platform); }
            else if (existsConfiguration && existsTF) { return string.Join(separator, proj.Configuration, proj.TargetFramework); }
            else if (existsConfiguration) { return proj.Configuration; }
            else if (existsPlatform) { return proj.Platform; }
            else if (existsTF) { return proj.TargetFramework; }

            return string.Empty;
        }
    }
}
