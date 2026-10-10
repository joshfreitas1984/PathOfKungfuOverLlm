using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MiniExcelLibs;
using Newtonsoft.Json;

namespace PathOfKungfuPlugin;

/// <summary>
/// Dumps the game's Chinese source text to CSV for the translation pipeline. Files are flat
/// (<c>Workbook__Sheet.csv</c>) because LlmKit writes <c>Converted/{Path}.yaml</c> without creating subfolders. Reads the game's static
/// data files directly (no Harmony): .xlsx via the game's own MiniExcel, plus the TornData TSV and
/// LanguageConfig key=value files. Output is Chinese-base only - the per-language workbooks/sheets
/// the game ships are skipped. Every CSV starts with a "Row" column (1-based source row, or line
/// number for text files) so the injector can address cells; newlines inside cells are written as
/// the two characters "\n" so each record stays on one line.
/// </summary>
internal static class Dumper
{
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);
    private static readonly Regex LanguageWorkbook = new(@"_(en|fr|de|ja|ko|es|ft|th|vi)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private sealed class ManifestEntry
    {
        public string Source = "";
        public string Output = "";
        public int Rows;
        public int CjkCells;
        public List<string> Headers = new();
    }

    public static void Run(string streamingAssets, string outputFolder, string gameVersion)
    {
        Directory.CreateDirectory(outputFolder);
        var manifest = new List<ManifestEntry>();
        var skipped = new List<string>();

        DumpExcel(Path.Combine(streamingAssets, "resdata", "ExcelData"), outputFolder, manifest, skipped);
        DumpTsv(Path.Combine(streamingAssets, "TornData"), outputFolder, manifest);
        DumpKeyValue(Path.Combine(streamingAssets, "LanguageConfig", "LanguageConfig_jt.txt"), outputFolder, manifest);

        File.WriteAllText(Path.Combine(outputFolder, "manifest.json"),
            JsonConvert.SerializeObject(new { gameVersion = ReadGameVersion(streamingAssets) ?? gameVersion, files = manifest, skippedNoChinese = skipped }, Formatting.Indented),
            new UTF8Encoding(true));
        Plugin.Logger.LogInfo($"Dump finished: {manifest.Count} files written to {outputFolder} ({skipped.Count} sheets without Chinese skipped).");
    }

    /// <summary>
    /// The game's own version ("Version=v_1.2.0.1_v" in LanguageConfig); Application.version is a
    /// different, older number (1.0.1.3) and does not match Files/Packaging/GameVersion.txt.
    /// </summary>
    private static string? ReadGameVersion(string streamingAssets)
    {
        var path = Path.Combine(streamingAssets, "LanguageConfig", "LanguageConfig_jt.txt");
        if (!File.Exists(path)) return null;
        var match = Regex.Match(File.ReadAllText(path, Encoding.UTF8), @"^Version=v_(.+?)_v", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static void DumpExcel(string excelFolder, string outputFolder, List<ManifestEntry> manifest, List<string> skipped)
    {
        foreach (var path in Directory.GetFiles(excelFolder, "*.xlsx").OrderBy(p => p, StringComparer.Ordinal))
        {
            var book = Path.GetFileNameWithoutExtension(path);
            if (LanguageWorkbook.IsMatch(book))
                continue;

            Plugin.Logger.LogInfo($"Dumping {book}.xlsx");
            List<string> sheets;
            try { sheets = MiniExcel.GetSheetNames(path).ToList(); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"{book}: cannot list sheets: {ex.Message}"); continue; }

            foreach (var sheet in sheets)
            {
                if (sheet.EndsWith("英文", StringComparison.Ordinal) || sheet.EndsWith("English", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var rows = ReadSheet(path, sheet, out var columns);
                    var cjk = CountCjk(rows);
                    if (cjk == 0) { skipped.Add($"{book}/{sheet}"); continue; }

                    var relative = SafeName(book) + "__" + SafeName(sheet) + ".csv";
                    WriteCsv(Path.Combine(outputFolder, relative), rows.Select((r, i) => (i + 1, r)), columns);
                    manifest.Add(new ManifestEntry
                    {
                        Source = $"{book}.xlsx#{sheet}",
                        Output = relative.Replace('\\', '/'),
                        Rows = rows.Count,
                        CjkCells = cjk,
                        Headers = HeaderOf(rows),
                    });
                    DumpJsonStrings(rows, SafeName(book) + "__" + SafeName(sheet) + ".json.csv", $"{book}.xlsx#{sheet}", outputFolder, manifest);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"{book}/{sheet}: {ex.Message}");
                }
            }
        }
    }

    private static void DumpJsonStrings(List<string?[]> rows, string relative, string source, string outputFolder, List<ManifestEntry> manifest)
    {
        var leaves = JsonFlattener.FlattenRows(rows);
        if (leaves.Count == 0) return;

        leaves.Insert(0, (1, new string?[] { "RowId", "Column", "Path", "Text" }));
        WriteCsv(Path.Combine(outputFolder, relative), leaves, 4);
        manifest.Add(new ManifestEntry
        {
            Source = source + " (json strings)",
            Output = relative.Replace('\\', '/'),
            Rows = leaves.Count,
            CjkCells = leaves.Count - 1,
            Headers = new List<string> { "RowId", "Column", "Path", "Text" },
        });
    }

    /// <summary>Reads every row as raw text. Row i of the result is the i-th row MiniExcel returns.</summary>
    private static List<string?[]> ReadSheet(string path, string sheet, out int columns)
    {
        var result = new List<string?[]>();
        columns = 0;
        // useHeaderRow:false makes MiniExcel key cells by column letter (A, B, ...), header row included.
        IEnumerable<object> query = MiniExcel.Query(path, false, sheet);
        foreach (object row in query)
        {
            var dict = (IDictionary<string, object?>)row;
            var cells = new string?[dict.Count == 0 ? 0 : dict.Keys.Max(k => ColumnIndex(k) + 1)];
            foreach (var kv in dict)
                cells[ColumnIndex(kv.Key)] = kv.Value == null ? null : Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture);
            columns = Math.Max(columns, cells.Length);
            result.Add(cells);
        }
        return result;
    }

    private static int ColumnIndex(string letters)
    {
        var n = 0;
        foreach (var ch in letters) n = n * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        return n - 1;
    }

    private static int CountCjk(IEnumerable<string?[]> rows) =>
        rows.Sum(r => r.Count(c => c != null && Cjk.IsMatch(c)));

    private static List<string> HeaderOf(List<string?[]> rows) =>
        rows.Count > 0 ? rows[0].Select(c => c ?? "").ToList() : new List<string>();

    private static void DumpTsv(string folder, string outputFolder, List<ManifestEntry> manifest)
    {
        // Only the files that carry displayed text. Data_Word keeps ID/Section/Chinese; the other
        // language columns are the shipped translations we are replacing.
        var files = new (string Name, int Columns)[] { ("Data_Word.txt", 3), ("Data_Skill.txt", int.MaxValue), ("Data_Speak.txt", int.MaxValue) };
        foreach (var (name, maxColumns) in files)
        {
            var path = Path.Combine(folder, name);
            if (!File.Exists(path)) { Plugin.Logger.LogWarning($"Missing {path}"); continue; }

            var rows = File.ReadAllLines(path, Encoding.UTF8)
                .Select(l => l.Split('\t').Take(maxColumns).Cast<string?>().ToArray())
                .ToList();
            var columns = rows.Max(r => r.Length);
            var relative = "TornData__" + Path.GetFileNameWithoutExtension(name) + ".csv";
            WriteCsv(Path.Combine(outputFolder, relative), rows.Select((r, i) => (i + 1, r)), columns);
            manifest.Add(new ManifestEntry
            {
                Source = $"TornData/{name}",
                Output = relative.Replace('\\', '/'),
                Rows = rows.Count,
                CjkCells = CountCjk(rows),
                Headers = HeaderOf(rows),
            });
        }
    }

    private static void DumpKeyValue(string path, string outputFolder, List<ManifestEntry> manifest)
    {
        // _jt is the Simplified Chinese table (_ft Traditional, _en the shipped English).
        if (!File.Exists(path)) { Plugin.Logger.LogWarning($"Missing {path}"); return; }

        var rows = new List<(int Row, string?[] Cells)>();
        var line = 0;
        foreach (var text in File.ReadAllText(path, Encoding.UTF8).Split('\n'))
        {
            line++;
            var eq = text.IndexOf('=');
            if (eq <= 0) continue; // blank and // comment lines carry no text, matching LanguageLocal.LoadText
            rows.Add((line, new string?[] { text.Substring(0, eq), text.Substring(eq + 1).TrimEnd('\r') }));
        }

        var relative = "LanguageConfig__LanguageConfig_jt.csv";
        WriteCsv(Path.Combine(outputFolder, relative), rows, 2);
        manifest.Add(new ManifestEntry
        {
            Source = "LanguageConfig/LanguageConfig_jt.txt",
            Output = relative.Replace('\\', '/'),
            Rows = rows.Count,
            CjkCells = CountCjk(rows.Select(r => r.Cells)),
            Headers = new List<string> { "key", "value" },
        });
    }

    private static void WriteCsv(string path, IEnumerable<(int Row, string?[] Cells)> rows, int columns)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        foreach (var (row, cells) in rows)
        {
            var sb = new StringBuilder();
            sb.Append(row);
            for (var c = 0; c < columns; c++)
            {
                sb.Append(',');
                sb.Append(Escape(c < cells.Length ? cells[c] : null));
            }
            writer.WriteLine(sb.ToString());
        }
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        value = value!.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");
        return value.IndexOfAny(new[] { ',', '"' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    private static string SafeName(string name)
    {
        foreach (var bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
        return name;
    }
}
