# Context hints for runtime-token lines

Status: proposed, not started. Follows the named-token work (`Config.yaml: namedTokenReplacers`, see
[`features/glossary-seed.md`](../features/glossary-seed.md) "Named tokens").

## Problem

A line holding `{firstName}`, `{playerTitle}`, `{fullName}` or `{faction}` reaches the model with only a one-line description of
the slot. Nothing says who is speaking, who is being addressed, or how they relate. The only line context today is
`CharacterLineContext`: a character named in the text is told their gender. The player is deliberately unlisted (they pick a
gender in game), so the model has no cue for the player and invents one (`InventedGender`/`WrongGender`, about 25 flags) or flattens
the register of a title ("Young Hero" from a stranger vs "Big Brother" from a younger sister).

## Hypothesis

Telling the model who speaks to whom improves pronoun correctness and title register on token lines. Unproven: extra prompt text can
also confuse the 14B translator, so each hint is added behind a measured before/after run.

## Candidate hints (cheapest first)

1. **Addressed party.** When `{playerTitle}` or `{firstName}` is present the listener is the player. Add to the slot descriptions in
   `Config.yaml` ("the speaker is not the player"). No new data needed.
2. **Speaker facts.** Speaker gender, age band and role (shopkeeper, sect elder, gang member) via `GameHooks.LineContextProvider`.
   Needs a speaker per line; not yet checked whether the dumps carry one (see step 1 below).
3. **Relationship cues in `NPC__Dialog`.** Its condition columns (`playerParam`/`term`/`npcParam`: `sex`, `ager`, `love`, `sect`)
   say how the player relates to the speaker (row 9 `sex // 1` is only shown to a male player). Turn them into a hint such as
   "the player is younger than the speaker". Only some rows have conditions; a row with a `sex` condition tells us the player's
   gender, so such lines would not carry gender-alternative tokens.

## Steps

1. **Inventory speaker data.** For each file with token lines (`SandBox_NpcFromTouch__Touch.json.csv`, `SandBox_Touch__Touch.json.csv`,
   `TaskProcess__TaskProcess.json.csv`, `Touch__Touch.json.csv`, `NPC__Dialog`, `NPC__NpcDialog`) list which columns identify the
   speaker or NPC (check `Files/Raw/ColumnInventory.csv`). Output: a table of file, speaker column, how many token lines have one.
2. **Build the baseline sample.** Fix about 60 token lines: all current `InventedGender`/`WrongGender` lines plus a spread of title and
   name-token lines. Record current flags and translations. Use the LlmKit gold-set approach so it can be re-run.
3. **Hint 1 (addressed party).** Description wording only. Retranslate the sample, compare flags and read the diffs.
4. **Hint 2 (speaker facts)**, if step 1 found speaker data. Extend `CharacterLineContext` or add a sibling provider; extend
   `Files/CharacterGenders.yaml` only if the speaker is a named NPC missing from it. Retranslate, compare.
5. **Hint 3 (relationship cues)**, only if hints 1-2 leave a measurable gap. Parse the condition columns into short hints.
6. **Decide.** Keep a hint only if flags fall and a read of the diffs shows no new damage. Record results in
   `docs/investigations/`.

## Risks

- A longer prompt can hurt the 14B model; compare against the baseline, not against intuition.
- A speaker hint can be wrong for generic NPCs (the table lists named characters only).
- The column inventory also shows an `lp[哥哥||姊姊]` token form in `NPC/Dialog.csv`; check whether the current patterns catch it
  before measuring anything on that file.

## Run safety

Use filtered tests only. `dotnet test Tests` unfiltered runs Reset All Flags, real translation, brute force and packaging and
rewrites `Files/Converted`, `Files/Mod` and `Files/TestResults`.
