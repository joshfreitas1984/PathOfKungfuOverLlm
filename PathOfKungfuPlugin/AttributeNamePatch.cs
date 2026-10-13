using HarmonyLib;

namespace PathOfKungfuPlugin;

/// <summary>
/// Equipment tooltips build their attribute titles from the item data's 主属性 / 附属属性 keys ("攻击力+2;防御力+8"), which the game
/// code also uses as stat keys, so the data cannot be translated. The game translates the titles through StringExtensions.TranslateAttribute,
/// but its list has no entry for 攻击力, 防御力 or 气血回复 (气血 becomes "HP" first, leaving "HP回复"), so they stay Chinese. This adds them.
/// </summary>
[HarmonyPatch(typeof(StringExtensions), nameof(StringExtensions.TranslateAttribute))]
internal static class AttributeNamePatch
{
    private static void Postfix(ref string __result)
    {
        if (string.IsNullOrEmpty(__result)) return;

        __result = __result
            .Replace("攻击力", "Attack")
            .Replace("防御力", "Defense")
            .Replace("HP回复", "HP Recovery");
    }
}
