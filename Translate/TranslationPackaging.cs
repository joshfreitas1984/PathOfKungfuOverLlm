using System.Text.Json;
using FanslationStudio.LlmKit.Support;
using FanslationStudio.LlmKit.Utility;
using FanslationStudio.LlmKit.Workflow;
using SharedAssembly.DynamicStrings;

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

        await RestoreColorCloseTagsAsync(outputPath);
        await AddGameEnglishContractsAsync(workingDirectory, outputPath);
        RestoreHeaderRows(workingDirectory, outputPath, textFiles);
        MirrorIntoGameEnglishColumns(outputPath);
        WriteTranslatedColumns(outputPath, textFiles);

        Console.WriteLine($"Passed: {passedCount}");
        Console.WriteLine($"QC failures: {qcRejectedCount}");
        Console.WriteLine($"Fell back to raw: {rawFallbackCount}");
    }

    /// <summary>
    /// A code literal can be one piece of a string the game concatenates, so it may start with the "&lt;/color&gt;" that closes a tag opened in the
    /// piece before it ("&lt;/color&gt;&lt;color=red&gt;等级&lt;/color&gt;"). The model sees a stray closing tag and drops it, leaving the colour open for the
    /// rest of the line. When the Chinese literal starts with one and the translation does not, put it back.
    /// </summary>
    private static async Task RestoreColorCloseTagsAsync(string outputPath)
    {
        var contractsFile = $"{outputPath}/dynamicStrings.txt.yaml";
        if (!File.Exists(contractsFile))
            return;

        var contracts = YamlHelper.CreateDeserializer().Deserialize<List<DynamicStringContract>>(await File.ReadAllTextAsync(contractsFile)) ?? [];
        var fixedCount = RestoreColorCloseTags(contracts);
        if (fixedCount == 0)
            return;

        await File.WriteAllTextAsync(contractsFile, YamlHelper.CreateSerializer().Serialize(contracts));
        Console.WriteLine($"Restored a leading </color> on {fixedCount} dynamic strings");
    }

    public static int RestoreColorCloseTags(List<DynamicStringContract> contracts)
    {
        const string close = "</color>";
        var count = 0;
        foreach (var contract in contracts)
        {
            if (contract.Raw.StartsWith(close, StringComparison.OrdinalIgnoreCase) && !contract.Translation.StartsWith(close, StringComparison.OrdinalIgnoreCase))
            {
                contract.Translation = close + contract.Translation;
                count++;
            }
        }
        return count;
    }

    public const string GameEnglishTable = "DynamicStringsGameEnglish.csv";

    /// <summary>
    /// The game picks text with <c>Open ? "English" : "中文"</c>, and the plugin runs in English mode, so the game's own English would be shown instead of
    /// the translation of the Chinese side. For each row of <see cref="GameEnglishTable"/> (written by Files/ScanGameEnglishLiterals.py) whose Chinese
    /// literal was packaged, this adds a contract with the same translation for the English literal, so the same transpiler replaces it.
    /// </summary>
    private static async Task AddGameEnglishContractsAsync(string workingDirectory, string outputPath)
    {
        var contractsFile = $"{outputPath}/dynamicStrings.txt.yaml";
        var table = $"{workingDirectory}/{GameEnglishTable}";
        if (!File.Exists(contractsFile) || !File.Exists(table))
            return;

        var contracts = YamlHelper.CreateDeserializer().Deserialize<List<DynamicStringContract>>(await File.ReadAllTextAsync(contractsFile)) ?? [];
        var rows = File.ReadLines(table).Skip(1).Where(l => l.Length > 0).Select(CompoundFieldSplitter.ParseCsvRow)
            .Where(f => f.Length >= 4).Select(f => (Type: f[0], Method: f[1], English: f[2], Chinese: f[3]));

        var added = AddGameEnglishContracts(contracts, rows);
        if (added.Count == 0)
            return;

        contracts.AddRange(added);
        await File.WriteAllTextAsync(contractsFile, YamlHelper.CreateSerializer().Serialize(contracts));
        Console.WriteLine($"Added {added.Count} contracts for the game's own English literals");
    }

    public static List<DynamicStringContract> AddGameEnglishContracts(
        List<DynamicStringContract> contracts, IEnumerable<(string Type, string Method, string English, string Chinese)> rows)
    {
        // The dump writes commas as fullwidth, the table may not.
        static (string, string, string) Key(string type, string method, string raw) => (type, method, raw.Replace(',', '，'));

        var byChinese = contracts.GroupBy(c => Key(c.Type, c.Method, c.Raw)).ToDictionary(g => g.Key, g => g.First());
        var existing = contracts.Select(c => Key(c.Type, c.Method, c.Raw)).ToHashSet();
        var added = new List<DynamicStringContract>();

        foreach (var (type, method, english, chinese) in rows)
        {
            if (!byChinese.TryGetValue(Key(type, method, chinese), out var source) || !existing.Add(Key(type, method, english)))
                continue;

            added.Add(new DynamicStringContract
            {
                Type = source.Type,
                Method = source.Method,
                ILOffset = source.ILOffset,
                Raw = english,
                Translation = source.Translation,
                Parameters = source.Parameters,
            });
        }

        return added;
    }

    /// <summary>
    /// Sheets that carry the developers' own English next to the Chinese text (WorldEvolution/attribute: name2 beside name, tip2 beside tip).
    /// The game shows the English column in English mode, so translating the Chinese column alone leaves "Rookie" on screen. Each pair is
    /// (Chinese source column, game English target column); the target is overwritten with the translation of the source.
    /// </summary>
    private static readonly Dictionary<string, (int Source, int Target)[]> GameEnglishColumnMirrors = new()
    {
        ["WorldEvolution__attribute.csv"] = [(3, 4), (6, 7), (24, 25)],
        // taskText2 is empty in every row, so the English breakthrough screen showed no meridian task text.
        ["JingLuo__collaterals.csv"] = [(3, 4), (10, 11)],
    };

    private static bool IsMirrorTarget(string path, int column) =>
        GameEnglishColumnMirrors.TryGetValue(path, out var pairs) && pairs.Any(p => p.Target == column);

    private static void MirrorIntoGameEnglishColumns(string outputPath)
    {
        foreach (var (path, pairs) in GameEnglishColumnMirrors)
        {
            var file = $"{outputPath}/{path}";
            if (!File.Exists(file))
                continue;

            var lines = File.ReadAllLines(file);
            // Rows 0 and 1 are the header rows restored from the dump.
            for (var i = 2; i < lines.Length; i++)
            {
                var cells = CompoundFieldSplitter.ParseCsvRow(lines[i]);
                var changed = false;
                foreach (var (source, target) in pairs)
                {
                    // "0" is the sheet's placeholder for "no text"; keep the game's own value then.
                    if (cells.Length <= Math.Max(source, target) || cells[source].Length == 0 || cells[source] == "0")
                        continue;
                    // The game splits getTip/getTip2 on ';' and indexes them in step, so a list must keep the game's item count.
                    if (cells[target].Contains(';') && cells[target].Count(c => c == ';') != cells[source].Count(c => c == ';'))
                        continue;
                    cells[target] = cells[source];
                    changed = true;
                }

                if (changed)
                    lines[i] = string.Join(",", cells.Select(CsvCell));
            }

            File.WriteAllLines(file, lines);
        }
    }

    private static string CsvCell(string cell) =>
        cell.IndexOfAny([',', '"', '\n', '\r']) < 0 ? cell : $"\"{cell.Replace("\"", "\"\"")}\"";

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
            columns[textFile.Path] = Enumerable.Range(1, width - 1).Where(c => !textFile.SkipColumns.Contains(c) || IsMirrorTarget(textFile.Path, c)).ToArray();
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
