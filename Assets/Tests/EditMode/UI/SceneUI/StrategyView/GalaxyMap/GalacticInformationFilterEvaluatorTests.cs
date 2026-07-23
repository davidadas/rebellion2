using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalacticInformationFilterEvaluatorTests
    {
        private const string _playerFactionId = "player";
        private const string _opponentFactionId = "opponent";

        private GameRoot _game;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _game.Factions.Add(new Faction { InstanceID = _playerFactionId });
            _game.Factions.Add(new Faction { InstanceID = _opponentFactionId });
        }

        [Test]
        public void Marker_Values_PreservesEvaluationResult()
        {
            GalacticInformationMarker marker = new GalacticInformationMarker(
                2,
                _playerFactionId,
                true
            );

            Assert.AreEqual(2, marker.Index);
            Assert.AreEqual(_playerFactionId, marker.FactionInstanceId);
            Assert.IsTrue(marker.Mixed);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Evaluate_MissingInput_ReturnsLowestUnownedMarker(bool missingPlanet)
        {
            Planet planet = missingPlanet ? null : CreatePlanet(_playerFactionId);
            GalacticInformationFilterTheme filter = missingPlanet ? CreateFilter() : null;

            GalacticInformationMarker marker = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                planet,
                _playerFactionId,
                filter
            );

            Assert.AreEqual(0, marker.Index);
            Assert.IsNull(marker.FactionInstanceId);
            Assert.IsFalse(marker.Mixed);
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(10, 3)]
        public void Evaluate_PopularSupport_MapsConfiguredThresholdBoundaries(
            int support,
            int expectedIndex
        )
        {
            Planet planet = CreatePlanet(_playerFactionId);
            planet.PopularSupport[_playerFactionId] = support;
            GalacticInformationFilterTheme filter = CreateFilter(
                GalacticInformationFilterMode.PopularSupport
            );

            GalacticInformationMarker marker = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                planet,
                _playerFactionId,
                filter
            );

            Assert.AreEqual(expectedIndex, marker.Index);
            Assert.AreEqual(_playerFactionId, marker.FactionInstanceId);
            Assert.IsFalse(marker.Mixed);
        }

        [TestCase(GalacticInformationFilterMode.Uprisings)]
        [TestCase(GalacticInformationFilterMode.AvailableEnergy)]
        [TestCase(GalacticInformationFilterMode.AvailableRawMaterial)]
        [TestCase(GalacticInformationFilterMode.Mines)]
        [TestCase(GalacticInformationFilterMode.Refineries)]
        [TestCase(GalacticInformationFilterMode.Shipyards)]
        [TestCase(GalacticInformationFilterMode.TrainingFacilities)]
        [TestCase(GalacticInformationFilterMode.ConstructionYards)]
        [TestCase(GalacticInformationFilterMode.Troopers)]
        [TestCase(GalacticInformationFilterMode.FighterSquadrons)]
        [TestCase(GalacticInformationFilterMode.DeathStarShields)]
        [TestCase(GalacticInformationFilterMode.PlanetaryShieldGenerators)]
        [TestCase(GalacticInformationFilterMode.PlanetaryDefenseBatteries)]
        public void Evaluate_ScalarMode_WithOneMatchingValueReturnsLowMarker(
            GalacticInformationFilterMode mode
        )
        {
            Planet planet = CreatePlanet(_playerFactionId);
            AddScalarValue(planet, mode);
            GalacticInformationFilterTheme filter = CreateFilter(mode);

            GalacticInformationMarker marker = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                planet,
                _playerFactionId,
                filter
            );

            Assert.AreEqual(1, marker.Index);
            Assert.AreEqual(_playerFactionId, marker.FactionInstanceId);
            Assert.IsFalse(marker.Mixed);
        }

        [TestCase(GalacticInformationFilterMode.IdleShipyards, ManufacturingType.Ship)]
        [TestCase(GalacticInformationFilterMode.IdleTrainingFacilities, ManufacturingType.Troop)]
        [TestCase(GalacticInformationFilterMode.IdleConstructionYards, ManufacturingType.Building)]
        public void Evaluate_IdleManufacturing_PlayerOwnedEmptyQueueReturnsLowMarker(
            GalacticInformationFilterMode mode,
            ManufacturingType type
        )
        {
            Planet planet = CreatePlanet(_playerFactionId);
            planet.Buildings.Add(CreateProductionFacility(type));

            GalacticInformationMarker marker = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                planet,
                _playerFactionId,
                CreateFilter(mode)
            );

            Assert.AreEqual(1, marker.Index);
        }

        [Test]
        public void Evaluate_IdleManufacturing_ForeignOwnerOrQueuedWorkReturnsLowestMarker()
        {
            Planet foreignPlanet = CreatePlanet(_opponentFactionId);
            foreignPlanet.Buildings.Add(CreateProductionFacility(ManufacturingType.Ship));
            Planet busyPlanet = CreatePlanet(_playerFactionId);
            busyPlanet.Buildings.Add(CreateProductionFacility(ManufacturingType.Ship));
            busyPlanet.ManufacturingQueue[ManufacturingType.Ship] = new List<IManufacturable>
            {
                new CapitalShip(),
            };
            GalacticInformationFilterTheme filter = CreateFilter(
                GalacticInformationFilterMode.IdleShipyards
            );

            GalacticInformationMarker foreign = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                foreignPlanet,
                _playerFactionId,
                filter
            );
            GalacticInformationMarker busy = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                busyPlanet,
                _playerFactionId,
                filter
            );

            Assert.AreEqual(0, foreign.Index);
            Assert.AreEqual(0, busy.Index);
        }

        [Test]
        public void Evaluate_ActiveUnits_ExcludesBuildingAndMovingEntities()
        {
            Planet planet = CreatePlanet(_playerFactionId);
            planet.Regiments.Add(
                new Regiment { ManufacturingStatus = ManufacturingStatus.Complete }
            );
            planet.Regiments.Add(
                new Regiment { ManufacturingStatus = ManufacturingStatus.Building }
            );
            planet.Regiments.Add(
                new Regiment
                {
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    Movement = new MovementState(),
                }
            );
            GalacticInformationFilterTheme filter = CreateFilter(
                GalacticInformationFilterMode.Troopers
            );

            GalacticInformationMarker marker = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                planet,
                _playerFactionId,
                filter
            );

            Assert.AreEqual(1, marker.Index);
        }

        [Test]
        public void Evaluate_IdleFleets_BothFactionsReturnsHighestMixedMarker()
        {
            Planet planet = CreatePlanet(_playerFactionId);
            planet.Fleets.Add(CreateFleet(_playerFactionId, false));
            planet.Fleets.Add(CreateFleet(_opponentFactionId, false));

            GalacticInformationMarker marker = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                planet,
                _playerFactionId,
                CreateFilter(GalacticInformationFilterMode.IdleFleets)
            );

            Assert.AreEqual(3, marker.Index);
            Assert.AreEqual(_playerFactionId, marker.FactionInstanceId);
            Assert.IsTrue(marker.Mixed);
        }

        [Test]
        public void Evaluate_FleetsEnroute_OpposingFleetsReturnsOpponentIntensity()
        {
            Planet planet = CreatePlanet(_playerFactionId);
            planet.Fleets.Add(CreateFleet(_opponentFactionId, true));
            planet.Fleets.Add(CreateFleet(_opponentFactionId, true));
            planet.Fleets.Add(CreateFleet(_playerFactionId, false));

            GalacticInformationMarker marker = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                planet,
                _playerFactionId,
                CreateFilter(GalacticInformationFilterMode.FleetsEnroute)
            );

            Assert.AreEqual(2, marker.Index);
            Assert.AreEqual(_opponentFactionId, marker.FactionInstanceId);
            Assert.IsFalse(marker.Mixed);
        }

        [Test]
        public void Evaluate_FactionCountWithoutMatches_ReturnsPlanetOwnerAtLowestIntensity()
        {
            Planet planet = CreatePlanet(_opponentFactionId);

            GalacticInformationMarker marker = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                planet,
                _playerFactionId,
                CreateFilter(GalacticInformationFilterMode.IdleFleets)
            );

            Assert.AreEqual(0, marker.Index);
            Assert.AreEqual(_opponentFactionId, marker.FactionInstanceId);
            Assert.IsFalse(marker.Mixed);
        }

        [Test]
        public void Evaluate_IdlePersonnel_ExcludesUnavailablePersonnelAndIncompleteForces()
        {
            Planet planet = CreatePlanet(_playerFactionId);
            planet.Officers.Add(new Officer { OwnerInstanceID = _playerFactionId });
            planet.Officers.Add(
                new Officer { OwnerInstanceID = _playerFactionId, IsCaptured = true }
            );
            planet.Officers.Add(
                new Officer { OwnerInstanceID = _playerFactionId, InjuryPoints = 1 }
            );
            planet.Officers.Add(
                new Officer { OwnerInstanceID = _playerFactionId, IsKilled = true }
            );
            planet.Officers.Add(
                new Officer { OwnerInstanceID = _playerFactionId, Movement = new MovementState() }
            );
            planet.SpecialForces.Add(
                new SpecialForces
                {
                    OwnerInstanceID = _playerFactionId,
                    ManufacturingStatus = ManufacturingStatus.Building,
                }
            );
            planet.SpecialForces.Add(
                new SpecialForces
                {
                    OwnerInstanceID = _playerFactionId,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            GalacticInformationMarker marker = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                planet,
                _playerFactionId,
                CreateFilter(GalacticInformationFilterMode.IdlePersonnel)
            );

            Assert.AreEqual(2, marker.Index);
            Assert.AreEqual(_playerFactionId, marker.FactionInstanceId);
        }

        [Test]
        public void Evaluate_ActivePersonnel_CountsNonIdleOpposingPersonnel()
        {
            Planet planet = CreatePlanet(_playerFactionId);
            planet.Officers.Add(
                new Officer { OwnerInstanceID = _opponentFactionId, Movement = new MovementState() }
            );
            planet.Officers.Add(
                new Officer { OwnerInstanceID = _opponentFactionId, IsCaptured = true }
            );

            GalacticInformationMarker marker = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                planet,
                _playerFactionId,
                CreateFilter(GalacticInformationFilterMode.ActivePersonnel)
            );

            Assert.AreEqual(2, marker.Index);
            Assert.AreEqual(_opponentFactionId, marker.FactionInstanceId);
            Assert.IsFalse(marker.Mixed);
        }

        [Test]
        public void Evaluate_PersonnelCarriedByMovingFleet_CountsAsActive()
        {
            Planet planet = CreatePlanet(_playerFactionId);
            GameFleet fleet = new GameFleet
            {
                OwnerInstanceID = _playerFactionId,
                Movement = new MovementState(),
            };
            CapitalShip ship = new CapitalShip { OwnerInstanceID = _playerFactionId };
            Officer officer = new Officer { OwnerInstanceID = _playerFactionId };
            planet.Fleets.Add(fleet);
            fleet.CapitalShips.Add(ship);
            ship.Officers.Add(officer);
            fleet.SetParent(planet);
            ship.SetParent(fleet);
            officer.SetParent(ship);

            GalacticInformationMarker idleMarker = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                planet,
                _playerFactionId,
                CreateFilter(GalacticInformationFilterMode.IdlePersonnel)
            );
            GalacticInformationMarker activeMarker = GalacticInformationFilterEvaluator.Evaluate(
                _game,
                planet,
                _playerFactionId,
                CreateFilter(GalacticInformationFilterMode.ActivePersonnel)
            );

            Assert.AreEqual(0, idleMarker.Index);
            Assert.AreEqual(1, activeMarker.Index);
        }

        private static GalacticInformationFilterTheme CreateFilter(
            GalacticInformationFilterMode mode = GalacticInformationFilterMode.PopularSupport
        )
        {
            return new GalacticInformationFilterTheme
            {
                Mode = mode,
                LowThreshold = 1,
                MediumThreshold = 2,
                HighThreshold = 3,
            };
        }

        private static Planet CreatePlanet(string ownerId)
        {
            return new Planet { OwnerInstanceID = ownerId };
        }

        private static void AddScalarValue(Planet planet, GalacticInformationFilterMode mode)
        {
            switch (mode)
            {
                case GalacticInformationFilterMode.Uprisings:
                    planet.IsInUprising = true;
                    break;
                case GalacticInformationFilterMode.AvailableEnergy:
                    planet.EnergyCapacity = 1;
                    break;
                case GalacticInformationFilterMode.AvailableRawMaterial:
                    planet.NumRawResourceNodes = 1;
                    break;
                case GalacticInformationFilterMode.Mines:
                    planet.Buildings.Add(CreateBuilding(BuildingType.Mine));
                    break;
                case GalacticInformationFilterMode.Refineries:
                    planet.Buildings.Add(CreateBuilding(BuildingType.Refinery));
                    break;
                case GalacticInformationFilterMode.Shipyards:
                    planet.Buildings.Add(CreateProductionFacility(ManufacturingType.Ship));
                    break;
                case GalacticInformationFilterMode.TrainingFacilities:
                    planet.Buildings.Add(CreateProductionFacility(ManufacturingType.Troop));
                    break;
                case GalacticInformationFilterMode.ConstructionYards:
                    planet.Buildings.Add(CreateProductionFacility(ManufacturingType.Building));
                    break;
                case GalacticInformationFilterMode.Troopers:
                    planet.Regiments.Add(
                        new Regiment { ManufacturingStatus = ManufacturingStatus.Complete }
                    );
                    break;
                case GalacticInformationFilterMode.FighterSquadrons:
                    planet.Starfighters.Add(
                        new Starfighter { ManufacturingStatus = ManufacturingStatus.Complete }
                    );
                    break;
                case GalacticInformationFilterMode.DeathStarShields:
                    planet.Buildings.Add(
                        CreateDefenseBuilding(DefenseFacilityClass.DeathStarShield)
                    );
                    break;
                case GalacticInformationFilterMode.PlanetaryShieldGenerators:
                    planet.Buildings.Add(CreateDefenseBuilding(DefenseFacilityClass.Shield));
                    break;
                case GalacticInformationFilterMode.PlanetaryDefenseBatteries:
                    planet.Buildings.Add(CreateDefenseBuilding(DefenseFacilityClass.KDY));
                    break;
            }
        }

        private static Building CreateBuilding(BuildingType type)
        {
            return new Building
            {
                BuildingType = type,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private static Building CreateProductionFacility(ManufacturingType type)
        {
            return new Building
            {
                ProductionType = type,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private static Building CreateDefenseBuilding(DefenseFacilityClass facilityClass)
        {
            return new Building
            {
                DefenseFacilityClass = facilityClass,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private static GameFleet CreateFleet(string ownerId, bool enroute)
        {
            return new GameFleet
            {
                OwnerInstanceID = ownerId,
                Movement = enroute ? new MovementState() : null,
            };
        }
    }
}
