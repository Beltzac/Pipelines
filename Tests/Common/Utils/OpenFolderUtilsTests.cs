using Common.Utils;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Common.Tests.Utils
{
    public class OpenFolderUtilsTests
    {
        private readonly Mock<ILogger> _loggerMock;

        public OpenFolderUtilsTests()
        {
            _loggerMock = new Mock<ILogger>();
        }

        [Test]
        public void FindSolutionFile_WithSolutionInRoot_ReturnsSolutionPath()
        {
            // Arrange
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath);
            var solutionPath = Path.Combine(tempPath, "test.sln");
            File.WriteAllText(solutionPath, "");

            try
            {
                // Act
                var result = OpenFolderUtils.FindSolutionFile(tempPath);

                // Assert
                result.Should().Be(solutionPath);
            }
            finally
            {
                // Cleanup
                Directory.Delete(tempPath, true);
            }
        }

        [Test]
        public void ProjectContainsTopshelfReference_WithTopshelfReference_ReturnsTrue()
        {
            // Arrange
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath);
            var projectPath = Path.Combine(tempPath, "test.csproj");

            var projectContent = @"
                <Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Topshelf"" Version=""4.3.0"" />
                    </ItemGroup>
                </Project>";

            File.WriteAllText(projectPath, projectContent);

            try
            {
                // Act
                var result = OpenFolderUtils.ProjectContainsTopshelfReference(projectPath);

                // Assert
                result.Should().BeTrue();
            }
            finally
            {
                // Cleanup
                Directory.Delete(tempPath, true);
            }
        }

        [Test]
        public void ProjectContainsTopshelfReference_WithoutTopshelfReference_ReturnsFalse()
        {
            // Arrange
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath);
            var projectPath = Path.Combine(tempPath, "test.csproj");

            var projectContent = @"
                <Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.1"" />
                    </ItemGroup>
                </Project>";

            File.WriteAllText(projectPath, projectContent);

            try
            {
                // Act
                var result = OpenFolderUtils.ProjectContainsTopshelfReference(projectPath);

                // Assert
                result.Should().BeFalse();
            }
            finally
            {
                // Cleanup
                Directory.Delete(tempPath, true);
            }
        }
    }
}
