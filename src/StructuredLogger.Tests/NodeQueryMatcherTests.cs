using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogViewer.Tests
{
    public class NodeQueryMatcherTests
    {
        [Theory]
        [InlineData("name^=Q", "Q", true)]
        [InlineData("name^=q", "Quality", true)]
        [InlineData("name^=Q", "FooQ", false)]
        [InlineData("name$=Q", "FooQ", true)]
        [InlineData("name$=q", "FooQ", true)]
        [InlineData("name$=Q", "QFoo", false)]
        [InlineData("name^=", "Quality", false)]
        [InlineData("name$=", "Quality", false)]
        [InlineData("name^=Q", "", true)]
        [InlineData("name^ = Q", "Quality", true)]
        [InlineData("name$ = Q", "FooQ", true)]
        [InlineData("name^ = Q", "Quality", true)]
        [InlineData("name$ = Q", "FooQ", true)]
        public void NamePrefixAndSuffixFilters(string filter, string name, bool expected)
        {
            var matcher = new NodeQueryMatcher("$property " + filter);
            var property = new Property { Name = name, Value = "Q" };
            Assert.Equal(expected, matcher.IsMatch(property) != null);
        }

        [Fact]
        public void OrdinaryPropertySearchRemainsSubstringSearchAcrossNameAndValue()
        {
            var matcher = new NodeQueryMatcher("$property Q");
            Assert.NotNull(matcher.IsMatch(new Property { Name = "Quality", Value = "" }));
            Assert.NotNull(matcher.IsMatch(new Property { Name = "Other", Value = "Q" }));
            Assert.Null(matcher.IsMatch(new Property { Name = "Other", Value = "Debug" }));
        }

        [Fact]
        public void NamePrefixFilterDoesNotMatchValue()
        {
            var matcher = new NodeQueryMatcher("$property name^=Q");
            Assert.Null(matcher.IsMatch(new Property { Name = "Other", Value = "Quality" }));
        }

        [Fact]
        public void NamePrefixCanBeCombinedWithValueFilter()
        {
            var matcher = new NodeQueryMatcher("$property name^=Q value=Debug");
            Assert.NotNull(matcher.IsMatch(new Property { Name = "Quality", Value = "Debug" }));
            Assert.Null(matcher.IsMatch(new Property { Name = "OtherQuality", Value = "Debug" }));
            Assert.Null(matcher.IsMatch(new Property { Name = "Quality", Value = "Release" }));
        }
    }
}
