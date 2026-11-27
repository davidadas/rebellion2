using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Galaxy;
using Rebellion.Game.ShipComponents;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    public enum PrimaryWeaponType
    {
        Turbolaser,
        IonCannon,
        LaserCannon,
    }

    public enum PrimaryWeaponArc
    {
        Fore,
        Aft,
        Port,
        Starboard,
    }

    public enum CapitalShipRole
    {
        PrimaryLine,
        SecondaryLine,
        Escort,
        Interdictor,
        Transport,
        Carrier,
        Flagship,
    }

    /// <summary>
    /// Represents a capital ship in the game.
    /// </summary>
    public class CapitalShip : ContainerNode, IManufacturable, IMovable, IEncyclopediaSource
    {
        public string EncyclopediaImagePath { get; set; }
        public List<EncyclopediaEntryStat> EncyclopediaStats { get; set; } =
            new List<EncyclopediaEntryStat>();
        public string EncyclopediaDescription { get; set; }
        public static IReadOnlyList<PrimaryWeaponArc> PrimaryWeaponArcs { get; } =
            Array.AsReadOnly((PrimaryWeaponArc[])Enum.GetValues(typeof(PrimaryWeaponArc)));

        public static int PrimaryWeaponRangeIndex => PrimaryWeaponArcs.Count;

        internal static IReadOnlyList<PrimaryWeaponType> PrimaryWeaponTypes { get; } =
            Array.AsReadOnly((PrimaryWeaponType[])Enum.GetValues(typeof(PrimaryWeaponType)));

        [PersistableMember(Name = "HasAssignedName")]
        private bool _hasAssignedName;

        public string BattleResultImagePath { get; set; }
        public string BattleResultInTransitImagePath { get; set; }
        public string BattleResultDamagedImagePath { get; set; }
        public string BackgroundImagePath { get; set; }

        // Naming Info.
        public string ShipNamePoolID { get; set; }
        public bool HasAssignedName => _hasAssignedName;

        // Manufacture Info.
        public string ProducerOwnerID { get; set; }
        public string ProducerPlanetID { get; set; }
        public long ManufacturingQueueSequence { get; set; }
        public int ConstructionCost { get; set; }
        public int MaintenanceCost { get; set; }
        public int BaseBuildSpeed { get; set; }
        public List<string> ManufacturingFactionInstanceIDs { get; set; }
        public int ResearchOrder { get; set; }
        public int ResearchDifficulty { get; set; }
        public int UprisingDefense { get; set; }

        // Hull, Shield, and Repair Info.
        [PersistableMember(Name = nameof(MaxHullStrength))]
        private int _maxHullStrength;

        [PersistableMember(Name = nameof(CurrentHullStrength))]
        private int _currentHullStrength;

        [PersistableMember(Name = nameof(NextRepairTick))]
        private int _nextRepairTick;

        [PersistableMember(Name = nameof(RepairIntervalTicks))]
        private int _repairIntervalTicks;

        [PersistableMember(Name = nameof(DamageControl))]
        private int _damageControl;

        private int _maxShieldStrength;

        [PersistableMember(Name = nameof(MaxShieldStrength))]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private int? LegacyMaxShieldStrength
        {
            get => ShieldGenerator == null ? _maxShieldStrength : null;
            set => _maxShieldStrength = value.GetValueOrDefault();
        }

        private int _shieldRechargeRate;

        [PersistableMember(Name = nameof(ShieldRechargeRate))]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private int? LegacyShieldRechargeRate
        {
            get => ShieldGenerator == null ? _shieldRechargeRate : null;
            set => _shieldRechargeRate = value.GetValueOrDefault();
        }

        // Movement Info.
        private Hyperdrive[] _hyperdrives = Array.Empty<Hyperdrive>();
        private bool _hasAuthoredHyperdrives;
        private int _legacyDriveComponents;

        [PersistableMember(Name = nameof(Hyperdrive))]
        [PersistableIgnore]
        [SuppressMessage(
            "Roslynator",
            "RCS1213",
            Justification = "Reads older ship content and saves."
        )]
        private int? LegacyHyperdrive
        {
            get => null;
            set => ReadLegacyDriveRating(0, value);
        }

        [PersistableMember(Name = "BackupHyperdrive")]
        [PersistableIgnore]
        [SuppressMessage(
            "Roslynator",
            "RCS1213",
            Justification = "Reads older ship content and saves."
        )]
        private int? LegacyBackupHyperdrive
        {
            get => null;
            set => ReadLegacyDriveRating(1, value);
        }

        [PersistableMember(Name = "PrimaryDrive")]
        [PersistableIgnore]
        [SuppressMessage(
            "Roslynator",
            "RCS1213",
            Justification = "Reads older ship content and saves."
        )]
        private Hyperdrive LegacyPrimaryDrive
        {
            get => null;
            set => ReadLegacyDriveComponent(0, value);
        }

        [PersistableMember(Name = "BackupDrive")]
        [PersistableIgnore]
        [SuppressMessage(
            "Roslynator",
            "RCS1213",
            Justification = "Reads older ship content and saves."
        )]
        private Hyperdrive LegacyBackupDrive
        {
            get => null;
            set => ReadLegacyDriveComponent(1, value);
        }

        [PersistableMember(Name = nameof(HyperdriveCount))]
        private int? _hyperdriveCount;

        public CapitalShipSystemDamage SystemDamage { get; set; } = new CapitalShipSystemDamage();
        public CapitalShipGeometry PhysicalGeometry { get; set; }
        public Engine Engine { get; set; }

        [PersistableIgnore]
        public Hyperdrive[] Hyperdrives
        {
            get => _hyperdrives;
            set
            {
                _hyperdrives = value ?? Array.Empty<Hyperdrive>();
                _hasAuthoredHyperdrives = true;
            }
        }

        [PersistableMember(Name = nameof(Hyperdrives))]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private Hyperdrive[] SavedHyperdrives
        {
            get =>
                Array.Exists(_hyperdrives, drive => drive == null)
                    ? Array.ConvertAll(_hyperdrives, drive => drive ?? new Hyperdrive())
                    : _hyperdrives;
            set => Hyperdrives = value;
        }

        internal bool HasAuthoredHyperdrives => _hasAuthoredHyperdrives || _hyperdrives.Length > 1;
        public ShieldGenerator ShieldGenerator { get; set; }
        public StarfighterBay StarfighterBay { get; set; }
        public GravityWellGenerator GravityWellGenerator { get; set; }
        private List<HardpointGroup> _hardpointGroups = new();

        public List<HardpointGroup> HardpointGroups => _hardpointGroups;

        [PersistableMember(Name = nameof(HardpointGroups))]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private List<HardpointGroup> SavedHardpointGroups
        {
            get => _hardpointGroups;
            set
            {
                if (value is { Count: > 0 })
                    _hardpointGroups = value;
            }
        }

        private int _sublightSpeed;

        [PersistableMember(Name = nameof(SublightSpeed))]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private int? LegacySublightSpeed
        {
            get => Engine == null ? _sublightSpeed : null;
            set => _sublightSpeed = value.GetValueOrDefault();
        }

        private int _maneuverability;

        [PersistableMember(Name = nameof(Maneuverability))]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private int? LegacyManeuverability
        {
            get => Engine == null ? _maneuverability : null;
            set => _maneuverability = value.GetValueOrDefault();
        }

        // Capacity Info.
        private int _starfighterCapacity;

        [PersistableMember(Name = nameof(StarfighterCapacity))]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private int? LegacyStarfighterCapacity
        {
            get => StarfighterBay == null ? _starfighterCapacity : null;
            set => _starfighterCapacity = value.GetValueOrDefault();
        }

        [PersistableMember(Name = nameof(RegimentCapacity))]
        private int _regimentCapacity;

        [PersistableMember(Name = nameof(Roles))]
        private List<CapitalShipRole> _roles = new List<CapitalShipRole>();

        // Unit Info.
        [PersistableMember(Name = "Officers")]
        private List<Officer> _officers = new List<Officer>();

        [PersistableMember(Name = "Regiments")]
        private List<Regiment> _regiments = new List<Regiment>();

        [PersistableMember(Name = "SpecialForces")]
        private List<SpecialForces> _specialForces = new List<SpecialForces>();

        [PersistableMember(Name = "Starfighters")]
        private List<Starfighter> _starfighters = new List<Starfighter>();

        [PersistableMember(Name = "PrimaryWeapons")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Reads older saves and content.")]
        private Dictionary<PrimaryWeaponType, int[]> LegacyPrimaryWeapons
        {
            get => null;
            set
            {
                if (HardpointGroups is { Count: > 0 } || value == null)
                    return;

                List<HardpointGroup> groups = new(PrimaryWeaponArcs.Count);
                foreach (PrimaryWeaponArc arc in PrimaryWeaponArcs)
                {
                    HardpointGroup group = new() { Arc = GetBatteryArc(arc) };
                    foreach (PrimaryWeaponType weapon in PrimaryWeaponTypes)
                    {
                        if (!value.TryGetValue(weapon, out int[] ratings) || ratings == null)
                            continue;
                        group
                            .GetHardpoints()
                            .Add(
                                new Hardpoint
                                {
                                    Weapon = new WeaponData
                                    {
                                        WeaponType = GetHardpointWeaponType(weapon),
                                        Damage = (int)arc < ratings.Length ? ratings[(int)arc] : 0,
                                        Range =
                                            PrimaryWeaponRangeIndex < ratings.Length
                                                ? ratings[PrimaryWeaponRangeIndex]
                                                : 0,
                                    },
                                }
                            );
                    }

                    groups.Add(group);
                }

                _hardpointGroups = groups;
            }
        }

        [PersistableMember(Name = nameof(WeaponRecharge))]
        private int _weaponRecharge;

        [PersistableMember(Name = nameof(Bombardment))]
        private int _bombardment;

        // Manufacturing Info.
        public int ManufacturingProgress { get; set; } = 0;
        public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;

        public int RefinedMaterialProgress { get; set; }
        public int ProductionCapacity { get; set; }
        public int ProductionCapacityUsed { get; set; }
        public int KdyPool { get; set; }
        public int LnrPool { get; set; }

        // Movement Info.
        public MovementState Movement
        {
            get => _movement;
            set
            {
                if (ReferenceEquals(_movement, value))
                    return;
                _movement = value;
                OnAvailabilityChanged();
            }
        }

        private MovementState _movement;

        // Misc Info.
        [PersistableMember(Name = nameof(TractorBeamPower))]
        private int _tractorBeamPower;

        [PersistableMember(Name = nameof(TractorBeamnRange))]
        private int _tractorBeamRange;

        private bool _hasGravityWell;

        [PersistableMember(Name = nameof(HasGravityWell))]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private bool? LegacyHasGravityWell
        {
            get => GravityWellGenerator == null ? _hasGravityWell : null;
            set => _hasGravityWell = value.GetValueOrDefault();
        }

        private int? _sectorSuppression;
        private PlanetSector _suppressionSector;

        /// <summary>
        /// Whether this ship can destroy planets.
        /// </summary>
        public bool CanDestroyPlanets { get; set; }

        /// <summary>
        /// Sector garrison divisor while operational and stationary; values zero or one have no effect.
        /// </summary>
        public int SectorSuppression
        {
            get => _sectorSuppression.GetValueOrDefault();
            set
            {
                _sectorSuppression = Math.Max(value, 0);
                UpdateSuppressionSector();
            }
        }

        [PersistableMember(Name = nameof(DetectionRating))]
        private int _detectionRating;

        [PersistableIgnore]
        public int MaxHullStrength
        {
            get => _maxHullStrength;
            set => _maxHullStrength = value;
        }

        [PersistableIgnore]
        public int CurrentHullStrength
        {
            get => _currentHullStrength;
            set => _currentHullStrength = value;
        }

        [PersistableIgnore]
        public int NextRepairTick
        {
            get => _nextRepairTick;
            set => _nextRepairTick = value;
        }

        [PersistableIgnore]
        public int RepairIntervalTicks
        {
            get => _repairIntervalTicks;
            set => _repairIntervalTicks = value;
        }

        [PersistableIgnore]
        public int DamageControl
        {
            get => _damageControl;
            set => _damageControl = value;
        }

        [PersistableIgnore]
        public int MaxShieldStrength
        {
            get => ShieldGenerator?.Capacity ?? _maxShieldStrength;
            set
            {
                if (ShieldGenerator != null)
                    ShieldGenerator.Capacity = value;
                _maxShieldStrength = value;
            }
        }

        [PersistableIgnore]
        public int ShieldRechargeRate
        {
            get => ShieldGenerator?.RechargeRate ?? _shieldRechargeRate;
            set
            {
                if (ShieldGenerator != null)
                    ShieldGenerator.RechargeRate = value;
                _shieldRechargeRate = value;
            }
        }

        [PersistableIgnore]
        public int Hyperdrive
        {
            get => GetHyperdriveRating(0);
            set
            {
                if (_hyperdrives.Length > 0 && _hyperdrives[0] != null)
                    _hyperdrives[0].Rating = value;
                else if (value != 0 || _hyperdrives.Length > 0)
                    SetHyperdrive(0, new Hyperdrive { Rating = value });
            }
        }

        /// <summary>
        /// Gets or sets the number of hyperdrive systems that can be disabled in combat.
        /// An omitted legacy count uses the installed drives.
        /// </summary>
        [PersistableIgnore]
        public int HyperdriveCount
        {
            get => _hyperdriveCount ?? _hyperdrives.Count(drive => drive is { Rating: > 0 });
            set => _hyperdriveCount = value;
        }

        [PersistableIgnore]
        public int SublightSpeed
        {
            get => Engine?.SublightSpeed ?? _sublightSpeed;
            set
            {
                if (Engine != null)
                    Engine.SublightSpeed = value;
                _sublightSpeed = value;
            }
        }

        [PersistableIgnore]
        public int Maneuverability
        {
            get => Engine?.Maneuverability ?? _maneuverability;
            set
            {
                if (Engine != null)
                    Engine.Maneuverability = value;
                _maneuverability = value;
            }
        }

        [PersistableIgnore]
        public int StarfighterCapacity
        {
            get => StarfighterBay?.Capacity ?? _starfighterCapacity;
            set
            {
                if (StarfighterBay != null)
                    StarfighterBay.Capacity = value;
                _starfighterCapacity = value;
            }
        }

        [PersistableIgnore]
        public int RegimentCapacity
        {
            get => _regimentCapacity;
            set => _regimentCapacity = value;
        }

        [PersistableIgnore]
        public List<CapitalShipRole> Roles
        {
            get => _roles;
            set => _roles = value;
        }

        [PersistableIgnore]
        public int WeaponRecharge
        {
            get => _weaponRecharge;
            set => _weaponRecharge = value;
        }

        [PersistableIgnore]
        public int Bombardment
        {
            get => _bombardment;
            set => _bombardment = value;
        }

        [PersistableIgnore]
        public int TractorBeamPower
        {
            get => _tractorBeamPower;
            set => _tractorBeamPower = value;
        }

        [PersistableIgnore]
        public int TractorBeamnRange
        {
            get => _tractorBeamRange;
            set => _tractorBeamRange = value;
        }

        [PersistableIgnore]
        public bool HasGravityWell
        {
            get => GravityWellGenerator != null || _hasGravityWell;
            set
            {
                if (!value)
                    GravityWellGenerator = null;
                _hasGravityWell = value;
            }
        }

        [PersistableIgnore]
        public int DetectionRating
        {
            get => _detectionRating;
            set => _detectionRating = value;
        }

        // Owner Info.
        public string InitialParentInstanceID { get; set; }

        /// <summary>
        /// Adds the legacy catalog drive when older ship data omitted its rating.
        /// </summary>
        /// <param name="rating">The fallback rating from the legacy tactical catalog.</param>
        internal void ApplyLegacyDriveRating(int rating)
        {
            if (!HasAuthoredHyperdrives && rating != 0)
                SetHyperdrive(1, new Hyperdrive { Rating = rating });
        }

        /// <summary>
        /// Reads a drive rating in ship order.
        /// </summary>
        /// <param name="index">The drive index.</param>
        /// <returns>The rating, or zero when the slot is empty.</returns>
        internal int GetHyperdriveRating(int index) =>
            index < _hyperdrives.Length ? _hyperdrives[index]?.Rating ?? 0 : 0;

        /// <summary>
        /// Reads a rating from the legacy flat ship XML.
        /// </summary>
        /// <param name="index">The legacy drive index.</param>
        /// <param name="rating">The serialized rating, when supplied.</param>
        private void ReadLegacyDriveRating(int index, int? rating)
        {
            if (
                !_hasAuthoredHyperdrives
                && (_legacyDriveComponents & (1 << index)) == 0
                && rating.HasValue
            )
                SetHyperdrive(index, new Hyperdrive { Rating = rating.Value });
        }

        /// <summary>
        /// Reads a component from older ship XML with separate drive elements.
        /// </summary>
        /// <param name="index">The legacy drive index.</param>
        /// <param name="drive">The serialized drive.</param>
        private void ReadLegacyDriveComponent(int index, Hyperdrive drive)
        {
            if (_hasAuthoredHyperdrives || drive == null)
                return;
            _legacyDriveComponents |= 1 << index;
            SetHyperdrive(index, drive);
        }

        /// <summary>
        /// Stores a drive without changing the order of existing equipment.
        /// </summary>
        /// <param name="index">The drive index.</param>
        /// <param name="drive">The installed drive.</param>
        private void SetHyperdrive(int index, Hyperdrive drive)
        {
            int previousLength = _hyperdrives.Length;
            if (index >= previousLength)
            {
                Array.Resize(ref _hyperdrives, index + 1);
                for (int slot = previousLength; slot < index; slot++)
                    _hyperdrives[slot] = new Hyperdrive();
            }
            _hyperdrives[index] = drive;
        }

        /// <summary>
        /// Maps a campaign weapon category to its hardpoint weapon type.
        /// </summary>
        /// <param name="weapon">The weapon type being read or translated.</param>
        /// <returns>The equivalent type used by the installed mounts.</returns>
        internal static HardpointWeaponType GetHardpointWeaponType(PrimaryWeaponType weapon) =>
            weapon switch
            {
                PrimaryWeaponType.LaserCannon => HardpointWeaponType.LaserCannon,
                PrimaryWeaponType.IonCannon => HardpointWeaponType.IonCannon,
                PrimaryWeaponType.Turbolaser => HardpointWeaponType.Turbolaser,
                _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
            };

        /// <summary>
        /// Maps a legacy ship weapon arc into the battery's arc.
        /// </summary>
        /// <param name="arc">The requested ship-relative weapon arc.</param>
        /// <returns>The corresponding ship-relative arc.</returns>
        internal static TacticalWeaponArc GetBatteryArc(PrimaryWeaponArc arc) =>
            arc switch
            {
                PrimaryWeaponArc.Fore => TacticalWeaponArc.Forward,
                PrimaryWeaponArc.Starboard => TacticalWeaponArc.Starboard,
                PrimaryWeaponArc.Aft => TacticalWeaponArc.Aft,
                PrimaryWeaponArc.Port => TacticalWeaponArc.Port,
                _ => throw new ArgumentOutOfRangeException(nameof(arc)),
            };

        /// <summary>
        /// Installs any missing equipment from legacy flat ship ratings.
        /// </summary>
        internal void InstallLegacyEquipment()
        {
            Engine ??= new Engine
            {
                SublightSpeed = _sublightSpeed,
                Maneuverability = _maneuverability,
            };
            ShieldGenerator ??= new ShieldGenerator
            {
                Capacity = _maxShieldStrength,
                RechargeRate = _shieldRechargeRate,
            };
            StarfighterBay ??= new StarfighterBay { Capacity = _starfighterCapacity };
            if (_hasGravityWell)
                GravityWellGenerator ??= new GravityWellGenerator();
        }

        /// <summary>
        /// Reads the strength of the installed weapons.
        /// </summary>
        /// <param name="weapon">The weapon type being read or translated.</param>
        /// <param name="arc">The requested ship-relative weapon arc.</param>
        /// <returns>The weapon strength in one arc or across all arcs.</returns>
        public int GetPrimaryWeaponStrength(PrimaryWeaponType weapon, PrimaryWeaponArc arc)
        {
            if (HardpointGroups is not { Count: > 0 })
                return 0;

            TacticalWeaponArc batteryArc = GetBatteryArc(arc);
            HardpointWeaponType mountWeapon = GetHardpointWeaponType(weapon);
            int strength = 0;
            foreach (HardpointGroup group in HardpointGroups)
                if (group.Arc == batteryArc)
                    strength += group.GetStrength(mountWeapon);
            return strength;
        }

        /// <summary>
        /// Reads the maximum installed range for a primary weapon.
        /// </summary>
        /// <param name="weapon">The weapon type being read or translated.</param>
        /// <returns>The stored range in campaign units.</returns>
        internal int GetPrimaryWeaponRange(PrimaryWeaponType weapon)
        {
            if (HardpointGroups is not { Count: > 0 })
                return 0;

            HardpointWeaponType mountWeapon = GetHardpointWeaponType(weapon);
            int range = 0;
            foreach (HardpointGroup group in HardpointGroups)
                range = Math.Max(range, (int)group.GetRange(mountWeapon));
            return range;
        }

        /// <summary>
        /// Returns the ship's surviving installed hyperdrive rating.
        /// </summary>
        /// <returns>The rating of the first usable drive, or zero when all drives are unavailable.</returns>
        public int GetOperationalHyperdrive()
        {
            for (int index = 0; index < _hyperdrives.Length; index++)
            {
                int rating = GetHyperdriveRating(index);
                if (rating > 0 && (SystemDamage?.GetHyperdriveDamage(index) ?? 0) == 0)
                    return rating;
            }
            return 0;
        }

        /// <summary>
        /// Counts the drives that can still support automatic combat withdrawal.
        /// </summary>
        /// <returns>The number of functioning drives up to the legacy auto-resolve limit.</returns>
        internal int GetOperationalHyperdriveCount()
        {
            int installed = 0;
            int working = 0;
            for (int index = 0; index < _hyperdrives.Length; index++)
            {
                if (GetHyperdriveRating(index) <= 0)
                    continue;
                installed++;
                if ((SystemDamage?.GetHyperdriveDamage(index) ?? 0) == 0)
                    working++;
            }

            // Older auto-resolve data can specify systems without individual drive ratings.
            int legacyLimit = Math.Max(_hyperdriveCount ?? installed, 0);
            int unrated = Math.Max(legacyLimit - installed, 0);
            return Math.Min(legacyLimit, working + unrated);
        }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public CapitalShip() { }

        /// <summary>
        /// Updates sector membership after this ship or an ancestor is reparented.
        /// </summary>
        protected override void OnHierarchyChanged()
        {
            UpdateSuppressionSector();
            base.OnHierarchyChanged();
        }

        /// <summary>
        /// Supplies content defaults only when a retained unit predates the suppression property.
        /// </summary>
        /// <param name="suppression">The divisor authored on the unit template.</param>
        internal void ApplySectorSuppressionDefault(int suppression)
        {
            if (!_sectorSuppression.HasValue)
                SectorSuppression = suppression;
        }

        /// <summary>
        /// Maintains suppression membership without scanning sector descendants.
        /// </summary>
        private void UpdateSuppressionSector()
        {
            PlanetSector sector = SectorSuppression > 1 ? GetParentOfType<PlanetSector>() : null;
            if (_suppressionSector == sector)
                return;
            _suppressionSector?.UnregisterSuppressionSource(this);
            _suppressionSector = sector;
            sector?.RegisterSuppressionSource(this);
        }

        /// <summary>
        /// Creates an empty capital-ship copy.
        /// </summary>
        /// <returns>The created node copy.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new CapitalShip();

        /// <summary>
        /// Copies capital-ship state into an empty destination.
        /// </summary>
        /// <param name="destination">The destination.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            CapitalShip copy = (CapitalShip)destination;
            ((IEncyclopediaSource)this).CopyEncyclopediaStateTo(copy);
            copy.BattleResultImagePath = BattleResultImagePath;
            copy.BattleResultInTransitImagePath = BattleResultInTransitImagePath;
            copy.BattleResultDamagedImagePath = BattleResultDamagedImagePath;
            copy.BackgroundImagePath = BackgroundImagePath;
            copy.ShipNamePoolID = ShipNamePoolID;
            copy._hasAssignedName = _hasAssignedName;
            copy.ProducerOwnerID = ProducerOwnerID;
            copy.ProducerPlanetID = ProducerPlanetID;
            copy.ManufacturingQueueSequence = ManufacturingQueueSequence;
            copy.ConstructionCost = ConstructionCost;
            copy.MaintenanceCost = MaintenanceCost;
            copy.BaseBuildSpeed = BaseBuildSpeed;
            copy.ManufacturingFactionInstanceIDs =
                ManufacturingFactionInstanceIDs == null
                    ? null
                    : new List<string>(ManufacturingFactionInstanceIDs);
            copy.ResearchOrder = ResearchOrder;
            copy.ResearchDifficulty = ResearchDifficulty;
            copy.UprisingDefense = UprisingDefense;
            copy.MaxHullStrength = MaxHullStrength;
            copy.CurrentHullStrength = CurrentHullStrength;
            copy.NextRepairTick = NextRepairTick;
            copy.RepairIntervalTicks = RepairIntervalTicks;
            copy.DamageControl = DamageControl;
            copy.MaxShieldStrength = MaxShieldStrength;
            copy.ShieldRechargeRate = ShieldRechargeRate;
            copy._hyperdriveCount = _hyperdriveCount;
            copy.PhysicalGeometry = PhysicalGeometry?.CreateCopy();
            copy.SystemDamage = SystemDamage?.CreateCopy() ?? new CapitalShipSystemDamage();
            copy.SublightSpeed = SublightSpeed;
            copy.Maneuverability = Maneuverability;
            copy.StarfighterCapacity = StarfighterCapacity;
            copy.RegimentCapacity = RegimentCapacity;
            copy.Roles = new List<CapitalShipRole>(Roles);
            copy.WeaponRecharge = WeaponRecharge;
            copy.Bombardment = Bombardment;
            copy.ManufacturingProgress = ManufacturingProgress;
            copy.ManufacturingStatus = ManufacturingStatus;
            copy.RefinedMaterialProgress = RefinedMaterialProgress;
            copy.ProductionCapacity = ProductionCapacity;
            copy.ProductionCapacityUsed = ProductionCapacityUsed;
            copy.KdyPool = KdyPool;
            copy.LnrPool = LnrPool;
            copy.Movement = Movement?.CreateCopy();
            copy.TractorBeamPower = TractorBeamPower;
            copy.TractorBeamnRange = TractorBeamnRange;
            copy.HasGravityWell = HasGravityWell;
            copy.CanDestroyPlanets = CanDestroyPlanets;
            copy.SectorSuppression = SectorSuppression;
            copy.DetectionRating = DetectionRating;
            copy.Engine = (Engine)Engine?.CreateCopy();
            copy._hyperdrives = Array.ConvertAll(
                _hyperdrives,
                drive => (Hyperdrive)drive?.CreateCopy()
            );
            copy._hasAuthoredHyperdrives = _hasAuthoredHyperdrives;
            copy.ShieldGenerator = (ShieldGenerator)ShieldGenerator?.CreateCopy();
            copy.StarfighterBay = (StarfighterBay)StarfighterBay?.CreateCopy();
            copy.GravityWellGenerator = (GravityWellGenerator)GravityWellGenerator?.CreateCopy();
            copy._hardpointGroups = _hardpointGroups.ConvertAll(group => group.CreateCopy());
            copy.InitialParentInstanceID = InitialParentInstanceID;
        }

        /// <summary>
        /// Replaces the ship's display name and prevents automatic naming from replacing it.
        /// </summary>
        /// <param name="displayName">The name to assign.</param>
        public void AssignName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A ship name is required.", nameof(displayName));

            DisplayName = displayName;
            _hasAssignedName = true;
        }

        /// <summary>
        /// Replaces the ship's child collections while constructing a detached projection.
        /// </summary>
        /// <param name="officers">The officers to retain in the projection.</param>
        /// <param name="regiments">The regiments to retain in the projection.</param>
        /// <param name="specialForces">The special-forces units to retain in the projection.</param>
        /// <param name="starfighters">The starfighters to retain in the projection.</param>
        internal void SetChildren(
            IEnumerable<Officer> officers,
            IEnumerable<Regiment> regiments,
            IEnumerable<SpecialForces> specialForces,
            IEnumerable<Starfighter> starfighters
        )
        {
            _officers = officers?.ToList() ?? new List<Officer>();
            _regiments = regiments?.ToList() ?? new List<Regiment>();
            _specialForces = specialForces?.ToList() ?? new List<SpecialForces>();
            _starfighters = starfighters?.ToList() ?? new List<Starfighter>();
        }

        /// <summary>
        /// Returns the maximum number of starfighters this ship can carry.
        /// </summary>
        /// <returns>The starfighter capacity of this ship.</returns>
        public int GetStarfighterCapacity()
        {
            return StarfighterCapacity;
        }

        /// <summary>
        /// Returns the number of starfighters currently assigned to this ship.
        /// </summary>
        /// <returns>The count of starfighters currently on board.</returns>
        public int GetCurrentStarfighterCount()
        {
            return _starfighters.Count;
        }

        /// <summary>
        /// Returns the maximum number of regiments this ship can carry.
        /// </summary>
        /// <returns>The regiment capacity of this ship.</returns>
        public int GetRegimentCapacity()
        {
            return RegimentCapacity;
        }

        /// <summary>
        /// Returns the ship roles used by fleet and production planning.
        /// </summary>
        /// <returns>The roles assigned to this ship type.</returns>
        public IReadOnlyList<CapitalShipRole> GetRoles()
        {
            return Roles;
        }

        /// <summary>
        /// Returns whether this ship has the requested role.
        /// </summary>
        /// <param name="role">The role to inspect.</param>
        /// <returns>True if this ship has the requested role.</returns>
        public bool HasRole(CapitalShipRole role)
        {
            return GetRoles().Contains(role);
        }

        /// <summary>
        /// Returns the ship's total primary weapon strength.
        /// </summary>
        /// <returns>The summed primary weapon strength.</returns>
        public int GetPrimaryWeaponStrength() =>
            PrimaryWeaponTypes.Sum(weapon => GetPrimaryWeaponStrength(weapon));

        /// <summary>
        /// Reads the strength of the installed weapons.
        /// </summary>
        /// <param name="weapon">The weapon type being read or translated.</param>
        /// <returns>The weapon strength in one arc or across all arcs.</returns>
        internal int GetPrimaryWeaponStrength(PrimaryWeaponType weapon)
        {
            int strength = 0;
            foreach (PrimaryWeaponArc arc in PrimaryWeaponArcs)
                strength += GetPrimaryWeaponStrength(weapon, arc);
            return strength;
        }

        /// <summary>
        /// Returns active combat value adjusted for current hull condition.
        /// </summary>
        /// <returns>The ship's active combat value, or zero when unavailable.</returns>
        public int GetCombatValue()
        {
            if (ManufacturingStatus != ManufacturingStatus.Complete || Movement != null)
                return 0;

            if (MaxHullStrength > 0 && CurrentHullStrength <= 0)
                return 0;

            return CalculateCombatValue(CurrentHullStrength);
        }

        /// <summary>
        /// Returns combat value at full manufactured durability.
        /// </summary>
        /// <returns>The projected combat value.</returns>
        internal int GetProjectedCombatValue()
        {
            if (
                ManufacturingStatus == ManufacturingStatus.Complete
                && MaxHullStrength > 0
                && CurrentHullStrength <= 0
            )
                return 0;

            int hullStrength =
                ManufacturingStatus == ManufacturingStatus.Complete
                    ? CurrentHullStrength
                    : MaxHullStrength;
            return CalculateCombatValue(hullStrength);
        }

        /// <summary>
        /// Returns combat value at a supplied hull strength without changing the unit.
        /// </summary>
        /// <param name="hullStrength">The remaining hull strength used for the estimate.</param>
        /// <returns>The calculated value.</returns>
        public int CalculateCombatValue(int hullStrength)
        {
            if (MaxHullStrength > 0 && hullStrength <= 0)
                return 0;

            int attackStrength = GetPrimaryWeaponStrength();
            long durability = Math.Max(0L, hullStrength) + Math.Max(0L, MaxShieldStrength);
            if (attackStrength <= 0 || durability <= 0)
                return attackStrength;

            double combatValue = Math.Sqrt(attackStrength * (double)durability);
            return combatValue >= int.MaxValue ? int.MaxValue : (int)combatValue;
        }

        /// <summary>
        /// Returns the number of regiments currently assigned to this ship.
        /// </summary>
        /// <returns>The count of regiments currently on board.</returns>
        public int GetCurrentRegimentCount()
        {
            return _regiments.Count;
        }

        /// <summary>
        /// Returns the number of additional starfighters this ship can carry.
        /// </summary>
        /// <returns>Remaining starfighter berths (capacity minus current count). Zero if full.</returns>
        public int GetExcessStarfighterCapacity()
        {
            return StarfighterCapacity - _starfighters.Count;
        }

        /// <summary>
        /// Returns the number of additional regiments this ship can carry.
        /// </summary>
        /// <returns>Remaining regiment berths (capacity minus current count). Zero if full.</returns>
        public int GetExcessRegimentCapacity()
        {
            return RegimentCapacity - _regiments.Count;
        }

        /// <summary>
        /// Adds a starfighter to the capital ship.
        /// </summary>
        /// <param name="starfighter">The starfighter to add.</param>
        /// <exception cref="InvalidOperationException">Thrown when adding the starfighter would exceed the capacity.</exception>
        public void AddStarfighter(Starfighter starfighter)
        {
            if (_starfighters.Count >= StarfighterCapacity)
            {
                throw new InvalidOperationException(
                    $"Adding starfighters to \"{this.GetDisplayName()}\" would exceed its capacity."
                );
            }
            _starfighters.Add(starfighter);
        }

        /// <summary>
        /// Checks whether the ship can receive squadrons after its planned departures.
        /// </summary>
        /// <param name="arrivals">The number of squadrons arriving.</param>
        /// <param name="departures">The number of currently hosted squadrons leaving.</param>
        /// <returns>Whether the ship can accept the new squadrons.</returns>
        internal bool CanAcceptStarfighters(int arrivals, int departures = 0) =>
            ManufacturingStatus == ManufacturingStatus.Complete
            && arrivals <= GetExcessStarfighterCapacity() + departures;

        /// <summary>
        /// Adds a regiment to the capital ship.
        /// </summary>
        /// <param name="regiment">The regiment to add.</param>
        /// <exception cref="InvalidOperationException">Thrown when adding the regiment would exceed the capacity limit.</exception>
        public void AddRegiment(Regiment regiment)
        {
            if (_regiments.Count >= RegimentCapacity)
            {
                throw new InvalidOperationException(
                    $"Adding regiments to \"{this.GetDisplayName()}\" would exceed its capacity."
                );
            }
            _regiments.Add(regiment);
        }

        /// <summary>
        /// Adds an officer to the capital ship.
        /// </summary>
        /// <param name="officer">The officer to add.</param>
        /// <exception cref="SceneAccessException">Thrown when the child does not share OwnerInstanceID with parent.</exception>
        public void AddOfficer(Officer officer)
        {
            if (!officer.IsCaptured && this.OwnerInstanceID != officer.OwnerInstanceID)
            {
                throw new SceneAccessException(officer, this);
            }

            _officers.Add(officer);
        }

        /// <summary>
        /// Adds special forces to this ship.
        /// </summary>
        /// <param name="specialForces">The special forces unit to add.</param>
        public void AddSpecialForces(SpecialForces specialForces)
        {
            if (this.OwnerInstanceID != specialForces.OwnerInstanceID)
            {
                throw new SceneAccessException(specialForces, this);
            }

            _specialForces.Add(specialForces);
        }

        /// <summary>
        /// Returns true if this ship can accept the child: Starfighters and Regiments require spare
        /// capacity; Officers must share the ship's owner or be captured.
        /// </summary>
        /// <param name="child">The candidate child node.</param>
        /// <returns>True if AddChild would succeed; otherwise false.</returns>
        public override bool CanAcceptChild(ISceneNode child)
        {
            if (ManufacturingStatus != ManufacturingStatus.Complete)
                return false;

            if (child is Starfighter)
                return CanAcceptStarfighters(1);
            if (child is Regiment)
                return GetExcessRegimentCapacity() > 0;
            if (child is Officer officer)
                return officer.IsCaptured || officer.GetOwnerInstanceID() == GetOwnerInstanceID();
            if (child is SpecialForces specialForces)
                return specialForces.GetOwnerInstanceID() == GetOwnerInstanceID();
            return false;
        }

        /// <summary>
        /// Returns whether the ship can accept a child alongside already planned additions.
        /// </summary>
        /// <param name="child">The child.</param>
        /// <param name="plannedChildren">The planned children.</param>
        /// <returns>True when the accept child condition is met; otherwise false.</returns>
        internal override bool CanAcceptChild(
            ISceneNode child,
            IReadOnlyCollection<ISceneNode> plannedChildren
        )
        {
            if (!CanAcceptChild(child))
                return false;

            if (child is Starfighter)
            {
                return CanAcceptStarfighters(
                    1 + plannedChildren.Count(candidate => candidate is Starfighter)
                );
            }

            if (child is Regiment)
            {
                return plannedChildren.Count(candidate => candidate is Regiment)
                    < GetExcessRegimentCapacity();
            }

            return true;
        }

        /// <summary>
        /// Adds a child to the capital ship.
        /// </summary>
        /// <param name="child">The child to add.</param>
        /// <exception cref="SceneAccessException">Thrown when the child does not share OwnerInstanceID with parent.</exception>
        public override void AddChild(ISceneNode child)
        {
            if (child is Starfighter starfighter)
            {
                AddStarfighter(starfighter);
            }
            else if (child is Regiment regiment)
            {
                AddRegiment(regiment);
            }
            else if (child is Officer officer)
            {
                AddOfficer(officer);
            }
            else if (child is SpecialForces specialForces)
            {
                AddSpecialForces(specialForces);
            }
        }

        /// <summary>
        /// Removes a child from the capital ship.
        /// </summary>
        /// <param name="child">The child to remove.</param>
        public override void RemoveChild(ISceneNode child)
        {
            if (child is Starfighter starfighter)
            {
                _starfighters.Remove(starfighter);
            }
            else if (child is Regiment regiment)
            {
                _regiments.Remove(regiment);
            }
            else if (child is Officer officer)
            {
                _officers.Remove(officer);
            }
            else if (child is SpecialForces specialForces)
            {
                _specialForces.Remove(specialForces);
            }
        }

        /// <summary>
        /// Returns true if this ship has hull damage that can be repaired.
        /// </summary>
        /// <returns>True if current hull strength is below maximum hull strength.</returns>
        public bool IsDamaged() => CurrentHullStrength < MaxHullStrength;

        /// <summary>
        /// Repairs hull damage by the specified amount, capped at MaxHullStrength.
        /// </summary>
        /// <param name="amount">Hull points to restore.</param>
        public void RepairHull(int amount)
        {
            CurrentHullStrength = Math.Min(MaxHullStrength, CurrentHullStrength + amount);
        }

        /// <summary>
        /// Schedules the next hull-repair pulse.
        /// </summary>
        /// <param name="currentTick">The tick from which to measure the interval.</param>
        /// <param name="intervalTicks">The positive number of ticks before the next pulse.</param>
        public void ScheduleRepair(int currentTick, int intervalTicks)
        {
            int clampedInterval = Math.Max(1, intervalTicks);
            NextRepairTick = checked(currentTick + clampedInterval);
            RepairIntervalTicks = clampedInterval;
        }

        /// <summary>
        /// Clears the hull-repair timer.
        /// </summary>
        public void ClearRepairSchedule()
        {
            NextRepairTick = 0;
            RepairIntervalTicks = 0;
        }

        /// <summary>
        /// Returns the manufacturing type of this ship.
        /// </summary>
        /// <returns>The ship manufacturing type.</returns>
        public ManufacturingType GetManufacturingType()
        {
            return ManufacturingType.Ship;
        }

        /// <summary>
        /// Returns whether this ship can start movement.
        /// </summary>
        /// <returns>True if this ship is not currently moving.</returns>
        public bool IsMovable()
        {
            return Movement == null;
        }

        /// <summary>
        /// Collects direct children without allocating a raw traversal iterator.
        /// </summary>
        /// <typeparam name="T">The child type retained in the snapshot.</typeparam>
        /// <param name="collector">The current query's validation and collection state.</param>
        protected override void CollectDirectChildren<T>(ref DirectChildCollector<T> collector)
        {
            if (GetType() != typeof(CapitalShip))
            {
                base.CollectDirectChildren(ref collector);
                return;
            }

            List<Officer> officers = _officers;
            for (int index = 0; index < officers.Count; index++)
                collector.Add(officers[index]);
            List<Starfighter> starfighters = _starfighters;
            for (int index = 0; index < starfighters.Count; index++)
                collector.Add(starfighters[index]);
            List<Regiment> regiments = _regiments;
            for (int index = 0; index < regiments.Count; index++)
                collector.Add(regiments[index]);
            List<SpecialForces> specialForces = _specialForces;
            for (int index = 0; index < specialForces.Count; index++)
                collector.Add(specialForces[index]);
        }

        /// <summary>
        /// Returns the ship's carried units and officers.
        /// </summary>
        /// <returns>The children carried by this ship.</returns>
        protected override IEnumerable<ISceneNode> EnumerateChildren()
        {
            List<Officer> officers = _officers;
            for (int index = 0; index < officers.Count; index++)
                yield return officers[index];
            List<Starfighter> starfighters = _starfighters;
            for (int index = 0; index < starfighters.Count; index++)
                yield return starfighters[index];
            List<Regiment> regiments = _regiments;
            for (int index = 0; index < regiments.Count; index++)
                yield return regiments[index];
            List<SpecialForces> specialForces = _specialForces;
            for (int index = 0; index < specialForces.Count; index++)
                yield return specialForces[index];
        }
    }
}
