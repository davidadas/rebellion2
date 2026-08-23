using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Finder
{
    [TestFixture]
    public class FinderWindowRowBuilderTests
    {
        private const string _playerFactionId = "player";
        private const string _opponentFactionId = "opponent";

        private Planet _alpha;
        private Planet _beta;
        private Planet _neutral;
        private Planet _unexplored;
        private GalaxyMapPlanet _alphaMapPlanet;
        private GalaxyMapPlanet _betaMapPlanet;
        private Faction _opponentFaction;
        private Faction _playerFaction;
        private FinderWindowRowBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _alpha = CreatePlanet("alpha", "Alpha", _playerFactionId, _playerFactionId);
            _beta = CreatePlanet("beta", "beta", _opponentFactionId, _opponentFactionId);
            _neutral = CreatePlanet("neutral", "Neutral", null, _playerFactionId);
            _unexplored = CreatePlanet("unexplored", "Unknown", null);
            GalaxyPlanetSector firstSector = new GalaxyPlanetSector();
            GalaxyPlanetSector secondSector = new GalaxyPlanetSector();
            _alphaMapPlanet = new GalaxyMapPlanet(firstSector, _alpha, string.Empty);
            _betaMapPlanet = new GalaxyMapPlanet(secondSector, _beta, string.Empty);
            GalaxyMapPlanet neutralMapPlanet = new GalaxyMapPlanet(
                firstSector,
                _neutral,
                string.Empty
            );
            GalaxyMapPlanet unexploredMapPlanet = new GalaxyMapPlanet(
                secondSector,
                _unexplored,
                string.Empty
            );
            GalaxyMapSector[] sectors =
            {
                new GalaxyMapSector(firstSector, new[] { neutralMapPlanet, _alphaMapPlanet }),
                new GalaxyMapSector(secondSector, new[] { unexploredMapPlanet, _betaMapPlanet }),
            };
            _playerFaction = new Faction { InstanceID = _playerFactionId, DisplayName = "Player" };
            _opponentFaction = new Faction
            {
                InstanceID = _opponentFactionId,
                DisplayName = "Opponent",
            };
            Faction[] factions = { _opponentFaction, _playerFaction };
            _builder = new FinderWindowRowBuilder(
                sectors,
                factions,
                _playerFactionId,
                _ => new[] { "armor", "infantry", "commando", "foreign" },
                _ =>
                    new[]
                    {
                        "commando",
                        "spy",
                        "mission spy",
                        "fleet commando",
                        "planet commando",
                        "foreign",
                    }
            );
        }

        [Test]
        public void Constructor_NullSectors_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new FinderWindowRowBuilder(null, new Faction[0], _playerFactionId)
            );
        }

        [Test]
        public void GetRows_TroopsWithoutConfiguredColumns_ThrowsInvalidOperationException()
        {
            _alpha.AddTestChild(CreateRegiment("planet-armor", "armor", "Armor", _playerFactionId));
            FinderWindowRowBuilder builder = CreateBuilder(_ => null, _ => new[] { "commando" });

            Assert.Throws<System.InvalidOperationException>(() =>
                builder.GetRows(
                    FinderMode.Troops,
                    false,
                    FinderWindowTab.Faction(_playerFactionId, "Player")
                )
            );
        }

        [TestCase(FinderMode.Troops, false)]
        [TestCase(FinderMode.Personnel, true)]
        public void GetRows_UnitModeWithoutFactionTab_ReturnsNoRows(FinderMode mode, bool panel)
        {
            List<FinderWindowRow> rows = _builder.GetRows(mode, panel, null);

            Assert.IsEmpty(rows);
        }

        [Test]
        public void GetRows_TroopsWithDuplicateColumns_ThrowsInvalidOperationException()
        {
            _alpha.AddTestChild(CreateRegiment("planet-armor", "armor", "Armor", _playerFactionId));
            FinderWindowRowBuilder builder = CreateBuilder(
                _ => new[] { "armor", "armor" },
                _ => new[] { "commando" }
            );

            Assert.Throws<System.InvalidOperationException>(() =>
                builder.GetRows(
                    FinderMode.Troops,
                    false,
                    FinderWindowTab.Faction(_playerFactionId, "Player")
                )
            );
        }

        [Test]
        public void GetRows_TroopsWithUnmappedUnitType_ThrowsInvalidOperationException()
        {
            _alpha.AddTestChild(CreateRegiment("planet-armor", "armor", "Armor", _playerFactionId));
            FinderWindowRowBuilder builder = CreateBuilder(
                _ => new[] { "infantry" },
                _ => new[] { "commando" }
            );

            Assert.Throws<System.InvalidOperationException>(() =>
                builder.GetRows(
                    FinderMode.Troops,
                    false,
                    FinderWindowTab.Faction(_playerFactionId, "Player")
                )
            );
        }

        [Test]
        public void GetRows_AllSystems_ReturnsEveryPlanetInAlphabeticalOrder()
        {
            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Systems,
                false,
                FinderWindowTab.All()
            );

            CollectionAssert.AreEqual(
                new[] { "Alpha", "beta", "Neutral", "Unknown" },
                rows.Select(row => row.Name)
            );
            Assert.IsTrue(rows.All(row => row.TargetIcon == PlanetIcon.None));
        }

        [Test]
        public void GetRows_FactionSystems_ReturnsOnlyFactionOwnedPlanets()
        {
            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Systems,
                false,
                FinderWindowTab.Faction(_playerFactionId, "Player")
            );

            CollectionAssert.AreEqual(new[] { "Alpha" }, rows.Select(row => row.Name));
            Assert.AreSame(_alphaMapPlanet, rows[0].Planet);
        }

        [Test]
        public void GetRows_NeutralSystems_ExcludesUnexploredPlanets()
        {
            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Systems,
                false,
                FinderWindowTab.Neutral()
            );

            CollectionAssert.AreEqual(new[] { "Neutral" }, rows.Select(row => row.Name));
        }

        [Test]
        public void GetRows_UnexploredSystems_ReturnsPlanetsWithoutVisitors()
        {
            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Systems,
                false,
                FinderWindowTab.Unexplored()
            );

            CollectionAssert.AreEqual(new[] { "Unknown" }, rows.Select(row => row.Name));
        }

        [Test]
        public void GetRows_NullSystemTab_ReturnsNoRows()
        {
            List<FinderWindowRow> rows = _builder.GetRows(FinderMode.Systems, false, null);

            Assert.IsEmpty(rows);
        }

        [Test]
        public void GetRows_AllFleets_ReturnsFleetDestinationsInAlphabeticalOrder()
        {
            GameFleet zeta = CreateFleet("zeta", "Zeta Fleet", _playerFactionId);
            GameFleet escort = CreateFleet("escort", "Escort Fleet", _opponentFactionId);
            _alpha.AddTestChild(zeta);
            _beta.AddTestChild(escort);

            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Fleets,
                false,
                FinderWindowTab.All()
            );

            CollectionAssert.AreEqual(
                new[] { "Escort Fleet", "Zeta Fleet" },
                rows.Select(row => row.Name)
            );
            Assert.AreSame(_betaMapPlanet, rows[0].Planet);
            Assert.AreEqual(PlanetIcon.Fleet, rows[0].TargetIcon);
            Assert.AreSame(escort, rows[0].Node);
        }

        [Test]
        public void GetRows_FactionFleets_ReturnsOnlyMatchingOwner()
        {
            GameFleet playerFleet = CreateFleet("player-fleet", "Player Fleet", _playerFactionId);
            _alpha.AddTestChild(playerFleet);
            _beta.AddTestChild(CreateFleet("opponent-fleet", "Opponent Fleet", _opponentFactionId));

            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Fleets,
                false,
                FinderWindowTab.Faction(_playerFactionId, "Player")
            );

            Assert.AreEqual(1, rows.Count);
            Assert.AreSame(playerFleet, rows[0].Node);
        }

        [Test]
        public void GetRows_ShipPanel_ReturnsShipsWithContainingFleet()
        {
            CapitalShip cruiser = CreateCapitalShip("cruiser", "Cruiser", _playerFactionId);
            CapitalShip assault = CreateCapitalShip("assault", "Assault Ship", _playerFactionId);
            GameFleet fleet = CreateFleet("fleet", "Fleet", _playerFactionId, cruiser, assault);
            _alpha.AddTestChild(fleet);

            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Fleets,
                true,
                FinderWindowTab.Faction(_playerFactionId, "Player")
            );

            CollectionAssert.AreEqual(
                new[] { "Assault Ship", "Cruiser" },
                rows.Select(row => row.Name)
            );
            Assert.AreSame(assault, rows[0].Node);
            Assert.AreSame(fleet, rows[0].Fleet);
            Assert.AreEqual(PlanetIcon.Fleet, rows[0].TargetIcon);
        }

        [Test]
        public void GetRows_TroopsOnPlanet_AggregatesCountsInAuthoredColumnOrder()
        {
            _alpha.AddTestChild(CreateRegiment("planet-armor", "armor", "Armor", _playerFactionId));
            _alpha.AddTestChild(
                CreateRegiment("planet-infantry-1", "infantry", "Infantry", _playerFactionId)
            );
            _alpha.AddTestChild(
                CreateRegiment("planet-infantry-2", "infantry", "infantry", _playerFactionId)
            );
            _alpha.AddTestChild(
                CreateRegiment("foreign", "foreign", "Foreign", _opponentFactionId)
            );
            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Troops,
                false,
                FinderWindowTab.Faction(_playerFactionId, "Player")
            );

            CollectionAssert.AreEqual(new[] { "Alpha" }, rows.Select(row => row.Name));
            CollectionAssert.AreEqual(new[] { 1, 2, 0, 0 }, rows[0].Counts);
            Assert.AreEqual(PlanetIcon.Defense, rows[0].TargetIcon);
        }

        [Test]
        public void GetRows_TroopsInFleet_AggregatesCountsInAuthoredColumnOrder()
        {
            CapitalShip transport = CreateCapitalShip("transport", "Transport", _playerFactionId);
            transport.AddTestChild(
                CreateRegiment("fleet-armor", "armor", "Armor", _playerFactionId)
            );
            transport.AddTestChild(
                CreateRegiment("fleet-commando", "commando", "Commando", _playerFactionId)
            );
            GameFleet fleet = CreateFleet("fleet", "Fleet Base", _playerFactionId, transport);
            _alpha.AddTestChild(fleet);

            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Troops,
                false,
                FinderWindowTab.Faction(_playerFactionId, "Player")
            );

            Assert.AreEqual(1, rows.Count);
            CollectionAssert.AreEqual(new[] { 1, 0, 1, 0 }, rows[0].Counts);
            Assert.AreEqual(PlanetIcon.Fleet, rows[0].TargetIcon);
            Assert.AreSame(fleet, rows[0].Node);
        }

        [Test]
        public void GetRows_TroopsWithoutFactionTab_ReturnsNoRows()
        {
            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Troops,
                false,
                FinderWindowTab.All()
            );

            Assert.IsEmpty(rows);
        }

        [Test]
        public void GetRows_Personnel_ProjectsMissionFleetAndPlanetLocationsWithoutDuplicates()
        {
            Officer missionOfficer = new Officer
            {
                InstanceID = "mission-officer",
                DisplayName = "Agent",
                OwnerInstanceID = _playerFactionId,
                IsCaptured = true,
            };
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = "mission",
                OwnerInstanceID = _playerFactionId,
            };
            mission.AddChild(missionOfficer);
            _alpha.AddTestChild(mission);
            _alpha.AddTestChild(missionOfficer);
            Officer fleetOfficer = new Officer
            {
                InstanceID = "fleet-officer",
                DisplayName = "Han",
                OwnerInstanceID = _playerFactionId,
                CurrentRank = OfficerRank.Admiral,
                Movement = new MovementState(),
            };
            CapitalShip ship = CreateCapitalShip("ship", "Ship", _playerFactionId);
            ship.AddTestChild(fleetOfficer);
            GameFleet fleet = CreateFleet("fleet", "Fleet Alpha", _playerFactionId, ship);
            _alpha.AddTestChild(fleet);
            Officer planetOfficer = new Officer
            {
                InstanceID = "planet-officer",
                DisplayName = "Leia",
                OwnerInstanceID = _playerFactionId,
                CurrentRank = OfficerRank.General,
                InjuryPoints = 1,
            };
            _alpha.AddTestChild(planetOfficer);
            _alpha.AddTestChild(
                new Officer
                {
                    InstanceID = "foreign-officer",
                    DisplayName = "Foreign",
                    OwnerInstanceID = _opponentFactionId,
                }
            );

            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Personnel,
                false,
                FinderWindowTab.Faction(_playerFactionId, "Player")
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    "Agent - Alpha ( Captured )",
                    "Han - Fleet Alpha ( Enroute ) ( Admiral )",
                    "Leia - Alpha ( Injured ) ( General )",
                },
                rows.Select(row => row.Name)
            );
            Assert.AreEqual(PlanetIcon.Mission, rows[0].TargetIcon);
            Assert.AreSame(mission, rows[0].Mission);
            Assert.AreEqual(PlanetIcon.Fleet, rows[1].TargetIcon);
            Assert.AreSame(fleet, rows[1].Fleet);
            Assert.AreEqual(PlanetIcon.Defense, rows[2].TargetIcon);
        }

        [Test]
        public void GetRows_PersonnelRetainedOutsideGalaxy_IncludesOwnedOfficer()
        {
            Officer retiredOfficer = new Officer
            {
                InstanceID = "retired-officer",
                DisplayName = "Retired Officer",
                OwnerInstanceID = _playerFactionId,
                IsRetired = true,
            };
            _playerFaction.AddOwnedUnit(retiredOfficer);

            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Personnel,
                false,
                FinderWindowTab.Faction(_playerFactionId, "Player")
            );

            Assert.AreEqual("Retired Officer - Unknown ( Retired )", rows.Single().Name);
        }

        [Test]
        public void GetRows_OpponentPersonnelOutsideSnapshot_DoesNotRevealLiveOfficerState()
        {
            _opponentFaction.AddOwnedUnit(
                new Officer
                {
                    InstanceID = "hidden-opponent",
                    DisplayName = "Hidden Opponent",
                    OwnerInstanceID = _opponentFactionId,
                    Movement = new MovementState { TransitTicks = 10 },
                }
            );

            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Personnel,
                false,
                FinderWindowTab.Faction(_opponentFactionId, "Opponent")
            );

            Assert.IsEmpty(rows);
        }

        [Test]
        public void GetRows_Personnel_ExcludesSpecialForcesShownByDedicatedPanel()
        {
            SpecialForces missionUnit = CreateSpecialForces(
                "mission-spy",
                "Mission Spy",
                _playerFactionId
            );
            DiplomacyMission mission = new DiplomacyMission { InstanceID = "mission" };
            mission.AddChild(missionUnit);
            _alpha.AddTestChild(mission);
            CapitalShip ship = CreateCapitalShip("ship", "Ship", _playerFactionId);
            ship.AddTestChild(
                CreateSpecialForces("fleet-commando", "Fleet Commando", _playerFactionId)
            );
            _alpha.AddTestChild(CreateFleet("fleet", "Fleet", _playerFactionId, ship));
            _alpha.AddTestChild(
                CreateSpecialForces("planet-commando", "Planet Commando", _playerFactionId)
            );

            List<FinderWindowRow> personnelRows = _builder.GetRows(
                FinderMode.Personnel,
                false,
                FinderWindowTab.Faction(_playerFactionId, "Player")
            );
            List<FinderWindowRow> specialForcesRows = _builder.GetRows(
                FinderMode.Personnel,
                true,
                FinderWindowTab.Faction(_playerFactionId, "Player")
            );

            Assert.IsEmpty(personnelRows);
            Assert.AreEqual(1, specialForcesRows.Count);
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1, 1, 0 }, specialForcesRows[0].Counts);
        }

        [Test]
        public void GetRows_SpecialForces_AggregatesPlanetMissionAndFleetUnits()
        {
            _alpha.AddTestChild(
                CreateSpecialForces("planet-commando-1", "Commando", _playerFactionId)
            );
            _alpha.AddTestChild(
                CreateSpecialForces("planet-commando-2", "commando", _playerFactionId)
            );
            SpecialForces missionUnit = CreateSpecialForces("mission-spy", "Spy", _playerFactionId);
            DiplomacyMission mission = new DiplomacyMission { InstanceID = "mission" };
            mission.AddChild(missionUnit);
            _alpha.AddTestChild(mission);
            CapitalShip ship = CreateCapitalShip("ship", "Ship", _playerFactionId);
            ship.AddTestChild(CreateSpecialForces("fleet-commando", "Commando", _playerFactionId));
            _alpha.AddTestChild(CreateFleet("fleet", "Fleet", _playerFactionId, ship));
            _alpha.AddTestChild(CreateSpecialForces("foreign", "Foreign", _opponentFactionId));

            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Personnel,
                true,
                FinderWindowTab.Faction(_playerFactionId, "Player")
            );

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("Alpha", rows[0].Name);
            CollectionAssert.AreEqual(new[] { 3, 1, 0, 0, 0, 0 }, rows[0].Counts);
            Assert.AreEqual(PlanetIcon.Defense, rows[0].TargetIcon);
            Assert.AreSame(_alpha, rows[0].Node);
        }

        [Test]
        public void GetRows_UnsupportedMode_ReturnsNoRows()
        {
            List<FinderWindowRow> rows = _builder.GetRows((FinderMode)99, false, null);

            Assert.IsEmpty(rows);
        }

        [Test]
        public void GetTabs_NullFactions_ReturnsModeSpecificNonFactionTabs()
        {
            FinderWindowRowBuilder builder = new FinderWindowRowBuilder(
                new GalaxyMapSector[0],
                null,
                _playerFactionId
            );

            List<FinderWindowTab> tabs = builder.GetTabs(FinderMode.Systems);

            Assert.AreEqual(3, tabs.Count);
            Assert.IsTrue(tabs[0].IsAll);
            Assert.IsTrue(tabs[1].IsNeutral);
            Assert.IsTrue(tabs[2].IsUnexplored);
        }

        private static Planet CreatePlanet(
            string instanceId,
            string displayName,
            string ownerId,
            params string[] visitingFactionIds
        )
        {
            return new Planet
            {
                InstanceID = instanceId,
                DisplayName = displayName,
                OwnerInstanceID = ownerId,
                VisitingFactionIDs = visitingFactionIds.ToList(),
            };
        }

        private static GameFleet CreateFleet(
            string instanceId,
            string displayName,
            string ownerId,
            params CapitalShip[] ships
        )
        {
            return new GameFleet(ownerId, displayName, ships.ToList()) { InstanceID = instanceId };
        }

        private static CapitalShip CreateCapitalShip(
            string instanceId,
            string displayName,
            string ownerId
        )
        {
            return new CapitalShip
            {
                InstanceID = instanceId,
                DisplayName = displayName,
                OwnerInstanceID = ownerId,
            };
        }

        private static Regiment CreateRegiment(
            string instanceId,
            string typeId,
            string displayName,
            string ownerId
        )
        {
            return new Regiment
            {
                InstanceID = instanceId,
                TypeID = typeId,
                DisplayName = displayName,
                OwnerInstanceID = ownerId,
            };
        }

        private FinderWindowRowBuilder CreateBuilder(
            System.Func<string, IReadOnlyList<string>> getTroopColumnTypeIds,
            System.Func<string, IReadOnlyList<string>> getSpecialForcesColumnTypeIds
        )
        {
            GalaxyPlanetSector planetSector = new GalaxyPlanetSector();
            return new FinderWindowRowBuilder(
                new[]
                {
                    new GalaxyMapSector(
                        planetSector,
                        new[] { new GalaxyMapPlanet(planetSector, _alpha, string.Empty) }
                    ),
                },
                new[]
                {
                    new Faction { InstanceID = _playerFactionId, DisplayName = "Player" },
                },
                _playerFactionId,
                getTroopColumnTypeIds,
                getSpecialForcesColumnTypeIds
            );
        }

        private static SpecialForces CreateSpecialForces(
            string instanceId,
            string displayName,
            string ownerId
        )
        {
            return new SpecialForces
            {
                InstanceID = instanceId,
                TypeID = displayName.ToLowerInvariant(),
                DisplayName = displayName,
                OwnerInstanceID = ownerId,
            };
        }
    }
}
