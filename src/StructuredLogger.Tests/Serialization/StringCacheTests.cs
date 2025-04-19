using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="StringCache"/> class.
    /// </summary>
    public class StringCacheTests
    {
        private readonly StringCache _stringCache;

        public StringCacheTests()
        {
            _stringCache = new StringCache();
        }

        /// <summary>
        /// Tests that the constructor initializes the Instances property to an empty collection.
        /// </summary>
        [Fact]
        public void Constructor_InitializesInstancesAsEmptyCollection()
        {
            // Arrange & Act
            var cache = new StringCache();

            // Assert
            Assert.NotNull(cache.Instances);
            Assert.Empty(cache.Instances);
        }

        /// <summary>
        /// Tests that Intern returns the original text when null or empty.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Intern_NullOrEmptyInput_ReturnsSameInput(string input)
        {
            // Act
            string result = _stringCache.Intern(input);

            // Assert
            Assert.Equal(input, result);
        }

        /// <summary>
        /// Tests that when DisableDeduplication is true, Intern returns the input text without deduplication.
        /// </summary>
        [Fact]
        public void Intern_WhenDeduplicationDisabled_ReturnsInputUnmodified()
        {
            // Arrange
            _stringCache.DisableDeduplication = true;
            string input = "Test String";

            // Act
            string result = _stringCache.Intern(input);

            // Assert
            Assert.Equal(input, result);
        }

        /// <summary>
        /// Tests that Intern deduplicates strings for non-null, non-empty input when deduplication is enabled.
        /// Subsequent calls with equivalent input return the same instance.
        /// </summary>
        [Fact]
        public void Intern_WhenCalledTwice_ReturnsSameInstance()
        {
            // Arrange
            string input = "Test String";

            // Act
            string firstIntern = _stringCache.Intern(input);
            string secondIntern = _stringCache.Intern(input);

            // Assert
            Assert.Same(firstIntern, secondIntern);
        }

        /// <summary>
        /// Tests that SoftIntern returns the input text immediately if HasDeduplicatedStrings is true.
        /// </summary>
        [Fact]
        public void SoftIntern_WhenHasDeduplicatedStringsTrue_ReturnsInputImmediately()
        {
            // Arrange
            string input = "Test String";
            _stringCache.HasDeduplicatedStrings = true;

            // Act
            string result = _stringCache.SoftIntern(input);

            // Assert
            Assert.Equal(input, result);
        }

        /// <summary>
        /// Tests that SoftIntern deduplicates strings when HasDeduplicatedStrings is false.
        /// </summary>
        [Fact]
        public void SoftIntern_WhenHasDeduplicatedStringsFalse_DeduplicatesString()
        {
            // Arrange
            string input = "Test String";
            _stringCache.HasDeduplicatedStrings = false;

            // Act
            string firstIntern = _stringCache.SoftIntern(input);
            string secondIntern = _stringCache.SoftIntern(input);

            // Assert
            Assert.Same(firstIntern, secondIntern);
        }

        /// <summary>
        /// Tests that Intern(IEnumerable&lt;string&gt;) interns each string in the collection and deduplicates them.
        /// </summary>
        [Fact]
        public void InternEnumerable_ValidCollection_DeduplicatesStrings()
        {
            // Arrange
            var inputStrings = new List<string> { "One", "Two", "One", "Three" };

            // Act
            _stringCache.Intern(inputStrings);
            // Calling Intern twice with the same individual string should return same instance.
            string internedOne1 = _stringCache.Intern("One");
            string internedOne2 = _stringCache.Intern("One");

            // Assert
            Assert.Same(internedOne1, internedOne2);
        }

        /// <summary>
        /// Tests that Seal creates a new array with an empty string at index 0 and the deduplicated strings following.
        /// Also verifies that DisableDeduplication is set to true.
        /// </summary>
        [Fact]
        public void Seal_WhenCalled_UpdatesInstancesAndDisablesDeduplication()
        {
            // Arrange
            // Intern some strings first.
            _stringCache.Intern("Alpha");
            _stringCache.Intern("Beta");

            // Act
            _stringCache.Seal();

            // Assert
            Assert.True(_stringCache.DisableDeduplication);
            var instancesAsArray = _stringCache.Instances.ToArray();
            // There should be an empty string at the beginning.
            Assert.Equal("", instancesAsArray[0]);
            // The rest of the entries should contain the deduplicated strings.
            // Order is not guaranteed because Dictionary.Keys order is undefined.
            // So we simply check that both "Alpha" and "Beta" exist in the array.
            Assert.Contains("Alpha", instancesAsArray);
            Assert.Contains("Beta", instancesAsArray);
        }

        /// <summary>
        /// Tests that SetStrings sets the Instances property and disables deduplication.
        /// </summary>
        [Fact]
        public void SetStrings_ValidInput_SetsInstancesAndDisablesDeduplication()
        {
            // Arrange
            var newStrings = new List<string> { "One", "Two", "Three" };

            // Act
            _stringCache.SetStrings(newStrings);

            // Assert
            Assert.True(_stringCache.DisableDeduplication);
            Assert.Equal(newStrings, _stringCache.Instances);
        }

        /// <summary>
        /// Tests the Contains method by checking the status of a string before and after interning.
        /// </summary>
        [Fact]
        public void Contains_BeforeAndAfterInterning_ReturnsExpectedResults()
        {
            // Arrange
            string testString = "Test";

            // Act & Assert
            Assert.False(_stringCache.Contains(testString));
            _stringCache.Intern(testString);
            Assert.True(_stringCache.Contains(testString));
        }

        /// <summary>
        /// Tests that InternStringDictionary returns null when provided a null dictionary.
        /// </summary>
        [Fact]
        public void InternStringDictionary_InputNull_ReturnsNull()
        {
            // Act
            var result = _stringCache.InternStringDictionary(null);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that InternStringDictionary returns the same dictionary when the input dictionary is empty.
        /// </summary>
        [Fact]
        public void InternStringDictionary_EmptyDictionary_ReturnsSameInstance()
        {
            // Arrange
            var emptyDictionary = new Dictionary<string, string>();

            // Act
            var result = _stringCache.InternStringDictionary(emptyDictionary);

            // Assert
            Assert.Same(emptyDictionary, result);
        }

        /// <summary>
        /// Tests that InternStringDictionary interns both keys and values for a non-empty dictionary.
        /// </summary>
        [Fact]
        public void InternStringDictionary_ValidDictionary_InternsKeysAndValues()
        {
            // Arrange
            var inputDictionary = new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" },
                { "Key1", "Value1" } // duplicate key will not be allowed in Dictionary, so simulate with same content keys
            };

            // Act
            var result = _stringCache.InternStringDictionary(inputDictionary);

            // Assert
            Assert.NotSame(inputDictionary, result);
            foreach (var kvp in result)
            {
                // Calling Intern on keys and values should return the same instance as stored in the result.
                string internedKey = _stringCache.Intern(kvp.Key);
                string internedValue = _stringCache.Intern(kvp.Value);
                Assert.Same(internedKey, kvp.Key);
                Assert.Same(internedValue, kvp.Value);
            }
        }

        /// <summary>
        /// Tests that InternList returns null when provided a null list.
        /// </summary>
        [Fact]
        public void InternList_InputNull_ReturnsNull()
        {
            // Act
            var result = _stringCache.InternList(null);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that InternList returns the same list when the input list is empty.
        /// </summary>
        [Fact]
        public void InternList_EmptyList_ReturnsSameInstance()
        {
            // Arrange
            var emptyList = new List<string>();

            // Act
            var result = _stringCache.InternList(emptyList);

            // Assert
            Assert.Same(emptyList, result);
        }

        /// <summary>
        /// Tests that InternList interns each element in a non-empty list.
        /// </summary>
        [Fact]
        public void InternList_ValidList_InternsElements()
        {
            // Arrange
            var inputList = new List<string> { "Alpha", "Beta", "Alpha" };

            // Act
            var result = _stringCache.InternList(inputList);

            // Assert
            Assert.NotSame(inputList, result);
            // The interned "Alpha" should be the same instance in both occurrences.
            var occurrences = result.Where(s => s == _stringCache.Intern("Alpha")).ToList();
            Assert.True(occurrences.Count >= 2);
            // Additionally, check that all values have been interned by verifying reference equality.
            for (int i = 0; i < result.Count; i++)
            {
                string interned = _stringCache.Intern(result[i]);
                Assert.Same(interned, result[i]);
            }
        }

        /// <summary>
        /// Tests that InternStringDictionary and InternList return the input as-is when DisableDeduplication is true.
        /// </summary>
        [Fact]
        public void InternMethods_WhenDisableDeduplicationTrue_ReturnInputUnaltered()
        {
            // Arrange
            _stringCache.DisableDeduplication = true;

            var dict = new Dictionary<string, string>
            {
                { "Key", "Value" }
            };
            var list = new List<string> { "Test" };

            // Act
            var dictResult = _stringCache.InternStringDictionary(dict);
            var listResult = _stringCache.InternList(list);
            var internedString = _stringCache.Intern("Test");

            // Assert
            Assert.Same(dict, dictResult);
            Assert.Same(list, listResult);
            Assert.Equal("Test", internedString);
        }
    }
}
