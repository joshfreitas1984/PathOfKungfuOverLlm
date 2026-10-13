using System.IO;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PathOfKungfuPlugin;

/// <summary>BepInEx 5 (Mono) plugin: injects the packaged translation at load time, and can dump the source text (off by default).</summary>
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

        NamePools.Enabled = Config.Bind("Names", "PinyinNpcNames", true,
            "Give randomly generated NPCs pinyin names (Zhao Chen) in every game language, and convert the names of NPCs already in the save.").Value;

        StoryNpcNameRefresh.Enabled = Config.Bind("Names", "RefreshStoryNpcNames", true,
            "When a game starts, set story NPCs in the save to the current (translated) name from the Touch table. Fixes saves made before the translation was installed.").Value;

        ConvertTextEnglishBranchPatch.Enabled = Config.Bind("Translation", "EnglishTokensInChineseMode", false,
            "Experimental, untested in game. When the game language is Chinese, run NPC text tokens (a, p, lp, gangName...) on the English path so the English translation is not corrupted. Off: the mod targets English mode only.").Value;

        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        foreach (var patch in new[]
        {
            typeof(NamePoolPatch), typeof(RandomNameSpacePatch), typeof(NameXTrimPatch), typeof(NameSetterSpacePatch),
            typeof(OriginNameSpacePatch), typeof(SavedNameMigration), typeof(JobNpcNamePatch),
            typeof(AllowLatinNamePatch), typeof(NameFieldLengthPatch), typeof(RandomChildNamePatch), typeof(SameNameCheckPatch),
            typeof(ConvertTextEnglishBranchPatch), typeof(StoryNpcNameRefresh), typeof(AttributeNamePatch),
        })
            harmony.CreateClassProcessor(patch).Patch();

        if (Config.Bind("Translation", "Enabled", true,
                "Replace the game's text with the packaged translation in <plugin folder>/resources. Turn off to see the game's own text.").Value)
            Injection.Install(harmony);

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
