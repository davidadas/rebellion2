using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

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
        private FinderWindowRowBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _alpha = CreatePlanet("alpha", "Alpha", _playerFactionId, _playerFactionId);
            _beta = CreatePlanet("beta", "beta", _opponentFactionId, _opponentFactionId);
            _neutral = CreatePlanet("neutral", "Neutral", null, _playerFactionId);
            _unexplored = CreatePlanet("unexplored", "Unknown", null);
            GamePlanetSystem firstSystem = new GamePlanetSystem();
            GamePlanetSystem secondSystem = new GamePlanetSystem();
            _alphaMapPlanet = new GalaxyMapPlanet(firstSystem, _alpha, string.Empty);
            _betaMapPlanet = new GalaxyMapPlanet(secondSystem, _beta, string.Empty);
            GalaxyMapPlanet neutralMapPlanet = new GalaxyMapPlanet(
                firstSystem,
                _neutral,
                string.Empty
            );
            GalaxyMapPlanet unexploredMapPlanet = new GalaxyMapPlanet(
                secondSystem,
                _unexplored,
                string.Empty
            );
            GalaxyMapSector[] sectors =
            {
                new GalaxyMapSector(firstSystem, new[] { neutralMapPlanet, _alphaMapPlanet }),
                new GalaxyMapSector(secondSystem, new[] { unexploredMapPlanet, _betaMapPlanet }),
            };
            Faction[] factions =
            {
                new Faction { InstanceID = _opponentFactionId, DisplayName = "Opponent" },
                new Faction { InstanceID = _playerFactionId, DisplayName = "Player" },
            };
            _builder = new FinderWindowRowBuilder(sectors, factions, _playerFactionId);
        }

        [Test]
        public void Constructor_NullSectors_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new FinderWindowRowBuilder(null, new Faction[0], _playerFactionId)
            );
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
            _alpha.Fleets.Add(zeta);
            _beta.Fleets.Add(escort);

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
            _alpha.Fleets.Add(playerFleet);
            _beta.Fleets.Add(CreateFleet("opponent-fleet", "Opponent Fleet", _opponentFactionId));

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
            _alpha.Fleets.Add(fleet);

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
        public void GetRows_Troops_AggregatesPlanetAndFleetRegimentsByType()
        {
            _alpha.Regiments.Add(CreateRegiment("planet-armor", "Armor", _playerFactionId));
            _alpha.Regiments.Add(CreateRegiment("planet-infantry-1", "Infantry", _playerFactionId));
            _alpha.Regiments.Add(CreateRegiment("planet-infantry-2", "infantry", _playerFactionId));
            _alpha.Regiments.Add(CreateRegiment("foreign", "Foreign", _opponentFactionId));
            CapitalShip transport = CreateCapitalShip("transport", "Transport", _playerFactionId);
            transport.Regiments.Add(CreateRegiment("fleet-armor", "Armor", _playerFactionId));
            transport.Regiments.Add(CreateRegiment("fleet-commando", "Commando", _playerFactionId));
            GameFleet fleet = CreateFleet("fleet", "Fleet Base", _playerFactionId, transport);
            _alpha.Fleets.Add(fleet);

            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Troops,
                false,
                FinderWindowTab.Faction(_playerFactionId, "Player")
            );

            CollectionAssert.AreEqual(
                new[] { "Alpha", "Fleet Base" },
                rows.Select(row => row.Name)
            );
            CollectionAssert.AreEqual(new[] { 1, 2 }, rows[0].Counts);
            CollectionAssert.AreEqual(new[] { 1, 1 }, rows[1].Counts);
            Assert.AreEqual(PlanetIcon.Defense, rows[0].TargetIcon);
            Assert.AreEqual(PlanetIcon.Fleet, rows[1].TargetIcon);
            Assert.AreSame(fleet, rows[1].Node);
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
                MainParticipants = new List<IMissionParticipant> { missionOfficer },
            };
            _alpha.Missions.Add(mission);
            _alpha.Officers.Add(missionOfficer);
            Officer fleetOfficer = new Officer
            {
                InstanceID = "fleet-officer",
                DisplayName = "Han",
                OwnerInstanceID = _playerFactionId,
                CurrentRank = OfficerRank.Admiral,
                Movement = new MovementState(),
            };
            CapitalShip ship = CreateCapitalShip("ship", "Ship", _playerFactionId);
            ship.Officers.Add(fleetOfficer);
            GameFleet fleet = CreateFleet("fleet", "Fleet Alpha", _playerFactionId, ship);
            _alpha.Fleets.Add(fleet);
            Officer planetOfficer = new Officer
            {
                InstanceID = "planet-officer",
                DisplayName = "Leia",
                OwnerInstanceID = _playerFactionId,
                CurrentRank = OfficerRank.General,
                InjuryPoints = 1,
            };
            _alpha.Officers.Add(planetOfficer);
            _alpha.Officers.Add(
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
        public void GetRows_SpecialForces_AggregatesPlanetMissionAndFleetUnits()
        {
            _alpha.SpecialForces.Add(
                CreateSpecialForces("planet-commando-1", "Commando", _playerFactionId)
            );
            _alpha.SpecialForces.Add(
                CreateSpecialForces("planet-commando-2", "commando", _playerFactionId)
            );
            SpecialForces missionUnit = CreateSpecialForces("mission-spy", "Spy", _playerFactionId);
            _alpha.Missions.Add(
                new DiplomacyMission
                {
                    InstanceID = "mission",
                    MainParticipants = new List<IMissionParticipant> { missionUnit },
                }
            );
            CapitalShip ship = CreateCapitalShip("ship", "Ship", _playerFactionId);
            ship.SpecialForces.Add(
                CreateSpecialForces("fleet-commando", "Commando", _playerFactionId)
            );
            _alpha.Fleets.Add(CreateFleet("fleet", "Fleet", _playerFactionId, ship));
            _alpha.SpecialForces.Add(CreateSpecialForces("foreign", "Foreign", _opponentFactionId));

            List<FinderWindowRow> rows = _builder.GetRows(
                FinderMode.Personnel,
                true,
                FinderWindowTab.Faction(_playerFactionId, "Player")
            );

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("Alpha", rows[0].Name);
            CollectionAssert.AreEqual(new[] { 3, 1 }, rows[0].Counts);
            Assert.AreEqual(PlanetIcon.Defense, rows[0].TargetIcon);
            Assert.AreSame(_alpha, rows[0].Node);
        }

        [Test]
        public void GetRows_UnsupportedMode_ReturnsNoRows()
        {
            List<FinderWindowRow> rows = _builder.GetRows((FinderMode)99, false, null);

            Assert.IsEmpty(rows);
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
            string displayName,
            string ownerId
        )
        {
            return new Regiment
            {
                InstanceID = instanceId,
                DisplayName = displayName,
                OwnerInstanceID = ownerId,
            };
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
                DisplayName = displayName,
                OwnerInstanceID = ownerId,
            };
        }
    }
}
