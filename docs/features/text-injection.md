# Text injection (plan step 5)

Status: written, checked offline against the real workbooks, and loaded in the game (English mode): the log shows every sheet matching exactly as in the offline run. What the player sees on screen is still to be checked.

The plugin replaces the game's text while the game loads its data. No workbook is written, so a game update cannot be broken by
an out-of-date copy, and removing the plugin DLL and the `resources` folder restores the game's own text.

## Where the translation comes from

`6. Package to Game Files` ([`Tests/FileOutputWorkflowTests.cs`](../../Tests/FileOutputWorkflowTests.cs)) packages `Files/Mod` and copies
it to `<game>\BepInEx\plugins\resources`. The plugin finds that folder next to its own DLL
([`Injection.cs`](../../PathOfKungfuPlugin/Injection.cs)); `7. Package Release` ships the same folder.
Packaging also does two things for the plugin ([`TranslationPackaging.cs`](../../Translate/TranslationPackaging.cs)):

- `translated-columns.json` lists, per CSV, the columns that were translated (index 0 is the dumper's Row column). The CSVs keep every
  column of the dump, so without it the plugin could not tell translated columns from untouched ones, and it writes **only** these.
- The two header rows of each sheet are copied back from the raw dump. The header rows are translated like any other row of a translated
  column (`地图名称` became "Map Name"), and the plugin finds the game's columns by those names.

## What is hooked

| Source | Hook | Code |
| --- | --- | --- |
| Excel sheets (`NPC`, `item`, `ECA`, `SkillBattle`, `Touch`, ...) | A transpiler on every `GDataManager` method (and its async state machines) that calls `ExcelUtil.GetListOject<T>` / `GetDictionary<TKey,T>` points the call at `Injection.GetListOject<T>` / `GetDictionary<TKey,T>`, which call ExcelUtil and then inject. **Not** a Harmony patch on ExcelUtil itself: see below. | [`SheetInjector.cs`](../../PathOfKungfuPlugin/SheetInjector.cs) |
| Dialogue JSON inside cells (`talkLib`, `searchTalks`, `mainTouch`, ...) | Same hook; the flattened `*.json.csv` is applied by path to the cell's JSON. | [`JsonInjector.cs`](../../PathOfKungfuPlugin/JsonInjector.cs) |
| `TornData/Data_Word.txt` (UI strings) | Postfix on `Data_Word.Onload`; the English goes into the Chinese, English and Traditional lists, so whichever language is chosen shows it. | `DataWordPatch` |
| `TornData/Data_Speak.txt` (battle barks) | Postfix on `Data_Speak.Onload`. | `DataSpeakPatch` |
| `LanguageConfig_jt.txt` (system messages) | Postfix on `LanguageLocal.LoadText` (the only table the game loads, for every language). | `LanguageConfigPatch` |

| NPC text tokens (`a`, `p`, `lp`, `gangName`...) in Chinese mode | Prefix/finalizer on `FunTool.ConvertText` that sets `WordManager.Open` for the call when the text has no CJK, so English text takes the English branch (the Chinese branch replaces the letters `a b i p x` everywhere). | `ConvertTextEnglishBranchPatch`, `EnglishTokenBranch` |

Config: `[Translation] Enabled` (default true) in `BepInEx/config`. Turn it off to see the game's own text. `[Translation] EnglishTokensInChineseMode` (default false, untested in game) turns the `ConvertText` patch on; the mod currently targets English mode only.

## Why ExcelUtil is not patched

The first version put a Harmony postfix on `ExcelUtil.GetListOject<T>`, one closed instantiation per row class. Mono shares one body for all
reference-type instantiations, and the patch made every call run as the first patched type: `DebugConfig` rows were read as another class,
`LoadData_Must` threw `KeyNotFoundException: 1` and the game stayed on a black screen. Rewriting the callers avoids touching the shared body.
`GetDictionary` is rebuilt in `Injection` on the translated list, so keys that are translated names (skill names) are the English ones.

## How a sheet is matched

- **Language.** The game loads `NPC_EN`, `Touch_en`, `装备英文`, `MapConfigEnglish` ... when the player's Steam language is not Chinese. The packaged
  files are named after the Chinese base (`NPC__Dialog.csv`), so the `_xx` workbook suffix and the `英文`/`English` sheet suffix are stripped
  and the same file applies in both modes. `WordManager.Open` is never consulted.
- **Columns.** The game maps header cells to properties by name (`MiniExcel`); the plugin does the same with the packaged header row, so a CSV
  column lands on the property the game would have read it into. A translated column with no string property (the game never reads it) is
  reported in the log.
- **Rows.** Paired by the sheet's `id` column (or, without one, the first mapped column), not by position or by the dump's Row number, because the
  game drops blank rows and English workbooks have fewer rows. Duplicate keys are paired in order.
- **Blank cells** in the CSV keep what the game loaded. Line breaks are restored from the dump's `\n` (kept literal where the game's own cell writes `\n`).
- **Dialogue JSON** is addressed by path (`.name`, `[2]`, `{json}` for a JSON string inside a JSON string, `;1` for segment 1 of an `action`),
  which finds the same place in the Chinese and the English workbooks. Only the translated strings change; everything else in the cell is byte-for-byte
  what the game had (checked).

Every sheet logs one line, for example `Touch/Touch: Touch__Touch: 6107/6107 rows matched (6107 packaged), 11822 cells changed, dialogue 12165 applied / 0 not found`.
It is a warning when rows or dialogue paths were not matched or a translated column has no property.

## Checked offline

`Tests/InjectionTests.cs` covers CSV reading, language-name stripping, id pairing, line breaks and JSON cells on small made-up sheets. The plugin's
injection code was also run over every translated sheet of the real xlsx files, in Chinese and English variants (a throwaway harness, not in the repo):
every sheet matched all of its rows and all dialogue paths, apart from 6 dialogue paths in `TaskProcess_en` (the English workbook differs there), and the
dialogue cells round-trip unchanged apart from the translated strings.

## Findings

- `ECA__EffectData` column 6 (the translated "effect text"), `ECA__PassiveSkillData` column 2 (`技能总称`) and `SkillBattle__招式配置` column 23
  (`ReplaceDes`) are columns the game does not read (blank header or no property), so translating them has no effect in game. They can come out
  of `TextFileConfiguration.cs`.
- The game picks one of two files per language for most tables; injecting into both means a player on any language sees the same text.

## Still to confirm in game

- English text on screen, overflowing the UI (plan step 5, last bullet), and anything still Chinese: UI/prefab text, hard-coded strings and the tables that were
  never translated (`monster`, shops, drop tables, name pools) need a runtime pass, see [`text-files-to-split.md`](text-files-to-split.md).
- Chinese mode (Steam language Chinese) was checked offline only.
