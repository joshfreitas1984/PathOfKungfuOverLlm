# PathOfKungfuOverLlm — Documentation Hub

# Latest release
## Install with the installer (recommended)

1. Download the installer: [Windows](https://github.com/joshfreitas1984/PathOfKungfuOverLlm/releases/download/installer/Installer-win-x64.exe) or [Linux](https://github.com/joshfreitas1984/PathOfKungfuOverLlm/releases/download/installer/Installer-linux-x64). These links always give you the newest installer. All releases are listed on the [releases page](https://github.com/joshfreitas1984/PathOfKungfuOverLlm/releases).
2. Run it. It finds the game through Steam (use **Browse** if it can't), installs BepInEx, and installs the [latest patch release](https://github.com/joshfreitas1984/PathOfKungfuOverLlm/releases/latest). Press **Install / Update**.
3. Start the game once and let it reach the main menu, so BepInEx can generate its files.

Windows may warn "Windows protected your PC" because the installer isn't code-signed. Choose **More info**, then **Run anyway**.

**Linux (Steam/Proton):** the game and BepInEx are the Windows builds. Paste this into the game's Steam **Properties > Launch Options** (the installer shows it with a Copy button):

```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

To remove the patch, run the installer and press **Uninstall patch**. It removes the patch files and leaves BepInEx in place.

## Updates

When the game starts it checks for a newer patch. If there is one, an **Update available** window appears about 15 seconds later. **Update now** downloads it, closes the game, applies the update and restarts the game through Steam (on Linux you may need to start the game yourself afterwards). **Later** hides it until the next start. If a check or download fails, nothing changes and the reason is written to the BepInEx log.

Your settings files under `BepInEx/config` are never overwritten by an update. To turn the check off, set `Enabled = false` under `[Updates]` in `BepInEx/config/FanslationStudio.Plugins.UIEditor.cfg`. Updates only work for a patch installed with the installer.

## Manual install

Install [BepInEx 5.4.23.3 for Windows **x64** (64-bit)](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.3) (`BepInEx_win_x64_5.4.23.3.zip`) into your `<Game Folder>` where daojianjianghulu.exe is, then extract the [latest release zip](https://github.com/joshfreitas1984/PathOfKungfuOverLlm/releases/latest) over it. Release zips no longer contain BepInEx itself. A manual install won't get the in-game update prompt.

# Contacting us
You can join us here: [Discord](https://discord.gg/sqXd5ceBWT)

This is the canonical navigation entry point for the repository. It exists so that a human or an
AI agent (Copilot, Claude Code, or otherwise) can find the right source of truth without relying on
vendor-specific memory.

## Repository overview

PathOfKungfuOverLlm builds an English fan-translation patch for the Unity game *Path of Kung Fu*
(Steam app 2361680, Mono, Unity 2021.3, x64), plus the tooling used to translate the game's dumped
text via `FanslationStudio.LlmKit`. The sub-projects are independent and documented separately:

| Project | Purpose |
| --- | --- |
| [`Translate/`](../Translate/) | Extracts game text data, drives the LLM translation workflow (built on `FanslationStudio.LlmKit` — a **sibling repo** at `../FanslationStudio.LlmKit`, referenced via project reference, not a NuGet package), and repackages translated data for the plugin. |
| [`PathOfKungfuPlugin/`](../PathOfKungfuPlugin/) | BepInEx 5 Mono runtime plugin that dumps game text and injects translated text into the running game. |
| [`Tests/`](../Tests/) | xUnit regression tests and the numbered manual pipeline/release steps. |
| [`Files/`](../Files/) | Working-directory data: raw dumped game text, glossary, manual translations, converted/translated output, packaging inputs, and the mod output consumed by the plugin. |
| [`Installer/`](../Installer/) | Thin host for LlmKit's installer/auto-updater; configured by [`installer.json`](../Installer/installer.json). |

## Documentation taxonomy

- **`.github/copilot-instructions.md`** is the concise Copilot-format mirror of `AGENTS.md`.
- **`docs/KNOWN_ISSUES.md`** is an index only; full investigations live under `docs/investigations/`.
- **`docs/features/`** contains current behavior and project references.
- **`docs/investigations/`** contains incidents, bug investigations, and postmortems.
- **`docs/plans/`** contains active implementation plans.
- **`docs/architecture/`** contains durable structural references and decisions.
- **`AGENTS.md`** (repo root) — short, auto-loaded, current-state rules. `CLAUDE.md` is a thin
  pointer to it.

Shared-library mechanics belong in the sibling `FanslationStudio.LlmKit/docs/` tree; link to them
instead of duplicating their implementation details here.

## Where should I look?

| Task | Start here |
| --- | --- |
| See what is left to do after scaffolding | [`plans/initial-setup-next-steps.md`](plans/initial-setup-next-steps.md) |
| Plan the extra context for token lines | [`plans/token-context-hints.md`](plans/token-context-hints.md) |
| Work on the extraction → LLM translation → repackaging pipeline | [`Translate/TextFileConfiguration.cs`](../Translate/TextFileConfiguration.cs), [`Translate/TranslationExport.cs`](../Translate/TranslationExport.cs), [`Translate/TranslationPackaging.cs`](../Translate/TranslationPackaging.cs) |
| Understand the `Files/` raw/converted/mod data layout | [`Files/`](../Files/) and the [`Translate/`](../Translate/) source above |
| Change model, QC or batching settings | [`Files/Config.yaml`](../Files/Config.yaml) |
| Write the runtime dump/injection patches | [`PathOfKungfuPlugin/Plugin.cs`](../PathOfKungfuPlugin/Plugin.cs) |
| Understand or change how translated text is injected into the game | [`features/text-injection.md`](features/text-injection.md), [`PathOfKungfuPlugin/Injection.cs`](../PathOfKungfuPlugin/Injection.cs) |
| Read or search the game's code (how a string is shown, what a table column is used for) | `Files/Raw/Decompiled/Assembly-CSharp/` (gitignored; made by test `0. Decompile Game Assemblies` in [`Tests/DecompileTests.cs`](../Tests/DecompileTests.cs)). Grep it rather than re-decompiling |
| Package or publish a release | [`Tests/FileOutputWorkflowTests.cs`](../Tests/FileOutputWorkflowTests.cs), [`Installer/installer.json`](../Installer/installer.json), [`Files/Packaging/`](../Files/Packaging/) |
| Investigate a known issue | [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) |
| Work on the QC (quality control) pass | [`quality-control-pass.md`](../../FanslationStudio.LlmKit/docs/features/translation-pipeline/quality-control-pass.md) (**sibling repo** — the actual `QualityControlWorkflow` implementation lives there, not in this repo) |
| Investigate a packaging issue | [`packaging-workflows.md`](../../FanslationStudio.LlmKit/docs/features/packaging/packaging-workflows.md) (**sibling repo**) |
| Reconcile this repo's structure with the canonical layout | [`downstream-project-structure.md`](../../FanslationStudio.LlmKit/docs/architecture/downstream-project-structure/downstream-project-structure.md) (**sibling repo**) |
