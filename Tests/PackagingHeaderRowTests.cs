using Translate;

namespace Tests;

public class PackagingHeaderRowTests
{
    private static string SecondRow(string file) =>
        File.ReadLines($"{GameFileHandling.WorkingDirectory}/Raw/Dumped/GameData/{file}").Skip(1).First();

    // item__* sheets have one header row, so their second row is the first item: restoring it from the dump left it in Chinese.
    [Theory(DisplayName = "A sheet whose second row is a record has one header row")]
    [InlineData("item__艺能.csv")]
    [InlineData("item__装备.csv")]
    [InlineData("Touch__Touch.csv")]
    public void SingleHeaderSheet(string file) => Assert.True(TranslationPackaging.IsDataRow(SecondRow(file)));

    [Theory(DisplayName = "A sheet whose second row names the properties has two header rows")]
    [InlineData("Cook__CookBook.csv")]
    [InlineData("ECA__BuffData.csv")]
    [InlineData("SkillBattle__招式配置.csv")]
    public void TwoHeaderSheet(string file) => Assert.False(TranslationPackaging.IsDataRow(SecondRow(file)));
}
