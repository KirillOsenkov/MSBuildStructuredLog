using System.Text;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Xunit;

namespace StructuredLogger.Tests
{
    internal static class AssertEx
    {
        /// <summary>
        /// Asserts that two strings are equal, and prints a diff between the two if they are not.
        /// </summary>
        /// <param name="expected">The expected string. This is presented as the "baseline/before" side in the diff.</param>
        /// <param name="actual">The actual string. This is presented as the changed or "after" side in the diff.</param>
        /// <param name="message">The message to precede the diff, if the values are not equal.</param>
        public static void EqualOrDiff(string expected, string actual, string message = null)
        {
            if (expected == actual)
            {
                return;
            }

            var diffBuilder = new InlineDiffBuilder(new DiffPlex.Differ());
            var diff = diffBuilder.BuildDiffModel(expected, actual, ignoreWhitespace: false);
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine(
                string.IsNullOrEmpty(message)
                    ? "Actual and expected values differ. Expected shown in baseline of diff:"
                    : message);

            foreach (var line in diff.Lines)
            {
                switch (line.Type)
                {
                    case ChangeType.Inserted:
                        messageBuilder.Append("+");
                        break;
                    case ChangeType.Deleted:
                        messageBuilder.Append("-");
                        break;
                    default:
                        messageBuilder.Append(" ");
                        break;
                }

                messageBuilder.AppendLine(line.Text);
            }

            Assert.Fail(messageBuilder.ToString());
        }
    }
}
