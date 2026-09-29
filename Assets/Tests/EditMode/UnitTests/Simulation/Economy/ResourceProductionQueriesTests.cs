using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class ResourceProductionQueriesTests
    {
        private Faction _faction;
        private GameRoot _game;
        private PlanetSector _sector;
        private int _nextBuildingID;

        /// <summary>
        /// Creates an owned economy for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _faction = new Faction { InstanceID = "FACTION1" };
            _faction.Settings.ResourceProcessingPointsPerFacility = 50;
            _game.GetFactions().Add(_faction);
            _sector = new PlanetSector { InstanceID = "SECTOR1" };
            _game.AttachNode(_sector, _game.Galaxy);
        }

        [Test]
        public void GetSummary_MixedFacilityStates_ReturnsCountsRatesAndMaintenance()
        {
            Planet activePlanet = AddPlanet("ACTIVE", rawResourceNodes: 1);
            Building activeMine = AddBuilding(activePlanet, BuildingType.Mine, processRate: 2);
            activeMine.ProductionCycleDuration = 2;
            AddBuilding(activePlanet, BuildingType.Mine, processRate: 2);
            Building activeRefinery = AddBuilding(
                activePlanet,
                BuildingType.Refinery,
                processRate: 4
            );
            activeRefinery.ProductionCycleDuration = 4;

            Planet suspendedPlanet = AddPlanet("SUSPENDED", rawResourceNodes: 1);
            suspendedPlanet.IsInUprising = true;
            AddBuilding(suspendedPlanet, BuildingType.Mine, processRate: 2);
            AddBuilding(suspendedPlanet, BuildingType.Refinery, processRate: 4);
            AddBuilding(
                activePlanet,
                BuildingType.Mine,
                processRate: 2,
                status: ManufacturingStatus.Building
            );
            AddBuilding(
                activePlanet,
                BuildingType.Refinery,
                processRate: 4,
                status: ManufacturingStatus.Building
            );
            AddBuilding(
                activePlanet,
                BuildingType.Mine,
                processRate: 2,
                status: ManufacturingStatus.Delivering
            );
            AddBuilding(
                activePlanet,
                BuildingType.Refinery,
                processRate: 4,
                status: ManufacturingStatus.Delivering
            );
            _game.AttachNode(
                new Regiment
                {
                    InstanceID = "REGIMENT1",
                    OwnerInstanceID = _faction.InstanceID,
                    MaintenanceCost = 12,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                activePlanet
            );

            ResourceEconomySummary summary = new ResourceProductionQueries(_game).GetSummary(
                _faction
            );

            Assert.AreEqual(1, summary.Mines.Active);
            Assert.AreEqual(2, summary.Mines.Offline);
            Assert.AreEqual(1, summary.Mines.Building);
            Assert.AreEqual(1, summary.Mines.EnRoute);
            Assert.AreEqual(1, summary.Refineries.Active);
            Assert.AreEqual(1, summary.Refineries.Offline);
            Assert.AreEqual(1, summary.Refineries.Building);
            Assert.AreEqual(1, summary.Refineries.EnRoute);
            Assert.AreEqual(0.5, summary.RawOutputPerTick, 0.0001);
            Assert.AreEqual(0.25, summary.DeliveredRawOutputPerTick, 0.0001);
            Assert.AreEqual(0.25, summary.ProjectedRawOutputPerTick, 0.0001);
            Assert.AreEqual(0.25, summary.RefinedOutputPerTick, 0.0001);
            Assert.AreEqual(0.4, summary.DeliveredRefinedOutputPerTick, 0.0001);
            Assert.AreEqual(0.6, summary.ProjectedRefinedOutputPerTick, 0.0001);
            Assert.AreEqual(50, summary.MaintenanceCapacity);
            Assert.AreEqual(50, summary.DeliveredMaintenanceCapacity);
            Assert.AreEqual(50, summary.ProjectedMaintenanceCapacity);
            Assert.AreEqual(12, summary.MaintenanceCommitted);
            Assert.AreEqual(12, summary.Maintenance.Regiments);
            Assert.AreEqual(12, summary.Maintenance.Deployed);
            Assert.AreEqual(38, summary.MaintenanceHeadroom);
        }

        [Test]
        public void GetSummary_CommittedResourceFacilities_ProjectsSteadyOutputAndCapacity()
        {
            Planet planet = AddPlanet("PLANET1", rawResourceNodes: 3);
            Building activeMine = AddBuilding(planet, BuildingType.Mine, processRate: 2);
            activeMine.ProductionCycleDuration = 2;
            Building activeRefinery = AddBuilding(planet, BuildingType.Refinery, processRate: 4);
            activeRefinery.ProductionCycleDuration = 4;
            AddBuilding(
                planet,
                BuildingType.Mine,
                processRate: 2,
                status: ManufacturingStatus.Building
            );
            AddBuilding(
                planet,
                BuildingType.Refinery,
                processRate: 4,
                status: ManufacturingStatus.Building
            );
            AddBuilding(
                planet,
                BuildingType.Mine,
                processRate: 2,
                status: ManufacturingStatus.Delivering
            );
            AddBuilding(
                planet,
                BuildingType.Refinery,
                processRate: 4,
                status: ManufacturingStatus.Delivering
            );

            ResourceEconomySummary summary = new ResourceProductionQueries(_game).GetSummary(
                _faction
            );

            Assert.AreEqual(0.5, summary.RawOutputPerTick, 0.0001);
            Assert.AreEqual(1.0, summary.DeliveredRawOutputPerTick, 0.0001);
            Assert.AreEqual(1.5, summary.ProjectedRawOutputPerTick, 0.0001);
            Assert.AreEqual(0.25, summary.RefinedOutputPerTick, 0.0001);
            Assert.AreEqual(0.5, summary.DeliveredRefinedOutputPerTick, 0.0001);
            Assert.AreEqual(0.75, summary.ProjectedRefinedOutputPerTick, 0.0001);
            Assert.AreEqual(50, summary.MaintenanceCapacity);
            Assert.AreEqual(100, summary.DeliveredMaintenanceCapacity);
            Assert.AreEqual(150, summary.ProjectedMaintenanceCapacity);
        }

        [Test]
        public void GetSummary_DeliveredFacilityWithSatisfiedDemand_PreservesCurrentAllocations()
        {
            _game.Config.Production.ResourceCollectionBasePercent = 100;
            _game.Config.Production.ResourceMaintenanceLoadPercent = 20;
            Planet planet = AddPlanet("PLANET1", rawResourceNodes: 2);
            Building activeMine = AddBuilding(planet, BuildingType.Mine, processRate: 2);
            activeMine.ResourceMaintenanceAllocation = 50;
            AddBuilding(
                planet,
                BuildingType.Mine,
                processRate: 2,
                status: ManufacturingStatus.Delivering
            );
            _game.AttachNode(
                new Regiment
                {
                    InstanceID = "REGIMENT1",
                    OwnerInstanceID = _faction.InstanceID,
                    MaintenanceCost = 50,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            ResourceEconomySummary summary = new ResourceProductionQueries(_game).GetSummary(
                _faction
            );

            Assert.AreEqual((1.0 / 7.0) + (1.0 / 2.0), summary.DeliveredRawOutputPerTick, 0.0001);
        }

        [Test]
        public void GetSummary_MixedMaintenanceCosts_ClassifiesAssetsAndOrders()
        {
            _faction.AddOwnedUnit(
                new CapitalShip
                {
                    MaintenanceCost = 10,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            _faction.AddOwnedUnit(
                new Starfighter
                {
                    MaintenanceCost = 20,
                    ManufacturingStatus = ManufacturingStatus.Delivering,
                }
            );
            _faction.AddOwnedUnit(
                new Regiment
                {
                    MaintenanceCost = 30,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            _faction.AddOwnedUnit(
                new SpecialForces
                {
                    MaintenanceCost = 40,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            _faction.AddOwnedUnit(
                new Building
                {
                    MaintenanceCost = 50,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            _faction.AddOwnedUnit(
                new CapitalShip
                {
                    MaintenanceCost = 60,
                    ManufacturingStatus = ManufacturingStatus.Building,
                }
            );

            ResourceEconomySummary summary = new ResourceProductionQueries(_game).GetSummary(
                _faction
            );

            Assert.AreEqual(10, summary.Maintenance.CapitalShips);
            Assert.AreEqual(20, summary.Maintenance.Starfighters);
            Assert.AreEqual(30, summary.Maintenance.Regiments);
            Assert.AreEqual(40, summary.Maintenance.SpecialForces);
            Assert.AreEqual(50, summary.Maintenance.Facilities);
            Assert.AreEqual(60, summary.Maintenance.Orders);
            Assert.AreEqual(130, summary.Maintenance.Deployed);
            Assert.AreEqual(20, summary.Maintenance.EnRoute);
            Assert.AreEqual(60, summary.Maintenance.Building);
            Assert.AreEqual(210, summary.MaintenanceCommitted);
        }

        [Test]
        public void GetSummary_CompleteAssetWithMovement_ClassifiesMaintenanceAsEnRoute()
        {
            _faction.AddOwnedUnit(
                new Regiment
                {
                    MaintenanceCost = 30,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    Movement = new MovementState(),
                }
            );

            ResourceEconomySummary summary = new ResourceProductionQueries(_game).GetSummary(
                _faction
            );

            Assert.AreEqual(0, summary.Maintenance.Deployed);
            Assert.AreEqual(30, summary.Maintenance.EnRoute);
        }

        [Test]
        public void GetSummary_CompleteAssetInMovingFleet_ClassifiesMaintenanceAsEnRoute()
        {
            Planet planet = AddPlanet("PLANET1", rawResourceNodes: 0);
            Fleet fleet = new Fleet
            {
                InstanceID = "FLEET1",
                OwnerInstanceID = _faction.InstanceID,
                Movement = new MovementState(),
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "SHIP1",
                OwnerInstanceID = _faction.InstanceID,
                MaintenanceCost = 40,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(fleet, planet);
            _game.AttachNode(ship, fleet);

            ResourceEconomySummary summary = new ResourceProductionQueries(_game).GetSummary(
                _faction
            );

            Assert.AreEqual(0, summary.Maintenance.Deployed);
            Assert.AreEqual(40, summary.Maintenance.EnRoute);
        }

        [Test]
        public void GetSummary_UninitializedLowSupportMine_UsesCalculatedCycleRate()
        {
            Planet planet = AddPlanet("PLANET1", rawResourceNodes: 1, support: 50);
            AddBuilding(planet, BuildingType.Mine, processRate: 2);

            ResourceEconomySummary summary = new ResourceProductionQueries(_game).GetSummary(
                _faction
            );

            Assert.AreEqual(0.25, summary.RawOutputPerTick, 0.0001);
        }

        /// <summary>
        /// Adds an owned colonized planet to the test sector.
        /// </summary>
        /// <param name="instanceID">The planet instance identifier.</param>
        /// <param name="rawResourceNodes">The planet's raw-resource nodes.</param>
        /// <param name="support">The owning faction's popular support.</param>
        /// <returns>The attached planet.</returns>
        private Planet AddPlanet(string instanceID, int rawResourceNodes, int support = 100)
        {
            Planet planet = new Planet
            {
                InstanceID = instanceID,
                OwnerInstanceID = _faction.InstanceID,
                IsColonized = true,
                EnergyCapacity = 20,
                NumRawResourceNodes = rawResourceNodes,
                PopularSupport = new Dictionary<string, int> { [_faction.InstanceID] = support },
            };
            _game.AttachNode(planet, _sector);
            return planet;
        }

        /// <summary>
        /// Adds one resource facility to a planet.
        /// </summary>
        /// <param name="planet">The destination planet.</param>
        /// <param name="type">The building type.</param>
        /// <param name="processRate">The facility process rate.</param>
        /// <param name="status">The manufacturing status.</param>
        /// <returns>The attached building.</returns>
        private Building AddBuilding(
            Planet planet,
            BuildingType type,
            int processRate,
            ManufacturingStatus status = ManufacturingStatus.Complete
        )
        {
            Building building = new Building
            {
                InstanceID = $"BUILDING{++_nextBuildingID}",
                OwnerInstanceID = _faction.InstanceID,
                BuildingType = type,
                ProcessRate = processRate,
                ManufacturingStatus = status,
            };
            _game.AttachNode(building, planet);
            return building;
        }
    }
}
