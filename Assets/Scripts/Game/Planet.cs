using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Planet : GameNode
{
    public int OrbitSlots;
    public int GroundSlots;
    public int NumResources;
    public string OwnerGameID;

    // Status
    public bool IsDestroyed;

    // Popular support.
    public SerializableDictionary<string, int> PopularSupport =
        new SerializableDictionary<string, int>();

    // Child nodes.
    public List<Fleet> Fleets = new List<Fleet>();
    public List<Officer> Officers = new List<Officer>();

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Planet() { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        AppendableList<GameNode> combinedList = new AppendableList<GameNode>();
        combinedList.AppendAll(Fleets, Officers);

        return combinedList.ToArray();
    }
}
