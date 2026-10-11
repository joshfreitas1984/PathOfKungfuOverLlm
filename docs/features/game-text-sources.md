# Game text sources (plan step 1 findings)

Status: step 1 findings, based on static inspection only (file listing, `G:\PathOfKungfu-AssemblyCSharp`
decompile, and a scan of the `.xlsx` files). Nothing was run in-game. Items marked **unverified**
still need confirming in step 2.

## Key findings

- **Source language: Simplified Chinese** (`schinese`). `glossaryPreset` should be the Simplified
  Chinese one. Names are pinyin-style xianxia/wuxia (see `LanguageConfig_en.txt`, `Data_Word.txt`).
- **The game already ships machine-quality English and nine other languages.** The goal is to replace
  that English, so the Chinese base is the translation source and the shipped English is only an
  optional reference. Do not treat existing `_en` text as ground truth.
- **Language is chosen by Steam language** (`SteamHelper.GetCurrentGameLanguage`, falls back to
  `schinese` if Steam is not initialised). `GDataManager.SetExcelExtrNameOfLang` maps it to a file
  suffix (`_en`, `_fr`, ... or none for Chinese) and `WordManager.Open` is true for every language
  except `schinese`/`tchinese`. So "English mode" loads different files/sheets than "Chinese mode".
  The old AutoTranslator setup (`G:\PathOfKungfu`, `FromLanguage=zh`, ~16.7k lines) evidently ran
  against Chinese mode and hooked rendered UI text; nothing reusable beyond its `resizer.txt`
  (135 lines) and `splitters.txt` (21 lines) for step 5.
- **Data is Excel-driven.** Tables are `.xlsx` read with `MiniExcel` through `ExcelUtil`
  (`GetListOject<T>` / `GetDictionary<TKey,T>`), mapped onto classes by column header name.
  The plugin therefore can read the same files with the game's own `MiniExcel.dll`.

## Text sources

All paths are under `daojianjianghulu_Data\StreamingAssets`.

| Source | Format | Content | Loader | Suggested TextFileType |
| --- | --- | --- | --- | --- |
| `resdata/ExcelData/*.xlsx` (160 files) | Excel, many sheets per workbook, header row + data | Items, skills, buffs/effects, NPCs, shops, maps, quests, names, tips, poems... | `ExcelUtil` via `GDataManager` | `RawCsv` once dumped to CSV per sheet |
| JSON embedded in Excel cells (`Touch.talkLib`, `searchTalks`, `TaskProcess.mainTouch`/`otherTouch`, `avatarData`) | JSON strings inside cells | NPC/story dialogue lives here | `Newtonsoft.Json` after load | See "JSON cells" below |
| `TornData/Data_Word.txt` | TSV, 2 header rows, columns `ID Section Chinese English French ChineseTraditional Spanish Japanese Korean German Thailand Vietnam` | UI strings keyed by ID, referenced from the `Word` component on prefabs | `Data_Word`, `WordManager.GetWord(id)` | Dump Chinese column to CSV, `RawCsv` |
| `TornData/Data_Skill.txt`, `Data_Speak.txt` | TSV, 2 header rows | Skill descriptions; battle barks by HP bracket (mostly empty cells) | `Data_*` classes, `File.ReadAllLines` | `RawCsv` |
| `TornData/Data_AI*.txt`, `Data_Effect`, `Data_FlyItem*`, `Data_BattleProperty`, `Data_DuDou`, `Data_MonsterUI`, `Data_Wulin` | TSV | Mostly numeric/config. `Data_Effect.Name`/`Data_FlyItem` names are internal labels (e.g. "弓箭_技能3释放特效") | `Data_*` classes | **Not translated** unless a column is shown in the UI (check in step 2) |
| `LanguageConfig/LanguageConfig_en.txt` (+ `_jt`, `_ft`) | `key=value` lines, `//` comments | ~900 UI/system strings with `{0}` placeholders and `<color=#...>` tags. Existing English still contains leaked Chinese (`冤枉`) | `LanguageLocal.LoadText` | Needs a key=value type (or convert to CSV in the dumper); `_yz` is empty |
| `WulinData/wulin.json` (4.1 MB) | Nested JSON keyed by character name | Runtime/save-style data (character state, `randomItemName`...). Chinese strings are mostly names/IDs | JSON | **Probably not text to translate**; verify (**unverified**) |
| `Json/eyesAvatarData*.json` | Config | No text | | Skip |
| `bundles/*`, `resdata/**/*.unity3d` (4,759 AssetBundles), `resources.assets`, `sharedassets*` | Unity assets | Prefab/UI text: `Text` components with a `Word` ID resolve via `Data_Word`; others are baked-in strings. Not inspected (**unverified**) | Unity | `PrefabText` or runtime hooks |
| Hard-coded literals in `Assembly-CSharp.dll` | IL string literals | Concatenated/formatted UI strings, e.g. in `Debug` and UI code | | `DynamicStrings` (Mono, not IL2CPP) |
| `Mod/` (game root) | empty | The game's own mod support. **Decision: not used for our patch** (see "Where the plugin reads from") | | |

