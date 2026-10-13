using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace PathOfKungfuPlugin;

/// <summary>
/// Saves keep the names NPCs were generated with: an English-mode save from before the pinyin pool has Western names
/// (Smith, James) and a Chinese-mode save has Chinese ones. When a game starts, NPCs whose surname and given name are
/// both from the old Western pool or the Chinese pool get pinyin names instead.
/// Western surnames map one to one, so families stay together; Western given names are picked from the whole pool using
/// the NPC id, so the same save always converts the same way. Chinese names become the pinyin of the same pool row.
/// Names are written back into the NPC, so the next save keeps them. Names stored as text elsewhere (task logs,
/// messages) are not touched.
/// </summary>
[HarmonyPatch(typeof(WorldEvolutionManager), "InitData")]
internal static class SavedNameMigration
{
    // Coprime to the pool sizes (337, 1422, 1395), so distinct Western surnames map to distinct pinyin surnames.
    private const int SurnameStride = 131;

    private static void Postfix()
    {
        try
        {
            if (!NamePools.Enabled) return;
            var playerData = Singleton<GDataManager>.Instance.playerData;
            if (playerData == null) return;
            NamePools.EnsureLoaded();
            if (NamePools.PinyinSurnames.Count == 0) return;

            var seen = new HashSet<NPCData>();
            var changed = 0;
            foreach (var npc in AllNpcs(playerData))
                if (seen.Add(npc) && Migrate(npc)) changed++;
            if (changed > 0) Plugin.Logger.LogInfo($"Converted {changed} saved NPC names from the Western pool to pinyin.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Saved name conversion failed: {ex}");
        }
    }

    internal static bool Migrate(NPCData npc)
    {
        var oldX = npc.nameX;
        var oldM = npc.nameM;
        if (string.IsNullOrEmpty(oldX) && string.IsNullOrEmpty(oldM))
        {
            // City job NPCs only have a name, built from the Chinese pool ("赵晨(掌柜)").
            var converted = JobNpcNamePatch.ConvertSavedName(npc.name);
            if (converted == null) return false;
            npc.name = converted;
            return true;
        }

        if (string.IsNullOrEmpty(oldX) || string.IsNullOrEmpty(oldM)) return false;

        var male = npc.sex == 1;
        var given = male ? NamePools.PinyinMale : NamePools.PinyinFemale;
        string newX, newM;
        if (NamePools.WesternSurnames.TryGetValue(oldX, out var surnameRank)
            && (male ? NamePools.WesternMale : NamePools.WesternFemale).ContainsKey(oldM))
        {
            // Generated in English mode before the pinyin pool.
            newX = NamePools.PinyinSurnames[(surnameRank + 1) * SurnameStride % NamePools.PinyinSurnames.Count];
            newM = given[new Random(unchecked(npc.id * 7919 + 17)).Next(given.Count)];
        }
        else if (!npc.IsJuQing()
                 && NamePools.ChineseSurnames.TryGetValue(oldX, out var surnameId)
                 && (male ? NamePools.ChineseMale : NamePools.ChineseFemale).TryGetValue(oldM, out var givenId)
                 && NamePools.Pinyin != null
                 && NamePools.Pinyin.TryGetValue(surnameId, out var surnameRow)
                 && NamePools.Pinyin.TryGetValue(givenId, out var givenRow)
                 && !string.IsNullOrEmpty(surnameRow.surnam)
                 && !string.IsNullOrEmpty(male ? givenRow.male : givenRow.female))
        {
            // Generated in Chinese mode: the exact pinyin of the same pool row.
            newX = surnameRow.surnam;
            newM = male ? givenRow.male : givenRow.female;
        }
        else
        {
            return false;
        }

        var oldFull = oldX + oldM;
        var newFull = newX + " " + newM;
        var name = npc.name;
        npc.nameX = newX;
        npc.nameM = newM;
        // Keep anything the game added around the name (gym titles and so on).
        npc.name = !string.IsNullOrEmpty(name) && name.Contains(oldFull) ? name.Replace(oldFull, newFull) : newFull;
        return true;
    }

    /// <summary>Every NPCData held by the save: dictionaries, lists and single NPCs on PlayerData.</summary>
    internal static IEnumerable<NPCData> AllNpcs(PlayerData playerData)
    {
        foreach (var property in typeof(PlayerData).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            // Read/write properties are the saved state; read-only ones are derived (and may be expensive).
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0) continue;
            var value = property.GetValue(playerData, null);
            switch (value)
            {
                case NPCData single:
                    yield return single;
                    break;
                case IDictionary dictionary:
                    foreach (var item in dictionary.Values)
                        if (item is NPCData npc) yield return npc;
                    break;
                case IEnumerable list when value is not string:
                    foreach (var item in list)
                        if (item is NPCData npc) yield return npc;
                    break;
            }
        }
    }
}
