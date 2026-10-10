# Initial setup: next steps

Status: active. Created when the repo was scaffolded with the `new-translation-project` skill.

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

`G:\PathOfKungfu` (an AutoTranslator config) and `G:\PathOfKungfu-AssemblyCSharp` look like earlier
work on this game; check them for reusable findings before starting from scratch.

## Steps

Work through these in order. Each ends with something verifiable.

### 1. Find out where the game's text lives
- Inspect `Assembly-CSharp.dll` (decompile, or use the old `G:\PathOfKungfu-AssemblyCSharp` output)
  and `StreamingAssets` / `Resources` for the data source: CSV/Excel tables, ScriptableObjects,
  localisation tables, or text baked into prefabs.
- Decide per source whether it is `RawCsv`, `PrefabText`, `DynamicStrings`, or needs a new approach.
- Confirm the source language (Chinese variant) so `glossaryPreset` is right.
- Done when: a short list of text sources and their formats is written into `docs/features/`.

### 2. Plugin dumper
- Implement dumping in `PathOfKungfuPlugin`, off by default behind a `Config.Bind` toggle, writing to
  `Files/Raw/Dumped`.
- Add Harmony only if patching is needed (add the `HarmonyX` package like LegendOfMortal does).
- Done when: running the game with the toggle on produces files in `Files/Raw/Dumped`.

### 3. Configure the text files
- Fill `TextFilesToSplit` in `Translate/TextFileConfiguration.cs` against the real dumped files
  (`SkipColumns`, `TextFileType`, `PackageOutput`).
- Extend `TranslationExport` / `TranslationPackaging` for any non-CSV types.
- Add `CompoundFieldSplitter` options or `splitCharactersList` in `Config.yaml` only for placeholder
  syntax actually found in the data.
- Done when: the export workflow writes `Files/Raw/Export` without errors.

### 4. Choose models and run the first translation
- Replace the `TODO-` placeholders in `Files/Config.yaml` (translator and QC models).
- Run a small first translation and review it. Use the `diagnose-translation-failure` skill for
  lines that keep failing.
- Add a game glossary and, if named characters are mistranslated or pronouns guessed, a character
  gender table (`add-character-gender-context` skill). Decide preset-vs-game glossary entries with
  `add-glossary-fix`.
- Done when: `Files/Converted` holds translated output that passes validation.

### 5. Plugin text injection
- Implement injection in the plugin so the packaged `Files/Mod` output is shown in the game.
- Confirm the folder the plugin reads from, then update the `Mods/English` placeholder in
  `Tests/FileOutputWorkflowTests.cs` (steps 6 and 7) to match. It is marked TODO.
- Handle font/layout problems if English overflows the UI; add resizer support only if needed.
- Done when: step "6. Package to Game Files" followed by launching the game shows English text.

### 6. QC pass
- Run the QC workflow, hand-triage flagged items, then set `qualityControl.minAcceptableScore` and
  populate `autoAcceptDefectCategories` from this game's own results (never copy another repo's list).
  See LlmKit `docs/features/translation-pipeline/quality-control-pass.md`.

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
- Text source for this game is unknown until step 1; the plan after step 1 may change shape.
- Game updates will change `Files/Packaging/GameVersion.txt` and may require a re-dump.
- The `XCOPY` post-build steps in the plugin csproj exited with code 2 during the scaffold build
  (warning only, cause not investigated); check the plugin DLL actually reaches `BepInEx\plugins`.
