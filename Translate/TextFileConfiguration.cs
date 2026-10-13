using FanslationStudio.LlmKit.Support;

namespace Translate;

/// <summary>
/// Per-file split/package configuration for the CSVs written by the plugin dumper into Files/Raw/Dumped/GameData.
/// File names are flat (Workbook__Sheet.csv) because LlmKit writes Converted/{Path}.yaml without creating folders.
/// Column 0 of every file is the dumper's "Row" column and is never translated.
///
/// Which columns are translated comes from the game itself: a column the developers translated in the shipped
/// English sheets (item names and descriptions, quest text, dialogue...) is display text, and the shipped English
/// proves that translating a name column is safe, since the game looks names up in the language it loaded. Columns they
/// left in Chinese in those sheets (item category, weapon type, effect target, formulas) are enums or keys the code
/// compares against, so they stay untouched. Chinese-only tables that are still shown to the player (ECA buff and effect
/// text, UI strings, battle barks) were added by hand. See docs/features/text-files-to-split.md for what is not covered yet.
/// </summary>
public static class TextFileConfiguration
{
    /// <summary>Skip every column of a file except the listed ones. <paramref name="columnCount"/> includes the Row column.</summary>
    private static HashSet<int> TranslateOnly(int columnCount, params int[] columns) =>
        Enumerable.Range(0, columnCount).Except(columns).ToHashSet();

