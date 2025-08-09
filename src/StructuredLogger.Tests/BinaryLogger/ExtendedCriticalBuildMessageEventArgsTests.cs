// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.Framework;
using Moq;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ExtendedCriticalBuildMessageEventArgs"/> class.
    /// </summary>
    public class ExtendedCriticalBuildMessageEventArgsTests
    {
        /// <summary>
        /// Tests that the constructor accepting only the type parameter sets the ExtendedType correctly.
        /// Also verifies that ExtendedMetadata and ExtendedData remain null.
        /// </summary>
        [Fact]
        public void Constructor_WithTypeOnly_SetsExtendedType()
        {
            // Arrange
            string expectedType = "CustomType";

            // Act
            var eventArgs = new ExtendedCriticalBuildMessageEventArgs(expectedType);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
            Assert.Null(eventArgs.ExtendedMetadata);
            Assert.Null(eventArgs.ExtendedData);
        }

        /// <summary>
        /// Tests that the constructor without a timestamp properly sets the ExtendedType and leaves optional properties null.
        /// </summary>
        [Fact]
        public void Constructor_WithoutTimestamp_ValidParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            string expectedType = "Error";
            string? subcategory = null;
            string? code = "E001";
            string? file = "File.cs";
            int lineNumber = 10;
            int columnNumber = 5;
            int endLineNumber = 10;
            int endColumnNumber = 15;
            string? message = "An error occurred.";
            string? helpKeyword = "Help";
            string? senderName = "Compiler";

            // Act
            var eventArgs = new ExtendedCriticalBuildMessageEventArgs(
                expectedType,
                subcategory,
                code,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message,
                helpKeyword,
                senderName);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
            Assert.Null(eventArgs.ExtendedMetadata);
            Assert.Null(eventArgs.ExtendedData);
        }

        /// <summary>
        /// Tests that the constructor with a custom timestamp properly sets the ExtendedType and leaves optional properties null.
        /// </summary>
        [Fact]
        public void Constructor_WithTimestamp_ValidParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            string expectedType = "Warning";
            string? subcategory = "SubWarning";
            string? code = "W001";
            string? file = "WarningFile.cs";
            int lineNumber = 20;
            int columnNumber = 3;
            int endLineNumber = 20;
            int endColumnNumber = 30;
            string? message = "Warning message.";
            string? helpKeyword = "WarnHelp";
            string? senderName = "Analyzer";
            DateTime customTimestamp = new DateTime(2020, 1, 1);

            // Act
            var eventArgs = new ExtendedCriticalBuildMessageEventArgs(
                expectedType,
                subcategory,
                code,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message,
                helpKeyword,
                senderName,
                customTimestamp);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
            Assert.Null(eventArgs.ExtendedMetadata);
            Assert.Null(eventArgs.ExtendedData);
        }

        /// <summary>
        /// Tests that the constructor with message arguments properly sets the ExtendedType and leaves optional properties null.
        /// </summary>
        [Fact]
        public void Constructor_WithMessageArgs_ValidParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            string expectedType = "Info";
            string? subcategory = "SubInfo";
            string? code = "I001";
            string? file = "InfoFile.cs";
            int lineNumber = 30;
            int columnNumber = 2;
            int endLineNumber = 30;
            int endColumnNumber = 25;
            string? message = "Informational message.";
            string? helpKeyword = "InfoHelp";
            string? senderName = "Logger";
            DateTime customTimestamp = new DateTime(2021, 6, 15);
            object[] messageArgs = new object[] { "arg1", 123 };

            // Act
            var eventArgs = new ExtendedCriticalBuildMessageEventArgs(
                expectedType,
                subcategory,
                code,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message,
                helpKeyword,
                senderName,
                customTimestamp,
                messageArgs);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
            Assert.Null(eventArgs.ExtendedMetadata);
            Assert.Null(eventArgs.ExtendedData);
        }

        /// <summary>
        /// Tests that the internal default constructor sets the ExtendedType to "undefined".
        /// Uses reflection to access the non-public default constructor.
        /// </summary>
        [Fact]
        public void DefaultConstructor_Internal_SetsExtendedTypeToUndefined()
        {
            // Arrange & Act
            var instance = (ExtendedCriticalBuildMessageEventArgs)Activator.CreateInstance(
                typeof(ExtendedCriticalBuildMessageEventArgs),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: null,
                culture: null)!;

            // Assert
            Assert.Equal("undefined", instance.ExtendedType);
            Assert.Null(instance.ExtendedMetadata);
            Assert.Null(instance.ExtendedData);
        }

        /// <summary>
        /// Tests that the properties ExtendedType, ExtendedMetadata, and ExtendedData can be set and retrieved correctly.
        /// </summary>
        [Fact]
        public void Properties_GetterAndSetter_WorkAsExpected()
        {
            // Arrange
            var eventArgs = new ExtendedCriticalBuildMessageEventArgs("InitialType");
            var metadata = new Dictionary<string, string?> { { "Key", "Value" } };
            string newType = "NewType";
            string newData = "Some extended data";

            // Act
            eventArgs.ExtendedType = newType;
            eventArgs.ExtendedMetadata = metadata;
            eventArgs.ExtendedData = newData;

            // Assert
            Assert.Equal(newType, eventArgs.ExtendedType);
            Assert.Equal(metadata, eventArgs.ExtendedMetadata);
            Assert.Equal(newData, eventArgs.ExtendedData);
        }

        /// <summary>
        /// Tests that constructors handle null values for optional parameters gracefully.
        /// Ensures that ExtendedType is set correctly even when other parameters are null.
        /// </summary>
        [Fact]
        public void Constructors_NullOptionalParameters_HandleNullValuesGracefully()
        {
            // Arrange
            string expectedType = "NullTest";
            DateTime customTimestamp = DateTime.UtcNow;

            // Act: Constructor without timestamp.
            var eventArgs1 = new ExtendedCriticalBuildMessageEventArgs(
                expectedType,
                subcategory: null,
                code: null,
                file: null,
                lineNumber: 0,
                columnNumber: 0,
                endLineNumber: 0,
                endColumnNumber: 0,
                message: null,
                helpKeyword: null,
                senderName: null);

            // Act: Constructor with timestamp.
            var eventArgs2 = new ExtendedCriticalBuildMessageEventArgs(
                expectedType,
                subcategory: null,
                code: null,
                file: null,
                lineNumber: 0,
                columnNumber: 0,
                endLineNumber: 0,
                endColumnNumber: 0,
                message: null,
                helpKeyword: null,
                senderName: null,
                eventTimestamp: customTimestamp);

            // Act: Constructor with message arguments (null messageArgs).
            var eventArgs3 = new ExtendedCriticalBuildMessageEventArgs(
                expectedType,
                subcategory: null,
                code: null,
                file: null,
                lineNumber: 0,
                columnNumber: 0,
                endLineNumber: 0,
                endColumnNumber: 0,
                message: null,
                helpKeyword: null,
                senderName: null,
                eventTimestamp: customTimestamp,
                messageArgs: null);

            // Assert
            Assert.Equal(expectedType, eventArgs1.ExtendedType);
            Assert.Equal(expectedType, eventArgs2.ExtendedType);
            Assert.Equal(expectedType, eventArgs3.ExtendedType);
        }
    }
}
