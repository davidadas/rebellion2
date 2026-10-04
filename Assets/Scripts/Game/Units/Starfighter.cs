using System;
using System.Collections.Generic;
using Rebellion.Game.Encyclopedia;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Represents a starfighter squadron that can be stationed on a planet or capital ship.
    /// </summary>
    public class Starfighter : LeafNode, IManufacturable, IMovable, IEncyclopediaSource
    {
        public string EncyclopediaImagePath { get; set; }
        public List<EncyclopediaEntryStat> EncyclopediaStats { get; set; } =
            new List<EncyclopediaEntryStat>();
        public string EncyclopediaDescription { get; set; }
        public string BattleResultImagePath { get; set; }
        public string BattleResultInTransitImagePath { get; set; }
        public string BattleResultDamagedImagePath { get; set; }

        // Construction Info.
        public int ConstructionCost { get; set; }
        public int MaintenanceCost { get; set; }
        public int BaseBuildSpeed { get; set; }
        public List<string> ManufacturingFactionInstanceIDs { get; set; }
        public int ResearchOrder { get; set; }
        public int ResearchDifficulty { get; set; }
        public int UprisingDefense { get; set; }

        // General Info.
        [PersistableMember(Name = nameof(MaxSquadronSize))]
        private int _maxSquadronSize;

        [PersistableMember(Name = nameof(CurrentSquadronSize))]
        private int _currentSquadronSize;

        [PersistableMember(Name = nameof(DetectionRating))]
        private int _detectionRating;

        [PersistableMember(Name = nameof(Bombardment))]
        private int _bombardment;

        [PersistableMember(Name = nameof(ShieldStrength))]
        private int _shieldStrength;

        // Maneuverability Info.
        [PersistableMember(Name = nameof(Hyperdrive))]
        private int _hyperdrive;

        [PersistableMember(Name = nameof(SublightSpeed))]
        private int _sublightSpeed;

        [PersistableMember(Name = nameof(Agility))]
        private int _agility;

        // Weapon Info.
        [PersistableMember(Name = nameof(LaserCannon))]
        private int _laserCannon;

        [PersistableMember(Name = nameof(IonCannon))]
        private int _ionCannon;

        [PersistableMember(Name = nameof(Torpedoes))]
        private int _torpedoes;

        // Weapon Range Info.
        [PersistableMember(Name = nameof(LaserRange))]
        private int _laserRange;

        [PersistableMember(Name = nameof(IonRange))]
        private int _ionRange;

        [PersistableMember(Name = nameof(TorpedoRange))]
        private int _torpedoRange;

        [PersistableIgnore]
        public int MaxSquadronSize
        {
            get => _maxSquadronSize;
            set => _maxSquadronSize = value;
        }

        [PersistableIgnore]
        public int CurrentSquadronSize
        {
            get => _currentSquadronSize;
            set => _currentSquadronSize = value;
        }

        [PersistableIgnore]
        public int DetectionRating
        {
            get => _detectionRating;
            set => _detectionRating = value;
        }

        [PersistableIgnore]
        public int Bombardment
        {
            get => _bombardment;
            set => _bombardment = value;
        }

        [PersistableIgnore]
        public int ShieldStrength
        {
            get => _shieldStrength;
            set => _shieldStrength = value;
        }

        [PersistableIgnore]
        public int Hyperdrive
        {
            get => _hyperdrive;
            set => _hyperdrive = value;
        }

        [PersistableIgnore]
        public int SublightSpeed
        {
            get => _sublightSpeed;
            set => _sublightSpeed = value;
        }

        [PersistableIgnore]
        public int Agility
        {
            get => _agility;
            set => _agility = value;
        }

        [PersistableIgnore]
        public int LaserCannon
        {
            get => _laserCannon;
            set => _laserCannon = value;
        }

        [PersistableIgnore]
        public int IonCannon
        {
            get => _ionCannon;
            set => _ionCannon = value;
        }

        [PersistableIgnore]
        public int Torpedoes
        {
            get => _torpedoes;
            set => _torpedoes = value;
        }

        [PersistableIgnore]
        public int LaserRange
        {
            get => _laserRange;
            set => _laserRange = value;
        }

        [PersistableIgnore]
        public int IonRange
        {
            get => _ionRange;
            set => _ionRange = value;
        }

        [PersistableIgnore]
        public int TorpedoRange
        {
            get => _torpedoRange;
            set => _torpedoRange = value;
        }

        // Manufacturing Info.
        public string ProducerOwnerID { get; set; }
        public string ProducerPlanetID { get; set; }
        public long ManufacturingQueueSequence { get; set; }
        public int ManufacturingProgress { get; set; } = 0;
        public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;

        // Movement Info.
        public MovementState Movement { get; set; }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public Starfighter() { }

        /// <summary>Creates an empty starfighter copy.</summary>
        /// <returns>The created node copy.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new Starfighter();

        /// <summary>Copies starfighter state into an empty destination.</summary>
        /// <param name="destination">The destination.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            Starfighter copy = (Starfighter)destination;
            ((IEncyclopediaSource)this).CopyEncyclopediaStateTo(copy);
            copy.BattleResultImagePath = BattleResultImagePath;
            copy.BattleResultInTransitImagePath = BattleResultInTransitImagePath;
            copy.BattleResultDamagedImagePath = BattleResultDamagedImagePath;
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
            copy.MaxSquadronSize = MaxSquadronSize;
            copy.CurrentSquadronSize = CurrentSquadronSize;
            copy.DetectionRating = DetectionRating;
            copy.Bombardment = Bombardment;
            copy.ShieldStrength = ShieldStrength;
            copy.Hyperdrive = Hyperdrive;
            copy.SublightSpeed = SublightSpeed;
            copy.Agility = Agility;
            copy.LaserCannon = LaserCannon;
            copy.IonCannon = IonCannon;
            copy.Torpedoes = Torpedoes;
            copy.LaserRange = LaserRange;
            copy.IonRange = IonRange;
            copy.TorpedoRange = TorpedoRange;
            copy.ProducerOwnerID = ProducerOwnerID;
            copy.ProducerPlanetID = ProducerPlanetID;
            copy.ManufacturingQueueSequence = ManufacturingQueueSequence;
            copy.ManufacturingProgress = ManufacturingProgress;
            copy.ManufacturingStatus = ManufacturingStatus;
            copy.Movement = Movement?.CreateCopy();
        }

        /// <summary>
        /// Returns true if this squadron has lost fighters that can be replaced.
        /// </summary>
        /// <returns>True if CurrentSquadronSize is below MaxSquadronSize.</returns>
        public bool HasLosses() => CurrentSquadronSize < MaxSquadronSize;

        /// <summary>
        /// Replaces lost fighters by the specified amount, capped at MaxSquadronSize.
        /// </summary>
        /// <param name="amount">Fighters to replace.</param>
        public void ReplaceFighters(int amount)
        {
            CurrentSquadronSize = Math.Min(MaxSquadronSize, CurrentSquadronSize + amount);
        }

        /// <summary>
        /// Returns the combined weapon strength of one fighter in the squadron.
        /// </summary>
        /// <returns>The combined laser, ion cannon, and torpedo strength.</returns>
        public int GetWeaponStrength()
        {
            return LaserCannon + IonCannon + Torpedoes;
        }

        /// <summary>
        /// Returns the combat value of the available squadron.
        /// </summary>
        /// <returns>The squadron's combat value, or zero when it is unavailable.</returns>
        public int GetCombatValue()
        {
            if (ManufacturingStatus != ManufacturingStatus.Complete || Movement != null)
                return 0;

            return CalculateCombatValue(CurrentSquadronSize);
        }

        /// <summary>
        /// Returns combat value using the current size of a completed squadron or the full size of
        /// a squadron still under construction.
        /// </summary>
        /// <returns>The projected squadron combat value.</returns>
        internal int GetProjectedCombatValue()
        {
            int squadronSize =
                ManufacturingStatus == ManufacturingStatus.Complete
                    ? CurrentSquadronSize
                    : MaxSquadronSize;
            return CalculateCombatValue(squadronSize);
        }

        /// <summary>
        /// Returns the combined weapon strength contributed by the specified number of fighters.
        /// </summary>
        /// <param name="squadronSize">The number of fighters contributing weapon strength.</param>
        /// <returns>The squadron combat value.</returns>
        internal int CalculateCombatValue(int squadronSize)
        {
            return GetWeaponStrength() * Math.Max(0, squadronSize);
        }

        /// <summary>
        /// Returns the manufacturing type for this unit.
        /// </summary>
        /// <returns>The manufacturing type.</returns>
        public ManufacturingType GetManufacturingType()
        {
            return ManufacturingType.Ship;
        }

        /// <summary>
        /// Returns whether the starfighter squadron can be ordered to move.
        /// </summary>
        /// <returns>True if the squadron is not currently in transit; otherwise, false.</returns>
        public bool IsMovable()
        {
            return Movement == null;
        }
    }
}
