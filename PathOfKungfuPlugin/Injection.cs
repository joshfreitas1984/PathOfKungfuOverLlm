using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace PathOfKungfuPlugin;

/// <summary>
/// Installs the translation at load time: every sheet the game reads through <c>ExcelUtil.GetListOject&lt;T&gt;</c> /
/// <c>GetDictionary&lt;TKey,T&gt;</c> is passed to <see cref="SheetInjector"/> right after it is read, and the text files the game reads
/// directly (Data_Word, Data_Speak, LanguageConfig) are patched after they load. Nothing on disk is modified, and a failure only leaves
/// the game's own text.
/// <para>
/// ExcelUtil's methods are generic over reference types, and Mono shares one body for all of them: patching it with Harmony makes every
/// call run with the first patched type (a black screen: DebugConfig rows were read as another class). So ExcelUtil is left alone, and
/// the calls to it inside GDataManager are rewritten (transpiler) to call <see cref="GetListOject{T}"/> / <see cref="GetDictionary{TKey,T}"/>
/// here, which call ExcelUtil and then inject. Public because the rewritten game code calls them.
/// </para>
/// </summary>
public static class Injection
{
    internal static ITranslationSource? Source;

    internal static string ResourceFolder() =>
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".", "resources");

    internal static void Install(Harmony harmony)
    {
        var folder = ResourceFolder();
        if (!Directory.Exists(folder))
        {
            Plugin.Logger.LogWarning($"No translation folder at {folder}: the game keeps its own text.");
            return;
        }

        Source = new FolderTranslationSource(folder);

        var patched = 0;
        var transpiler = new HarmonyMethod(typeof(Injection), nameof(RedirectExcelUtil));
        foreach (var type in new[] { typeof(GDataManager) }.SelectManyNested())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                try
                {
                    if (method.GetMethodBody() == null || !CallsExcelUtil(method)) continue;
                    harmony.Patch(method, transpiler: transpiler);
                    patched++;
                }
                catch (Exception ex) { Plugin.Logger.LogError($"Cannot patch {type.Name}.{method.Name}: {ex.Message}"); }
            }
        }

        foreach (var patch in new[] { typeof(DataWordPatch), typeof(DataSpeakPatch), typeof(LanguageConfigPatch) })
            harmony.CreateClassProcessor(patch).Patch();

        Plugin.Logger.LogInfo($"Translation injection on: {patched} GDataManager methods redirected, reading {folder}.");
    }

    private static IEnumerable<Type> SelectManyNested(this IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectManyNested())
                yield return nested; // async methods live in compiler-generated state machine classes
        }
    }

    private static bool CallsExcelUtil(MethodBase method)
    {
        foreach (var instruction in PatchProcessor.ReadMethodBody(method))
            if (instruction.Value is MethodInfo called && called.DeclaringType == typeof(ExcelUtil) && called.IsGenericMethod) return true;
        return false;
    }

    private static readonly MethodInfo ListMethod = typeof(Injection).GetMethod(nameof(GetListOject))!;
    private static readonly MethodInfo DictionaryMethod = typeof(Injection).GetMethod(nameof(GetDictionary))!;

    /// <summary>Points the generic ExcelUtil calls at this class's twins, with the same type arguments.</summary>
    private static IEnumerable<CodeInstruction> RedirectExcelUtil(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo { IsGenericMethod: true } called && called.DeclaringType == typeof(ExcelUtil))
            {
                var parameters = called.GetParameters().Length;
                if (called.Name == nameof(ExcelUtil.GetListOject) && parameters == 3)
                    instruction.operand = ListMethod.MakeGenericMethod(called.GetGenericArguments());
                else if (called.Name == nameof(ExcelUtil.GetDictionary) && parameters == 5)
                    instruction.operand = DictionaryMethod.MakeGenericMethod(called.GetGenericArguments());
            }
            yield return instruction;
        }
    }

    public static List<T> GetListOject<T>(string excelName, string sheetName, string startCell = "A1") where T : class, new()
    {
        var list = ExcelUtil.GetListOject<T>(excelName, sheetName, startCell);
        Translate(list, excelName, sheetName, startCell);
        return list;
    }

    /// <summary>ExcelUtil.GetDictionary, rebuilt on the translated list so keys that are translated names (skill names...) are the English ones.</summary>
    public static Dictionary<TKey, T> GetDictionary<TKey, T>(string excelName, string sheetName, string startCell = "A1", string keyName = "id", bool isClearZero = false) where T : class, new()
    {
        var list = GetListOject<T>(excelName, sheetName, startCell);
        Dictionary<TKey, T>? dictionary = null;
        if (list != null && list.Count > 0)
        {
            dictionary = new Dictionary<TKey, T>(list.Count);
            foreach (var item in list)
            {
                if (item == null) continue;
                var key = item.GetPropertyValue(keyName);
                if (key == null || key.GetType() != typeof(TKey))
                    Debug.LogError("配置" + excelName + "-" + sheetName + " key的类型定义异常:" + keyName + ",");
                else if (!isClearZero || (int)key != 0)
                    dictionary[(TKey)key] = item;
            }
        }
        return dictionary ?? new Dictionary<TKey, T>();
    }

    private static void Translate(IList? list, string excelName, string sheetName, string startCell)
    {
        try
        {
            if (list == null || Source == null) return;

            var stats = SheetInjector.Apply(list, excelName, sheetName, startCell, Source);
            if (stats == null) return;

            var line = $"{excelName}/{sheetName}: {stats}";
            if (stats.Matched < stats.Items || stats.JsonMissed > 0 || stats.UnmappedColumns.Count > 0) Plugin.Logger.LogWarning(line);
            else Plugin.Logger.LogInfo(line);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Translating {excelName}/{sheetName} failed, keeping the game's text: {ex}");
        }
    }
}

