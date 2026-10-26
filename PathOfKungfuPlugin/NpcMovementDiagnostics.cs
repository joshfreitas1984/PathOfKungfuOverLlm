using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PathOfKungfuPlugin;

/// <summary>
/// Diagnostic, off by default ([Diagnostics] NpcMovement): finds out why NPCs stay clumped. Every <see cref="IntervalSeconds"/> it logs a census
/// of the NPCs in the save (on an action, moving, idle on an action, stuck on the same tile since the last census, destination (0,0)) plus the
/// counters below since the last line, and the first few stuck NPCs in full. Failed SetDestination calls and exceptions thrown in the NPC action
/// code (which the game would otherwise swallow into a stopped NPC) are counted, and the first few of each are logged with the NPC's state.
/// </summary>
internal static class NpcMovementDiagnostics
{
    internal static float IntervalSeconds = 30;

    /// <summary>"Type.Method" names to write the original and current (patched) IL of to BepInEx/ilDump, once, on the first tick.</summary>
    internal static string[] DumpIl = new string[0];
    private static bool ilDumped;

    private const int DetailLimit = 20;
    private const int StuckSamples = 5;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, int> Counts = new();
    private static readonly Dictionary<string, int> Logged = new();
    private static readonly Dictionary<int, string> LastPoints = new();
    private static float nextCensus;

    internal static void Count(string key)
    {
        lock (Gate) Counts[key] = Counts.TryGetValue(key, out var n) ? n + 1 : 1;
    }

    /// <summary>True for the first <paramref name="limit"/> times a key is seen, so a flood of the same failure logs a few lines.</summary>
    internal static bool ShouldLog(string key, int limit)
    {
        lock (Gate)
        {
            var seen = Logged.TryGetValue(key, out var n) ? n : 0;
            Logged[key] = seen + 1;
            return seen < limit;
        }
    }

    internal static string Describe(NPCData npc)
    {
        var action = npc.currAction;
        var actionText = action == null
            ? "no action"
            : $"{action.ActionType}#{action.ActionId} started={action.IsStart} arrived={action.IsArrived} dest={action.DestMapId}:{action.Destination}";
        return $"{npc.id} \"{npc.name}\" home={npc.birthplace} at={npc.point} dest={npc.destMapId}:{npc.destination} " +
               $"path={(npc.Path == null ? "null" : npc.Path.Count.ToString())} paused={npc.IsMotionPaused} follow={npc.followTargetId} [{actionText}]";
    }

    internal static void LogFailure(string key, NPCData npc)
    {
        Count(key);
        if (ShouldLog(key, DetailLimit)) Plugin.Logger.LogWarning($"[NpcDiag] {key}: {Describe(npc)}");
    }

    /// <summary>Called from the plugin's Update; does nothing until the interval has passed.</summary>
    internal static void Tick()
    {
        if (Time.realtimeSinceStartup < nextCensus) return;
        nextCensus = Time.realtimeSinceStartup + IntervalSeconds;
        if (!ilDumped)
        {
            ilDumped = true;
            LeaveProbe.Run();
            foreach (var name in DumpIl) DumpMethodIl(name.Trim());
        }

        try { Census(); }
        catch (Exception ex) { Plugin.Logger.LogError($"[NpcDiag] census failed: {ex}"); }
    }

