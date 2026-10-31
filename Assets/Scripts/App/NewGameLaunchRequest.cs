using System;
using System.Collections.Generic;
using System.IO;
using Rebellion.Game;
using Rebellion.Util.Serialization;

/// <summary>
/// Carries an explicit new-campaign request across a desktop process boundary.
/// </summary>
[PersistableObject]
public sealed class NewGameLaunchRequest
{
    public string PackDirectory { get; set; }
    public GameSummary Summary { get; set; }

    /// <summary>
    /// Reads an optional new-game request without changing the saved content selection.
    /// </summary>
    /// <param name="arguments">The application command line.</param>
    /// <returns>The explicit request, or null for normal application startup.</returns>
    internal static NewGameLaunchRequest Read(IReadOnlyList<string> arguments)
    {
        string requestPath = null;
        for (int i = 0; i < arguments.Count; i++)
        {
            if (!string.Equals(arguments[i], "-newGame", StringComparison.OrdinalIgnoreCase))
                continue;
            if (
                requestPath != null
                || i + 1 >= arguments.Count
                || string.IsNullOrWhiteSpace(arguments[i + 1])
                || arguments[i + 1].StartsWith("-", StringComparison.Ordinal)
            )
                throw new ArgumentException("Supply one launch file after -newGame.");
            requestPath = arguments[++i];
        }
        if (requestPath == null)
            return null;
        using FileStream input = File.OpenRead(requestPath);
        return (NewGameLaunchRequest)
            new GameSerializer(typeof(NewGameLaunchRequest)).Deserialize(input);
    }

    /// <summary>
    /// Opens and checks the exact requested content before any application services are created.
    /// </summary>
    /// <param name="contentRootPath">The installed application and base-pack content directory.</param>
    /// <returns>The validated content for this new campaign.</returns>
    internal ContentPack OpenContent(string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(PackDirectory) || Summary == null)
            throw new InvalidDataException(
                "The launch file requires a content-pack folder and game settings."
            );
        ContentPack pack = ContentPackLoader.OpenFolder(
            contentRootPath,
            PackDirectory,
            Summary.ScenarioID
        );
        if (!pack.MatchesContentIdentity(Summary))
            throw new InvalidDataException(
                "The launch settings do not match the requested content pack."
            );
        if (!pack.Scenario.PlayableFactionIDs.Contains(Summary.PlayerFactionID))
            throw new InvalidDataException(
                "The selected faction is not playable in this scenario."
            );
        if (
            !Enum.IsDefined(typeof(GameSize), Summary.GalaxySize)
            || !Enum.IsDefined(typeof(GameDifficulty), Summary.Difficulty)
            || !Enum.IsDefined(typeof(GameResourceAvailability), Summary.ResourceAvailability)
            || !Enum.IsDefined(typeof(GameVictoryCondition), Summary.VictoryCondition)
        )
            throw new InvalidDataException("The launch file contains invalid game settings.");
        return pack;
    }
}
