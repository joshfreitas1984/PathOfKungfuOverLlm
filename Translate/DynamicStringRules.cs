using FanslationStudio.LlmKit.Support;
using FanslationStudio.LlmKit.Utility;

namespace Translate;

/// <summary>
/// Dynamic strings that must not be translated. The runtime patch swaps a string literal for its translation everywhere that literal
/// appears in the method, so a literal the code also uses as a key (animation or state name, sheet name, a value compared with data,
/// a Replace pattern) breaks the game once translated. Files/DynamicStringsDoNotTranslate.csv lists them per Type + Method + Raw,
/// found by reading the decompiled code. "Key" rows are always skipped; "Log" rows (log and exception text the player never sees)
/// only while <see cref="SkipLogStrings"/> is on.
/// </summary>
public static class DynamicStringRules
{
    public const string TableFile = "DynamicStringsDoNotTranslate.csv";

    /// <summary>Skip log and exception text to save translation time. Turn off to translate it too.</summary>
    public static bool SkipLogStrings = true;

    private static readonly Lazy<Dictionary<(string Type, string Method, string Raw), string>> Entries = new(() => Load(GameFileHandling.WorkingDirectory));

    public static Dictionary<(string Type, string Method, string Raw), string> Load(string workingDirectory) =>
        File.ReadLines(Path.Combine(workingDirectory, TableFile)).Skip(1).Where(l => l.Length > 0)
            .Select(CompoundFieldSplitter.ParseCsvRow)
            .ToDictionary(f => (f[2], f[3], f[4]), f => f[0]);

    /// <summary>
    /// A prefab line longer than this is not translated, because its QC prompt does not fit the QC model's context (num_ctx 6144 with a base prompt
    /// of several thousand tokens). Three lines are over it: the version changelog (8,725 characters, about 20,000 tokens), the user agreement
    /// (1,246, 6,819 tokens) and a DLC advert (882). The next longest is 327. Packaging leaves them as they are.
    /// </summary>
    public const int MaxPrefabTextLength = 800;

    /// <summary>
    /// A long line made of one phrase repeated (a run of 啊, or a confirmation prompt pasted a hundred times) is developer layout filler. It also builds the
    /// largest QC prompts in the pass, since every repeat is translated again. Detected as very few distinct characters for the length.
    /// </summary>
    private static bool IsRepeatedFiller(string raw) => raw.Length >= 100 && raw.Distinct().Count() * 10 < raw.Length;

    /// <summary>The line's dump format is Type,Method,ILOffset,RawText,ParamTypes.</summary>
    public static bool IsUnsafe(TextFileToSplit textFile, TranslationLine line, TranslationSplit split)
    {
        if (textFile.TextFileType == TextFileType.PrefabText)
            return line.Raw.Length > MaxPrefabTextLength || IsRepeatedFiller(line.Raw);

        if (textFile.TextFileType != TextFileType.DynamicStrings)
            return false;

        var fields = line.Raw.Split(',');
        if (fields.Length != 5 || !Entries.Value.TryGetValue((fields[0], fields[1], fields[3]), out var kind))
            return false;

        return kind == "Key" || SkipLogStrings;
    }
}
