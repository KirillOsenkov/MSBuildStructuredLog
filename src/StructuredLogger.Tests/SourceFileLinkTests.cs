using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class SourceFileLinkTests
    {
        private const string Original = @"C:\Program Files\MSBuild\Microsoft.Common.targets";
        private const string Replacement = @"D:\User's Projects (test)\Custom.targets";
        private static readonly ISourceFileResolver resolver = new ArchiveFileResolver(new[]
        {
            new ArchiveFile(Original, "<Project />"),
            new ArchiveFile(Replacement, "<Project />"),
            new ArchiveFile("/home/user/my project/custom.targets", "<Project />"),
            new ArchiveFile(@"\\server\share\my project\custom.targets", "<Project />")
        });

        public static IEnumerable<object[]> Cultures => StringsSet.ResourcesCollection.Keys.Select(c => new object[] { c });

        [Theory]
        [MemberData(nameof(Cultures))]
        public void LinksBothOverriddenTargetFilesInEverySupportedCulture(string culture)
        {
            var format = new StringsSet(culture).GetString("OverridingTarget");
            var text = string.Format(format, "Publish", Original, "Publish", Replacement);
            var links = SourceFileLink.Find(text, resolver).ToArray();

            Assert.Equal(2, links.Length);
            Assert.Equal(new[] { Original, Replacement }.OrderBy(p => text.IndexOf(p, StringComparison.Ordinal)), links.Select(l => l.FilePath));
            foreach (var link in links)
            {
                Assert.Equal(link.FilePath, text.Substring(link.Start, link.Length));
            }

            var first = links[0];
            Assert.Equal(first.FilePath, Assert.Single(SourceFileLink.Find(text, resolver, first.Start + first.Length)).FilePath);
        }

        [Theory]
        [InlineData("\"{0}\" was imported from \"{1}\".")]
        [InlineData("Using \"{0}\" and then \"{1}\".")]
        [InlineData("First: \"{0}\".\r\nSecond: \"{1}\".")]
        public void LinksQuotedPathsInOtherMessages(string format)
        {
            var text = string.Format(format, Original, Replacement);
            Assert.Equal(new[] { Original, Replacement }, SourceFileLink.Find(text, resolver).Select(l => l.FilePath));
        }

        [Theory]
        [InlineData("/home/user/my project/custom.targets")]
        [InlineData(@"\\server\share\my project\custom.targets")]
        public void LinksUnixAndUncPaths(string path)
        {
            foreach (var quote in new[] { "\"", "'" })
            {
                var text = "Imported " + quote + path + quote + ".";
                var link = Assert.Single(SourceFileLink.Find(text, resolver));
                Assert.Equal(path, link.FilePath);
                Assert.Equal(path, text.Substring(link.Start, link.Length));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Overriding target \"Publish\".")]
        [InlineData("Missing \"Z:\\not-in-the-archive\\missing.targets\".")]
        [InlineData("Unterminated \"C:\\Program Files\\MSBuild\\Microsoft.Common.targets")]
        public void MissingOrIncompletePathsAreNotLinks(string text)
        {
            Assert.Empty(SourceFileLink.Find(text, resolver));
        }

        [Fact]
        public void OnlyCompleteVisiblePathsAreLinkedInShortenedMessages()
        {
            var text = $"Using \"{Original}\" and \"{Replacement}\".";
            var first = SourceFileLink.Find(text, resolver).First();
            Assert.Empty(SourceFileLink.Find(text, resolver, first.Start + first.Length - 1));
            Assert.Equal(Original, Assert.Single(SourceFileLink.Find(text, resolver, first.Start + first.Length)).FilePath);
        }

        [Fact]
        public void UnavailableOverridePathDoesNotHideTheOtherLink()
        {
            var format = new StringsSet("en-US").GetString("OverridingTarget");
            var text = string.Format(format, "Publish", @"Z:\missing.targets", "Publish", Replacement);
            Assert.Equal(Replacement, Assert.Single(SourceFileLink.Find(text, resolver)).FilePath);
        }

        [Fact]
        public void AssemblyReferencesAreNotReadOrLinked()
        {
            const string assemblyPath = @"C:\MSBuild\Task.dll";
            Assert.Empty(SourceFileLink.Find($"Using task assembly \"{assemblyPath}\".", new UnexpectedFileResolver()));
        }

        [Theory]
        [InlineData(" (57,11)")]
        [InlineData("(57,11)")]
        [InlineData(" ( 57, 11 )")]
        public void LinksQuotedSourceLocationsWithoutChangingTheDisplayedText(string suffix)
        {
            var text = $"The target \"CompileDesignTime\" listed in a BeforeTargets attribute at \"{Original}{suffix}\" does not exist in the project, and will be ignored.";
            var link = Assert.Single(SourceFileLink.Find(text, resolver));

            Assert.Equal(Original, link.FilePath);
            Assert.Equal(57, link.LineNumber);
            Assert.Equal(11, link.ColumnNumber);
            Assert.Equal(Original + suffix, link.Text);
            Assert.Equal(link.Text, text.Substring(link.Start, link.Length));
            Assert.Empty(SourceFileLink.Find(text, resolver, link.Start + link.Length - 1));
        }

        [Theory]
        [InlineData(" (2147483648,11)")]
        [InlineData(" (57,2147483648)")]
        [InlineData(" (0,11)")]
        [InlineData(" (57,-1)")]
        [InlineData(" (57,)")]
        public void InvalidSourceLocationsAreNotLinks(string suffix)
        {
            Assert.Empty(SourceFileLink.Find($"File \"{Original}{suffix}\".", resolver));
        }

        private sealed class UnexpectedFileResolver : ISourceFileResolver
        {
            public SourceText GetSourceFileText(string filePath) => throw new InvalidOperationException(filePath);
        }
    }
}
