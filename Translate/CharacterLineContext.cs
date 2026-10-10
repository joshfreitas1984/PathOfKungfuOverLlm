using FanslationStudio.LlmKit.Support;

namespace Translate;

/// <summary>
/// Line context for Path of Kung Fu: a line that names a character from Files/CharacterGenders.yaml (derived from the
/// game's own NPC table, NPC.xlsx column 性别 and monster.xlsx 角色模组) is told that character's gender, so a pronoun for
/// them is written and checked against it. Names seen with both genders and the player character (who picks a gender in
/// game) are not listed. Used only when Config.yaml's lineContextEnabled is true.
/// </summary>
public static class CharacterLineContext
{
    private const string TableFile = "CharacterGenders.yaml";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Characters = new(() => Load(GameFileHandling.WorkingDirectory));

    public static IReadOnlyDictionary<TranslationSplit, LineContext> Provide(string workingDirectory, TextFileToSplit textFile, IReadOnlyList<TranslationLine> lines)
    {
        // The cached table is for the game's own working directory; any other directory (a test copy) is read directly.
        var characters = string.Equals(workingDirectory, GameFileHandling.WorkingDirectory, StringComparison.Ordinal)
            ? Characters.Value
            : Load(workingDirectory);

        var contexts = new Dictionary<TranslationSplit, LineContext>();
        // Two-character minimum: single characters are ordinary words and would match everywhere.
        CharacterContext.AddCharacterContext(contexts, lines, characters, minNameLength: 2);
        return contexts;
    }

    public static IReadOnlyDictionary<string, string> Load(string workingDirectory) =>
        CharacterContext.FromYaml(Path.Combine(workingDirectory, TableFile));
}
