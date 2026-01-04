using FluentAssertions;
using Xunit;

namespace BinlogTool.Tests;

public class BinlogDiscoveryTests
{
    [Fact]
    public void ParseCsvPaths_WithSinglePath_ReturnsSingleItem()
    {
        // Arrange
        var input = "build.binlog";

        // Act
        var result = BinlogDiscovery.ParseCsvPaths(input);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().Be("build.binlog");
    }

    [Fact]
    public void ParseCsvPaths_WithMultiplePaths_ReturnsAllPaths()
    {
        // Arrange
        var input = "build.binlog,test.binlog,deploy.binlog";

        // Act
        var result = BinlogDiscovery.ParseCsvPaths(input);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Be("build.binlog");
        result[1].Should().Be("test.binlog");
        result[2].Should().Be("deploy.binlog");
    }

    [Fact]
    public void ParseCsvPaths_WithQuotedPathContainingComma_TreatsCommaAsLiteral()
    {
        // Arrange
        var input = "\"C:\\Program Files\\Build, Test.binlog\",other.binlog";

        // Act
        var result = BinlogDiscovery.ParseCsvPaths(input);

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Be("C:\\Program Files\\Build, Test.binlog");
        result[1].Should().Be("other.binlog");
    }

    [Fact]
    public void ParseCsvPaths_WithSpacesAroundPaths_TrimsWhitespace()
    {
        // Arrange
        var input = " build.binlog , test.binlog , deploy.binlog ";

        // Act
        var result = BinlogDiscovery.ParseCsvPaths(input);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Be("build.binlog");
        result[1].Should().Be("test.binlog");
        result[2].Should().Be("deploy.binlog");
    }
}
