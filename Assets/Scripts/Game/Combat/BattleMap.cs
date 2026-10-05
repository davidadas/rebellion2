using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Identifies the environment in which a battle takes place.
    /// </summary>
    public enum BattleKind
    {
        Space,
        Ground,
    }

    /// <summary>
    /// Defines an axis-aligned region of battle space.
    /// </summary>
    [PersistableObject]
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
    [PersistableObject]
    public sealed class BattleMapDeploymentRegion
    {
        public string ParticipantFactionInstanceID { get; set; }
        public BattleMapBounds Bounds { get; set; } = new BattleMapBounds();
    }

    /// <summary>
    /// Describes the map used by a battle.
    /// </summary>
    [PersistableObject]
    public sealed class BattleMap
    {
        [PersistableMember(Name = "DeploymentRegions")]
        private List<BattleMapDeploymentRegion> _deploymentRegions =
            new List<BattleMapDeploymentRegion>();

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
}
