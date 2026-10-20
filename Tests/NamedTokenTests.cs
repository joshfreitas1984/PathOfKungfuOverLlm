using FanslationStudio.LlmKit.Configuration;
using FanslationStudio.LlmKit.Support;
using FanslationStudio.LlmKit;
using FanslationStudio.LlmKit.Utility;
using Translate;

namespace Tests;

/// <summary>The Config.yaml namedTokenReplacers against the real glossary (offline - no model).</summary>
[Collection("NamedTokens")]
public class NamedTokenTests
{
    // Loading the config is what sets the replacer's named tokens, so every test must load it before masking anything.
    private static readonly Lazy<LlmConfig> LoadedConfig = new(() => ConfigurationExtensions.GetConfiguration(GameFileHandling.WorkingDirectory));

    private readonly LlmConfig Config = LoadedConfig.Value;

    private IEnumerable<string> TitleAlternatives() =>
        Config.Runtime.GlossaryLines
            .Select(l => l.Raw)
            .Where(r => GameFileHandling.GenderAlternativePattern.IsMatch(r) && r != "<他||她>")
            .Where(r => r.Split("||").All(side => side.Trim('<', '>').Length <= 8));

    private static (string Slotted, Dictionary<string, TokenValue> Tokens) Slot(string text) => StringTokenReplacer.Slotter.Slot(text);

    private static string Restore(string slotted, Dictionary<string, TokenValue> tokens) => StringTokenReplacer.Slotter.Restore(slotted, tokens).Text;

    [Theory(DisplayName = "Packaging puts a title before the name it followed in the source")]
    [InlineData("<fName><少侠||姑娘>，你好", "Hello, <fName> <Big Brother||Big Sister>.", "Hello, <Big Brother||Big Sister> <fName>.")]
    [InlineData("fp[大哥||姐姐]来了", "///fp///<Big Brother||Big Sister> is here", "<Big Brother||Big Sister> ///fp/// is here")]
    [InlineData("<fName>说<少侠||姑娘>好", "<fName> told <Big Brother||Big Sister>", "<fName> told <Big Brother||Big Sister>")]
    [InlineData("<fName><少侠||姑娘>", "<fName> <he||she> came", "<fName> <he||she> came")]
    [InlineData("<fName>兄？我爹他在里面！", "<fName> brother? My father is in there!", "Brother <fName>? My father is in there!")]
    [InlineData("<fName>师兄，你来了", "<fName> Senior Brother, you came", "Senior Brother <fName>, you came")]
    [InlineData("p大哥你来了", "///p/// Big Brother, you came", "Big Brother ///p///, you came")]
    [InlineData("<fName>少侠，请", "<fName> Young Hero, please", "Young Hero <fName>, please")]
    [InlineData("<fName>姑娘，请", "<fName> Miss, please", "Miss <fName>, please")]
    [InlineData("<fName>兄的剑", "<fName> Brother's sword", "<fName> Brother's sword")]
    [InlineData("<fName>兄的剑", "<fName> Brother-in-law's sword", "<fName> Brother-in-law's sword")]
    [InlineData("<fName>说兄长来了", "<fName> said Brother came", "<fName> said Brother came")]
    [InlineData("<fName>师兄，你来了", "Senior Brother <fName>, you came", "Senior Brother <fName>, you came")]
    public void PutTitleBeforeName(string raw, string result, string expected) =>
        Assert.Equal(expected, GameFileHandling.PutTitleBeforeName(raw, result));

    [Theory(DisplayName = "Packaging spaces colour tags from adjacent words")]
    [InlineData("captured by<color=#E64D00>///a///</color>, whose", "captured by <color=#E64D00>///a///</color>, whose")]
    [InlineData("<color=#E64D00>///a///</color>While passing", "<color=#E64D00>///a///</color> While passing")]
    [InlineData("Master<color=#E64D00>///a///</color>Wants", "Master <color=#E64D00>///a///</color> Wants")]
    [InlineData("of <color=#E64D00>///p///</color>'s", "of <color=#E64D00>///p///</color>'s")]
    [InlineData("Confirm the cost<color=green>{0}</color>", "Confirm the cost <color=green>{0}</color>")]
    [InlineData("</color>once learned", "</color> once learned")]
    [InlineData("<color=red>Sword</color>s and more", "<color=red>Sword</color>s and more")]
    [InlineData("a\\n<color=green>HP", "a\\n<color=green>HP")]
    public void SpaceColorTags(string result, string expected) =>
        Assert.Equal(expected, GameFileHandling.SpaceColorTags(result));