### Excel files with a Chinese and an English variant

The game has three variants of language handling, which matters for what we dump and what we replace:

1. **Separate `_xx` workbooks, same sheet layout:** `Touch`, `SandBox_Touch`, `TaskNew`,
   `TaskProcess` (`_en` `_fr` `_de` `_ja` `_ko` `_es` `_ft` `_th` `_vi`). The Chinese base is the
   unsuffixed file.
2. **Parallel `_EN` workbooks:** `NPC.xlsx` / `NPC_EN.xlsx`, `ECA.xlsx` / `ECA_EN.xlsx` (the `_EN`
   file is used when `WordManager.Open`).
3. **Parallel `...英文`/`...English` sheets in one workbook:** `item.xlsx` (`装备`/`装备英文`,
   `秘籍`/`秘籍英文`, ...), `SkillBattle` (`招式配置`/`招式配置英文`), `地图表` (`MapConfig`/
   `MapConfigEnglish`), `JingLuo`, `Cook`, `CookJingYing`, `TaskDes`, `引导表`, `WorldEvolution`,
   `aptitude`, `inborn`.

Everything else (for example `monster`, `shop`, `shop3`, `姓名库`, `资源采集`, `RefreshBoss`, `Poetry`,
`box`, `GangData`, `LoadingTip`) has **only a Chinese variant** and is shown as Chinese in every
language unless the Chinese is replaced.

Observed in the English variants: item/NPC/quest names and descriptions are translated, but columns
such as `item.装备英文.主属性` (`攻击力+32`) and `按钮文本` are still Chinese, and several JSON
cells still contain Chinese. A systematic pass therefore needs to scan every cell, not just the
"text" columns.

### Chinese cell counts (Chinese base sheets, rough scan)

Largest first: `monster` (~15k per difficulty sheet, but most are config/remark columns),
`Touch` (15.5k), `SkillBattle` (10k), `装备随机掉落配置` (8.4k), `ECA` (~7k), `item` (7k + 4k +
3.7k...), `SandBox_Touch` (5.9k), `NPC` (3.7k+2.8k), `shop`/`shop3` (3.7k each), `姓名库` (3.2k),
`TaskDes` (2.3k), `TaskProcess` (large, JSON), `skillgrow` (2k). Most of these cells include
designer remark columns and ID-lookup strings that must **not** be translated, so per-sheet column
curation (`SkipColumns`) is the bulk of step 3. Counts include both text and non-text cells.

## Risks found

- **Strings used as keys.** Some tables look up rows by Chinese name (e.g. `GetDictionary<string,
  BattleSkillData>(..., "招式配置", "A2", "name")`). Translating a key column without also
  translating every reference to it will break the game. Each translated column needs a check for
  being a key or being referenced from another sheet (`avatarData` `dataName:人贩喽啰（通用）`,
  `monster` model names, `item` `道具名` used in `装备随机掉落配置`, etc.).
- **Parsed strings.** Stat strings such as `攻击力+32`, `1001+16`, and `;`-delimited lists are
  parsed or matched by code. Treat as non-translatable unless proven display-only.
- **Excel written back by the plugin is unnecessary** if injection is done at load time (below).

## Proposed design: plugin dumper (step 2)

1. **Read the xlsx files directly** with the game's `MiniExcel.dll`, not via Harmony. The files are
   static, so no scene/game state is needed and no patch can break the game. Run from a one-shot
   `Start`/coroutine after `Application.streamingAssetsPath` is available, behind
   `Config.Bind("Dump", "DumpOnStart", false)`, plus a toggle for `ExitAfterDump`.
