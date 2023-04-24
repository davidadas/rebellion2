using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ICollectionExtensions;
using UnityEngine;

public class Planet : GameNode
{
    public bool IsColonized;
    public int OrbitSlots;
    public int GroundSlots;
    public int EnergySlots;
    public int NumResourceNodes;
    public string OwnerGameID;

    // Status
    public bool IsDestroyed;
    public bool IsHeadquarters;

    // Popular Support
    public SerializableDictionary<string, int> PopularSupport =
        new SerializableDictionary<string, int>();

    // Child Nodes
    public SerializableDictionary<BuildingSlot, Building[]> Buildings =
        new SerializableDictionary<BuildingSlot, Building[]>();
    public List<Fleet> Fleets = new List<Fleet>();
    public List<Officer> Officers = new List<Officer>();

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Planet() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="factionGameId"></param>
    /// <param name="support"></param>
    public void SetPopularSupport(string factionGameId, int support)
    {
        if (!PopularSupport.ContainsKey(factionGameId))
        {
            PopularSupport.Add(factionGameId, support);
        }

        PopularSupport[factionGameId] = support;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="building"></param>
    public void AddBuilding(Building building)
    {
        BuildingSlot slot = building.Slot;

        if (
            slot == BuildingSlot.Ground && Buildings[slot].Length == GroundSlots
            || slot == BuildingSlot.Orbit && Buildings[slot].Length == OrbitSlots
        )
        {
            throw new GameException(
                $"Cannot add {building.DisplayName} to {this.DisplayName}. Planet is at capacity."
            );
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        List<GameNode> combinedList = new List<GameNode>();
        combinedList.AddAll(Fleets, Officers);
        Debug.Log(Buildings.Values.ToList());

        return combinedList.ToArray();
    }
}
