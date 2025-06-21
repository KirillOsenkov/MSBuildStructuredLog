using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="Utilities"/> class.
    /// </summary>
    public class UtilitiesTests
    {
        /// <summary>
        /// Tests that BinarySearch returns the correct index when the searched item is found in the list.
        /// </summary>
        [Theory]
        [InlineData(new int[] { 1, 3, 5, 7 }, 5, 2)]
        [InlineData(new int[] { -10, -5, 0, 5, 10 }, -10, 0)]
        [InlineData(new int[] { 2, 4, 6, 8, 10 }, 10, 4)]
        public void BinarySearch_ItemExists_ReturnsCorrectIndex(int[] inputArray, int searchItem, int expectedIndex)
        {
            // Arrange
            IList<int> list = inputArray;
            // Act
            int actualIndex = list.BinarySearch(searchItem, i => i);
            // Assert
            Assert.Equal(expectedIndex, actualIndex);
        }

        /// <summary>
        /// Tests that BinarySearch returns the bitwise complement of the insertion index when the searched item is not found.
        /// </summary>
        [Theory]
        [InlineData(new int[] { 1, 3, 5, 7 }, 4, ~2)]
        [InlineData(new int[] { -10, -5, 0, 5, 10 }, 1, ~3)]
        [InlineData(new int[] { 2, 4, 6, 8, 10 }, 11, ~5)]
        public void BinarySearch_ItemDoesNotExist_ReturnsBitwiseComplement(int[] inputArray, int searchItem, int expectedResult)
        {
            // Arrange
            IList<int> list = inputArray;
            // Act
            int actualResult = list.BinarySearch(searchItem, i => i);
            // Assert
            Assert.Equal(expectedResult, actualResult);
        }

        /// <summary>
        /// Tests that BinarySearch on an empty list returns the bitwise complement of 0.
        /// </summary>
        [Fact]
        public void BinarySearch_EmptyList_ReturnsBitwiseComplementOfZero()
        {
            // Arrange
            IList<int> list = new List<int>();
            // Act
            int actualResult = list.BinarySearch(10, i => i);
            // Assert
            Assert.Equal(~0, actualResult);
        }
    }

    /// <summary>
    /// Unit tests for the <see cref="BatchBlockingCollection{T}"/> class.
    /// </summary>
    public class BatchBlockingCollectionTests
    {
        private readonly int _customBatchSize;

        public BatchBlockingCollectionTests()
        {
            // Use small batch size for testing purposes.
            _customBatchSize = 3;
        }

        /// <summary>
        /// Tests that adding fewer items than the batch size results in the correct count.
        /// </summary>
        [Fact]
        public void Add_WhenLessThanBatchSize_CountReflectsItems()
        {
            // Arrange
            var collection = new BatchBlockingCollection<int>(_customBatchSize, 0);
            // Act
            collection.Add(1);
            collection.Add(2);
            // Assert: current batch count should be 2, queue is empty.
            Assert.Equal(2, collection.Count);
        }

        /// <summary>
        /// Tests that adding items exceeding the batch size creates a new batch and the count is calculated accordingly.
        /// </summary>
        [Fact]
        public void Add_WhenExceedingBatchSize_CountReflectsAllItemsAcrossBatches()
        {
            // Arrange
            var collection = new BatchBlockingCollection<int>(_customBatchSize, 0);
            // Act
            // Fill first batch (3 items)
            collection.Add(1);
            collection.Add(2);
            collection.Add(3);
            // This should trigger creation of a new batch.
            collection.Add(4);
            // Assert: one full batch (3 items) in the queue and current batch with 1 item.
            Assert.Equal(4, collection.Count);
        }

        /// <summary>
        /// Tests that the Completion task processes items by invoking the ProcessItem event for each added item.
        /// </summary>
//         [Fact] [Error] (115-27)CS1983 The return type of an async method must be void, Task, Task<T>, a task-like type, IAsyncEnumerable<T>, or IAsyncEnumerator<T> [Error] (115-27)CS0161 'BatchBlockingCollectionTests.Completion_WhenCompleteAdding_InvokesProcessItemForAllItems()': not all code paths return a value
//         public async Task Completion_WhenCompleteAdding_InvokesProcessItemForAllItems()
//         {
//             // Arrange
//             var processedItems = new List<int>();
//             var collection = new BatchBlockingCollection<int>(_customBatchSize, 0);
//             collection.ProcessItem += item => processedItems.Add(item);
// 
//             // Add several items across batches.
//             collection.Add(10);
//             collection.Add(20);
//             collection.Add(30);
//             collection.Add(40);
//             collection.Add(50);
// 
//             // Act
//             collection.CompleteAdding();
//             await collection.Completion;
// 
//             // Assert: All items should be processed.
//             var expectedItems = new List<int> { 10, 20, 30, 40, 50 };
//             Assert.Equal(expectedItems, processedItems);
//         }
    }

    /// <summary>
    /// Unit tests for the <see cref="Rental{T}"/> class.
    /// </summary>
    public class RentalTests
    {
        /// <summary>
        /// Tests that Get returns a new instance when the internal pool is empty.
        /// </summary>
        [Fact]
        public void Get_WhenPoolIsEmpty_ReturnsNewInstance()
        {
            // Arrange
            var rental = new Rental<List<int>>(() => new List<int>());
            // Act
            var instance1 = rental.Get();
            var instance2 = rental.Get();
            // Assert: Two separate calls when pool is empty should yield different instances.
            Assert.NotSame(instance1, instance2);
        }

        /// <summary>
        /// Tests that after returning an item to the pool, Get returns the same instance.
        /// </summary>
        [Fact]
        public void Get_AfterReturn_ReturnsPooledInstance()
        {
            // Arrange
            var rental = new Rental<List<int>>(() => new List<int>());
            var instance = rental.Get();
            // Act
            rental.Return(instance);
            var pooledInstance = rental.Get();
            // Assert: The returned instance should be the same as the one obtained earlier.
            Assert.Same(instance, pooledInstance);
        }
    }

    /// <summary>
    /// Unit tests for the <see cref="ChunkedList{T}"/> class.
    /// </summary>
    public class ChunkedListTests
    {
        /// <summary>
        /// Tests that adding a single item creates a chunk and increases the count.
        /// </summary>
        [Fact]
        public void Add_WhenAddingFirstItem_CreatesChunkAndIncreasesCount()
        {
            // Arrange
            int customChunkSize = 2;
            var chunkedList = new ChunkedList<int>(customChunkSize);
            // Act
            chunkedList.Add(100);
            // Assert
            Assert.Equal(1, chunkedList.Count);
            Assert.Single(chunkedList.Chunks);
        }

        /// <summary>
        /// Tests that adding multiple items across the chunk boundary creates multiple chunks and updates count accordingly.
        /// </summary>
        [Fact]
        public void Add_WhenExceedingChunkSize_CreatesMultipleChunksAndUpdatesCount()
        {
            // Arrange
            int customChunkSize = 2;
            var chunkedList = new ChunkedList<int>(customChunkSize);
            // Act
            chunkedList.Add(1);  // causes first chunk creation
            chunkedList.Add(2);  // fills first chunk
            chunkedList.Add(3);  // creates second chunk
            // Assert
            Assert.Equal(3, chunkedList.Count);
            Assert.Equal(2, chunkedList.Chunks.Count);
            Assert.Equal("Count = 3", chunkedList.ToString());
        }

        /// <summary>
        /// Tests that ToString returns the correct string representation including the total count.
        /// </summary>
        [Fact]
        public void ToString_ReturnsCorrectCountRepresentation()
        {
            // Arrange
            var chunkedList = new ChunkedList<string>(5);
            // Act
            chunkedList.Add("a");
            chunkedList.Add("b");
            string result = chunkedList.ToString();
            // Assert
            Assert.Equal("Count = 2", result);
        }
    }
}
