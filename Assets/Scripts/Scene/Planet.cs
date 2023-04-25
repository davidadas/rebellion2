using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ICollectionExtensions;
using UnityEngine;

/// <summary>
///
/// </summary>
public class Planet : GameNode
{
    public bool IsColonized;
    public int OrbitSlots;
    public int GroundSlots;
    public int NumResourceNodes;
    public string OwnerGameID;

    // Status
    public bool IsDestroyed;
    public bool IsHeadquarters;

    // Popular Support
    public SerializableDictionary<string, int> PopularSupport =
        new SerializableDictionary<string, int>();

    // Child Nodes
    public List<Fleet> Fleets = new List<Fleet>();
    public List<Officer> Officers = new List<Officer>();
    public SerializableDictionary<BuildingSlot, List<Building>> Buildings =
        new SerializableDictionary<BuildingSlot, List<Building>>()
        {
            { BuildingSlot.Ground, new List<Building>() },
            { BuildingSlot.Orbit, new List<Building>() },
        };

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
    /// <param name="slot"></param>
    /// <returns></returns>
    public int GetAvailableSlots(BuildingSlot slot)
    {
        int numUsedSlots = Buildings[slot].Count(building => building.Slot == slot);
        int maxSlots = slot == BuildingSlot.Ground ? GroundSlots : OrbitSlots;

        return maxSlots - numUsedSlots;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="building"></param>
    public void AddBuilding(Building building)
    {
        BuildingSlot slot = building.Slot;

        if (
            slot == BuildingSlot.Ground && Buildings[slot].Count == GroundSlots
            || slot == BuildingSlot.Orbit && Buildings[slot].Count == OrbitSlots
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
    /// <param name="buildings"></param>
    public void AddBuildings(Building[] buildings)
    {
        IEnumerable<Building> groundBuildings = buildings.Where(
            building => building.Slot == BuildingSlot.Ground
        );
        IEnumerable<Building> orbitBuildings = buildings.Where(
            building => building.Slot == BuildingSlot.Orbit
        );

        // Ensure there is sufficient capacity for new buildings.
        if (
            groundBuildings.Count() > GetAvailableSlots(BuildingSlot.Ground)
            || orbitBuildings.Count() > GetAvailableSlots(BuildingSlot.Orbit)
        )
        {
            throw new GameException(
                $"Addional buildings would exceed {this.DisplayName}'s capacity."
            );
        }

        // Add the provided buildings to the existing building lists.
        Buildings[BuildingSlot.Ground].AddAll(groundBuildings);
        Buildings[BuildingSlot.Orbit].AddAll(orbitBuildings);
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        List<GameNode> combinedList = new List<GameNode>();
        Building[] buildings = Buildings.Values.SelectMany(building => building).ToArray();
        combinedList.AddAll(Fleets, Officers, buildings);

        return combinedList.ToArray();
    }
}
