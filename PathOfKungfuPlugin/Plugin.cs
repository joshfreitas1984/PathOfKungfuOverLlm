using System.IO;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace PathOfKungfuPlugin;

/// <summary>BepInEx 5 (Mono) plugin. Dumping is implemented (off by default); injection is still to do.</summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger = null!;

    private ConfigEntry<bool> dumpOnStart = null!;
    private ConfigEntry<string> dumpFolder = null!;
    private ConfigEntry<bool> quitAfterDump = null!;
    private volatile bool dumpFinished;

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loaded.");

        dumpOnStart = Config.Bind("Dump", "DumpOnStart", false,
            "Dump the game's Chinese source text to CSV once at startup (for translators; leave off for players).");
        dumpFolder = Config.Bind("Dump", "OutputFolder", "",
            "Folder to write the dump to. Empty = <game>/BepInEx/dump/GameData. Point it at <repo>/Files/Raw/Dumped/GameData to feed the translation pipeline.");
        quitAfterDump = Config.Bind("Dump", "QuitAfterDump", false, "Close the game when the dump has finished.");

        if (!dumpOnStart.Value) return;

        var output = string.IsNullOrWhiteSpace(dumpFolder.Value)
            ? Path.Combine(Paths.BepInExRootPath, "dump", "GameData")
            : dumpFolder.Value;
        var streamingAssets = Application.streamingAssetsPath;
        var version = Application.version;

        // Off the main thread: reading ~160 workbooks would freeze the game window.
        new Thread(() =>
        {
            try { Dumper.Run(streamingAssets, output, version); }
            catch (System.Exception ex) { Logger.LogError($"Dump failed: {ex}"); }
            finally { dumpFinished = true; }
        }) { IsBackground = true, Name = "PathOfKungfu dump" }.Start();
    }

    private void Update()
    {
        if (dumpFinished && quitAfterDump.Value)
        {
            dumpFinished = false;
            Application.Quit();
        }
    }
}
