using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
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

        [Test]
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
        public void GetAutomaticWithdrawalGroups_AIControlledOwnedHeadquarters_ReturnsEmpty()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CreatePlanet(game, "home", owner: "alliance");
            game.SetFactionController("alliance", "ai", PlayerControllerType.AI);
            game.GetFactionByOwnerInstanceID("alliance").HQInstanceID = planet.InstanceID;
            planet.IsHeadquarters = true;

            List<IReadOnlyCollection<ISceneNode>> groups = queries.GetAutomaticWithdrawalGroups(
                new[] { fleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.IsEmpty(groups);
        }

        [Test]
        public void GetAutomaticWithdrawalGroups_HumanControlledOwnedHeadquarters_ReturnsFleet()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CreatePlanet(game, "home", owner: "alliance");
            game.SetFactionController("alliance", "player", PlayerControllerType.Human);
            game.GetFactionByOwnerInstanceID("alliance").HQInstanceID = planet.InstanceID;
            planet.IsHeadquarters = true;

            List<IReadOnlyCollection<ISceneNode>> groups = queries.GetAutomaticWithdrawalGroups(
                new[] { fleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.AreEqual(1, groups.Count);
        }

        [Test]
        public void GetAutomaticWithdrawalGroups_MultipleEligibleFleets_ReturnsSingleFleetGroup()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CreatePlanet(game, "home", owner: "alliance");
            Fleet secondFleet = AddFleet(game, planet, "second-fleet", "alliance");
            CapitalShip firstShip = fleet.GetChildren<CapitalShip>()[0];
            CapitalShip secondShip = secondFleet.GetChildren<CapitalShip>()[0];

            List<IReadOnlyCollection<ISceneNode>> groups = queries.GetAutomaticWithdrawalGroups(
                new[] { fleet, secondFleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.AreEqual(1, groups.Count);
            CollectionAssert.AreEquivalent(new ISceneNode[] { firstShip, secondShip }, groups[0]);
        }

        [Test]
        public void GetAutomaticWithdrawalGroups_OneFleetCannotWithdraw_ReturnsNoFleetGroup()
        {
            (GameRoot game, Planet planet, Fleet fleet, SpaceCombatQueries queries) =
                CreateScenario();
            CreatePlanet(game, "home", owner: "alliance");
            Fleet trappedFleet = AddFleet(game, planet, "trapped-fleet", "alliance");
            trappedFleet.GetChildren<CapitalShip>()[0].Hyperdrive = 0;

            List<IReadOnlyCollection<ISceneNode>> groups = queries.GetAutomaticWithdrawalGroups(
                new[] { fleet, trappedFleet },
                new List<Fleet>(),
                planet,
                "alliance"
            );

            Assert.IsEmpty(groups);
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
