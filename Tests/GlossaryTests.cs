using FanslationStudio.LlmKit.Configuration;
using Translate;

namespace Tests;

// Read-only: loads Config.yaml and every file in Files/Glossary the way a translation run does.
public class GlossaryTests
{
    [Fact(DisplayName = "Glossary: every file under Files/Glossary is loaded, and workspace entries win over the preset")]
    public void WorkspaceGlossary_Loads()
    {
        var config = ConfigurationExtensions.GetConfiguration(GameFileHandling.WorkingDirectory);
        var lines = config.Runtime.GlossaryLines;

        string? Result(string raw) => lines.LastOrDefault(l => l.Raw == raw)?.Result;

        Assert.True(lines.Count > 900);
        Assert.Equal("Taihe Sect", Result("太和派"));
        Assert.Equal("Lianhuan Stronghold", Result("连环寨"));
        Assert.Equal("Max Stamina", Result("体力上限"));
        Assert.Equal("Stronghold Master", Result("寨主")); // overrides the preset's "Fort Master"
        Assert.Single(lines, l => l.Raw == "寨主");

        // Manual translations are exact whole-cell overrides applied before the LLM. How many there are changes with every review,
        // so only check that they load and that no raw text is listed twice (the later one would silently lose).
        var manual = config.Runtime.ManualTranslations;
        Assert.NotEmpty(manual);
        Assert.All(manual, m => Assert.False(string.IsNullOrWhiteSpace(m.Result), $"Manual translation for {m.Raw} has no result"));
        Assert.Empty(manual.GroupBy(m => m.Raw).Where(g => g.Count() > 1).Select(g => g.Key));
    }
}
