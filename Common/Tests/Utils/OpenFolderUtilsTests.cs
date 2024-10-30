using Xunit;
using Common.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using System.Xml;

namespace Common.Tests.Utils
{
    public class OpenFolderUtilsTests
    {
        private readonly Mock<ILogger> _loggerMock;

        public OpenFolderUtilsTests()
        {
            _loggerMock = new Mock<ILogger>();
        }

        [Fact]
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
                Assert.Equal(solutionPath, result);
            }
            finally
            {
                // Cleanup
                Directory.Delete(tempPath, true);
            }
        }

        [Fact]
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
                Assert.True(result);
            }
            finally
            {
                // Cleanup
                Directory.Delete(tempPath, true);
            }
        }

        [Fact]
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
                Assert.False(result);
            }
            finally
            {
                // Cleanup
                Directory.Delete(tempPath, true);
            }
        }
    }
}
