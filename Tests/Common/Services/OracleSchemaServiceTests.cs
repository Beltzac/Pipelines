using Common.Models;
using Common.Repositories.TCP.Interfaces;
using Common.Services;
using FluentAssertions;
using KellermanSoftware.CompareNetObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests.Common.Services
{
    public class OracleSchemaServiceTests
    {
        private readonly DbContextOptions<TcpDbContext> _options;
        private readonly DbContext _context;
        private readonly OracleSchemaService _service;
        private readonly Mock<IConfigurationService> _configServiceMock;
        private readonly Mock<IOracleRepository> _oracleRepositoryMock;

        public OracleSchemaServiceTests()
        {
            _configServiceMock = new Mock<IConfigurationService>();
            _oracleRepositoryMock = new Mock<IOracleRepository>();

            _service = new OracleSchemaService(
                Mock.Of<ILogger<OracleSchemaService>>(),
                _configServiceMock.Object,
                _oracleRepositoryMock.Object);
        }

        [Test]
        public async Task TestConnectionAsync_ReturnsTrue()
        {
            // Arrange
            _oracleRepositoryMock.Setup(repo => repo.GetSingleFromSqlAsync<int>(It.IsAny<string>(), It.IsAny<FormattableString>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            var result = await _service.TestConnectionAsync("mock-connection-string", "MOCK_SCHEMA");

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public async Task GetViewDefinitionAsync_ReturnsCorrectView()
        {
            // Arrange
            _oracleRepositoryMock.Setup(repo => repo.GetSingleFromSqlAsync<OracleViewDefinition>(It.IsAny<string>(), It.IsAny<FormattableString>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OracleViewDefinition("mock-view", "SELECT * FROM mock-table") { Owner = "MOCK_SCHEMA" });

            // Act
            var viewDefinition = await _service.GetViewDefinitionAsync("mock-connection-string", "MOCK_SCHEMA", "mock-view");

            // Assert
            viewDefinition.Should().NotBeNull();
            viewDefinition.Name.Should().Be("mock-view");
        }

        [Test]
        public async Task GetViewDefinitionsAsync_ReturnsAllViews()
        {
            // Arrange
            _oracleRepositoryMock.Setup(repo => repo.GetFromSqlAsync<OracleViewDefinition>(It.IsAny<string>(), It.IsAny<FormattableString>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([new OracleViewDefinition("VIEW1", "SELECT * FROM TABLE1") { Owner = "MOCK_SCHEMA" }, new OracleViewDefinition("VIEW2", "SELECT * FROM TABLE2") { Owner = "MOCK_SCHEMA" }]);

            // Act
            var results = await _service.GetViewDefinitionsAsync("any", "MOCK_SCHEMA");

            // Assert
            results.Should().HaveCount(2);
        }

        [Test]
        public async Task CompareViewDefinitions_ReturnsCorrectDifferences()
        {
            var devViews = new List<OracleViewDefinition>
            {
                new("VIEW1", "CREATE VIEW VIEW1 AS SELECT * FROM TABLE1"),
                new("VIEW2", "CREATE VIEW VIEW2 AS SELECT * FROM TABLE2")
            };

            var qaViews = new List<OracleViewDefinition>
            {
                new("VIEW1", "CREATE VIEW VIEW1 AS SELECT * FROM TABLE1_MODIFIED")
            };

            var differences = await _service.CompareViewDefinitions(devViews, qaViews);

            differences.Should().ContainSingle(d =>
                d.Key == "VIEW1" &&
                d.FormattedDiff.Contains("TABLE1_MODIFIED") &&
                d.HasDifferences
            );
        }

        [Test]
        public async Task Compare_ReturnsCorrectDifferences()
        {
            // Arrange
            var config = new ConfigModel
            {
                OracleEnvironments =
                [
                    new() { Name = "DEV", ConnectionString = "mock-dev-conn", Schema = "DEV_SCHEMA" },
                    new() { Name = "QA", ConnectionString = "mock-qa-conn", Schema = "QA_SCHEMA" }
                ]
            };

            // Mock DEV views
            _oracleRepositoryMock.Setup(repo => repo.GetFromSqlAsync<OracleViewDefinition>(
                    "mock-dev-conn",
                    It.IsAny<FormattableString>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([new("View1", "DEV_VIEW_DEFINITION")]);

            // Mock QA views
            _oracleRepositoryMock.Setup(repo => repo.GetFromSqlAsync<OracleViewDefinition>(
                    "mock-qa-conn",
                    It.IsAny<FormattableString>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([new("View1", "QA_VIEW_DEFINITION")]);

            _configServiceMock.Setup(s => s.GetConfig()).Returns(config);

            // Act
            var results = await _service.Compare("DEV", "QA");

            // Assert
            results.Should().ContainSingle().Which.Should().Match<OracleDiffResult>(x =>
                x.Key == "View1" &&
                x.FormattedDiff.Contains("DEV_VIEW_DEFINITION") &&
                x.HasDifferences
            );
        }

        [Test]
        public async Task Compare_ShouldReturnDifferences_WhenSchemasAreDifferent()
        {
            var sourceViews = new List<OracleViewDefinition>
            {
                new("View1", "SELECT * FROM Table1"),
                new("View2", "SELECT * FROM Table2")
            };

            var targetViews = new List<OracleViewDefinition>
            {
                new("View1", "SELECT * FROM Table1"),
                new("View2", "SELECT * FROM Table2 WHERE Column1 = 'Value'")
            };

            var differences = await _service.CompareViewDefinitions(sourceViews, targetViews);

            differences.Where(x => x.HasDifferences).Should().ContainSingle();
            differences.First(x => x.HasDifferences).Key.Should().Be("View2");
        }

        [Test]
        public async Task GetViewDefinitionsAsync_ShouldReturnViewDefinitions()
        {
            var connectionString = "FakeConnectionString";
            var schema = "FakeSchema";

            var expectedViews = new List<(string ViewName, string Text)>
            {
                ("VIEW1", "SELECT * FROM TABLE1"),
                ("VIEW2", "SELECT * FROM TABLE2")
            };

            _oracleRepositoryMock.Setup(repo => repo.GetFromSqlAsync<OracleViewDefinition>(It.IsAny<string>(), It.IsAny<FormattableString>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OracleViewDefinition> { new OracleViewDefinition("VIEW1", "SELECT * FROM TABLE1") { Owner = "MOCK_SCHEMA" }, new OracleViewDefinition("VIEW2", "SELECT * FROM TABLE2") { Owner = "MOCK_SCHEMA" } });

            var viewDefinitions = await _service.GetViewDefinitionsAsync(connectionString, schema);

            viewDefinitions.Should().HaveCount(expectedViews.Count);
            viewDefinitions.Should().Contain(v =>
                v.Name == "VIEW1" && // Ensure exact case match
                v.Definition == "SELECT * FROM TABLE1"
            );
            viewDefinitions.Should().Contain(v =>
                v.Name == "VIEW2" && // Ensure exact case match
                v.Definition == "SELECT * FROM TABLE2"
            );
        }

        [Test]
        public async Task TestConnectionAsync_ShouldReturnTrue_WhenConnectionIsSuccessful()
        {
            var connectionString = "ValidConnectionString";
            var schema = "ValidSchema";

            var result = await _service.TestConnectionAsync(connectionString, schema);

            result.Should().BeTrue();
        }

        [Test]
        public async Task GetViewDefinitionAsync_ShouldReturnCorrectViewDefinition()
        {
            _oracleRepositoryMock.Setup(repo => repo.GetSingleFromSqlAsync<OracleViewDefinition>(It.IsAny<string>(), It.IsAny<FormattableString>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OracleViewDefinition("View1", "SELECT * FROM TABLE1") { Owner = "MOCK_SCHEMA" });

            var connectionString = "ValidConnectionString";
            var schema = "ValidSchema";
            var viewName = "View1";

            var viewDefinition = await _service.GetViewDefinitionAsync(connectionString, schema, viewName);

            viewDefinition.Should().NotBeNull();
            viewDefinition.Name.Should().Be(viewName);
        }

        [Test]
        public async Task CompareViewDefinitions_ShouldReturnNoDifferences_WhenSchemasAreIdentical()
        {
            var sourceViews = new List<OracleViewDefinition>
            {
                new("View1", "SELECT * FROM Table1"),
                new("View2", "SELECT * FROM Table2")
            };

            var targetViews = new List<OracleViewDefinition>
            {
                new("View1", "SELECT * FROM Table1"),
                new("View2", "SELECT * FROM Table2")
            };

            var differences = await _service.CompareViewDefinitions(sourceViews, targetViews);

            differences.Where(x => x.HasDifferences).Should().BeEmpty();
        }

        [Test]
        public async Task CompareViewDefinitions_ShouldReturnDifferences_WhenViewDefinitionsDiffer()
        {
            var sourceViews = new List<OracleViewDefinition>
            {
                new("View1", "SELECT * FROM Table1"),
                new("View2", "SELECT * FROM Table2")
            };

            var targetViews = new List<OracleViewDefinition>
            {
                new("View1", "SELECT * FROM Table1"),
                new("View2", "SELECT * FROM Table2 WHERE Column1 = 'Value'")
            };

            var differences = await _service.CompareViewDefinitions(sourceViews, targetViews);

            differences.Where(x => x.HasDifferences).Should().ContainSingle();
            differences.First(x => x.HasDifferences).Key.Should().Be("View2");
        }

        [Test]
        public async Task CompareViewDefinitions_ShouldReturnNoDifferences_WhenViewDefinitionsAreIdentical()
        {
            var sourceViews = new List<OracleViewDefinition>
            {
                new("View1", "SELECT * FROM Table1"),
                new("View2", "SELECT * FROM Table2")
            };

            var targetViews = new List<OracleViewDefinition>
            {
                new("View1", "SELECT * FROM Table1"),
                new("View2", "SELECT * FROM Table2")
            };

            var differences = await _service.CompareViewDefinitions(sourceViews, targetViews);

            differences.Where(x => x.HasDifferences).Should().BeEmpty();
        }

        [Test]
        public async Task Compare_ShouldReturnDifferences_WhenSchemasDiffer()
        {
            // Arrange
            var config = new ConfigModel
            {
                OracleEnvironments =
                [
                    new() { Name = "DEV", ConnectionString = "mock-dev-conn", Schema = "DEV_SCHEMA" },
                    new() { Name = "QA", ConnectionString = "mock-qa-conn", Schema = "QA_SCHEMA" }
                ]
            };

            _configServiceMock.Setup(s => s.GetConfig()).Returns(config);

            _oracleRepositoryMock.Setup(repo => repo.GetFromSqlAsync<OracleViewDefinition>("mock-dev-conn", It.IsAny<FormattableString>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new OracleViewDefinition("VIEW1", "SELECT * FROM DEV_VIEW_DEFINITION") { Owner = "MOCK_SCHEMA" }, new OracleViewDefinition("VIEW2", "SELECT * FROM TABLE2") { Owner = "MOCK_SCHEMA" }]);

            _oracleRepositoryMock.Setup(repo => repo.GetFromSqlAsync<OracleViewDefinition>("mock-qa-conn", It.IsAny<FormattableString>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new OracleViewDefinition("VIEW1", "SELECT * FROM QA_VIEW_DEFINITION") { Owner = "MOCK_SCHEMA" }, new OracleViewDefinition("VIEW2", "SELECT * FROM TABLE2") { Owner = "MOCK_SCHEMA" }]);

            // Act
            var results = await _service.Compare("DEV", "QA");

            // Assert
            results.Where(x => x.HasDifferences).Should().ContainSingle().Which.Should().Match<OracleDiffResult>(x =>
                x.Key == "VIEW1" &&
                x.FormattedDiff.Contains("DEV_VIEW_DEFINITION") &&
                x.HasDifferences
            );
        }

        [Test]
        public async Task Compare_ShouldReturnNoDifferences_WhenSchemasAreIdentical()
        {
            var config = new ConfigModel
            {
                OracleEnvironments =
                [
                    new() { Name = "DEV", ConnectionString = "mock-dev-conn", Schema = "DEV_SCHEMA" },
                    new() { Name = "DEV", ConnectionString = "mock-dev-conn", Schema = "DEV_SCHEMA" }
                ]
            };

            _configServiceMock.Setup(s => s.GetConfig()).Returns(config);

            _oracleRepositoryMock.Setup(repo => repo.GetFromSqlAsync<OracleViewDefinition>(It.IsAny<string>(), It.IsAny<FormattableString>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new OracleViewDefinition("VIEW1", "SELECT * FROM TABLE1") { Owner = "MOCK_SCHEMA" }, new OracleViewDefinition("VIEW2", "SELECT * FROM TABLE2") { Owner = "MOCK_SCHEMA" }]);

            var results = await _service.Compare("DEV", "DEV");

            results.Where(x => x.HasDifferences).Should().BeEmpty();
        }
    }
}
