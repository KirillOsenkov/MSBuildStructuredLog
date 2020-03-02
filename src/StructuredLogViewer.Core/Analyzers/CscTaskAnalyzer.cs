using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class CscTaskAnalyzer
    {
        public static void Analyze(Task task)
        {
            Folder analyzerReport = null;
            Folder parent = null;

            foreach (var message in task.Children.OfType<Message>().ToArray())
            {
                var text = message.Text;
                if (text.StartsWith(Strings.TotalAnalyzerExecutionTime))
                {
                    analyzerReport = new Folder();
                    analyzerReport.Name = Strings.AnalyzerReport;
                    task.AddChild(analyzerReport);
                    parent = analyzerReport;
                }
                else if (text.Contains(", Version=") && analyzerReport != null)
                {
                    var lastAssembly = new Folder();
                    lastAssembly.Name = text;
                    analyzerReport.AddChild(lastAssembly);
                    parent = lastAssembly;
                }

                if (parent != null)
                {
                    message.Parent.Children.Remove(message);
                    parent.AddChild(message);
                }
            }
        }
    }
}
