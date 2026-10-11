using HarmonyLib;

namespace PathOfKungfuPlugin;

/// <summary>
/// The naming screen for a newborn child (UINpcNamed, also used by the mod editor) only accepts Chinese characters
/// unless the game is in a non-Chinese language, and its fields may be too short for pinyin (Shangguan).
/// </summary>
[HarmonyPatch(typeof(UINpcNamed), nameof(UINpcNamed.CheckChinese))]
internal static class AllowLatinNamePatch
{
    private static void Postfix(string content, ref string __result)
    {
        if (NamePools.Enabled) __result = content ?? string.Empty;
    }
}

[HarmonyPatch(typeof(UINpcNamed), "InitView")]
internal static class NameFieldLengthPatch
{
    private const int MinCharacterLimit = 24;

    private static void Postfix(UINpcNamed __instance)
    {
        if (!NamePools.Enabled) return;
        foreach (var field in new[] { __instance.inputField_nameX, __instance.inputField_nameM })
            if (field != null && field.characterLimit != 0 && field.characterLimit < MinCharacterLimit)
                field.characterLimit = MinCharacterLimit;
    }
}

/// <summary>The random button fills the fields from GetARangeName, whose surname carries the join space; keep it out of the field.</summary>
[HarmonyPatch(typeof(UINpcNamed), "OnClickRandom")]
internal static class RandomChildNamePatch
{
    private static void Postfix(UINpcNamed __instance)
    {
        if (!NamePools.Enabled) return;
        __instance.inputField_nameX.text = __instance.inputField_nameX.text.TrimEnd();
        __instance.inputField_nameM.text = __instance.inputField_nameM.text.TrimEnd();
    }
}

/// <summary>The duplicate check compares surname + given name with NPC names, which now have a space between the parts.</summary>
[HarmonyPatch(typeof(NpcSpawnManager), nameof(NpcSpawnManager.CheckNoSameName))]
internal static class SameNameCheckPatch
{
    private static void Prefix(ref string sX, string sM)
    {
        if (NamePools.Enabled && !string.IsNullOrEmpty(sX) && !string.IsNullOrEmpty(sM)) sX = sX.TrimEnd() + " ";
    }
}
