using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PathOfKungfuPlugin;

/// <summary>
/// Puts the packaged translation into the rows the game has just read from a workbook. Pure logic (no Unity, no Harmony), so it
/// can be run over the real workbooks without the game.
/// <para>
/// The game loads a sheet into a list of objects whose properties are matched to the header row by name. The packaged CSV
/// <c>Workbook__Sheet.csv</c> has that same header row, so each CSV column maps to the same property. Rows are paired by the
/// first column (the ID) rather than by position, because the game drops blank rows and the dump's row numbers drift.
/// Only the columns listed in translated-columns.json are written; everything else stays as the game loaded it.
/// </para>
/// <para>
/// The game loads different files for English (<c>NPC_EN</c>, <c>装备英文</c>); the packaged files are named after the
/// Chinese base, so the language marker is stripped. The patch therefore applies whichever language the player runs.
/// </para>
/// </summary>
internal static class SheetInjector
{
    private static readonly Regex LanguageWorkbook = new(@"_(en|fr|de|ja|ko|es|ft|th|vi)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// <summary>Sheets the game swaps for an English twin without a language marker in the name (WordManager.Open), mapped to the Chinese sheet they replace.</summary>
    private static readonly Dictionary<(string Workbook, string Sheet), (string Workbook, string Sheet)> EnglishTwins = new()
    {
        [("box", "boxpoint2")] = ("box", "boxpoint"),
        [("Read", "Sheet2")] = ("Read", "Sheet1"),
        [("MapBuffer", "body2")] = ("MapBuffer", "body"),
        [("WorldEvolution", "explain2")] = ("WorldEvolution", "explain"),
    };

    private static readonly Regex CellRow = new(@"\d+", RegexOptions.Compiled);

    internal sealed class Stats
    {
        public string File = "";
        public int Items;
        public int Rows;
        public int Matched;
        public int CellsChanged;
        public int JsonApplied;
        public int JsonMissed;
        public bool Positional;
        public readonly List<string> UnmappedColumns = new();

        public override string ToString() =>
            $"{File}: {Matched}/{Items} rows matched ({Rows} packaged), {CellsChanged} cells changed" +
            (JsonApplied + JsonMissed > 0 ? $", dialogue {JsonApplied} applied / {JsonMissed} not found" : "") +
            (UnmappedColumns.Count > 0 ? $", no string property for column(s) {string.Join("/", UnmappedColumns)}" : "");
    }

    /// <summary>The packaged file name for a workbook and sheet the game asked for, with the language marker removed.</summary>
    internal static string BaseName(string workbook, string sheet)
    {
        workbook = LanguageWorkbook.Replace(workbook, "");
        if (EnglishTwins.TryGetValue((workbook, sheet), out var chinese)) (workbook, sheet) = chinese;
        else if (workbook == "skill2") workbook = "skill";
        if (sheet.EndsWith("英文", StringComparison.Ordinal)) sheet = sheet.Substring(0, sheet.Length - "英文".Length);
        else if (sheet.EndsWith("English", StringComparison.OrdinalIgnoreCase)) sheet = sheet.Substring(0, sheet.Length - "English".Length);
        return SafeName(workbook) + "__" + SafeName(sheet);
    }

    private static readonly Dictionary<(string Workbook, string Sheet), (string Workbook, string Sheet)> SandboxTwins = new()
    {
        [("RefreshBoss", "Refresh2")] = ("RefreshBoss", "Refresh"),
        [("RefreshBoss", "RefreshTalk2")] = ("RefreshBoss", "RefreshTalk"),
        [("PigeonMsgData", "PigeonMsgData2")] = ("PigeonMsgData", "PigeonMsgData"),
    };

    /// <summary>
    /// The Chinese sheet an English-mode copy (BetShop2, GangData2, RefreshTalk2...) mirrors, to use when nothing is packaged under the copy's own name.
    /// The copies keep the Chinese sheets' ids, rows and column order (checked on the dump), so rows pair by id, duplicate ids in order.
    /// Null when the sheet is not such a copy.
    /// </summary>
    internal static string? SandboxCopy(string workbook, string sheet)
    {
        workbook = LanguageWorkbook.Replace(workbook, "");
        if (SandboxTwins.TryGetValue((workbook, sheet), out var twin)) return SafeName(twin.Workbook) + "__" + SafeName(twin.Sheet);
        if (workbook is "BetShop2" or "GangData2" or "shop2") return SafeName(workbook.TrimEnd('2')) + "__" + SafeName(sheet);
        return null;
    }

    private static string SafeName(string name)
    {
        foreach (var bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
        return name;
    }

    /// <summary>Returns null when nothing is packaged for this sheet.</summary>
    public static Stats? Apply(IList list, string workbook, string sheet, string startCell, ITranslationSource source)
    {
        if (list.Count == 0) return null;

        var baseName = BaseName(workbook, sheet);
        var table = source.Get(baseName + ".csv");
        if (table == null && SandboxCopy(workbook, sheet) is { } chineseName && source.Get(chineseName + ".csv") != null)
        {
            baseName = chineseName;
            table = source.Get(baseName + ".csv");
        }
        if (table == null) return null;

        var headerIndex = HeaderRow(startCell) - 1;
        if (headerIndex < 0 || headerIndex >= table.Records.Count) return null;
        var header = table.Records[headerIndex];

        var itemType = list[0]!.GetType();
        var properties = PropertiesByHeader(itemType, header);
        var stats = new Stats { File = baseName, Items = list.Count };

        var translated = (source.TranslatedColumns(baseName + ".csv") ?? new int[0]).Where(c => c > 0).ToArray();
        var columns = translated.Where(c => c < header.Length && properties[c] is { PropertyType: var t } && t == typeof(string)).ToArray();
        foreach (var c in translated.Except(columns))
            stats.UnmappedColumns.Add(c < header.Length ? $"{c} '{header[c]}'" : c.ToString(CultureInfo.InvariantCulture));

        var dialogue = LoadDialogue(source, baseName + ".json.csv", header, properties);
        if (columns.Length == 0 && dialogue.Count == 0) return stats;

        var rows = table.Records.Skip(headerIndex + 1).Where(r => r.Skip(1).Any(c => c.Length > 0)).ToList();
        stats.Rows = rows.Count;

        foreach (var (item, row) in Pair(list, rows, header, properties, stats))
        {
            stats.Matched++;
            foreach (var c in columns)
            {
                if (c >= row.Length || row[c].Length == 0) continue; // blank: keep what the game has
                var property = properties[c]!;
                var current = (string?)property.GetValue(item);
                var value = Unescape(row[c], current);
                if (value == current) continue;
                property.SetValue(item, value);
                stats.CellsChanged++;
            }

            if (dialogue.Count > 0 && dialogue.TryGetValue(TranslationTable.RowOf(row), out var cells))
            {
                foreach (var (column, paths) in cells)
                {
                    var property = properties[column]!;
                    var json = (string?)property.GetValue(item);
                    if (string.IsNullOrEmpty(json)) { stats.JsonMissed += paths.Count; continue; }

                    var applied = ApplyDialogueCached(baseName, TranslationTable.RowOf(row), column, json!, paths);
                    stats.JsonApplied += applied.Used;
                    stats.JsonMissed += paths.Count - applied.Used;
                    if (applied.Json != json)
                    {
                        property.SetValue(item, applied.Json);
                        stats.CellsChanged++;
                    }
                }
            }
        }

        return stats;
    }

    private sealed class CachedDialogue
    {
        public string Input = "";
        public string Json = "";
        public int Used;
    }

    private static readonly Dictionary<(string File, int Row, int Column), CachedDialogue> DialogueCache = new();

    /// <summary>
    /// The game reads some sheets more than once (Touch, SandBox_Touch), and rewriting a cell's JSON is the slowest part of injecting.
    /// The result for a cell only depends on its text, so when the same cell comes round again with the same text the earlier result is reused.
    /// </summary>
    private static CachedDialogue ApplyDialogueCached(string file, int row, int column, string json, Dictionary<string, string> paths)
    {
        var key = (file, row, column);
        lock (DialogueCache)
        {
            if (DialogueCache.TryGetValue(key, out var cached) && cached.Input == json) return cached;
        }

        var applied = JsonInjector.Apply(json, paths);
        var entry = new CachedDialogue { Input = json, Json = applied.Json, Used = applied.Used.Count };
        lock (DialogueCache) DialogueCache[key] = entry;
        return entry;
    }

    /// <summary>"A2" is the second row; the default start cell is the first.</summary>
    private static int HeaderRow(string? startCell)
    {
        var match = CellRow.Match(startCell ?? "A1");
        return match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : 1;
    }

    /// <summary>For each CSV column, the property MiniExcel would fill from that header (null if none).</summary>
    private static PropertyInfo?[] PropertiesByHeader(Type type, string[] header)
    {
        var all = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite).ToList();
        var result = new PropertyInfo?[header.Length];
        for (var c = 1; c < header.Length; c++)
        {
            var name = header[c].Trim();
            if (name.Length == 0) continue;
            result[c] = all.FirstOrDefault(p => ColumnName(p) == name)
                        ?? all.FirstOrDefault(p => string.Equals(ColumnName(p), name, StringComparison.OrdinalIgnoreCase));
        }
        return result;
    }

    /// <summary>The header MiniExcel matches a property to: its [ExcelColumnName] if it has one, else its name (found by name so this has no MiniExcel dependency).</summary>
    private static string ColumnName(PropertyInfo property)
    {
        foreach (var attribute in property.GetCustomAttributes(false))
        {
            var type = attribute.GetType();
            if (type.Name == "ExcelColumnNameAttribute" && type.GetProperty("ExcelColumnName")?.GetValue(attribute) is string { Length: > 0 } name)
                return name;
        }
        return property.Name;
    }

    /// <summary>
    /// The column rows are paired by: the "id" column when the sheet has one, otherwise the first column that maps to a property.
    /// (Not always the first column: ECA's first column is a remark that differs between the Chinese and English workbooks.)
    /// </summary>
    private static int KeyColumn(string[] header, PropertyInfo?[] properties)
    {
        for (var c = 1; c < properties.Length; c++)
            if (properties[c] != null && string.Equals(header[c].Trim(), "id", StringComparison.OrdinalIgnoreCase)) return c;
        for (var c = 1; c < properties.Length; c++)
            if (properties[c] != null) return c;
        return -1;
    }

    /// <summary>Pairs each object the game loaded with its packaged row: by position when the key column agrees everywhere, otherwise by key.</summary>
    private static IEnumerable<(object Item, string[] Row)> Pair(IList list, List<string[]> rows, string[] header, PropertyInfo?[] properties, Stats stats)
    {
        var column = KeyColumn(header, properties);
        var key = column < 0 ? null : properties[column];
        string KeyOf(object item) => Convert.ToString(key!.GetValue(item), CultureInfo.InvariantCulture) ?? "";
        string CellOf(string[] row) => column < row.Length ? row[column] : "";

        if (list.Count == rows.Count && (key == null || Enumerable.Range(0, rows.Count).All(i => KeyOf(list[i]!) == CellOf(rows[i]))))
        {
            stats.Positional = true;
            for (var i = 0; i < rows.Count; i++)
                yield return (list[i]!, rows[i]);
            yield break;
        }

        if (key == null) yield break; // neither the counts nor a key to go by

        var byKey = new Dictionary<string, Queue<string[]>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var cell = CellOf(row);
            if (!byKey.TryGetValue(cell, out var queue)) byKey[cell] = queue = new Queue<string[]>();
            queue.Enqueue(row);
        }

        foreach (var item in list)
        {
            if (byKey.TryGetValue(KeyOf(item!), out var queue) && queue.Count > 0)
                yield return (item!, queue.Dequeue());
        }
    }

