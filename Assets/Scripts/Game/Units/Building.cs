using System;
using System.Collections.Generic;
using Rebellion.Game.Encyclopedia;
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
    public class Building : LeafNode, IManufacturable, IMovable, IEncyclopediaSource
    {
        public string EncyclopediaImagePath { get; set; }
        public List<EncyclopediaEntryStat> EncyclopediaStats { get; set; } =
            new List<EncyclopediaEntryStat>();
        public string EncyclopediaDescription { get; set; }

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
        public bool IsDestroyed { get; set; }
        public string UpgradeTargetTypeID { get; set; }
        public string UpgradeTargetDisplayName { get; set; }
        public int UpgradeConstructionCost { get; set; }
        public int UpgradeMaintenanceCost { get; set; }

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

        /// <summary>
        /// Creates an empty building copy.
        /// </summary>
        /// <returns>The created node copy.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new Building();

        /// <summary>
        /// Copies building state into an empty destination.
        /// </summary>
        /// <param name="destination">The destination.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            Building copy = (Building)destination;
            ((IEncyclopediaSource)this).CopyEncyclopediaStateTo(copy);
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
            copy.IsDestroyed = IsDestroyed;
            copy.UpgradeTargetTypeID = UpgradeTargetTypeID;
            copy.UpgradeTargetDisplayName = UpgradeTargetDisplayName;
            copy.UpgradeConstructionCost = UpgradeConstructionCost;
            copy.UpgradeMaintenanceCost = UpgradeMaintenanceCost;
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
            return upgrade != null
                && upgrade.BuildingType == BuildingType
                && upgrade.ProductionType == ProductionType
                && Upgrades?.Contains(upgrade.TypeID) == true;
        }

        /// <summary>
        /// Returns whether this building is being upgraded in place.
        /// </summary>
        /// <returns>True while an authored upgrade is in progress.</returns>
        public bool IsUpgrading()
        {
            return !string.IsNullOrEmpty(UpgradeTargetTypeID)
                && ManufacturingStatus == ManufacturingStatus.Building;
        }

        /// <summary>
        /// Returns the construction cost of the active upgrade or this building.
        /// </summary>
        /// <returns>The effective construction cost.</returns>
        public int GetConstructionCost()
        {
            return IsUpgrading() ? UpgradeConstructionCost : ConstructionCost;
        }

        /// <summary>
        /// Returns the maintenance cost of the active upgrade or this building.
        /// </summary>
        /// <returns>The effective maintenance cost.</returns>
        public int GetMaintenanceCost()
        {
            return IsUpgrading() ? UpgradeMaintenanceCost : MaintenanceCost;
        }

        /// <summary>
        /// Returns the upgrade target name while an upgrade is active.
        /// </summary>
        /// <returns>The effective display name.</returns>
        public override string GetDisplayName()
        {
            return IsUpgrading() && !string.IsNullOrEmpty(UpgradeTargetDisplayName)
                ? UpgradeTargetDisplayName
                : base.GetDisplayName();
        }

        /// <summary>
        /// Starts an authored upgrade while preserving this building's occupied planet slot.
        /// </summary>
        /// <param name="upgrade">The authored upgrade definition.</param>
        /// <exception cref="InvalidOperationException">Thrown when the upgrade is not valid.</exception>
        public void BeginUpgrade(Building upgrade)
        {
            if (!IsOperational() || !CanUpgradeTo(upgrade))
                throw new InvalidOperationException("The building cannot start that upgrade.");

            UpgradeTargetTypeID = upgrade.TypeID;
            UpgradeTargetDisplayName = upgrade.DisplayName;
            UpgradeConstructionCost = upgrade.ConstructionCost;
            UpgradeMaintenanceCost = upgrade.MaintenanceCost;
            ManufacturingStatus = ManufacturingStatus.Building;
            ManufacturingProgress = 0;
            Movement = null;
        }

        /// <summary>
        /// Applies an authored upgrade definition and returns the building to operational service.
        /// </summary>
        /// <param name="upgrade">The authored upgrade definition.</param>
        /// <exception cref="InvalidOperationException">Thrown when the active upgrade does not match.</exception>
        public void CompleteUpgrade(Building upgrade)
        {
            if (
                !IsUpgrading()
                || upgrade == null
                || !string.Equals(upgrade.TypeID, UpgradeTargetTypeID, StringComparison.Ordinal)
            )
                throw new InvalidOperationException("The active building upgrade does not match.");

            ApplyAuthoredDefinition(upgrade);
            ClearUpgradeState();
            ManufacturingStatus = ManufacturingStatus.Complete;
            ManufacturingProgress = 0;
            ManufacturingQueueSequence = 0;
            ProducerOwnerID = null;
            ProducerPlanetID = null;
            ProductionCycleProgress = 0;
            ProductionCycleDuration = 0;
            ProductionPointReady = false;
            ProductionInputReserved = false;
            ResourceMaintenanceAllocation = 0;
            ResourceStartupCyclePending = true;
            Movement = null;
        }

        /// <summary>
        /// Cancels an active upgrade and restores the original building identity.
        /// </summary>
        public void CancelUpgrade()
        {
            if (!IsUpgrading())
                return;

            ClearUpgradeState();
            ManufacturingStatus = ManufacturingStatus.Complete;
            ManufacturingProgress = 0;
            ManufacturingQueueSequence = 0;
            ProducerOwnerID = null;
            ProducerPlanetID = null;
            Movement = null;
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
        /// Returns whether this building is complete, stationary, and not destroyed.
        /// </summary>
        /// <returns>True when the building can participate in gameplay.</returns>
        public bool IsOperational()
        {
            return !IsDestroyed
                && ManufacturingStatus == ManufacturingStatus.Complete
                && Movement == null;
        }

        /// <summary>
        /// Returns whether this building is idle rubble that can be selected for reconstruction.
        /// </summary>
        /// <returns>True when the destroyed building is not currently being rebuilt.</returns>
        public bool IsRubble()
        {
            return IsDestroyed && ManufacturingStatus == ManufacturingStatus.Complete;
        }

        /// <summary>
        /// Returns whether this destroyed building is currently being reconstructed.
        /// </summary>
        /// <returns>True when reconstruction is in progress.</returns>
        public bool IsRebuilding()
        {
            return IsDestroyed && ManufacturingStatus == ManufacturingStatus.Building;
        }

        /// <summary>
        /// Converts this building into non-operational rubble while preserving its planet slot.
        /// </summary>
        public void MarkDestroyed()
        {
            if (IsRubble())
                return;

            IsDestroyed = true;
            ClearUpgradeState();
            ManufacturingStatus = ManufacturingStatus.Complete;
            ManufacturingProgress = 0;
            ManufacturingQueueSequence = 0;
            ProducerOwnerID = null;
            ProducerPlanetID = null;
            ProductionCycleProgress = 0;
            ProductionCycleDuration = 0;
            ProductionPointReady = false;
            ProductionInputReserved = false;
            ResourceMaintenanceAllocation = 0;
            ResourceStartupCyclePending = true;
            Movement = null;
        }

        /// <summary>
        /// Starts rebuilding this destroyed building in place.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the building is not destroyed.</exception>
        public void BeginRebuild()
        {
            if (!IsDestroyed)
                throw new InvalidOperationException("Only a destroyed building can be rebuilt.");

            ManufacturingStatus = ManufacturingStatus.Building;
            ManufacturingProgress = 0;
            Movement = null;
        }

        /// <summary>
        /// Restores this building to operational service after rebuilding finishes.
        /// </summary>
        public void CompleteRebuild()
        {
            IsDestroyed = false;
            ManufacturingStatus = ManufacturingStatus.Complete;
            ManufacturingQueueSequence = 0;
            ProducerOwnerID = null;
            ProducerPlanetID = null;
            ProductionCycleProgress = 0;
            ProductionCycleDuration = 0;
            ProductionPointReady = false;
            ProductionInputReserved = false;
            ResourceMaintenanceAllocation = 0;
            ResourceStartupCyclePending = true;
            Movement = null;
        }

        /// <summary>
        /// Returns an interrupted rebuild to its idle rubble state.
        /// </summary>
        public void CancelRebuild()
        {
            if (!IsDestroyed)
                return;

            ManufacturingStatus = ManufacturingStatus.Complete;
            ManufacturingProgress = 0;
            ManufacturingQueueSequence = 0;
            ProducerOwnerID = null;
            ProducerPlanetID = null;
            ProductionCycleProgress = 0;
            ProductionCycleDuration = 0;
            ProductionPointReady = false;
            ProductionInputReserved = false;
            ResourceMaintenanceAllocation = 0;
            ResourceStartupCyclePending = true;
            Movement = null;
        }

        /// <summary>
        /// Copies authored facility data from an upgrade definition without changing runtime identity.
        /// </summary>
        /// <param name="upgrade">The authored upgrade definition.</param>
        private void ApplyAuthoredDefinition(Building upgrade)
        {
            TypeID = upgrade.TypeID;
            DisplayName = upgrade.DisplayName;
            DisplayStatus = upgrade.DisplayStatus;
            DisplayImagePath = upgrade.DisplayImagePath;
            SmallDisplayImagePath = upgrade.SmallDisplayImagePath;
            MessageImagePath = upgrade.MessageImagePath;
            InTransitImagePath = upgrade.InTransitImagePath;
            InTransitSmallImagePath = upgrade.InTransitSmallImagePath;
            DamagedImagePath = upgrade.DamagedImagePath;
            DamagedSmallImagePath = upgrade.DamagedSmallImagePath;
            CapturedOverlayImagePath = upgrade.CapturedOverlayImagePath;
            InjuredImagePath = upgrade.InjuredImagePath;
            Description = upgrade.Description;
            EncyclopediaImagePath = upgrade.EncyclopediaImagePath;
            EncyclopediaDescription = upgrade.EncyclopediaDescription;
            EncyclopediaStats =
                upgrade.EncyclopediaStats == null
                    ? new List<EncyclopediaEntryStat>()
                    : new List<EncyclopediaEntryStat>(upgrade.EncyclopediaStats);
            ConstructionCost = upgrade.ConstructionCost;
            MaintenanceCost = upgrade.MaintenanceCost;
            BaseBuildSpeed = upgrade.BaseBuildSpeed;
            ManufacturingFactionInstanceIDs =
                upgrade.ManufacturingFactionInstanceIDs == null
                    ? null
                    : new List<string>(upgrade.ManufacturingFactionInstanceIDs);
            ResearchOrder = upgrade.ResearchOrder;
            ResearchDifficulty = upgrade.ResearchDifficulty;
            BuildingType = upgrade.BuildingType;
            ProcessRate = upgrade.ProcessRate;
            Bombardment = upgrade.Bombardment;
            WeaponStrength = upgrade.WeaponStrength;
            ShieldStrength = upgrade.ShieldStrength;
            WeaponPower = upgrade.WeaponPower;
            DefenseWeaponEffect = upgrade.DefenseWeaponEffect;
            IsDetectionBlocker = upgrade.IsDetectionBlocker;
            ProtectedUnitTypeIDs =
                upgrade.ProtectedUnitTypeIDs == null
                    ? new List<string>()
                    : new List<string>(upgrade.ProtectedUnitTypeIDs);
            ProductionModifier = upgrade.ProductionModifier;
            Upgrades =
                upgrade.Upgrades == null ? new List<string>() : new List<string>(upgrade.Upgrades);
            ProductionType = upgrade.ProductionType;
        }

        /// <summary>
        /// Clears persisted state used only while an upgrade is active.
        /// </summary>
        private void ClearUpgradeState()
        {
            UpgradeTargetTypeID = null;
            UpgradeTargetDisplayName = null;
            UpgradeConstructionCost = 0;
            UpgradeMaintenanceCost = 0;
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
