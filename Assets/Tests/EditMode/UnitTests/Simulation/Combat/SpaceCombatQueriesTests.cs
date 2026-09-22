using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
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
        public void CanRetreatForces_PlanetaryFighterWithoutHyperdrive_ReturnsFalse()
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

            Assert.IsFalse(canRetreat);
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
