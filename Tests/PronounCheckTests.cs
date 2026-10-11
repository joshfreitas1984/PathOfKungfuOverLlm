using FanslationStudio.LlmKit.Support;
using FanslationStudio.LlmKit.Workflow;
using Translate;

namespace Tests;

public class PronounCheckTests
{
    private static string? Category(string raw, string translated) =>
        PronounDefectWorkflow.Classify(new TranslationSplit { Text = raw, Translated = translated }, GameFileHandling.Hooks, null, false).Category;

    // Real lines from the first translation run: the pronoun alternative <他||她> becomes <him||her>, and must not read as an invented gender.
    [Theory(DisplayName = "The pronoun check leaves <him||her> style gender tokens alone")]
    [InlineData("不能让<他||她>活着！", "Can't let <him||her> live!")]
    [InlineData("寨主，<他||她>？", "Stronghold Master, <he||she>?")]
    [InlineData("若不服气，同<他||她>过两招便是，这有何难？", "If you're not satisfied, just spar with <him||her> for a few rounds. How hard can it be?")]
    [InlineData("二爷所言果然不错，将<他||她>拿下了！", "Second Master was right; we've got <him||her>!")]
    public void PronounAlternativesAreNotInventedGenders(string raw, string translated) =>
        Assert.Null(Category(raw, translated));

    // A title alternative reaches the model as a {playerTitle} slot and comes back as the glossary pair, which has no pronoun in it.
    [Fact(DisplayName = "The pronoun check leaves a restored <Young Hero||Miss> title alone")]
    public void TitleAlternativesAreNotInventedGenders() =>
        Assert.Null(Category("<少侠||姑娘>，请留步。", "<Young Hero||Miss>, please wait."));

    [Fact(DisplayName = "The pronoun check still flags a he/she invented for the player")]
    public void StillCatchesAnInventedGender() =>
        Assert.Equal("InventedGender", Category("<fName>，你来了。", "<fName>, you came, and he looks tired."));
}
