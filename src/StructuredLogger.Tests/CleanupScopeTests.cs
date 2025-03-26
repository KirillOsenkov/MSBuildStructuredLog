using DotUtils.StreamUtils;
using System;
using Xunit;

namespace DotUtils.StreamUtils.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="CleanupScope"/> struct.
    /// </summary>
    public class CleanupScopeTests
    {
        /// <summary>
        /// Tests that the Dispose method executes the provided dispose action.
        /// Arrange: A flag is set to false initially and a CleanupScope is created with an action that sets the flag to true.
        /// Act: The Dispose method is called.
        /// Assert: The flag is verified to be true, indicating that the action was executed.
        /// </summary>
        [Fact]
        public void Dispose_WhenDisposeActionProvided_ExecutesAction()
        {
            // Arrange
            bool actionExecuted = false;
            var cleanupScope = new CleanupScope(() => actionExecuted = true);

            // Act
            cleanupScope.Dispose();

            // Assert
            Assert.True(actionExecuted, "Dispose did not execute the provided action.");
        }

        /// <summary>
        /// Tests that the Dispose method throws a NullReferenceException when a null dispose action is provided.
        /// Arrange: A CleanupScope is created with a null action.
        /// Act & Assert: Calling Dispose should throw a NullReferenceException.
        /// </summary>
        [Fact]
        public void Dispose_WhenDisposeActionIsNull_ThrowsNullReferenceException()
        {
            // Arrange
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            var cleanupScope = new CleanupScope(null);
#pragma warning restore CS8625

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => cleanupScope.Dispose());
        }

        /// <summary>
        /// Tests that calling Dispose multiple times executes the action each time.
        /// Arrange: A counter is initialized and a CleanupScope is created with an action that increments the counter.
        /// Act: The Dispose method is called twice.
        /// Assert: The counter's value is 2, indicating the action was executed on each call.
        /// </summary>
        [Fact]
        public void Dispose_WhenCalledMultipleTimes_ActionIsExecutedEachTime()
        {
            // Arrange
            int counter = 0;
            var cleanupScope = new CleanupScope(() => counter++);

            // Act
            cleanupScope.Dispose();
            cleanupScope.Dispose();

            // Assert
            Assert.Equal(2, counter);
        }
    }
}
