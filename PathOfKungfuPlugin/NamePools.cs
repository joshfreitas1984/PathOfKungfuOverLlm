using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PathOfKungfuPlugin;

/// <summary>The pinyin pool that replaces the game's Western random NPC names, and the Western pool it replaces.</summary>
internal static class NamePools
{
    /// <summary>Applies in every game language: the Chinese pool is replaced too. Set from the plugin config.</summary>
    internal static bool Enabled { get; set; } = true;

    /// <summary>Pinyin pool in the game's own shape (IDs from 1, blank cells null). Null until loaded.</summary>
    internal static Dictionary<int, WordNameData>? Pinyin { get; private set; }

    internal static List<string> PinyinSurnames { get; } = new();
    internal static List<string> PinyinMale { get; } = new();
    internal static List<string> PinyinFemale { get; } = new();

    // The game's original name2 sheet: name -> position among the distinct names of its column.
    internal static Dictionary<string, int> WesternSurnames { get; } = new();
    internal static Dictionary<string, int> WesternMale { get; } = new();
    internal static Dictionary<string, int> WesternFemale { get; } = new();

    // The game's Chinese name sheet: name -> row ID, which is also the row of its pinyin in the pinyin pool.
    internal static Dictionary<string, int> ChineseSurnames { get; } = new();
    internal static Dictionary<string, int> ChineseMale { get; } = new();
    internal static Dictionary<string, int> ChineseFemale { get; } = new();

    private static bool loaded;

    internal static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        // Columns: ID,surnam,male,female
        var pool = new Dictionary<int, WordNameData>();
        foreach (var cells in ReadRows("WorldEvolution__name2.csv", 4))
        {
            if (!int.TryParse(cells[0], out var id)) continue;
            // Blank cells stay null like the game's own sheets; the game counts and tests them for null/whitespace.
            pool[id] = new WordNameData { id = id, surnam = Cell(cells[1]), male = Cell(cells[2]), female = Cell(cells[3]) };
            Add(PinyinSurnames, cells[1]);
            Add(PinyinMale, cells[2]);
            Add(PinyinFemale, cells[3]);
        }

        // Columns: Row,ID,surnam,male,female (the dumper's layout)
        foreach (var cells in ReadRows("WorldEvolution__name2.western.csv", 5))
        {
            Index(WesternSurnames, cells[2]);
            Index(WesternMale, cells[3]);
            Index(WesternFemale, cells[4]);
        }

        // Columns: Row,ID,surnam,male,female
        foreach (var cells in ReadRows("WorldEvolution__name.chinese.csv", 5))
        {
            if (!int.TryParse(cells[1], out var id)) continue;
            if (cells[2].Length > 0) ChineseSurnames[cells[2]] = id;
            if (cells[3].Length > 0) ChineseMale[cells[3]] = id;
            if (cells[4].Length > 0) ChineseFemale[cells[4]] = id;
        }

        Pinyin = pool;
        Plugin.Logger.LogInfo($"Loaded {pool.Count} pinyin random NPC name rows ({PinyinSurnames.Count} surnames, " +
                              $"{PinyinMale.Count} male, {PinyinFemale.Count} female).");
    }

    private static IEnumerable<string[]> ReadRows(string resource, int width)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
        if (stream == null)
        {
            Plugin.Logger.LogError($"Embedded {resource} is missing; random NPC names are unchanged.");
            yield break;
        }

        using var reader = new StreamReader(stream);
        reader.ReadLine(); // header
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var cells = line.Split(',');
            if (cells.Length >= width) yield return cells;
        }
    }

    private static string? Cell(string value) => value.Length == 0 ? null : value;

    private static void Add(List<string> list, string value)
    {
        if (value.Length > 0) list.Add(value);
    }

    private static void Index(Dictionary<string, int> index, string value)
    {
        if (value.Length > 0 && !index.ContainsKey(value)) index[value] = index.Count;
    }
}