2. **Dump only Chinese-base content:** skip `_en/_fr/_de/_ja/_ko/_es/_ft/_th/_vi` workbooks, `_EN`
   workbooks, and sheets whose name ends in `英文`/`English`. This avoids translating the nine
   existing languages. (Optionally write the existing English next to it as a reference column in a
   separate `Files/Raw/Reference/` tree, not in the translation path.)
3. **Output layout:** `Files/Raw/Dumped/<Workbook>/<Sheet>.csv`, UTF-8 with BOM, a CSV row per Excel
   row, quote-aware, first column(s) being the row's original index/ID. Write cell values as text
   exactly as loaded (including `\n` in cells, escaped consistently).
4. **JSON cells:** the plugin flattens JSON-in-cell columns into a second CSV
   (`<Sheet>.<Column>.json.csv`) with one row per translatable string (`rowId`, `jsonPath`,
   `text`), by walking the parsed JSON and emitting only string values that contain CJK and whose
   property name is in a curated allow-list (`talk`, `name`, `content`, `text`, `dialog`...). The
   allow-list is the step 3 work; for the first dump, emit every CJK string with its path so the
   list can be built from data. Alternative: extend LlmKit's `RawJson` (additive only) to address
   cell JSON by `SplitPath`. Decide after looking at the first dump.
5. **TSV and key=value files:** `TornData/Data_Word.txt` (Chinese column only), `Data_Skill.txt`,
   `Data_Speak.txt` and `LanguageConfig_*.txt` are plain text; copy/convert to the same CSV layout
   so `RawCsv` handles them. Preserve the two TSV header rows.
6. **A manifest** `Files/Raw/Dumped/manifest.json` recording game version (`LanguageConfig` has
   `Version=v_1.2.0.1_v`, which matches `GameVersion.txt`), file, sheet, row count, and column
   headers, so re-dumps after game updates can be diffed.
7. **Separate pass for prefab/UI and code strings** (not in the first dump): hook `Text`/TMP `text`
   setters with Harmony, or enumerate loaded bundles, to log CJK strings seen at runtime into a
   `DynamicStrings`-style file. Do this only after the table translation is working, to see what
   Chinese remains on screen.

## Where the plugin reads from

Decision: the plugin reads the packaged translation from the BepInEx plugins folder
(`<Game>\BepInEx\plugins\resources`, next to the plugin DLL), **not** from the game root `Mod/` folder,
even though the game supports modding there. That keeps the patch self-contained in the installer's
files, so "Uninstall patch" removes it and it cannot collide with other mods or with the game's own
mod loader. Steps 6 and 7 in `Tests/FileOutputWorkflowTests.cs` use `BepInEx/plugins/resources`, and
the plugin resolves it from its own assembly location rather than a hard-coded path.

## Injection (step 5, done)

Implemented as described in [text-injection.md](text-injection.md): a Harmony postfix on `ExcelUtil.GetListOject<T>` and the `Data_Word`,
`Data_Speak` and `LanguageLocal` loaders overwrite the loaded values from the packaged files in `BepInEx\plugins\resources`, in Chinese
and English mode alike. The rejected alternative (writing translated `_en` workbooks over the originals) needs an xlsx writer and breaks on every game update.

## Proposed changes to the plan

- **Step 1** is done for tables and loaders; leave two items open: UI/prefab text in bundles and code
  literals. Do them during step 5 (runtime log), not before, because the Word/`Data_Word` route
  already covers most UI text.
- **Step 2** becomes "dump xlsx/TSV directly via MiniExcel, no Harmony", with a flattening of JSON
  cells. Harmony is only needed at step 5.
- **Step 3** gets a new sub-task: a per-sheet column inventory marking which columns are display
  text, keys, references or remarks. Do not start with `SkipColumns` guesses; generate the inventory
  from the dump (columns that contain CJK, with sample values, and whether the value appears as a
  key elsewhere).
- Add a **language-handling decision** to step 5: the patch must work when the player's Steam
  language is English *and* Chinese. Recommendation: patch the load path so our English wins in both.
- Add a **step 4 prerequisite**: build a glossary from the existing `_en`/`Data_Word` English
  *only as candidate terms*, since the user considers the official English poor.
