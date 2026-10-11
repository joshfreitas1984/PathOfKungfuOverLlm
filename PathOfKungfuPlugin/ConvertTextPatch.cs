using System;
using HarmonyLib;

namespace PathOfKungfuPlugin;

/// <summary>
/// Runs FunTool.ConvertText (names, NPC and item tokens in NPC dialogue and actions) on its English branch for English text, whatever the
/// game language - see <see cref="EnglishTokenBranch"/>. WordManager.Open is switched on for that one call and put back in a finalizer, so
/// an exception cannot leave it changed, and Chinese strings (untranslated ones) keep the branch they had.
/// </summary>
[HarmonyPatch(typeof(FunTool), nameof(FunTool.ConvertText))]
internal static class ConvertTextEnglishBranchPatch
{
    internal static bool Enabled = true;

    private static void Prefix(string txt, out bool __state)
    {
        __state = false;
        var words = WordManager.Singleton;
        if (!Enabled || words == null || !EnglishTokenBranch.ShouldForce(words.Open, txt)) return;

        words.Open = true;
        __state = true;
    }

    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state) WordManager.Singleton.Open = false;
        return __exception;
    }
}
