using System.Text.RegularExpressions;
using FanslationStudio.LlmKit;
using FanslationStudio.LlmKit.Configuration;
using FanslationStudio.LlmKit.Support;
using FanslationStudio.LlmKit.Utility;
using FanslationStudio.LlmKit.Workflow;
using Translate;

namespace Tests;

/// <summary>
/// Golden packaged output for a fixed set of token-bearing and token-free rows (Tests/Golden/Raw), run end to end in a temp copy:
/// export with the game's splitter, fake-translate, package. Never touches Files/. Set POK_UPDATE_GOLDEN=1 to rewrite Tests/Golden/Expected
/// after an intended change (review the diff). The fake translation is deterministic: every run of non-ASCII text becomes "x&lt;hex&gt;", so a
/// moved or lost token shows up in the packaged file.
/// </summary>
[Collection("NamedTokens")]
public class PackagingGoldenTests
{
    private const string GoldenFolder = "../../../Golden";

    private static readonly string[] FixtureFiles =
    [
        "NPC__Dialog.csv", "NPC__SelfAction.csv", "NPC__NpcDialog.csv", "Touch__Touch.json.csv", "item__药品.csv",
    ];

    [Fact(DisplayName = "Golden: fixed token and token-free rows package to the expected files")]
    public async Task FixtureRows_PackageToGolden()
    {
        var work = NewTempDirectory();
        try
        {
            CopyDirectory($"{GoldenFolder}/Raw", $"{work}/Raw");
            File.Copy($"{GoldenFolder}/Config.yaml", $"{work}/Config.yaml");
            CopyDirectory($"{GoldenFolder}/Glossary", $"{work}/Glossary");

            var files = TextFileConfiguration.TextFilesToSplit.Where(t => FixtureFiles.Contains(t.Path)).ToArray();
            Assert.Equal(FixtureFiles.Length, files.Length);

            // Loading the config is what sets the replacer's named tokens.
            var config = ConfigurationExtensions.GetConfiguration(work);

            foreach (var file in files)
            {
                CsvGameDataWorkflow.ExportToCustomFormat(work, file, GameFileHandling.SplitterOptions);
                await FakeTranslateAsync(work, file, config);
            }

            // The fixture only proves the slotted path when Converted really holds slots and token values (and none for token-free text).
            Assert.Contains("tokens:", File.ReadAllText($"{work}/Converted/NPC__Dialog.csv.yaml"));
            Assert.Contains("{playerTitle}", File.ReadAllText($"{work}/Converted/NPC__Dialog.csv.yaml"));
            Assert.Contains("{npcName}", File.ReadAllText($"{work}/Converted/NPC__SelfAction.csv.yaml"));
            Assert.DoesNotContain("tokens:", File.ReadAllText($"{work}/Converted/item__药品.csv.yaml"));

            await TranslationPackaging.PackageFinalTranslationAsync(work, files);

            var expected = $"{GoldenFolder}/Expected";
            if (Environment.GetEnvironmentVariable("POK_UPDATE_GOLDEN") == "1")
            {
                Directory.CreateDirectory(expected);
                foreach (var file in Directory.GetFiles($"{work}/Mod"))
                    File.Copy(file, Path.Combine(expected, Path.GetFileName(file)), true);
            }

            var packaged = Directory.GetFiles($"{work}/Mod").Select(Path.GetFileName).Order().ToArray();
            Assert.Equal(Directory.GetFiles(expected).Select(Path.GetFileName).Order().ToArray(), packaged);
            foreach (var name in packaged)
                Assert.Equal(File.ReadAllText(Path.Combine(expected, name!)), File.ReadAllText($"{work}/Mod/{name}"));
        }
        finally
        {
            Directory.Delete(work, true);
        }
    }

