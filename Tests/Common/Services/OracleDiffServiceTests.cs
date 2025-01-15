using Common.ExternalApis;
using Common.Models;
using Common.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.Services.Common;

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

            var result = await (new OracleSchemaService(null, null)).GetViewDiff(view, old, newString);

            result.Should().NotBeNull();
            result.ViewName.Should().Be(view);
            result.FormattedDiff.Should().NotBeNullOrEmpty();
            result.HasDifferences.Should().BeTrue();
        }
    }
}
