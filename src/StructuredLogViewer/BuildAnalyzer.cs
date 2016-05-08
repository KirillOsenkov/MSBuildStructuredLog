using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildAnalyzer
    {
        private Build build;

        public BuildAnalyzer(Build build)
        {
            this.build = build;
        }

        public static void AnalyzeBuild(Build build)
        {
            var analyzer = new BuildAnalyzer(build);
            analyzer.Analyze();
        }

        private void Analyze()
        {
            build.VisitAllChildren<Target>(t => MarkAsLowRelevanceIfNeeded(t));
        }

        private void MarkAsLowRelevanceIfNeeded(Target target)
        {
            if (target.Children.All(c => c is Message))
            {
                target.IsLowRelevance = true;
                foreach (var child in target.Children.OfType<LogProcessNode>())
                {
                    child.IsLowRelevance = true;
                }
            }
        }
    }
}
