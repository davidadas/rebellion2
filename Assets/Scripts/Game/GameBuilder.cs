using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// @WARNING: This class is considered a placeholder and is likely to change in the future.
/// </summary>
public class GameBuilder
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="summary"></param>
    /// <returns></returns>
    public static Game BuildGame(GameSummary summary)
    {
        PlanetSystem[] planetSystems = ResourceManager.GetGameNodeData<PlanetSystem>();
        Faction[] factions = ResourceManager.GetGameNodeData<Faction>();
        Game game = new Game(planetSystems, factions);

        game.Summary = summary;
        return game;
    }
}
