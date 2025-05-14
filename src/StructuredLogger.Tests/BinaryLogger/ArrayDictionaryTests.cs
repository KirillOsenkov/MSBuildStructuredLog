using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Moq;
using Xunit;

namespace Microsoft.Build.Collections.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ArrayDictionary{TKey, TValue}"/> class.
    /// </summary>
    public class ArrayDictionaryTests
    {
        private readonly int _capacity = 5;

        /// <summary>
        /// Tests that the constructor initializes keys and values arrays with the specified capacity.
        /// </summary>
        [Fact]
        public void Constructor_WithValidCapacity_InitializesInternalArrays()
        {
            // Arrange & Act
            var dictionary = new ArrayDictionary<string, int>(_capacity);

            // Assert
            Assert.Equal(0, dictionary.Count);
            Assert.NotNull(dictionary.KeyArray);
            Assert.NotNull(dictionary.ValueArray);
            Assert.Equal(_capacity, dictionary.KeyArray.Length);
            Assert.Equal(_capacity, dictionary.ValueArray.Length);
        }

        /// <summary>
        /// Tests that the static Create method returns a non-null IDictionary instance.
        /// </summary>
        [Fact]
        public void Create_WithValidCapacity_ReturnsNonNullDictionary()
        {
            // Arrange
            int capacity = 3;

            // Act
            IDictionary<string, int> dictionary = ArrayDictionary<string, int>.Create(capacity);

            // Assert
            Assert.NotNull(dictionary);
            Assert.Equal(0, dictionary.Count);
        }

        /// <summary>
        /// Tests that the indexer getter returns the default value if key is not present.
        /// </summary>
        [Fact]
        public void IndexerGet_KeyNotPresent_ReturnsDefaultValue()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            string missingKey = "missing";

            // Act
            int value = dictionary[missingKey];

            // Assert
            Assert.Equal(default(int), value);
        }

        /// <summary>
        /// Tests that the indexer setter adds a new key-value pair when key is not present.
        /// </summary>
        [Fact]
        public void IndexerSet_KeyNotPresent_AddsNewKeyValuePair()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            string key = "key1";
            int expectedValue = 42;

            // Act
            dictionary[key] = expectedValue;

            // Assert
            Assert.True(dictionary.ContainsKey(key));
            Assert.Equal(expectedValue, dictionary[key]);
            Assert.Equal(1, dictionary.Count);
        }

        /// <summary>
        /// Tests that the indexer setter updates an existing key's value.
        /// </summary>
        [Fact]
        public void IndexerSet_KeyPresent_UpdatesExistingValue()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            string key = "key1";
            dictionary.Add(key, 10);
            int newValue = 99;

            // Act
            dictionary[key] = newValue;

            // Assert
            Assert.Equal(newValue, dictionary[key]);
            Assert.Equal(1, dictionary.Count);
        }

        /// <summary>
        /// Tests that the Add method successfully adds a new key-value pair when capacity is not exceeded.
        /// </summary>
        [Fact]
        public void Add_WhenCapacityNotExceeded_AddsItem()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            string key = "key1";
            int value = 1;

            // Act
            dictionary.Add(key, value);

            // Assert
            Assert.True(dictionary.ContainsKey(key));
            Assert.Equal(value, dictionary[key]);
            Assert.Equal(1, dictionary.Count);
        }

        /// <summary>
        /// Tests that the Add method throws an InvalidOperationException when capacity is exceeded.
        /// </summary>
        [Fact]
        public void Add_WhenCapacityExceeded_ThrowsInvalidOperationException()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(2);
            dictionary.Add("key1", 1);
            dictionary.Add("key2", 2);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => dictionary.Add("key3", 3));
            Assert.Contains("ArrayDictionary is at capacity", ex.Message);
        }

        /// <summary>
        /// Tests that the Add(KeyValuePair) method successfully adds a new key-value pair.
        /// </summary>
        [Fact]
        public void AddKeyValuePair_WhenCapacityNotExceeded_AddsItem()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            var kvp = new KeyValuePair<string, int>("key1", 100);

            // Act
            dictionary.Add(kvp);

            // Assert
            Assert.True(dictionary.Contains(kvp));
            Assert.Equal(1, dictionary.Count);
        }

        /// <summary>
        /// Tests that the Clear method throws a NotImplementedException.
        /// </summary>
        [Fact]
        public void Clear_Always_ThrowsNotImplementedException()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => dictionary.Clear());
        }

        /// <summary>
        /// Tests that Contains(KeyValuePair) returns true if the key-value pair is present.
        /// </summary>
        [Fact]
        public void ContainsKeyValuePair_WhenPresent_ReturnsTrue()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            dictionary.Add("key1", 10);
            var kvp = new KeyValuePair<string, int>("key1", 10);

            // Act
            bool contains = dictionary.Contains(kvp);

            // Assert
            Assert.True(contains);
        }

        /// <summary>
        /// Tests that Contains(KeyValuePair) returns false if the key-value pair is not present.
        /// </summary>
        [Fact]
        public void ContainsKeyValuePair_WhenNotPresent_ReturnsFalse()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            dictionary.Add("key1", 10);
            var kvp = new KeyValuePair<string, int>("key1", 20);

            // Act
            bool contains = dictionary.Contains(kvp);

            // Assert
            Assert.False(contains);
        }

        /// <summary>
        /// Tests that ContainsKey returns true for an existing key.
        /// </summary>
        [Fact]
        public void ContainsKey_WhenKeyExists_ReturnsTrue()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            string key = "key1";
            dictionary.Add(key, 5);

            // Act
            bool exists = dictionary.ContainsKey(key);

            // Assert
            Assert.True(exists);
        }

        /// <summary>
        /// Tests that ContainsKey returns false for a non-existing key.
        /// </summary>
        [Fact]
        public void ContainsKey_WhenKeyDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);

            // Act
            bool exists = dictionary.ContainsKey("nonexistent");

            // Assert
            Assert.False(exists);
        }

        /// <summary>
        /// Tests that CopyTo copies the key-value pairs into an array at the specified index.
        /// </summary>
        [Fact]
        public void CopyTo_WithSufficientArraySize_CopiesAllItems()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            dictionary.Add("a", 1);
            dictionary.Add("b", 2);
            var targetArray = new KeyValuePair<string, int>[5];
            int startIndex = 2;

            // Act
            dictionary.CopyTo(targetArray, startIndex);

            // Assert
            Assert.Equal(new KeyValuePair<string, int>("a", 1), targetArray[startIndex]);
            Assert.Equal(new KeyValuePair<string, int>("b", 2), targetArray[startIndex + 1]);
        }

        /// <summary>
        /// Tests that ICollection.CopyTo throws a NotImplementedException.
        /// </summary>
        [Fact]
        public void ICollectionCopyTo_Always_ThrowsNotImplementedException()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            ((IDictionary)dictionary).Add("key1", 1); // Using IDictionary.Add via indexer not supported.
            var array = new object[10];

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => ((ICollection)dictionary).CopyTo(array, 0));
        }

        /// <summary>
        /// Tests that GetEnumerator returns an enumerator that iterates through the collection.
        /// </summary>
        [Fact]
        public void GetEnumerator_WhenCalled_IteratesOverAllItems()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            dictionary.Add("one", 1);
            dictionary.Add("two", 2);
            var items = new List<KeyValuePair<string, int>>();

            // Act
            foreach (var kvp in dictionary)
            {
                items.Add(kvp);
            }

            // Assert
            Assert.Equal(2, items.Count);
            Assert.Contains(new KeyValuePair<string, int>("one", 1), items);
            Assert.Contains(new KeyValuePair<string, int>("two", 2), items);
        }

        /// <summary>
        /// Tests that Remove(TKey) throws a NotImplementedException.
        /// </summary>
        [Fact]
        public void RemoveTKey_Always_ThrowsNotImplementedException()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => dictionary.Remove("key"));
        }

        /// <summary>
        /// Tests that Remove(KeyValuePair) throws a NotImplementedException.
        /// </summary>
        [Fact]
        public void RemoveKeyValuePair_Always_ThrowsNotImplementedException()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            var kvp = new KeyValuePair<string, int>("key", 1);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => dictionary.Remove(kvp));
        }

        /// <summary>
        /// Tests that TryGetValue returns true and sets the out value when the key exists.
        /// </summary>
        [Fact]
        public void TryGetValue_KeyExists_ReturnsTrueAndSetsValue()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            dictionary.Add("existing", 123);

            // Act
            bool found = dictionary.TryGetValue("existing", out int val);

            // Assert
            Assert.True(found);
            Assert.Equal(123, val);
        }

        /// <summary>
        /// Tests that TryGetValue returns false and sets the out value to default when the key does not exist.
        /// </summary>
        [Fact]
        public void TryGetValue_KeyDoesNotExist_ReturnsFalseAndDefaultValue()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);

            // Act
            bool found = dictionary.TryGetValue("nonexistent", out int val);

            // Assert
            Assert.False(found);
            Assert.Equal(default(int), val);
        }

        /// <summary>
        /// Tests that IDictionary.Contains returns true for a valid key and false for an invalid key type.
        /// </summary>
        [Fact]
        public void IDictionaryContains_VariousKeys_ReturnsAppropriateBoolean()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            dictionary.Add("test", 100);
            IDictionary idict = dictionary;

            // Act
            bool containsValid = idict.Contains("test");
            bool containsInvalid = idict.Contains(123); // invalid type

            // Assert
            Assert.True(containsValid);
            Assert.False(containsInvalid);
        }

        /// <summary>
        /// Tests that IDictionary.Add always throws a NotSupportedException.
        /// </summary>
        [Fact]
        public void IDictionaryAdd_Always_ThrowsNotSupportedException()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            IDictionary idict = dictionary;

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => idict.Add("key", 1));
        }

        /// <summary>
        /// Tests that IDictionary.Remove always throws a NotImplementedException.
        /// </summary>
        [Fact]
        public void IDictionaryRemove_Always_ThrowsNotImplementedException()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            IDictionary idict = dictionary;

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => idict.Remove("key"));
        }

        /// <summary>
        /// Tests that the Sort method sorts the keys and reorders the corresponding values.
        /// </summary>
        [Fact]
        public void Sort_WhenCalled_SortsKeysAndReordersValues()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            // Insert keys in unsorted order
            dictionary.Add("delta", 4);
            dictionary.Add("alpha", 1);
            dictionary.Add("charlie", 3);
            dictionary.Add("bravo", 2);

            // Act
            dictionary.Sort();

            // Assert
            // Only consider the array portion that has items (Count elements)
            var sortedKeys = new List<string>(dictionary.KeyArray).GetRange(0, dictionary.Count);
            var sortedValues = new List<int>(dictionary.ValueArray).GetRange(0, dictionary.Count);
            var expectedOrder = new List<string> { "alpha", "bravo", "charlie", "delta" };

            Assert.Equal(expectedOrder, sortedKeys);

            // Verify that values are reordered correspondingly. Map expected key to value.
            var expectedMapping = new Dictionary<string, int>
            {
                { "alpha", 1 },
                { "bravo", 2 },
                { "charlie", 3 },
                { "delta", 4 }
            };

            for (int i = 0; i < sortedKeys.Count; i++)
            {
                Assert.Equal(expectedMapping[sortedKeys[i]], sortedValues[i]);
            }
        }

        /// <summary>
        /// Tests that calling Sort a second time does not alter the already sorted data.
        /// </summary>
        [Fact]
        public void Sort_WhenAlreadySorted_NoChangeOccurs()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            dictionary.Add("alpha", 1);
            dictionary.Add("bravo", 2);

            // Act
            dictionary.Sort();
            var sortedKeysFirst = new List<string>(dictionary.KeyArray).GetRange(0, dictionary.Count);
            dictionary.Sort(); // call sort again
            var sortedKeysSecond = new List<string>(dictionary.KeyArray).GetRange(0, dictionary.Count);

            // Assert
            Assert.Equal(sortedKeysFirst, sortedKeysSecond);
        }

        /// <summary>
        /// Tests that the IDictionary indexer getter and setter work correctly via explicit interface implementation.
        /// </summary>
        [Fact]
        public void IDictionaryIndexer_GetAndSet_WorksAsExpected()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            IDictionary idict = dictionary;
            string key = "key";
            int value = 55;

            // Act & Assert
            // Test setter via IDictionary indexer. It should add the item.
            idict[key] = value;
            Assert.Equal(value, idict[key]);

            // Changing the value using indexer.
            int newValue = 77;
            idict[key] = newValue;
            Assert.Equal(newValue, idict[key]);
        }

        /// <summary>
        /// Tests that the IDictionary enumerator returns dictionary entries correctly.
        /// </summary>
        [Fact]
        public void IDictionaryGetEnumerator_WhenCalled_IteratesDictionaryEntries()
        {
            // Arrange
            var dictionary = new ArrayDictionary<string, int>(_capacity);
            dictionary.Add("one", 1);
            dictionary.Add("two", 2);
            IDictionary idict = dictionary;
            var entries = new List<DictionaryEntry>();

            // Act
            IDictionaryEnumerator enumerator = idict.GetEnumerator();
            while (enumerator.MoveNext())
            {
                entries.Add(enumerator.Entry);
            }

            // Assert
            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, entry => (string)entry.Key == "one" && (int)entry.Value == 1);
            Assert.Contains(entries, entry => (string)entry.Key == "two" && (int)entry.Value == 2);
        }
    }
}
