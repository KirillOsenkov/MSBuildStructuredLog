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
        public static string MakeAdormentString(this IProjectOrEvaluation proj)
        {
            bool existsTF = !string.IsNullOrEmpty(proj.TargetFramework);
            bool existsPlatform = !string.IsNullOrEmpty(proj.Platform);
            bool existsConfiguration = !string.IsNullOrEmpty(proj.Configuration);

            if (existsConfiguration && existsTF && existsPlatform) { return $"{proj.Configuration};{proj.Platform};{proj.TargetFramework}"; }
            else if (existsTF && existsPlatform) { return $"{proj.Configuration};{proj.Platform}"; }
            else if (existsConfiguration && existsPlatform) { return $"{proj.Platform};{proj.TargetFramework}"; }
            else if (existsConfiguration && existsTF) { return $"{proj.Configuration};{proj.Platform};{proj.TargetFramework}"; }
            else if (existsConfiguration) { return proj.Configuration; }
            else if (existsPlatform) { return proj.Platform; }
            else if (existsTF) { return proj.TargetFramework; }

            return string.Empty;
        }
    }
}
