using FanslationStudio.LlmKit.Support;
using FanslationStudio.LlmKit.Workflow;

namespace Translate;

/// <summary>Exports dumped game files into the custom translation format ahead of LLM translation.</summary>
public static class TranslationExport
{
    public static void ExportGameSpecificTextAssetsToCustomFormat(string workingDirectory)
    {
        foreach (var textFile in TextFileConfiguration.TextFilesToSplit.Where(t => t.TextFileType == TextFileType.RawCsv))
            CsvGameDataWorkflow.ExportToCustomFormat(workingDirectory, textFile, GameFileHandling.SplitterOptions);
    }
}
