using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="FscTask"/> class.
    /// </summary>
    public class FscTaskTests
    {
        private readonly FscTask _fscTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="FscTaskTests"/> class.
        /// </summary>
        public FscTaskTests()
        {
            _fscTask = new FscTask();
        }

        /// <summary>
        /// Tests that the constructor instantiates the FscTask object successfully.
        /// </summary>
        [Fact]
        public void Constructor_WhenCalled_InstantiatesFscTask()
        {
            // Act & Assert
            Assert.NotNull(_fscTask);
        }

        /// <summary>
        /// Tests that FscTask inherits from ManagedCompilerTask.
        /// </summary>
        [Fact]
        public void Inheritance_FscTask_IsInstanceOfManagedCompilerTask()
        {
            // Act & Assert
            Assert.IsAssignableFrom<ManagedCompilerTask>(_fscTask);
        }
    }
}
