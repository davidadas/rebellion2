using Rebellion.SceneGraph;

namespace Rebellion.Game
{
    /// <summary>
    /// Represents a regiment that can be stationed on a planet or capital ship.
    /// </summary>
    public class Regiment : LeafNode, IManufacturable, IMovable
    {
        // Construction Info
        public int ConstructionCost { get; set; }
        public int MaintenanceCost { get; set; }
        public int BaseBuildSpeed { get; set; }
        public int ResearchOrder { get; set; }
        public int ResearchDifficulty { get; set; }

        // Regiment Info
        public int AttackRating { get; set; }
        public int DefenseRating { get; set; }
        public int DetectionRating { get; set; }
        public int BombardmentDefense { get; set; }

        // Status Info
        public string ProducerOwnerID { get; set; }
        public string ProducerPlanetID { get; set; }
        public int ManufacturingProgress { get; set; } = 0;
        public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;

        // Movement Info
        public MovementState Movement { get; set; }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public Regiment() { }

        /// <summary>
        /// Returns the manufacturing type for this unit.
        /// </summary>
        /// <returns>The manufacturing type.</returns>
        public ManufacturingType GetManufacturingType()
        {
            return ManufacturingType.Troop;
        }

        /// <summary>
        /// Returns whether the regiment can be ordered to move.
        /// </summary>
        /// <returns>True if the regiment has no active movement state; otherwise, false.</returns>
        public bool IsMovable()
        {
            return Movement == null;
        }
    }
}
