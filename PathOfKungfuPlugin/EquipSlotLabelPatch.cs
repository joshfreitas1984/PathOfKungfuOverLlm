using System;
using HarmonyLib;
using UnityEngine.UI;

namespace PathOfKungfuPlugin;

/// <summary>
/// The equipment slot labels on the character's avatar (Item/Zb/ObjAvatar/EquipTrans/{slot}/Text) are prefab text whose English comes from a baked
/// Data_Word id, and some of those ids are wrong: slot 7 shows "System Settings". The game itself sets slot 2 to "Weapon" in code for this reason.
/// Slots 0 to 7 follow the EquipType order (项链, 戒指, 武器, 护甲, 护臂, 护腰, 坠饰, 护腿), so set each label from our own Data_Word entry for that
/// type (ids 222 to 230, section 默认界面).
/// </summary>
[HarmonyPatch(typeof(UIXingLiang), nameof(UIXingLiang.OnShow))]
internal static class EquipSlotLabelPatch
{
    // Data_Word ids for 项链, 戒指, 武器, 护甲, 护臂, 护腰, 坠饰, 护腿, by slot.
    private static readonly int[] WordIds = { 222, 227, 223, 228, 224, 229, 225, 230 };

    private static void Postfix(UIXingLiang __instance)
    {
        try
        {
            if (WordManager.Singleton == null || !WordManager.Singleton.Open) return;

            for (var slot = 0; slot < WordIds.Length; slot++)
            {
                var label = __instance.transform.Find($"Item/Zb/ObjAvatar/EquipTrans/{slot}/Text");
                var text = label == null ? null : label.GetComponent<Text>();
                var word = WordManager.Singleton.GetWord(WordIds[slot]);
                if (text != null && !string.IsNullOrWhiteSpace(word)) text.text = word.Trim();
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Setting equipment slot labels failed: {ex}");
        }
    }
}
