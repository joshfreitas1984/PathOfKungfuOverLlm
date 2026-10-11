using HarmonyLib;

namespace PathOfKungfuPlugin;

/// <summary>
/// Replaces the Western random NPC name pool (WorldEvolution/name2 in non-Chinese languages, name in Chinese) with the
/// pinyin pool built by Files/NamePools/GeneratePinyinNames.py, and keeps a space between surname and given name.
/// The game builds a full name as plain surname + given name, so the space is added where the name is built and
/// kept out of the stored nameX.
/// </summary>
[HarmonyPatch(typeof(GDataManager), nameof(GDataManager.InitNameData))]
internal static class NamePoolPatch
{
    private static void Postfix(GDataManager __instance)
    {
        if (!NamePools.Enabled) return;
        NamePools.EnsureLoaded();
        var pool = NamePools.Pinyin;
        if (pool == null || pool.Count == 0 || ReferenceEquals(__instance.m_anmeDataDic, pool)) return;
        __instance.m_anmeDataDic = pool;
    }
}

/// <summary>Callers join the tuple as Item1 + Item2 (and keep that as the NPC name), so the space goes after the surname.</summary>
[HarmonyPatch(typeof(GDataManager), nameof(GDataManager.GetARangeName))]
internal static class RandomNameSpacePatch
{
    private static void Postfix(GDataManager __instance, ref (string, string) __result)
    {
        if (!ReferenceEquals(__instance.m_anmeDataDic, NamePools.Pinyin)) return;
        if (string.IsNullOrEmpty(__result.Item1) || string.IsNullOrEmpty(__result.Item2)) return;
        __result = (__result.Item1 + " ", __result.Item2);
    }
}

/// <summary>nameX is stored without the trailing space so it stays clean when shown or reused on its own.</summary>
[HarmonyPatch(typeof(NPCData), nameof(NPCData.nameX), MethodType.Setter)]
internal static class NameXTrimPatch
{
    private static void Prefix(ref string value)
    {
        if (value != null && NamePools.Enabled) value = value.TrimEnd();
    }
}

/// <summary>Places that set name = nameX + nameM (children, renames, spouses...) get the space.</summary>
[HarmonyPatch(typeof(NPCData), nameof(NPCData.name), MethodType.Setter)]
internal static class NameSetterSpacePatch
{
    private static void Prefix(NPCData __instance, ref string value)
    {
        if (NamePools.Enabled) value = SpacedName.Fix(__instance, value);
    }
}

[HarmonyPatch(typeof(NPCData), nameof(NPCData.GetOriginName))]
internal static class OriginNameSpacePatch
{
    private static void Postfix(NPCData __instance, ref string __result)
    {
        if (NamePools.Enabled) __result = SpacedName.Fix(__instance, __result);
    }
}

internal static class SpacedName
{
    /// <summary>Returns nameX + " " + nameM when <paramref name="name"/> is exactly nameX + nameM, otherwise unchanged.</summary>
    internal static string Fix(NPCData npc, string name)
    {
        var x = npc.nameX;
        var m = npc.nameM;
        if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(m) || name != x + m) return name;
        return x + " " + m;
    }
}
