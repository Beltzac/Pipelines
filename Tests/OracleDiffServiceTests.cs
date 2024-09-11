using Xunit;

namespace Common.Tests
{
    public class OracleDiffServiceTests
    {
        [Fact]
        public void GetDiff_ReturnsExpectedPatchResult()
        {
            string view = "view";
            string old = "old";
            string newString = "new";

            var result = OracleDiffService.GetDiff(view, old, newString);

            Assert.NotNull(result);
            Assert.NotEmpty(result.Hunks);
            Assert.All(result.Hunks, diff => 
            {
                Assert.NotEmpty(diff.lines);
                Assert.NotEmpty(diff.lines);
            });
        }
    }
}
