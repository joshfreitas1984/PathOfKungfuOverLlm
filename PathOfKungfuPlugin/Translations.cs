using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace PathOfKungfuPlugin;

/// <summary>One packaged CSV (a file of Files/Mod): every record, column 0 being the dumper's Row column.</summary>
internal sealed class TranslationTable
{
    public TranslationTable(List<string[]> records) => Records = records;

    public List<string[]> Records { get; }

    /// <summary>The Row column (source row or line number) of record <paramref name="index"/>.</summary>
    public static int RowOf(string[] record) => int.TryParse(record[0], out var row) ? row : -1;

    /// <summary>
    /// RFC 4180 reader: quoted cells may hold commas, doubled quotes and line breaks. The dumper writes line breaks as the two
    /// characters "\n", but a translation may still contain a real one.
    /// </summary>
    public static List<string[]> Parse(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

        var records = new List<string[]>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var any = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch != '"') cell.Append(ch);
                else if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else quoted = false;
                continue;
            }

            switch (ch)
            {
                case '"' when cell.Length == 0:
                    quoted = true;
                    any = true;
                    break;
                case ',':
                    cells.Add(cell.ToString());
                    cell.Clear();
                    any = true;
                    break;
                case '\r':
                    break;
                case '\n':
                    if (any || cell.Length > 0)
                    {
                        cells.Add(cell.ToString());
                        records.Add(cells.ToArray());
                    }
                    cells.Clear();
                    cell.Clear();
                    any = false;
                    break;
                default:
                    cell.Append(ch);
                    any = true;
                    break;
            }
        }

        if (any || cell.Length > 0)
        {
            cells.Add(cell.ToString());
            records.Add(cells.ToArray());
        }

        return records;
    }
}

/// <summary>Where the packaged translations come from. A seam so the injection can be run against the real files without the game.</summary>
internal interface ITranslationSource
{
    /// <summary>The packaged CSV with this file name (for example "NPC__Dialog.csv"), or null when it was not packaged.</summary>
    TranslationTable? Get(string fileName);

    /// <summary>Indexes of the translated columns of a file (column 0 is Row), from translated-columns.json; null when unknown.</summary>
    int[]? TranslatedColumns(string fileName);
}

/// <summary>Reads the packaged translation from a folder (BepInEx/plugins/resources), caching every file.</summary>
internal sealed class FolderTranslationSource : ITranslationSource
{
    public const string ColumnsFile = "translated-columns.json";

    private readonly string folder;
    private readonly Dictionary<string, TranslationTable?> tables = new(StringComparer.Ordinal);
    private Dictionary<string, int[]>? columns;

    public FolderTranslationSource(string folder) => this.folder = folder;

    public TranslationTable? Get(string fileName)
    {
        lock (tables)
        {
            if (tables.TryGetValue(fileName, out var cached)) return cached;

            TranslationTable? table = null;
            var path = Path.Combine(folder, fileName);
            if (File.Exists(path))
            {
                try
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    table = new TranslationTable(TranslationTable.Parse(File.ReadAllText(path, Encoding.UTF8)));
                    Plugin.Logger.LogInfo($"Read {fileName}: {table.Records.Count} records in {timer.ElapsedMilliseconds} ms");
                }
                catch (Exception ex) { Plugin.Logger.LogError($"Cannot read {path}: {ex.Message}"); }
            }

            tables[fileName] = table;
            return table;
        }
    }

    public int[]? TranslatedColumns(string fileName)
    {
        lock (tables)
        {
            if (columns == null)
            {
                columns = new Dictionary<string, int[]>(StringComparer.Ordinal);
                var path = Path.Combine(folder, ColumnsFile);
                if (File.Exists(path))
                {
                    try { columns = JsonConvert.DeserializeObject<Dictionary<string, int[]>>(File.ReadAllText(path, Encoding.UTF8)) ?? columns; }
                    catch (Exception ex) { Plugin.Logger.LogError($"Cannot read {path}: {ex.Message}"); }
                }
                else
                    Plugin.Logger.LogWarning($"{path} is missing: table columns will not be translated (dialogue and text files still are).");
            }

            return columns.TryGetValue(fileName, out var result) ? result : null;
        }
    }
}