    /// <summary>
    /// Writes the method's IL as the game shipped it and as it runs now (after every transpiler, including the dynamic string patcher's), side by
    /// side with a marker on each differing line, to BepInEx/ilDump/Type.Method.txt.
    /// </summary>
    private static void DumpMethodIl(string typeAndMethod)
    {
        try
        {
            if (typeAndMethod.Length == 0) return;
            var dot = typeAndMethod.LastIndexOf('.');
            var type = AccessTools.TypeByName(typeAndMethod.Substring(0, dot));
            var method = type == null ? null : AccessTools.Method(type, typeAndMethod.Substring(dot + 1));
            if (method == null) { Plugin.Logger.LogWarning($"[NpcDiag] DumpIl: cannot find {typeAndMethod}"); return; }

            var original = PatchProcessor.GetOriginalInstructions(method).Select(i => i.ToString()).ToList();
            var current = PatchProcessor.GetCurrentInstructions(method).Select(i => i.ToString()).ToList();
            var info = Harmony.GetPatchInfo(method);
            var lines = new List<string>
            {
                $"{typeAndMethod}: {original.Count} original instructions, {current.Count} current",
                $"prefixes={info?.Prefixes.Count ?? 0} postfixes={info?.Postfixes.Count ?? 0} transpilers={info?.Transpilers.Count ?? 0} finalizers={info?.Finalizers.Count ?? 0}",
                "",
            };
            for (var i = 0; i < Math.Max(original.Count, current.Count); i++)
            {
                var a = i < original.Count ? original[i] : "";
                var b = i < current.Count ? current[i] : "";
                lines.Add($"{(a == b ? "  " : "!!")} {i,4}  {a}" + (a == b ? "" : $"    ==>    {b}"));
            }

            var folder = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "ilDump");
            System.IO.Directory.CreateDirectory(folder);
            var path = System.IO.Path.Combine(folder, typeAndMethod + ".txt");
            System.IO.File.WriteAllLines(path, lines);
            Plugin.Logger.LogInfo($"[NpcDiag] DumpIl: wrote {path} ({lines.Count(l => l.StartsWith("!!"))} differing lines)");
        }
        catch (Exception ex) { Plugin.Logger.LogError($"[NpcDiag] DumpIl {typeAndMethod} failed: {ex}"); }
    }

    private static void Census()
    {
        var npcs = Singleton<GDataManager>.Instance?.playerData?.m_nPCDataDic;
        if (npcs == null)
        {
            Plugin.Logger.LogInfo("[NpcDiag] census: no NPC data in the save yet");
            return;
        }

        int total = 0, onAction = 0, moving = 0, idleOnAction = 0, destZero = 0, stuck = 0;
        var byType = new Dictionary<string, int>();
        var samples = new List<string>();
        var points = new Dictionary<int, string>();

        foreach (var npc in npcs.Values.ToList())
        {
            total++;
            var point = $"{npc.birthplace}:{npc.point}";
            points[npc.id] = point;

            var action = npc.currAction;
            if (action == null) continue;

            onAction++;
            var type = action.ActionType.ToString();
            byType[type] = byType.TryGetValue(type, out var n) ? n + 1 : 1;
            if (action.Destination.ToString() == "{X=0,Y=0}") destZero++;

            if (npc.Path != null && npc.Path.Count > 0) { moving++; continue; }

            idleOnAction++;
            if (LastPoints.TryGetValue(npc.id, out var last) && last == point)
            {
                stuck++;
                if (samples.Count < StuckSamples) samples.Add(Describe(npc));
            }
        }

        LastPoints.Clear();
        foreach (var pair in points) LastPoints[pair.Key] = pair.Value;

        string counters;
        lock (Gate)
        {
            counters = Counts.Count == 0 ? "none" : string.Join(", ", Counts.OrderByDescending(c => c.Value).Select(c => $"{c.Key}={c.Value}"));
            Counts.Clear();
        }

        Plugin.Logger.LogInfo($"[NpcDiag] npcs={total} onAction={onAction} moving={moving} idleOnAction={idleOnAction} stuckSinceLastCensus={stuck} " +
                              $"actionDestIsZero={destZero} types=[{string.Join(", ", byType.Select(t => $"{t.Key}={t.Value}"))}] since last: {counters}");
        foreach (var sample in samples) Plugin.Logger.LogInfo($"[NpcDiag] stuck: {sample}");
    }
}

/// <summary>A SetDestination that returns false (no path) or aims at (0,0) leaves the NPC where it is.</summary>
[HarmonyPatch(typeof(NPCData), nameof(NPCData.SetDestination))]
internal static class NpcSetDestinationDiagnostic
{
    /// <summary>The argument as the caller passed it, before SetDestination swaps in a transfer point or anything else changes it.</summary>
    private static void Prefix(NPCData __instance, int mapId, System.Drawing.Point destination)
    {
        try
        {
            if (destination.ToString() == "{X=0,Y=0}") NpcMovementDiagnostics.LogFailure($"SetDestination CALLED with (0,0) (map {mapId})", __instance);
        }
        catch (Exception ex) { Plugin.Logger.LogError($"[NpcDiag] SetDestination prefix diagnostic failed: {ex}"); }
    }