/// <summary>TornData/Data_Word.txt: UI strings by ID, used for every language. English reads column 3, Chinese column 2.</summary>
[HarmonyPatch(typeof(Data_Word), nameof(Data_Word.Onload))]
internal static class DataWordPatch
{
    private static void Postfix(Data_Word __instance)
    {
        try
        {
            var table = Injection.Source?.Get("TornData__Data_Word.csv");
            if (table == null) return;

            var index = IdIndex(__instance.list0);
            var changed = 0;
            foreach (var record in table.Records)
            {
                // Records are Row, ID, Section, Chinese; the first two rows are headers.
                if (record.Length < 4 || TranslationTable.RowOf(record) < 3 || record[3].Length == 0 || !int.TryParse(record[1], out var id)) continue;
                if (!index.TryGetValue(id, out var i)) continue;
                // The player's language only picks which column is read, so the English goes in the Chinese, English and Traditional ones.
                __instance.list2[i] = record[3];
                __instance.list3[i] = record[3];
                __instance.list5[i] = record[3];
                changed++;
            }
            Plugin.Logger.LogInfo($"Data_Word: {changed} of {__instance.list0.Count} strings translated.");
        }
        catch (Exception ex) { Plugin.Logger.LogError($"Translating Data_Word failed, keeping the game's text: {ex}"); }
    }

    internal static Dictionary<int, int> IdIndex(List<int> ids)
    {
        var index = new Dictionary<int, int>(ids.Count);
        for (var i = ids.Count - 1; i >= 0; i--) index[ids[i]] = i; // the game's own IndexOf finds the first, so the first wins
        return index;
    }
}

/// <summary>TornData/Data_Speak.txt: battle barks. Columns 1 to 11 (HP brackets) are the lists list1 to list11 whatever the language.</summary>
[HarmonyPatch(typeof(Data_Speak), nameof(Data_Speak.Onload))]
internal static class DataSpeakPatch
{
    private static void Postfix(Data_Speak __instance)
    {
        try
        {
            var table = Injection.Source?.Get("TornData__Data_Speak.csv");
            if (table == null) return;

            var lists = new[] { __instance.list1, __instance.list2, __instance.list3, __instance.list4, __instance.list5, __instance.list6,
                __instance.list7, __instance.list8, __instance.list9, __instance.list10, __instance.list11 };
            var index = DataWordPatch.IdIndex(__instance.list0);
            var changed = 0;
            foreach (var record in table.Records)
            {
                // Records are Row, ID, then one cell per list; the first two rows are headers.
                if (record.Length < 3 || TranslationTable.RowOf(record) < 3 || !int.TryParse(record[1], out var id) || !index.TryGetValue(id, out var i)) continue;
                for (var c = 2; c < record.Length && c - 2 < lists.Length; c++)
                {
                    if (record[c].Length == 0 || i >= lists[c - 2].Count) continue;
                    lists[c - 2][i] = record[c];
                    changed++;
                }
            }
            Plugin.Logger.LogInfo($"Data_Speak: {changed} barks translated.");
        }
        catch (Exception ex) { Plugin.Logger.LogError($"Translating Data_Speak failed, keeping the game's text: {ex}"); }
    }
}

/// <summary>LanguageConfig/LanguageConfig_jt.txt: system messages by key. It is the only table the game loads (for every language).</summary>
[HarmonyPatch(typeof(LanguageLocal), "LoadText")]
internal static class LanguageConfigPatch
{
    private static void Postfix(string texName, Dictionary<string, string> languageList)
    {
        try
        {
            if (texName != "_jt") return;
            var table = Injection.Source?.Get("LanguageConfig__LanguageConfig_jt.csv");
            if (table == null) return;

            var changed = 0;
            foreach (var record in table.Records)
            {
                // Records are Row, key, value.
                if (record.Length < 3 || record[2].Length == 0 || !languageList.TryGetValue(record[1], out var original)) continue;
                // The game keeps the line's trailing "\r" in the value, so keep it too.
                languageList[record[1]] = original.EndsWith("\r") ? record[2] + "\r" : record[2];
                changed++;
            }
            Plugin.Logger.LogInfo($"LanguageConfig: {changed} of {languageList.Count} messages translated.");
        }
        catch (Exception ex) { Plugin.Logger.LogError($"Translating LanguageConfig failed, keeping the game's text: {ex}"); }
    }
}
