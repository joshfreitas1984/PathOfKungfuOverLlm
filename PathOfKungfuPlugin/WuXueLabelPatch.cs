using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PathOfKungfuPlugin;

/// <summary>
/// The martial arts screen labels its buttons from the names of its English enums ("FistAndPalm", "HeavyBlade", "LightAttack"), not from Data_Word,
/// so they show the game's own English. Data_Word 40 to 54 hold our names for the weapon types in the same order as the game's enums
/// (拳掌, 单手轻刃, ... 短兵), and 288 to 292 the five basic techniques (格挡, 闪避, 纵跃, 轻击, 重击). These patches set the labels from those.
/// Only the label text changes; the object names the game looks up stay as they are.
/// </summary>
internal static class WuXueLabels
{
    internal const int WeaponTypeWord = 40;
    internal const int BasicTechniqueWord = 288;

    // The names the game's English enums and replace chains produce, with the Data_Word id of our name for each.
    private static readonly Dictionary<string, int> ByGameEnglish = new()
    {
        ["FistAndPalm"] = 40, ["Blade"] = 41, ["HeavyBlade"] = 42, ["Polearm"] = 43, ["Fan"] = 44, ["Throwing"] = 45, ["Internal"] = 46,
        ["Lightness"] = 47, ["Kicks"] = 48, ["TwoBlade"] = 49, ["RopeDart"] = 50, ["Archery"] = 51, ["SoftWhip"] = 52, ["Firearms"] = 53, ["Short"] = 54,
        ["Block"] = 288, ["Dodge"] = 289, ["Evasion"] = 289, ["Leap"] = 290, ["LightAttack"] = 291, ["LightHit"] = 291, ["HeavyAttack"] = 292, ["HeavyHit"] = 292,
    };

    internal static bool English => WordManager.Singleton != null && WordManager.Singleton.Open;

    internal static string Word(int id)
    {
        var word = WordManager.Singleton.GetWord(id);
        return string.IsNullOrWhiteSpace(word) ? null : word.Trim();
    }

    internal static string FromGameEnglish(string gameEnglish) =>
        gameEnglish != null && ByGameEnglish.TryGetValue(gameEnglish, out var id) ? Word(id) : null;

    internal static void SetLabel(Transform parent, string path, string text)
    {
        var label = path.Length == 0 ? parent : parent.Find(path);
        var component = label == null ? null : label.GetComponent<Text>();
        if (component != null && text != null) component.text = text;
    }
}

/// <summary>Weapon type buttons (拳掌 ... 短兵, and 内功 and 轻功): the label is set to the object's English enum name.</summary>
[HarmonyPatch(typeof(UIWuXueNew), "InitItem_YanBen")]
internal static class WuXueWeaponTypePatch
{
    private static void Postfix(Transform go, int i)
    {
        try
        {
            if (!WuXueLabels.English || i < 0 || i > 14) return;

            var text = WuXueLabels.Word(WuXueLabels.WeaponTypeWord + i);
            var label = go.GetComponentInChildren<Text>();
            if (label != null && text != null) label.text = text;
        }
        catch (Exception ex) { Plugin.Logger.LogError($"Weapon type labels failed: {ex}"); }
    }
}

/// <summary>General technique list (13 entries, weapon types skipping 内功 and 轻功): index 0 to 5 are the weapon types 0 to 5, then 8 to 14.</summary>
[HarmonyPatch(typeof(UIWuXueNew), "InitMainList_Tongshi")]
internal static class WuXueGeneralTechniquePatch
{
    private static void Postfix(Transform ___content_tongshi)
    {
        try
        {
            if (!WuXueLabels.English || ___content_tongshi == null) return;

            for (var i = 0; i < ___content_tongshi.childCount && i < 13; i++)
            {
                var weaponType = i <= 5 ? i : i + 2;
                var label = ___content_tongshi.GetChild(i).GetComponentInChildren<Text>();
                var text = WuXueLabels.Word(WuXueLabels.WeaponTypeWord + weaponType);
                if (label != null && text != null) label.text = text;
            }
        }
        catch (Exception ex) { Plugin.Logger.LogError($"General technique labels failed: {ex}"); }
    }
}

