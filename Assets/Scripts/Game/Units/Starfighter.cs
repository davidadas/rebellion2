using System;
using System.Collections.Generic;
using Rebellion.Game.Movement;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Represents a starfighter squadron that can be stationed on a planet or capital ship.
    /// </summary>
    public class Starfighter : LeafNode, IManufacturable, IMovable
    {
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

        // General Info.
        public int MaxSquadronSize;
        public int CurrentSquadronSize;
        public int DetectionRating;
        public int Bombardment;
        public int ShieldStrength;

        // Maneuverability Info.
        public int Hyperdrive;
        public int SublightSpeed;
        public int Agility;

        // Weapon Info.
        public int LaserCannon;
        public int IonCannon;
        public int Torpedoes;

        // Weapon Range Info.
        public int LaserRange;
        public int IonRange;
        public int TorpedoRange;

        // Manufacturing Info.
        public string ProducerOwnerID { get; set; }
        public string ProducerPlanetID { get; set; }
        public int ManufacturingProgress { get; set; } = 0;
        public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;

        // Movement Info.
        public MovementState Movement { get; set; }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public Starfighter() { }

        /// <summary>Creates an empty starfighter copy.</summary>
        protected override BaseSceneNode CreateNodeCopy() => new Starfighter();

        /// <summary>Copies starfighter state into an empty destination.</summary>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            Starfighter copy = (Starfighter)destination;
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
