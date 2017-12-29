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
            T("a\r\nb\nc\rd", "a", "b", "c", "d");
            T2("a\r\nb\nc\rd", "a\r\n", "b\n", "c\r", "d");
        }

        [Fact]
        public void TestGetNumberOfLeadingSpaces()
        {
            var text = "abcd   efghi";
            Assert.Equal(3, Utilities.GetNumberOfLeadingSpaces(text, new Span(4, 6)));
            Assert.Equal(2, Utilities.GetNumberOfLeadingSpaces(text, new Span(4, 2)));
            Assert.Equal(1, Utilities.GetNumberOfLeadingSpaces(text, new Span(5, 1)));
            Assert.Equal(0, Utilities.GetNumberOfLeadingSpaces(text, new Span(1, 3)));
        }

        [Fact]
        public void TestSkip()
        {
            Assert.Equal(3, new Span(1, 10).Skip(2).Start);
            Assert.Equal(8, new Span(1, 10).Skip(2).Length);
        }

        [Fact]
        public void TestStringOperations()
        {
            var text = "abcd   efghi";
            Assert.Equal("   efg", text.Substring(new Span(4, 6)));
            Assert.Equal(true, text.Contains(new Span(7, 4), 'e'));
            Assert.Equal(7, text.IndexOf(new Span(7, 4), 'e'));
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
