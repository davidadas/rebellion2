using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class ResourceProductionCommandsTests
    {
        private GameRoot _game;
        private ResourceProductionCommands _system;
        private Faction _faction;
        private PlanetSector _planetSector;
        private Planet _planet;
        private int _nextBuildingId;

        /// <summary>
        /// Sets up.
        /// </summary>
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
            _system = new ResourceProductionCommands(_game);
        }

        /// <summary>Verifies a mine produces only after completing its startup cycle.</summary>
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

        /// <summary>Verifies smuggling redirects a completed mine output in the same tick.</summary>
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

        /// <summary>Verifies smuggling redirects a completed refinery output in the same tick.</summary>
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

        /// <summary>Verifies scarce raw material is delivered in pending-request order.</summary>
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

        /// <summary>Verifies queued production reserves newly available refined material.</summary>
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

        /// <summary>Verifies a stale production request does not consume material.</summary>
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

        /// <summary>Verifies suspension preserves material delivery without advancing the refinery.</summary>
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

        /// <summary>Verifies suspended production may reserve material without advancing.</summary>
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

        /// <summary>Verifies completed mine output services a waiting refinery before the tick ends.</summary>
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

        /// <summary>Verifies completed refinery output services queued production before the tick ends.</summary>
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

        /// <summary>Verifies reduced support increases the resource cycle duration.</summary>
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

        /// <summary>Verifies maintenance allocation increases the resource cycle duration.</summary>
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

        /// <summary>Verifies maintenance demand is allocated across available mines.</summary>
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

        /// <summary>Verifies both resource lanes account for faction-wide maintenance demand.</summary>
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

        /// <summary>Verifies blockade suspends resource cycles despite a shield-damaging defense.</summary>
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

        /// <summary>Verifies an uprising suspends resource cycles.</summary>
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

        /// <summary>Verifies a suspended resource facility retains its allocation and resumes progress.</summary>
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

        /// <summary>
        /// Creates owned planet.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The created owned planet.</returns>
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

        /// <summary>
        /// Adds complete building.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="type">The type.</param>
        /// <param name="processRate">The process rate.</param>
        /// <param name="productionType">The production type.</param>
        /// <returns>The result of add complete building.</returns>
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

        /// <summary>
        /// Processes ticks.
        /// </summary>
        /// <param name="count">The count.</param>
        private void ProcessTicks(int count)
        {
            for (int tick = 0; tick < count; tick++)
                _system.ProcessTick();
        }
    }
}
