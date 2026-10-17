using FanslationStudio.LlmKit.Utility;
using Translate;

namespace Tests;

// The detector lives in FanslationStudio.LlmKit (Utility/PersonShiftDetector.cs, with its own tests); these are this game's lines and its report.
public class PersonShiftTests
{
    // Real lines from Files/Converted: the wife NPC 81000052002 says 夫君 for her own husband.
    [Theory(DisplayName = "Person shift: this game's known mistranslations are caught")]
    [InlineData("夫君曾有一老师，乃是朝廷清流重臣，夫君对他颇为尊崇，然此僚人面兽心。", "Your husband once had a teacher who was a high-ranking official. Your husband held him in great respect.", PersonShiftDetector.SpouseTermAsYours)]
    [InlineData("被你爹一直囚在这里，不人不鬼过了这些年。", "After all these years, locked up here by my father.", PersonShiftDetector.SecondPersonLost)]
    [InlineData("“……你勾结螣蛇蛊主意欲何为？”", "It seems there was an instruction to correct a previous output, but no specific previous output was given.", PersonShiftDetector.ModelNarration)]
    public void CatchesShift(string raw, string translated, string expected) =>
        Assert.Equal(expected, PersonShiftDetector.Find(raw, translated));

    // Read-only: writes TestResults/PersonShifts.txt, one entry per flagged split. Candidates for review, never a failure - the same line
    // can be right when the speaker is the other person. No LLM calls.
    [Fact(DisplayName = "Person shift: list candidates in Files/Converted")]
    public async Task ListCandidates()
    {
        var workingDirectory = GameFileHandling.WorkingDirectory;
        var candidates = await PersonShiftDetector.FindCandidatesAsync(workingDirectory, TextFileConfiguration.TextFilesToSplit);

        Directory.CreateDirectory($"{workingDirectory}/TestResults");
        File.WriteAllText($"{workingDirectory}/TestResults/PersonShifts.txt", PersonShiftDetector.FormatReport(candidates));
    }
}
