using NUnit.Framework;
using Rebellion.AI.Core;
using Rebellion.AI.Fleets;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Core.Helpers;

namespace Rebellion.Tests.AI.Fleets
{
    [TestFixture]
    public class AIFleetProductionAllocationScorerTests
    {
        [Test]
        public void ScoreAssembly_WithDifferentCombatStrength_PrioritizesWeakerFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.PlanetsPerBattleFleet = 100;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1000;
            game.Config.AI.FleetDeployment.MinimumMobileCombatStrength = 1000;
            game.Config.AI.FleetDeployment.MobileCombatStrengthPerPlanet = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "planet", empire.InstanceID);
            Fleet weaker = AddFleet(game, planet, empire.InstanceID, "weaker", 100);
            Fleet stronger = AddFleet(game, planet, empire.InstanceID, "stronger", 1000);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double weakerScore = AIFleetProductionAllocationScorer.ScoreAssembly(context, weaker);
            double strongerScore = AIFleetProductionAllocationScorer.ScoreAssembly(
                context,
                stronger
            );

            Assert.Greater(weakerScore, strongerScore);
        }

        /// <summary>
        /// Adds a battle fleet with one completed capital ship.
        /// </summary>
        /// <param name="game">The game root.</param>
        /// <param name="planet">The fleet's planet.</param>
        /// <param name="ownerInstanceId">The owning faction identifier.</param>
        /// <param name="instanceId">The fleet identifier.</param>
        /// <param name="combatStrength">The capital ship's combat strength.</param>
        /// <returns>The created fleet.</returns>
        private static Fleet AddFleet(
            GameRoot game,
            Planet planet,
            string ownerInstanceId,
            string instanceId,
            int combatStrength
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(instanceId, ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    $"{instanceId}-ship",
                    ownerInstanceId,
                    combatStrength
                ),
                fleet
            );
            return fleet;
        }
    }
}
