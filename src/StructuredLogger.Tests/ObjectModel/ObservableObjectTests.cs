using System;
using System.ComponentModel;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// A test subclass of ObservableObject to expose protected members for testing.
    /// </summary>
    public class TestObservableObject : ObservableObject
    {
        private int _intValue;
        /// <summary>
        /// Gets the current integer value.
        /// </summary>
        public int IntValue => _intValue;

        private string _stringValue;
        /// <summary>
        /// Gets the current string value.
        /// </summary>
        public string StringValue => _stringValue;

        /// <summary>
        /// Exposes the protected SetField method for an integer field.
        /// </summary>
        /// <param name="newValue">The new value to set.</param>
        /// <param name="propertyName">The name of the property to pass to the event.</param>
        /// <returns>True if the field was updated; false otherwise.</returns>
        public bool TestSetFieldInt(int newValue, string propertyName = "IntValue")
        {
            return SetField(ref _intValue, newValue, propertyName);
        }

        /// <summary>
        /// Exposes the protected SetField method for a string field.
        /// </summary>
        /// <param name="newValue">The new value to set.</param>
        /// <param name="propertyName">The name of the property to pass to the event.</param>
        /// <returns>True if the field was updated; false otherwise.</returns>
        public bool TestSetFieldString(string newValue, string propertyName = "StringValue")
        {
            return SetField(ref _stringValue, newValue, propertyName);
        }

        /// <summary>
        /// Exposes the protected RaisePropertyChanged method.
        /// </summary>
        /// <param name="propertyName">The property name to raise change notification for.</param>
        public void TestRaisePropertyChanged(string propertyName = "TestProperty")
        {
            RaisePropertyChanged(propertyName);
        }
    }

    /// <summary>
    /// Unit tests for the ObservableObject class.
    /// </summary>
    public class ObservableObjectTests
    {
        private readonly TestObservableObject _testObservableObject;

        /// <summary>
        /// Initializes a new instance of the ObservableObjectTests class.
        /// </summary>
        public ObservableObjectTests()
        {
            _testObservableObject = new TestObservableObject();
        }

        /// <summary>
        /// Tests that TestSetFieldInt returns true and fires the PropertyChanged event when the field is updated with a new value.
        /// </summary>
        [Fact]
        public void TestSetFieldInt_DifferentValue_FiresPropertyChangedEvent()
        {
            // Arrange
            int eventCallCount = 0;
            string receivedPropertyName = null;
            _testObservableObject.TestSetFieldInt(5, "MyIntProperty"); // initial set to 5
            _testObservableObject.PropertyChanged += (sender, args) =>
            {
                eventCallCount++;
                receivedPropertyName = args.PropertyName;
            };

            // Act
            bool result = _testObservableObject.TestSetFieldInt(10, "MyIntProperty");

            // Assert
            Assert.True(result);
            Assert.Equal(10, _testObservableObject.IntValue);
            Assert.Equal(1, eventCallCount);
            Assert.Equal("MyIntProperty", receivedPropertyName);
        }

        /// <summary>
        /// Tests that TestSetFieldInt returns false and does not fire the PropertyChanged event when setting the same value.
        /// </summary>
        [Fact]
        public void TestSetFieldInt_SameValue_NoEventRaised()
        {
            // Arrange
            int eventCallCount = 0;
            _testObservableObject.TestSetFieldInt(20, "IntValue");
            _testObservableObject.PropertyChanged += (sender, args) => eventCallCount++;

            // Act
            bool result = _testObservableObject.TestSetFieldInt(20, "IntValue");

            // Assert
            Assert.False(result);
            Assert.Equal(20, _testObservableObject.IntValue);
            Assert.Equal(0, eventCallCount);
        }

        /// <summary>
        /// Tests that TestSetFieldString returns true and fires the PropertyChanged event when the string field is updated from null to a new value.
        /// </summary>
        [Fact]
        public void TestSetFieldString_NullToNonNull_FiresPropertyChangedEvent()
        {
            // Arrange
            int eventCallCount = 0;
            string receivedPropertyName = null;
            _testObservableObject.PropertyChanged += (sender, args) =>
            {
                eventCallCount++;
                receivedPropertyName = args.PropertyName;
            };

            // Act
            bool result = _testObservableObject.TestSetFieldString("Hello", "Greeting");

            // Assert
            Assert.True(result);
            Assert.Equal("Hello", _testObservableObject.StringValue);
            Assert.Equal(1, eventCallCount);
            Assert.Equal("Greeting", receivedPropertyName);
        }

        /// <summary>
        /// Tests that TestSetFieldString returns false and does not fire the PropertyChanged event when setting the same (non-null) string value.
        /// </summary>
        [Fact]
        public void TestSetFieldString_SameNonNullValue_NoEventRaised()
        {
            // Arrange
            int eventCallCount = 0;
            _testObservableObject.TestSetFieldString("World", "StringValue");
            _testObservableObject.PropertyChanged += (sender, args) => eventCallCount++;

            // Act
            bool result = _testObservableObject.TestSetFieldString("World", "StringValue");

            // Assert
            Assert.False(result);
            Assert.Equal("World", _testObservableObject.StringValue);
            Assert.Equal(0, eventCallCount);
        }

        /// <summary>
        /// Tests that TestSetFieldString returns true and fires the PropertyChanged event when updating a non-null string value to null.
        /// </summary>
        [Fact]
        public void TestSetFieldString_NonNullToNull_FiresPropertyChangedEvent()
        {
            // Arrange
            int eventCallCount = 0;
            _testObservableObject.TestSetFieldString("NotNull", "StringValue");
            _testObservableObject.PropertyChanged += (sender, args) => eventCallCount++;

            // Act
            bool result = _testObservableObject.TestSetFieldString(null, "StringValue");

            // Assert
            Assert.True(result);
            Assert.Null(_testObservableObject.StringValue);
            Assert.Equal(1, eventCallCount);
        }

        /// <summary>
        /// Tests that TestRaisePropertyChanged fires the PropertyChanged event with the correct property name.
        /// </summary>
        [Fact]
        public void TestRaisePropertyChanged_ValidPropertyName_FiresEvent()
        {
            // Arrange
            int eventCallCount = 0;
            string receivedPropertyName = null;
            _testObservableObject.PropertyChanged += (sender, args) =>
            {
                eventCallCount++;
                receivedPropertyName = args.PropertyName;
            };

            // Act
            _testObservableObject.TestRaisePropertyChanged("CustomProperty");

            // Assert
            Assert.Equal(1, eventCallCount);
            Assert.Equal("CustomProperty", receivedPropertyName);
        }
    }
}
