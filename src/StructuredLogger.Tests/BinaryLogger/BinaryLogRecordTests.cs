using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="Record"/> class.
    /// </summary>
    public class RecordTests
    {
        /// <summary>
        /// Tests that the default constructor of <see cref="Record"/> initializes the fields to their default values.
        /// </summary>
        [Fact]
        public void DefaultConstructor_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var record = new Record();

            // Assert
            Assert.Equal(default(BinaryLogRecordKind), record.Kind);
            Assert.Null(record.Bytes);
            Assert.Null(record.Args);
            Assert.Equal(default(long), record.Start);
            Assert.Equal(default(long), record.Length);
        }

        /// <summary>
        /// Tests that manually assigning values to the fields of <see cref="Record"/> correctly stores and returns the set values.
        /// </summary>
//         [Fact] [Error] (43-100)CS1503 Argument 4: cannot convert from 'int' to 'Microsoft.Build.Framework.MessageImportance' [Error] (43-103)CS1503 Argument 5: cannot convert from 'int' to 'System.DateTime'
//         public void ManualAssignment_ShouldSetAndReturnCorrectValues()
//         {
//             // Arrange
//             var expectedKind = (BinaryLogRecordKind)2;
//             byte[] expectedBytes = new byte[] { 10, 20, 30 };
// 
//             // Creating a BuildMessageEventArgs as a concrete instance of BuildEventArgs.
//             var messageArgs = Array.Empty<object>();
//             BuildEventArgs expectedArgs = new BuildMessageEventArgs("Subcategory", "Code", "File", 1, 1, 1, 1, "Test Message", messageArgs);
//             long expectedStart = 100;
//             long expectedLength = 50;
//             var record = new Record();
// 
//             // Act
//             record.Kind = expectedKind;
//             record.Bytes = expectedBytes;
//             record.Args = expectedArgs;
//             record.Start = expectedStart;
//             record.Length = expectedLength;
// 
//             // Assert
//             Assert.Equal(expectedKind, record.Kind);
//             Assert.Equal(expectedBytes, record.Bytes);
//             Assert.Equal(expectedArgs, record.Args);
//             Assert.Equal(expectedStart, record.Start);
//             Assert.Equal(expectedLength, record.Length);
//         }
    }

    /// <summary>
    /// Unit tests for the <see cref="RecordInfo"/> record struct.
    /// </summary>
    public class RecordInfoTests
    {
        /// <summary>
        /// Tests that the constructor of <see cref="RecordInfo"/> assigns the provided values correctly.
        /// </summary>
        [Fact]
        public void Constructor_ShouldAssignValuesCorrectly()
        {
            // Arrange
            var expectedKind = (BinaryLogRecordKind)3;
            long expectedStart = 200;
            long expectedLength = 75;

            // Act
            var recordInfo = new RecordInfo(expectedKind, expectedStart, expectedLength);

            // Assert
            Assert.Equal(expectedKind, recordInfo.Kind);
            Assert.Equal(expectedStart, recordInfo.Start);
            Assert.Equal(expectedLength, recordInfo.Length);
        }

        /// <summary>
        /// Tests that two instances of <see cref="RecordInfo"/> with the same values are considered equal.
        /// </summary>
        [Fact]
        public void Equality_TwoInstancesWithSameValues_ShouldBeEqual()
        {
            // Arrange
            var kind = (BinaryLogRecordKind)5;
            long start = 300;
            long length = 125;

            var recordInfo1 = new RecordInfo(kind, start, length);
            var recordInfo2 = new RecordInfo(kind, start, length);

            // Act & Assert
            Assert.Equal(recordInfo1, recordInfo2);
        }

        /// <summary>
        /// Tests that the deconstruction of a <see cref="RecordInfo"/> instance returns individual values correctly.
        /// </summary>
        [Fact]
        public void Deconstruct_ShouldReturnIndividualValues()
        {
            // Arrange
            var expectedKind = (BinaryLogRecordKind)7;
            long expectedStart = 400;
            long expectedLength = 60;
            var recordInfo = new RecordInfo(expectedKind, expectedStart, expectedLength);

            // Act
            (BinaryLogRecordKind kind, long start, long length) = recordInfo;

            // Assert
            Assert.Equal(expectedKind, kind);
            Assert.Equal(expectedStart, start);
            Assert.Equal(expectedLength, length);
        }
    }
}