    private static void Postfix(NPCData __instance, bool __result, int mapId)
    {
        try
        {
            NpcMovementDiagnostics.Count("SetDestination");
            if (!__result) NpcMovementDiagnostics.LogFailure($"SetDestination returned false (map {mapId})", __instance);
            if (__instance.destination.ToString() == "{X=0,Y=0}") NpcMovementDiagnostics.LogFailure($"SetDestination to (0,0) (map {mapId})", __instance);
        }
        catch (Exception ex) { Plugin.Logger.LogError($"[NpcDiag] SetDestination diagnostic failed: {ex}"); }
    }
}

/// <summary>
/// The point the pathfinder is actually asked to reach. When this is (0,0) but "SetDestination CALLED with (0,0)" never fired for the NPC, the
/// zero came from inside SetDestination (the transfer point to another map), not from the action.
/// </summary>
[HarmonyPatch(typeof(MapManager), nameof(MapManager.FindPath))]
internal static class FindPathDiagnostic
{
    private static void Prefix(int mapId, System.Drawing.Point start, System.Drawing.Point end, NPCData data)
    {
        try
        {
            NpcMovementDiagnostics.Count("FindPath");
            if (end.ToString() != "{X=0,Y=0}") return;
            var key = $"FindPath end is (0,0) (map {mapId}, start {start})";
            NpcMovementDiagnostics.Count("FindPath end (0,0)");
            if (NpcMovementDiagnostics.ShouldLog(key, 10) && data != null)
                Plugin.Logger.LogWarning($"[NpcDiag] {key}: {NpcMovementDiagnostics.Describe(data)}");
        }
        catch (Exception ex) { Plugin.Logger.LogError($"[NpcDiag] FindPath diagnostic failed: {ex}"); }
    }
}

/// <summary>
/// Actions that start with destination (0,0) get it from here: it returns (0,0) for an empty string or when parsing throws. Logs the map and
/// the exact string the game passed in when it is empty or does not parse as "x,y;x,y".
/// </summary>
[HarmonyPatch(typeof(NpcActionUtil), nameof(NpcActionUtil.GetPointByConfStr))]
internal static class NpcPointConfDiagnostic
{
    private static void Prefix(int mapId, string str)
    {
        try
        {
            NpcMovementDiagnostics.Count("GetPointByConfStr");
            string? problem = null;
            if (string.IsNullOrEmpty(str)) problem = "empty";
            else
                foreach (var part in str.Split(';'))
                {
                    var xy = part.Split(',');
                    if (xy.Length != 2 || !int.TryParse(xy[0], out _) || !int.TryParse(xy[1], out _)) { problem = $"unparseable part \"{part}\""; break; }
                }

            if (problem == null) return;
            var key = $"GetPointByConfStr {problem} (map {mapId})";
            NpcMovementDiagnostics.Count(key);
            if (NpcMovementDiagnostics.ShouldLog(key, 10))
                Plugin.Logger.LogWarning($"[NpcDiag] {key}: str={(str == null ? "null" : $"\"{(str.Length > 120 ? str.Substring(0, 120) + "..." : str)}\"")}");
        }
        catch (Exception ex) { Plugin.Logger.LogError($"[NpcDiag] GetPointByConfStr diagnostic failed: {ex}"); }
    }

    /// <summary>A string that parses but still gives (0,0) means the method threw inside its try block, or something rewrote it.</summary>
    private static void Postfix(int mapId, string str, System.Drawing.Point __result)
    {
        try
        {
            if (string.IsNullOrEmpty(str) || __result.ToString() != "{X=0,Y=0}") return;
            var key = $"GetPointByConfStr returned (0,0) for a non-empty string (map {mapId})";
            NpcMovementDiagnostics.Count(key);
            if (NpcMovementDiagnostics.ShouldLog(key, 10))
                Plugin.Logger.LogWarning($"[NpcDiag] {key}: str=\"{(str.Length > 120 ? str.Substring(0, 120) + "..." : str)}\"");
        }
        catch (Exception ex) { Plugin.Logger.LogError($"[NpcDiag] GetPointByConfStr result diagnostic failed: {ex}"); }
    }
}

