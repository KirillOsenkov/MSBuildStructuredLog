using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using System;
using System.IO;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref = "EvaluationProfileEntry"/> class.
    /// </summary>
    public class EvaluationProfileEntryTests
    {
        private readonly EvaluationProfileEntry _evaluationProfileEntry;
        public EvaluationProfileEntryTests()
        {
            _evaluationProfileEntry = new EvaluationProfileEntry();
        }

        /// <summary>
        /// Tests that AddEntry method sets the ProfiledLocation property.
        /// </summary>
//         [Fact] [Error] (30-17)CS0200 Property or indexer 'ProfiledLocation.InclusiveTime' cannot be assigned to -- it is read only [Error] (31-17)CS0200 Property or indexer 'ProfiledLocation.NumberOfHits' cannot be assigned to -- it is read only
//         public void AddEntry_ValidProfiledLocation_SetsProfiledLocationProperty()
//         {
//             // Arrange
//             var expectedProfiledLocation = new ProfiledLocation
//             {
//                 InclusiveTime = TimeSpan.FromMilliseconds(200),
//                 NumberOfHits = 2
//             };
//             // Act
//             _evaluationProfileEntry.AddEntry(expectedProfiledLocation);
//             // Assert
//             Assert.NotNull(_evaluationProfileEntry.ProfiledLocation);
//             Assert.Equal(expectedProfiledLocation, _evaluationProfileEntry.ProfiledLocation);
//         }

        /// <summary>
        /// Tests that NumberOfHits property returns an empty string when number of hits is zero.
        /// </summary>
//         [Fact] [Error] (49-17)CS0200 Property or indexer 'ProfiledLocation.InclusiveTime' cannot be assigned to -- it is read only [Error] (50-17)CS0200 Property or indexer 'ProfiledLocation.NumberOfHits' cannot be assigned to -- it is read only
//         public void NumberOfHits_WhenNoHits_ReturnsEmptyString()
//         {
//             // Arrange
//             var profiledLocation = new ProfiledLocation
//             {
//                 InclusiveTime = TimeSpan.FromMilliseconds(100),
//                 NumberOfHits = 0
//             };
//             _evaluationProfileEntry.AddEntry(profiledLocation);
//             // Act
//             var result = _evaluationProfileEntry.NumberOfHits;
//             // Assert
//             Assert.Equal(string.Empty, result);
//         }

        /// <summary>
        /// Tests that NumberOfHits property returns the correct hit count as string when hits are greater than zero.
        /// </summary>
//         [Fact] [Error] (69-17)CS0200 Property or indexer 'ProfiledLocation.InclusiveTime' cannot be assigned to -- it is read only [Error] (70-17)CS0200 Property or indexer 'ProfiledLocation.NumberOfHits' cannot be assigned to -- it is read only
//         public void NumberOfHits_WhenHitsPresent_ReturnsHitsAsString()
//         {
//             // Arrange
//             int hits = 5;
//             var profiledLocation = new ProfiledLocation
//             {
//                 InclusiveTime = TimeSpan.FromMilliseconds(100),
//                 NumberOfHits = hits
//             };
//             _evaluationProfileEntry.AddEntry(profiledLocation);
//             // Act
//             var result = _evaluationProfileEntry.NumberOfHits;
//             // Assert
//             Assert.Equal(hits.ToString(), result);
//         }

        /// <summary>
        /// Tests that DurationText returns an empty string when InclusiveTime is zero.
        /// </summary>
//         [Fact] [Error] (88-17)CS0200 Property or indexer 'ProfiledLocation.InclusiveTime' cannot be assigned to -- it is read only [Error] (89-17)CS0200 Property or indexer 'ProfiledLocation.NumberOfHits' cannot be assigned to -- it is read only
//         public void DurationText_WhenInclusiveTimeIsZero_ReturnsEmptyString()
//         {
//             // Arrange
//             var profiledLocation = new ProfiledLocation
//             {
//                 InclusiveTime = TimeSpan.Zero,
//                 NumberOfHits = 1
//             };
//             _evaluationProfileEntry.AddEntry(profiledLocation);
//             // Act
//             var durationText = _evaluationProfileEntry.DurationText;
//             // Assert
//             Assert.Equal(string.Empty, durationText);
//         }

        /// <summary>
        /// Tests that DurationText returns a properly formatted string with " (1 hit)" suffix when there is one hit.
        /// </summary>
//         [Fact] [Error] (108-17)CS0200 Property or indexer 'ProfiledLocation.InclusiveTime' cannot be assigned to -- it is read only [Error] (109-17)CS0200 Property or indexer 'ProfiledLocation.NumberOfHits' cannot be assigned to -- it is read only
//         public void DurationText_WhenInclusiveTimeNonZero_AndOneHit_ReturnsDurationWithSingleHitSuffix()
//         {
//             // Arrange
//             var testTime = TimeSpan.FromMilliseconds(150);
//             var profiledLocation = new ProfiledLocation
//             {
//                 InclusiveTime = testTime,
//                 NumberOfHits = 1
//             };
//             _evaluationProfileEntry.AddEntry(profiledLocation);
//             // Act
//             var durationText = _evaluationProfileEntry.DurationText;
//             // Assert
//             // Since the DisplayDuration method is external, we check that the result contains the expected hit suffix.
//             Assert.Contains(" (1 hit)", durationText);
//             Assert.NotEqual(string.Empty, durationText);
//         }

        /// <summary>
        /// Tests that DurationText returns a properly formatted string with " (n hits)" suffix when there are multiple hits.
        /// </summary>
//         [Fact] [Error] (131-17)CS0200 Property or indexer 'ProfiledLocation.InclusiveTime' cannot be assigned to -- it is read only [Error] (132-17)CS0200 Property or indexer 'ProfiledLocation.NumberOfHits' cannot be assigned to -- it is read only
//         public void DurationText_WhenInclusiveTimeNonZero_AndMultipleHits_ReturnsDurationWithMultipleHitsSuffix()
//         {
//             // Arrange
//             var testTime = TimeSpan.FromMilliseconds(250);
//             int hits = 3;
//             var profiledLocation = new ProfiledLocation
//             {
//                 InclusiveTime = testTime,
//                 NumberOfHits = hits
//             };
//             _evaluationProfileEntry.AddEntry(profiledLocation);
//             // Act
//             var durationText = _evaluationProfileEntry.DurationText;
//             // Assert
//             Assert.Contains($" ({hits} hits)", durationText);
//             Assert.NotEqual(string.Empty, durationText);
//         }

        /// <summary>
        /// Tests that FileName property returns the correct file name extracted from SourceFilePath.
        /// </summary>
        [Fact]
        public void FileName_WhenSourceFilePathIsSet_ReturnsFileName()
        {
            // Arrange
            string filePath = @"C:\folder\file.txt";
            _evaluationProfileEntry.SourceFilePath = filePath;
            string expectedFileName = Path.GetFileName(filePath);
            // Act
            var result = _evaluationProfileEntry.FileName;
            // Assert
            Assert.Equal(expectedFileName, result);
        }

        /// <summary>
        /// Tests that Title property returns EvaluationPassDescription when SourceFilePath is null.
        /// </summary>
        [Fact]
        public void Title_WhenSourceFilePathIsNull_ReturnsEvaluationPassDescription()
        {
            // Arrange
            string evaluationDescription = "Evaluation Pass Description";
            _evaluationProfileEntry.EvaluationPassDescription = evaluationDescription;
            _evaluationProfileEntry.SourceFilePath = null;
            // Act
            var title = _evaluationProfileEntry.Title;
            // Assert
            Assert.Equal(evaluationDescription, title);
        }

        /// <summary>
        /// Tests that Title property returns the file name and line number when Kind is Element.
        /// </summary>
        [Fact]
        public void Title_WhenKindIsElement_ReturnsFileNameAndLineNumber()
        {
            // Arrange
            string filePath = @"C:\folder\build.csproj";
            _evaluationProfileEntry.SourceFilePath = filePath;
            _evaluationProfileEntry.LineNumber = 42;
            _evaluationProfileEntry.Kind = EvaluationLocationKind.Element;
            string expectedTitle = $"{Path.GetFileName(filePath)}:{_evaluationProfileEntry.LineNumber}";
            // Act
            var title = _evaluationProfileEntry.Title;
            // Assert
            Assert.Equal(expectedTitle, title);
        }

        /// <summary>
        /// Tests that Title property returns the file name, line number, and Kind when Kind is not Element.
        /// </summary>
//         [Fact] [Error] (203-44)CS0221 Constant value '999' cannot be converted to a 'EvaluationLocationKind' (use 'unchecked' syntax to override)
//         public void Title_WhenKindIsNotElement_ReturnsFileNameLineNumberAndKind()
//         {
//             // Arrange
//             string filePath = @"C:\folder\build.csproj";
//             _evaluationProfileEntry.SourceFilePath = filePath;
//             _evaluationProfileEntry.LineNumber = 42;
//             // Using a non-Element value by casting an arbitrary integer.
//             _evaluationProfileEntry.Kind = (EvaluationLocationKind)999;
//             string expectedTitle = $"{Path.GetFileName(filePath)}:{_evaluationProfileEntry.LineNumber} 999";
//             // Act
//             var title = _evaluationProfileEntry.Title;
//             // Assert
//             Assert.Equal(expectedTitle, title);
//         }

        /// <summary>
        /// Tests that ShortenedElementDescription returns the original description when its length is within the limit.
        /// </summary>
        [Fact]
        public void ShortenedElementDescription_WhenDescriptionWithinLimit_ReturnsOriginalDescription()
        {
            // Arrange
            string description = "Short description";
            _evaluationProfileEntry.ElementDescription = description;
            // Act
            var result = _evaluationProfileEntry.ShortenedElementDescription;
            // Assert
            Assert.Equal(description, result);
        }

        /// <summary>
        /// Tests that ShortenedElementDescription returns a truncated description when the original description exceeds the maximum allowed characters.
        /// </summary>
        [Fact]
        public void ShortenedElementDescription_WhenDescriptionExceedsLimit_ReturnsTruncatedDescription()
        {
            // Arrange
            string longDescription = new string ('a', 100);
            _evaluationProfileEntry.ElementDescription = longDescription;
            // Act
            var result = _evaluationProfileEntry.ShortenedElementDescription;
            // Assert
            Assert.NotEqual(longDescription, result);
            Assert.True(result.EndsWith("..."));
            // Optionally, check that the result length is not excessively long.
            Assert.True(result.Length <= 83);
        }

        /// <summary>
        /// Tests that ToString returns the same value as the Title property.
        /// </summary>
        [Fact]
        public void ToString_ReturnsTitle()
        {
            // Arrange
            string filePath = @"C:\folder\example.cs";
            _evaluationProfileEntry.SourceFilePath = filePath;
            _evaluationProfileEntry.LineNumber = 10;
            _evaluationProfileEntry.Kind = EvaluationLocationKind.Element;
            // Title should be formatted as "example.cs:10".
            var expectedTitle = _evaluationProfileEntry.Title;
            // Act
            var result = _evaluationProfileEntry.ToString();
            // Assert
            Assert.Equal(expectedTitle, result);
        }

        /// <summary>
        /// Tests that TypeName property returns the correct class name.
        /// </summary>
        [Fact]
        public void TypeName_ReturnsExpectedClassName()
        {
            // Arrange
            var expectedTypeName = nameof(EvaluationProfileEntry);
            // Act
            var result = _evaluationProfileEntry.TypeName;
            // Assert
            Assert.Equal(expectedTypeName, result);
        }

        /// <summary>
        /// Tests that accessing DurationText without setting ProfiledLocation throws a NullReferenceException.
        /// </summary>
        [Fact]
        public void DurationText_WithoutProfiledLocation_ThrowsNullReferenceException()
        {
            // Arrange
            // Ensure ProfiledLocation is not set.
            // Act & Assert
            Assert.Throws<NullReferenceException>(() =>
            {
                var _ = _evaluationProfileEntry.DurationText;
            });
        }
    }
}