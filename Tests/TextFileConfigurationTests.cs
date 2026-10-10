using FanslationStudio.LlmKit.Support;
using Translate;

namespace Tests;

// Read-only checks of TextFileConfiguration against the dumped CSVs; nothing is exported or written.
public class TextFileConfigurationTests
{
    private static string DumpedPath(TextFileToSplit file) => $"{GameFileHandling.WorkingDirectory}/Raw/Dumped/GameData/{file.Path}";

    [Fact(DisplayName = "TextFileConfiguration: every file exists, is flat, and has a translated column")]
    public void Files_Exist()
    {
        Assert.NotEmpty(TextFileConfiguration.TextFilesToSplit);
        Assert.Equal(TextFileConfiguration.TextFilesToSplit.Length, TextFileConfiguration.TextFilesToSplit.Select(f => f.Path).Distinct().Count());

        foreach (var file in TextFileConfiguration.TextFilesToSplit)
        {
            Assert.DoesNotContain('/', file.Path);
            Assert.True(File.Exists(DumpedPath(file)), $"Missing dumped file: {file.Path}");
            Assert.Contains(0, file.SkipColumns); // the dumper's Row column
        }
    }

    [Fact(DisplayName = "TextFileConfiguration: skip columns cover the whole file width, leaving only the chosen columns")]
    public void SkipColumns_MatchFileWidth()
    {
        foreach (var file in TextFileConfiguration.TextFilesToSplit)
        {
            var width = File.ReadLines(DumpedPath(file)).Max(line => FanslationStudio.LlmKit.Utility.CompoundFieldSplitter.ParseCsvRow(line).Length);

            Assert.True(file.SkipColumns.Max() < width, $"{file.Path}: skip column {file.SkipColumns.Max()} is outside the {width}-column file");
            Assert.True(file.SkipColumns.Count < width, $"{file.Path}: every column is skipped");
        }
    }
}
