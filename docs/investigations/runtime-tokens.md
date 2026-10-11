# Runtime tokens in the game's text

> **How tokens are stored (rolled out 2026-10-11, LlmKit `docs/features/translation-pipeline/token-slots.md`).** `Files/Converted` holds `{slots}` in `text` and
> `translated`, and each split (and template) a `tokens` map from slot to the real token. Nothing is masked or restored while translating, validating or running QC; packaging
> is the only place values are put back (`///token///` for the NPC files, the game's own form elsewhere). The tokens in a template (the lone `a` in `<color=#E64D00>a</color>{0}`, 3,936 in
> `NPC__SelfAction`) are slotted too; before, they were packaged bare. Golden packaged output for a fixed set of rows: `Tests/PackagingGoldenTests.cs`, `Tests/Golden/`.

Found by decompiling `Assembly-CSharp.dll` (ilspycmd 9.1) and scanning `Files/Raw/Dumped`. Source of truth for the substitution is
`FunTool.ConvertText` plus `TalkData.ReplaceTalkNames` and `UIMain` (talk options). The English-mode wrapper is confirmed from the game's own data; the in-game effect of our current output is not yet checked.

## Two substitution paths

| Path | Where | Chinese mode (`WordManager.Open` false) | English mode (`Open` true, every non-Chinese language) |
| --- | --- | --- | --- |
| `TalkData.ReplaceTalkNames` / talk options | JSON dialogue (`Touch`, `TaskProcess`, talk options) | `<name>` `<fName>` `<lName>` `<gender>`, `<A\|\|B>`, `<faction=N,A\|\|B>` | same: works with the bare tokens |
| `FunTool.ConvertText` | NPC dialogue and actions (`NPC__Dialog`, `NPC__NpcDialog`, `NPC__NpcDialogLips`, action files, life text, pigeon messages) | bare tokens, `String.Replace` everywhere in the string | only the **`///token///`** form is replaced (the game's own English sheet `PigeonMsgData2` writes `///<fName>///`, `///<name>///`) |

Confirmed against the game's own English workbook (`StreamingAssets/resdata/ExcelData/NPC_EN.xlsx`): the wrapped form is used in
`Dialog` (157), `NpcDialog` (30), `NpcDialogLips` (78), `PlayerAction` (47), `SelfAction` (3,645), `NpcAction` (109) and `proAction`
(103) - tokens seen: `///a///` `///b///` `///p///` `///fp///` `///lp///` `///i///` `///i1///` `///i2///` `///x///` `///gangName///`
`///mapName///` `///gangWuxue///` `///lastRandomAttributeName///`. Titles are written `[Young Hero||Heroine]` there. The JSON talk
files (`Touch`, `TaskProcess`) use bare tokens.

`[A||B]` and `<A||B>` are resolved in both modes (`ReplaceCall` handles only the first `[...]` pair in a string).

## Tokens in `ConvertText`

| Token | Replaced with | Seen in data |
| --- | --- | --- |
| `gangName`, `mapName` | gang of / parent map of the current map | `NPC__Dialog`, `NPC__NpcDialog` (bare, inside Chinese sentences) |
| `gangWuxue` | a **Chinese sentence** built at runtime ("所擅《...》更是威名远扬") | `NPC__NpcDialog` |
| `lastRandomAttributeName`, `gangJoinCDRemain` | attribute name / days | `NPC__SelfAction` |
| `<name>` `<fName>` `<lName>` | player full / first / last name (code replaces `lName>`, leaving `<`) | everywhere |
| `i`, `i1`, `i2` | the NPC's random item name | action files |
| `a`, `b` | the NPC's name / the second NPC's name | `NPC__NpcDialog`, `NPC__PlayerAction` |
| `x` | a random digit | action files |
| `p`, `fp`, `lp` | player full / first / last name; `fp[大哥\|\|姐姐]` = first name + title by player sex | `NPC__Dialog` (about 130 uses) |
| `{0}` | the NPC's name (`string.Format`) | handled by LlmKit's numbered mask |

In Chinese mode the single letters are replaced anywhere in the string, which is why the data has no stray Latin letters.

## Other tokens seen

- `<city>`, `<x>`, `<y>` in `BetShop__SkillTask` quest text (16 uses): no substitution found yet in the code searched.
- `<id>`, `<ChouDi>`, `<JuanLv>`, `<PeiOu>`, ... sit in condition/code columns (`JiaoQing<ChouDi>`), not display text.
- `{json}` is the dumper's flattening marker, not game text.
- `<gender>` exists in code (男/女) and not in the dump.

## Consequences (unconfirmed in game)

1. Packaged text contains no `///` anywhere. In English mode every bare `ConvertText` token in `NPC__*` text would show literally
   (`gangName`, `a`, `lp`). Needs an in-game check, then a restore-as-`///token///` step at packaging.
2. The splitter currently keeps these tokens in templates and translates the Chinese around them in pieces
   (`{0}gangName{1}mapName{2}`), the same sentence-fragment problem `<fName>` had.
3. `gangWuxue` injects Chinese text at runtime; it needs a plugin patch (Harmony on `ConvertText`) or a fixed English replacement.

## Letter tokens are confined to the NPC files

A scan of the dump for `a`, `b`, `p`, `i`, `i1`, `i2`, `fp`, `lp` touching CJK text (not inside tags) found them only in `NPC__SelfAction`
(3,943), `NPC__dialogChange` (470, not a `///` sheet in the English data: check it is display text), `NPC__NpcAction` (359),
`NPC__NpcDialogLips` (307), `NPC__Dialog` (179), `NPC__PlayerAction` (116), `NPC__proAction` (108) and `NPC__NpcDialog` (47). `x` appears
once each in `ECA__Sheet3` and `SkillBattle__招式配置` (a multiplier, not a token), so leave `x` out.

## Packaging versus a game-side hook

`///token///` for the NPC files and bare `<fName>` for the JSON talk files means the form depends on the file. Options:

- **Restore in the game's form** (`restoreAs`, now done at packaging): letter and word tokens (`///a///`, `///gangName///`) are unambiguous.
  `<name>`/`<fName>`/`<lName>` would need `///<fName>///` only in the NPC files; they are not in the NPC files' tokens, so a packaging fixup
  (`GameHooks.CustomPackagingFixup`) would be needed if that turns up.
- **Harmony hook** on `FunTool.ConvertText` and `TalkData.ReplaceTalkNames` accepting one canonical token everywhere. Covers the
  runtime-built `gangWuxue` sentence too, but `UIMain` and `NpcTalk` also replace `<name>` inline (not patchable as methods), so
  it cannot be the only route.

## Chinese-mode players (Steam language Chinese)

The plugin injects the same English into both language modes (`text-injection.md`), so a Chinese-mode player runs our English through
`ConvertText`'s Chinese branch. There, `Replace("a", npcName)`, `Replace("i", ...)`, `Replace("p", ...)`, `Replace("b", ...)` run on the
whole string, so every "a" in an English sentence becomes the NPC's name, whatever token form we use. `///token///` is not recognised in
that branch either. So NPC-path English text cannot work in Chinese mode without a hook.

Fix written, not yet tried in game: `ConvertTextEnglishBranchPatch` (config `[Translation] EnglishTokensInChineseMode`, default off: the mod targets English mode for now) - a Harmony prefix/finalizer on `FunTool.ConvertText` that sets `WordManager.Singleton.Open = true` for the call
when the string has no CJK (restoring it after), so the English branch and `///token///` apply in both modes. `Open` is a public field read
once at the top of the method. To check: other code reached from `ConvertText` (`GetName()`, item and NPC name lookups) may also read
`Open`, and untranslated Chinese strings keep the original branch because they contain CJK.

## Implemented (English mode)

All in `Files/Config.yaml: namedTokenReplacers` (the model reads `{npcName}`-style slots with a description; packaging restores them in the game's form), with
`GameFileHandling` patterns keeping each token inside its sentence (`ValidateRuntimeTokens` and `PlayerTokenPrompt.txt` needed no change), and `Tests/NamedTokenTests.cs` checking the config, the splitter and every
token in the six NPC files. No `translate` (phrase) tokens are configured.

| Source | Slot | Restored as |
| --- | --- | --- |
| `gangName` `mapName` `gangWuxue` `lastRandomAttributeName` `gangJoinCDRemain` | `{gangName}` `{mapName}` `{gangMartialArts}` `{attributeName}` `{cooldownRemaining}` | `///word///` |
| `a` `b` | `{npcName}` `{otherNpcName}` | `///a///` `///b///` |
| `p` `fp` `lp` | `{playerName}` `{playerFirstName}` `{playerLastName}` | `///p///` `///fp///` `///lp///` |
| `i` `i1` `i2` | `{randomItem}` `{randomItemOne}` `{randomItemTwo}` | `///i///` ... |
| `<A||B>` and `[A||B]` titles | `{playerTitle}` | the glossary pair in `<A||B>` form (kind `glossary`, resolved at packaging, so a glossary fix needs no retranslation) |
| `<fName>` `<lName>` `<name>` `<faction>` | `{firstName}` `{lastName}` `{fullName}` `{faction}` | unchanged (only in the JSON talk files, where the game takes the bare form) |

Letters count only with Chinese text, a tag, punctuation or a string end on both sides, so ordinary English words and `x` are left alone.
Pronoun alternatives (`<他||她>`) stay visible. The ten bracket titles that had no `<A||B>` glossary entry got new English pairs in
`Glossary/Player-Gender-Tokens.yaml`; they are first drafts to review. NPC files not translated by this pipeline (`NPC__NpcAction`,
`NPC__dialogChange`, `NPC__NPC`) are not covered.

## Rollout result (2026-10-11)

Re-export and merge, then a full translation. Checked on a copy, not in game:

- Every slot in the translated text matches the source's slots (0 mismatches in 121,648 splits); no `{slot}` is left in the packaged Mod files.
- Packaged Mod: `///a///` 3,813, `///p///` 272, `///gangName///` 6, `///mapName///` 3, bare `<fName>` 2,122 (JSON talk files).
- Splits that were cut around a token before are whole sentences now (`NPC__Dialog` 1,205 to 617 splits, `NPC__NpcDialog` 784 to 604).
- Lines still untranslated: 405 (239 before the change), nearly all `flaggedForRetranslation` for glossary or validation reasons, 300 of them in `TaskProcess__TaskProcess.json` and mostly sentences with `{firstName}`/`{playerTitle}`. Another translate pass retries them.

## Not done

- `gangWuxue` still becomes a Chinese sentence at runtime (needs a plugin patch or a fixed English phrase).
- The `ConvertText` Chinese-mode patch is off by default; Chinese mode is not supported for now.
- In-game check that `///token///` is substituted, and that a restored `<A||B>` title reads correctly.
