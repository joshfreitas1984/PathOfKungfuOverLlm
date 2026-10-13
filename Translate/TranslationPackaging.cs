using System.Text.Json;
using FanslationStudio.LlmKit.Support;
using FanslationStudio.LlmKit.Utility;
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
                TextFileType.DynamicStrings => await DynamicStringsCecilWorkflow.PackageDynamicStringsCecilAsync(workingDirectory, textFile),
                _ => (0, 0, 0),
            };

            passedCount += passed;
            qcRejectedCount += qcRejected;
            rawFallbackCount += rawFallback;
        }

        RestoreHeaderRows(workingDirectory, outputPath, textFiles);
        WriteTranslatedColumns(outputPath, textFiles);

        Console.WriteLine($"Passed: {passedCount}");
        Console.WriteLine($"QC failures: {qcRejectedCount}");
        Console.WriteLine($"Fell back to raw: {rawFallbackCount}");
    }

    /// <summary>
    /// The first two rows of a sheet are headers (Chinese labels, then the property names the game maps columns by), but they
    /// are translated like any other row of a translated column: 地图名称 became "Map Name", and the game would no longer find
    /// its column. The plugin finds columns by these names, so put the dump's own header rows back. Not for the flattened
    /// dialogue files (one header row) or LanguageConfig (no header, row 2 is a message).
    /// </summary>
    private static void RestoreHeaderRows(string workingDirectory, string outputPath, TextFileToSplit[] textFiles)
    {
        foreach (var textFile in textFiles.Where(t => t.PackageOutput && !t.Path.EndsWith(".json.csv") && !t.Path.StartsWith("LanguageConfig__")))
        {
            var packaged = $"{outputPath}/{textFile.Path}";
            var dumped = $"{workingDirectory}/Raw/Dumped/GameData/{textFile.Path}";
            if (!File.Exists(packaged) || !File.Exists(dumped))
                continue;

            var lines = File.ReadAllLines(packaged);
            var headers = File.ReadLines(dumped).Take(2).ToArray();

            // Sheets such as item__* have one header row, where the Chinese label is the property name. Their second row is the first
            // record (its id is a number), which must stay translated.
            if (headers.Length == 2 && IsDataRow(headers[1]))
                headers = headers[..1];

            if (lines.Length < headers.Length)
                continue;

            for (var i = 0; i < headers.Length; i++)
                lines[i] = headers[i];
            File.WriteAllLines(packaged, lines);
        }
    }

    /// <summary>True when a dumped row is a record: the cell after the row number is a plain integer (a header row names columns instead).</summary>
    public static bool IsDataRow(string dumpedLine)
    {
        var cells = CompoundFieldSplitter.ParseCsvRow(dumpedLine);
        return cells.Length > 1 && int.TryParse(cells[1].Trim(), out _);
    }

    /// <summary>
    /// The packaged CSVs keep every column of the dump, so the plugin cannot tell translated columns from untouched ones.
    /// This writes them to <c>translated-columns.json</c> (file name to column indexes, column 0 being Row) next to the CSVs:
    /// the plugin writes only those columns into the game's rows.
    /// </summary>
    private static void WriteTranslatedColumns(string outputPath, TextFileToSplit[] textFiles)
    {
        var columns = new SortedDictionary<string, int[]>(StringComparer.Ordinal);

        foreach (var textFile in textFiles.Where(t => t.PackageOutput))
        {
            var file = $"{outputPath}/{textFile.Path}";
            if (!File.Exists(file))
                continue;

            var width = ColumnCount(File.ReadLines(file).First());
            columns[textFile.Path] = Enumerable.Range(1, width - 1).Where(c => !textFile.SkipColumns.Contains(c)).ToArray();
        }

        File.WriteAllText($"{outputPath}/translated-columns.json", JsonSerializer.Serialize(columns, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>The number of cells in one CSV line (the header, which never has a line break inside a cell).</summary>
    private static int ColumnCount(string line)
    {
        var count = 1;
        var quoted = false;
        foreach (var ch in line)
        {
            if (ch == '"')
                quoted = !quoted;
            else if (ch == ',' && !quoted)
                count++;
        }
        return count;
    }
}
