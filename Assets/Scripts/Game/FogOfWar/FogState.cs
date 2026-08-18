using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.FogOfWar
{
    /// <summary>
    /// Stores a faction's known view of observed systems.
    /// </summary>
    [PersistableObject]
    public class FogState
    {
        // Snapshots.

        /// <summary>
        /// System snapshots keyed by system instance ID.
        /// </summary>
        public Dictionary<string, PlanetSectorSnapshot> Snapshots;

        /// <summary>
        /// Last observed planet for each visible entity instance ID.
        /// </summary>
        public Dictionary<string, string> EntityLastSeenAt;

        /// <summary>
        /// System instance ID for each observed planet instance ID.
        /// </summary>
        public Dictionary<string, string> PlanetToSector;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public FogState()
        {
            Snapshots = new Dictionary<string, PlanetSectorSnapshot>();
            EntityLastSeenAt = new Dictionary<string, string>();
            PlanetToSector = new Dictionary<string, string>();
        }
    }
}
