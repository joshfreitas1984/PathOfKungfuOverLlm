using FanslationStudio.LlmKit.Utility;
using FanslationStudio.LlmKit.Workflow;
using Translate;

namespace Tests;

public class TextResizerTests
{
    // The splitting itself lives in LlmKit (EditorFileSplitter) so every game packages the editor folders the same way.
    // The splitter ignores missing folders, so these are safe before any resizers/layouts/sprites exist.
    [Fact]
    public static void MoveResizersIntoPathBasedFiles() => EditorFileSplitter.SplitResizers(GameFileHandling.WorkingDirectory);

    [Fact]
    public static void MoveSpritesIntoPathBasedFiles() => EditorFileSplitter.SplitSprites(GameFileHandling.WorkingDirectory);

    [Fact]
    public static void MoveLayoutsIntoPathBasedFiles() => EditorFileSplitter.SplitLayouts(GameFileHandling.WorkingDirectory);

    // Symlink the game's BepInEx editor folders to this repo's Files/ folders, so resizers, layouts and
    // sprites made with the in-game editor are saved straight into the working tree and can be checked in.
    // Needs an elevated IDE (or Windows Developer Mode). An existing destination folder is deleted first.
    [Fact]
    public void CreateSymlinkToResizer() => Link("Resizers", "resizers");

    [Fact]
    public void CreateSymlinkToSprites() => Link("Sprites", "sprites2");

    [Fact]
    public void CreateSymlinkToLayouts() => Link("Layouts", "layouts");

    private static void Link(string repoFolder, string bepInExFolder)
    {
        var source = Path.GetFullPath($"{GameFileHandling.WorkingDirectory}/{repoFolder}");
        Directory.CreateDirectory(source);
        SymlinkWorkflow.CreateSymlink(source, $@"{GameFileHandling.GameFolder}\BepInEx\{bepInExFolder}");
    }
}
