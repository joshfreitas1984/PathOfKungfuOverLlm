using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace PathOfKungfuPlugin;

/// <summary>
/// The game names a gang when the world is made: a random word from the GangName sheet's pool plus the style suffix (帮/会/教), stored in
/// the save as one string. The English sheet it loads in English mode is the game's own rough machine translation ("loss of the soul",
/// "poison control", "by god"), and the suffix column stays Chinese. This replaces the pool and the suffixes after the sheet has loaded,
/// and converts the names of gangs already in the save (old English pool + 会, or the Chinese pool).
/// <para>The pool is only used as a list of prefixes, and the game refuses a prefix that is contained in an existing gang's name, so no
/// word below may contain another.</para>
/// </summary>
internal static class GangNames
{
    internal static readonly string[] Suffixes = { " Gang", " Society", " Sect" };
    private static readonly string[] ChineseSuffixes = { "帮", "会", "教" };
    private static readonly string[] OldEnglishSuffixes = { "Help", "Will", "Teach" };

    // Chinese word, our English, the game's English for the same word (null when it dropped or merged it).
    private static readonly (string Zh, string En, string Game)[][] Pools =
    {
        new[]
        {
            ("龙卷", "Dragon Whirlwind", "Tornado"), ("湿婆", "Shiva", "shiva"), ("丧魂", "Lost Soul", "loss of the soul"),
            ("毒蝶", "Poison Butterfly", "poison control"), ("千山", "Thousand Mountains", "qianshan mountain"),
            ("活杀", "Lifetaker", "live to kill"), ("风沙", "Wind and Sand", "sand"), ("梨花", "Pear Blossom", "pear flower"),
            ("兽皇", "Beast Emperor", "beast huang"), ("灵猴", "Spirit Monkey", "spirit monkey"), ("轮回", "Reincarnation", "reincarnation"),
            ("青龙", "Azure Dragon", "blue dragon"), ("白虎", "White Tiger", "white tiger"), ("玄武", "Black Tortoise", "xuanwu"),
            ("麒麟", "Qilin", "kylin"), ("天龙", "Tianlong", "dragon"), ("鳄鱼", "Crocodile", "crocodiles"),
            ("龙王", "Dragon King", "the dragon king"), ("修罗", "Asura", "shura"), ("般若", "Prajna", "prajnaparamita"),
            ("三才", "Three Powers", "three to four elephants"), ("四象", "Four Symbols", null), ("五行", "Five Elements", "five lines"),
            ("六合", "Six Harmonies", "liuhe"), ("七星", "Seven Stars", "seven stars"), ("八卦", "Eight Trigrams", "gossip"),
        },
        new[]
        {
            ("云龙", "Cloud Dragon", "Yunlong"), ("八幡", "Hachiman", "yawata"), ("琉璃", "Lazuli", "coloured glaze"),
            ("夜叉", "Yaksha", "hag"), ("罗刹", "Rakshasa", "Russian"), ("伏羲", "Fuxi", "fuxi and nuwa"), ("女娲", "Nuwa", null),
            ("神农", "Shennong", "shennong"), ("慈悲", "Mercy", "compassion"), ("祝融", "Zhurong", "ravaged"),
            ("共工", "Gonggong", "gong gong"), ("摩诃", "Maha", "maha"), ("金刚", "Vajra", "king kong"), ("菩提", "Bodhi", "bodhi"),
            ("黄河", "Yellow River", "Yellow River"), ("欢喜", "Joyful", "rejoice"), ("财神", "God of Wealth", "the god of wealth"),
            ("太初", "Primordial", "in the beginning"), ("青霞", "Azure Mist", "green xia"), ("九宫", "Nine Palaces", "jiugong"),
            ("碎星", "Shattered Star", "broken stars"), ("掩月", "Veiled Moon", "yan"), ("自在", "Carefree", "comfortable"),
            ("霓虹", "Rainbow", "neon"), ("先天", "Innate", "innate"),
        },
        new[]
        {
            ("九宫", "Nine Palaces", "Jiugong"), ("阴阳", "Yin Yang", "Yin and Yang"), ("极乐", "Paradise", "bliss"),
            ("皇天", "Imperial Heaven", "by god"), ("黄龙", "Yellow Dragon", "huanglong"), ("魍魉", "Specter", "shadows"),
            ("骷髅", "Skull", "and the skeleton"), ("鬼蝠", "Ghost Bat", "ghost bat"), ("贪狼", "Greedy Wolf", "Wolf"),
            ("破军", "Army Breaker", "army"), ("七杀", "Seven Kills", "seven killed"), ("紫微", "Ziwei", "and Chinese"),
            ("天魁", "Tiankui", "day chief"), ("天罡", "Tiangang", "plough"), ("地煞", "Disha", "the detective"),
            ("乾坤", "Qiankun", "qiankun"), ("廉贞", "Lianzhen", "LianZhen"), ("勾陈", "Gouchen", "hook"),
            ("玄天", "Mystic Heaven", "Chen xuan"), ("雄狮", "Lion", "lion"), ("猛虎", "Fierce Tiger", "tiger"),
            ("苍鹰", "Eagle", "eagle"), ("雪驼", "Snow Camel", "snow camel"), ("九霄", "Ninth Heaven", "nine"),
        },
    };

