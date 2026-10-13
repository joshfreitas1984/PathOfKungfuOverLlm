using HarmonyLib;

namespace PathOfKungfuPlugin;

/// <summary>
/// WordGroupHandle runs on the map and other screens in English mode and replaces any of about 20 Chinese place names left in the screen's text with the
/// game's own English ("襄阳" -> "Xiangyang", leaving the 城 of "襄阳城"; "青萍村" -> "Green Duckweed Village"). It only matches substrings, so a
/// translation of ours that still has a Chinese place name gets half rewritten, and its names differ from our glossary. With this on, the screens show
/// our translation only: a label the packaged prefab text does not cover stays Chinese until it is dumped and translated.
/// </summary>
[HarmonyPatch(typeof(WordGroupHandle), nameof(WordGroupHandle.CheckWord))]
internal static class GameEnglishPlaceNamesPatch
{
    internal static bool UseGameEnglish { get; set; }

    private static bool Prefix() => UseGameEnglish;
}

/// <summary>
/// WordSpecial is attached to some Text components and, when Steam's language is English, rewrites their text every frame with the game's own words:
/// 两 -> "Liang", 文 -> "Wen", 药品 and 其他 -> "Normal", 食物 -> "Energy", 闪避 -> "Eva", 未习得 -> "UnLearn". It rewrites any Chinese left in the text
/// whatever it means, so skip it and let the translation of the text stand.
/// </summary>
[HarmonyPatch(typeof(WordSpecial), nameof(WordSpecial.Update))]
internal static class WordSpecialPatch
{
    private static bool Prefix() => GameEnglishPlaceNamesPatch.UseGameEnglish;
}
