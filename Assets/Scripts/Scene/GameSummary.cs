using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public enum GameSize
{
    Small = 1,
    Medium = 2,
    Large = 3,
}

public enum GameDifficulty
{
    Easy,
    Medium,
    Hard,
    VeryHard,
}

public enum GameVictoryCondition
{
    Headquarters,
    Conquest,
}

public enum GameResourceAvailability
{
    Limited,
    Normal,
    Abundant,
}

public enum GameStartingPlanets
{
    Sparse,
}

/// <summary>
///
/// </summary>
[Serializable]
public sealed class GameSummary
{
    public GameSize GalaxySize;
    public GameDifficulty Difficulty;
    public GameVictoryCondition VictoryCondition;
    public GameResourceAvailability ResourceAvailability;
    public GameStartingPlanets PlanetaryStart;

    public int StartingResearchLevel;
    public string PlayerFactionID;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public GameSummary() { }
}
