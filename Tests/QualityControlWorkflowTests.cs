using FanslationStudio.LlmKit.Support;
using FanslationStudio.LlmKit.Utility;
using FanslationStudio.LlmKit.Workflow;
using Translate;

namespace Tests;

// QC pass: needs Ollama with the Config.yaml qualityControl model. All of these are a no-op if qualityControl.enabled is false.
// Loop: sample run -> full run -> find flagged -> triage -> fix prompts -> fix by hand -> reset -> re-run.
// Reference: FanslationStudio.LlmKit docs/features/translation-pipeline/quality-control-pass.md
public class QualityControlWorkflowTests
{
    // Reviews a small random sample instead of every column, to judge speed, score spread and correction quality first.
    [Fact(DisplayName = "1. RunQualityControlPassSample")]
    public async Task RunQualityControlPassSample()
    {
        await QualityControlWorkflow.RunAsync(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit, sampleSize: 300, hooks: GameFileHandling.Hooks);
    }

    // Full pass. Columns already reviewed and unchanged are skipped, so it is safe to stop and resume.
    [Fact(DisplayName = "2. RunQualityControlPass")]
    public async Task RunQualityControlPass()
    {
        await QualityControlWorkflow.RunAsync(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit, hooks: GameFileHandling.Hooks);
        await FileOutputWorkflowTests.PackageFinalTranslation();
    }

    // Run after a glossary or config change so stored corrections pick up the same rule changes as Translated.
    [Fact(DisplayName = "3. ApplyRulesToQCReview")]
    public async Task ApplyRulesToQCReview()
    {
        await QualityControlWorkflow.ApplyRulesToCurrentQcTranslated(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit, GameFileHandling.Hooks);
    }

    [Fact(DisplayName = "4. Find Flagged Quality Control Items")]
    public async Task FindFlaggedQcReviews()
    {
        var workingDirectory = GameFileHandling.WorkingDirectory;
        var flagged = await QualityControlWorkflow.GetFlaggedQcReviews(workingDirectory, TextFileConfiguration.TextFilesToSplit);
        FileHelper.WriteAllTextWithRetry($"{workingDirectory}/TestResults/FlaggedQcReviews.yaml", YamlHelper.CreateSerializer().Serialize(flagged));
    }

    // Writes TestResults/QcTriage*.yaml: reason clusters, a low-score sample and a sample per DEFECT category. No LLM calls.
    [Fact(DisplayName = "5. Triage Flagged Quality Control Items")]
    public async Task TriageFlaggedQcReviews()
    {
        await QualityControlWorkflow.WriteTriageReportAsync(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit, GameFileHandling.Hooks);
    }

    // Writes TestResults/QcTriagePrompts.md, one section per cluster, to paste into a chat. No LLM calls.
    [Fact(DisplayName = "6. Generate Quality Control Fix Prompts")]
    public async Task GenerateQcFixPrompts()
    {
        await QualityControlWorkflow.WriteFixPromptsAsync(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit, GameFileHandling.Hooks);
    }

    // After fixing whatever caused a persistent rule violation, so columns parked at FailedValidation get retried.
    [Fact(DisplayName = "7. Reset Qc Retry Limits")]
    public async Task ResetQcRetryLimits()
    {
        await QualityControlWorkflow.ResetQcRetryLimits(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit);
    }

    [Fact(DisplayName = "Reset Leaked Quality Control Corrections")]
    public async Task ResetLeakedQcCorrections()
    {
        await QualityControlWorkflow.ResetLeakedQcCorrections(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit);
    }

    // After changing the scoring rubric or the QC model: re-reviews only columns below minAcceptableScore.
    [Fact(DisplayName = "7. Reset Low-Score Quality Control State")]
    public async Task ResetLowScoreQcState()
    {
        await QualityControlWorkflow.ResetLowScoreQcState(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit, GameFileHandling.Hooks);
    }

    // After changing autoAcceptDefectCategories: re-reviews flagged columns whose category is not auto-accepted.
    [Fact(DisplayName = "8. Reset Non-Auto-Accepted Quality Control State")]
    public async Task ResetNonAutoAcceptedQcState()
    {
        await QualityControlWorkflow.ResetNonAutoAcceptedQcState(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit, GameFileHandling.Hooks);
    }
}
