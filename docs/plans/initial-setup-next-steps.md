# Initial setup: next steps

Status: active. Steps 1-3 are done, step 4 (first full translation run) is in progress, and step 5 (plugin injection) is written and checked offline, awaiting an in-game check. Created when the repo was scaffolded with the `new-translation-project` skill.

The scaffold builds, the plugin loads and does nothing, and nothing has been dumped, translated or
released yet. This plan covers the game-specific work the scaffold deliberately left out.

## Facts established at scaffold time

| Item | Value |
| --- | --- |
| Game / Steam app ID | Path of Kung Fu / 2361680 |
| Install folder | `G:\SteamLibrary\steamapps\common\The road of Jianghu` (exe `daojianjianghulu.exe`, data `daojianjianghulu_Data`) |
| Runtime | Mono, Unity 2021.3.16, 64-bit exe |
| BepInEx | 5.4.23.3 x64, pinned with SHA256 in `Installer/installer.json` |
| MonoMod in `Managed` | None, so no `dllSearchPathOverride` |
| Game version | 1.2.0.1 (`Files/Packaging/GameVersion.txt`) |
| Plugin | `PathOfKungfuPlugin`, assembly `FanslationStudio.PathOfKungfu.Plugin`, BepInEx 5 `BaseUnityPlugin` |
| Game assemblies of interest | `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`, `MiniExcel.dll` (hint: data may be spreadsheet-driven), `Unity.TextMeshPro.dll`, `UnityEngine.UI.dll` |

## Progress (updated after the first full run was started)

