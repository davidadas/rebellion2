using System.Collections.Generic;
using Rebellion.Game.Combat;

/// <summary>
/// Defines an axis-aligned region of battle space.
/// </summary>
public sealed class BattleMapBounds
{
    public float MinimumX { get; set; }
    public float MaximumX { get; set; }
    public float MinimumY { get; set; }
    public float MaximumY { get; set; }
    public float MinimumZ { get; set; }
    public float MaximumZ { get; set; }
}

/// <summary>
/// Assigns a bounded deployment region to one battle participant.
/// </summary>
public sealed class BattleMapDeploymentRegion
{
    public string ParticipantSlotID { get; set; }
    public BattleMapBounds Bounds { get; set; } = new BattleMapBounds();
}

/// <summary>
/// Describes an authored map available to tactical battles.
/// </summary>
public sealed class BattleMap
{
    private readonly List<BattleMapDeploymentRegion> _deploymentRegions =
        new List<BattleMapDeploymentRegion>();

    public string InstanceID { get; set; }
    public BattleKind Kind { get; set; }
    public BattleMapBounds PlayableBounds { get; set; } = new BattleMapBounds();

    /// <summary>
    /// Returns the regions in which battle participants may be deployed.
    /// </summary>
    /// <returns>The map's deployment regions.</returns>
    public List<BattleMapDeploymentRegion> GetDeploymentRegions()
    {
        return _deploymentRegions;
    }
}
