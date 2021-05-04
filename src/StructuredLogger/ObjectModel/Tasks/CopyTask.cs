using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

            Match match;
            foreach (var message in this.Children.OfType<Message>())
            {
                var text = message.Text;

                match = Strings.CopyingFileFromRegex.Match(text);
                if (match.Success && match.Groups.Count > 2)
                {
                    var operation = ParseCopyingFileFrom(match);
                    list.Add(operation);
                    continue;
                }

                match = Strings.DidNotCopyRegex.Match(text);
                if (match.Success && match.Groups.Count > 2)
                {
                    var operation = ParseCopyingFileFrom(match, copied: false);
                    list.Add(operation);
                    continue;
                }

                match = Strings.CreatingHardLinkRegex.Match(text);
                if (match.Success && match.Groups.Count > 2)
                {
                    var operation = ParseCopyingFileFrom(match);
                    list.Add(operation);
                    continue;
                }
            }

            return list;
        }

        private static readonly string to = Strings.To;
        private static readonly string toFile = Strings.ToFile;

        private static FileCopyOperation ParseCopyingFileFrom(Match match, bool copied = true)
        {
            var result = new FileCopyOperation();

            result.Source = match.Groups["From"].Value;
            result.Destination = match.Groups["To"].Value;
            result.Copied = copied;

            return result;
        }
    }
}
