using System;
using System.Collections.Generic;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.FogOfWar
{
    [Flags]
    public enum PlanetIntelligenceCategory
    {
        None = 0,
        Planet = 1 << 0,
        CapitalShips = 1 << 1,
        Starfighters = 1 << 2,
        GroundForces = 1 << 3,
        Buildings = 1 << 4,
        Officers = 1 << 5,
        Missions = 1 << 6,
        All = Planet | CapitalShips | Starfighters | GroundForces | Buildings | Officers | Missions,
    }

    /// <summary>
    /// Stores the last known state of an observed planet.
    /// </summary>
    [PersistableObject]
    public class PlanetSnapshot
    {
        // Planet State.
        [PersistableMember(Name = nameof(TickCaptured))]
        private int _tickCaptured;

        [PersistableMember(Name = nameof(OwnerInstanceID))]
        private string _ownerInstanceId;

        [PersistableMember(Name = nameof(IsColonized))]
        private bool _isColonized;

        [PersistableMember(Name = nameof(IsInUprising))]
        private bool _isInUprising;

        [PersistableMember(Name = nameof(IsDestroyed))]
        private bool _isDestroyed;

        [PersistableMember(Name = nameof(IsHeadquarters))]
        private bool _isHeadquarters;

        [PersistableMember(Name = nameof(NumRawResourceNodes))]
        private int _numRawResourceNodes;

        [PersistableMember(Name = nameof(EnergyCapacity))]
        private int _energyCapacity;

        [PersistableMember(Name = nameof(AllocatedEnergy))]
        private int _allocatedEnergy;

        // Popular Support.
        [PersistableMember(Name = nameof(PopularSupport))]
        private Dictionary<string, int> _popularSupport;

        // Visible Entities.
        [PersistableMember(Name = nameof(Officers))]
        private List<Officer> _officers;

        [PersistableMember(Name = nameof(Fleets))]
        private List<Fleet> _fleets;

        [PersistableMember(Name = nameof(Regiments))]
        private List<Regiment> _regiments;

        [PersistableMember(Name = nameof(SpecialForces))]
        private List<SpecialForces> _specialForces;

        [PersistableMember(Name = nameof(Buildings))]
        private List<Building> _buildings;

        [PersistableMember(Name = nameof(Starfighters))]
        private List<Starfighter> _starfighters;

        [PersistableMember(Name = nameof(Missions))]
        private List<Mission> _missions;

        // Intelligence categories revealed by espionage, informants, and other sources.
        [PersistableMember(Name = nameof(RevealedCategories))]
        private PlanetIntelligenceCategory _revealedCategories;

        // Manufacturing Intelligence.
        [PersistableMember(Name = nameof(HasManufacturingIntelligence))]
        private bool _hasManufacturingIntelligence;

        [PersistableMember(Name = nameof(ManufacturingQueueItems))]
        private List<IManufacturable> _manufacturingQueueItems;

        [PersistableIgnore]
        public int TickCaptured
        {
            get => _tickCaptured;
            set => _tickCaptured = value;
        }

        [PersistableIgnore]
        public string OwnerInstanceID
        {
            get => _ownerInstanceId;
            set => _ownerInstanceId = value;
        }

        [PersistableIgnore]
        public bool IsColonized
        {
            get => _isColonized;
            set => _isColonized = value;
        }

        [PersistableIgnore]
        public bool IsInUprising
        {
            get => _isInUprising;
            set => _isInUprising = value;
        }

        [PersistableIgnore]
        public bool IsDestroyed
        {
            get => _isDestroyed;
            set => _isDestroyed = value;
        }

        [PersistableIgnore]
        public bool IsHeadquarters
        {
            get => _isHeadquarters;
            set => _isHeadquarters = value;
        }

        [PersistableIgnore]
        public int NumRawResourceNodes
        {
            get => _numRawResourceNodes;
            set => _numRawResourceNodes = value;
        }

        [PersistableIgnore]
        public int EnergyCapacity
        {
            get => _energyCapacity;
            set => _energyCapacity = value;
        }

        [PersistableIgnore]
        public int AllocatedEnergy
        {
            get => _allocatedEnergy;
            set => _allocatedEnergy = value;
        }

        [PersistableIgnore]
        public Dictionary<string, int> PopularSupport
        {
            get => _popularSupport;
            set => _popularSupport = value;
        }

        [PersistableIgnore]
        public List<Officer> Officers
        {
            get => _officers;
            set => _officers = value;
        }

        [PersistableIgnore]
        public List<Fleet> Fleets
        {
            get => _fleets;
            set => _fleets = value;
        }

        [PersistableIgnore]
        public List<Regiment> Regiments
        {
            get => _regiments;
            set => _regiments = value;
        }

        [PersistableIgnore]
        public List<SpecialForces> SpecialForces
        {
            get => _specialForces;
            set => _specialForces = value;
        }

        [PersistableIgnore]
        public List<Building> Buildings
        {
            get => _buildings;
            set => _buildings = value;
        }

        [PersistableIgnore]
        public List<Starfighter> Starfighters
        {
            get => _starfighters;
            set => _starfighters = value;
        }

        [PersistableIgnore]
        public List<Mission> Missions
        {
            get => _missions;
            set => _missions = value;
        }

        [PersistableIgnore]
        public PlanetIntelligenceCategory RevealedCategories
        {
            get => _revealedCategories;
            set => _revealedCategories = value;
        }

        [PersistableIgnore]
        public bool HasManufacturingIntelligence
        {
            get => _hasManufacturingIntelligence;
            set => _hasManufacturingIntelligence = value;
        }

        [PersistableIgnore]
        public List<IManufacturable> ManufacturingQueueItems
        {
            get => _manufacturingQueueItems;
            set => _manufacturingQueueItems = value;
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public PlanetSnapshot()
        {
            _popularSupport = new Dictionary<string, int>();
            _officers = new List<Officer>();
            _fleets = new List<Fleet>();
            _regiments = new List<Regiment>();
            _specialForces = new List<SpecialForces>();
            _buildings = new List<Building>();
            _starfighters = new List<Starfighter>();
            _missions = new List<Mission>();
            _manufacturingQueueItems = new List<IManufacturable>();
        }
    }
}
