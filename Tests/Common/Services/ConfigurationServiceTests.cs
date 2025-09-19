using Common.Models;
using FluentAssertions;
using System.Text.Json;

namespace Tests.Common.Services
{
    public class ConfigurationServiceTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly string _testJsonPath;
        private readonly ConfigurationService _configService;

        public ConfigurationServiceTests()
        {
            // Use a temporary directory for testing
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            _testConfigPath = Path.Combine(tempPath, "config.properties");
            _testJsonPath = Path.Combine(tempPath, "config.json");

            // Create a test-specific ConfigurationService
            _configService = new ConfigurationService(_testConfigPath);
        }

        [Test]
        public void GetConfig_ReturnsDefaultValues_WhenConfigFileDoesNotExist()
        {
            var config = _configService.GetConfig();

            config.OrganizationUrl.Should().Be("https://dev.azure.com/terminal-cp");
            config.LocalCloneFolder.Should().Be(@"C:\repos");
            config.PAT.Should().BeNullOrEmpty();
            config.Should().NotBeNull();
            config.Should().BeOfType<ConfigModel>();
        }

        [Test]
        public async Task SaveConfigAsync_SavesConfigCorrectly()
        {
            var testConfig = new ConfigModel
            {
                PAT = "testPAT",
                OrganizationUrl = "https://test.com",
                LocalCloneFolder = @"D:\test",
                IsDarkMode = true,
                MinUpdateTime = 300,
                MaxUpdateTime = 2000
            };

            await _configService.SaveConfigAsync(testConfig);

            var loadedConfig = _configService.GetConfig();
            loadedConfig.PAT.Should().Be(testConfig.PAT);
            loadedConfig.OrganizationUrl.Should().Be(testConfig.OrganizationUrl);
            loadedConfig.LocalCloneFolder.Should().Be(testConfig.LocalCloneFolder);
            loadedConfig.IsDarkMode.Should().Be(testConfig.IsDarkMode);
            loadedConfig.MinUpdateTime.Should().Be(testConfig.MinUpdateTime);
            loadedConfig.MaxUpdateTime.Should().Be(testConfig.MaxUpdateTime);
        }

        [Test]
        public async Task SaveConfigAsync_SavesComplexObjectsAsJson()
        {
            var testConfig = new ConfigModel
            {
                ConsulEnvironments = new List<ConsulEnvironment>
                {
                    new ConsulEnvironment { Name = "TestEnv", ConsulUrl = "https://test.com" }
                },
                OracleEnvironments = new List<OracleEnvironment>
                {
                    new OracleEnvironment { Name = "TestDB", ConnectionString = "test-conn", Schema = "TEST" }
                }
            };

            await _configService.SaveConfigAsync(testConfig);

            var loadedConfig = _configService.GetConfig();
            loadedConfig.ConsulEnvironments.Should().HaveCount(1);
            loadedConfig.ConsulEnvironments[0].Name.Should().Be("TestEnv");
            loadedConfig.OracleEnvironments.Should().HaveCount(1);
            loadedConfig.OracleEnvironments[0].Name.Should().Be("TestDB");
        }

        [Test]
        public async Task SaveConfigAsync_SavesNestedObjectsWithDotNotation()
        {
            var testConfig = new ConfigModel
            {
                TempoConfig = new TempoConfiguration
                {
                    BaseUrl = "https://tempo.test.com",
                    ApiToken = "test-token",
                    AccountId = "12345"
                },
                JiraConfig = new JiraConfiguration
                {
                    BaseUrl = "https://jira.test.com",
                    Email = "test@example.com",
                    ApiToken = "jira-token"
                }
            };

            await _configService.SaveConfigAsync(testConfig);

            var loadedConfig = _configService.GetConfig();
            loadedConfig.TempoConfig.BaseUrl.Should().Be("https://tempo.test.com");
            loadedConfig.TempoConfig.ApiToken.Should().Be("test-token");
            loadedConfig.JiraConfig.BaseUrl.Should().Be("https://jira.test.com");
            loadedConfig.JiraConfig.Email.Should().Be("test@example.com");
        }

        [Test]
        public async Task SaveConfigAsync_PreservesUnknownKeys()
        {
            // Manually create a properties file with unknown keys
            var propertiesContent = new[]
            {
                "organizationurl=https://test.com",
                "unknownkey1=value1",
                "unknownkey2=value2",
                "minupdatetime=500"
            };
            await File.WriteAllLinesAsync(_testConfigPath, propertiesContent);

            // Create new service instance to load the file
            var newService = new ConfigurationService(_testConfigPath);
            var config = newService.GetConfig();
            config.PAT = "newPAT";
            await newService.SaveConfigAsync(config);

            // Check that unknown keys are preserved
            var savedContent = await File.ReadAllLinesAsync(_testConfigPath);
            savedContent.Should().Contain("unknownkey1=value1");
            savedContent.Should().Contain("unknownkey2=value2");
            savedContent.Should().Contain("pat=newPAT");
        }

