using System;
using System.Reflection;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="PlatformUtilities"/> class.
    /// </summary>
    public class PlatformUtilitiesTests
    {
        /// <summary>
        /// Tests the <see cref="PlatformUtilities.HasThreads"/> property for various simulated platform conditions.
        /// This test sets the private static readonly fields _isBrowser and _isWasi via reflection,
        /// then verifies that the HasThreads property returns the expected value according to the logic:
        /// HasThreads = !_isBrowser && !_isWasi.
        /// </summary>
        /// <param name="isBrowser">Simulated value for _isBrowser field.</param>
        /// <param name="isWasi">Simulated value for _isWasi field.</param>
        /// <param name="expected">Expected result of the HasThreads property.</param>
        [Theory]
        [InlineData(false, false, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, true, false)]
        public void HasThreads_WhenPlatformConditionsVary_ReturnsExpectedResult(bool isBrowser, bool isWasi, bool expected)
        {
            // Arrange
            Type platformType = typeof(PlatformUtilities);
            FieldInfo browserField = platformType.GetField("_isBrowser", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo wasiField = platformType.GetField("_isWasi", BindingFlags.NonPublic | BindingFlags.Static);
            bool originalBrowser = (bool)browserField.GetValue(null);
            bool originalWasi = (bool)wasiField.GetValue(null);

            try
            {
                browserField.SetValue(null, isBrowser);
                wasiField.SetValue(null, isWasi);

                // Act
                bool actualHasThreads = PlatformUtilities.HasThreads;

                // Assert
                Assert.Equal(expected, actualHasThreads);
            }
            finally
            {
                // Restore the original values
                browserField.SetValue(null, originalBrowser);
                wasiField.SetValue(null, originalWasi);
            }
        }

        /// <summary>
        /// Tests the <see cref="PlatformUtilities.HasTempStorage"/> property for different WASI conditions.
        /// This test simulates different values for the _isWasi field using reflection, and asserts that
        /// HasTempStorage returns true when _isWasi is false and false when _isWasi is true.
        /// </summary>
        /// <param name="isWasi">Simulated value for _isWasi field.</param>
        /// <param name="expected">Expected result of the HasTempStorage property.</param>
        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void HasTempStorage_WhenWasiConditionVaries_ReturnsExpectedResult(bool isWasi, bool expected)
        {
            // Arrange
            Type platformType = typeof(PlatformUtilities);
            FieldInfo browserField = platformType.GetField("_isBrowser", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo wasiField = platformType.GetField("_isWasi", BindingFlags.NonPublic | BindingFlags.Static);
            bool originalBrowser = (bool)browserField.GetValue(null);
            bool originalWasi = (bool)wasiField.GetValue(null);

            try
            {
                // The _isBrowser value does not affect HasTempStorage, set it arbitrarily.
                browserField.SetValue(null, false);
                wasiField.SetValue(null, isWasi);

                // Act
                bool actualHasTempStorage = PlatformUtilities.HasTempStorage;

                // Assert
                Assert.Equal(expected, actualHasTempStorage);
            }
            finally
            {
                // Restore the original values
                browserField.SetValue(null, originalBrowser);
                wasiField.SetValue(null, originalWasi);
            }
        }

        /// <summary>
        /// Tests the <see cref="PlatformUtilities.HasColor"/> property for different WASI conditions.
        /// This test simulates different values for the _isWasi field using reflection, and asserts that
        /// HasColor returns true when _isWasi is false and false when _isWasi is true.
        /// </summary>
        /// <param name="isWasi">Simulated value for _isWasi field.</param>
        /// <param name="expected">Expected result of the HasColor property.</param>
        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void HasColor_WhenWasiConditionVaries_ReturnsExpectedResult(bool isWasi, bool expected)
        {
            // Arrange
            Type platformType = typeof(PlatformUtilities);
            FieldInfo browserField = platformType.GetField("_isBrowser", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo wasiField = platformType.GetField("_isWasi", BindingFlags.NonPublic | BindingFlags.Static);
            bool originalBrowser = (bool)browserField.GetValue(null);
            bool originalWasi = (bool)wasiField.GetValue(null);

            try
            {
                // The _isBrowser value does not affect HasColor, set it arbitrarily.
                browserField.SetValue(null, false);
                wasiField.SetValue(null, isWasi);

                // Act
                bool actualHasColor = PlatformUtilities.HasColor;

                // Assert
                Assert.Equal(expected, actualHasColor);
            }
            finally
            {
                // Restore the original values
                browserField.SetValue(null, originalBrowser);
                wasiField.SetValue(null, originalWasi);
            }
        }
    }
}
