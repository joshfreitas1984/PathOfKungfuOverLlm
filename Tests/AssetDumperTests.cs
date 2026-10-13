using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Translate;

namespace Tests;

/// <summary>
/// "0b. Dump prefab text from asset files" - offline equivalent of the plugin's runtime prefab dumper. The runtime dumper only sees Text objects that are
/// loaded when you press the button, so screens you did not open (the map) are missing. This reads the game's serialized files
/// (levelN, sharedassetsN, resources.assets, bundles) directly, so every prefab and scene text is found without opening any screen.
/// The Mono assemblies in Managed describe the MonoBehaviour layouts (UnityEngine.UI.Text's m_Text).
///
/// Merges the strings it finds into Files/Raw/Dumped/PrefabText/prefabText.txt (existing lines kept, new ones appended) and writes
/// dumpedOtherText.txt (every string with its field and path, for reference).
/// </summary>
public class AssetDumperTests
{
    private static readonly Regex Chinese = new(@"\p{IsCJKUnifiedIdeographs}", RegexOptions.Compiled);
    private const int MaxStringLength = 2000;
    private static readonly string[] IgnoredFields = ["m_Name", "first"];

    [Fact(DisplayName = "0b. Dump prefab text from asset files")]
    public void DumpPrefabTextFromAssets()
    {
        var dataFolder = Directory.GetDirectories(GameFileHandling.GameFolder, "*_Data").Single();
        var manager = new AssetsManager { MonoTempGenerator = new MonoCecilTempGenerator(Path.Combine(dataFolder, "Managed")) };

        if (File.Exists("classdata.tpk"))
            manager.LoadClassPackage("classdata.tpk");

        var found = new Dictionary<string, (string Field, string Path)>();
        var errors = new Dictionary<string, int>();
        var scanned = 0;
        var failed = 0;

        foreach (var path in Directory.GetFiles(dataFolder, "*", SearchOption.AllDirectories).Where(IsCandidate))
        {
            try
            {
                var extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension is ".unity3d" or ".assetbundle" or ".bundle")
                {
                    var bundle = manager.LoadBundleFile(path, true);
                    for (var i = 0; i < bundle.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
                    {
                        if (bundle.file.IsAssetsFile(i))
                            Scan(manager, manager.LoadAssetsFileFromBundle(bundle, i, false), found, errors, ref scanned, ref failed);
                    }
                }
                else
                {
                    Scan(manager, manager.LoadAssetsFile(path, false), found, errors, ref scanned, ref failed);
                }
            }
            catch (Exception ex) { errors[ex.Message] = errors.GetValueOrDefault(ex.Message) + 1; }
        }

        var outputDirectory = $"{GameFileHandling.WorkingDirectory}/Raw/Dumped/PrefabText";
        Directory.CreateDirectory(outputDirectory);

        var primary = found.Where(kv => kv.Value.Field is "m_Text" or "m_text").Select(kv => kv.Key).Order(StringComparer.Ordinal).ToList();

        // Merge into prefabText.txt, the one translation input: every line already there is kept (the runtime dump has strings the game builds itself,
        // and keeping them keeps their translations), and the asset strings it lacks are appended. Re-run after copying in a fresh runtime dump.
        var dumpPath = $"{outputDirectory}/prefabText.txt";
        var existing = File.Exists(dumpPath) ? File.ReadAllLines(dumpPath).Where(line => line.Length > 0).ToList() : [];
        var known = existing.ToHashSet();
        var added = primary.Where(text => !known.Contains(text)).ToList();
        File.WriteAllLines(dumpPath, existing.Concat(added));
        Console.WriteLine($"prefabText.txt: {existing.Count} existing lines kept, {added.Count} added from the asset files.");
        File.WriteAllLines($"{outputDirectory}/dumpedOtherText.txt", found.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}\t{kv.Value.Field}\t{kv.Value.Path}"));

        Console.WriteLine($"{found.Count} distinct Chinese strings ({primary.Count} in m_Text) from {scanned} assets; {failed} assets could not be read.");
        foreach (var (message, count) in errors.OrderByDescending(kv => kv.Value).Take(5))
            Console.WriteLine($"  [{count}x] {message}");

        Assert.NotEmpty(primary);
    }

    // Only Unity's serialized files: AssetBundles (.unity3d), .assets, and the extension-less globalgamemanagers/levelN. The 10,000 png/jpg, the
    // xlsx tables and the .mData files in StreamingAssets are not asset containers and only produce exceptions.
    private static bool IsCandidate(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".unity3d" or ".assets" || (extension.Length == 0 && Path.GetFileName(path) is var name
            && (name.StartsWith("level") || name == "globalgamemanagers"));
    }

    private static void Scan(AssetsManager manager, AssetsFileInstance instance, Dictionary<string, (string, string)> found, Dictionary<string, int> errors,
        ref int scanned, ref int failed)
    {
        if (manager.ClassDatabase == null && manager.ClassPackage != null)
            manager.LoadClassDatabaseFromPackage(instance.file.Metadata.UnityVersion);

        foreach (var info in instance.file.Metadata.AssetInfos)
        {
            // Text components are MonoBehaviours. Skipping every other type (textures, meshes, audio, TextAssets that hold whole data files) is what keeps this fast.
            if (info.TypeId != (int)AssetClassID.MonoBehaviour)
                continue;

            scanned++;
            try
            {
                var field = manager.GetBaseField(instance, info);
                if (field == null) { failed++; continue; }
                Walk(field, found, field.TypeName);
            }
            catch (Exception ex)
            {
                failed++;
                errors[ex.Message] = errors.GetValueOrDefault(ex.Message) + 1;
            }
        }
    }

    private static void Walk(AssetTypeValueField field, Dictionary<string, (string, string)> found, string path)
    {
        var current = $"{path}.{field.FieldName}";
        if (field.Value is { ValueType: AssetValueType.String })
        {
            var text = field.AsString;
            if (!string.IsNullOrEmpty(text) && text.Length <= MaxStringLength && Chinese.IsMatch(text) && !IgnoredFields.Contains(field.FieldName))
                found.TryAdd(text.Replace("\n", "\\n").Replace("\r", ""), (field.FieldName, current));
        }

        foreach (var child in field.Children)
            Walk(child, found, current);
    }
}
