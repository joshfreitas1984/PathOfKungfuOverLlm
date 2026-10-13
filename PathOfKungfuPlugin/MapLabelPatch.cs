using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PathOfKungfuPlugin;

/// <summary>
/// The map screen's labels (UIMap/bgImage/Bg/mapItems/{map id}/map/tip) are Chinese text baked into the prefab ("襄阳城", "龙门荒漠"), and the game only
/// translates them through WordGroupHandle's list of about 20 place names, which half rewrites some ("Xiangyang城") and misses the rest. Each item is
/// named by its map id, so set the label from our own map table (地图名称, translated with 地图表/MapConfig) instead.
/// </summary>
[HarmonyPatch(typeof(UIMap), "Awake")]
internal static class MapLabelPatch
{
    private static void Postfix(Transform ___mapItemsRoot)
    {
        try
        {
            if (WordManager.Singleton == null || !WordManager.Singleton.Open || ___mapItemsRoot == null) return;

            var maps = Singleton<GDataManager>.Instance.dicMapConfig;
            if (maps == null) return;

            var set = 0;
            for (var i = 0; i < ___mapItemsRoot.childCount; i++)
            {
                var item = ___mapItemsRoot.GetChild(i);
                if (!int.TryParse(item.name, out var id) || !maps.TryGetValue(id, out var config) || string.IsNullOrWhiteSpace(config.地图名称)) continue;

                var tip = item.Find("map/tip");
                var text = tip == null ? null : tip.GetComponent<Text>();
                if (text == null) continue;

                text.text = config.地图名称.Trim();
                set++;
            }

            Plugin.Logger.LogInfo($"Map screen: {set} of {___mapItemsRoot.childCount} labels set from the map table.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Setting map labels failed: {ex}");
        }
    }
}
