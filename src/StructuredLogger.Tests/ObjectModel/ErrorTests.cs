using System;
using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="Error"/> class.
    /// </summary>
    public class ErrorTests
    {
        private readonly Error _error;

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorTests"/> class.
        /// </summary>
        public ErrorTests()
        {
            _error = new Error();
        }

        /// <summary>
        /// Tests that the TypeName property of <see cref="Error"/> returns "Error".
        /// Functional steps:
        /// 1. Retrieve the value of the TypeName property.
        /// 2. Assert that the returned value is equal to "Error".
        /// Expected outcome: The property returns "Error".
        /// </summary>
        [Fact]
        public void TypeName_Get_ReturnsError()
        {
            // Act
            string typeName = _error.TypeName;

            // Assert
            Assert.Equal("Error", typeName);
        }
    }

    /// <summary>
    /// Unit tests for the <see cref="BuildError"/> class.
    /// </summary>
    public class BuildErrorTests
    {
        private readonly BuildError _buildError;

        /// <summary>
        /// Initializes a new instance of the <see cref="BuildErrorTests"/> class.
        /// </summary>
        public BuildErrorTests()
        {
            _buildError = new BuildError();
        }

        /// <summary>
        /// Tests that the TypeName property of <see cref="BuildError"/> returns "Build Error".
        /// Functional steps:
        /// 1. Retrieve the value of the TypeName property.
        /// 2. Assert that the returned value is equal to "Build Error".
        /// Expected outcome: The property returns "Build Error".
        /// </summary>
        [Fact]
        public void TypeName_Get_ReturnsBuildError()
        {
            // Act
            string typeName = _buildError.TypeName;

            // Assert
            Assert.Equal("Build Error", typeName);
        }

        /// <summary>
        /// Tests that <see cref="BuildError"/> inherits from <see cref="Error"/>.
        /// Functional steps:
        /// 1. Assert that an instance of <see cref="BuildError"/> is assignable to <see cref="Error"/>.
        /// Expected outcome: The instance is assignable to the <see cref="Error"/> type.
        /// </summary>
        [Fact]
        public void BuildError_IsAssignableTo_Error()
        {
            // Act & Assert
            Assert.IsAssignableFrom<Error>(_buildError);
        }
    }
}
