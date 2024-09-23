using System.Collections;
using System.Collections.Generic;
using System;

public enum GameSize
{
    Small = 0,
    Medium = 1,
    Large = 2,
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

    public int StartingResearchLevel;
    public string PlayerFactionID;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public GameSummary() { }
}
