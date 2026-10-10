using FanslationStudio.LlmKit.Support;
using Translate;

namespace Tests;

public class CharacterLineContextTests
{
    private static TranslationLine Line(string text) => new() { Raw = text, Splits = [new TranslationSplit { Text = text }] };

    [Fact(DisplayName = "CharacterGenders.yaml loads and keeps genders only where every NPC row agrees")]
    public void Table_Loads()
    {
        var characters = CharacterLineContext.Load(GameFileHandling.WorkingDirectory);

        Assert.True(characters.Count > 1000);
        Assert.Equal(LineContext.Female, characters["小钰"]);
        Assert.Equal(LineContext.Male, characters["杜颜白"]);
        // Generic roles seen with both genders are left out rather than guessed.
        Assert.False(characters.ContainsKey("掌柜"));
        Assert.False(characters.ContainsKey("行人"));
    }

    [Fact(DisplayName = "CharacterLineContext gives a line naming a listed character that character's gender")]
    public void Provide_NamedCharacter()
    {
        var named = Line("小钰低着头，不敢看他。");
        var male = Line("杜颜白点了点头。");
        var unlisted = Line("掌柜擦了擦桌子。");
        var lines = new List<TranslationLine> { named, male, unlisted };

        var contexts = CharacterLineContext.Provide(GameFileHandling.WorkingDirectory, new TextFileToSplit { Path = "TaskProcess.json.csv" }, lines);

        // 小钰 is female, but the source states a gender (他), so that line is skipped by the helper on purpose.
        Assert.False(contexts.ContainsKey(named.Splits[0]));
        Assert.Equal(LineContext.Male, contexts[male.Splits[0]].Gender);
        Assert.True(contexts[male.Splits[0]].GenderKnown);
        Assert.False(contexts.ContainsKey(unlisted.Splits[0]));
    }

    [Fact(DisplayName = "CharacterLineContext tells a line naming both genders who is who")]
    public void Provide_BothGenders()
    {
        var both = Line("杜颜白看着小钰。");

        var contexts = CharacterLineContext.Provide(GameFileHandling.WorkingDirectory, new TextFileToSplit { Path = "TaskProcess.json.csv" }, [both]);

        Assert.True(contexts.ContainsKey(both.Splits[0]));
        // Gender is known but there is no single one to check against, so the Gender field stays empty.
        Assert.True(contexts[both.Splits[0]].GenderKnown);
        Assert.Equal(string.Empty, contexts[both.Splits[0]].Gender);
        Assert.Contains("杜颜白", contexts[both.Splits[0]].Prompt);
        Assert.Contains("小钰", contexts[both.Splits[0]].Prompt);
    }
}
