# NPCs clumped at the map corner: a Harmony transpiler loses `leave`

Found 2026-10-26. With the mod installed, NPCs gathered in one spot and barely moved, and the log filled with `FindPath error, end on Obstacles <map>:[0,0]` and
`There are no places to go around.<map>:{X=0,Y=0}`. Without the mod they dispersed.

## Cause

`NpcActionUtil.GetPointByConfStr(mapId, "x,y;x,y;...")` returned (0,0) for every valid string, without throwing. Self actions (`ActionSelf`, `ActionSelfPro`) set
their destination from it in `DoOnStart`, so each NPC that started one walked to tile (0,0), an obstacle, and `FindPath` moved it to the nearest walkable tile.

The method is shaped `try { ...; return point; } catch { ... } return default;`. When the dynamic string patcher gave it a HarmonyX transpiler, HarmonyX 2.9 rebuilt it
without the explicit `leave` that ends the try block (aimed at the real return, not at the code after the catch). The success path fell through to the default return.

The only literal patched in that method was log text, and an identity transpiler (0 differing IL lines) reproduced it, so the strings were never the problem. A probe method
with the same shape, patched in game, returned 0 with an identity transpiler and 42 with an identity `ILManipulator`.

## Fix

- `FanslationStudio.Plugins` (`StringTranspiler.Patch`): patch with an `ILManipulator`, which edits `ldstr` operands in place. The one method with another game's
  map fix keeps the transpiler (it edits locals by index). **Any game on the old plugin DLL has the bug in every translated method of this shape.**
- `GetPointByConfStr`'s log literal is a `Log` row in `Files/DynamicStringsDoNotTranslate.csv`, so that method is no longer patched at all.
- Actions already started in a save keep their saved (0,0) destination until the game ends them (`IsOverTime`, one in-game day). Waiting is safe; a one-off cleanup was
  considered and not built (stopping a half-done action skips its end text and can leave a partner NPC mid-action).

## How it was found

Opt-in diagnostics (since removed, see commit `d9f9f48`, `PathOfKungfuPlugin/NpcMovementDiagnostics.cs`) logged an NPC census every 30 seconds, `SetDestination` calls with and
without a (0,0) argument, the `end` passed to `FindPath`, and the exact string `GetPointByConfStr` received. That showed: valid string in, (0,0) out, no exception in `Player.log`.
Removing the one patched entry from the installed `dynamicStrings.txt.yaml` fixed it; the same entry with the translation set equal to the original did not.

Two traps while testing:

- The patcher loads **every** `*dynamicStrings*` file in `BepInEx/plugins/resources`, so a backup saved there is loaded too and silently undoes the edit.
- `sed -i` on the yaml turned CRLF into LF; restore line endings before putting it back.

Useful places: the BepInEx log does not contain the game's own `Debug.LogError` output, `Player.log` (`%USERPROFILE%\AppData\LocalLow\HuaLongZ\daojianjianghulu\Player.log`) does.

## Audit of the other translated dynamic strings

Every translated literal (1,844) was checked against the decompiled source of its method for key-like use (comparisons, `switch` cases, `Replace` patterns, dictionary keys,
lookups). One real issue, now a `Key` row: `UIWuXueNew.InitCurSanxue` `闪避` was a dictionary key later passed to `Enum.TryParse<ESkillTipType>`, so the dodge tooltip got the
wrong type. Cosmetic only, left alone: the chase-member tag `追杀）\n` loses its newline (both methods match), `ShowTongshi` turns "Dodge" into "Evasion", and
`UIEquipItem` `.Replace("未知","Unknown")` is a no-op. Verified fine: `InitPlayData` Neigong/Qinggong (the item column is translated with the same words),
`UI_WulinLastPanel` rank titles (`GetRankTitle` and `SetItem` translate identically), `UIShopShow` "Wen".

The scan only sees literals used directly in key-like calls. A string put in a local first and used as a key later is missed, as the earlier `GetDictionary` sheet-name misses were.
