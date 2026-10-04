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
        [PersistableMember(Name = nameof(Snapshots))]
        private Dictionary<string, PlanetSectorSnapshot> _snapshots;

        // Last observed planet for each visible entity instance ID.
        [PersistableMember(Name = nameof(EntityLastSeenAt))]
        private Dictionary<string, string> _entityLastSeenAt;

        // Sector instance ID for each observed planet instance ID.
        [PersistableMember(Name = nameof(PlanetToSector))]
        private Dictionary<string, string> _planetToSector;

        [PersistableIgnore]
        public Dictionary<string, PlanetSectorSnapshot> Snapshots
        {
            get => _snapshots;
            set => _snapshots = value;
        }

        [PersistableIgnore]
        public Dictionary<string, string> EntityLastSeenAt
        {
            get => _entityLastSeenAt;
            set => _entityLastSeenAt = value;
        }

        [PersistableIgnore]
        public Dictionary<string, string> PlanetToSector
        {
            get => _planetToSector;
            set => _planetToSector = value;
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public FogState()
        {
            _snapshots = new Dictionary<string, PlanetSectorSnapshot>();
            _entityLastSeenAt = new Dictionary<string, string>();
            _planetToSector = new Dictionary<string, string>();
        }
    }
}
