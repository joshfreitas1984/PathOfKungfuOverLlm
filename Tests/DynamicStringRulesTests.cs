using Translate;

namespace Tests;

public class DynamicStringRulesTests
{
    // Every row of the do-not-translate table must still name a literal in the dump, or a game update has made it stale.
    [Fact(DisplayName = "Do-not-translate table matches the dynamic strings dump")]
    public void TableMatchesDump()
    {
        var dump = File.ReadLines($"{GameFileHandling.WorkingDirectory}/Raw/Dumped/DynamicStrings/dynamicStrings.txt")
            .Select(l => l.Split(',')).Select(f => (f[0], f[1], f[3])).ToHashSet();

        var stale = DynamicStringRules.Load(GameFileHandling.WorkingDirectory).Keys.Where(k => !dump.Contains(k)).ToList();
        Assert.Empty(stale);
    }
}