    [Fact(DisplayName = "Every title alternative is slotted and restores to its glossary result")]
    public void TitleAlternatives_RoundTripThroughGlossary()
    {
        var titles = TitleAlternatives().ToList();
        Assert.True(titles.Count > 70, $"only {titles.Count} title alternatives found");

        foreach (var raw in titles)
        {
            var (slotted, tokens) = Slot($"{raw}，好");

            Assert.True(slotted.StartsWith("{playerTitle}"), $"{raw} was not slotted: {slotted}");
            Assert.Equal(raw, tokens["playerTitle"].Source);
            var expected = Config.Runtime.GlossaryLines.First(l => l.Raw == raw).Result;
            Assert.Equal($"{expected}, ok", Restore("{playerTitle}, ok", tokens));
            Assert.Null(StringTokenReplacer.Slotter.FindSlotProblem(slotted, "{playerTitle}, ok"));
        }
    }

    [Fact(DisplayName = "A pronoun alternative stays visible for the model to inflect")]
    public void PronounAlternative_IsNotSlotted()
    {
        Assert.Equal("我要杀<他||她>", Slot("我要杀<他||她>").Slotted);
    }

    [Fact(DisplayName = "Name tokens are slotted under their names and the prompt describes them")]
    public void NameTokens_AreSlotted()
    {
        var (slotted, tokens) = Slot("<fName>，<lName>，<name>，<faction>");

        Assert.Equal("{firstName}，{lastName}，{fullName}，{faction}", slotted);
        Assert.Equal("<fName>，<lName>，<name>，<faction>", Restore(slotted, tokens));
        Assert.Contains("player's first name", StringTokenReplacer.DescribeNamedTokensIn(slotted));
    }

    [Fact(DisplayName = "Gang and assassin task tokens are slotted under their names and restore as written")]
    public void TaskTokens_AreSlotted()
    {
        const string raw = "速去将npcName击杀，交付itemName，其在<city>（<x>，<y>），是你的<relationship>。";
        var (slotted, tokens) = Slot(raw);

        Assert.Equal("速去将{taskNpcName}击杀，交付{taskItemName}，其在{cityName}（{mapPositionX}，{mapPositionY}），是你的{relationship}。", slotted);
        Assert.Equal(raw, Restore(slotted, tokens));
    }

    [Theory(DisplayName = "NPC text tokens are masked as named slots and restored in the game's ///token/// form")]
    [InlineData("咱们gangName在这mapName，可是大大有名。", "咱们{gangName}在这{mapName},可是大大有名。", "Our ///gangName/// is famous in ///mapName///.")]
    [InlineData("致使a内息受阻，全身乏力。", "致使{npcName}内息受阻,全身乏力。", "///a/// has blocked qi.")]
    [InlineData("在文昌镇外，随时等候p大人。", "在文昌镇外,随时等候{playerName}大人。", "Waiting outside, ///p///.")]
    [InlineData("lp，你我既是知己好友！", "{playerLastName},你我既是知己好友!", "///lp///, we are friends!")]
    [InlineData("<color=#B08200>i1</color>而不<color=#B08200>i2</color>", "<color=0>{randomItemOne}</color>而不<color=1>{randomItemTwo}</color>", "<color=#B08200>///i1///</color> not <color=#B08200>///i2///</color>")]
    [InlineData("得到《i》", "得到《{randomItem}》", "Got ///i///")]
    [InlineData("盗取了物品<color=#B08200>i*2</color>，", "盗取了物品<color=0>{randomItem}*{0}</color>,", "Stole <color=#B08200>///i///*2</color>,")]
    public void NpcTokens_RoundTrip(string raw, string expectedMasked, string modelOutput)
    {
        var replacer = new StringTokenReplacer();
        var (slotted, tokens) = Slot(raw);
        var masked = LineValidation.PrepareRaw(slotted, replacer);

        Assert.Equal(expectedMasked, masked);

        // The model's output is written with the slots; restoring gives the game's form with every token present.
        var translated = System.Text.RegularExpressions.Regex.Replace(modelOutput, "///([A-Za-z0-9]+)///", m => "{" + SlotFor(m.Groups[1].Value) + "}");
        var restored = Restore(replacer.Restore(translated), tokens);

        Assert.Equal(modelOutput, restored);
        Assert.Null(StringTokenReplacer.Slotter.FindSlotProblem(slotted, translated));
    }

    private static string SlotFor(string token) => token switch
    {
        "gangName" => "gangName", "mapName" => "mapName", "a" => "npcName", "p" => "playerName", "lp" => "playerLastName",
        "i1" => "randomItemOne", "i2" => "randomItemTwo", "i" => "randomItem", _ => throw new ArgumentException(token),
    };

    [Fact(DisplayName = "A first name followed by a bracket title is two slots and restores to the game's forms")]
    public void FirstNameAndBracketTitle()
    {
        var (slotted, tokens) = Slot("嗯……fp[大哥||姐姐]若要小玥同行，便来临川村寻我。");
        var masked = LineValidation.PrepareRaw(slotted, new StringTokenReplacer());

        Assert.Contains("{playerFirstName}{playerTitle}", masked);
        Assert.Equal("Hm... ///fp/// <Big Brother||Big Sister>, find me.", Restore("Hm... {playerFirstName} {playerTitle}, find me.", tokens));
    }

