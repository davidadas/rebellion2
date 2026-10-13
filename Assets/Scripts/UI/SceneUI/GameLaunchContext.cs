using System;
using System.Linq;
using Rebellion.Game;

/// <summary>
/// Carries new-game and load-game selections between menu and strategy scenes.
/// </summary>
public static class GameLaunchContext
{
    private static ContentPack _contentPackAfterRestart;
    private static string _saveFileNameAfterContentRestart;

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
    /// Preserves a save request while application content is reconstructed.
    /// </summary>
    /// <param name="fileName">The save identifier to restore after reconstruction.</param>
    /// <param name="contentPack">The verified content pack to transfer to the replacement bootstrap.</param>
    internal static void PrepareSaveLoadAfterContentRestart(
        string fileName,
        ContentPack contentPack = null
    )
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A save file name is required.", nameof(fileName));

        _saveFileNameAfterContentRestart = fileName;
        _contentPackAfterRestart = contentPack;
    }

    /// <summary>
    /// Returns and consumes the verified content pack prepared for bootstrap reconstruction.
    /// </summary>
    /// <returns>The prepared content pack, or null when normal content selection should be loaded.</returns>
    internal static ContentPack ConsumeContentPackAfterRestart()
    {
        ContentPack contentPack = _contentPackAfterRestart;
        _contentPackAfterRestart = null;
        return contentPack;
    }

    /// <summary>
    /// Restores and consumes a save request preserved across content reconstruction.
    /// </summary>
    internal static void RestoreSaveLoadAfterContentRestart()
    {
        if (string.IsNullOrWhiteSpace(_saveFileNameAfterContentRestart))
            return;

        string fileName = _saveFileNameAfterContentRestart;
        _saveFileNameAfterContentRestart = null;
        _contentPackAfterRestart = null;
        PrepareSaveLoad(fileName);
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
    /// Creates the default launch summary.
    /// </summary>
    /// <param name="contentPack">The active content pack.</param>
    /// <returns>The default launch summary.</returns>
    private static GameSummary CreateDefaultSummary(ContentPack contentPack)
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
            StartingResearchLevel = 0,
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
