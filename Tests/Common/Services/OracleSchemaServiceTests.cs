using Common.Models;
using Common.Services;
using Common.Services.Interfaces;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Dapper;
using System.Data;

namespace Tests.Common.Services
{
    public class OracleSchemaServiceTests
    {
        private readonly OracleSchemaService _service;
        private readonly Mock<ILogger<OracleSchemaService>> _loggerMock;
        private readonly Mock<IConfigurationService> _configServiceMock;
        private readonly Mock<IDbConnection> _connectionMock;
        private readonly Mock<IOracleConnectionFactory> _connectionFactoryMock;

        public OracleSchemaServiceTests()
        {
            _loggerMock = new Mock<ILogger<OracleSchemaService>>();
            _configServiceMock = new Mock<IConfigurationService>();
            _connectionMock = new Mock<IDbConnection>();
            _connectionFactoryMock = new Mock<IOracleConnectionFactory>();

            // Mock the connection factory to return a mock connection
            _connectionFactoryMock.Setup(f => f.CreateConnection(It.IsAny<string>()))
                .Returns(_connectionMock.Object);

            // Use Moq.Dapper to mock Dapper's QueryAsync extension method
            _connectionMock
                .SetupDapperAsync(c => c.QueryAsync<(string sql, string param)>(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    null,
                    null,
                    null))
                .ReturnsAsync(new List<(string ViewName, string Text)>
                {
                    ("VIEW_1", "SELECT * FROM TABLE_1"),
                    ("VIEW_2", "SELECT * FROM TABLE_2")
                });

            // Use Moq.Dapper to mock Dapper's QueryFirstAsync extension method
            _connectionMock
                .SetupDapperAsync(c => c.QueryFirstAsync<int>(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    null,
                    null,
                    null))
                .ReturnsAsync(1);

            _service = new OracleSchemaService(
                _loggerMock.Object,
                _configServiceMock.Object,
                _connectionFactoryMock.Object);
        }

        [Test]
        public async Task TestConnectionAsync_ReturnsTrue()
        {
            // Arrange

            // Use Moq.Dapper to mock QueryFirstAsync
            _connectionMock
                .SetupDapperAsync(c => c.QueryFirstAsync<int>(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    null,
                    null,
                    null))
                .ReturnsAsync(1);

            // Act
            var result = await _service.TestConnectionAsync("mock-connection-string", "MOCK_SCHEMA");

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public async Task GetViewDefinitionAsync_ReturnsCorrectView()
        {
            var viewDefinition = await _service.GetViewDefinitionAsync("mock-connection-string", "MOCK_SCHEMA", "mock-view");
            viewDefinition.Should().NotBeNull();
            viewDefinition.Name.Should().Be("mock-view");
        }

        [Test]
        public async Task GetViewDefinitionsAsync_ReturnsAllViews()
        {
            var viewDefinitions = await _service.GetViewDefinitionsAsync("mock-connection-string", "MOCK_SCHEMA");
            viewDefinitions.Should().HaveCount(2)
                .And.Contain(v => v.Name == "VIEW_1")
                .And.Contain(v => v.Name == "VIEW_2");
        }

        [Test]
        public async Task CompareViewDefinitions_ReturnsCorrectDifferences()
        {
            var devViews = new List<OracleViewDefinition>
            {
                new("VIEW_1", "VIEW_1_SQL"),
                new("VIEW_2", "VIEW_2_SQL")
            };

            var qaViews = new List<OracleViewDefinition>
            {
                new("VIEW_1", "VIEW_1_QA")
            };

            var differences = await _service.CompareViewDefinitions(devViews, qaViews);
            differences.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                ViewName = "VIEW_1",
                HasDifferences = true
            });
        }

        [Test]
        public async Task Compare_ReturnsCorrectDifferences()
        {
            var config = new ConfigModel
            {
                OracleEnvironments = new List<OracleEnvironment>
                {
                    new() { Name = "DEV", ConnectionString = "mock-dev-conn", Schema = "DEV_SCHEMA" },
                    new() { Name = "QA", ConnectionString = "mock-qa-conn", Schema = "QA_SCHEMA" }
                }
            };

            _configServiceMock.Setup(s => s.GetConfig()).Returns(config);

            var results = await _service.Compare("DEV", "QA");
            results.Should().ContainSingle().Which.Key.Should().Be("V1");
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

            differences.Should().ContainSingle();
            differences.First().Key.Should().Be("View2");
        }

        [Test]
        public async Task GetViewDefinitionsAsync_ShouldReturnViewDefinitions()
        {
            var connectionString = "FakeConnectionString";
            var schema = "FakeSchema";

            var expectedViews = new List<(string ViewName, string Text)>
            {
                ("View1", "SELECT * FROM Table1"),
                ("View2", "SELECT * FROM Table2")
            };

            var viewDefinitions = await _service.GetViewDefinitionsAsync(connectionString, schema);

            viewDefinitions.Should().HaveCount(expectedViews.Count);
            viewDefinitions.Should().Contain(v => v.Name == "View1" && v.Definition == "SELECT * FROM Table1");
            viewDefinitions.Should().Contain(v => v.Name == "View2" && v.Definition == "SELECT * FROM Table2");
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

            differences.Should().BeEmpty();
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

            differences.Should().ContainSingle();
            differences.First().Key.Should().Be("View2");
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

            differences.Should().BeEmpty();
        }

        [Test]
        public async Task Compare_ShouldReturnDifferences_WhenSchemasDiffer()
        {
            var config = new ConfigModel
            {
                OracleEnvironments = new List<OracleEnvironment>
                {
                    new() { Name = "DEV", ConnectionString = "mock-dev-conn", Schema = "DEV_SCHEMA" },
                    new() { Name = "QA", ConnectionString = "mock-qa-conn", Schema = "QA_SCHEMA" }
                }
            };

            _configServiceMock.Setup(s => s.GetConfig()).Returns(config);

            var results = await _service.Compare("DEV", "QA");

            results.Should().ContainSingle().Which.Key.Should().Be("V1");
        }

        [Test]
        public async Task Compare_ShouldReturnNoDifferences_WhenSchemasAreIdentical()
        {
            var config = new ConfigModel
            {
                OracleEnvironments = new List<OracleEnvironment>
                {
                    new() { Name = "DEV", ConnectionString = "mock-dev-conn", Schema = "DEV_SCHEMA" },
                    new() { Name = "DEV", ConnectionString = "mock-dev-conn", Schema = "DEV_SCHEMA" }
                }
            };

            _configServiceMock.Setup(s => s.GetConfig()).Returns(config);

            var results = await _service.Compare("DEV", "DEV");

            results.Should().BeEmpty();
        }
    }
}
