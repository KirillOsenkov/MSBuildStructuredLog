using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public interface IProjectOrEvaluation
    {
        public string AdormentString { get; }

        public string TargetFramework { get; set; }

        public string Platform { get; set; }

        public string Configuration { get; set; }
    }

    public static class IProjectOrEvaluationHelper
    {
        private class IProjectOrEvaluationComparer : IEqualityComparer<IProjectOrEvaluation>
        {
            public bool Equals(IProjectOrEvaluation x, IProjectOrEvaluation y)
            {
                return x.TargetFramework == y.TargetFramework
                    && x.Platform == y.Platform
                    && x.Configuration == y.Configuration;
            }

            public int GetHashCode(IProjectOrEvaluation obj)
            {
                int hashcode = 123456789;
                hashcode = hashcode * 987654321 ^ (obj.TargetFramework == null ? 1 : obj.TargetFramework.GetHashCode());
                hashcode = hashcode * 987654321 ^ (obj.Platform == null ? 1 : obj.Platform.GetHashCode());
                hashcode = hashcode * 987654321 ^ (obj.Configuration == null ? 1 : obj.Configuration.GetHashCode());

                return hashcode;
            }
        }

        private const string seperator = ",";

        private static IProjectOrEvaluationComparer comparer = new();

        private static Dictionary<IProjectOrEvaluation, string> AdormentStringCache = new(comparer);

        public static string GetAdormentString(this IProjectOrEvaluation proj)
        {
            if (AdormentStringCache.TryGetValue(proj, out string value))
            {
                return value;
            }

            string adorment = CreateAdorment(proj);
            AdormentStringCache.Add(proj, adorment);

            return adorment;
        }

        private static string CreateAdorment(IProjectOrEvaluation proj)
        {
            bool existsTF = !string.IsNullOrEmpty(proj.TargetFramework);
            bool existsPlatform = !string.IsNullOrEmpty(proj.Platform);
            bool existsConfiguration = !string.IsNullOrEmpty(proj.Configuration);

            if (existsConfiguration && existsPlatform && existsTF) { return string.Join(seperator, proj.Configuration, proj.Platform, proj.TargetFramework); }
            else if (existsPlatform && existsTF) { return string.Join(seperator, proj.Platform, proj.TargetFramework); }
            else if (existsConfiguration && existsPlatform) { return string.Join(seperator, proj.Configuration, proj.Platform); }
            else if (existsConfiguration && existsTF) { return string.Join(seperator, proj.Configuration, proj.TargetFramework); }
            else if (existsConfiguration) { return proj.Configuration; }
            else if (existsPlatform) { return proj.Platform; }
            else if (existsTF) { return proj.TargetFramework; }

            return string.Empty;
        }
    }
}
