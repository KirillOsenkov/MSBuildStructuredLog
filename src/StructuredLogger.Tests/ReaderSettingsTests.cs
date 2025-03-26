using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ReaderSettings"/> class.
    /// </summary>
    public class ReaderSettingsTests
    {
        /// <summary>
        /// Tests that the static Default property returns an instance with UnknownDataBehavior set to Warning.
        /// This test verifies that the default settings are correctly initialized.
        /// </summary>
        [Fact]
        public void Default_Get_ReturnsInstanceWithUnknownDataBehaviorWarning()
        {
            // Act
            ReaderSettings defaultSettings = ReaderSettings.Default;

            // Assert
            Assert.NotNull(defaultSettings);
            Assert.Equal(UnknownDataBehavior.Warning, defaultSettings.UnknownDataBehavior);
        }

        /// <summary>
        /// Tests that multiple calls to the static Default property return the same instance.
        /// This ensures the singleton behavior of the Default property.
        /// </summary>
        [Fact]
        public void Default_MultipleCalls_ReturnSameInstance()
        {
            // Act
            ReaderSettings firstCall = ReaderSettings.Default;
            ReaderSettings secondCall = ReaderSettings.Default;

            // Assert
            Assert.Same(firstCall, secondCall);
        }

        /// <summary>
        /// Tests that the UnknownDataBehavior property can be updated and retrieved correctly.
        /// This test covers the set and get functionality of the property.
        /// </summary>
        [Fact]
        public void UnknownDataBehavior_SetValue_UpdatesProperty()
        {
            // Arrange
            var settings = new ReaderSettings();
            
            // Act
            settings.UnknownDataBehavior = UnknownDataBehavior.Warning; // Using Warning as test value; if additional enum values existed, they could be used to further verify behavior.
            
            // Assert
            Assert.Equal(UnknownDataBehavior.Warning, settings.UnknownDataBehavior);
        }
    }
}
