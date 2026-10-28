using System;
using System.Collections.Generic;
using GProject.Data;
using HarmonyLib;

namespace PathOfKungfuPlugin;

/// <summary>
/// When a quest ends, the game saves the "next quest" hint of the quests it unlocks as "title_description" in m_zhiYingTipShow
/// (TaskManager.DongNextTaskShow, from the finished quest's zhiYingTaskTitle/zhiYingTaskContent columns) and never rebuilds it. A save made
/// before a translation fix therefore keeps showing the old description in the quest log for ever. When a game starts, this rebuilds each
/// saved hint from the current table text, the same way the game builds it.
/// </summary>
[HarmonyPatch(typeof(WorldEvolutionManager), "InitData")]
internal static class SavedZhiYingTipRefresh
{
    private static void Postfix()
    {
        try
        {
            var gData = Singleton<GDataManager>.Instance;
            var saved = gData.playerData?.m_zhiYingTipShow;
            var tasks = gData.dicTask;
            if (saved == null || saved.Count == 0 || tasks == null) return;

            var current = new Dictionary<long, string>();
            foreach (var task in tasks.Values)
            {
                Collect(current, task.zhiYingId, task.zhiYingTaskTitle, task.zhiYingTaskContent);
                Collect(current, task.zhiYingId2, task.zhiYingTaskTitle2, task.zhiYingTaskContent2);
                Collect(current, task.zhiYingId3, task.zhiYingTaskTitle3, task.zhiYingTaskContent3);
            }

            var changed = 0;
            foreach (var key in new List<long>(saved.Keys))
                if (current.TryGetValue(key, out var text) && saved[key] != text)
                {
                    Plugin.Logger.LogInfo($"Quest hint {key}: \"{saved[key]}\" -> \"{text}\"");
                    saved[key] = text;
                    changed++;
                }

            if (changed > 0) Plugin.Logger.LogInfo($"Refreshed {changed} saved quest hints from the task table.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Saved quest hint refresh failed: {ex}");
        }
    }

    private static void Collect(Dictionary<long, string> current, string? ids, string? titles, string? contents)
    {
        if (string.IsNullOrEmpty(ids) || string.IsNullOrEmpty(titles) || string.IsNullOrEmpty(contents)) return;

        var idList = ids.ToStringList_2();
        var titleList = titles.ToStringList_2();
        var contentList = contents.ToStringList_2();
        for (var i = 0; i < idList.Count; i++)
        {
            if (!long.TryParse(idList[i], out var id) || id == 0) continue;
            current[id] = (titleList.Count > i ? titleList[i] : string.Empty) + "_" + (contentList.Count > i ? contentList[i] : string.Empty);
        }
    }
}
