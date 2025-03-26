using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BinlogStats"/> class.
    /// </summary>
    public class BinlogStatsTests
    {
        /// <summary>
        /// Tests the Calculate method with an empty binlog file.
        /// The test creates a temporary empty file, invokes Calculate, and expects the returned BinlogStats to have a FileSize equal to zero and no records processed.
        /// </summary>
//         [Fact] [Error] (48-44)CS1579 foreach statement cannot operate on variables of type 'BinlogStats.RecordsByType' because 'BinlogStats.RecordsByType' does not contain a public instance or extension definition for 'GetEnumerator' [Error] (50-41)CS1503 Argument 2: cannot convert from 'method group' to 'int'
//         public void Calculate_WithEmptyFile_ReturnsStatsWithNoRecords()
//         {
//             // Arrange
//             string tempFile = Path.GetTempFileName();
//             try
//             {
//                 // Ensure the file is empty.
//                 using (FileStream fs = new FileStream(tempFile, FileMode.Truncate))
//                 {
//                     fs.SetLength(0);
//                 }
// 
//                 // Act
//                 // This test assumes that an empty file yields an empty records enumeration.
//                 // Since the BinLogReader is instantiated internally, the expected result is that no records are processed.
//                 BinlogStats stats = BinlogStats.Calculate(tempFile);
// 
//                 // Assert
//                 Assert.NotNull(stats);
//                 Assert.Equal(0, stats.FileSize);
//                 Assert.Equal(0, stats.RecordCount);
//                 Assert.Equal(0, stats.UncompressedStreamSize);
//                 Assert.Empty(stats.AllStrings);
//                 // The CategorizedRecords may be non-null but should have no buckets with records.
//                 if (stats.CategorizedRecords != null)
//                 {
//                     foreach (var bucket in stats.CategorizedRecords)
//                     {
//                         Assert.Equal(0, bucket.Count);
//                     }
//                 }
//             }
//             finally
//             {
//                 // Cleanup temporary file.
//                 if (File.Exists(tempFile))
//                 {
//                     File.Delete(tempFile);
//                 }
//             }
//         }

        /// <summary>
        /// Tests the GetString method to ensure it returns a correctly formatted string.
        /// </summary>
        /// <param name="name">Input name.</param>
        /// <param name="total">Total size value.</param>
        /// <param name="count">Count value.</param>
        /// <param name="largest">Largest value.</param>
        [Theory]
        [InlineData("TestName", 1000, 5, 300)]
        [InlineData("LongerTestName", 123456789, 10, 5000)]
        public void GetString_WithValidInputs_ReturnsFormattedString(string name, long total, int count, int largest)
        {
            // Arrange
            // Expected string format as per GetString implementation.
            string expected = $"{name.PadRight(30, ' ')}\t\t\tTotal size: {total:N0}\t\t\tCount: {count:N0}\t\t\tLargest: {largest:N0}";

            // Act
            string result = BinlogStats.GetString(name, total, count, largest);

            // Assert
            Assert.Equal(expected, result);
        }
    }

    /// <summary>
    /// Unit tests for the <see cref="BinlogStats.RecordsByType"/> nested class.
    /// </summary>
    public class BinlogStatsRecordsByTypeTests
    {
        /// <summary>
        /// A dummy implementation of the Record class used for testing purposes.
        /// </summary>
        internal class DummyRecord
        {
            public long Length { get; set; }
            public object Args { get; set; }

            public DummyRecord(long length, object args)
            {
                Length = length;
                Args = args;
            }
        }

        /// <summary>
        /// Tests the Add method when called with a null type.
        /// The record should be added to the current bucket's list.
        /// </summary>
        [Fact]
        public void Add_NullType_AddsRecordToBaseList()
        {
            // Arrange
            var recordsByType = new BinlogStats.RecordsByType("Base");
            var record = new DummyRecord(150, null);
            int initialCount = recordsByType.Records.Count();

            // Act
            // Passing type as null should add the record to the base list.
            // We cast DummyRecord to dynamic to simulate the necessary properties for sorting (Length and Args).
            recordsByType.Add(record as dynamic, null, null);

            // Assert
            Assert.Equal(initialCount + 1, recordsByType.Records.Count());
            // Validate that the Count, TotalLength, and Largest properties are updated.
            Assert.Equal(1, recordsByType.Count);
            Assert.Equal(150, recordsByType.TotalLength);
            Assert.Equal(150, recordsByType.Largest);
        }

        /// <summary>
        /// Tests the Add method when called with a non-null type and a valid BinlogStats instance.
        /// The record should be added to a sub-bucket.
        /// </summary>
//         [Fact] [Error] (145-40)CS1729 'BuildMessageEventArgs' does not contain a constructor that takes 3 arguments
//         public void Add_NonNullType_AndValidStats_AddsRecordToSubBucket()
//         {
//             // Arrange
//             var rootBucket = new BinlogStats.RecordsByType("Root");
//             var stats = new BinlogStats();
//             // Create a BuildMessageEventArgs with a message that does not match any specific subtype rules.
//             var message = "Some message that is longer than 50 characters to avoid 'Short' categorization override.";
//             var buildMessageArgs = new BuildMessageEventArgs(message, string.Empty, "sender");
//             var record = new DummyRecord(200, buildMessageArgs);
// 
//             // Act
//             // Passing a non-null type should route the record to a sub-bucket.
//             rootBucket.Add(record as dynamic, "TestType", stats);
// 
//             // After adding, seal the bucket to set CategorizedRecords.
//             rootBucket.Seal();
// 
//             // Assert
//             // The root bucket's Count should include the new record.
//             Assert.Equal(1, rootBucket.Count);
//             // The sub-bucket should exist in the CategorizedRecords.
//             Assert.NotNull(rootBucket.CategorizedRecords);
//             Assert.Single(rootBucket.CategorizedRecords);
//             var subBucket = rootBucket.CategorizedRecords.First();
//             // Since stats.GetSubType("TestType", record.Args) is expected to return null (because "TestType" does not match any condition),
//             // the record will be added to the base list of the sub-bucket.
//             Assert.Equal(1, subBucket.Count);
//             Assert.Single(subBucket.Records);
//             Assert.Equal(200, subBucket.TotalLength);
//         }

        /// <summary>
        /// Tests the Seal method to ensure that records in the base list are sorted in descending order based on criteria.
        /// </summary>
//         [Fact] [Error] (180-33)CS1729 'BuildMessageEventArgs' does not contain a constructor that takes 3 arguments [Error] (181-32)CS1729 'BuildMessageEventArgs' does not contain a constructor that takes 3 arguments
//         public void Seal_WithMultipleRecords_SortsRecordsInDescendingOrder()
//         {
//             // Arrange
//             var bucket = new BinlogStats.RecordsByType("SortTest");
//             // Create two records with BuildMessageEventArgs with different message lengths.
//             var shortMsg = "Short msg";
//             var longMsg = "This is a much longer build message for testing sort order.";
//             var argsShort = new BuildMessageEventArgs(shortMsg, string.Empty, "sender");
//             var argsLong = new BuildMessageEventArgs(longMsg, string.Empty, "sender");
// 
//             var recordShort = new DummyRecord(shortMsg.Length, argsShort);
//             var recordLong = new DummyRecord(longMsg.Length, argsLong);
// 
//             // Act
//             // Add records to base list by passing type as null.
//             bucket.Add(recordShort as dynamic, null, null);
//             bucket.Add(recordLong as dynamic, null, null);
// 
//             // Before seal, order is as added.
//             var preSealOrder = bucket.Records.ToList();
//             // Now, call Seal to perform sorting.
//             bucket.Seal();
//             var postSealOrder = bucket.Records.ToList();
// 
//             // Assert
//             // Verify that after sealing, the record with the longer message appears before the shorter one.
//             Assert.Equal(2, bucket.Count);
//             Assert.True(postSealOrder.First().Length >= postSealOrder.Last().Length);
//         }

        /// <summary>
        /// Tests the ToString override to ensure it returns the same formatted output as GetString.
        /// </summary>
        [Fact]
        public void ToString_ReturnsFormattedStringBasedOnProperties()
        {
            // Arrange
            string type = "TestBucket";
            var bucket = new BinlogStats.RecordsByType(type);
            // Manually update properties to simulate processed records.
            // For testing purposes, we simulate a scenario with total length = 500, count = 3, largest = 300.
            typeof(BinlogStats.RecordsByType)
                .GetProperty("TotalLength")
                .SetValue(bucket, (long)500);
            typeof(BinlogStats.RecordsByType)
                .GetProperty("Count")
                .SetValue(bucket, 3);
            typeof(BinlogStats.RecordsByType)
                .GetProperty("Largest")
                .SetValue(bucket, 300);

            string expected = BinlogStats.GetString(type, 500, 3, 300);

            // Act
            string result = bucket.ToString();

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
