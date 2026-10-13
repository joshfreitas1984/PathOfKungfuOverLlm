# TextFilesToSplit (plan step 3)

[`Translate/TextFileConfiguration.cs`](../../Translate/TextFileConfiguration.cs) lists 50 dumped CSVs. The export workflow has **not** been run
on them yet; `Tests/TextFileConfigurationTests.cs` checks the list against the dumped files (read-only).

## Dump layout changed to flat names

LlmKit writes `Raw/Export/{Path}.yaml`, `Converted/{Path}.yaml` and `Mod/{Path}` without creating subfolders, so
`TextFileToSplit.Path` cannot contain `/`. The dumper now writes `Workbook__Sheet.csv` (for example
`item__装备.csv`, `Touch__Touch.json.csv`, `TornData__Data_Word.csv`,
`LanguageConfig__LanguageConfig_jt.csv`) and the existing dump was renamed to match. Older docs that say
`<Workbook>/<Sheet>.csv` refer to the first layout.

## How the columns were chosen

Each file translates only the listed columns (`TranslateOnly(width, columns)`; column 0 is the dumper's Row column).

1. **The game as oracle.** For every Chinese sheet with a shipped English counterpart, a column counts as display text if the
   developers translated at least 30% of its Chinese cells (and 3 or more). Columns they left in Chinese in the English
   sheets (item `类别`, `武器类型`, `主属性` stat strings, effect target, formulas) are enums, keys or parsed strings the code
   compares against and stay untouched. Translated key columns (`招式名称`, `道具名`) are safe, because the game loads the English
   sheets with the same lookups. This gave 44 files.
2. **Chinese-only but shown to the player**, added by hand: `ECA` buff names/descriptions and effect text (the shipped
   `ECA_EN` is still Chinese), `TornData__Data_Word` (UI strings), `TornData__Data_Speak` (battle barks by HP bracket),
   `LanguageConfig` (system messages).
3. **Dialogue:** the four flattened `*.json.csv` files, text column only. The JSON columns in the main `Touch`, `SandBox_Touch` and
   `TaskProcess` CSVs are skipped so nothing is translated twice.

## Workload

About **110k cells, 47k distinct strings, 1.14M Chinese characters**. The flattened `TaskProcess` dialogue alone is 28k distinct
strings and 0.83M characters (73% of it).

## Not covered yet (no shipped English to learn from, so a manual decision)

- **monster**: now covered. The four difficulty sheets (`monster_easy/normal/hard/hardEX`) hold the same 1,424 names, and column 2 (`name`) is
  translated. The battle screen shows it directly (`Monster.textName.text = data.name`), and nothing looks it up by name (the sheets link
  to models by id). `角色模组` model names are not translated. Found when enemy names were still Chinese in battle.
- **shop / shop2 / shop3** (`商品名`), `装备随机掉落配置`, `奖励组`, `RefreshBoss`, `monster_team`: mostly names that exist elsewhere; need a check that the
  code does not compare them to Chinese literals before translating.
- **Name pools** (random NPC names): not translated line by line. English mode loads `WorldEvolution/name2`, a Western pool
  (Smith, James...), which breaks immersion. `Files/NamePools/GeneratePinyinNames.py` builds a pinyin replacement; see
  [name-pools.md](name-pools.md). `姓名库` is not read by the game code.
- **`ECA__Sheet1/2/3`, `skill`, `skill2`, `Data_Skill`**: look like design copies of tables already covered by `SkillBattle`/`ECA`.
- **`NPC__NPC` names**: the game takes NPC names from `Touch`, not from this sheet; the shipped `NPC_EN` leaves them Chinese.
- Remaining small sheets (gangs, `GangName` title pools, Cook, Poetry, `Audio`, `角色动作配置`...) were not judged.
- Code literals and prefab/UI text: dumped and registered, see [dynamic-strings.md](dynamic-strings.md).

To add a file, add one line using the column inventory
([`Files/Raw/ColumnInventory.csv`](../../Files/Raw/ColumnInventory.csv), whose file names use the old `Workbook/Sheet.csv` layout)
and check the cells are not keys or parsed values.
