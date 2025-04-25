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
            var result = (new OracleSchemaService(null, null, oracleRepositoryMock.Object)).GetViewDiffAsync(view, old, newString);

            result.Should().NotBeNull();
            (await result).Key.Should().Be(view);
            (await result).FormattedDiff.Should().NotBeNullOrEmpty();
            (await result).HasDifferences.Should().BeTrue();
        }
    }
}
