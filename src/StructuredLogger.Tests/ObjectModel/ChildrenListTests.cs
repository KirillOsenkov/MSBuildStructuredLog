using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// A minimal implementation of BaseNode for testing purposes.
    /// </summary>
    public class BaseNode
    {
        public string Title { get; set; }
    }

    /// <summary>
    /// A simple test node derived from BaseNode for unit tests.
    /// </summary>
    public class TestNode : BaseNode
    {
        public TestNode(string title)
        {
            Title = title;
        }
    }

    /// <summary>
    /// Unit tests for the <see cref="ChildrenList"/> class.
    /// </summary>
    public class ChildrenListTests
    {
        private readonly ChildrenList _childrenList;

        public ChildrenListTests()
        {
            // Initialize an empty ChildrenList for testing.
            _childrenList = new ChildrenList();
        }

        /// <summary>
        /// Tests that RaiseCollectionChanged does not throw an exception when there are no subscribers.
        /// </summary>
//         [Fact] [Error] (49-36)CS0117 'Record' does not contain a definition for 'Exception'
//         public void RaiseCollectionChanged_NoSubscribers_DoesNotThrow()
//         {
//             // Act and Assert: Should not throw any exception.
//             var exception = Record.Exception(() => _childrenList.RaiseCollectionChanged());
//             Assert.Null(exception);
//         }

        /// <summary>
        /// Tests that RaiseCollectionChanged invokes the subscribed event with the correct arguments.
        /// </summary>
        [Fact]
        public void RaiseCollectionChanged_WithSubscriber_InvokesEvent()
        {
            // Arrange
            bool eventInvoked = false;
            NotifyCollectionChangedEventArgs receivedArgs = null;
            object receivedSender = null;

            _childrenList.CollectionChanged += (sender, args) =>
            {
                eventInvoked = true;
                receivedSender = sender;
                receivedArgs = args;
            };

            // Act
            _childrenList.RaiseCollectionChanged();

            // Assert
            Assert.True(eventInvoked);
            Assert.Equal(_childrenList, receivedSender);
            Assert.NotNull(receivedArgs);
            Assert.Equal(NotifyCollectionChangedAction.Reset, receivedArgs.Action);
        }

        /// <summary>
        /// Tests that FindNode returns the node when a matching node exists in the list.
        /// </summary>
//         [Fact] [Error] (90-41)CS1503 Argument 1: cannot convert from 'System.Collections.Generic.List<Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode>' to 'int' [Error] (93-31)CS0311 The type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TestNode' cannot be used as type parameter 'T' in the generic type or method 'ChildrenList.FindNode<T>(string)'. There is no implicit reference conversion from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TestNode' to 'Microsoft.Build.Logging.StructuredLogger.BaseNode'.
//         public void FindNode_NodeExists_ReturnsMatchingNode()
//         {
//             // Arrange
//             var testNode1 = new TestNode("Node1");
//             var testNode2 = new TestNode("Node2");
//             var list = new ChildrenList(new List<BaseNode> { testNode1, testNode2 });
// 
//             // Act
//             var result = list.FindNode<TestNode>("Node2");
// 
//             // Assert
//             Assert.NotNull(result);
//             Assert.Equal("Node2", result.Title);
//         }

        /// <summary>
        /// Tests that FindNode returns null when no node with the specified name exists.
        /// </summary>
//         [Fact] [Error] (108-41)CS1503 Argument 1: cannot convert from 'System.Collections.Generic.List<Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode>' to 'int' [Error] (111-31)CS0311 The type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TestNode' cannot be used as type parameter 'T' in the generic type or method 'ChildrenList.FindNode<T>(string)'. There is no implicit reference conversion from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TestNode' to 'Microsoft.Build.Logging.StructuredLogger.BaseNode'.
//         public void FindNode_NodeDoesNotExist_ReturnsNull()
//         {
//             // Arrange
//             var testNode = new TestNode("ExistingNode");
//             var list = new ChildrenList(new List<BaseNode> { testNode });
// 
//             // Act
//             var result = list.FindNode<TestNode>("NonExistentNode");
// 
//             // Assert
//             Assert.Null(result);
//         }

#if !NET8_0_OR_GREATER
        /// <summary>
        /// Tests that EnsureCapacity properly sets the Capacity property.
        /// </summary>
        [Fact]
        public void EnsureCapacity_ValidCapacity_SetsCapacity()
        {
            // Arrange
            var list = new ChildrenList(0);
            int newCapacity = 100;

            // Act
            list.EnsureCapacity(newCapacity);

            // Assert
            Assert.True(list.Capacity >= newCapacity);
        }
#endif
    }

    /// <summary>
    /// Unit tests for the <see cref="CacheByNameChildrenList"/> class.
    /// </summary>
    public class CacheByNameChildrenListTests
    {
        /// <summary>
        /// Tests that FindNode returns the matching node when it exists and caches the result.
        /// The test verifies that subsequent calls with the same type and name return the same instance.
        /// Also tests that the name matching is case-insensitive.
        /// </summary>
//         [Fact] [Error] (152-57)CS1503 Argument 1: cannot convert from 'System.Collections.Generic.List<Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode>' to 'int' [Error] (155-39)CS0311 The type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TestNode' cannot be used as type parameter 'T' in the generic type or method 'CacheByNameChildrenList.FindNode<T>(string)'. There is no implicit reference conversion from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TestNode' to 'Microsoft.Build.Logging.StructuredLogger.BaseNode'. [Error] (156-40)CS0311 The type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TestNode' cannot be used as type parameter 'T' in the generic type or method 'CacheByNameChildrenList.FindNode<T>(string)'. There is no implicit reference conversion from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TestNode' to 'Microsoft.Build.Logging.StructuredLogger.BaseNode'.
//         public void FindNode_NodeExists_ReturnsCachedMatchingNode()
//         {
//             // Arrange
//             var testNode = new TestNode("CachedNode");
//             var cacheList = new CacheByNameChildrenList(new List<BaseNode> { testNode });
// 
//             // Act
//             var firstCall = cacheList.FindNode<TestNode>("cachednode");
//             var secondCall = cacheList.FindNode<TestNode>("CACHEDNODE");
// 
//             // Assert
//             Assert.NotNull(firstCall);
//             Assert.Same(firstCall, secondCall);
//             Assert.Equal("CachedNode", firstCall.Title);
//         }

        /// <summary>
        /// Tests that FindNode returns null when no matching node is found and ensures that the cache is not populated.
        /// </summary>
//         [Fact] [Error] (172-57)CS1503 Argument 1: cannot convert from 'System.Collections.Generic.List<Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode>' to 'int' [Error] (175-36)CS0311 The type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TestNode' cannot be used as type parameter 'T' in the generic type or method 'CacheByNameChildrenList.FindNode<T>(string)'. There is no implicit reference conversion from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TestNode' to 'Microsoft.Build.Logging.StructuredLogger.BaseNode'.
//         public void FindNode_NodeDoesNotExist_ReturnsNull()
//         {
//             // Arrange
//             var testNode = new TestNode("ExistingNode");
//             var cacheList = new CacheByNameChildrenList(new List<BaseNode> { testNode });
// 
//             // Act
//             var result = cacheList.FindNode<TestNode>("NonExistentNode");
// 
//             // Assert
//             Assert.Null(result);
//         }

#if !NET8_0_OR_GREATER
        /// <summary>
        /// Tests that EnsureCapacity properly sets the Capacity property for CacheByNameChildrenList.
        /// </summary>
        [Fact]
        public void EnsureCapacity_ValidCapacity_SetsCapacity()
        {
            // Arrange
            var cacheList = new CacheByNameChildrenList(0);
            int newCapacity = 50;

            // Act
            cacheList.EnsureCapacity(newCapacity);

            // Assert
            Assert.True(cacheList.Capacity >= newCapacity);
        }
#endif
    }
}
