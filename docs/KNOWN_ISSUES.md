# Known issues

Index only — one line per known issue or postmortem, linking to the full write-up under
`docs/investigations/`. 
- [Person shifts and corrector narration in Converted](investigations/person-shift-and-corrector-narration.md) — wife/husband and I/you flips; model narration in translations.
- [NPCs clumped at the map corner](investigations/npc-clump-harmony-leave.md) — a Harmony transpiler lost a `leave`, so `GetPointByConfStr` returned (0,0); fixed in `FanslationStudio.Plugins`.
