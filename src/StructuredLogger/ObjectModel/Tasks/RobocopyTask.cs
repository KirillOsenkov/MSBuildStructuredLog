using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class RobocopyTask : CopyTask
    {
        //public override string TypeName => nameof(RobocopyTask);

        protected override IEnumerable<FileCopyOperation> GetFileCopyOperations()
        {
            if (!HasChildren)
            {
                return Enumerable.Empty<FileCopyOperation>();
            }

            List<FileCopyOperation> list = new List<FileCopyOperation>();

            Match match;
            foreach (var message in Children.OfType<Message>())
            {
                var text = message.Text;

                match = Strings.RobocopyFileCopiedRegex.Match(text);
                if (match.Success && match.Groups.Count > 2)
                {
                    var operation = ParseCopyingFileFrom(match);
                    operation.Message = message;
                    list.Add(operation);
                    continue;
                }

                match = Strings.RobocopyFileSkippedRegex.Match(text);
                if (match.Success && match.Groups.Count > 2)
                {
                    var operation = ParseCopyingFileFrom(match, copied: false);
                    operation.Message = message;
                    list.Add(operation);
                    continue;
                }

                match = Strings.RobocopyFileSkippedAsDuplicateRegex.Match(text);
                if (match.Success && match.Groups.Count > 2)
                {
                    var operation = ParseCopyingFileFrom(match, copied: false);
                    operation.Message = message;
                    list.Add(operation);
                    continue;
                }

                match = Strings.RobocopyFileFailedRegex.Match(text);
                if (match.Success && match.Groups.Count > 2)
                {
                    var operation = ParseCopyingFileFrom(match, copied: false);
                    operation.Message = message;
                    list.Add(operation);
                    continue;
                }
            }

            return list;
        }
    }
}
