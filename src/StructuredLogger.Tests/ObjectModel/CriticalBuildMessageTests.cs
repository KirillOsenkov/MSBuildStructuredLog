using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="CriticalBuildMessage"/> class.
    /// </summary>
    public class CriticalBuildMessageTests
    {
        private readonly CriticalBuildMessage _criticalBuildMessage;

        /// <summary>
        /// Initializes a new instance of the <see cref="CriticalBuildMessageTests"/> class.
        /// </summary>
        public CriticalBuildMessageTests()
        {
            _criticalBuildMessage = new CriticalBuildMessage();
        }

        /// <summary>
        /// Tests the <see cref="CriticalBuildMessage.TypeName"/> property to ensure it returns the correct type name.
        /// This test follows the Arrange-Act-Assert pattern and validates that the returned value matches "CriticalBuildMessage".
        /// </summary>
        [Fact]
        public void TypeName_WhenCalled_ReturnsCriticalBuildMessage()
        {
            // Act
            string typeName = _criticalBuildMessage.TypeName;
            
            // Assert
            Assert.Equal("CriticalBuildMessage", typeName);
        }
    }
}
