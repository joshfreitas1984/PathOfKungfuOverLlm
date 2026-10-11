# Random NPC name pools

## How the game picks names

`GDataManager.InitNameData` loads `WorldEvolution` sheet `name2` when `WordManager.Open` is true (every language except
Chinese) and sheet `name` otherwise. `GetARangeName(isMale)` returns a random surname and a random male or female given
name, and callers join them as `surnam + given` with nothing between (`NPCData.name = nameX + nameM`).

Both sheets have columns `id, surnam, male, female`. The game counts the non-blank cells per column and picks
`Range(1, count)` by ID, so each column must stay gap-free from ID 1.

| Sheet | Content |
| --- | --- |
| `name` | 337 Chinese surnames, 1,422 male and 1,395 female given names |
| `name2` | Western names (Smith, James, Elizabeth...), about 524 rows; this is what English players see |

The standalone `姓名库` workbook duplicates `name` and no game code reads it.
`WorldEvolutionManager.InitJob` always reads `name` (Chinese), even in English mode.

## The fix

[`Files/NamePools/GeneratePinyinNames.py`](../../Files/NamePools/GeneratePinyinNames.py) converts `name` to pinyin with
`pypinyin` and writes `Files/NamePools/WorldEvolution__name2.csv` with the same IDs and blanks.

- Surnames are one word, compound ones too (`Gongye`). The space between surname and given name is added by the plugin,
  not stored in the pool (see Injection).
- Given names are one word (`Birong`), with an apostrophe where a syllable starts with a vowel (`Zi'an`).
- Surname readings are overridden where they differ from the usual reading (`乐` Yue, `单` Shan, `解` Xie, `曾` Zeng...).
- u-umlaut after l/n is `yu` (`吕` Lyu), the glossary's convention.
- Given-name readings are pypinyin's defaults. Spot check any that look wrong; they are not hand reviewed.

`Tests/NamePoolTests.cs` checks the output against the Chinese pool (IDs, blanks, ASCII only, surname readings).

## Injection

All in [`PathOfKungfuPlugin/`](../../PathOfKungfuPlugin/) (`NamePools.cs`, `NamePoolPatch.cs`, `SavedNameMigration.cs`). The
pool and the original Western pool are embedded in the DLL from `Files/NamePools/` (rebuild the plugin after
regenerating). The patches apply in every game language, Chinese included, so the mod works whichever Steam
language the player uses; set `[Names] PinyinNpcNames = false` in the BepInEx config to turn them off. Built, **not yet run in the game**.

| Hook | What it does |
| --- | --- |
| `GDataManager.InitNameData` postfix | Swaps `m_anmeDataDic` for the pinyin pool (replaces `name2` in English and `name` in Chinese) |
| `GDataManager.GetARangeName` postfix | Puts a space after the surname in the returned tuple. Callers build the NPC name as `Item1 + Item2` |
| `NPCData.nameX` setter prefix | Trims the stored `nameX`, so a surname shown alone has no trailing space |
| `NPCData.name` setter prefix, `GetOriginName` postfix | `name == nameX + nameM` becomes `nameX + " " + nameM` (children, renames, spouses, mod editor) |
| `WorldEvolutionManager.InitData` postfix | Converts NPCs already in the save (below) |
| `WorldEvolutionManager.InitJob` prefix | City job NPCs: runs a copy of `InitJob` with a pinyin name and translated job name (`Zhao Chen (Shopkeeper)`) |
| `UINpcNamed` patches (`ChildNamePatch.cs`) | Child naming screen: Latin letters allowed in every language, field length at least 24, the random button fills pinyin without the join space, and the duplicate-name check compares the spaced name |

## Existing saves

`NPCData.name`, `nameX` and `nameM` are serialised in the save and loaded as stored, so a save started in English mode
keeps its Western names. When a game starts, `SavedNameMigration` walks every `NPCData` on `PlayerData` (found by
reflection over its read/write properties) and converts NPCs whose surname and given name are both in the original
`name2` pool (`WorldEvolution__name2.western.csv`; it only has about 25 names per column), or in the Chinese pool.

- Surnames map one to one (`Smith` always becomes the same pinyin surname), so families stay together.
- Given names are picked from the whole pinyin pool using the NPC id and sex, so a save converts the same way every time.
- The new names are written into the NPC and persist at the next save. Text around the name (gym titles) is kept.
- City job NPCs saved as `赵晨(掌柜)` are converted to `Zhao Chen (Shopkeeper)` by looking the Chinese name up in the Chinese pool
  (`WorldEvolution__name.chinese.csv`), so it is the exact pinyin of the same row.
- NPCs generated in Chinese mode (Chinese surname and given name from the Chinese pool, not story NPCs) become the pinyin
  of the same pool row.
- Not converted: names stored as text elsewhere (quest logs, messages) and NPCs the player renamed.

## Known gaps

- `InitJob` is replaced rather than patched, so a game update that changes it will not be picked up. The job sheet has
  one row, `(掌柜)`, translated by a one-entry table in `JobNpcNamePatch`; move it to the translation pipeline if the
  sheet grows.

## Story NPC names in old saves

Not a name-pool issue, but the same save problem. `WorldEvolutionManager.MakeMasterData` copies each story NPC's name from the
Touch table into the save when a new game starts and never refreshes it, so a save made before the translation was
installed keeps the game's own English (`The Coon Lady` for 浣衣娘, now `Washerwoman`). `StoryNpcNameRefresh` sets those
NPCs (`playerData.m_touch`) to the current table name at game start. It skips NPCs with a surname or given name, mod NPCs and
gym guards, and logs the first five changes. Turn it off with `[Names] RefreshStoryNpcNames = false`.
