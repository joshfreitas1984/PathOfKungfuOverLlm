using FanslationStudio.LlmKit.Workflow;
using Translate;

namespace Tests;

public class TranslationWorkflowTests
{
    // Small files used to check models, glossary and prompts before a real run. Needs Ollama with the Config.yaml models.
    private static readonly string[] SampleFiles = ["地图表__MapConfig.csv", "item__药品.csv", "item__食物.csv", "SandBox_NpcFromTouch__Touch.json.csv"];

    [Fact(DisplayName = "0. Reset All Flags")]
    public async Task ResetAllFlags()
    {
        await TranslationWorkflow.ResetAllFlags(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit);
    }

    [Fact(DisplayName = "1. Translate sample files")]
    public async Task TranslateSample()
    {
        var sample = TextFileConfiguration.TextFilesToSplit.Where(f => SampleFiles.Contains(f.Path)).ToArray();
        Assert.Equal(SampleFiles.Length, sample.Length);
        await TranslationWorkflow.TranslateLines(GameFileHandling.WorkingDirectory, sample, GameFileHandling.Hooks);
    }

    [Fact(DisplayName = "2. TranslateLinesBruteForce")]
    public async Task TranslateLinesBruteForce()
    {
        await TranslationWorkflow.TranslateLinesBruteForce(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit, GameFileHandling.Hooks);
        await FileOutputWorkflowTests.PackageFinalTranslation();
    }

    [Fact(DisplayName = "3. ApplyRulesToCurrentTranslation")]
    public async Task ApplyRulesToCurrentTranslation()
    {
        await TranslationWorkflow.ApplyAllRulesToCurrentTranslation(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit, GameFileHandling.Hooks);
    }
}
