using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class RobocopyTask : CopyTask
    {
        public override string TypeName => nameof(RobocopyTask);

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

                match = Strings.RobocopyingFileFromRegex.Match(text);
                if (match.Success && match.Groups.Count > 2)
                {
                    var operation = ParseCopyingFileFrom(match);
                    list.Add(operation);
                    continue;
                }

                // TODO: Robocopy with hard links and assess

                //match = Strings.DidNotCopyRegex.Match(text);
                //if (match.Success && match.Groups.Count > 2)
                //{
                //    var operation = ParseCopyingFileFrom(match, copied: false);
                //    list.Add(operation);
                //    continue;
                //}

                //match = Strings.CreatingHardLinkRegex.Match(text);
                //if (match.Success && match.Groups.Count > 2)
                //{
                //    var operation = ParseCopyingFileFrom(match);
                //    list.Add(operation);
                //    continue;
                //}
            }

            return list;
        }
    }
}
