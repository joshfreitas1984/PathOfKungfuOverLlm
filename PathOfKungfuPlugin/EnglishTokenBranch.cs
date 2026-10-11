namespace PathOfKungfuPlugin;

/// <summary>
/// Decides when FunTool.ConvertText should take its English branch. The game picks the branch from WordManager.Open (true for every
/// non-Chinese Steam language). The Chinese branch replaces the bare letters a, b, i, p and x anywhere in the string, which wrecks English
/// text, and does not know the <c>///token///</c> form the packaged NPC text uses. The plugin injects the same English into both
/// languages, so an English string reaching the Chinese branch is switched to the English one for that call.
/// </summary>
internal static class EnglishTokenBranch
{
    /// <summary>True when the call should be forced onto the English branch: the game is in Chinese mode and the text has no CJK (so it is ours).</summary>
    internal static bool ShouldForce(bool wordManagerOpen, string? text) =>
        !wordManagerOpen && !string.IsNullOrEmpty(text) && !HasCjk(text);

    private static bool HasCjk(string text)
    {
        foreach (var c in text)
            if (c >= '㐀' && c <= '鿿')
                return true;

        return false;
    }
}
