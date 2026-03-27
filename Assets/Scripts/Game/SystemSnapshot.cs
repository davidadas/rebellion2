using System.Collections.Generic;
using Rebellion.Util.Attributes;

namespace Rebellion.Game
{
    /// <summary>
    /// System-level snapshot container.
    /// Contains per-planet snapshots within this system.
    /// </summary>
    [PersistableObject]
    public class SystemSnapshot
    {
        /// <summary>
        /// Per-planet snapshots within this system.
        /// Key: Planet.InstanceID
        /// Value: Snapshot of that planet
        /// </summary>
        public Dictionary<string, PlanetSnapshot> Planets;

        public SystemSnapshot()
        {
            Planets = new Dictionary<string, PlanetSnapshot>();
        }
    }
}
