using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using ICollectionExtensions;
using IDictionaryExtensions;
using UnityEngine;

public class Game : GameRoot
{
    // Game Details
    public GameSummary Summary;

    // Child Nodes
    public List<PlanetSystem> GalaxyMap = new List<PlanetSystem>();
    public List<Faction> Factions = new List<Faction>();
    public List<Officer> UnrecruitedOfficers = new List<Officer>();
    public SerializableDictionary<int, GameEventList> Events =
        new SerializableDictionary<int, GameEventList>();

    // Reference List
    public SerializableDictionary<string, ReferenceNode> Refences =
        new SerializableDictionary<string, ReferenceNode>();

    // Game States
    public int CurrentTick = 0;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Game() { }

    /// <summary>
    ///
    /// </summary>
    public void IncrementTick()
    {
        CurrentTick++;

        GameEventList gameEvents = new GameEventList();
        Events.TryGetValue(CurrentTick, out gameEvents);

        foreach (GameEvent gameEvent in gameEvents)
        {
            gameEvent.Trigger(this);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="node"></param>
    public override void AddReferenceNode(GameNode node)
    {
        Refences.Add(node.GameID, new ReferenceNode(node));
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public override GameNode GetReferenceNode(string gameId)
    {
        return Refences[gameId].Reference;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="tick"></param>
    /// <param name="gameEvent"></param>
    public void AddGameEvent(int tick, GameEvent gameEvent)
    {
        GameEventList eventList = Events.GetOrAddValue(tick, new GameEventList());
        eventList.Add(gameEvent);
    }

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
