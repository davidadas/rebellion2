using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ICollectionExtensions;
using UnityEngine;

public class Game : GameNode
{
    public GameSummary Summary;

    // Child nodes.
    public List<PlanetSystem> GalaxyMap = new List<PlanetSystem>();
    public List<Faction> Factions = new List<Faction>();
    public List<Building> BuildingResearchList = new List<Building>();
    public List<Officer> UnrecruitedOfficers = new List<Officer>();

    public int CurrentTick = 0;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Game() { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        List<GameNode> combinedList = new List<GameNode>();
        combinedList.AddAll(Factions, GalaxyMap, UnrecruitedOfficers);

        return combinedList.ToArray();
    }
}
