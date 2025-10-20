using System.Globalization;
using System.Linq;
using Xunit;

namespace StructuredLogger.Tests
{
    public class CommandLineDiffTests
    {
        public CommandLineDiffTests()
        {
            CultureInfo.DefaultThreadCurrentCulture = new("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new("en-US");
        }

        [Theory]
        [InlineData("")]
        [InlineData("csc.exe")]
        [InlineData("csc.exe -help")]
        [InlineData("csc.exe --help")]
        [InlineData("csc.exe /help")]
        [InlineData("csc.exe -pathmap:file1.txt=file2.txt")]
        [InlineData("csc.exe -out:program.exe file.cs")]
        public void CompareSame(string s)
        {
            Assert.True(CommandLineDiffer.TryCompare(s, s, out var lr, out var rr));
            Assert.Empty(lr);
            Assert.Empty(rr);
        }

        [Theory]
        [InlineData(@"", @"")]
        [InlineData(@"csc.exe", @"csc.exe")]
        [InlineData(@"csc.exe source.cs", @"csc.exe -out:file.exe source.cs", @"-out:file.exe")]
        [InlineData(@"csc.exe -out:file.exe source.cs", @"csc.exe -out:file.exe -embed source.cs", @"-embed")]
        [InlineData(@"csc.exe source1.cs source2.cs", @"csc.exe source2.cs source1.cs")]
        [InlineData(@"csc.exe -switch1 -switch2", @"csc.exe -switch2 -switch1")]
        [InlineData(@"csc.exe -switch1 -switch2", @"csc.exe -switch2 -switch1 -switch3", @"-switch3")]
        [InlineData(@"csc.exe -switch1 -switch1", @"csc.exe -switch1 -switch1 -switch1", @"-switch1")]
        [InlineData(@"csc.exe file.txt file.txt", @"csc.exe file.txt file.txt file.txt", @"file.txt")]
        [InlineData(@"csc.exe file.txt file.txt file.txt", @"csc.exe file.txt file.txt", @"file.txt")]
        [InlineData(@"csc.exe", @"csc.exe file.txt file.txt file.txt", @"file.txt", @"file.txt", @"file.txt")]
        public void CompareDifferent(string leftString, string rightString, params string[] expected)
        {
            var result = CommandLineDiffer.TryCompare(leftString, rightString, out var leftRemainder, out var rightRemainder);
            Assert.True(result);
            Assert.Equal(expected.Length, leftRemainder.Count + rightRemainder.Count);

            var actual = leftRemainder.Concat(rightRemainder).ToList();

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

        [Theory]
        [InlineData("csc.exe", "csc.exe")]
        [InlineData("csc.exe -Switch", "csc.exe -switch")]
        [InlineData("csc.exe -Switch", "csc.exe -switch1", @"-Switch", @"-switch1")]
        [InlineData(@"csc.exe -Switch Folder\File.txt", @"csc.exe -Switch folder\file.txt")]
        public void CompareCaseInsensitive(string leftString, string rightString, params string[] expected)
        {
            var setting = new CommandLineDiffer.CommandLineDiffSetting()
            {
                CaseSensitive = false,
            };

            var result = CommandLineDiffer.TryCompare(leftString, rightString, out var leftRemainder, out var rightRemainder, setting);
            Assert.True(result);
            Assert.Equal(expected.Length, leftRemainder.Count + rightRemainder.Count);

            var actual = leftRemainder.Concat(rightRemainder).ToList();

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

        [Theory]
        [InlineData(@"csc.exe --switch", @"csc.exe", @"--switch")]
        [InlineData(@"csc.exe --switch file.txt", @"csc.exe", @"--switch", @"file.txt")]
        [InlineData(@"csc.exe --switch1 ""--switch2""", @"csc.exe", @"--switch1", @"--switch2")]
        public void ParseCommandLine(string testString, params string[] expected)
        {
            var result = CommandLineDiffer.TryParseCommandLine(testString, out var actual);
            Assert.True(result);
            Assert.Equal(expected.Length, actual.Count);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

        [Theory]
        [InlineData(@"one", @"one")]
        [InlineData(@"""one""", @"one")]
        [InlineData(@"""one two""", @"one two")]
        [InlineData(@"one two", @"one", @"two")]
        [InlineData(@"one two three", @"one", @"two", @"three")]
        [InlineData(@"""one two"" three", @"one two", @"three")]
        public void ParseParameters(string testString, params string[] expected)
        {
            var actual = CommandLineDiffer.ParseParameters(testString, 0);

            Assert.Equal(expected.Length, actual.Count);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

        [Theory]
        [InlineData(@"csc.exe -switch", true, "csc.exe")]
        [InlineData(@"""csc.exe", false, "")]  // missing closing quote
        [InlineData(@"""csc.exe -switch", false, "")] // missing closing quote
        [InlineData(@"""folder\csc.exe"" -switch", true, @"folder\csc.exe")]
        [InlineData(@"C:\Program Folder(x86)\folder\csc.exe -switch", true, @"C:\Program Folder(x86)\folder\csc.exe")]
        [InlineData(@"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe -switch", true, @"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe")]
        [InlineData(@"""C:\Program Folder(x86)\folder\csc.exe"" -switch", true, @"C:\Program Folder(x86)\folder\csc.exe")]
        public void ProgramPaths(string testString, bool expected, string expectedOutput)
        {
            bool result = CommandLineDiffer.TryParseExe(testString, out string actualOutput);
            Assert.Equal(result, expected);
            Assert.Equal(expectedOutput, actualOutput);
        }
    }
}
