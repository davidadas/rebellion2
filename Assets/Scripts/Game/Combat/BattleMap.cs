using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
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
    /// Describes the map used by a battle.
    /// </summary>
    [PersistableObject]
    public sealed class BattleMap
    {
        [PersistableMember(Name = "DeploymentRegions")]
        private List<BattleMapBounds> _deploymentRegions = new List<BattleMapBounds>();

        public BattleKind Kind { get; set; }
        public BattleMapBounds PlayableBounds { get; set; } = new BattleMapBounds();

        /// <summary>
        /// Returns the regions in which battle participants may be deployed.
        /// </summary>
        /// <returns>The map's deployment regions.</returns>
        public List<BattleMapBounds> GetDeploymentRegions()
        {
            return _deploymentRegions;
        }
    }
}
