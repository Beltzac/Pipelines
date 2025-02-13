using Common.Repositories.TCP.Interfaces;
using Common.Services;
using Common.Services.Interfaces;
using FluentAssertions;
using Moq;

namespace Tests.Common.Services
{
    public class OracleDiffServiceTests
    {
        [Test]
        public async Task GetViewDiff_ReturnsExpectedDiffResult()
        {
            string view = "view";
            string old = "old";
            string newString = "new";

            var connectionFactoryMock = new Mock<IOracleConnectionFactory>();
            var oracleRepositoryMock = new Mock<IOracleRepository>();
            var result = (new OracleSchemaService(null, null, oracleRepositoryMock.Object)).GetViewDiff(view, old, newString);

            result.Should().NotBeNull();
            result.Key.Should().Be(view);
            result.FormattedDiff.Should().NotBeNullOrEmpty();
            result.HasDifferences.Should().BeTrue();
        }
    }
}
