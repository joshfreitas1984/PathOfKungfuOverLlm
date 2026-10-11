using System;
using HarmonyLib;

namespace PathOfKungfuPlugin;

/// <summary>
/// When a new game starts, the game copies every story NPC's name from the Touch table into the save once
/// (WorldEvolutionManager.MakeMasterData) and never refreshes it. A save made before the translation was installed
/// therefore keeps the game's own English ("The Coon Lady") for ever. When a game starts, this sets those NPCs to the
/// current table name.
/// Only NPCs with no surname or given name (the naming system never touched them) are changed, and never mod NPCs or
/// gym guards, whose names the game decorates itself.
/// </summary>
[HarmonyPatch(typeof(WorldEvolutionManager), "InitData")]
internal static class StoryNpcNameRefresh
{
    internal static bool Enabled { get; set; } = true;

    private static void Postfix()
    {
        if (!Enabled) return;
        try
        {
            var gData = Singleton<GDataManager>.Instance;
            var story = gData.playerData?.m_touch;
            var table = gData.dicTouchData;
            if (story == null || table == null) return;

            var changed = 0;
            foreach (var pair in story)
            {
                var npc = pair.Value;
                if (npc == null || npc.isModNpc || npc.isGymsGuarder) continue;
                if (!string.IsNullOrEmpty(npc.nameX) || !string.IsNullOrEmpty(npc.nameM)) continue;
                if (!table.TryGetValue(npc.touchId, out var touch) || string.IsNullOrEmpty(touch.name)) continue;
                if (npc.name == touch.name) continue;

                if (changed < 5) Plugin.Logger.LogInfo($"Story NPC {npc.touchId}: \"{npc.name}\" -> \"{touch.name}\"");
                npc.name = touch.name;
                changed++;
            }

            if (changed > 0) Plugin.Logger.LogInfo($"Refreshed {changed} story NPC names from the Touch table.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Story NPC name refresh failed: {ex}");
        }
    }
}
