# NPC movement diagnostics

[`NpcMovementDiagnostics.cs`](../../PathOfKungfuPlugin/NpcMovementDiagnostics.cs), off by default. Added to find out why NPCs stay clumped
with the mod installed (they disperse without it) and why `MapManager.FindPath` logs `end on Obstacles ...:[0,0]`.

Turn on in the BepInEx config: `[Diagnostics] NpcMovement = true` (`NpcMovementIntervalSeconds`, default 30). Look for `[NpcDiag]` in the BepInEx log.

| Output | Meaning |
| --- | --- |
| `npcs= onAction= moving= idleOnAction= stuckSinceLastCensus= actionDestIsZero=` | Census of `playerData.m_nPCDataDic` every interval. `stuck` = on an action, no path, same tile as the previous census |
| `since last: SetDestination=… SetDestination returned false…` | Counters since the previous line. A false return means no path; `(0,0)` is the empty-destination case |
| `stuck: <id> "<name>" home= at= dest= path= [action]` | The first few stuck NPCs in full |
| `EXCEPTION Type.Method` + stack | Exceptions thrown through `NpcAction*` / `NPCData` movement methods. The game swallows them and the NPC just stops. Rethrown unchanged |

`[Diagnostics] DumpIl = Type.Method,...` writes a method's original and current (patched) IL to `BepInEx/ilDump/` on the first tick (`!!` marks changed lines). With diagnostics on, the first tick also logs `LeaveProbe`: two copies of a `try { return x; } catch { } return default;` method, one patched with an identity transpiler and one with an identity `ILManipulator` (expect 42 for the manipulator; 0 for the transpiler is the Harmony bug in [dynamic-strings.md](dynamic-strings.md)).

Compare a run with the mod to a run without it (the exception hooks and census work with the translation off too: `[Translation] Enabled = false`).
