using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Util.Reflection;

/// <summary>
/// Carries new-game and load-game selections between menu and strategy scenes.
/// </summary>
public static class GameLaunchContext
{
    /// <summary>
    /// Gets the launch settings for a new game.
    /// </summary>
    public static GameSummary Summary { get; private set; } = new GameSummary();

    public static string SaveFileName { get; set; }

    public static bool IsLoadGame { get; set; }

    public static bool PlayIntroCutscene { get; set; }

    /// <summary>
    /// Configures the next strategy-scene launch to restore a save.
    /// </summary>
    /// <param name="fileName">The save identifier to restore.</param>
    public static void PrepareSaveLoad(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A save file name is required.", nameof(fileName));

        SaveFileName = fileName;
        IsLoadGame = true;
        PlayIntroCutscene = false;
    }

    /// <summary>
    /// Restores the launch context to the authored new-game defaults.
    /// </summary>
    /// <param name="contentPack">The active content pack.</param>
    public static void Reset(ContentPack contentPack)
    {
        Summary = CreateDefaultSummary(contentPack);
        SaveFileName = null;
        IsLoadGame = false;
        PlayIntroCutscene = false;
    }

    /// <summary>
    /// Replaces the pending launch configuration with a detached custom-game summary.
    /// </summary>
    /// <param name="summary">The complete custom-game configuration.</param>
    internal static void ApplyCustomGameSummary(GameSummary summary)
    {
        Summary = (summary ?? throw new ArgumentNullException(nameof(summary))).GetDeepCopy();
        SaveFileName = null;
        IsLoadGame = false;
        PlayIntroCutscene = false;
    }

    /// <summary>
    /// Creates the default launch summary.
    /// </summary>
    /// <param name="contentPack">The active content pack.</param>
    /// <returns>The default launch summary.</returns>
    internal static GameSummary CreateDefaultSummary(ContentPack contentPack)
    {
        if (contentPack == null)
            throw new ArgumentNullException(nameof(contentPack));

        string[] startingFactionIds = contentPack.Scenario.PlayableFactionIDs.ToArray();
        return new GameSummary
        {
            Difficulty = GameDifficulty.Easy,
            GalaxySize = GameSize.Large,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = contentPack.Scenario.DefaultPlayerFactionID,
            StartingFactionIDs = startingFactionIds,
            PackID = contentPack.Definition.ID,
            PackVersion = contentPack.Definition.Version,
            ScenarioID = contentPack.Scenario.ID,
            ModIDs = contentPack.FileResolver.Mods.Select(mod => mod.ID).ToArray(),
            ModVersions = contentPack.FileResolver.Mods.Select(mod => mod.Version).ToArray(),
        };
    }
}
