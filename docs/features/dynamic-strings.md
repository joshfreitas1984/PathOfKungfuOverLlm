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

After a game update, re-dump and copy the files over. `Converted/` is preserved on re-export, so existing translations are kept.

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
(911 rows: 891 from the scan on 2026-10-13, plus 20 added after in-game errors):

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

## Known gap: the game's own English wins

About 150 dumped literals sit in `WordManager.Singleton.Open ? "English" : "中文"` ternaries. The plugin targets English mode, where
`Open` is true, so the game shows its own English ("Leave", "Max HP", "can not do it") and our translation of the Chinese side is
unused (it only shows in Chinese mode). The dump holds only the Chinese literals, so the English side is never replaced.

Not fixed yet; to be handled as they turn up in game. The idea if it is worth doing: save the game's English literal for each
ternary in a committed file and have packaging emit an extra contract for it carrying the same translation, so the same transpiler
replaces that `ldstr`. Forcing `Open = false` per method (as `ConvertTextEnglishBranchPatch` does for one call) is likely to break
other code paths.

Related, lower priority: `WordGroupHandle.CheckWord` replaces any Chinese place name still on screen with the game's own English
("Green Duckweed Village"), which may not match our glossary. It cannot touch text we translated, only text left in Chinese.
