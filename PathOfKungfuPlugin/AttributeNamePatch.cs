using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;

namespace PathOfKungfuPlugin;

/// <summary>
/// Item data names attributes by key ("攻击力+2;防御力+8" on equipment, "精力+6" on food). The game also parses those keys, so the data cannot be
/// translated; it translates them for display through StringExtensions.TranslateAttribute, with its own English ("PhysicalAttack", "HPMAX") and
/// no entry at all for 攻击力, 防御力, 气血回复 or 精力. The names here follow the glossary (Files/Glossary/Stats.yaml and the preset's CommonStats).
/// </summary>
internal static class AttributeNames
{
    // Longest first, so 外功攻击力 is not cut short by 外功攻击 and 气血上限 is not cut short by 气血.
    private static readonly (string Chinese, string English)[] Names = new (string, string)[]
    {
        ("武器基础值", "Weapon Base"), ("武力", "Might"), ("筋骨", "Physique"), ("身法", "Agility"), ("内息", "Inner Qi"), ("悟性", "Comprehension"),
        ("气血上限", "Max Lifeforce"), ("气血恢复", "Lifeforce Recovery"), ("气血回复", "Lifeforce Recovery"), ("气血", "Lifeforce"),
        ("真气上限", "Max True Qi"), ("真气", "True Qi"), ("体力恢复", "Stamina Recovery"), ("精力", "Energy"),
        ("命中", "Accuracy"), ("闪避", "Dodge"), ("暴击", "Critical Hit"), ("免暴", "Crit Immunity"), ("内劲增长", "Inner Force Gain"),
        ("移动", "Movement"), ("吸血", "Lifesteal"), ("反伤", "Damage Reflection"),
        ("外功攻击力", "External Attack Power"), ("内功攻击力", "Internal Attack Power"), ("外功攻击", "External Attack"), ("外功防御", "External Defense"),
        ("内功攻击", "Internal Attack"), ("内功防御", "Internal Defense"), ("攻击力", "Attack Power"), ("防御力", "Defense"),
        ("外功防御力", "External Defense"), ("内功防御力", "Internal Defense"), ("攻击", "Attack"), ("防御", "Defense"),
        ("体力上限", "Max Stamina"), ("体力回复", "Stamina Recovery"), ("暴击率", "Crit Rate"), ("暴击伤害", "Crit Damage"), ("暴击减免", "Crit Reduction"),
        ("速度", "Speed"), ("移动速度", "Movement Speed"), ("攻击速度", "Attack Speed"),
    }.OrderByDescending(n => n.Item1.Length).ToArray();

    internal static bool IsAttributeName(string chinese) => Names.Any(n => n.Chinese == chinese);

    internal static string Translate(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        foreach (var (chinese, english) in Names)
            text = text.Replace(chinese, english);

        return text;
    }
}

[HarmonyPatch(typeof(StringExtensions), nameof(StringExtensions.TranslateAttribute))]
internal static class AttributeNamePatch
{
    // Chinese mode keeps the game's own behaviour (it returns the text unchanged).
    private static bool Prefix(string original, ref string __result)
    {
        if (WordManager.Singleton == null || !WordManager.Singleton.Open) return true;

        __result = AttributeNames.Translate(original);
        return false;
    }
}

/// <summary>
/// The life skill (cooking, crafting) screen puts the raw 使用效果 / 主属性 of the selected item into Tip1 without translating it
/// ("精力+6", "攻击力+32"), so translate the text it set.
/// </summary>
[HarmonyPatch(typeof(UILiftSkill), "ShowItem")]
internal static class LifeSkillTipPatch
{
    private static void Postfix(Text ___Tip1)
    {
        if (___Tip1 == null || string.IsNullOrEmpty(___Tip1.text)) return;

        ___Tip1.text = AttributeNames.Translate(___Tip1.text);
    }
}

/// <summary>
/// The skill tooltip appends the raw SkillGrowData.effect ("气血上限+2419") to its "Bonus Attribute: " label in English mode, without translating it.
/// </summary>
[HarmonyPatch(typeof(TipShowWinSkill), "UpdateInfo")]
internal static class SkillTipBonusAttributePatch
{
    private static void Postfix(Text ___txt_AddAttr)
    {
        if (___txt_AddAttr == null || string.IsNullOrEmpty(___txt_AddAttr.text)) return;

        ___txt_AddAttr.text = AttributeNames.Translate(___txt_AddAttr.text);
    }
}

