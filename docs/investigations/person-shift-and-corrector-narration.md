# Person shifts and corrector narration in Converted

Found 2026-10-17 from a reported line: a wife's 夫君 ("my husband") was translated "Your husband", QC `Passed` at 100.

## Findings

- **Spouse terms.** 夫君/相公 are what a wife calls her husband. A few lines come out as "your husband"; some are right (the
  husband speaking, 为夫), so only the speaker decides. `Files/Glossary/Social-and-Gangs.yaml` now pins 夫君 to "my husband".
- **你 became "I"** (and the reverse) in a small number of lines, mostly narration addressed to the player.
- **The first-pass translator sometimes narrates instead of translating.** 34 stored `translated` values are task-talk ("Ah, ... I wonder
  what there is to do around here.", "It seems there was an instruction to correct a previous output..."). QC replaced 33 of them with a
  proper `qcTranslated`, so they did not ship; one QC correction was itself narration and ships. Mostly `TaskProcess__TaskProcess.json.csv.yaml`.
  Fixed in LlmKit (`LineValidation.FindModelNarration`, part of the shared validation): see its quality-control-postmortems.md.


## Detector

`Translate/PersonShiftDetector.cs` holds four narrow, source-grounded rules (SpouseTermAsYours, FirstPersonLost,
SecondPersonLost, ModelNarration). `Tests/PersonShiftTests.cs` pins them to real lines and writes
`Files/TestResults/PersonShifts.txt` (gitignored) listing every candidate in `Files/Converted`. A hit is a candidate for review: a
line alone cannot say who is speaking.

## Open

- Retranslate or manually override the listed lines; the glossary entry only affects new translations.
- Run "2. ApplyRulesToCurrentTranslation" (mutates Converted): flags the 34 narrated `Translated` values for retranslation, then a 
  translation run (needs the local LLM) redoes them. The one shipped narrated QC correction is redone by the same pass.
