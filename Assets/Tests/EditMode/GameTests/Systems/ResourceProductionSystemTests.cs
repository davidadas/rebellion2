using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class ResourceProductionSystemTests
    {
        private GameRoot _game;
        private ResourceProductionSystem _system;
        private Faction _faction;
        private PlanetSector _planetSector;
        private Planet _planet;
        private int _nextBuildingId;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestContent.Data.GameConfig) { Random = new StubRNG() };
            _faction = new Faction { InstanceID = "FACTION1" };
            _faction.Settings.ResourceProcessingPointsPerFacility = 50;
            _game.GetFactions().Add(_faction);
            Faction secondFaction = new Faction { InstanceID = "FACTION2" };
            secondFaction.Settings.ResourceProcessingPointsPerFacility = 50;
            _game.GetFactions().Add(secondFaction);

            _planetSector = new PlanetSector
            {
                InstanceID = "SECTOR1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(_planetSector, _game.Galaxy);

            _planet = CreateOwnedPlanet("PLANET1");
            _game.AttachNode(_planet, _planetSector);
            _system = new ResourceProductionSystem(_game);
        }

        [Test]
        public void ProcessTick_MineStartupCycle_ProducesAfterStartupDuration()
        {
            Building mine = AddCompleteBuilding(_planet, BuildingType.Mine, processRate: 4);

            _system.ProcessTick();

            Assert.AreEqual(0, _faction.RawMaterialStockpile);
            Assert.IsTrue(mine.ProductionInputReserved);

            _system.ProcessTick();

            Assert.AreEqual(1, _faction.RawMaterialStockpile);
            Assert.IsTrue(mine.ProductionInputReserved);
            Assert.IsFalse(mine.ResourceStartupCyclePending);
        }

        [Test]
        public void ProcessTick_SmugglingRoll_RedirectsCompletedResourceToBeneficiary()
        {
            _planet.PopularSupport = new Dictionary<string, int>
            {
                { "FACTION1", 15 },
                { "FACTION2", 85 },
            };
            Building mine = AddCompleteBuilding(_planet, BuildingType.Mine, processRate: 1);
            mine.ProductionInputReserved = true;
            mine.ProductionCycleDuration = 1;
            mine.ResourceStartupCyclePending = false;
            Faction beneficiary = _game.GetFactionByOwnerInstanceID("FACTION2");

            _system.ProcessTick();

            Assert.AreEqual(0, _faction.RawMaterialStockpile);
            Assert.AreEqual(1, beneficiary.RawMaterialStockpile);
        }

        [Test]
        public void ProcessTick_SmugglingRoll_RedirectsCompletedRefinedResourceToBeneficiary()
        {
            _planet.PopularSupport = new Dictionary<string, int>
            {
                { "FACTION1", 15 },
                { "FACTION2", 85 },
            };
            Building refinery = AddCompleteBuilding(_planet, BuildingType.Refinery, processRate: 1);
            refinery.ProductionInputReserved = true;
            refinery.ProductionCycleDuration = 1;
            refinery.ResourceStartupCyclePending = false;
            Faction beneficiary = _game.GetFactionByOwnerInstanceID("FACTION2");

            _system.ProcessTick();

            Assert.AreEqual(0, _faction.RefinedMaterialStockpile);
            Assert.AreEqual(1, beneficiary.RefinedMaterialStockpile);
        }

        [Test]
        public void ProcessTick_RefineriesWaitingForRawMaterial_AreServicedInRequestOrder()
        {
            Building first = AddCompleteBuilding(_planet, BuildingType.Refinery, processRate: 2);
            Building second = AddCompleteBuilding(_planet, BuildingType.Refinery, processRate: 2);

            _system.ProcessTick();

            CollectionAssert.AreEqual(
                new[] { first.InstanceID, second.InstanceID },
                _faction.PendingRawMaterialFacilityIDs
            );

            _faction.RawMaterialStockpile = 1;
            _system.ProcessTick();

            Assert.AreEqual(1, _faction.RefinedMaterialStockpile);
            CollectionAssert.AreEqual(
                new[] { second.InstanceID, first.InstanceID },
                _faction.PendingRawMaterialFacilityIDs
            );
        }

        [Test]
        public void ProcessTick_PendingProductionFacility_ReceivesAvailableRefinedMaterial()
        {
            Building facility = AddCompleteBuilding(
                _planet,
                BuildingType.ConstructionFacility,
                processRate: 2,
                productionType: ManufacturingType.Building
            );
            Building queuedBuilding = new Building
            {
                InstanceID = "QUEUED_BUILDING",
                OwnerInstanceID = _faction.InstanceID,
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            _game.AttachNode(queuedBuilding, _planet);
            _planet.AddToManufacturingQueue(queuedBuilding);
            _faction.RequestRefinedMaterial(facility);
            _faction.RefinedMaterialStockpile = 1;

            _system.ProcessTick();

            Assert.AreEqual(0, _faction.RefinedMaterialStockpile);
            Assert.IsTrue(facility.ProductionInputReserved);
            Assert.IsEmpty(_faction.PendingRefinedMaterialFacilityIDs);
        }

        [Test]
        public void ProcessTick_PendingProductionFacilityWithoutQueue_DoesNotConsumeMaterial()
        {
            Building facility = AddCompleteBuilding(
                _planet,
                BuildingType.ConstructionFacility,
                processRate: 2,
                productionType: ManufacturingType.Building
            );
            _faction.RequestRefinedMaterial(facility);
            _faction.RefinedMaterialStockpile = 1;

            _system.ProcessTick();

            Assert.AreEqual(1, _faction.RefinedMaterialStockpile);
            Assert.IsFalse(facility.ProductionInputReserved);
            Assert.IsEmpty(_faction.PendingRefinedMaterialFacilityIDs);
        }

        [Test]
        public void ProcessTick_SuspendedRefineryWithPendingRequest_ReservesAvailableRawMaterial()
        {
            Building refinery = AddCompleteBuilding(_planet, BuildingType.Refinery, processRate: 2);
            _faction.RequestRawMaterial(refinery);
            _faction.RawMaterialStockpile = 1;
            _planet.IsInUprising = true;

            _system.ProcessTick();

            Assert.AreEqual(0, _faction.RawMaterialStockpile);
            Assert.IsTrue(refinery.ProductionInputReserved);
            Assert.AreEqual(0, refinery.ProductionCycleProgress);
            Assert.IsEmpty(_faction.PendingRawMaterialFacilityIDs);
        }

        [Test]
        public void ProcessTick_SuspendedProductionFacilityWithPendingRequest_ReservesAvailableRefinedMaterial()
        {
            Building facility = AddCompleteBuilding(
                _planet,
                BuildingType.ConstructionFacility,
                processRate: 2,
                productionType: ManufacturingType.Building
            );
            Building queuedBuilding = new Building
            {
                InstanceID = "QUEUED_BUILDING",
                OwnerInstanceID = _faction.InstanceID,
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            _game.AttachNode(queuedBuilding, _planet);
            _planet.AddToManufacturingQueue(queuedBuilding);
            _faction.RequestRefinedMaterial(facility);
            _faction.RefinedMaterialStockpile = 1;
            _planet.IsInUprising = true;

            _system.ProcessTick();

            Assert.AreEqual(0, _faction.RefinedMaterialStockpile);
            Assert.IsTrue(facility.ProductionInputReserved);
            Assert.AreEqual(0, facility.ProductionCycleProgress);
            Assert.IsEmpty(_faction.PendingRefinedMaterialFacilityIDs);
        }

        [Test]
        public void ProcessTick_CompletedMineCycle_ServicesSuspendedRefineryInSameTick()
        {
            Building mine = AddCompleteBuilding(_planet, BuildingType.Mine, processRate: 1);
            mine.ProductionInputReserved = true;
            mine.ProductionCycleDuration = 1;
            mine.ResourceStartupCyclePending = false;

            Planet suspendedPlanet = CreateOwnedPlanet("PLANET2");
            suspendedPlanet.IsInUprising = true;
            _game.AttachNode(suspendedPlanet, _planetSector);
            Building refinery = AddCompleteBuilding(
                suspendedPlanet,
                BuildingType.Refinery,
                processRate: 2
            );
            _faction.RequestRawMaterial(refinery);

            _system.ProcessTick();

            Assert.AreEqual(0, _faction.RawMaterialStockpile);
            Assert.IsTrue(refinery.ProductionInputReserved);
            Assert.IsEmpty(_faction.PendingRawMaterialFacilityIDs);
            Assert.AreEqual(0, refinery.ProductionCycleProgress);
        }

        [Test]
        public void ProcessTick_CompletedRefineryCycle_ServicesProductionFacilityInSameTick()
        {
            Building refinery = AddCompleteBuilding(_planet, BuildingType.Refinery, processRate: 1);
            refinery.ProductionInputReserved = true;
            refinery.ProductionCycleDuration = 1;
            refinery.ResourceStartupCyclePending = false;

            Building facility = AddCompleteBuilding(
                _planet,
                BuildingType.ConstructionFacility,
                processRate: 2,
                productionType: ManufacturingType.Building
            );
            Building queuedBuilding = new Building
            {
                InstanceID = "QUEUED_BUILDING",
                OwnerInstanceID = _faction.InstanceID,
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            _game.AttachNode(queuedBuilding, _planet);
            _planet.AddToManufacturingQueue(queuedBuilding);
            _faction.RawMaterialStockpile = 1;
            _faction.RequestRefinedMaterial(facility);

            _system.ProcessTick();

            Assert.AreEqual(0, _faction.RefinedMaterialStockpile);
            Assert.IsTrue(facility.ProductionInputReserved);
            Assert.IsEmpty(_faction.PendingRefinedMaterialFacilityIDs);
        }

        [Test]
        public void ProcessTick_LowerPopularSupport_ExtendsResourceCycle()
        {
            Building mine = AddCompleteBuilding(_planet, BuildingType.Mine, processRate: 2);
            mine.ProductionInputReserved = true;
            mine.ResourceStartupCyclePending = false;
            _planet.SetPopularSupport(_faction.InstanceID, 50);

            ProcessTicks(3);

            Assert.AreEqual(0, _faction.RawMaterialStockpile);

            _system.ProcessTick();

            Assert.AreEqual(1, _faction.RawMaterialStockpile);
        }

        [Test]
        public void ProcessTick_MaintenanceAllocation_ExtendsResourceCycle()
        {
            Building mine = AddCompleteBuilding(_planet, BuildingType.Mine, processRate: 2);
            mine.ProductionInputReserved = true;
            mine.ResourceStartupCyclePending = false;
            _game.AttachNode(
                new Regiment
                {
                    InstanceID = "REGIMENT1",
                    OwnerInstanceID = _faction.InstanceID,
                    MaintenanceCost = 15,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                _planet
            );

            ProcessTicks(3);

            Assert.AreEqual(15, mine.ResourceMaintenanceAllocation);
            Assert.AreEqual(0, _faction.RawMaterialStockpile);

            _system.ProcessTick();

            Assert.AreEqual(1, _faction.RawMaterialStockpile);
        }

        [Test]
        public void ProcessTick_MaintenanceAllocationAcrossMultipleMines_ReachesDemand()
        {
            Building firstMine = AddCompleteBuilding(_planet, BuildingType.Mine, processRate: 2);
            Building secondMine = AddCompleteBuilding(_planet, BuildingType.Mine, processRate: 2);
            _game.AttachNode(
                new Regiment
                {
                    InstanceID = "REGIMENT1",
                    OwnerInstanceID = _faction.InstanceID,
                    MaintenanceCost = 40,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                _planet
            );

            _system.ProcessTick();

            Assert.AreEqual(20, firstMine.ResourceMaintenanceAllocation);
            Assert.AreEqual(20, secondMine.ResourceMaintenanceAllocation);
        }

        [Test]
        public void ProcessTick_MineAndRefineryOnDifferentPlanets_ShareMaintenanceDemand()
        {
            Planet secondPlanet = CreateOwnedPlanet("PLANET2");
            _game.AttachNode(secondPlanet, _planetSector);
            Building mine = AddCompleteBuilding(_planet, BuildingType.Mine, processRate: 2);
            Building refinery = AddCompleteBuilding(
                secondPlanet,
                BuildingType.Refinery,
                processRate: 2
            );
            _game.AttachNode(
                new Regiment
                {
                    InstanceID = "REGIMENT1",
                    OwnerInstanceID = _faction.InstanceID,
                    MaintenanceCost = 20,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                _planet
            );

            _system.ProcessTick();

            Assert.AreEqual(50, _faction.MaintenanceCapacity);
            Assert.AreEqual(20, mine.ResourceMaintenanceAllocation);
            Assert.AreEqual(20, refinery.ResourceMaintenanceAllocation);
        }

        [Test]
        public void ProcessTick_BlockadedPlanetWithKdy_DoesNotAdvanceResourceCycle()
        {
            Building mine = AddCompleteBuilding(_planet, BuildingType.Mine, processRate: 1);
            mine.ProductionInputReserved = true;
            mine.ResourceStartupCyclePending = false;
            Building kdy = AddCompleteBuilding(_planet, BuildingType.Defense, processRate: 1);
            kdy.DefenseWeaponEffect = DefenseWeaponEffect.ShieldDamage;
            Fleet hostileFleet = new Fleet
            {
                InstanceID = "HOSTILE_FLEET",
                OwnerInstanceID = "FACTION2",
            };
            _game.AttachNode(hostileFleet, _planet);
            _game.AttachNode(
                new CapitalShip
                {
                    InstanceID = "HOSTILE_SHIP",
                    OwnerInstanceID = "FACTION2",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                hostileFleet
            );

            _system.ProcessTick();

            Assert.AreEqual(0, mine.ProductionCycleProgress);
            Assert.AreEqual(0, _faction.RawMaterialStockpile);
        }

        [Test]
        public void ProcessTick_PlanetInUprising_DoesNotAdvanceResourceCycle()
        {
            Building mine = AddCompleteBuilding(_planet, BuildingType.Mine, processRate: 1);
            mine.ProductionInputReserved = true;
            mine.ResourceStartupCyclePending = false;
            _planet.IsInUprising = true;

            _system.ProcessTick();

            Assert.AreEqual(0, mine.ProductionCycleProgress);
            Assert.AreEqual(0, _faction.RawMaterialStockpile);
        }

        [Test]
        public void ProcessTick_SuspendedResourceFacility_PreservesAllocationAndResumesCycle()
        {
            Building mine = AddCompleteBuilding(_planet, BuildingType.Mine, processRate: 2);
            mine.ProductionInputReserved = true;
            mine.ProductionCycleDuration = 2;
            mine.ResourceMaintenanceAllocation = 15;
            mine.ResourceStartupCyclePending = false;
            _game.AttachNode(
                new Regiment
                {
                    InstanceID = "REGIMENT1",
                    OwnerInstanceID = _faction.InstanceID,
                    MaintenanceCost = 15,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                _planet
            );
            _planet.IsInUprising = true;

            _system.ProcessTick();

            Assert.AreEqual(15, mine.ResourceMaintenanceAllocation);
            Assert.AreEqual(0, mine.ProductionCycleProgress);

            _planet.IsInUprising = false;
            _system.ProcessTick();

            Assert.AreEqual(15, mine.ResourceMaintenanceAllocation);
            Assert.AreEqual(1, mine.ProductionCycleProgress);
        }

        private Planet CreateOwnedPlanet(string instanceId)
        {
            return new Planet
            {
                InstanceID = instanceId,
                OwnerInstanceID = _faction.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
                NumRawResourceNodes = 5,
                PopularSupport = new Dictionary<string, int> { { _faction.InstanceID, 100 } },
            };
        }

        private Building AddCompleteBuilding(
            Planet planet,
            BuildingType type,
            int processRate,
            ManufacturingType productionType = ManufacturingType.None
        )
        {
            Building building = new Building
            {
                InstanceID = $"BUILDING{++_nextBuildingId}",
                OwnerInstanceID = planet.GetOwnerInstanceID(),
                BuildingType = type,
                ProductionType = productionType,
                ProcessRate = processRate,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(building, planet);
            return building;
        }

        private void ProcessTicks(int count)
        {
            for (int tick = 0; tick < count; tick++)
                _system.ProcessTick();
        }
    }
}
