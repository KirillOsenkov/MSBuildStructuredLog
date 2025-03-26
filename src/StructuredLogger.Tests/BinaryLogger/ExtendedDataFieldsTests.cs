using System;
using System.Collections.Generic;
using Microsoft.Build.Logging;
using Xunit;

namespace Microsoft.Build.Logging.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ExtendedDataFields"/> class.
    /// </summary>
    public class ExtendedDataFieldsTests
    {
        private readonly ExtendedDataFields _extendedDataFields;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtendedDataFieldsTests"/> class.
        /// </summary>
        public ExtendedDataFieldsTests()
        {
            _extendedDataFields = new ExtendedDataFields();
        }

        /// <summary>
        /// Tests that upon instantiation, all properties of <see cref="ExtendedDataFields"/> are null.
        /// Expected Outcome: ExtendedType, ExtendedMetadata and ExtendedData are null.
        /// </summary>
        [Fact]
        public void Constructor_DefaultValues_AreNull()
        {
            // Act & Assert
            Assert.Null(_extendedDataFields.ExtendedType);
            Assert.Null(_extendedDataFields.ExtendedMetadata);
            Assert.Null(_extendedDataFields.ExtendedData);
        }

        /// <summary>
        /// Tests that the ExtendedType property getter and setter operate correctly.
        /// Expected Outcome: The property returns the same value that was set.
        /// </summary>
        [Fact]
        public void ExtendedType_SetAndGet_ReturnsExpectedValue()
        {
            // Arrange
            string expectedValue = "SampleType";

            // Act
            _extendedDataFields.ExtendedType = expectedValue;

            // Assert
            Assert.Equal(expectedValue, _extendedDataFields.ExtendedType);
        }

        /// <summary>
        /// Tests that the ExtendedData property getter and setter operate correctly.
        /// Expected Outcome: The property returns the same value that was set.
        /// </summary>
        [Fact]
        public void ExtendedData_SetAndGet_ReturnsExpectedValue()
        {
            // Arrange
            string expectedData = "Sample extended data";

            // Act
            _extendedDataFields.ExtendedData = expectedData;

            // Assert
            Assert.Equal(expectedData, _extendedDataFields.ExtendedData);
        }

        /// <summary>
        /// Tests that the ExtendedMetadata property getter and setter operate correctly when assigned 
        /// a non-null dictionary.
        /// Expected Outcome: The property returns the same dictionary instance that was set.
        /// </summary>
        [Fact]
        public void ExtendedMetadata_SetAndGet_NonNullDictionary_ReturnsExpectedValue()
        {
            // Arrange
            IDictionary<string, string> expectedDictionary = new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            };

            // Act
            _extendedDataFields.ExtendedMetadata = expectedDictionary;

            // Assert
            Assert.Equal(expectedDictionary, _extendedDataFields.ExtendedMetadata);
        }

        /// <summary>
        /// Tests that the ExtendedMetadata property getter and setter operate correctly when assigned a null value.
        /// Expected Outcome: The property returns null after being set to null.
        /// </summary>
        [Fact]
        public void ExtendedMetadata_SetAndGet_NullValue_ReturnsNull()
        {
            // Arrange & Act
            _extendedDataFields.ExtendedMetadata = null;

            // Assert
            Assert.Null(_extendedDataFields.ExtendedMetadata);
        }
    }
}
