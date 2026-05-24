using System.Collections.Generic;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.FogOfWar
{
    /// <summary>
    /// Stores the last known state of an observed planet.
    /// </summary>
    [PersistableObject]
    public class PlanetSnapshot
    {
        // Planet state.

        /// <summary>
        /// Tick when this snapshot was captured.
        /// </summary>
        public int TickCaptured;

        /// <summary>
        /// Faction instance ID that controlled the planet.
        /// </summary>
        public string OwnerInstanceID;

        /// <summary>
        /// Popular support by faction instance ID.
        /// </summary>
        public Dictionary<string, int> PopularSupport;

        // Visible entities.

        /// <summary>
        /// Officers visible on the planet.
        /// </summary>
        public List<Officer> Officers;

        /// <summary>
        /// Fleets visible at the planet.
        /// </summary>
        public List<Fleet> Fleets;

        /// <summary>
        /// Capital ships stationed at the planet.
        /// </summary>
        public List<CapitalShip> CapitalShips;

        /// <summary>
        /// Regiments stationed at the planet.
        /// </summary>
        public List<Regiment> Regiments;

        /// <summary>
        /// Buildings visible on the planet.
        /// </summary>
        public List<Building> Buildings;

        /// <summary>
        /// Starfighters stationed at the planet.
        /// </summary>
        public List<Starfighter> Starfighters;

        /// <summary>
        /// Missions previously detected at the planet.
        /// </summary>
        public List<Mission> Missions;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public PlanetSnapshot()
        {
            PopularSupport = new Dictionary<string, int>();
            Officers = new List<Officer>();
            Fleets = new List<Fleet>();
            CapitalShips = new List<CapitalShip>();
            Regiments = new List<Regiment>();
            Buildings = new List<Building>();
            Starfighters = new List<Starfighter>();
            Missions = new List<Mission>();
        }
    }
}
