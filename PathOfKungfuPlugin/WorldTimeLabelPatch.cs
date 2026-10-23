using System;

using HarmonyLib;

namespace PathOfKungfuPlugin;

/// <summary>
/// The date label (UIMain.GameTimeShow) appends the weather as "(" + enum name + ")", and in English mode swaps the Chinese names for English with a
/// Replace chain that has two bugs: "雨天" is replaced before "雷雨天", so a thunderstorm shows "雷Rainy", and it appends an extra ")" ("Day 3 (Rainy))").
/// Rebuild the label in English mode with the weather names and brackets done properly.
/// </summary>
[HarmonyPatch(typeof(UIMain), "GameTimeShow")]
internal static class WorldTimeLabelPatch
{
    private static void Postfix(UIMain __instance)
    {
        try
        {
            if (WordManager.Singleton == null || !WordManager.Singleton.Open) return;
            if (MapManager.Instance == null || __instance.m_worldTime == null) return;

            __instance.m_worldTime.text = $"{WorldTime.GetTimeString().TrimEnd()} ({WeatherName(MapManager.Instance.GetWeatherType())})";
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Setting the date label failed: {ex}");
        }
    }

    private static string WeatherName(GProject.Data.WeatherType weather) => weather switch
    {
        GProject.Data.WeatherType.晴天 => "Sunny",
        GProject.Data.WeatherType.雨天 => "Rainy",
        GProject.Data.WeatherType.雪天 => "Snowy",
        GProject.Data.WeatherType.雷雨天 => "Thunderstorm",
        GProject.Data.WeatherType.雾天 => "Foggy",
        _ => weather.ToString(),
    };
}

/// <summary>
/// In English mode WorldTime.GetTimeString() joins the year, month and day as "1.IV.Day 3". Spell it out as "Year 1, Month 4, Day 3" instead, for the
/// date label and every other place that shows the game date.
/// </summary>
[HarmonyPatch(typeof(WorldTime), nameof(WorldTime.GetTimeString), new System.Type[0])]
internal static class WorldTimeStringPatch
{
    private static bool Prefix(ref string __result)
    {
        if (WordManager.Singleton == null || !WordManager.Singleton.Open) return true;

        WorldTime.GetTime(out var year, out var month, out var day, out _);
        __result = $"Year {year}, Month {month}, Day {day} ";
        return false;
    }
}
