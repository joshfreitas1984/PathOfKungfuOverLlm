using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace PathOfKungfuPlugin;

/// <summary>
/// A character's aptitude titles ("天才", "奇才") are rolled once and kept in the save (m_chengWeiNowData). In English mode the game rolls them from
/// the chengWei2 column ("Top", "Ace"), so a character made in Chinese mode keeps the Chinese title and the attribute screen prints it. When a game
/// starts in English mode, this turns each saved Chinese title into the English one from the same position in the aptitude table, for the player and
/// every NPC. The titles come from the table, so a game update that renames them needs no change here.
/// </summary>
[HarmonyPatch(typeof(WorldEvolutionManager), "InitData")]
internal static class SavedAptitudeTitleMigration
{
    private static void Postfix()
    {
        try
        {
            if (WordManager.Singleton == null || !WordManager.Singleton.Open) return;
            var gData = Singleton<GDataManager>.Instance;
            var playerData = gData.playerData;
            if (playerData == null || gData.dicAptitudeData == null) return;

            var english = new Dictionary<string, string>();
            foreach (var row in gData.dicAptitudeData.Values)
            {
                var chinese = (row.chengWei ?? string.Empty).Split(';');
                var translated = (row.chengWei2 ?? string.Empty).Split(';');
                for (var i = 0; i < Math.Min(chinese.Length, translated.Length); i++)
                    if (chinese[i].Length > 0 && translated[i].Length > 0 && chinese[i] != translated[i])
                        english[chinese[i]] = translated[i];
            }

            if (english.Count == 0) return;

            var changed = Convert(playerData.m_chengWeiNowData, english);
            var seen = new HashSet<NPCData>();
            foreach (var npc in SavedNameMigration.AllNpcs(playerData))
                if (seen.Add(npc)) changed += Convert(npc.m_chengWeiNowData, english);

            if (changed > 0) Plugin.Logger.LogInfo($"Converted {changed} saved aptitude titles to English.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Saved aptitude title conversion failed: {ex}");
        }
    }

    private static int Convert(List<string> titles, Dictionary<string, string> english)
    {
        if (titles == null) return 0;

        var changed = 0;
        for (var i = 0; i < titles.Count; i++)
            if (titles[i] != null && english.TryGetValue(titles[i], out var title))
            {
                titles[i] = title;
                changed++;
            }

        return changed;
    }
}
