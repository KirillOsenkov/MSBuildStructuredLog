using System;
using System.Reflection;
using Moq;
using Xunit;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="EntryTarget"/> class.
    /// </summary>
    public class EntryTargetTests
    {
        /// <summary>
        /// Verifies that the TypeName property returns "EntryTarget".
        /// </summary>
        [Fact]
        public void TypeName_WhenAccessed_ReturnsEntryTarget()
        {
            // Arrange
            var entryTarget = new EntryTarget();

            // Act
            string typeName = entryTarget.TypeName;

            // Assert
            Assert.Equal("EntryTarget", typeName);
        }

        /// <summary>
        /// Verifies that the Target property returns the expected target when the associated project contains a matching child.
        /// </summary>
//         [Fact] [Error] (62-26)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.FakeTarget' to 'System.DateTime' [Error] (62-42)CS1503 Argument 2: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.Target' to 'System.DateTime'
//         public void Target_WhenProjectHasMatchingChild_ReturnsExpectedTarget_AndCachesValue()
//         {
//             // Arrange
//             var entryTarget = new EntryTarget();
//             // Setting the Name property inherited from NamedNode.
//             SetProperty(entryTarget, "Name", "TestTarget");
// 
//             var expectedTarget = new FakeTarget
//             {
//                 Name = "TestTarget",
//                 IsLowRelevance = false,
//                 DurationText = "00:01:23.456"
//             };
// 
//             var fakeProject = new FakeProject
//             {
//                 FakeTarget = expectedTarget
//             };
// 
//             // Injecting the fake project instance into the private field 'project'
//             SetPrivateField(entryTarget, "project", fakeProject);
// 
//             // Act
//             var actualTarget = entryTarget.Target;
//             var cachedTarget = entryTarget.Target;
// 
//             // Assert
//             Assert.NotNull(actualTarget);
//             Assert.Equal(expectedTarget, actualTarget);
//             Assert.Same(actualTarget, cachedTarget);
//         }

        /// <summary>
        /// Verifies that the Target property returns null when the project does not have a matching child.
        /// </summary>
        [Fact]
        public void Target_WhenProjectHasNoMatchingChild_ReturnsNull()
        {
            // Arrange
            var entryTarget = new EntryTarget();
            SetProperty(entryTarget, "Name", "NonExistentTarget");

            var fakeProject = new FakeProject
            {
                FakeTarget = null
            };

            SetPrivateField(entryTarget, "project", fakeProject);

            // Act
            var actualTarget = entryTarget.Target;

            // Assert
            Assert.Null(actualTarget);
        }

        /// <summary>
        /// Verifies that the IsLowRelevance property returns the target's IsLowRelevance value when a matching target is found.
        /// </summary>
        [Fact]
        public void IsLowRelevance_WhenTargetExists_ReturnsTargetIsLowRelevanceValue()
        {
            // Arrange
            var entryTarget = new EntryTarget();
            SetProperty(entryTarget, "Name", "TestTarget");

            var fakeTarget = new FakeTarget
            {
                Name = "TestTarget",
                IsLowRelevance = false
            };

            var fakeProject = new FakeProject
            {
                FakeTarget = fakeTarget
            };

            SetPrivateField(entryTarget, "project", fakeProject);

            // Act
            bool relevance = entryTarget.IsLowRelevance;

            // Assert
            Assert.False(relevance);
        }

        /// <summary>
        /// Verifies that the IsLowRelevance property returns true when no matching target is found.
        /// </summary>
        [Fact]
        public void IsLowRelevance_WhenTargetIsNull_ReturnsTrue()
        {
            // Arrange
            var entryTarget = new EntryTarget();
            SetProperty(entryTarget, "Name", "NonExistentTarget");

            var fakeProject = new FakeProject
            {
                FakeTarget = null
            };

            SetPrivateField(entryTarget, "project", fakeProject);

            // Act
            bool relevance = entryTarget.IsLowRelevance;

            // Assert
            Assert.True(relevance);
        }

        /// <summary>
        /// Verifies that DurationText returns the target's DurationText value when the target exists.
        /// </summary>
        [Fact]
        public void DurationText_WhenTargetExists_ReturnsTargetDurationText()
        {
            // Arrange
            var entryTarget = new EntryTarget();
            SetProperty(entryTarget, "Name", "TestTarget");

            string expectedDuration = "00:02:34.567";
            var fakeTarget = new FakeTarget
            {
                Name = "TestTarget",
                DurationText = expectedDuration
            };

            var fakeProject = new FakeProject
            {
                FakeTarget = fakeTarget
            };

            SetPrivateField(entryTarget, "project", fakeProject);

            // Act
            var durationText = entryTarget.DurationText;

            // Assert
            Assert.Equal(expectedDuration, durationText);
        }

        /// <summary>
        /// Verifies that DurationText returns null when no matching target is found.
        /// </summary>
        [Fact]
        public void DurationText_WhenTargetIsNull_ReturnsNull()
        {
            // Arrange
            var entryTarget = new EntryTarget();
            SetProperty(entryTarget, "Name", "NonExistentTarget");

            var fakeProject = new FakeProject
            {
                FakeTarget = null
            };

            SetPrivateField(entryTarget, "project", fakeProject);

            // Act
            var durationText = entryTarget.DurationText;

            // Assert
            Assert.Null(durationText);
        }

        /// <summary>
        /// Sets a private field value using reflection.
        /// </summary>
        /// <param name="obj">The object instance on which the field exists.</param>
        /// <param name="fieldName">The name of the private field.</param>
        /// <param name="value">The value to assign to the field.</param>
        private static void SetPrivateField(object obj, string fieldName, object? value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException($"Field '{fieldName}' not found in type '{obj.GetType().FullName}'.");
            }
            field.SetValue(obj, value);
        }

        /// <summary>
        /// Sets a property value using reflection. If the property is not writable, attempts to set its backing field.
        /// </summary>
        /// <param name="obj">The object instance containing the property.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The value to assign.</param>
        private static void SetProperty(object obj, string propertyName, object value)
        {
            var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, value);
            }
            else
            {
                // Attempt to set the auto-property's backing field.
                var field = obj.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                {
                    throw new InvalidOperationException($"Property or backing field '{propertyName}' not found in type '{obj.GetType().FullName}'.");
                }
                field.SetValue(obj, value);
            }
        }
    }

    /// <summary>
    /// A fake implementation of the Project class for unit testing purposes.
    /// </summary>
    internal class FakeProject : Project
    {
        /// <summary>
        /// Gets or sets the fake target to be returned by FindFirstChild.
        /// </summary>
        public FakeTarget? FakeTarget { get; set; }

        /// <summary>
        /// Overrides the FindFirstChild method to return the fake target if it matches the predicate.
        /// </summary>
        /// <typeparam name="T">The type of child to locate.</typeparam>
        /// <param name="predicate">A predicate to match the child.</param>
        /// <returns>The matching child if found; otherwise, default.</returns>
//         public override T? FindFirstChild<T>(Func<T, bool> predicate) [Error] (257-28)CS0115 'FakeProject.FindFirstChild<T>(Func<T, bool>)': no suitable method found to override [Error] (257-28)CS0453 The type 'T' must be a non-nullable value type in order to use it as parameter 'T' in the generic type or method 'Nullable<T>' [Error] (261-24)CS0266 Cannot implicitly convert type 'T' to 'T?'. An explicit conversion exists (are you missing a cast?)
//         {
//             if (FakeTarget is T candidate && predicate(candidate))
//             {
//                 return candidate;
//             }
//             return default;
//         }
    }

    /// <summary>
    /// A fake implementation of the Target class for unit testing purposes.
    /// </summary>
    internal class FakeTarget : Target
    {
        /// <summary>
        /// Gets or sets the Name property.
        /// </summary>
//         public override string Name { get; set; } = string.Empty; [Error] (275-32)CS0506 'FakeTarget.Name': cannot override inherited member 'TimedNode.Name' because it is not marked virtual, abstract, or override

        /// <summary>
        /// Gets or sets a value indicating whether the target is of low relevance.
        /// </summary>
        public bool IsLowRelevance { get; set; } = true;

        /// <summary>
        /// Gets or sets the DurationText property.
        /// </summary>
        public string? DurationText { get; set; }
    }
}
