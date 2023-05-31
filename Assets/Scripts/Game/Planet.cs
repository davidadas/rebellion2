using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ICollectionExtensions;

/// <summary>
///
/// </summary>
public class Planet : GameNode
{
    public bool IsColonized;
    public int OrbitSlots;
    public int GroundSlots;
    public int NumResourceNodes;

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

    // Owner Info
    [CloneIgnore]
    public string OwnerGameID { get; set; }
    public string[] AllowedOwnerGameIDs;

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
        if (!IsColonized)
            throw new GameException(
                $"Cannot add building ${building.DisplayName} to {this.DisplayName}. Planet is not colonized."
            );

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
    /// <param name="capitalShip"></param>
    public void AddCapitalShip(CapitalShip capitalShip)
    {
        if (Fleets.Count > 0)
        {
            Fleets[0].AddCapitalShip(capitalShip);
        }
        else
        {
            if (this.OwnerGameID != capitalShip.OwnerGameID)
            {
                throw new SceneException(capitalShip, this, SceneExceptionType.Access);
            }
            Fleet fleet = new Fleet { OwnerGameID = capitalShip.OwnerGameID };
            fleet.AddCapitalShip(capitalShip);
            Fleets.Add(fleet);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="officer"></param>
    public void AddOfficer(Officer officer)
    {
        if (this.OwnerGameID != officer.OwnerGameID)
        {
            throw new SceneException(officer, this, SceneExceptionType.Access);
        }
        Officers.Add(officer);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="childNode"></param>
    protected override void AddChildNode(GameNode childNode)
    {
        if (childNode is Building)
        {
            AddBuilding((uilding)childNode);
        }
        else if (childNode is CapitalShip)
        {
            AddCapitalShip((CapitalShip)childNode);
        }
        else if (childNode is Officer)
        {
            AddOfficer((Officers)childNode);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="childNode"></param>
    protected override void RemoveChildNode(GameNode childNode)
    {

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
