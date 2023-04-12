using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Game : GameNode
{
    public GameSummary Summary;

    // Child nodes.
    public AppendableList<PlanetSystem> PlanetSystems = new AppendableList<PlanetSystem>();
    public AppendableList<Faction> Factions = new AppendableList<Faction>();

    public int CurrentTick;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Game() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="planetSystems"></param>
    /// <param name="factions"></param>
    public Game(PlanetSystem[] planetSystems, Faction[] factions)
    {
        PlanetSystems = new AppendableList<PlanetSystem>(planetSystems);
        Factions = new AppendableList<Faction>(factions);

        // This constructor is only called for new games.
        // Therefore, set the current tick to 0.
        CurrentTick = 0;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        AppendableList<GameNode> combinedList = new AppendableList<GameNode>();
        combinedList.AppendAll(Factions, PlanetSystems);

        return combinedList.ToArray();
    }
}
