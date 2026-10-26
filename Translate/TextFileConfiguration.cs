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
            new() { Path = "WorldEvolution__attribute.csv", SkipColumns = TranslateOnly(32, 3, 6, 24) },
            new() { Path = "WorldEvolution__characteristic.csv", SkipColumns = TranslateOnly(31, 3, 4) },
            new() { Path = "WorldEvolution__like.csv", SkipColumns = TranslateOnly(4, 2) },
            new() { Path = "WorldEvolution__tips.csv", SkipColumns = TranslateOnly(5, 2, 3) },
            new() { Path = "aptitude__Chance.csv", SkipColumns = TranslateOnly(14, 2, 12, 13) },
            new() { Path = "inborn__inborn.csv", SkipColumns = TranslateOnly(38, 2, 5) },
            new() { Path = "地图表__MapConfig.csv", SkipColumns = TranslateOnly(20, 2) },

            // Event, boss, gambling, assassin-task, gang-task and letter text, shown directly (nothing compares it with Chinese). The English-mode
            // copies (BetShop2, GangData2, RefreshTalk2, PigeonMsgData2) keep the Chinese sheets' ids and rows, so the plugin applies these
            // translations to them and replaces their shipped English (SheetInjector.SandboxCopy). The 说明 column of
            // dialogChange, the 备注 columns and WorldEvolution__gangTask (never loaded) are design notes.
            // GangData2__GangName is English in the shipped game except the style column (帮/会/教), which the game appends to the name ("... 会").
            // The plugin (GangNamePatch) replaces both the name pool and the suffixes after load, so this column's translation is overridden.
            new() { Path = "GangData2__GangName.csv", SkipColumns = TranslateOnly(11, 2) },
            // Gang property names (mines, shops): shown on the map under "(Sheltered by <gang>)". The English-mode copy (GangData2) is identical and uses this file.
            new() { Path = "GangData__GangProperty.csv", SkipColumns = TranslateOnly(18, 3) },
            new() { Path = "NPC__dialogChange.csv", SkipColumns = TranslateOnly(9, 6) },
            new() { Path = "NPC__multiAction.csv", SkipColumns = TranslateOnly(39, 28, 29) },
            new() { Path = "RefreshBoss__RefreshTalk.csv", SkipColumns = TranslateOnly(10, 2) },
            // Gambling table names (e.g. 襄阳城赌坊天字桌), shown as the NPC name tag. BetShop2 (English mode) is identical to this sheet, so the plugin uses it for both (SheetInjector.SandboxCopy).
            new() { Path = "BetShop__Config.csv", SkipColumns = TranslateOnly(15, 2) },
            new() { Path = "BetShop__Talk.csv", SkipColumns = TranslateOnly(3, 2) },
            new() { Path = "BetShop__Lips.csv", SkipColumns = TranslateOnly(11, 2, 9) },
            new() { Path = "BetShop__Bet.csv", SkipColumns = TranslateOnly(11, 2, 3, 4) },
            new() { Path = "BetShop__SkillTask.csv", SkipColumns = TranslateOnly(11, 3, 4, 5, 6, 7) },
            new() { Path = "BetShop__SkillLv.csv", SkipColumns = TranslateOnly(9, 2) },
            new() { Path = "GangData__GangTask.csv", SkipColumns = TranslateOnly(10, 3, 4) },
            // Assassin-organisation pigeon letters (title, description); the game replaces the literal words npcName / itemName.
            new() { Path = "KillerOrg__Task.csv", SkipColumns = TranslateOnly(8, 3, 4) },
            new() { Path = "GangData__GangStation.csv", SkipColumns = TranslateOnly(24, 10, 15) },
            new() { Path = "LoadingTip__Config.csv", SkipColumns = TranslateOnly(4, 2, 3) },

            // Sheets the game replaces with a shipped English twin in English mode (skill2, boxpoint2, body2, explain2, Sheet2). The plugin maps the
            // twin back to these Chinese sheets (SheetInjector.EnglishTwins), pairs rows by id and overwrites the shipped English.
            // Scroll (密卷) names and effect text, chapter names/reward text, scroll-effect names. Column 9 of 密卷 has no header, so the game never reads it.
            new() { Path = "skill__密卷.csv", SkipColumns = TranslateOnly(13, 2, 4) },
            new() { Path = "skill__mijuan.csv", SkipColumns = TranslateOnly(9, 2, 5, 8) },
            new() { Path = "skill__三才书属性加成.csv", SkipColumns = TranslateOnly(33, 2) },
            // Map status effects (hunger, poison...) and search-point text. Skipped: lipsStr (a comma-separated button list the code splits).
            new() { Path = "MapBuffer__body.csv", SkipColumns = TranslateOnly(8, 2, 3) },
            new() { Path = "box__boxpoint.csv", SkipColumns = TranslateOnly(10, 6) },
            new() { Path = "WorldEvolution__explain.csv", SkipColumns = TranslateOnly(3, 2) },
            new() { Path = "Read__Sheet1.csv", SkipColumns = TranslateOnly(9, 3) },
            new() { Path = "PigeonMsgData__PigeonMsgData.csv", SkipColumns = TranslateOnly(11, 4, 5) },

            // Display-only tables found by tracing the decompiled code (only tables the game still loads in English mode; the sheets that
            // have an English twin there - NPC_EN NpcAction, boxpoint2, explain2, body2, Read Sheet2, key1 name2 - are not listed).
            // Martial-hall (Gyms) labels shown under NPC names: "【name】" from Main and the "(title)_(title)_..." list from config, which the game
            // splits on '_' by staff type, so the translation must keep exactly 5 '_'-separated items. Job's title column is never read.
            new() { Path = "Gyms__Main.csv", SkipColumns = TranslateOnly(9, 2) },
            new() { Path = "Gyms__config.csv", SkipColumns = TranslateOnly(11, 4) },
            // NPC action buttons and their reply lines ({0} is the NPC name, {1} the player name).
            new() { Path = "NPC__NpcXingWei.csv", SkipColumns = TranslateOnly(16, 4) },
            new() { Path = "NPC__NpcXingWeiTalk.csv", SkipColumns = TranslateOnly(10, 4, 5, 6, 7, 8, 9) },
            new() { Path = "TaskDes__TaskZhiYing.csv", SkipColumns = TranslateOnly(6, 2, 3) },
            new() { Path = "EscortAgencyData__EscortAgencyData.csv", SkipColumns = TranslateOnly(15, 3) },
            // Interaction names (touch names go through GetLangText, which returns an unknown key unchanged) and map entry text.
            new() { Path = "PrisonData__PrisonData.csv", SkipColumns = TranslateOnly(9, 4) },
            new() { Path = "PrisonCellData__PrisonCellData.csv", SkipColumns = TranslateOnly(7, 4) },
            new() { Path = "MapDungeonEntryData__MapDungeonEntryData.csv", SkipColumns = TranslateOnly(10, 4, 8, 9) },
            new() { Path = "MapPracticeRoomEntryData__MapPracticeRoomEntryData.csv", SkipColumns = TranslateOnly(10, 3) },
            new() { Path = "StorageBoxData__StorageBoxData.csv", SkipColumns = TranslateOnly(7, 3) },
            new() { Path = "box__宝箱表现形式.csv", SkipColumns = TranslateOnly(6, 2) },
            new() { Path = "dungeon__config.csv", SkipColumns = TranslateOnly(16, 13) },
            // Team formations, mall, shops, partners, sects, world-evolution names, currency, character-creation labels, weapon suffixes.
            new() { Path = "Team__Team.csv", SkipColumns = TranslateOnly(17, 2, 6, 7, 8) },
            new() { Path = "mall__mall.csv", SkipColumns = TranslateOnly(11, 2, 5) },
            new() { Path = "shop2__shop.csv", SkipColumns = TranslateOnly(11, 2) },
            new() { Path = "partner__partner.csv", SkipColumns = TranslateOnly(27, 2) },
            new() { Path = "门派表__sect.csv", SkipColumns = TranslateOnly(15, 3) },
            new() { Path = "门派表__party.csv", SkipColumns = TranslateOnly(9, 2) },
            new() { Path = "WorldEvolution__lichang.csv", SkipColumns = TranslateOnly(9, 2) },
            new() { Path = "WorldEvolution__friends.csv", SkipColumns = TranslateOnly(5, 2) },
            new() { Path = "WorldEvolution__city.csv", SkipColumns = TranslateOnly(9, 2) },
            new() { Path = "currency__currency.csv", SkipColumns = TranslateOnly(6, 3) },
            new() { Path = "language__language.csv", SkipColumns = TranslateOnly(4, 2) },
            new() { Path = "Library__装备后缀.csv", SkipColumns = TranslateOnly(6, 5) },

            // Hard-coded UI text baked into prefabs (Raw/Dumped/PrefabText) and string literals in the game's code
            // (Raw/Dumped/DynamicStrings: Type,Method,ILOffset,RawText,ParamTypes).
            new() { Path = "prefabText.txt", TextFileType = TextFileType.PrefabText },
            // Strings the dumper cannot find because the game builds them at runtime (key names, ...). Written by hand, so a re-dump never replaces it;
            // the plugin loads every *prefabText* file. See docs/features/dynamic-strings.md.
            new() { Path = "prefabText.extra.txt", TextFileType = TextFileType.PrefabText },
            new() { Path = "dynamicStrings.txt", TextFileType = TextFileType.DynamicStrings },
    ];
}
