using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Util.Attributes;

namespace Rebellion.Game
{
    /// <summary>
    /// Frozen snapshot of a planet at a specific tick.
    /// Contains full deep copies of entities (NOT references).
    /// </summary>
    [PersistableObject]
    public class PlanetSnapshot
    {
        /// <summary>
        /// When this snapshot was captured (game tick).
        /// </summary>
        public int TickCaptured;

        /// <summary>
        /// Who controlled the planet when snapshot was taken.
        /// Nullable - planet might be neutral.
        /// </summary>
        public string OwnerInstanceID;

        /// <summary>
        /// Popular support levels at TickCaptured.
        /// Dictionary of faction ID -> support percentage.
        /// </summary>
        public Dictionary<string, int> PopularSupport;

        /// <summary>
        /// Full deep copies of officers as they existed at TickCaptured.
        /// NOT references - actual copied entities with stats frozen in time.
        /// </summary>
        public List<Officer> Officers;

        /// <summary>
        /// Full deep copies of fleets.
        /// </summary>
        public List<Fleet> Fleets;

        /// <summary>
        /// Full deep copies of regiments.
        /// </summary>
        public List<Regiment> Regiments;

        /// <summary>
        /// Full deep copies of buildings.
        /// </summary>
        public List<Building> Buildings;

        /// <summary>
        /// Full deep copies of starfighters.
        /// </summary>
        public List<Starfighter> Starfighters;

        /// <summary>
        /// Enemy missions captured at TickCaptured (e.g. revealed by espionage).
        /// Persists in the view even when the planet is later observed live,
        /// so the player can see both current units and previously-discovered missions.
        /// </summary>
        public List<Mission> Missions;

        public PlanetSnapshot()
        {
            PopularSupport = new Dictionary<string, int>();
            Officers = new List<Officer>();
            Fleets = new List<Fleet>();
            Regiments = new List<Regiment>();
            Buildings = new List<Building>();
            Starfighters = new List<Starfighter>();
            Missions = new List<Mission>();
        }
    }
}
