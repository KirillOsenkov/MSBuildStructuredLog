using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="StringsSet"/> class.
    /// </summary>
    public class StringsSetTests
    {
        private const string ValidCulture = "en-US";
        private const string InvalidCulture = "fr-FR";
        private readonly Dictionary<string, Dictionary<string, string>> _testResources;

        public StringsSetTests()
        {
            // Initialize a test resources dictionary.
            _testResources = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    ValidCulture, new Dictionary<string, string>
                    {
                        { "greeting", "Hello" },
                        { "farewell", "Goodbye" }
                    }
                }
            };

            // Set the static resourcesCollection field of StringsSet to our test dictionary.
            SetResourcesCollection(_testResources);
        }

        /// <summary>
        /// Sets the private static 'resourcesCollection' field of the StringsSet class via reflection.
        /// </summary>
        /// <param name="resources">The test resources to set.</param>
        private static void SetResourcesCollection(Dictionary<string, Dictionary<string, string>> resources)
        {
            var field = typeof(StringsSet).GetField("resourcesCollection", BindingFlags.NonPublic | BindingFlags.Static);
            field.SetValue(null, resources);
        }

        /// <summary>
        /// Tests the GetString method when the key exists in the current set; expects the correct value.
        /// </summary>
        [Fact]
        public void GetString_ExistingKey_ReturnsValue()
        {
            // Arrange
            var stringsSet = new StringsSet(ValidCulture);
            var expectedValue = "Hello";

            // Act
            var actualValue = stringsSet.GetString("greeting");

            // Assert
            Assert.Equal(expectedValue, actualValue);
        }

        /// <summary>
        /// Tests the GetString method when the key does not exist in the current set; expects an empty string.
        /// </summary>
        [Fact]
        public void GetString_NonExistingKey_ReturnsEmpty()
        {
            // Arrange
            var stringsSet = new StringsSet(ValidCulture);

            // Act
            var actualValue = stringsSet.GetString("nonexistent");

            // Assert
            Assert.Equal(string.Empty, actualValue);
        }

        /// <summary>
        /// Tests the GetString method with a null key; expects an ArgumentNullException.
        /// </summary>
        [Fact]
        public void GetString_NullKey_ThrowsArgumentNullException()
        {
            // Arrange
            var stringsSet = new StringsSet(ValidCulture);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => stringsSet.GetString(null));
        }

        /// <summary>
        /// Tests the constructor to ensure that providing an invalid culture (not present in the resourcesCollection) throws a KeyNotFoundException.
        /// </summary>
        [Fact]
        public void Constructor_InvalidCulture_ThrowsKeyNotFoundException()
        {
            // Arrange
            // Ensure that the test resources do not contain the invalid culture.
            // (Already _testResources only contains "en-US".)

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => new StringsSet(InvalidCulture));
        }

        /// <summary>
        /// Tests the Culture property getter and setter to ensure they work as expected.
        /// </summary>
        [Fact]
        public void CultureProperty_GetSet_WorksCorrectly()
        {
            // Arrange
            var initialCulture = ValidCulture;
            var newCulture = "es-ES";
            // Add new culture to test resources for safe construction.
            _testResources[newCulture] = new Dictionary<string, string> { { "greeting", "Hola" } };
            SetResourcesCollection(_testResources);
            var stringsSet = new StringsSet(initialCulture);

            // Act & Assert: Initial culture is set from constructor.
            Assert.Equal(initialCulture, stringsSet.Culture);

            // Act: Set the Culture property.
            stringsSet.Culture = newCulture;

            // Assert: Verify that the Culture property reflects the new value.
            Assert.Equal(newCulture, stringsSet.Culture);
        }

        /// <summary>
        /// Tests the ResourcesCollection static property to ensure that if it is manually set,
        /// subsequent calls return the same instance.
        /// </summary>
        [Fact]
        public void ResourcesCollection_StaticProperty_ReturnsSameInstanceIfSet()
        {
            // Arrange
            var expectedResources = _testResources;

            // Act
            var actualResources = StringsSet.ResourcesCollection;

            // Assert
            Assert.Same(expectedResources, actualResources);
        }
    }
}
