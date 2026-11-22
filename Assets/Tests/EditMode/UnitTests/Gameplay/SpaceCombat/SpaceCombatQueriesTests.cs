using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    /// <summary>
    /// Tests withdrawal eligibility without beginning or changing an encounter.
    /// </summary>
    [TestFixture]
    public class SpaceCombatQueriesTests : CombatTestBase
    {
        [Test]
        public void CanRetreatForces_FriendlyDestination_DoesNotMoveForces()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CreatePlanet(game, "home", owner: "alliance");
            fleet.Waypoints.Add("next-planet");

            bool canRetreat = queries.CanRetreatForces(
                new[] { fleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.IsTrue(canRetreat);
            Assert.AreSame(planet, fleet.GetParent());
            Assert.IsNull(fleet.Movement);
            CollectionAssert.AreEqual(new[] { "next-planet" }, fleet.Waypoints);
        }

        [Test]
        [Category("RequiresMedia")]
        public void CanRetreatForces_NoFriendlyDestination_ReturnsFalse()
        {
            (_, Planet planet, Fleet fleet, SpaceCombatQueries queries) = CreateScenario();

            bool canRetreat = queries.CanRetreatForces(
                new[] { fleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.IsFalse(canRetreat);
        }

        [Test]
        [Category("RequiresMedia")]
        public void CanRetreatForces_HyperdriveFighterWithOnlyLocalCarrier_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CapitalShip carrier = fleet.GetChildren<CapitalShip>().Single();
            carrier.Hyperdrive = 0;
            carrier.StarfighterCapacity = 1;
            game.AttachNode(
                new Starfighter
                {
                    InstanceID = "fighter",
                    OwnerInstanceID = "alliance",
                    Hyperdrive = 1,
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            bool canRetreat = queries.CanRetreatForces(
                new[] { fleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.IsFalse(canRetreat);
        }

        [Test]
        [Category("RequiresMedia")]
        public void CanRetreatForces_HostileGravityWell_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CreatePlanet(game, "home", owner: "alliance");
            Fleet opponent = AddFleet(game, planet, "opponent", "empire");
            opponent.GetChildren<CapitalShip>()[0].HasGravityWell = true;

            bool canRetreat = queries.CanRetreatForces(
                new[] { fleet },
                new[] { opponent },
                planet,
                "alliance"
            );

            Assert.IsFalse(canRetreat);
        }

        [Test]
        [Category("RequiresMedia")]
        public void CanRetreatForces_FleetWithPlanetaryNonHyperdriveFighter_ReturnsTrue()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CreatePlanet(game, "home", owner: "alliance");
            game.AttachNode(
                new Starfighter
                {
                    InstanceID = "fighter",
                    OwnerInstanceID = "alliance",
                    Hyperdrive = 0,
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            bool canRetreat = queries.CanRetreatForces(
                new[] { fleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.IsTrue(canRetreat);
        }

        [Test]
        [Category("RequiresMedia")]
        public void CanRetreatForces_FleetWithMixedHyperdriveShips_ReturnsTrue()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CreatePlanet(game, "home", owner: "alliance");
            game.AttachNode(
                new CapitalShip
                {
                    InstanceID = "stranded-ship",
                    OwnerInstanceID = "alliance",
                    Hyperdrive = 0,
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                fleet
            );

            bool canRetreat = queries.CanRetreatForces(
                new[] { fleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.IsTrue(canRetreat);
        }

        [Test]
        [Category("RequiresMedia")]
        public void CanRetreatForces_FleetWithoutAnyHyperdrive_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CreatePlanet(game, "home", owner: "alliance");
            fleet.GetChildren<CapitalShip>()[0].Hyperdrive = 0;

            bool canRetreat = queries.CanRetreatForces(
                new[] { fleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.IsFalse(canRetreat);
        }

        [Test]
        [Category("RequiresMedia")]
        public void CanRetreatForces_NonHyperdriveFleetWithCarriedHyperdriveFighter_ReturnsTrue()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CreatePlanet(game, "home", owner: "alliance");
            CapitalShip carrier = fleet.GetChildren<CapitalShip>()[0];
            carrier.Hyperdrive = 0;
            carrier.StarfighterCapacity = 1;
            game.AttachNode(
                new Starfighter
                {
                    InstanceID = "fighter",
                    OwnerInstanceID = "alliance",
                    Hyperdrive = 1,
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                carrier
            );

            bool canRetreat = queries.CanRetreatForces(
                new[] { fleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.IsTrue(canRetreat);
        }

        [Test]
        [Category("RequiresMedia")]
        public void CanRetreatForces_HumanControlledOwnedHeadquarters_ReturnsTrue()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CreatePlanet(game, "home", owner: "alliance");
            game.SetFactionController("alliance", "player", PlayerControllerType.Human);
            game.GetFactionByOwnerInstanceID("alliance").HQInstanceID = planet.InstanceID;
            planet.IsHeadquarters = true;

            bool canRetreat = queries.CanRetreatForces(
                new[] { fleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.IsTrue(canRetreat);
        }

        [Test]
        [Category("RequiresMedia")]
        public void CanRetreatForces_OpposingHeadquarters_ReturnsTrue()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CreatePlanet(game, "home", owner: "alliance");
            planet.OwnerInstanceID = "empire";
            game.GetFactionByOwnerInstanceID("empire").HQInstanceID = planet.InstanceID;
            planet.IsHeadquarters = true;

            bool canRetreat = queries.CanRetreatForces(
                new[] { fleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.IsTrue(canRetreat);
        }

        [Test]
        [Category("RequiresMedia")]
        public void TryFindContestedForces_PlanetOwner_DefendsAgainstOpposingFleet()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "combat", owner: "alliance");
            Fleet allianceFleet = AddFleet(game, planet, "alliance-fleet", "alliance");
            Fleet empireFleet = AddFleet(game, planet, "empire-fleet", "empire");
            SpaceCombatQueries queries = new SpaceCombatQueries(game, new MovementQueries(game));

            bool found = queries.TryFindContestedForces(
                new HashSet<string>(),
                out Planet contestedPlanet,
                out string attackerOwnerInstanceId,
                out string defenderOwnerInstanceId,
                out List<Fleet> attackerFleets,
                out List<Fleet> defenderFleets
            );

            Assert.IsTrue(found);
            Assert.AreSame(planet, contestedPlanet);
            Assert.AreEqual("empire", attackerOwnerInstanceId);
            Assert.AreEqual("alliance", defenderOwnerInstanceId);
            CollectionAssert.AreEqual(new[] { empireFleet }, attackerFleets);
            CollectionAssert.AreEqual(new[] { allianceFleet }, defenderFleets);
        }

        [Test]
        public void IsPlanetDestroyerShielded_OwnersCompletedGeneratorListsTheShipType_ReturnsTrue()
        {
            (Planet planet, CapitalShip planetDestroyer, _) = CreateShieldScenario();

            bool shielded = SpaceCombatQueries.IsPlanetDestroyerShielded(planetDestroyer, planet);

            Assert.IsTrue(shielded);
        }

        [Test]
        public void IsPlanetDestroyerShielded_OrdinaryCapitalShipHasProtectedType_ReturnsFalse()
        {
            (Planet planet, CapitalShip planetDestroyer, _) = CreateShieldScenario();
            planetDestroyer.CanDestroyPlanets = false;

            bool shielded = SpaceCombatQueries.IsPlanetDestroyerShielded(planetDestroyer, planet);

            Assert.IsFalse(shielded);
        }

        [Test]
        public void IsPlanetDestroyerShielded_GeneratorIsUnderConstruction_ReturnsFalse()
        {
            (Planet planet, CapitalShip planetDestroyer, Building shield) = CreateShieldScenario();
            shield.ManufacturingStatus = ManufacturingStatus.Building;

            bool shielded = SpaceCombatQueries.IsPlanetDestroyerShielded(planetDestroyer, planet);

            Assert.IsFalse(shielded);
        }

        [Test]
        public void IsPlanetDestroyerShielded_GeneratorIsMoving_ReturnsFalse()
        {
            (Planet planet, CapitalShip planetDestroyer, Building shield) = CreateShieldScenario();
            shield.Movement = new MovementState();

            bool shielded = SpaceCombatQueries.IsPlanetDestroyerShielded(planetDestroyer, planet);

            Assert.IsFalse(shielded);
        }

        [Test]
        public void IsPlanetDestroyerShielded_GeneratorHasDifferentOwner_ReturnsFalse()
        {
            (Planet planet, CapitalShip planetDestroyer, Building shield) = CreateShieldScenario();
            shield.OwnerInstanceID = "alliance";

            bool shielded = SpaceCombatQueries.IsPlanetDestroyerShielded(planetDestroyer, planet);

            Assert.IsFalse(shielded);
        }

        [Test]
        public void IsPlanetDestroyerShielded_GeneratorListsDifferentType_ReturnsFalse()
        {
            (Planet planet, CapitalShip planetDestroyer, Building shield) = CreateShieldScenario();
            shield.ProtectedUnitTypeIDs.Clear();
            shield.ProtectedUnitTypeIDs.Add("other-type");

            bool shielded = SpaceCombatQueries.IsPlanetDestroyerShielded(planetDestroyer, planet);

            Assert.IsFalse(shielded);
        }

        [Test]
        public void IsPlanetDestroyerShielded_PlanetIsMissing_ReturnsFalse()
        {
            (_, CapitalShip planetDestroyer, _) = CreateShieldScenario();

            bool shielded = SpaceCombatQueries.IsPlanetDestroyerShielded(planetDestroyer, null);

            Assert.IsFalse(shielded);
        }

        /// <summary>
        /// Creates a planet destroyer and a completed matching shield generator at its planet.
        /// </summary>
        /// <returns>The planet, planet destroyer, and shield generator.</returns>
        private static (
            Planet planet,
            CapitalShip planetDestroyer,
            Building shield
        ) CreateShieldScenario()
        {
            Planet planet = new Planet { InstanceID = "shield-planet", OwnerInstanceID = "empire" };
            CapitalShip planetDestroyer = new CapitalShip
            {
                InstanceID = "planet-destroyer",
                TypeID = "planet-destroyer-type",
                OwnerInstanceID = "empire",
                CanDestroyPlanets = true,
            };
            Building shield = new Building
            {
                InstanceID = "planet-destroyer-shield",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                ProtectedUnitTypeIDs = { "planet-destroyer-type" },
            };
            planet.AddTestChild(shield);
            return (planet, planetDestroyer, shield);
        }

        /// <summary>
        /// Creates a stationary fleet and the read-only combat query dependencies.
        /// </summary>
        /// <returns>The game, location, fleet, and queries.</returns>
        private (
            GameRoot game,
            Planet planet,
            Fleet fleet,
            SpaceCombatQueries queries
        ) CreateScenario()
        {
            GameRoot game = CreateGame();
            game.Random = new ThrowingRNG();
            (Planet planet, _) = CreatePlanet(game, "combat", owner: "alliance");
            Fleet fleet = AddFleet(game, planet, "fleet", "alliance");
            return (game, planet, fleet, new SpaceCombatQueries(game, new MovementQueries(game)));
        }

        /// <summary>
        /// Adds an active hyperspace-capable fleet at a planet.
        /// </summary>
        /// <param name="game">The game containing the fleet.</param>
        /// <param name="planet">The fleet's location.</param>
        /// <param name="instanceID">The fleet identifier.</param>
        /// <param name="ownerID">The controlling faction identifier.</param>
        /// <returns>The attached fleet.</returns>
        private static Fleet AddFleet(
            GameRoot game,
            Planet planet,
            string instanceID,
            string ownerID
        )
        {
            Fleet fleet = new Fleet { InstanceID = instanceID, OwnerInstanceID = ownerID };
            game.AttachNode(fleet, planet);
            game.AttachNode(
                new CapitalShip
                {
                    InstanceID = instanceID + "-ship",
                    OwnerInstanceID = ownerID,
                    Hyperdrive = 1,
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                fleet
            );
            return fleet;
        }
    }
}