/// <summary>
/// Logs exceptions thrown through the NPC action and movement methods and rethrows them unchanged. The game catches them further up and the NPC
/// just stops, so without this a failing action (a bad format string, a null lookup) is invisible.
/// </summary>
[HarmonyPatch]
internal static class NpcExceptionDiagnostic
{
    private static readonly HashSet<string> ActionMethods = new()
    {
        "Evolut", "Start", "Init", "DoOnStart", "DoOnArrive", "DoOnEvolut", "DoOnFinish", "CheckNextAction",
    };

    private static readonly HashSet<string> NpcMethods = new()
    {
        "Evolut", "EvolutMotion", "OnStartMove", "OnReachTile", "OnPathEnd", "OnArrive", "CheckFollowTarget", "SetDestination", "SetFollowTarget", "StopAction",
    };

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in typeof(NpcAction).Assembly.GetTypes().Where(t => typeof(NpcAction).IsAssignableFrom(t)))
            foreach (var method in AccessTools.GetDeclaredMethods(type))
                if (ActionMethods.Contains(method.Name) && !method.IsAbstract && method.GetMethodBody() != null)
                    yield return method;

        foreach (var method in AccessTools.GetDeclaredMethods(typeof(NPCData)))
            if (NpcMethods.Contains(method.Name) && method.GetMethodBody() != null)
                yield return method;
    }

    private static Exception? Finalizer(Exception? __exception, MethodBase __originalMethod)
    {
        NpcMovementDiagnostics.Tick(); // also driven from here: the plugin's own Update is not guaranteed to run
        if (__exception == null) return null;
        try
        {
            var key = $"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}: {__exception.GetType().Name}: {__exception.Message}";
            NpcMovementDiagnostics.Count($"EXCEPTION {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}");
            if (NpcMovementDiagnostics.ShouldLog(key, 3)) Plugin.Logger.LogError($"[NpcDiag] {key}\n{__exception.StackTrace}");
        }
        catch { /* never let the diagnostic replace the game's exception */ }

        return __exception;
    }
}

/// <summary>
/// Probe for the suspected Harmony bug: a method shaped like NpcActionUtil.GetPointByConfStr (try { ...; return x; } catch { ... } return default;)
/// patched with an identity transpiler. Expect Probe("42") == 42; a result of 0 means the transpiler round trip loses the explicit leave at the end
/// of the try block, so the success path falls through to the default return. A second copy is patched with an ILManipulator instead.
/// </summary>
internal static class LeaveProbe
{
    internal static int ViaTranspiler(string s)
    {
        try
        {
            if (string.IsNullOrEmpty(s)) return -1;
            var value = int.Parse(s);
            if (value < 0) UnityEngine.Debug.Log("negative");
            return value;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.Log("probe failed " + ex.Message);
        }

        return 0;
    }

    internal static int ViaManipulator(string s)
    {
        try
        {
            if (string.IsNullOrEmpty(s)) return -1;
            var value = int.Parse(s);
            if (value < 0) UnityEngine.Debug.Log("negative");
            return value;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.Log("probe failed " + ex.Message);
        }

        return 0;
    }

    private static IEnumerable<HarmonyLib.CodeInstruction> Identity(IEnumerable<HarmonyLib.CodeInstruction> instructions) => instructions;

    private static void Noop(MonoMod.Cil.ILContext context) { }

    internal static void Run()
    {
        try
        {
            var before = (ViaTranspiler("42"), ViaManipulator("42"));
            var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID + ".leaveprobe");
            harmony.Patch(AccessTools.Method(typeof(LeaveProbe), nameof(ViaTranspiler)), transpiler: new HarmonyMethod(typeof(LeaveProbe), nameof(Identity)));
            harmony.Patch(AccessTools.Method(typeof(LeaveProbe), nameof(ViaManipulator)), ilmanipulator: new HarmonyMethod(typeof(LeaveProbe), nameof(Noop)));
            var after = (ViaTranspiler("42"), ViaManipulator("42"));
            Plugin.Logger.LogInfo($"[NpcDiag] LeaveProbe (expect 42): unpatched transpiler-copy={before.Item1} manipulator-copy={before.Item2}; " +
                                  $"identity transpiler={after.Item1} identity ILManipulator={after.Item2}");
        }
        catch (Exception ex) { Plugin.Logger.LogError($"[NpcDiag] LeaveProbe failed: {ex}"); }
    }
}
