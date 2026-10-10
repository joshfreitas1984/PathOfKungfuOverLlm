# Dump column inventory (plan step 3 groundwork)

Status: based on the first full dump (game 1.2.0.1, 342 CSVs, `Files/Raw/Dumped/GameData`). The
classes below are **heuristics from the data**, not verified against game code. Nothing has been
translated or injected yet. Full per-column data: [`Files/Raw/ColumnInventory.csv`](../../Files/Raw/ColumnInventory.csv)
(917 columns with Chinese text: file, column number, header, Chinese cell count, distinct count,
average length, JSON share, how often its values appear in other columns, class, sample).

## How the classes were assigned

For every column containing Chinese: header keywords (`备注`, `说明`, `Test`, `策划` -> remark;
`path`/`图标`/`icon`/`skin`/`model` -> asset-ref), cell shape (starts with `{`/`[` -> json; average
length or sentence punctuation -> text; `;:+=_` and digits -> param) and **reference share**: how
many of the column's distinct short values also appear as exact cells in another column or sheet.
A high reference share means the value is a name used as an identifier across tables.

| Class | Columns | Chinese cells | Distinct strings | Meaning |
| --- | ---: | ---: | ---: | --- |
| key/ref | 455 | 177,584 | 13,887 | Short names that recur across sheets (NPC, skill, item, map names). Display text *and* join key. |
| text | 225 | 26,273 | 13,981 | Descriptions, tips, dialogue-like sentences. Plain translation. |
| name | 135 | 8,191 | 2,406 | Short names that do not recur elsewhere. |
| param | 36 | 6,117 | 3,720 | Strings with `;`, `:`, `+`, digits. Mostly parsed by code; translate only if proven display text. |
| remark | 48 | 10,524 | 3,288 | Designer notes. Several are actually shown (see below). |
| json | 14 | 11,675 | 6,155 (about 12M chars) | JSON in cells, mostly dialogue. By far the largest volume. |
| asset-ref | 4 | 1,068 | 91 | Resource paths. Do not translate. |

Distinct, non-JSON translatable strings (text + name + key/ref + param): about **32,800 strings,
783k characters**. The 244k Chinese cells collapse to this because the same names repeat in many
sheets (the four `monster` difficulty sheets are near-identical copies).

## Findings that shape the design

1. **Names are keys, and they repeat everywhere.** `兵痞`, `虞庆` etc. appear in `monster`
   (several sheets and columns), `NPC.交互名称`, `Touch.name`, dialogue speaker names, quest names.
   Skill names (`招式配置.招式名称`) match `ECA` buff/effect names, `shop` goods and drop tables.
   So translate **each distinct Chinese string once** and apply it everywhere it occurs, otherwise
   the same character or skill gets several English names and any name-based lookup (e.g.
   `GetDictionary<string, BattleSkillData>(..., "name")`) breaks. LlmKit already works on lines/
   splits; check that identical source strings are deduplicated and share one translation, and
   seed the glossary from the key/ref columns before the first run.
2. **Dialogue is double-encoded JSON.** `Touch.talkLib`, `Touch.searchTalks`, `SandBox_Touch.talkLib`,
   `TaskProcess.mainTouch`/`otherTouch` hold a JSON array whose `talks` value is *another JSON
   string*. Every cell parsed without error after decoding recursively. Text-bearing paths found
   (counts are Chinese string occurrences, distinct in brackets):

   | JSON path (after decoding) | Meaning | Count |
   | --- | --- | ---: |
   | `[].talks.<json>.[].talk` | Dialogue / narration line | ~34k (~25k) in the four sheets |
   | `[].talks.<json>.[].name` | Speaker name | ~17k (~1.2k): must match the NPC name translation |
   | `[].talks.<json>.[].options.<json>.[].name` | Player choice text | ~12.7k (~6.4k) |
   | `[].talks.<json>.[].action` | Semicolon list, e.g. `16:27000,25,37,1,守卫森严，不宜擅闯。;...`: the last field is a shown message | 180 |
   | `Touch.searchTalks[].talk`, `.name` | Look-at text for map objects | ~920 |
   | `[].triggerValue` (`talk,交互物ID`) | Code, **do not translate** | 14 |

   Dialogue also embeds placeholders such as `<fName>` that must pass through untouched.
3. **Some "remark" columns are shown to the player.** `NPC/SelfAction.文本说明` holds
   `<color=#E64D00>...</color>` text that looks like in-game action text; `ECA/BuffData.说明` and
   `ECA/EffectData` description columns are buff/skill descriptions. Do not drop a column because its
   header contains a designer-note word; check a sample.
4. **Parsed stat strings.** `item/装备.主属性` (`攻击力+32`), `skillgrow` effect strings, `param` columns
   and `;`-lists are probably parsed by the game. They need a code check before translation (see
   Risks in [game-text-sources.md](game-text-sources.md)).
5. **`avatarData`** (`dataName:新角色0625142937;protagonist:1;...`, 3.7k cells) is a key string of a
   character model, not text. Skip.
6. **Row numbers are unreliable on some sheets** (see the dump review). Address cells by the table's
   ID column.

## Where the volume is