    [Fact(DisplayName = "Every letter token, word token and bracket title in the NPC dump is masked, and the splitter keeps them in the sentence")]
    public void NpcDump_HasNoUnmaskedTokens()
    {
        var files = new[] { "NPC__Dialog.csv", "NPC__NpcDialog.csv", "NPC__NpcDialogLips.csv", "NPC__PlayerAction.csv", "NPC__SelfAction.csv", "NPC__proAction.csv" };
        var dump = Path.Combine(GameFileHandling.WorkingDirectory, "Raw", "Dumped", "GameData");
        var cjk = new System.Text.RegularExpressions.Regex(@"[㐀-鿿]");
        var tokens = 0;

        foreach (var file in files)
        {
            foreach (var line in File.ReadLines(Path.Combine(dump, file)).Skip(2))
            {
                foreach (var cell in CompoundFieldSplitter.ParseCsvRow(line).Where(c => cjk.IsMatch(c)))
                {
                    var wanted = GameFileHandling.WordTokenPattern.Matches(cell).Count + GameFileHandling.CodeLetterTokenPattern.Matches(cell).Count
                        + GameFileHandling.BracketAlternativePattern.Matches(cell).Count;
                    if (wanted == 0) continue;

                    // Slots like {gangName} contain the word itself; only what is left outside them counts.
                    var masked = System.Text.RegularExpressions.Regex.Replace(LineValidation.PrepareRaw(Slot(cell).Slotted, new StringTokenReplacer()), "{[A-Za-z0-9]+}", "|");
                    Assert.False(GameFileHandling.WordTokenPattern.IsMatch(masked), $"{file}: word token left in {masked}");
                    Assert.False(GameFileHandling.CodeLetterTokenPattern.IsMatch(masked), $"{file}: letter token left in {masked}");
                    Assert.False(GameFileHandling.BracketAlternativePattern.IsMatch(masked), $"{file}: bracket title left in {masked}");
                    tokens += wanted;
                }
            }
        }

        Assert.True(tokens > 4500, $"only {tokens} tokens found");
    }

    [Theory(DisplayName = "The splitter keeps a token inside its sentence as one fragment")]
    [InlineData("咱们gangName在这mapName，可是大大有名。", "咱们gangName在这mapName，可是大大有名。")]
    [InlineData("致使a内息受阻，全身乏力。", "致使a内息受阻，全身乏力。")]
    [InlineData("嗯……fp[大哥||姐姐]若要小玥同行，便来临川村寻我。", "嗯……fp[大哥||姐姐]若要小玥同行，便来临川村寻我。")]
    public void Splitter_KeepsTokensInTheSentence(string cell, string expectedFragment)
    {
        var (_, fragments) = CompoundFieldSplitter.Decompose(cell, GameFileHandling.SplitterOptions);

        Assert.Equal([expectedFragment], fragments);
    }

    [Fact(DisplayName = "Config.yaml's token patterns and the splitter's placeholder patterns are the same")]
    public void ConfigAndSplitterPatterns_Agree()
    {
        var entries = Config.NamedTokenReplacers;
        var letters = new[] { "fp", "lp", "p", "a", "b", "i1", "i2", "i" };
        var letterTemplate = GameFileHandling.CodeLetterTokenPattern.ToString();

        foreach (var letter in letters)
            Assert.Contains(entries, e => e.Pattern == letterTemplate.Replace("(?:fp|lp|p|a|b|i1|i2|i)", letter) && e.RestoreAs == $"///{letter}///");

        Assert.Contains(entries, e => e.Pattern == GameFileHandling.BracketAlternativePattern.ToString() && e.RestoreFromGlossary);

        var words = GameFileHandling.WordTokenPattern.ToString().Split('|');
        foreach (var word in words)
            Assert.Contains(entries, e => e.Literal == word && e.RestoreAs == $"///{word}///");
    }

    [Fact(DisplayName = "An extra name token or gender alternative in a translation fails validation")]
    public void ExtraTokens_FailValidation()
    {
        var file = new FanslationStudio.LlmKit.Support.TextFileToSplit { IgnoreHtmlTagsInText = true };

        Assert.Null(Validate("<fName>你好", "Hello <fName>"));
        Assert.NotNull(Validate("<fName>你好", "Hello <fName> <fName>"));
        Assert.Null(Validate("<少侠||姑娘>你好", "<Young Hero||Miss> hello"));
        Assert.NotNull(Validate("<少侠||姑娘>你好", "<Young Hero||Miss> <Brother||Sister> hello"));

        string? Validate(string raw, string result) => GameFileHandling.Hooks.CustomColumnValidator!(file, null, raw, result);
    }
}
