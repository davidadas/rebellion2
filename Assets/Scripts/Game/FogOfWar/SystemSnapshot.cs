using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.FogOfWar
{
    /// <summary>
    /// Stores the last known state of an observed system.
    /// </summary>
    [PersistableObject]
    public class SystemSnapshot
    {
        /// <summary>
        /// Planet snapshots keyed by planet instance ID.
        /// </summary>
        public Dictionary<string, PlanetSnapshot> Planets;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SystemSnapshot()
        {
            Planets = new Dictionary<string, PlanetSnapshot>();
        }
    }
}
