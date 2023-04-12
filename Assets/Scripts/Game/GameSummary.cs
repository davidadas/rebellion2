using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public enum GameSize
{
    Small,
    Medium,
    Large,
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

[Serializable]
public sealed class GameSummary
{
    public GameSize Size;
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
