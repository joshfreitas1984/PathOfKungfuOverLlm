# Prefab text and dynamic strings

Hard-coded text that is not in a table: UI text baked into prefabs, and string literals in the game's C# code. Both come from
runtime dumps, go through the normal translate/QC pipeline, and are packaged for `FanslationStudio.Plugins`, which applies them.

## Data flow

| | Prefab text | Dynamic strings |
| --- | --- | --- |
| Dump (written by the plugin to the game folder) | `BepInEx/plugins/rawStrings/prefabText.txt` | `BepInEx/plugins/rawStrings/dynamicStrings.txt` |
| Copy it to | `Files/Raw/Dumped/PrefabText/` | `Files/Raw/Dumped/DynamicStrings/` |
| Line format | one distinct string per line | `Type,Method,ILOffset,RawText,ParamTypes` (commas inside a field are fullwidth `，`) |
| `TextFileType` | `PrefabText` | `DynamicStrings` (Mono/Cecil, not the IL2CPP variant) |
| Registered in | [`TextFileConfiguration.cs`](../../Translate/TextFileConfiguration.cs) | same |
| Exported by | `TranslationExport` (test `1. ExportAssetsIntoTranslated`) | same, with `rawSubfolder` `Raw/Dumped/DynamicStrings` |
| Packaged by | `PrefabTextWorkflow.PackagePrefabTextAsync` | `DynamicStringsCecilWorkflow.PackageDynamicStringsCecilAsync` ([`TranslationPackaging.cs`](../../Translate/TranslationPackaging.cs)) |
| Output | `Files/Mod/prefabText.txt.yaml` (`raw`/`result`) | `Files/Mod/dynamicStrings.txt.yaml` (`DynamicStringContract`) |

Workflow mechanics (export shape, fallbacks, the `Converted` file being kept between exports) are in the sibling repo:
`FanslationStudio.LlmKit/docs/features/text-handling/dynamic-strings.md` and `prefab-text-workflow.md`.

After a game update, re-dump and copy the files over. Translations are kept for every line still in the new dump, but export rewrites `Converted/`
from the dump, so a line that is **not** in the new dump is dropped. Never add strings to `prefabText.txt` or its `Converted` file by hand.

The runtime dump only sees Text objects that are loaded when you press the button, so a screen you never opened (the map) is missing. Test
`0b. Dump prefab text from asset files` ([`AssetDumperTests.cs`](../../Tests/AssetDumperTests.cs), 8 seconds) reads every `MonoBehaviour` in the game's
serialized files (`levelN`, `sharedassetsN`, `resources.assets`, the `.unity3d` bundles) and merges the `m_Text` strings into
`Files/Raw/Dumped/PrefabText/prefabText.txt`: every line already there is kept (so the runtime-only strings and their translations survive) and the
missing ones are appended (about 1,000, including the map labels `襄阳城` and `龙门荒漠`). It also writes `dumpedOtherText.txt` with the field and path of
every string, for reference. It needs `Tests/classdata.tpk` (from the AssetsTools.NET releases) and the game's `Managed` folder. Run it after copying in
a fresh runtime dump, and again after a game update.

Strings neither dump can find, because the game builds them at runtime, go in [`Files/Raw/Dumped/PrefabText/prefabText.extra.txt`](../../Files/Raw/Dumped/PrefabText/prefabText.extra.txt)
(one per line, written by hand and never overwritten by a dump; registered in `TextFileConfiguration.cs`). It is translated and packaged like
the dump, to `Mod/prefabText.extra.txt.yaml`, and the plugin loads it because it loads every `*prefabText*` file. Today it holds the 16 key names the
game shows raw from its settings (`空格`, `左SHIFT`, ...).

## Strings that must not be translated

The runtime patch (`StringTranspiler` in `FanslationStudio.Plugins.Shared`) replaces **every** `ldstr` in a method that equals a
dumped raw string, not just the one at the dumped IL offset. A literal the code also uses as a key therefore breaks once translated.
A scan of the decompiled code (`Files/Raw/Decompiled/`) found no sprite loads by literal, but these:

- Spine animation and state names (`"受创"`, `"前闪避"`, `StateName.Contains("击飞")`, `GetAnimationNamePrefix() + "…"`).
- Excel sheet names in `GDataManager.LoadData_*` (`ExcelUtil.GetDictionary<…>("skill2", "武学配置", …)`).
- Values compared with data (`类型 == "武学研本"`, `InfoSettingData.GetKeyCode`, weapon `StartsWith("拳")`).
- Substring matches, `Replace()` search patterns, named lookups (`GetTipsContent("武力")`, `ComputeAttribute("真气上限")`), sound names.
- Literals the game itself replaces with its own English (`WordGroupHandle` place names, `Replace("内功攻击", "ATTACK")` in `TipShowWin*`):
  translating the Chinese side would make the game rewrite our English into its English.

[`Files/DynamicStringsDoNotTranslate.csv`](../../Files/DynamicStringsDoNotTranslate.csv) lists them as `Kind,Reason,Type,Method,Raw`
(1,025 rows: 891 from the scan on 2026-10-13, 20 added after in-game errors, and about 114 added on 2026-10-17 after translated hurt/launch state names cut knockups short, see below):

- `Key` rows are never translated.
- `Log` rows are log and exception text the player never sees. They are skipped only while `DynamicStringRules.SkipLogStrings` is true
  ([`DynamicStringRules.cs`](../../Translate/DynamicStringRules.cs)).

