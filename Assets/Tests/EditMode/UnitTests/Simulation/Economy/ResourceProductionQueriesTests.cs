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
            Assert.AreEqual(38, summary.MaintenanceHeadroom);
            Assert.AreEqual(38, summary.DeliveredMaintenanceHeadroom);
            Assert.AreEqual(38, summary.ProjectedMaintenanceHeadroom);
            Assert.AreEqual(12, summary.MaintenanceCommitted);
            Assert.AreEqual(12, summary.Maintenance.Regiments);
            Assert.AreEqual(12, summary.Maintenance.Deployed);
        }

        [Test]
        public void GetSummary_ExcludedMineWithSpareMine_UsesReplacementWithoutChangingGraph()
        {
            Planet planet = AddPlanet("planet", rawResourceNodes: 1);
            Building mine = AddBuilding(planet, BuildingType.Mine, processRate: 1);
            AddBuilding(planet, BuildingType.Mine, processRate: 1);
            AddBuilding(planet, BuildingType.Refinery, processRate: 1);

            ResourceEconomySummary after = new ResourceProductionQueries(_game).GetSummary(
                _faction,
                new[] { mine }
            );

            Assert.AreEqual(1, after.Mines.Active);
            Assert.AreEqual(50, after.MaintenanceHeadroom);
            Assert.AreSame(mine, _game.GetSceneNodeByInstanceID<Building>(mine.InstanceID));
            Assert.AreSame(planet, mine.GetParent());
        }

        [Test]
        public void GetSummary_ExcludedResourceWithHardBonus_MatchesCapacityAfterRemoval()
        {
            _game.Summary = new GameSummary { Difficulty = GameDifficulty.Hard };
            _game.SetFactionController(_faction.InstanceID, "FACTION1-AI", PlayerControllerType.AI);
            _game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                MaintenanceCapacityPercent = 150,
            };
            Planet planet = AddPlanet("planet", rawResourceNodes: 2);
            Building mine = AddBuilding(planet, BuildingType.Mine, processRate: 1);
            AddBuilding(planet, BuildingType.Mine, processRate: 1);
            AddBuilding(planet, BuildingType.Refinery, processRate: 1);
            AddBuilding(planet, BuildingType.Refinery, processRate: 1);
            ResourceProductionQueries queries = new(_game);

            ResourceEconomySummary projected = queries.GetSummary(_faction, new[] { mine });
            _game.DeleteNode(mine);
            ResourceEconomySummary actual = queries.GetSummary(_faction);

            Assert.AreEqual(75, projected.MaintenanceHeadroom);
            Assert.AreEqual(actual.MaintenanceHeadroom, projected.MaintenanceHeadroom);
        }

        [Test]
        public void GetSummary_ExcludedResourceWithQueuedUnit_ReservesUnfinishedMaintenance()
        {
            Planet planet = AddPlanet("planet", rawResourceNodes: 2);
            Building mine = AddBuilding(planet, BuildingType.Mine, processRate: 1);
            AddBuilding(planet, BuildingType.Mine, processRate: 1);
            AddBuilding(planet, BuildingType.Refinery, processRate: 1);
            AddBuilding(planet, BuildingType.Refinery, processRate: 1);
            _game.AttachNode(
                new Regiment
                {
                    InstanceID = "queued",
                    OwnerInstanceID = _faction.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Building,
                    MaintenanceCost = 60,
                },
                planet
            );

            ResourceEconomySummary after = new ResourceProductionQueries(_game).GetSummary(
                _faction,
                new[] { mine }
            );

            Assert.AreEqual(-10, after.MaintenanceHeadroom);
        }

        [TestCase(GameDifficulty.Medium)]
        [TestCase(GameDifficulty.Hard)]
        public void CaptureOperatingEconomy_ExclusionAlternatives_MatchesLiveQueries(
            GameDifficulty difficulty
        )
        {
            _game.Summary = new GameSummary { Difficulty = difficulty };
            _game.SetFactionController(_faction.InstanceID, "ai-player", PlayerControllerType.AI);
            _game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                MaintenanceCapacityPercent = 150,
                MineOutputPercent = 125,
                RefineryOutputPercent = 175,
            };
            Planet planet = AddPlanet("A", 1, support: 55);
            Building firstMine = AddBuilding(planet, BuildingType.Mine, 2);
            AddBuilding(planet, BuildingType.Mine, 5);
            Building zeroRate = AddBuilding(planet, BuildingType.Mine, 0);
            Building refinery = AddBuilding(planet, BuildingType.Refinery, 3);
            Building disabled = AddBuilding(planet, BuildingType.Refinery, 4);
            disabled.IsEnabled = false;
            Building industry = AddBuilding(planet, BuildingType.ConstructionFacility, 7);
            AddBuilding(
                planet,
                BuildingType.Mine,
                2,
                ManufacturingStatus.Building
            ).MaintenanceCost = 9;
            AddBuilding(
                planet,
                BuildingType.Refinery,
                3,
                ManufacturingStatus.Delivering
            ).MaintenanceCost = 4;
            Planet remote = AddPlanet("Z", 2, support: 80);
            AddBuilding(remote, BuildingType.Mine, 3);
            AddBuilding(remote, BuildingType.Refinery, 2);
            Planet suspended = AddPlanet("S", 1);
            suspended.IsInUprising = true;
            AddBuilding(suspended, BuildingType.Mine, 1);
            AddBuilding(suspended, BuildingType.Refinery, 1);
            Planet uncolonized = AddPlanet("U", 1);
            uncolonized.IsColonized = false;
            AddBuilding(uncolonized, BuildingType.Mine, 2, ManufacturingStatus.Building);
            AddBuilding(uncolonized, BuildingType.Refinery, 3, ManufacturingStatus.Building);
            List<Building> buildings = _faction.GetOwnedUnitsByType<Building>(
                includeDisabled: true
            );
            for (int index = 0; index < buildings.Count; index++)
            {
                buildings[index].MaintenanceCost = index + 1;
                buildings[index].ResourceMaintenanceAllocation = index * 7;
                buildings[index].ProductionCycleDuration = index + 2;
            }
            Building missing = new Building
            {
                InstanceID = "missing",
                OwnerInstanceID = _faction.InstanceID,
                MaintenanceCost = 100,
            };
            Building foreign = new Building
            {
                InstanceID = firstMine.InstanceID,
                OwnerInstanceID = "other",
            };
            Building[] alternatives =
            {
                firstMine,
                refinery,
                zeroRate,
                disabled,
                industry,
                missing,
            };
            ResourceProductionQueries queries = new(_game);
            var captured = queries.CaptureOperatingEconomy(_faction);

            for (int mask = 0; mask < 1 << alternatives.Length; mask++)
            {
                List<Building> excluded = new();
                for (int index = 0; index < alternatives.Length; index++)
                    if ((mask & (1 << index)) != 0)
                        excluded.Add(alternatives[index]);
                if (excluded.Count > 0)
                {
                    excluded.Add(excluded[0]);
                    excluded.Add(null);
                    excluded.Add(foreign);
                }

                Assert.AreEqual(
                    queries.GetOperatingEconomy(_faction, excluded),
                    captured.GetOperatingEconomy(excluded),
                    $"exclusions={mask}"
                );
            }
            Assert.AreEqual(
                queries.GetOperatingEconomy(_faction, null),
                captured.GetOperatingEconomy(null)
            );
        }

        [Test]
        public void CaptureOperatingEconomy_RemovingFirstMine_UsesNextMineWithinResourceLimit()
        {
            Planet planet = AddPlanet("planet", 1);
            Building first = AddBuilding(planet, BuildingType.Mine, 2);
            AddBuilding(planet, BuildingType.Mine, 5);
            AddBuilding(planet, BuildingType.Refinery, 1);
            var captured = new ResourceProductionQueries(_game).CaptureOperatingEconomy(_faction);

            var result = captured.GetOperatingEconomy(new[] { first });

            Assert.AreEqual(0.2, result.RawOutputPerTick);
            Assert.AreEqual(50, result.MaintenanceHeadroom);
        }

        [Test]
        public void CaptureOperatingEconomy_EvaluatingExclusions_PreservesLiveAllocations()
        {
            Planet planet = AddPlanet("planet", 2);
            Building mine = AddBuilding(planet, BuildingType.Mine, 2);
            Building refinery = AddBuilding(planet, BuildingType.Refinery, 3);
            mine.ResourceMaintenanceAllocation = 17;
            refinery.ResourceMaintenanceAllocation = 41;
            var captured = new ResourceProductionQueries(_game).CaptureOperatingEconomy(_faction);

            captured.GetOperatingEconomy(new[] { mine });
            captured.GetOperatingEconomy(new[] { refinery });

            Assert.AreEqual(17, mine.ResourceMaintenanceAllocation);
            Assert.AreEqual(41, refinery.ResourceMaintenanceAllocation);
        }

        [Test]
        public void CaptureOperatingEconomy_AfterGameChanges_UsesFreshEconomy()
        {
            Planet planet = AddPlanet("planet", 2);
            Building mine = AddBuilding(planet, BuildingType.Mine, 2);
            AddBuilding(planet, BuildingType.Mine, 5);
            AddBuilding(planet, BuildingType.Refinery, 3);
            ResourceProductionQueries queries = new(_game);
            var previous = queries
                .CaptureOperatingEconomy(_faction)
                .GetOperatingEconomy(new[] { mine });
            mine.MaintenanceCost = 13;
            AddBuilding(planet, BuildingType.Refinery, 1).MaintenanceCost = 5;

            var refreshed = queries
                .CaptureOperatingEconomy(_faction)
                .GetOperatingEconomy(new[] { mine });

            Assert.AreEqual(queries.GetOperatingEconomy(_faction, new[] { mine }), refreshed);
            Assert.AreNotEqual(previous, refreshed);
        }

        [Test]
        public void GetOperatingEconomy_ExcludedResource_MatchesFullSummaryProjection()
        {
            Planet planet = AddPlanet("planet", rawResourceNodes: 2);
            Building mine = AddBuilding(planet, BuildingType.Mine, processRate: 2);
            AddBuilding(planet, BuildingType.Mine, processRate: 1);
            AddBuilding(planet, BuildingType.Refinery, processRate: 3);
            AddBuilding(planet, BuildingType.Refinery, processRate: 2);
            ResourceProductionQueries queries = new(_game);

            var operating = queries.GetOperatingEconomy(_faction, new[] { mine });
            ResourceEconomySummary summary = queries.GetSummary(_faction, new[] { mine });

            Assert.AreEqual(summary.MaintenanceHeadroom, operating.MaintenanceHeadroom);
            Assert.AreEqual(summary.RawOutputPerTick, operating.RawOutputPerTick);
            Assert.AreEqual(summary.RefinedOutputPerTick, operating.RefinedOutputPerTick);
        }

        [Test]
        public void GetSummary_HardAiMaintenanceBonus_IncreasesEffectiveCapacity()
        {
            _game.Summary = new GameSummary { Difficulty = GameDifficulty.Hard };
            _game.SetFactionController(_faction.InstanceID, "FACTION1-AI", PlayerControllerType.AI);
            _game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                MaintenanceCapacityPercent = 150,
            };
            Planet planet = AddPlanet("PLANET1", rawResourceNodes: 1);
            AddBuilding(planet, BuildingType.Mine, processRate: 1);
            AddBuilding(planet, BuildingType.Refinery, processRate: 1);

            ResourceEconomySummary summary = new ResourceProductionQueries(_game).GetSummary(
                _faction
            );

            Assert.AreEqual(50, _faction.MaintenanceCapacity);
            Assert.AreEqual(75, summary.MaintenanceHeadroom);
            Assert.AreEqual(75, summary.DeliveredMaintenanceHeadroom);
            Assert.AreEqual(75, summary.ProjectedMaintenanceHeadroom);
        }

        [Test]
        public void GetSummary_MaximumMaintenanceModifiers_SaturatesEffectiveCapacity()
        {
            _game.Summary = new GameSummary { Difficulty = GameDifficulty.Hard };
            _game.SetFactionController(_faction.InstanceID, "FACTION1-AI", PlayerControllerType.AI);
            _game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                MaintenanceCapacityPercent = int.MaxValue,
            };
            _faction.Settings.ResourceProcessingPointsPerFacility = int.MaxValue;
            Planet planet = AddPlanet("PLANET1", rawResourceNodes: 2);
            AddBuilding(planet, BuildingType.Mine, processRate: 1);
            AddBuilding(planet, BuildingType.Mine, processRate: 1);
            AddBuilding(planet, BuildingType.Refinery, processRate: 1);
            AddBuilding(planet, BuildingType.Refinery, processRate: 1);

            ResourceEconomySummary summary = new ResourceProductionQueries(_game).GetSummary(
                _faction
            );

            Assert.AreEqual(int.MaxValue, summary.MaintenanceHeadroom);
            Assert.AreEqual(int.MaxValue, summary.DeliveredMaintenanceHeadroom);
            Assert.AreEqual(int.MaxValue, summary.ProjectedMaintenanceHeadroom);
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
            Assert.AreEqual(50, summary.MaintenanceHeadroom);
            Assert.AreEqual(100, summary.DeliveredMaintenanceHeadroom);
            Assert.AreEqual(150, summary.ProjectedMaintenanceHeadroom);
        }

        [Test]
        public void GetSummary_CommittedUnits_DeductsCostsFromEveryHeadroomProjection()
        {
            Planet planet = AddPlanet("PLANET1", rawResourceNodes: 3);
            AddBuilding(planet, BuildingType.Mine, processRate: 2);
            AddBuilding(planet, BuildingType.Refinery, processRate: 4);
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
            _faction.AddOwnedUnit(
                new Regiment
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
                new CapitalShip
                {
                    MaintenanceCost = 30,
                    ManufacturingStatus = ManufacturingStatus.Building,
                }
            );

            ResourceEconomySummary summary = new ResourceProductionQueries(_game).GetSummary(
                _faction
            );

            Assert.AreEqual(-10, summary.MaintenanceHeadroom);
            Assert.AreEqual(40, summary.DeliveredMaintenanceHeadroom);
            Assert.AreEqual(90, summary.ProjectedMaintenanceHeadroom);
            Assert.AreEqual(_faction.MaintenanceHeadroom, summary.MaintenanceHeadroom);
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
