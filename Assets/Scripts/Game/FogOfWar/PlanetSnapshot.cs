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
        /// Whether the planet was colonized when observed.
        /// </summary>
        public bool IsColonized;

        /// <summary>
        /// Whether the planet was in uprising when observed.
        /// </summary>
        public bool IsInUprising;

        /// <summary>
        /// Whether the planet was destroyed when observed.
        /// </summary>
        public bool IsDestroyed;

        /// <summary>
        /// Whether the planet hosted a headquarters when observed.
        /// </summary>
        public bool IsHeadquarters;

        /// <summary>
        /// Energy capacity available when the planet was observed.
        /// </summary>
        public int EnergyCapacity;

        /// <summary>
        /// Energy allocated when the planet was observed.
        /// </summary>
        public int AllocatedEnergy;

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
        /// Regiments stationed at the planet.
        /// </summary>
        public List<Regiment> Regiments;

        /// <summary>
        /// Special-forces units stationed at the planet.
        /// </summary>
        public List<SpecialForces> SpecialForces;

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

        public bool HasManufacturingIntelligence;

        public List<IManufacturable> ManufacturingQueueItems;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public PlanetSnapshot()
        {
            PopularSupport = new Dictionary<string, int>();
            Officers = new List<Officer>();
            Fleets = new List<Fleet>();
            Regiments = new List<Regiment>();
            SpecialForces = new List<SpecialForces>();
            Buildings = new List<Building>();
            Starfighters = new List<Starfighter>();
            Missions = new List<Mission>();
            ManufacturingQueueItems = new List<IManufacturable>();
        }
    }
}
