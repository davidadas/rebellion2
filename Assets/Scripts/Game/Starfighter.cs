using System;
using Rebellion.SceneGraph;

namespace Rebellion.Game
{
    /// <summary>
    /// Represents a starfighter squadron that can be stationed on a planet or capital ship.
    /// </summary>
    public class Starfighter : LeafNode, IManufacturable, IMovable
    {
        // Construction Info
        public int ConstructionCost { get; set; }
        public int MaintenanceCost { get; set; }
        public int BaseBuildSpeed { get; set; }
        public int ResearchOrder { get; set; }
        public int ResearchDifficulty { get; set; }

        // General Info
        public int MaxSquadronSize;
        public int CurrentSquadronSize;
        public int DetectionRating;
        public int Bombardment;
        public int ShieldStrength;

        // Maneuverability Info
        public int Hyperdrive;
        public int SublightSpeed;
        public int Agility;

        // Weapon Info
        public int LaserCannon;
        public int IonCannon;
        public int Torpedoes;

        // Weapon Range Info
        public int LaserRange;
        public int IonRange;
        public int TorpedoRange;

        // Manufacturing Info
        public string ProducerOwnerID { get; set; }
        public string ProducerPlanetID { get; set; }
        public int ManufacturingProgress { get; set; } = 0;
        public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;

        // Movement Info
        public MovementState Movement { get; set; }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public Starfighter() { }

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
