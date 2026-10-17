using HarmonyLib;
using System;

namespace PathOfKungfuPlugin;

/// <summary>
/// The game marks a dead NPC by looking for 尸体 / 尸首 / 屍首 (or its own English typo "corse") in the Touch or NPC name: it greys the name,
/// and blocks talking to, recruiting and fighting alongside the NPC. Touch names are translated here ("Mao Mao (corpse)", "Nameless corpse"), so
/// neither the Chinese words nor "corse" match. The Chinese literals are left untranslated (DynamicStringsDoNotTranslate.csv); this adds the
/// English wording the translation uses.
/// </summary>
[HarmonyPatch(typeof(StringTool), nameof(StringTool.IsContainsShiTi))]
internal static class CorpseNamePatch
{
    private static void Postfix(string str, ref bool __result)
    {
        if (__result || string.IsNullOrEmpty(str)) return;

        __result = str.IndexOf("corpse", StringComparison.OrdinalIgnoreCase) >= 0
            || str.IndexOf("dead body", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
