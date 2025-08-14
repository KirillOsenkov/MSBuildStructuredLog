using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="CscTask"/> class.
    /// </summary>
    public class CscTaskTests
    {
        private readonly CscTask _cscTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="CscTaskTests"/> class.
        /// </summary>
        public CscTaskTests()
        {
            // Arrange: Create an instance of the CscTask class.
            _cscTask = new CscTask();
        }

        /// <summary>
        /// Tests that the CscTask constructor creates a non-null instance.
        /// </summary>
        [Fact]
        public void CscTask_Constructor_CreatesInstance()
        {
            // Act is the constructor invocation in the test class constructor.
            
            // Assert: The instance should be successfully created and not be null.
            Assert.NotNull(_cscTask);
        }
    }
}
