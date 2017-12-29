using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class TextUtilitiesTests
    {
        [Fact]
        public void TestGetLines()
        {
            T("", "");
            T2("", "");
            T("a", "a");
            T2("a", "a");
            T("a\r\nb", "a", "b");
            T2("a\r\nb", "a\r\n", "b");
            T("a\nb", "a", "b");
            T2("a\nb", "a\n", "b");
            T("a\rb", "a", "b");
            T2("a\rb", "a\r", "b");
            T("\r", "", "");
            T2("\r", "\r", "");
            T("\n", "", "");
            T2("\n", "\n", "");
            T("\n\r", "", "", "");
            T2("\n\r", "\n", "\r", "");
            T("\r\n", "", "");
            T2("\r\n", "\r\n", "");
            T("\r\n\r", "", "", "");
            T2("\r\n\r", "\r\n", "\r", "");
            T("a\r\na\r\na", "a", "a", "a");
            T2("a\r\na\r\na", "a\r\n", "a\r\n", "a");
        }

        private static void T(string text, params string[] expectedLines)
        {
            var actualLines = text.GetLines();
            Assert.Equal(expectedLines, actualLines);
        }

        private static void T2(string text, params string[] expectedLines)
        {
            var actualLines = text.GetLines(includeLineBreak: true);
            Assert.Equal(expectedLines, actualLines);
        }
    }
}
