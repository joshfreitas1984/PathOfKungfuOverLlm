using HarmonyLib;

namespace PathOfKungfuPlugin;

/// <summary>
/// Silver is stored in wen (1000 wen = 1 liang). NumTool.GetGoldShow splits it into liang and wen, and in English mode prints the two numbers
/// without their 两 / 文 units ("0 238" for 238 wen), so show the plain total instead.
/// </summary>
[HarmonyPatch(typeof(NumTool), nameof(NumTool.GetGoldShow))]
internal static class GoldShowPatch
{
    // Chinese mode keeps the game's own "0两 238文".
    private static bool Prefix(long num, ref string __result)
    {
        if (WordManager.Singleton == null || !WordManager.Singleton.Open) return true;

        __result = num.ToString();
        return false;
    }
}
