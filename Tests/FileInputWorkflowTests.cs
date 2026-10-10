using FanslationStudio.LlmKit;
using Translate;

namespace Tests;

public class FileInputWorkflowTests
{
    // Reads Files/Raw/Dumped/GameData and writes Files/Raw/Export; seeds Files/Converted only for files that have none yet.
    [Fact(DisplayName = "1. ExportAssetsIntoTranslated")]
    public void ExportAssetsIntoTranslated()
    {
        TranslationExport.ExportGameSpecificTextAssetsToCustomFormat(GameFileHandling.WorkingDirectory);
    }

    [Fact(DisplayName = "99. MergeFilesIntoTranslated")]
    public async Task MergeFilesIntoTranslated()
    {
        await GameFileHandlingBase.MergeFilesIntoTranslatedAsync(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit);
    }

    [Fact(DisplayName = "999. Check File Lines Match")]
    public void CheckFileLinesMatch()
    {
        var badFiles = GameFileHandlingBase.CheckFileLinesMatch(GameFileHandling.WorkingDirectory, TextFileConfiguration.TextFilesToSplit);
        Assert.Empty(badFiles);
    }
}