        [Test]
        public async Task ImportConfigAsync_ImportsJsonAndSavesAsProperties()
        {
            var jsonConfig = @"{
                ""organizationUrl"": ""https://imported.com"",
                ""pat"": ""importedPAT"",
                ""isDarkMode"": true,
                ""consulEnvironments"": [{""name"": ""ImportedEnv"", ""consulUrl"": ""https://imported.com""}],
                ""oracleEnvironments"": [],
                ""savedQueries"": []
            }";

            await _configService.ImportConfigAsync(jsonConfig);

            // Create new service instance to load the saved properties
            var newService = new ConfigurationService(_testConfigPath);
            var loadedConfig = newService.GetConfig();
            loadedConfig.OrganizationUrl.Should().Be("https://imported.com");
            loadedConfig.PAT.Should().Be("importedPAT");
            loadedConfig.IsDarkMode.Should().Be(true);
            loadedConfig.ConsulEnvironments.Should().HaveCount(1);
            loadedConfig.ConsulEnvironments[0].Name.Should().Be("ImportedEnv");
        }

        [Test]
        public void ExportConfig_ReturnsJsonRepresentation()
        {
            var testConfig = new ConfigModel
            {
                OrganizationUrl = "https://export.com",
                PAT = "exportPAT",
                IsDarkMode = true
            };

            _configService.SaveConfigAsync(testConfig).Wait();

            var exportedJson = _configService.ExportConfig();
            exportedJson.Should().Contain("https://export.com");
            exportedJson.Should().Contain("exportPAT");
            exportedJson.Should().Contain("true");

            // Verify it's valid JSON
            var deserialized = JsonSerializer.Deserialize<ConfigModel>(exportedJson);
            deserialized.Should().NotBeNull();
            deserialized.OrganizationUrl.Should().Be("https://export.com");
        }

        [Test]
        public async Task LoadConfig_LoadsFromJsonFile_WhenPropertiesDoesNotExist()
        {
            // Create a JSON file instead of properties
            var jsonConfig = new ConfigModel
            {
                OrganizationUrl = "https://json.com",
                PAT = "jsonPAT"
            };
            var jsonContent = JsonSerializer.Serialize(jsonConfig);
            await File.WriteAllTextAsync(_testJsonPath, jsonContent);

            // Create new service instance to trigger loading
            var newService = new ConfigurationService(_testConfigPath);
            var loadedConfig = newService.GetConfig();

            loadedConfig.OrganizationUrl.Should().Be("https://json.com");
            loadedConfig.PAT.Should().Be("jsonPAT");

            // Check that properties file was created
            File.Exists(_testConfigPath).Should().Be(true);
        }

        [Test]
        public async Task SaveSavedQueriesAsync_SavesQueriesCorrectly()
        {
            var savedQueries = new List<SavedQuery>
            {
                new SavedQuery { Name = "Query1", QueryString = "SELECT * FROM test" },
                new SavedQuery { Name = "Query2", QueryString = "SELECT id FROM users" }
            };

            await _configService.SaveSavedQueriesAsync(savedQueries);

            var loadedQueries = _configService.LoadSavedQueries();
            loadedQueries.Should().HaveCount(2);
            loadedQueries[0].Name.Should().Be("Query1");
            loadedQueries[1].Name.Should().Be("Query2");
        }

        [Test]
        public void LoadSavedQueries_ReturnsEmptyList_WhenNoQueriesSaved()
        {
            var queries = _configService.LoadSavedQueries();
            queries.Should().NotBeNull();
            queries.Should().BeEmpty();
        }

        [Test]
        public async Task SaveConfigAsync_SavesCollectionsInFlattenedFormat()
        {
            var testConfig = new ConfigModel
            {
                ConsulEnvironments = new List<ConsulEnvironment>
                {
                    new ConsulEnvironment { Name = "TestEnv1", ConsulUrl = "https://test1.com" },
                    new ConsulEnvironment { Name = "TestEnv2", ConsulUrl = "https://test2.com" }
                },
                OracleEnvironments = new List<OracleEnvironment>
                {
                    new OracleEnvironment { Name = "TestDB", ConnectionString = "test-conn", Schema = "TEST" }
                }
            };

            await _configService.SaveConfigAsync(testConfig);

            var savedContent = await File.ReadAllLinesAsync(_testConfigPath);

            // Check that collections are flattened properly
            savedContent.Should().Contain("consulenvironments[0].name=TestEnv1");
            savedContent.Should().Contain("consulenvironments[0].consulurl=https://test1.com");
            savedContent.Should().Contain("consulenvironments[1].name=TestEnv2");
            savedContent.Should().Contain("consulenvironments[1].consulurl=https://test2.com");
            savedContent.Should().Contain("oracleenvironments[0].name=TestDB");
            savedContent.Should().Contain("oracleenvironments[0].connectionstring=test-conn");
            savedContent.Should().Contain("oracleenvironments[0].schema=TEST");

            // Verify no JSON objects in the file
            savedContent.Should().NotContainMatch(".*\\[.*\\].*=.*\\{.*\\}");
        }

        [Test]
        public async Task LoadConfig_LoadsFlattenedCollectionsCorrectly()
        {
            // Create a properties file with flattened collections
            var propertiesContent = new[]
            {
                "organizationurl=https://test.com",
                "consulenvironments[0].name=TestEnv1",
                "consulenvironments[0].consulurl=https://test1.com",
                "consulenvironments[1].name=TestEnv2",
                "consulenvironments[1].consulurl=https://test2.com",
                "oracleenvironments[0].name=TestDB",
                "oracleenvironments[0].connectionstring=test-conn",
                "oracleenvironments[0].schema=TEST"
            };
            await File.WriteAllLinesAsync(_testConfigPath, propertiesContent);

            // Create new service instance to load the file
            var newService = new ConfigurationService(_testConfigPath);
            var loadedConfig = newService.GetConfig();

            loadedConfig.ConsulEnvironments.Should().HaveCount(2);
            loadedConfig.ConsulEnvironments[0].Name.Should().Be("TestEnv1");
            loadedConfig.ConsulEnvironments[0].ConsulUrl.Should().Be("https://test1.com");
            loadedConfig.ConsulEnvironments[1].Name.Should().Be("TestEnv2");
            loadedConfig.ConsulEnvironments[1].ConsulUrl.Should().Be("https://test2.com");

            loadedConfig.OracleEnvironments.Should().HaveCount(1);
            loadedConfig.OracleEnvironments[0].Name.Should().Be("TestDB");
            loadedConfig.OracleEnvironments[0].ConnectionString.Should().Be("test-conn");
            loadedConfig.OracleEnvironments[0].Schema.Should().Be("TEST");
        }

        [Test]
        public async Task SaveConfigAsync_SavesRouteDomainsAsIndexedEntries()
        {
            var testConfig = new ConfigModel
            {
                RouteDomains = new List<string> { "example.com", "sub.example.com" }
            };

            await _configService.SaveConfigAsync(testConfig);

            var savedContent = await File.ReadAllLinesAsync(_testConfigPath);
            savedContent.Should().Contain("routedomains[0]=example.com");
            savedContent.Should().Contain("routedomains[1]=sub.example.com");
        }

        [Test]
        public async Task LoadConfig_LoadsRouteDomainsCorrectly()
        {
            var propertiesContent = new[]
            {
                "organizationurl=https://test.com",
                "routedomains[0]=example.com",
                "routedomains[1]=sub.example.com"
            };
            await File.WriteAllLinesAsync(_testConfigPath, propertiesContent);

            var newService = new ConfigurationService(_testConfigPath);
            var loadedConfig = newService.GetConfig();
            loadedConfig.RouteDomains.Should().HaveCount(2);
            loadedConfig.RouteDomains[0].Should().Be("example.com");
            loadedConfig.RouteDomains[1].Should().Be("sub.example.com");
        }

        [Test]
        public async Task ImportConfigAsync_ThrowsException_ForInvalidJson()
        {
            var invalidJson = "{ invalid json }";

            await _configService.Invoking(s => s.ImportConfigAsync(invalidJson))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("Formato JSON inválido");
        }

        [Test]
        public async Task ImportConfigAsync_ThrowsException_ForNullConfig()
        {
            var nullConfigJson = "null";

            await _configService.Invoking(s => s.ImportConfigAsync(nullConfigJson))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("Formato de configuração inválido");
        }

        [Test]
        public async Task ImportConfigAsync_ValidatesConfig_BeforeSaving()
        {
            var invalidConfigJson = @"{
                ""organizationUrl"": """",
                ""consulEnvironments"": [{""name"": """", ""consulUrl"": ""https://test.com""}]
            }";

            await _configService.Invoking(s => s.ImportConfigAsync(invalidConfigJson))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("A URL da organização é obrigatória");
        }

        public void Dispose()
        {
            // Clean up the temporary directory after tests
            if (Directory.Exists(Path.GetDirectoryName(_testConfigPath)))
            {
                Directory.Delete(Path.GetDirectoryName(_testConfigPath), true);
            }
        }
    }
}
