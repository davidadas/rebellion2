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
        // Planet State.
        public int TickCaptured;
        public string OwnerInstanceID;
        public bool IsColonized;
        public bool IsInUprising;
        public bool IsDestroyed;
        public bool IsHeadquarters;
        public int EnergyCapacity;
        public int AllocatedEnergy;

        // Popular Support.
        public Dictionary<string, int> PopularSupport;

        // Visible Entities.
        public List<Officer> Officers;
        public List<Fleet> Fleets;
        public List<Regiment> Regiments;
        public List<SpecialForces> SpecialForces;
        public List<Building> Buildings;
        public List<Starfighter> Starfighters;
        public List<Mission> Missions;

        // Manufacturing Intelligence.
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