Chinese cells by workbook (text / name / key-ref / param / json / remark):

| Workbook | text | name | key/ref | param | json | remark | Notes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| monster | 243 | 0 | 79,119 | 61 | 0 | 916 | 4 duplicated difficulty sheets; mostly names |
| ECA | 3,890 | 9 | 19,997 | 2,976 | 0 | 1,775 | Buff/skill descriptions and names |
| item | 3,303 | 3,006 | 12,240 | 1,176 | 0 | 21 | Items, equipment, manuals |
| Touch | 3,669 | 0 | 6,107 | 0 | 5,747 | 0 | Main dialogue source |
| SkillBattle | 2,691 | 118 | 8,724 | 19 | 0 | 627 | Skills/moves |
| NPC | 2,190 | 118 | 2,788 | 7 | 100 | 4,115 | NPC actions and dialogue |
| TaskProcess | 2,124 | 0 | 2,137 | 0 | 2,415 | 0 | Quest steps (largest JSON, ~4.4k chars per cell) |
| SandBox_Touch | 201 | 0 | 2,446 | 0 | 3,292 | 0 | Sandbox-mode dialogue |
| TaskDes | 1,529 | 0 | 770 | 0 | 0 | 0 | Quest descriptions |
| shop / shop2 / shop3 | ~290 | 0 | 13,900 | 0 | 0 | 0 | Goods; all names already defined in item/skill |
| 姓名库, WorldEvolution | ~180 | ~950 | ~8,600 | 9 | 0 | 49 | Random name pools (surnames/given names): needs a naming strategy, not line translation |
| skillgrow, skill, skill2 | ~1,500 | 15 | ~2,900 | ~1,100 | 0 | ~1,750 | Skill effect text |
| LanguageConfig, TornData | 1,018 | 971 | 1,019 | 0 | 0 | 0 | UI strings (`Data_Word`: 971 names) |

(106 workbooks in total; the remaining ~90 are small.)

## Proposed order for step 3

1. **Names first.** Build the name list (NPC, skill, item, map, faction) from the key/ref and name
   columns, translate it once, and put it in the glossary. This is small (about 16k distinct strings,
   mostly 2-4 characters) and fixes consistency for everything else.
2. **Plain text tables** (`text` class + `Data_Word`, `LanguageConfig`, item/skill/ECA
   descriptions, `TaskDes`, `tip`): about 14k strings, 680k characters. Configure as `RawCsv` with
   `SkipColumns` taken from the inventory: skip every column whose class is not text/name/key-ref.
3. **Dialogue JSON** (`talk`, `name`, `options.name`, `action` message): the dumper has to flatten
   these into one row per string with its path first. Using the paths above, a pre-pass can emit
   `file,rowId,jsonPath,text` rows (about 60k occurrences, about 35k distinct). Decide between
   flattening in the dumper (recommended, as in the earlier design) and extending LlmKit `RawJson`
   with `SplitPath` over double-encoded JSON.
   **Implemented:** the dumper now writes `<Sheet>.json.csv` next to each sheet that has JSON cells
   (`Touch`, `SandBox_Touch`, `SandBox_NpcFromTouch`, `TaskProcess`), using
   [`JsonFlattener`](../../PathOfKungfuPlugin/JsonFlattener.cs). Columns: `Row, RowId, Column, Path, Text`
   (`RowId` is the sheet's first cell, the stable key; `Column` is 1-based in the main CSV). Path
   syntax: `.name` property, `[2]` array index, `{json}` = the value was a JSON string that was
   decoded, `;1` = segment 1 of a `;`-separated `action` string (text is that segment's message after
   its leading code fields). `triggerValue` is never emitted. The JSON cells stay in the main CSV and
   must be in `SkipColumns` there so they are not translated twice.
   Result on the current dump: **67,231 strings**: 24k `talk` + 15k speaker `name` + 8k option `name`
   in `TaskProcess`; 9.7k `talk` + 4.5k option + 3k name + 1.8k look-at text in `Touch`; a few hundred
   `action` messages; `SandBox_Touch` adds the remainder. Files are 6.6 MB in total.
   These first files were produced by running the same `JsonFlattener` code offline over the existing
   dump, so the next in-game dump should reproduce them exactly (compare line counts: 48,255
   `TaskProcess`, 12,166 `Touch`, 6,115 `SandBox_Touch`, 699 `SandBox_NpcFromTouch`).
   Not yet done: the injector that walks a path and writes the translation back (step 5), and a
   round-trip check that re-serialised cells are accepted by the game.
4. **Naming pools** (`姓名库`, `WorldEvolution.name`): these are random-name generators for NPCs. Decide
   separately (likely transliterate with a consistent scheme, since there are about 2.8k given names).

## Open questions

- Is each `key/ref` column really used as a lookup key in code? The decompile can answer this per
  table (search for the column's class and look for string-keyed dictionaries); do it for `monster`,
  `shop`, `装备随机掉落配置`, `item` before translating those columns.
- Which `remark` columns are shown in the UI (start with `NPC.文本说明` and the `ECA` description
  columns)?
- Whether `monster_easy/normal/hard/hardEX` names differ at all (they appear identical for names,
  so translating one and copying is enough).