    public static readonly TextFileToSplit[] TextFilesToSplit = [
            // Dialogue (flattened JSON from Touch / SandBox_Touch / TaskProcess cells)
            new() { Path = "SandBox_NpcFromTouch__Touch.json.csv", IgnoreHtmlTagsInText = true, AdditionalPromptName = "PlayerTokenPrompt", SkipColumns = TranslateOnly(5, 4) },
            new() { Path = "SandBox_Touch__Touch.json.csv", IgnoreHtmlTagsInText = true, AdditionalPromptName = "PlayerTokenPrompt", SkipColumns = TranslateOnly(5, 4) },
            new() { Path = "TaskProcess__TaskProcess.json.csv", IgnoreHtmlTagsInText = true, AdditionalPromptName = "PlayerTokenPrompt", SkipColumns = TranslateOnly(5, 4) },
            new() { Path = "Touch__Touch.json.csv", IgnoreHtmlTagsInText = true, AdditionalPromptName = "PlayerTokenPrompt", SkipColumns = TranslateOnly(5, 4) },

            // UI strings and battle barks
            new() { Path = "LanguageConfig__LanguageConfig_jt.csv", SkipColumns = TranslateOnly(3, 2) },
            new() { Path = "TornData__Data_Speak.csv", SkipColumns = TranslateOnly(13, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12) },
            new() { Path = "TornData__Data_Word.csv", SkipColumns = TranslateOnly(4, 3) },

            // Quests
            new() { Path = "TaskDes__TaskDes.csv", SkipColumns = TranslateOnly(6, 2, 3, 4, 5) },
            new() { Path = "TaskNew__Task.csv", SkipColumns = TranslateOnly(14, 2, 12, 13) },
            new() { Path = "TaskProcess__TaskProcess.csv", SkipColumns = TranslateOnly(6, 2, 3) },
            new() { Path = "引导表__Guide.csv", SkipColumns = TranslateOnly(21, 12) },

            // NPCs, map objects and NPC dialogue
            new() { Path = "NPC__ActionDialog.csv", SkipColumns = TranslateOnly(13, 2, 3) },
            new() { Path = "NPC__Dialog.csv", SkipColumns = TranslateOnly(8, 5) },
            new() { Path = "NPC__NpcDialog.csv", SkipColumns = TranslateOnly(6, 2) },
            new() { Path = "NPC__NpcDialogLips.csv", SkipColumns = TranslateOnly(29, 2, 9, 25, 27, 28) },
            new() { Path = "NPC__PlayerAction.csv", SkipColumns = TranslateOnly(20, 13) },
            new() { Path = "NPC__SelfAction.csv", SkipColumns = TranslateOnly(32, 17) },
            new() { Path = "NPC__proAction.csv", SkipColumns = TranslateOnly(30, 18) },
            new() { Path = "SandBox_NpcFromTouch__Touch.csv", SkipColumns = TranslateOnly(18, 2) },
            new() { Path = "SandBox_Touch__Touch.csv", SkipColumns = TranslateOnly(20, 3) },
            new() { Path = "Touch__Touch.csv", SkipColumns = TranslateOnly(20, 3) },

            // Items
            new() { Path = "item__任务.csv", SkipColumns = TranslateOnly(25, 2, 19) },
            new() { Path = "item__其他.csv", SkipColumns = TranslateOnly(26, 2, 20) },
            new() { Path = "item__材料.csv", SkipColumns = TranslateOnly(26, 2, 20) },
            new() { Path = "item__秘籍.csv", SkipColumns = TranslateOnly(35, 2, 3, 26, 27) },
            new() { Path = "item__艺能.csv", SkipColumns = TranslateOnly(28, 2, 20, 21) },
            new() { Path = "item__药品.csv", SkipColumns = TranslateOnly(30, 2, 23, 24, 27) },
            new() { Path = "item__装备.csv", SkipColumns = TranslateOnly(34, 2, 26) },
            new() { Path = "item__食物.csv", SkipColumns = TranslateOnly(28, 2, 22, 23) },

            // Skills, buffs and effects
            new() { Path = "ECA__BuffData.csv", SkipColumns = TranslateOnly(24, 9, 10) },
            new() { Path = "ECA__EffectData.csv", SkipColumns = TranslateOnly(23, 6) },
            new() { Path = "ECA__PassiveSkillData.csv", SkipColumns = TranslateOnly(20, 2, 4) },
            new() { Path = "SkillBattle__心诀配置.csv", SkipColumns = TranslateOnly(13, 2, 8) },
            new() { Path = "SkillBattle__招式配置.csv", SkipColumns = TranslateOnly(45, 2, 3, 9, 23, 24) },
            new() { Path = "SkillBattle__散学对应招式表.csv", SkipColumns = TranslateOnly(6, 2, 4) },
            new() { Path = "SkillBattle__武学境界对应招式表.csv", SkipColumns = TranslateOnly(20, 2, 11) },
            new() { Path = "SkillBattle__轻击对应招式表.csv", SkipColumns = TranslateOnly(7, 2, 5) },
            new() { Path = "SkillBattle__重击对应招式表.csv", SkipColumns = TranslateOnly(7, 2, 5) },

            // Enemy names shown on the battle screen (MonsterData.name, display only). The four difficulty sheets hold the same names.
            new() { Path = "monster__monster_easy.csv", SkipColumns = TranslateOnly(48, 2) },
            new() { Path = "monster__monster_normal.csv", SkipColumns = TranslateOnly(46, 2) },
            new() { Path = "monster__monster_hard.csv", SkipColumns = TranslateOnly(46, 2) },
            new() { Path = "monster__monster_hardEX.csv", SkipColumns = TranslateOnly(46, 2) },

            // Other tables
            new() { Path = "CookJingYing__Config.csv", SkipColumns = TranslateOnly(18, 4, 5, 10) },
            new() { Path = "Cook__CookBook.csv", SkipColumns = TranslateOnly(9, 2, 7) },
            new() { Path = "JingLuo__acupoint.csv", SkipColumns = TranslateOnly(21, 2, 3) },
            new() { Path = "JingLuo__auxiliary.csv", SkipColumns = TranslateOnly(9, 2) },
            new() { Path = "JingLuo__collaterals.csv", SkipColumns = TranslateOnly(14, 3, 9, 10) },
            new() { Path = "WorldEvolution__attribute.csv", SkipColumns = TranslateOnly(32, 3) },
            new() { Path = "WorldEvolution__characteristic.csv", SkipColumns = TranslateOnly(31, 3, 4) },
            new() { Path = "WorldEvolution__like.csv", SkipColumns = TranslateOnly(4, 2) },
            new() { Path = "WorldEvolution__tips.csv", SkipColumns = TranslateOnly(5, 2, 3) },
            new() { Path = "aptitude__Chance.csv", SkipColumns = TranslateOnly(14, 2, 12, 13) },
            new() { Path = "inborn__inborn.csv", SkipColumns = TranslateOnly(38, 2, 5) },
            new() { Path = "地图表__MapConfig.csv", SkipColumns = TranslateOnly(20, 2) },
    ];
}
