using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.FogOfWar
{
    /// <summary>
    /// Stores the last known state of an observed planet sector.
    /// </summary>
    [PersistableObject]
    public class PlanetSectorSnapshot
    {
        // Planet snapshots keyed by planet instance ID.
        [PersistableMember(Name = nameof(Planets))]
        private Dictionary<string, PlanetSnapshot> _planets;

        [PersistableIgnore]
        public Dictionary<string, PlanetSnapshot> Planets
        {
            get => _planets;
            set => _planets = value;
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public PlanetSectorSnapshot()
        {
            _planets = new Dictionary<string, PlanetSnapshot>();
        }
    }
}