    // Old prefix (Chinese, or the game's English in lower case) -> our English. The pools share 九宫, which maps to the same word.
    private static readonly Dictionary<string, string> PrefixMap = BuildPrefixMap();

    private static Dictionary<string, string> BuildPrefixMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pool in Pools)
            foreach (var (zh, en, game) in pool)
            {
                map[zh] = en;
                if (game != null) map[game] = en;
            }
        return map;
    }

    /// <summary>The comma separated prefix pool for a style (1 = 帮, 2 = 会, 3 = 教), in the format the sheet uses.</summary>
    internal static string? Pool(int style) =>
        style >= 1 && style <= Pools.Length ? string.Join(",", Pools[style - 1].Select(w => w.En)) : null;

    internal static string? Suffix(int style) => style >= 1 && style <= Suffixes.Length ? Suffixes[style - 1] : null;

    /// <summary>
    /// The converted name of a gang whose name was built from an old pool, or null when it is already ours or not recognised.
    /// The style is the gang's own (1-3), which is also the suffix the old name ends with.
    /// </summary>
    internal static string? Convert(string name, int style)
    {
        if (string.IsNullOrEmpty(name) || style < 1 || style > Suffixes.Length) return null;

        var suffix = FindSuffix(name, style);
        if (suffix == null) return null;

        var prefix = name.Substring(0, name.Length - suffix.Length).Trim();
        return PrefixMap.TryGetValue(prefix, out var english) ? english + Suffixes[style - 1] : null;
    }

    private static string? FindSuffix(string name, int style)
    {
        foreach (var suffix in new[] { ChineseSuffixes[style - 1], OldEnglishSuffixes[style - 1] })
            if (name.EndsWith(suffix, StringComparison.Ordinal)) return suffix;
        return null;
    }
}

/// <summary>Replaces the pool and suffix the game has just loaded (after the translation was injected into the sheet).</summary>
[HarmonyPatch(typeof(GDataManager), "InitWorldEvolutionData")]
internal static class GangNamePoolPatch
{
    private static void Postfix(GDataManager __instance)
    {
        try
        {
            var dic = __instance.m_gangNameDataDic;
            if (dic == null) return;
            foreach (var (style, data) in dic)
            {
                var pool = GangNames.Pool(style);
                var suffix = GangNames.Suffix(style);
                if (pool == null || suffix == null) continue;
                data.name = pool;
                data.style = suffix;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Gang name pool replacement failed: {ex}");
        }
    }
}

/// <summary>Gangs keep the name they were generated with, so a save made before the pool was replaced is converted when the game starts.</summary>
[HarmonyPatch(typeof(WorldEvolutionManager), "InitData")]
internal static class SavedGangNameMigration
{
    private static void Postfix()
    {
        try
        {
            var gangs = Singleton<GDataManager>.Instance.playerData?.m_allGangDataDic;
            if (gangs == null) return;

            var changed = 0;
            var refreshed = 0;
            foreach (var gang in gangs.Values)
            {
                var converted = GangNames.Convert(gang.GangName, gang.Style);
                if (converted == null || converted == gang.GangName) continue;
                gang.GangName = converted;
                changed++;
                // The station NPCs (deacon, manager, guards...) keep the old name in their own saved names.
                try { GamePlaySingleton<GangManager>.Instance.RefreshStationNpcName(gang.gangDataId); }
                catch (Exception ex) { Plugin.Logger.LogWarning($"Could not refresh station NPC names for gang {gang.gangDataId}: {ex.Message}"); }
            }

            // A mine's label on the map ("(Sheltered by <gang>)" + the mine's name) is stored in the save's touch data and only rebuilt when
            // ownership changes, so it keeps the old gang name and the Chinese mine name until it is rebuilt here.
            foreach (var gang in gangs.Values)
            {
                if (gang.propertyInfos == null) continue;
                foreach (var info in gang.propertyInfos)
                {
                    try
                    {
                        if (info.GetPropertyType() != GangPropertyType.Mine || GangPropertyData.GetMineTouch(info.id) == null) continue;
                        GangPropertyData.UpdateMineTouch(info);
                        refreshed++;
                    }
                    catch (Exception ex) { Plugin.Logger.LogWarning($"Could not refresh the mine label of property {info.id}: {ex.Message}"); }
                }
            }

            if (changed > 0) Plugin.Logger.LogInfo($"Converted {changed} saved gang names to the translated pool.");
            if (refreshed > 0) Plugin.Logger.LogInfo($"Refreshed {refreshed} saved mine labels.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Saved gang name conversion failed: {ex}");
        }
    }
}