/// <summary>
/// The martial arts screen writes the cultivation bonus ("气血上限+1680") into txt_wxJiacheng from the raw SkillGrowData.effect, untranslated.
/// </summary>
[HarmonyPatch(typeof(UIWuXueNew), "RefreshWxXiuwei")]
internal static class WuXueBonusAttributePatch
{
    private static void Postfix(Text ___txt_wxJiacheng)
    {
        if (___txt_wxJiacheng == null || string.IsNullOrEmpty(___txt_wxJiacheng.text)) return;

        ___txt_wxJiacheng.text = AttributeNames.Translate(___txt_wxJiacheng.text);
    }
}

/// <summary>
/// Breaking through a meridian opens UITipContent with the raw JingLuo collaterals tip ("内功防御+5"), a key the game parses elsewhere, untranslated.
/// </summary>
[HarmonyPatch(typeof(UI.PopWin.UITipContent), nameof(UI.PopWin.UITipContent.SetContent))]
internal static class TipContentAttributePatch
{
    private static void Prefix(ref string content)
    {
        if (WordManager.Singleton == null || !WordManager.Singleton.Open) return;

        content = AttributeNames.Translate(content);
    }
}

/// <summary>
/// The meridian and acupoint data (JingLuo auxiliary/acupoint/collaterals tip columns) are raw attribute keys ("内功防御+13;气血上限+5"). The game
/// translates a few of them for English, and only some of the screens, so translate every tooltip and text that shows them.
/// </summary>
internal static class MeridianTipTexts
{
    internal static bool English => WordManager.Singleton != null && WordManager.Singleton.Open;

    internal static void Translate(Text text)
    {
        if (text != null && !string.IsNullOrEmpty(text.text))
            text.text = AttributeNames.Translate(text.text);
    }

    internal static void TranslateAll(Transform root)
    {
        if (root == null) return;

        foreach (var text in root.GetComponentsInChildren<Text>(true))
            Translate(text);
    }
}

/// <summary>
/// The acupoint screens and tooltips run the raw tip through a chain of .Replace("内功攻击", "ATTACK")...("气血上限", "Max HP"), the game's own
/// partial English, before anything else can see it. Turn those calls into no-ops so the Chinese reaches the patches below and gets the glossary names.
/// </summary>
[HarmonyPatch]
internal static class GameEnglishAttributeReplaceRemover
{
    private static readonly Type[] Types =
    {
        typeof(AcupointItem), typeof(UIAcupointItem), typeof(UIAcupoint), typeof(UI.PopWin.UITipChongHoleFinish), typeof(UIJingLuoMinGame), typeof(TipShowWin),
    };

    private static readonly MethodInfo Replace = AccessTools.Method(typeof(string), nameof(string.Replace), new[] { typeof(string), typeof(string) });
    private static readonly MethodInfo Keep = AccessTools.Method(typeof(GameEnglishAttributeReplaceRemover), nameof(KeepText));

    private static IEnumerable<MethodBase> TargetMethods() => Types.SelectMany(AccessTools.GetDeclaredMethods).Cast<MethodBase>();

    private static string KeepText(string text, string search, string replacement) => text;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        for (var i = 2; i < codes.Count; i++)
        {
            if (!codes[i].Calls(Replace) || codes[i - 2].opcode != OpCodes.Ldstr || codes[i - 1].opcode != OpCodes.Ldstr) continue;

            if (codes[i - 2].operand is string search && AttributeNames.IsAttributeName(search))
                codes[i] = new CodeInstruction(OpCodes.Call, Keep);
        }

        return codes;
    }
}

/// <summary>The acupoint tooltip's own replaceStr ("Defend", "Brit", "En Rec"): use the glossary names instead.</summary>
[HarmonyPatch(typeof(TipShowWinAcupoint), nameof(TipShowWinAcupoint.replaceStr))]
internal static class AcupointTipReplaceStrPatch
{
    private static bool Prefix(string str, ref string __result)
    {
        if (!MeridianTipTexts.English) return true;

        __result = AttributeNames.Translate(str);
        return false;
    }
}