/// <summary>The five basic technique buttons (格挡, 闪避, 纵跃, 轻击, 重击).</summary>
[HarmonyPatch(typeof(UIWuXueNew), "InitMainList_Sanxue")]
internal static class WuXueBasicTechniquePatch
{
    private static void Postfix(Transform ___content_sanxue)
    {
        try
        {
            if (!WuXueLabels.English || ___content_sanxue == null) return;

            for (var i = 0; i < ___content_sanxue.childCount && i < 5; i++)
            {
                var label = ___content_sanxue.GetChild(i).GetComponentInChildren<Text>();
                var text = WuXueLabels.Word(WuXueLabels.BasicTechniqueWord + i);
                if (label != null && text != null) label.text = text;
            }
        }
        catch (Exception ex) { Plugin.Logger.LogError($"Basic technique labels failed: {ex}"); }
    }
}

/// <summary>
/// Right-hand lists (general techniques, basic techniques) are given the game's English name as a parameter and put it in the row's "name" text
/// and in the selected row's. A name that is one of the game's enum names is replaced; skill names from the data are left alone.
/// </summary>
[HarmonyPatch(typeof(UIWuXueNew), "SXAndTSRightListCallback")]
internal static class WuXueRightListPatch
{
    private static void Postfix(Transform item, string name)
    {
        try
        {
            if (!WuXueLabels.English) return;

            var text = WuXueLabels.FromGameEnglish(name);
            if (text == null) return;

            var row = item.FindChildByName("name");
            var rowText = row == null ? null : row.GetComponent<Text>();
            if (rowText != null) rowText.text = text;

            var select = item.FindChildByName("Select");
            if (select != null) WuXueLabels.SetLabel(select.transform, "name", text);
        }
        catch (Exception ex) { Plugin.Logger.LogError($"Technique list labels failed: {ex}"); }
    }
}

/// <summary>Diagnostic: the tutorial's click on the martial arts "use" button throws a NullReferenceException inside SaveWxData. Logs which of its inputs is null.</summary>
[HarmonyPatch(typeof(UIWuXueNew), "SaveWxData")]
internal static class WuXueSaveDiagnosticPatch
{
    private static void Prefix(UIWuXueNew __instance, PlayerData ___playerData, bool initXinjue)
    {
        try
        {
            var global = Singleton<GDataManager>.Instance == null ? null : Singleton<GDataManager>.Instance.playerData;
            Plugin.Logger.LogInfo(
                $"SaveWxData(initXinjue={initXinjue}) isPlayer={__instance.isPlayer} this.playerData={(___playerData == null ? "NULL" : "ok")} " +
                $"gdata.playerData={(global == null ? "NULL" : "ok")} gdata.m_myWxSkillList={(global?.m_myWxSkillList == null ? "NULL" : "ok")} " +
                $"gdata.m_equipXJOfWuxue={(global?.m_equipXJOfWuxue == null ? "NULL" : "ok")} " +
                $"this.m_allSkillList={(___playerData?.m_allSkillList == null ? "NULL" : "ok")} this.m_tongyongWuXue={(___playerData?.m_tongyongWuXue == null ? "NULL" : "ok")} " +
                $"this.m_myNxSkillList={(___playerData?.m_myNxSkillList == null ? "NULL" : "ok")} this.m_myQgSkillList={(___playerData?.m_myQgSkillList == null ? "NULL" : "ok")} " +
                $"this.m_myWxSkillList={(___playerData?.m_myWxSkillList == null ? "NULL" : "ok")} UIManager={(UIManager.Instance == null ? "NULL" : "ok")} " +
                $"WordManager={(WordManager.Singleton == null ? "NULL" : "ok")}");
        }
        catch (Exception ex) { Plugin.Logger.LogError($"SaveWxData diagnostic failed: {ex}"); }
    }
}

/// <summary>Diagnostic: SaveWxData's only remaining unguarded dereference is itemName.Replace inside ShowGetItemAddToList (inlined, so it shows in SaveWxData's stack). Logs the name it receives.</summary>
[HarmonyPatch(typeof(UIManager), "ShowGetItemAddToList")]
internal static class ShowGetItemDiagnosticPatch
{
    private static void Prefix(string itemName) =>
        Plugin.Logger.LogInfo($"ShowGetItemAddToList itemName={(itemName == null ? "NULL" : $"\"{itemName}\"")}");
}