The table is wired in as `GameHooks.CustomUnsafeToTranslateRule`, so a matching split gets `SafeToTranslate = false` and is skipped by
translation, QC and packaging. The flag is only set by the rules pass (`ApplyAllRulesToCurrentTranslation` or
`TranslateLinesBruteForce`), never by plain `TranslateLines`, and is never cleared: removing a row does not re-enable a split
already marked. `DynamicStringRulesTests` fails when a row no longer matches the dump (a game update made it stale).

The scan was a heuristic over the decompiled source line, not a full data-flow analysis. The reasons in the CSV are a guide; a
literal that looks wrong in game may be missing from it, and a few display strings may be listed. Edit the CSV by hand.

Known misses, found from `Player.log` (`BepInEx` log has the patcher errors, the Unity log has the game's): Excel workbook and sheet
names assigned to a local first (`text = "角色战斗AI"; GetDictionary(text, text, …)`) were not matched, and translating them makes the
game fail with `Could not find file …\<English name>.xlsx` or `Please check sheetName`. Exception text in `IEvent.EventSystem`
(`Fire`, `OnListeningAdd/Remove`) was also missed, and patching that method made the game throw `Event SecondUpdate contains different
types of commissions` every second. Both are now `Key`/`Log` rows. After a game update, check `Player.log` for these before a release.

A second pass (2026-10-17) found more, after knockups ended early: `EnemyNormalHurtState.Execute` keeps an enemy in its hurt state only
while `StateName.Contains("受创"/"击飞"/…)` matches, and the hurt/launch names had been translated. The pass checked every packaged literal
against how the decompiled code uses it. Three kinds were missed by the first scan:

- State and object names used in `Contains` (`弹反`, `动物`, `受创`, `空中`, `格挡`).
- `Replace` **patterns** whose input is a Chinese enum name (`帮派任务`, `世间奇遇`, `格挡`, `重击`) or already-translated text (`暴击`): translating
  the pattern leaves Chinese on screen or lets the game rewrite our English.
- Attribute and category names compared with **untranslated** table columns: the equipment/aptitude/skill stat strings read by
  `UIRoleAttributeDataManager` (`武力+5`), `tipType` (`内功`/`轻功`, column 3 of `武学境界对应招式表`) and `ActiveType` (`主动`/`被动`, column 8
  of `招式配置`). A translated literal never matches, so stat bonuses silently stop applying. Before translating such a literal, check
  `Files/Mod/translated-columns.json`: it is only safe when the column is translated with the same wording (`item__秘籍` column 3 is, so
  `InitPlayData`'s `内功`/`轻功` stay translated).

Known unresolved: `StringTool.IsContainsShiTi` looks for `尸体`/`尸首`/`屍首` in NPC names, which are translated ("Dead Body of the Escort"), so
neither the Chinese nor the English literal matches. It needs the corpse wording aligned between the glossary and the literal.

Setting a row in the CSV does not clear an already-translated split; clear the split in `Files/Converted/dynamicStrings.txt.yaml` (set
`safeToTranslate: false` and drop the translation and QC fields) and repackage.

## The game's own English (`Open ? "English" : "中文"`)

About 160 dumped literals sit in `WordManager.Singleton.Open ? "English" : "中文"` ternaries. The plugin targets English mode, where `Open` is
true, so the game would show its own English ("Leave", "No Magic") and our translation of the Chinese side would go unused. The dump holds only the
Chinese literals.

[`Files/ScanGameEnglishLiterals.py`](../../Files/ScanGameEnglishLiterals.py) reads the decompiled code and writes
[`Files/DynamicStringsGameEnglish.csv`](../../Files/DynamicStringsGameEnglish.csv) (`Type,Method,English,Chinese`, about 160 rows). It finds
ternaries where both sides are literals (or the Chinese side is `"中文" + value`), calls of a helper such as `GetText("Phy Atk", "外功攻击", open)`,
and `if (Open) { … } else { … }` blocks with the same number of literals, which it pairs by position. An English side that is empty or has no two
letters is skipped (`Open ? "" : "文"` would replace every empty string in the method).
Packaging (`AddGameEnglishContracts` in [`TranslationPackaging.cs`](../../Translate/TranslationPackaging.cs)) adds, for each row whose Chinese
literal was packaged, a second contract with the English literal as `raw` and the same translation, so the same transpiler replaces that `ldstr`.
`GameEnglishContractTests` fails when a row no longer matches the dump; re-run the script after a game update.

Not covered: a side that is a field, a call or another expression (`Open ? fishingName2 : fishingName`, table columns the game fills itself), blocks
whose two sides have a different number of literals, and the equipment-type `Replace("项链", "Necklace")` chain, whose Chinese strings are lookup
keys. These show the game's English until they turn up in game.

Other game English that overrides ours is switched off by default in the plugin (`GameEnglishPlaceNamesPatch.cs`, `AttributeNamePatch.cs`):
`WordGroupHandle` (place names, `[Translation] UseGameEnglishPlaceNames`), `WordSpecial` (per-frame word swaps such as 两 → "Liang", same option) and
`StringExtensions.TranslateAttribute` (attribute names, now from our glossary).

Related, lower priority: `WordGroupHandle.CheckWord` replaces any Chinese place name still on screen with the game's own English
("Green Duckweed Village"), which may not match our glossary. It cannot touch text we translated, only text left in Chinese.
