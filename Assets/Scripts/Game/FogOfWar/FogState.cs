using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.FogOfWar
{
    /// <summary>
    /// Stores a faction's known view of observed planet sectors.
    /// </summary>
    [PersistableObject]
    public class FogState
    {
        // Snapshots.

        // Planet-sector snapshots keyed by sector instance ID.
        public Dictionary<string, PlanetSectorSnapshot> Snapshots;

        // Last observed planet for each visible entity instance ID.
        public Dictionary<string, string> EntityLastSeenAt;

        // Sector instance ID for each observed planet instance ID.
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
