using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ExtendedCustomBuildEventArgs"/> class.
    /// </summary>
    public class ExtendedCustomBuildEventArgsTests
    {
        /// <summary>
        /// Tests that the internal default constructor sets ExtendedType to "undefined".
        /// </summary>
        [Fact]
        public void DefaultConstructor_Internal_SetsExtendedTypeToUndefined()
        {
            // Arrange & Act
            var eventArgs = new ExtendedCustomBuildEventArgs();

            // Assert
            Assert.Equal("undefined", eventArgs.ExtendedType);
        }

        /// <summary>
        /// Tests that the constructor with a type parameter sets the ExtendedType property correctly.
        /// </summary>
        [Theory]
        [InlineData("CustomType1")]
        [InlineData("")]
        [InlineData(null)]
        public void Constructor_WithType_SetsExtendedTypeCorrectly(string type)
        {
            // Arrange & Act
            var eventArgs = new ExtendedCustomBuildEventArgs(type);

            // Assert
            Assert.Equal(type, eventArgs.ExtendedType);
        }

        /// <summary>
        /// Tests that the constructor with type, message, helpKeyword, and senderName sets properties correctly.
        /// Assumes that base properties Message, HelpKeyword and SenderName are accessible.
        /// </summary>
        [Theory]
        [InlineData("Type", "Message", "Help", "Sender")]
        [InlineData("Type", null, null, null)]
        public void Constructor_WithTypeMessageHelpSender_SetsPropertiesCorrectly(string type, string message, string helpKeyword, string senderName)
        {
            // Arrange & Act
            var eventArgs = new ExtendedCustomBuildEventArgs(type, message, helpKeyword, senderName);

            // Assert
            Assert.Equal(type, eventArgs.ExtendedType);
            Assert.Equal(message, eventArgs.Message);
            Assert.Equal(helpKeyword, eventArgs.HelpKeyword);
            Assert.Equal(senderName, eventArgs.SenderName);
        }

        /// <summary>
        /// Tests that the constructor with type, message, helpKeyword, senderName, and eventTimestamp sets properties correctly.
        /// Assumes that base property Timestamp is accessible.
        /// </summary>
        [Theory]
        [InlineData("Type", "Message", "Help", "Sender", "2020-01-01T12:00:00")]
        [InlineData("Type", null, null, null, "1999-12-31T23:59:59")]
        public void Constructor_WithTimestamp_SetsPropertiesCorrectly(string type, string message, string helpKeyword, string senderName, string timestampString)
        {
            // Arrange
            DateTime timestamp = DateTime.Parse(timestampString);

            // Act
            var eventArgs = new ExtendedCustomBuildEventArgs(type, message, helpKeyword, senderName, timestamp);

            // Assert
            Assert.Equal(type, eventArgs.ExtendedType);
            Assert.Equal(message, eventArgs.Message);
            Assert.Equal(helpKeyword, eventArgs.HelpKeyword);
            Assert.Equal(senderName, eventArgs.SenderName);
            Assert.Equal(timestamp, eventArgs.Timestamp);
        }

        /// <summary>
        /// Tests that the constructor with type, message, helpKeyword, senderName, eventTimestamp, 
        /// and messageArgs sets properties correctly.
        /// Assumes that base properties Timestamp and MessageArgs are accessible.
        /// </summary>
//         [Fact] [Error] (109-49)CS1061 'ExtendedCustomBuildEventArgs' does not contain a definition for 'MessageArgs' and no accessible extension method 'MessageArgs' accepting a first argument of type 'ExtendedCustomBuildEventArgs' could be found (are you missing a using directive or an assembly reference?)
//         public void Constructor_WithTimestampAndMessageArgs_SetsPropertiesCorrectly()
//         {
//             // Arrange
//             string type = "Type";
//             string message = "Message";
//             string helpKeyword = "Help";
//             string senderName = "Sender";
//             DateTime timestamp = DateTime.Now;
//             object[] messageArgs = new object[] { "Argument1", 42, null };
// 
//             // Act
//             var eventArgs = new ExtendedCustomBuildEventArgs(type, message, helpKeyword, senderName, timestamp, messageArgs);
// 
//             // Assert
//             Assert.Equal(type, eventArgs.ExtendedType);
//             Assert.Equal(message, eventArgs.Message);
//             Assert.Equal(helpKeyword, eventArgs.HelpKeyword);
//             Assert.Equal(senderName, eventArgs.SenderName);
//             Assert.Equal(timestamp, eventArgs.Timestamp);
//             Assert.Equal(messageArgs, eventArgs.MessageArgs);
//         }

        /// <summary>
        /// Tests that the ExtendedMetadata property can be set and retrieved correctly.
        /// </summary>
        [Fact]
        public void ExtendedMetadata_Property_CanBeSetAndRetrieved()
        {
            // Arrange
            var eventArgs = new ExtendedCustomBuildEventArgs("Type");
            var metadata = new Dictionary<string, string?>
            {
                { "Key1", "Value1" },
                { "Key2", null }
            };

            // Act
            eventArgs.ExtendedMetadata = metadata;

            // Assert
            Assert.Equal(metadata, eventArgs.ExtendedMetadata);
        }

        /// <summary>
        /// Tests that the ExtendedData property can be set and retrieved correctly.
        /// </summary>
        [Fact]
        public void ExtendedData_Property_CanBeSetAndRetrieved()
        {
            // Arrange
            var eventArgs = new ExtendedCustomBuildEventArgs("Type");
            string testData = "Sample extended data";

            // Act
            eventArgs.ExtendedData = testData;

            // Assert
            Assert.Equal(testData, eventArgs.ExtendedData);
        }

        /// <summary>
        /// Tests that properties are mutable after object creation.
        /// </summary>
        [Fact]
        public void Properties_Modification_AfterCreation_WorksAsExpected()
        {
            // Arrange
            var eventArgs = new ExtendedCustomBuildEventArgs("InitialType");
            var newMetadata = new Dictionary<string, string?>
            {
                { "NewKey", "NewValue" }
            };

            // Act
            eventArgs.ExtendedType = "UpdatedType";
            eventArgs.ExtendedData = "UpdatedData";
            eventArgs.ExtendedMetadata = newMetadata;

            // Assert
            Assert.Equal("UpdatedType", eventArgs.ExtendedType);
            Assert.Equal("UpdatedData", eventArgs.ExtendedData);
            Assert.Equal(newMetadata, eventArgs.ExtendedMetadata);
        }
    }
}
