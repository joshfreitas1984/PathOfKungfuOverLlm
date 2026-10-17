using FanslationStudio.LlmKit.Utility;
using SharedAssembly.DynamicStrings;
using Translate;

namespace Tests;

public class GameEnglishContractTests
{
    private static DynamicStringContract Contract(string type, string method, string raw, string translation) =>
        new() { Type = type, Method = method, ILOffset = 12, Raw = raw, Translation = translation, Parameters = ["System.Int32"] };

    [Fact(DisplayName = "A packaged Chinese literal gets a contract for the game's English literal with the same translation")]
    public void AddsEnglishContract()
    {
        var contracts = new List<DynamicStringContract> { Contract("UIShop", "Open", "更多，详情", "More") };
        var rows = new[] { ("UIShop", "Open", "More Info", "更多,详情") };

        var added = TranslationPackaging.AddGameEnglishContracts(contracts, rows);

        var contract = Assert.Single(added);
        Assert.Equal("More Info", contract.Raw);
        Assert.Equal("More", contract.Translation);
        Assert.Equal(12, contract.ILOffset);
        Assert.Equal(["System.Int32"], contract.Parameters);
    }

    [Fact(DisplayName = "A row whose Chinese literal was not packaged adds nothing, and a row is added only once")]
    public void SkipsUnpackagedAndDuplicates()
    {
        var contracts = new List<DynamicStringContract> { Contract("UIShop", "Open", "更多", "More") };
        var rows = new[] { ("UIShop", "Open", "Other", "离开"), ("UIShop", "Open", "More Info", "更多"), ("UIShop", "Open", "More Info", "更多") };

        Assert.Equal(["More Info"], TranslationPackaging.AddGameEnglishContracts(contracts, rows).Select(c => c.Raw));
    }

    // A row must still name a Chinese literal in the dump, or a game update has made it stale: re-run Files/ScanGameEnglishLiterals.py.
    [Fact(DisplayName = "Game English table matches the dynamic strings dump")]
    public void TableMatchesDump()
    {
        var dump = File.ReadLines($"{GameFileHandling.WorkingDirectory}/Raw/Dumped/DynamicStrings/dynamicStrings.txt")
            .Select(l => l.Split(',')).Select(f => (f[0], f[1], f[3])).ToHashSet();

        var stale = File.ReadLines($"{GameFileHandling.WorkingDirectory}/{TranslationPackaging.GameEnglishTable}").Skip(1).Where(l => l.Length > 0)
            .Select(CompoundFieldSplitter.ParseCsvRow).Where(f => !dump.Contains((f[0], f[1], f[3].Replace(',', '，')))).ToList();

        Assert.Empty(stale);
    }

    [Fact(DisplayName = "A literal that starts with the </color> closing a tag from the previous fragment keeps it in the translation")]
    public void RestoresLeadingColorClose()
    {
        var contracts = new List<DynamicStringContract>
        {
            Contract("UIMain", "A", "</color><color=red>等级</color>", "<color=red>Level</color>"),
            Contract("UIMain", "B", "</color>学会", "</color>learn"),
            Contract("UIMain", "C", "<color=green>外功攻击+", "<color=green>External Attack+"),
        };

        var fixedCount = TranslationPackaging.RestoreColorCloseTags(contracts);

        Assert.Equal(1, fixedCount);
        Assert.Equal("</color><color=red>Level</color>", contracts[0].Translation);
        Assert.Equal("</color>learn", contracts[1].Translation);
        Assert.Equal("<color=green>External Attack+", contracts[2].Translation);
    }
}
