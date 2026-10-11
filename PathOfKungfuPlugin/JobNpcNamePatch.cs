using System.Collections.Generic;
using GProject;
using GProject.Data;
using HarmonyLib;
using Manager;

namespace PathOfKungfuPlugin;

/// <summary>
/// City job NPCs (the shopkeeper) are created by WorldEvolutionManager.InitJob, which always reads the Chinese name sheet
/// and appends the Chinese job name, so the NPC was always "赵晨(掌柜)". This replaces InitJob with the
/// same logic but a pinyin name and a translated job name, in every language.
/// </summary>
[HarmonyPatch(typeof(WorldEvolutionManager), "InitJob")]
internal static class JobNpcNamePatch
{
    // The job sheet has one row and no English variant. Move this to the translation pipeline if it grows.
    private static readonly Dictionary<string, string> JobNames = new() { ["(掌柜)"] = "(Shopkeeper)" };

    internal static string JobName(string chinese) => JobNames.TryGetValue(chinese, out var english) ? english : chinese;

    private static bool Prefix()
    {
        if (!NamePools.Enabled) return true;
        NamePools.EnsureLoaded();
        if (NamePools.PinyinSurnames.Count == 0 || NamePools.PinyinMale.Count == 0) return true;

        var gData = Singleton<GDataManager>.Instance;
        foreach (var item in gData.m_jobDataDic)
        {
            var surname = NamePools.PinyinSurnames[UnityEngine.Random.Range(0, NamePools.PinyinSurnames.Count)];
            var given = NamePools.PinyinMale[UnityEngine.Random.Range(0, NamePools.PinyinMale.Count)];
            NpcTempData npcData = new NpcTempData
            {
                id = Singleton<NpcDataManager>.Instance.GetNextNpcId(),
                name = $"{surname} {given} {JobName(item.Value.name)}",
                sex = 1,
                attribute = 1,
                map = item.Value.mapId,
                point = FunTool.GetPoint(item.Value.point)
            };
            NPCData temporarilyNpcData = Singleton<NpcManager>.Instance.GetTemporarilyNpcData(npcData);
            temporarilyNpcData.gang = 0;
            temporarilyNpcData.dialogID = -1;
            temporarilyNpcData.JiaoQing = new Dictionary<string, List<int>>();
            temporarilyNpcData.m_jiaoQing = new JiaoQingData();
            temporarilyNpcData.dialogTsID = item.Value.dialogId;
            temporarilyNpcData.avatarName = string.IsNullOrEmpty(item.Value.avatar) ? temporarilyNpcData.avatarName : item.Value.avatar;
            gData.playerData.m_nPCDataDic.Add(temporarilyNpcData.id, temporarilyNpcData);
        }

        return false;
    }

    /// <summary>Converts a saved job NPC name "赵晨(掌柜)" to "Zhao Chen (Shopkeeper)"; null when it is not one.</summary>
    internal static string? ConvertSavedName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var job in JobNames)
        {
            if (!name!.EndsWith(job.Key)) continue;
            var person = name.Substring(0, name.Length - job.Key.Length);
            // Surnames are one or two characters; the rest must be a known given name.
            for (var length = System.Math.Min(2, person.Length - 1); length >= 1; length--)
            {
                if (!NamePools.ChineseSurnames.TryGetValue(person.Substring(0, length), out var surnameId)) continue;
                if (!NamePools.ChineseMale.TryGetValue(person.Substring(length), out var givenId)) continue;
                var pool = NamePools.Pinyin;
                if (pool == null || !pool.TryGetValue(surnameId, out var surname) || !pool.TryGetValue(givenId, out var given)) continue;
                if (string.IsNullOrEmpty(surname.surnam) || string.IsNullOrEmpty(given.male)) continue;
                return $"{surname.surnam} {given.male} {job.Value}";
            }
        }

        return null;
    }
}
