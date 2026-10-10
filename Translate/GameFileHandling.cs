using FanslationStudio.LlmKit.Configuration;
using FanslationStudio.LlmKit.Utility;

namespace Translate;

/// <summary>Game-specific constants and LLM repair/validation hooks for Path of Kung Fu.</summary>
public static class GameFileHandling
{
    public static readonly string WorkingDirectory = "../../../../Files";
    public static readonly string GameFolder = @"G:\SteamLibrary\steamapps\common\The road of Jianghu";

    public static readonly CompoundFieldSplitterOptions SplitterOptions = new();

    public static readonly GameHooks Hooks = new()
    {
        LineContextProvider = CharacterLineContext.Provide,
    };
}
