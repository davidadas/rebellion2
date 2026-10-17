using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation.Combat
{
    [TestFixture]
    public class CombatPresentationQueriesTests
    {
        [Test]
        public void GetPendingCombat_EnemyFleetInTransit_ReturnsDetachedVisibleForcesOnly()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "player" });
            game.GetFactions().Add(new Faction { InstanceID = "enemy" });
            game.Summary.PlayerFactionID = "player";
            game.SetFactionController("player", "PLAYER1", PlayerControllerType.Human);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "enemy" };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            Fleet playerFleet = AddFleet(game, planet, "player-fleet", "player");
            Fleet enemyFleet = AddFleet(game, planet, "enemy-fleet", "enemy");
            Fleet hiddenFleet = AddFleet(game, planet, "hidden-fleet", "enemy");
            hiddenFleet.Movement = new MovementState { TransitTicks = 4 };
            using GameSession session = TestContent.CreateGameSession(game);
            session
                .GetService<SpaceCombatCommands>()
                .DeferCombatDecision(
                    new SpaceCombatDecision
                    {
                        AttackerFleetInstanceIDs = new List<string> { playerFleet.InstanceID },
                        DefenderFleetInstanceIDs = new List<string> { enemyFleet.InstanceID },
                        AttackerOwnerInstanceID = "player",
                        DefenderOwnerInstanceID = "enemy",
                        PlanetInstanceID = planet.InstanceID,
                    }
                );

            PendingCombatSnapshot visible = session.GetPendingCombatForPlayer();

            Assert.IsNotNull(visible);
            Assert.AreNotSame(planet, visible.Planet);
            Assert.AreNotSame(playerFleet, visible.AttackerFleet);
            Assert.AreNotSame(enemyFleet, visible.DefenderFleet);
            CollectionAssert.AreEquivalent(
                new[] { playerFleet.InstanceID, enemyFleet.InstanceID },
                visible.Planet.GetChildren<Fleet>().Select(fleet => fleet.InstanceID)
            );
        }

        /// <summary>
        /// Adds one operational fleet to a planet.
        /// </summary>
        /// <param name="game">The active game.</param>
        /// <param name="planet">The fleet location.</param>
        /// <param name="fleetInstanceId">The fleet identifier.</param>
        /// <param name="ownerInstanceId">The owning faction identifier.</param>
        /// <returns>The created fleet.</returns>
        private static Fleet AddFleet(
            GameRoot game,
            Planet planet,
            string fleetInstanceId,
            string ownerInstanceId
        )
        {
            Fleet fleet = new Fleet(ownerInstanceId, fleetInstanceId)
            {
                InstanceID = fleetInstanceId,
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = $"{fleetInstanceId}-ship",
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                Hyperdrive = 1,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return fleet;
        }
    }
}