    /// <summary>
    /// Packages a copy of a Files-shaped snapshot (Converted, Config.yaml, Glossary, Raw/Dumped/GameData) and compares every packaged file
    /// with the golden Mod folder next to it, writing the golden first when it does not exist. Only runs with POK_GOLDEN_CORPUS set to the
    /// snapshot folder, because it packages the whole corpus.
    /// </summary>
    [Fact(DisplayName = "Golden: a snapshot of the whole Converted corpus packages to the same Mod files")]
    public async Task Corpus_PackagesToGolden()
    {
        var snapshot = Environment.GetEnvironmentVariable("POK_GOLDEN_CORPUS");
        if (string.IsNullOrEmpty(snapshot))
            return;

        var golden = Path.Combine(snapshot, "..", "golden-mod");
        var work = NewTempDirectory();
        try
        {
            CopyDirectory(snapshot, work);
            ConfigurationExtensions.GetConfiguration(work);

            await TranslationPackaging.PackageFinalTranslationAsync(work, TextFileConfiguration.TextFilesToSplit);

            if (!Directory.Exists(golden))
                CopyDirectory($"{work}/Mod", golden);

            var packaged = Directory.GetFiles($"{work}/Mod").Select(Path.GetFileName).Order().ToArray();
            Assert.Equal(Directory.GetFiles(golden).Select(Path.GetFileName).Order().ToArray(), packaged);

            var different = packaged.Where(n => !File.ReadAllBytes($"{work}/Mod/{n}").AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(golden, n!)))).ToList();
            Assert.True(different.Count == 0, $"{different.Count} packaged files differ from the golden: {string.Join(", ", different)}");
        }
        finally
        {
            Directory.Delete(work, true);
        }
    }

    /// <summary>
    /// The gate for storing tokens as slots: the real Converted corpus is re-slotted in a temp copy (each split's text slotted with the
    /// slots numbered across its cell, its translation's restored tokens put back as slots, template literals slotted, the values stored
    /// in <c>tokens</c>), packaged, and written next to the snapshot as <c>reslotted-mod</c> to compare with <c>golden-mod</c>. Only runs with
    /// POK_GOLDEN_CORPUS set. Returns without asserting equality: the differences are the point (see the plan's phase 4 notes), so a
    /// script compares the two folders. It does assert that packaging restored every slot.
    /// </summary>
    [Fact(DisplayName = "Golden: the Converted corpus re-slotted packages to Mod files for comparison with the golden")]
    public async Task Corpus_ReSlotted_WritesModForComparison()
    {
        var snapshot = Environment.GetEnvironmentVariable("POK_GOLDEN_CORPUS");
        if (string.IsNullOrEmpty(snapshot))
            return;

        var work = NewTempDirectory();
        try
        {
            CopyDirectory(snapshot, work);
            ConfigurationExtensions.GetConfiguration(work);
            var slotter = StringTokenReplacer.Slotter;
            // POK_RESLOT_TEMPLATES=0 leaves the template literals alone, which isolates what slotting the fragments alone changes (nothing).
            var skipTemplates = Environment.GetEnvironmentVariable("POK_RESLOT_TEMPLATES") == "0";

            var stats = new List<string>();
            foreach (var file in Directory.GetFiles($"{work}/Converted"))
            {
                var lines = YamlHelper.CreateDeserializer().Deserialize<List<TranslationLine>>(File.ReadAllText(file));
                foreach (var line in lines)
                {
                    foreach (var column in line.Splits.GroupBy(s => (s.Split, s.SplitPath)))
                    {
                        var cell = new Dictionary<string, TokenValue>();
                        foreach (var split in column.OrderBy(s => s.SubIndex))
                        {
                            var (slotted, used) = slotter.Slot(split.Text, cell);
                            split.Text = slotted;
                            split.Tokens = used;
                            split.Translated = slotter.MaskRestored(split.Translated, used);
                        }

                        foreach (var template in line.Templates.Where(t => (t.Split, t.SplitPath) == column.Key && !skipTemplates))
                        {
                            var (slottedTemplate, templateTokens) = slotter.Slot(template.Template, cell);
                            template.Template = slottedTemplate;
                            template.Tokens = templateTokens;
                        }
                    }
                }

                var slottedSplits = lines.SelectMany(l => l.Splits).Count(s => s.Tokens.Count > 0);
                var slottedTemplates = lines.SelectMany(l => l.Templates).Count(t => t.Tokens.Count > 0);
                if (slottedSplits + slottedTemplates > 0)
                    stats.Add($"{Path.GetFileName(file)}: splits with tokens {slottedSplits}, templates with tokens {slottedTemplates}");

                File.WriteAllText(file, YamlHelper.CreateSerializer().Serialize(lines));
            }

            await TranslationPackaging.PackageFinalTranslationAsync(work, TextFileConfiguration.TextFilesToSplit);

            var output = Path.Combine(snapshot, "..", skipTemplates ? "reslotted-fragments-only-mod" : "reslotted-mod");
            if (Directory.Exists(output))
                Directory.Delete(output, true);
            CopyDirectory($"{work}/Mod", output);

            File.WriteAllLines(Path.Combine(output, "reslot-stats.txt"), stats);
            foreach (var file in Directory.GetFiles(output, "*.csv"))
                Assert.DoesNotContain("{npcName}", File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(work, true);
        }
    }

    // The translation stage as the pipeline stores it today: mask, translate, restore, so Translated holds the game's own form.
    private static async Task FakeTranslateAsync(string work, TextFileToSplit file, LlmConfig config)
    {
        var path = $"{work}/Converted/{file.Path}.yaml";
        var lines = YamlHelper.CreateDeserializer().Deserialize<List<TranslationLine>>(File.ReadAllText(path));

        foreach (var split in lines.SelectMany(l => l.Splits).Where(s => s.Text.Length > 0))
        {
            var replacer = new StringTokenReplacer();
            var masked = LineValidation.PrepareRaw(split.Text, replacer);
            split.Translated = replacer.Restore(Regex.Replace(masked, @"[^\x00-\x7F]+", m => $"x{(int)m.Value[0]:x}"));
        }

        await File.WriteAllTextAsync(path, YamlHelper.CreateSerializer().Serialize(lines));
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pok-golden-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), true);
    }
}
