using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class MessageTaskAnalyzer
    {
        public static void Analyze(Task task)
        {
            var message = task.Children.OfType<Message>().FirstOrDefault();
            if (message?.ShortenedText != null)
            {
                task.Name = "Message: " + message.ShortenedText;
            }
        }
    }
}
