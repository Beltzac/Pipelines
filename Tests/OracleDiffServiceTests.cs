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
            Assert.NotEmpty(result.Diffs);
            Assert.All(result.Diffs, diff => 
            {
                Assert.NotNull(diff.OldValue);
                Assert.NotNull(diff.NewValue);
            });
        }
    }
}
