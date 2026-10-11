using FanslationStudio.LlmKit.Utility;
using Translate;

namespace Tests;

// Read-only: checks the generated pinyin pool (Files/NamePools) against the Chinese pool it was built from.
public class NamePoolTests
{
    private static string[][] Load(string path, int skip) =>
        File.ReadLines(path).Skip(skip).Select(CompoundFieldSplitter.ParseCsvRow).ToArray();

    [Fact(DisplayName = "NamePools: name2 is an ASCII pinyin copy of the Chinese pool with the same IDs and blanks")]
    public void Name2_MatchesChinesePool()
    {
        var chinese = Load($"{GameFileHandling.WorkingDirectory}/Raw/Dumped/GameData/WorldEvolution__name.csv", 2);
        var pinyin = Load($"{GameFileHandling.WorkingDirectory}/NamePools/WorldEvolution__name2.csv", 1);

        Assert.Equal(chinese.Length, pinyin.Length);
        for (var i = 0; i < chinese.Length; i++)
        {
            Assert.Equal(chinese[i][1], pinyin[i][0]);
            for (var column = 0; column < 3; column++)
            {
                var source = chinese[i][column + 2];
                var result = pinyin[i][column + 1];
                Assert.Equal(string.IsNullOrWhiteSpace(source), string.IsNullOrWhiteSpace(result));
                Assert.All(result, c => Assert.True(c < 128, $"Row {i}: non-ASCII in '{result}'"));
            }
        }

        // The game takes the first N rows by ID where N is the non-blank count, so each column must be gap-free.
        for (var column = 1; column <= 3; column++)
        {
            var filled = pinyin.Select(r => r[column]).Where(c => c.Length > 0).Count();
            Assert.DoesNotContain(pinyin.Take(filled), r => r[column].Length == 0);
        }
    }

    [Fact(DisplayName = "NamePools: surnames use surname readings and carry no space (the plugin adds it)")]
    public void Name2_SurnameReadings()
    {
        var pinyin = Load($"{GameFileHandling.WorkingDirectory}/NamePools/WorldEvolution__name2.csv", 1);
        var surnames = pinyin.Select(r => r[1]).Where(s => s.Length > 0).ToArray();

        Assert.All(surnames, s => Assert.Equal(s.Trim(), s));
        Assert.Equal("Zhao", pinyin[0][1]);
        Assert.Contains("Yue", surnames);       // 乐, not Le
        Assert.Contains("Shan", surnames);      // 单, not Dan
        Assert.Contains("Gongye", surnames);    // 公冶
        Assert.Contains("Lyu", surnames);       // 吕
    }

    [Fact(DisplayName = "NamePools: the Western snapshot used to convert old saves matches the game's pool and shares no name with the pinyin pool")]
    public void WesternSnapshot_MatchesGamePool()
    {
        var game = File.ReadAllText($"{GameFileHandling.WorkingDirectory}/Raw/Dumped/GameData/WorldEvolution__name2.csv");
        var snapshot = File.ReadAllText($"{GameFileHandling.WorkingDirectory}/NamePools/WorldEvolution__name2.western.csv");
        Assert.Equal(game, snapshot);

        // A converted NPC must not match the Western pool again.
        var western = Load($"{GameFileHandling.WorkingDirectory}/NamePools/WorldEvolution__name2.western.csv", 2)
            .SelectMany(r => r.Skip(2)).Where(c => c.Length > 0).ToHashSet();
        var pinyin = Load($"{GameFileHandling.WorkingDirectory}/NamePools/WorldEvolution__name2.csv", 1)
            .SelectMany(r => r.Skip(1)).Where(c => c.Length > 0);
        Assert.DoesNotContain(pinyin, western.Contains);
    }
}
