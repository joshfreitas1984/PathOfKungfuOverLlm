using FanslationStudio.LlmKit.Support;
using FanslationStudio.LlmKit.Workflow;

namespace Translate;

/// <summary>Packages translated files into the final Files/Mod output.</summary>
public static class TranslationPackaging
{
    public static async Task PackageFinalTranslationAsync(string workingDirectory, TextFileToSplit[] textFiles)
    {
        var outputPath = $"{workingDirectory}/Mod";

        if (Directory.Exists(outputPath))
            Directory.Delete(outputPath, true);

        Directory.CreateDirectory(outputPath);

        var passedCount = 0;
        var qcRejectedCount = 0;
        var rawFallbackCount = 0;

        foreach (var textFile in textFiles.Where(t => t.PackageOutput))
        {
            var (passed, qcRejected, rawFallback) = textFile.TextFileType switch
            {
                TextFileType.RawCsv => await CsvGameDataWorkflow.PackageAsync(workingDirectory, textFile),
                TextFileType.PrefabText => await PrefabTextWorkflow.PackagePrefabTextAsync(workingDirectory, textFile),
                _ => (0, 0, 0),
            };

            passedCount += passed;
            qcRejectedCount += qcRejected;
            rawFallbackCount += rawFallback;
        }

        Console.WriteLine($"Passed: {passedCount}");
        Console.WriteLine($"QC failures: {qcRejectedCount}");
        Console.WriteLine($"Fell back to raw: {rawFallbackCount}");
    }
}
