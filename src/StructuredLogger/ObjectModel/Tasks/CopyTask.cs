using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class CopyTask : Task
    {
        public override string TypeName => nameof(CopyTask);

        private IEnumerable<FileCopyOperation> fileCopyOperations;
        public IEnumerable<FileCopyOperation> FileCopyOperations => fileCopyOperations ?? (fileCopyOperations = GetFileCopyOperations());

        private IEnumerable<FileCopyOperation> GetFileCopyOperations()
        {
            if (!HasChildren)
            {
                return Enumerable.Empty<FileCopyOperation>();
            }

            List<FileCopyOperation> list = new List<FileCopyOperation>();

            foreach (var message in this.Children.OfType<Message>())
            {
                var text = message.Text;
                if (text.StartsWith(copyingFileFrom))
                {
                    var operation = ParseCopyingFileFrom(text, copyingFileFrom, to);
                    list.Add(operation);
                }
                else if (text.StartsWith(creatingHardLink))
                {
                    var operation = ParseCopyingFileFrom(text, creatingHardLink, to);
                    list.Add(operation);
                }
                else if (text.StartsWith(didNotCopy))
                {
                    var operation = ParseCopyingFileFrom(text, didNotCopy, toFile);
                    operation.Copied = false;
                    list.Add(operation);
                }
            }

            return list;
        }

        private static readonly string copyingFileFrom = Strings.CopyingFileFrom;
        private static readonly string creatingHardLink = Strings.CreatingHardLink;
        private static readonly string didNotCopy = Strings.DidNotCopy;
        private static readonly string to = Strings.To;
        private static readonly string toFile = Strings.ToFile;

        private static FileCopyOperation ParseCopyingFileFrom(string text, string prefix, string infix)
        {
            var result = new FileCopyOperation();

            var split = text.IndexOf(infix);
            var prefixLength = prefix.Length;
            int toLength = infix.Length;
            result.Source = text.Substring(prefixLength, split - prefixLength);
            result.Destination = text.Substring(split + toLength, text.Length - 2 - split - toLength);
            result.Copied = true;

            return result;
        }
    }
}
