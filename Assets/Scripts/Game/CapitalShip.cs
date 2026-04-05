using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

namespace Rebellion.Game
{
    public enum PrimaryWeaponType
    {
        Turbolaser,
        IonCannon,
        LaserCannon,
    }

    /// <summary>
    /// Represents a capital ship in the game.
    /// </summary>
    public class CapitalShip : ContainerNode, IManufacturable, IMovable
    {
        // Manufacture Info
        public string ProducerOwnerID { get; set; }
        public string ProducerPlanetID { get; set; }
        public int ConstructionCost { get; set; }
        public int MaintenanceCost { get; set; }
        public int BaseBuildSpeed { get; set; }
        public int RequiredResearchLevel { get; set; }

        // Hull, Shield, and Repair Info
        public int HullStrength;
        public int DamageControl;
        public int MaxShieldStrength;
        public int ShieldRechargeRate;

        // Movement Info
        public int Hyperdrive;
        public int SublightSpeed;
        public int Maneuverability;

        // Capacity Info
        public int StarfighterCapacity;
        public int RegimentCapacity;

        // Unit Info
        public List<Officer> Officers = new List<Officer>();
        public List<Regiment> Regiments = new List<Regiment>();
        public List<Starfighter> Starfighters = new List<Starfighter>();

        // Weapon Info.
        public Dictionary<PrimaryWeaponType, int[]> PrimaryWeapons = new Dictionary<
            PrimaryWeaponType,
            int[]
        >()
        {
            { PrimaryWeaponType.Turbolaser, new int[5] },
            { PrimaryWeaponType.IonCannon, new int[5] },
            { PrimaryWeaponType.LaserCannon, new int[5] },
        };
        public int WeaponRecharge;
        public int Bombardment;

        // Manufacturing Info
        public int ManufacturingProgress { get; set; } = 0;
        public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;

        public int RefinedMaterialProgress { get; set; }
        public int ProductionCapacity { get; set; }
        public int ProductionCapacityUsed { get; set; }
        public int KdyPool { get; set; }
        public int LnrPool { get; set; }

        // Movement Info
        public MovementState Movement { get; set; }

        // Misc Info
        public int TractorBeamPower;
        public int TractorBeamnRange;
        public bool HasGravityWell;
        public int DetectionRating;

        // Owner Info
        public string InitialParentInstanceID { get; set; }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public CapitalShip() { }

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
            return Starfighters.Count;
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
        /// Returns the number of regiments currently assigned to this ship.
        /// </summary>
        /// <returns>The count of regiments currently on board.</returns>
        public int GetCurrentRegimentCount()
        {
            return Regiments.Count;
        }

        /// <summary>
        /// Returns the number of additional starfighters this ship can carry.
        /// </summary>
        /// <returns>Remaining starfighter berths (capacity minus current count). Zero if full.</returns>
        public int GetExcessStarfighterCapacity()
        {
            return StarfighterCapacity - Starfighters.Count;
        }

        /// <summary>
        /// Returns the number of additional regiments this ship can carry.
        /// </summary>
        /// <returns>Remaining regiment berths (capacity minus current count). Zero if full.</returns>
        public int GetExcessRegimentCapacity()
        {
            return RegimentCapacity - Regiments.Count;
        }

        /// <summary>
        /// Adds a starfighter to the capital ship.
        /// </summary>
        /// <param name="starfighter">The starfighter to add</param>
        /// <exception cref="InvalidOperationException">Thrown when adding the starfighter would exceed the capacity.</exception>
        public void AddStarfighter(Starfighter starfighter)
        {
            if (Starfighters.Count >= StarfighterCapacity)
            {
                throw new InvalidOperationException(
                    $"Adding starfighters to \"{this.GetDisplayName()}\" would exceed its capacity."
                );
            }
            Starfighters.Add(starfighter);
        }

        /// <summary>
        /// Adds a regiment to the capital ship.
        /// </summary>
        /// <param name="regiment">The regiment to add.</param>
        /// <exception cref="InvalidOperationException">Thrown when adding the regiment would exceed the capacity limit.</exception>
        public void AddRegiment(Regiment regiment)
        {
            if (Regiments.Count >= RegimentCapacity)
            {
                throw new InvalidOperationException(
                    $"Adding regiments to \"{this.GetDisplayName()}\" would exceed its capacity."
                );
            }
            Regiments.Add(regiment);
        }

        /// <summary>
        /// Adds an officer to the capital ship.
        /// </summary>
        /// <param name="officer"></param>
        /// <exception cref="SceneAccessException">Thrown when the child does not share OwnerInstanceID with parent.</exception>
        public void AddOfficer(Officer officer)
        {
            if (this.OwnerInstanceID != officer.OwnerInstanceID)
            {
                throw new SceneAccessException(officer, this);
            }
            Officers.Add(officer);
        }

        /// <summary>
        /// Adds a child to the capital ship.
        /// </summary>
        /// <param name="child">The child to add</param>
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
        }

        /// <summary>
        /// Adds a child to the capital ship.
        /// </summary>
        /// <param name="child">The child to remove</param>
        public override void RemoveChild(ISceneNode child)
        {
            if (child is Starfighter starfighter)
            {
                Starfighters.Remove(starfighter);
            }
            else if (child is Regiment regiment)
            {
                Regiments.Remove(regiment);
            }
            else if (child is Officer officer)
            {
                Officers.Remove(officer);
            }
        }

        /// <summary>
        /// Returns the manufacturing manufacturing type of the manufacturable.
        /// </summary>
        /// <returns>ManufacturingType.Ship</returns>
        public ManufacturingType GetManufacturingType()
        {
            return ManufacturingType.Ship;
        }

        /// <summary>
        /// The movement status of the capital ship.
        /// </summary>
        /// <returns>True if the capital ship is movable, false otherwise.</returns>
        public bool IsMovable()
        {
            // Movement == null means not moving (can be moved)
            return Movement == null;
        }

        /// <summary>
        /// Retrieves the children of the node.
        /// </summary>
        /// <returns>The children of the node.</returns>
        public override IEnumerable<ISceneNode> GetChildren()
        {
            return Officers
                .Cast<ISceneNode>()
                .Concat(Starfighters.Cast<ISceneNode>())
                .Concat(Regiments.Cast<ISceneNode>());
        }
    }
}