/// <summary>The item tooltip lists a food's raw 使用效果 names ("精力:") as row titles, untranslated.</summary>
[HarmonyPatch(typeof(TipShowWinNewPanel), "SetItemUse")]
internal static class ItemUseTitlePatch
{
    private static void Postfix(Transform ___trans_Base)
    {
        if (!MeridianTipTexts.English || ___trans_Base == null) return;

        foreach (Transform row in ___trans_Base)
            MeridianTipTexts.Translate(row.FindChildByName("txt_Title")?.GetComponent<Text>());
    }
}

/// <summary>Every CommonTips tooltip: acupoint and meridian rows show the raw tip as content and content1-3.</summary>
[HarmonyPatch(typeof(CommonTips), nameof(CommonTips.SetUiData))]
internal static class CommonTipsAttributePatch
{
    private static void Prefix(TipsShowContect data)
    {
        if (data == null || !MeridianTipTexts.English) return;

        data.m_content = AttributeNames.Translate(data.m_content);
        data.m_content1 = AttributeNames.Translate(data.m_content1);
        data.m_content2 = AttributeNames.Translate(data.m_content2);
        data.m_content3 = AttributeNames.Translate(data.m_content3);
    }
}

/// <summary>The meridian mini game's acupoint nodes fire their raw auxiliary tip to the tooltip event on hover.</summary>
[HarmonyPatch(typeof(UIQiZiItem), nameof(UIQiZiItem.SetUiData))]
internal static class QiZiItemAttributePatch
{
    private static void Prefix(ref string tip)
    {
        if (MeridianTipTexts.English) tip = AttributeNames.Translate(tip);
    }
}

[HarmonyPatch(typeof(UIAcupoint), "ShowAcupointChongHoleView")]
internal static class AcupointChongHoleViewAttributePatch
{
    private static void Postfix(UIAcupoint __instance, bool isShow, Transform chongHoleTrans, Transform fxChongHoleTrans,
        Transform ___m_chongHoleContentTrans, Transform ___m_fxChongHoleContentTrans)
    {
        if (!MeridianTipTexts.English) return;

        MeridianTipTexts.TranslateAll(isShow ? chongHoleTrans : ___m_chongHoleContentTrans);
        MeridianTipTexts.TranslateAll(isShow ? fxChongHoleTrans : ___m_fxChongHoleContentTrans);
    }
}

[HarmonyPatch(typeof(UIAcupoint), "ShowMusclesOrVeinsChongHoleView")]
internal static class MusclesOrVeinsViewAttributePatch
{
    private static void Postfix(UIAcupoint __instance)
    {
        if (MeridianTipTexts.English)
            MeridianTipTexts.TranslateAll(__instance.m_musclesOrVeinsChongHoleView?.Find("TxtContent"));
    }
}

[HarmonyPatch(typeof(UI.PopWin.UITipChongHoleFinish), nameof(UI.PopWin.UITipChongHoleFinish.ShowChongHoleFinishView))]
internal static class ChongHoleFinishAttributePatch
{
    private static void Postfix(Text ___txtTip, Transform ___fXChongHoleContent)
    {
        if (!MeridianTipTexts.English) return;

        MeridianTipTexts.Translate(___txtTip);
        MeridianTipTexts.TranslateAll(___fXChongHoleContent);
    }
}

[HarmonyPatch(typeof(UIJingLuoMinGame), nameof(UIJingLuoMinGame.SetUIData))]
internal static class JingLuoMinGameAttributePatch
{
    private static void Postfix(Text ___m_tip2)
    {
        if (MeridianTipTexts.English) MeridianTipTexts.Translate(___m_tip2);
    }
}

/// <summary>
/// The next-realm panel builds each row as label + "︻" + value + "︼", vertical brackets meant for the Chinese layout. The literals are not in the
/// dynamic string dump (it only takes strings with Chinese characters), so replace them in the rows it created: label, line break, value.
/// </summary>
[HarmonyPatch(typeof(UIJingJieNew), "InitNextAttributeShuxData")]
internal static class RealmAttributeBracketPatch
{
    private static void Postfix(List<Transform> ___item2List)
    {
        if (___item2List == null) return;

        foreach (var item in ___item2List)
        {
            var text = item == null ? null : item.FindChildByName("tip")?.GetComponent<Text>();
            if (text != null && text.text.IndexOf('︻') >= 0)
                text.text = text.text.Replace("︻", "\n").Replace("︼", "");
        }
    }
}
