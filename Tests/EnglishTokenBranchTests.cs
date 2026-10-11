using PathOfKungfuPlugin;

namespace Tests;

public class EnglishTokenBranchTests
{
    [Theory(DisplayName = "ConvertText is forced onto the English branch only for English text in Chinese mode")]
    [InlineData(false, "Our ///gangName/// is famous", true)]
    [InlineData(false, "///lp///, you are welcome.", true)]
    [InlineData(false, "咱们gangName在这mapName", false)]
    [InlineData(false, "///a///与///p///郎情妾意", false)]
    [InlineData(true, "Our ///gangName/// is famous", false)]
    [InlineData(false, "", false)]
    [InlineData(false, null, false)]
    public void ShouldForce(bool wordManagerOpen, string? text, bool expected) =>
        Assert.Equal(expected, EnglishTokenBranch.ShouldForce(wordManagerOpen, text));
}