    /// <summary>Row (the dump's source row) to cells to paths to text, from the flattened dialogue file (Row, RowId, Column, Path, Text).</summary>
    private static Dictionary<int, Dictionary<int, Dictionary<string, string>>> LoadDialogue(ITranslationSource source, string fileName, string[] header, PropertyInfo?[] properties)
    {
        var result = new Dictionary<int, Dictionary<int, Dictionary<string, string>>>();
        var table = source.Get(fileName);
        if (table == null) return result;

        foreach (var record in table.Records.Skip(1))
        {
            if (record.Length < 5 || !int.TryParse(record[2], out var column) || column <= 0 || column >= properties.Length || properties[column] == null) continue;
            if (properties[column]!.PropertyType != typeof(string) || record[4].Length == 0) continue;

            var row = TranslationTable.RowOf(record);
            if (!result.TryGetValue(row, out var cells)) result[row] = cells = new Dictionary<int, Dictionary<string, string>>();
            if (!cells.TryGetValue(column, out var paths)) cells[column] = paths = new Dictionary<string, string>(StringComparer.Ordinal);
            paths[record[3]] = record[4].Replace("\\n", "\n"); // JSON strings hold real line breaks
        }
        return result;
    }

    /// <summary>The dump writes a line break inside a cell as the characters "\n". Put them back, unless the game's own cell writes it that way.</summary>
    internal static string Unescape(string cell, string? current)
    {
        if (cell.IndexOf("\\n", StringComparison.Ordinal) < 0) return cell;
        if (current != null && current.IndexOf("\\n", StringComparison.Ordinal) >= 0) return cell;
        return cell.Replace("\\n", "\n");
    }
}
