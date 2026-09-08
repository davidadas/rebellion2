using System;
using System.Collections.Generic;
using Rebellion.Game.Movement;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Represents the different types of buildings available in the game.
    /// </summary>
    public enum BuildingType
    {
        None,
        Mine,
        Refinery,
        Shipyard,
        TrainingFacility,
        ConstructionFacility,
        Defense,
        Weapon,
        Headquarters,
    }

    /// <summary>
    /// Defines how a planetary defense weapon applies damage after depleting ship shields.
    /// </summary>
    public enum DefenseWeaponEffect
    {
        HullDamage,
        ShieldDamage,
    }

    /// <summary>
    /// Represents a building in the game, implementing both IManufacturable and IMovable interfaces.
    /// </summary>
    public class Building : LeafNode, IManufacturable, IMovable
    {
        // Construction Info.
        public int ConstructionCost { get; set; }
        public int MaintenanceCost { get; set; }
        public int BaseBuildSpeed { get; set; }
        public List<string> ManufacturingFactionInstanceIDs { get; set; }
        public int ResearchOrder { get; set; }
        public int ResearchDifficulty { get; set; }

        // Building Info.
        public BuildingType BuildingType { get; set; }
        public int ProcessRate { get; set; }
        public int Bombardment { get; set; }
        public int WeaponStrength { get; set; }
        public int ShieldStrength { get; set; }
        public int WeaponPower { get; set; }
        public DefenseWeaponEffect DefenseWeaponEffect { get; set; }
        public bool IsDetectionBlocker { get; set; }
        public List<string> ProtectedUnitTypeIDs { get; set; } = new List<string>();
        public int ProductionModifier { get; set; }
        public List<string> Upgrades { get; set; } = new List<string>();

        // Manufacturing Info.
        public string ProducerOwnerID { get; set; }
        public string ProducerPlanetID { get; set; }
        public long ManufacturingQueueSequence { get; set; }
        public int ManufacturingProgress { get; set; } = 0;
        public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;
        public ManufacturingType ProductionType { get; set; }
        public double ProductionCycleProgress { get; set; }
        public int ProductionCycleDuration { get; set; }
        public bool ProductionPointReady { get; set; }
        public bool ProductionInputReserved { get; set; }
        public int ResourceMaintenanceAllocation { get; set; }
        public bool ResourceStartupCyclePending { get; set; } = true;

        // Movement Info.
        public MovementState Movement { get; set; }

        /// <summary>
        /// Default constructor for the Building class.
        /// </summary>
        public Building() { }

        /// <summary>Creates an empty building copy.</summary>
        protected override BaseSceneNode CreateNodeCopy() => new Building();

        /// <summary>Copies building state into an empty destination.</summary>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            Building copy = (Building)destination;
            copy.ConstructionCost = ConstructionCost;
            copy.MaintenanceCost = MaintenanceCost;
            copy.BaseBuildSpeed = BaseBuildSpeed;
            copy.ManufacturingFactionInstanceIDs =
                ManufacturingFactionInstanceIDs == null
                    ? null
                    : new List<string>(ManufacturingFactionInstanceIDs);
            copy.ResearchOrder = ResearchOrder;
            copy.ResearchDifficulty = ResearchDifficulty;
            copy.BuildingType = BuildingType;
            copy.ProcessRate = ProcessRate;
            copy.Bombardment = Bombardment;
            copy.WeaponStrength = WeaponStrength;
            copy.ShieldStrength = ShieldStrength;
            copy.WeaponPower = WeaponPower;
            copy.DefenseWeaponEffect = DefenseWeaponEffect;
            copy.IsDetectionBlocker = IsDetectionBlocker;
            copy.ProtectedUnitTypeIDs =
                ProtectedUnitTypeIDs == null
                    ? new List<string>()
                    : new List<string>(ProtectedUnitTypeIDs);
            copy.ProductionModifier = ProductionModifier;
            copy.Upgrades = Upgrades == null ? new List<string>() : new List<string>(Upgrades);
            copy.ProducerOwnerID = ProducerOwnerID;
            copy.ProducerPlanetID = ProducerPlanetID;
            copy.ManufacturingQueueSequence = ManufacturingQueueSequence;
            copy.ManufacturingProgress = ManufacturingProgress;
            copy.ManufacturingStatus = ManufacturingStatus;
            copy.ProductionType = ProductionType;
            copy.ProductionCycleProgress = ProductionCycleProgress;
            copy.ProductionCycleDuration = ProductionCycleDuration;
            copy.ProductionPointReady = ProductionPointReady;
            copy.ProductionInputReserved = ProductionInputReserved;
            copy.ResourceMaintenanceAllocation = ResourceMaintenanceAllocation;
            copy.ResourceStartupCyclePending = ResourceStartupCyclePending;
            copy.Movement = Movement?.CreateCopy();
        }

        /// <summary>
        /// Returns the building's type.
        /// </summary>
        /// <returns>The building's type.</returns>
        public BuildingType GetBuildingType()
        {
            return BuildingType;
        }

        /// <summary>
        /// Returns the rate at which this building processes resources or units.
        /// </summary>
        /// <returns>The process rate of the building.</returns>
        public int GetProcessRate()
        {
            return ProcessRate;
        }

        /// <summary>
        /// Determines whether this building can be replaced by the specified upgrade.
        /// </summary>
        /// <param name="upgrade">The proposed replacement building.</param>
        /// <returns>True when the replacement is an authored upgrade for this building.</returns>
        public bool CanUpgradeTo(Building upgrade)
        {
            return upgrade != null && Upgrades?.Contains(upgrade.TypeID) == true;
        }

        /// <summary>
        /// Returns whether this building participates in planetary defense.
        /// </summary>
        /// <returns>True for defense and weapon facilities.</returns>
        public bool IsDefenseFacility()
        {
            return BuildingType is BuildingType.Defense or BuildingType.Weapon;
        }

        /// <summary>
        /// Returns whether this building generates shields that protect its planet.
        /// </summary>
        /// <returns>True when the building supplies planetary shield strength.</returns>
        public bool IsPlanetaryShieldGenerator()
        {
            return BuildingType == BuildingType.Defense && ShieldStrength > 0;
        }

        /// <summary>
        /// Returns whether this building protects specific authored unit types.
        /// </summary>
        /// <returns>True when at least one protected unit type is configured.</returns>
        public bool IsUnitShieldGenerator()
        {
            return ProtectedUnitTypeIDs?.Count > 0;
        }

        /// <summary>
        /// Returns whether this building generates either planetary or unit-specific shields.
        /// </summary>
        /// <returns>True when the building provides either form of shield protection.</returns>
        public bool IsShieldGenerator()
        {
            return IsPlanetaryShieldGenerator() || IsUnitShieldGenerator();
        }

        /// <summary>
        /// Returns the type of production this building is capable of.
        /// </summary>
        /// <returns>The production type of the building.</returns>
        public ManufacturingType GetProductionType()
        {
            return ProductionType;
        }

        /// <summary>
        /// Returns this node's manufacturing type.
        /// </summary>
        /// <returns>ManufacturingType.Building</returns>
        public ManufacturingType GetManufacturingType()
        {
            return ManufacturingType.Building;
        }

        /// <summary>
        /// Returns this unit's manufacturing status.
        /// </summary>
        /// <returns>The manufacturing status.</returns>
        public ManufacturingStatus GetManufacturingStatus()
        {
            return ManufacturingStatus;
        }

        /// <summary>
        /// Sets the manufacturing status of the building.
        /// </summary>
        /// <param name="manufacturingStatus">The manufacturing status to set.</param>
        /// <exception cref="InvalidOperationException">Thrown when the requested status would reverse the manufacturing lifecycle.</exception>
        public void SetManufacturingStatus(ManufacturingStatus manufacturingStatus)
        {
            if (
                (
                    ManufacturingStatus == ManufacturingStatus.Delivering
                    && manufacturingStatus == ManufacturingStatus.Building
                )
                || (
                    ManufacturingStatus == ManufacturingStatus.Complete
                    && manufacturingStatus != ManufacturingStatus.Complete
                )
            )
            {
                throw new InvalidOperationException(
                    $"Invalid manufacturing status transition from '{ManufacturingStatus}' to '{manufacturingStatus}'."
                );
            }
            ManufacturingStatus = manufacturingStatus;
        }

        /// <summary>
        /// Returns this building's ability to move.
        /// </summary>
        /// <returns>True if the building is not in transit; otherwise, false.</returns>
        public bool IsMovable()
        {
            return Movement == null;
        }
    }
}
