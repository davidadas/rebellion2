using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Multiplayer;
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
    /// Gets whether the pending strategy launch belongs to a negotiated multiplayer session.
    /// </summary>
    public static bool IsMultiplayer { get; private set; }

    /// <summary>
    /// Gets the faction presented and controlled on this machine.
    /// </summary>
    public static string LocalPlayerFactionID { get; private set; }

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
        IsMultiplayer = false;
        LocalPlayerFactionID = null;
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
        IsMultiplayer = false;
        LocalPlayerFactionID = null;
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
        IsMultiplayer = false;
        LocalPlayerFactionID = null;
    }

    /// <summary>
    /// Configures the next strategy launch from an agreed multiplayer handshake.
    /// </summary>
    /// <param name="configuration">The negotiated game and local faction settings.</param>
    public static void PrepareMultiplayer(MultiplayerLaunchConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        Summary = configuration.Summary.GetDeepCopy();
        SaveFileName = null;
        IsLoadGame = false;
        PlayIntroCutscene = false;
        IsMultiplayer = true;
        LocalPlayerFactionID = configuration.LocalFactionID;
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
