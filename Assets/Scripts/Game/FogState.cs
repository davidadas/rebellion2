using System.Collections.Generic;
using Rebellion.Util.Attributes;

namespace Rebellion.Game
{
    /// <summary>
    /// Per-faction fog of war state.
    /// Stores snapshots of enemy/neutral systems and tracks entity locations.
    /// </summary>
    [PersistableObject]
    public class FogState
    {
        /// <summary>
        /// System snapshots with planet-level data.
        /// Key: PlanetSystem.InstanceID
        /// Value: SystemSnapshot containing per-planet snapshots
        /// </summary>
        public Dictionary<string, SystemSnapshot> Snapshots;

        /// <summary>
        /// Reverse index for invalidation.
        /// Key: Entity InstanceID (officer, fleet, regiment, building, etc.)
        /// Value: PlanetID where we last saw this entity
        /// </summary>
        public Dictionary<string, string> EntityLastSeenAt;

        /// <summary>
        /// Planet to system lookup for O(1) invalidation.
        /// Key: Planet.InstanceID
        /// Value: PlanetSystem.InstanceID
        /// </summary>
        public Dictionary<string, string> PlanetToSystem;

        public FogState()
        {
            Snapshots = new Dictionary<string, SystemSnapshot>();
            EntityLastSeenAt = new Dictionary<string, string>();
            PlanetToSystem = new Dictionary<string, string>();
        }
    }
}
