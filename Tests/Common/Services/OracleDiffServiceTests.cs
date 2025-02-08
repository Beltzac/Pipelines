using Common.Services;
using FluentAssertions;

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

            var result = (new OracleSchemaService(null, null)).GetViewDiff(view, old, newString);

            result.Should().NotBeNull();
            result.Key.Should().Be(view);
            result.FormattedDiff.Should().NotBeNullOrEmpty();
            result.HasDifferences.Should().BeTrue();
        }
    }
}
