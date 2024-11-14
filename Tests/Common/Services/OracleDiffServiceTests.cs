using Common.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.Services.Common;

namespace Tests.Common.Services
{
    public class OracleDiffServiceTests
    {
        [Test]
        public void GetDiff_ReturnsExpectedPatchResult()
        {
            string view = "view";
            string old = "old";
            string newString = "new";

            var result = OracleDiffUtils.GetDiff(view, old, newString);

            result.Should().NotBeNull();
            result.Hunks.Should().NotBeEmpty();
            result.Hunks.ForEach(diff =>
            {
                diff.lines.Should().NotBeEmpty();
                diff.lines.Should().NotBeEmpty();
            });
        }
    }
}