| Step | State | Where it is written up |
| --- | --- | --- |
| 1. Find the text | Done. Excel-driven (`MiniExcel`), Simplified Chinese source, the game ships its own poor English that is replaced, not used | [`features/game-text-sources.md`](../features/game-text-sources.md) |
| 2. Plugin dumper | Done, run once in game: 342 CSVs under `Files/Raw/Dumped/GameData` (flat `Workbook__Sheet.csv` names) plus flattened dialogue JSON (`*.json.csv`, 67k strings). `BepInEx 5.4.23.3` installed in the game folder; the plugin reads translations from `BepInEx\plugins\resources` (not the game's `Mod/` folder) | [`features/game-text-sources.md`](../features/game-text-sources.md), [`features/dump-column-inventory.md`](../features/dump-column-inventory.md) |
| 3. Configure the text files | Done: 50 files in `Translate/TextFileConfiguration.cs`, columns chosen from what the developers translated in the shipped English plus hand additions; export verified | [`features/text-files-to-split.md`](../features/text-files-to-split.md) |
| Glossary, genders, names (added to the plan) | Done: 24 glossary files, `CharacterGenders.yaml` (1,324 names), `ManualTranslations.yaml` (about 3,800 exact entries: 1,322 skill names, about 1,260 item names, 1,206 speaker/NPC names), 91 gender-alternative tokens | [`features/glossary-seed.md`](../features/glossary-seed.md) |
| 4. Models and first translation | **Run in progress** (started by the project owner): `qwen2.5:14b-instruct` translates, `Qwen3.8-27B IQ4_XS` is the QC model; QC settings copied from DragonHierOverLlm | this file, `Files/Config.yaml` |
| 5. Plugin text injection | **Written, checked offline, and loads in game (every sheet matched in the log); on-screen text still to check.** `GDataManager`'s `ExcelUtil` calls are redirected (transpiler) to inject the sheets, plus `Data_Word`, `Data_Speak`, `LanguageConfig`; dialogue JSON by path; works for both the Chinese and the English workbooks. Packaging now writes `translated-columns.json` and restores sheet header rows | [`features/text-injection.md`](../features/text-injection.md) |

Findings that changed the plan:
- The text-source step turned out to need a **dialogue flattener** (JSON inside spreadsheet cells) and a **flat file layout**, both done.
- A dialogue sample before the corpus run found **runtime tokens** (`<fName>`, gender alternatives `<A||B>`) splitting sentences and **inconsistent speaker names**; both fixed (see glossary-seed.md).
- Martial art, move, item and NPC names were **reviewed by hand** instead of being translated by the 14B model, which mistranslated idiom and poetry names.

## Next steps

### 4b. After the run finishes
- Run the same translate step once more: lines that failed validation (about 0.3% in the sample) are retried.
- Check the result: lines left untranslated or flagged, `Files/TestResults/UnprocessableItems.log` if present, lines whose `<fName>` / `<A||B>` tokens or colour tags changed, glossary and manual-translation names appearing as written.
- Spot-read the dialogue for pronouns (the player's gender is unknown), the `Player-Gender-Tokens` renderings, and the names in `Character-Names.yaml`.
- Decide whether `pronounCheck` should be enabled in `Config.yaml` (it is not set yet).

### 5. Plugin text injection
- Implement injection in the plugin: Harmony postfix on `ExcelUtil.GetListOject<T>` / `GetDictionary<TKey,T>` plus the `Data_Word`, `Data_Skill`/`Data_Speak` and `LanguageConfig` loaders, addressing cells by the table's **ID column** (not the dumper's `Row` number, which drifts on sheets with blank rows) and following JSON paths for dialogue.
- Done: the plugin reads the packaged output from `BepInEx\plugins\resources`, and steps 6 and 7 in `Tests/FileOutputWorkflowTests.cs` now use `BepInEx/plugins/resources` (the `Mods/English` placeholder is gone).
- Done: the patch applies whether the player's Steam language is English or Chinese (the language marker on `_en` workbooks and `英文` sheets is stripped; see `features/text-injection.md`).
- Not yet covered by the dump: UI/prefab text in asset bundles and hard-coded code strings. Log Chinese strings seen at runtime and decide then.
- Not yet translated tables (see `features/text-files-to-split.md`): `monster`, the shop sheets, drop tables, name pools (`姓名库`), several small sheets.
- Handle font/layout problems if English overflows the UI (resizer support only if needed).
- Remaining: confirm English text appears on screen; look at the screen; then a runtime pass for what is still Chinese. Done when step "6. Package to Game Files" followed by launching the game shows English text.

### 6. QC pass
- Run the QC workflow, hand-triage flagged items, then set `qualityControl.minAcceptableScore` and `autoAcceptDefectCategories` from this game's own results. The current values were copied from DragonHierOverLlm at the owner's request; verify them. See LlmKit `docs/features/translation-pipeline/quality-control-pass.md`.

### 7. Release plumbing
- Replace the placeholder `Files/Packaging/BepInEx.cfg` with the tailored one from a working local
  install (launch the game once with BepInEx first).
- Make sure `FanslationStudio.Plugins.dll` ends up in the release staging folder, which
  "7. Package Release" requires (`PrepareStagingFolder` throws if it is missing). The sibling
  `FanslationStudio.Plugins` repo has a `config.yaml` listing games for its post-build copy; add this
  game there (BepInEx 5). Check how LegendOfMortal does it first.
- Create the GitHub repo if it does not exist, then create the `installer` pre-release by hand
  (tick pre-release, tag `installer`).
- Run "7b. Package Installer" once, and "7. Package Release" when there is something to ship.
  Never run either as a smoke test on a dev machine: they rewrite the real `ReleaseFolder` and open a
  browser.

### 8. Housekeeping in other repos
- Add `PathOfKungfuOverLlm` to `DownstreamRepos` in `Tests/SkillSyncTests.cs` in
  `FanslationStudio.LlmKit`.
- When the work above is settled, record reusable findings in `docs/features/` and update this
  plan's status. Do not update docs after every intermediate edit.

## Risks / open questions
- Game updates change `Files/Packaging/GameVersion.txt` (currently 1.2.0.1, from `LanguageConfig`; `Application.version` reports 1.0.1.3) and need a re-dump.
- The plugin post-build copy now uses MSBuild `Copy` (the old `XCOPY` exited with code 2 and copied nothing); the DLL reaches
  `BepInEx\plugins`. `FanslationStudio.Plugins.dll` is deployed by building the sibling `FanslationStudio.Plugins.BepInEx5`
  project (the game is in that repo's `config.yaml`, uncommitted).
- Nothing in this repo or the sibling repos (`FanslationStudio.Plugins`, `FanslationStudio.LlmKit` skills) is committed yet.
