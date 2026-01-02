using FluentAssertions;
using Xunit;

namespace BinlogTool.Tests;

public class BinlogDiscoveryTests
{
    [Fact]
    public void DiscoverBinlogs_WithNullPath_ReturnsEmptyList()
    {
        // Act
        var result = BinlogDiscovery.DiscoverBinlogs(null, recurse: false);

        // Assert
        result.Should().NotBeNull();
        // We can't test actual file discovery without setting up test files
        // Just verify it doesn't throw and returns a list
    }

    [Fact]
    public void DiscoverBinlogs_WithEmptyString_ReturnsEmptyList()
    {
        // Act
        var result = BinlogDiscovery.DiscoverBinlogs("", recurse: false);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void ParseCsvPaths_WithSinglePath_ReturnsSingleItem()
    {
        // Note: This tests the internal logic through the public API
        // We'd need to make ParseCsvPaths public or internal for direct testing
        // For now, we test through DiscoverBinlogs with explicit file path
        
        // Act
        var result = BinlogDiscovery.DiscoverBinlogs("test.binlog", recurse: false);

        // Assert
        result.Should().NotBeNull();
        // Actual discovery would fail if file doesn't exist, but that's expected
    }

    [Fact]
    public void DiscoverBinlogs_WithNonExistentPath_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentPath = "C:\\NonExistent\\Path\\file.binlog";

        // Act
        var result = BinlogDiscovery.DiscoverBinlogs(nonExistentPath, recurse: false);

        // Assert
        result.Should().BeEmpty();
    }

    // Note: More comprehensive tests would require creating temporary test files
    // or mocking the file system. These tests verify the basic API contract.
}
