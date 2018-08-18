using System.Linq;

using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Core.Analyzers
{
    public static class MessageTaskAnalyzer
    {
        public static void Analyze(Task task)
        {
            var message = task.Children.OfType<Message>().FirstOrDefault();
            if (message?.ShortenedText != null)
            {
                task.Title = "Message: " + message.ShortenedText;
            }
        }
    }
}
