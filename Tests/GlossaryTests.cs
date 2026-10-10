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

        // Reviewed martial art and move names are applied as exact whole-cell translations before the LLM.
        var manual = config.Runtime.ManualTranslations;
        Assert.True(manual.Count > 1200);
        Assert.Equal("Embrace the Origin, Hold to the One", manual.First(m => m.Raw == "抱元守一").Result);
        Assert.Equal("Vast Sea Wave-Watching Blade Art", manual.First(m => m.Raw == "沧海观澜刀法").Result);
        Assert.Equal("Dark Steel Sword", manual.First(m => m.Raw == "玄铁剑").Result);
    }
}
