using System.Text.RegularExpressions;
using FanslationStudio.LlmKit.Configuration;
using FanslationStudio.LlmKit.Utility;

namespace Translate;

/// <summary>Game-specific constants and LLM repair/validation hooks for Path of Kung Fu.</summary>
public static class GameFileHandling
{
    public static readonly string WorkingDirectory = "../../../../Files";
    public static readonly string GameFolder = @"G:\SteamLibrary\steamapps\common\The road of Jianghu";

    // Dialogue holds two kinds of runtime tokens that must stay inside the sentence they are written in:
    //   <fName> <lName> <name> <faction>   the player's (or another character's) name, filled in by the game;
    //   <少侠||姑娘>                         a gender alternative: the game shows the part before "||" for a male player and
    //                                       the part after it for a female player (91 distinct, about 3,400 uses).
    // Left alone they split a sentence into fragments that are translated one at a time, which gave broken joins
    // ("Big Brother is so skilled with your hands"). Gluing them into the surrounding run keeps the sentence whole.
    public static readonly Regex NameTokenPattern = new(@"<(?:fName|lName|name|faction)>", RegexOptions.Compiled);
    public static readonly Regex GenderAlternativePattern = new(@"<[^<>|]*\|\|[^<>|]*>", RegexOptions.Compiled);
    private static readonly Regex ColorTagPattern = new(@"</?color[^>]*>", RegexOptions.Compiled);

    // NPC dialogue and action text also holds bare words and letters the game replaces itself (FunTool.ConvertText): gangName, mapName,
    // a/b (NPC names), p/fp/lp (player's full/first/last name), i/i1/i2 (item names), and "fp[大哥||姐姐]" with a [A||B] title. They sit
    // inside sentences, so they are glued in like the name tokens above. The letters count only when their neighbours are Chinese text,
    // a tag, punctuation or the string's ends (never a space or another letter), which keeps ordinary English words out. The same
    // patterns are in Config.yaml's namedTokenReplacers (the model sees them as {npcName}...); NamedTokenTests keeps the two in step.
    private const string TokenPrev = @"(?:(?<=[^\x00-\x7F])|(?<=[>,.!?:;()])|^)";
    private const string TokenNext = @"(?=[^\x00-\x7F]|[<,.!?:;()\[]|$)";
    public static readonly Regex WordTokenPattern = new(@"gangName|mapName|gangWuxue|lastRandomAttributeName|gangJoinCDRemain", RegexOptions.Compiled);
    public static readonly Regex CodeLetterTokenPattern = new(TokenPrev + "(?:fp|lp|p|a|b|i1|i2|i)" + TokenNext, RegexOptions.Compiled);
    public static readonly Regex BracketAlternativePattern = new(@"\[[^\[\]|]{1,8}\|\|[^\[\]|]{1,8}\]", RegexOptions.Compiled);

    public static readonly CompoundFieldSplitterOptions SplitterOptions = new()
    {
        PlaceholderPatterns = [NameTokenPattern, GenderAlternativePattern, WordTokenPattern, CodeLetterTokenPattern, BracketAlternativePattern],
    };

    public static readonly GameHooks Hooks = new()
    {
        LineContextProvider = CharacterLineContext.Provide,
        // A he/she next to a name token or a gender alternative refers to the player, whose gender is not known here.
        UnknownGenderPersonTokens = ["<fName>", "<lName>", "<name>", "///fp///", "///lp///", "///p///"],
        CustomColumnValidator = ValidateRuntimeTokens,
    };

    /// <summary>
    /// The dialogue files switch LlmKit's generic tag check off (a gender alternative such as &lt;少侠||姑娘&gt; looks like an
    /// HTML tag whose content must change in translation), so this keeps the equivalent guarantees: every name token,
    /// colour tag and gender alternative in the source must be in the result, in the same number.
    /// </summary>
    private static string? ValidateRuntimeTokens(FanslationStudio.LlmKit.Support.TextFileToSplit textFile, int? column, string raw, string result)
    {
        if (!textFile.IgnoreHtmlTagsInText || !raw.Contains('<'))
            return null;

        // Name tokens must match in number, not just be present: an extra one is the model inventing the player's name.
        // Colour tags only need to be present.
        foreach (var (pattern, exact) in new[] { (NameTokenPattern, true), (ColorTagPattern, false) })
        {
            var expected = pattern.Matches(raw).Select(m => m.Value).GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());
            foreach (var (token, count) in expected)
            {
                var found = Regex.Matches(result, Regex.Escape(token)).Count;
                if (exact ? found != count : found < count)
                    return token;
            }
        }

        // Same for gender alternatives: an extra one is a pair the model made up (seen: "<Brother||Sister>" added to a line
        // that had one).
        var alternatives = GenderAlternativePattern.Matches(raw).Count;
        if (alternatives > 0 && GenderAlternativePattern.Matches(result).Count != alternatives)
            return "<male||female>";

        return null;
    }
}
