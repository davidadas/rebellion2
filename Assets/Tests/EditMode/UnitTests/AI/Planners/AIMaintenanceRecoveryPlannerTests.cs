using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Planners;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public class AIMaintenanceRecoveryPlannerTests
    {
        /// <summary>
        /// Does not request retirement when the difficulty-adjusted budget is solvent.
        /// </summary>
        [Test]
        public void Plan_HardBonusCoversCommittedCosts_ReturnsNoProposals()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Summary = new GameSummary { Difficulty = GameDifficulty.Hard };
            game.SetFactionController(faction.InstanceID, "ai", PlayerControllerType.AI);
            game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                MaintenanceCapacityPercent = 125,
            };
            game.Config.AI.Selection.MaintenanceRecovery.Enabled = true;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "planet",
                faction.InstanceID,
                rawResourceNodes: 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "mine",
                BuildingType.Mine,
                ManufacturingType.None
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "refinery",
                BuildingType.Refinery,
                ManufacturingType.None
            );
            Regiment regiment = AITestSceneBuilder.CreateRegiment("regiment", faction.InstanceID);
            regiment.MaintenanceCost = 60;
            game.AttachNode(regiment, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);

            var proposals = new AIMaintenanceRecoveryPlanner().Plan(context);

            Assert.AreEqual(-10, faction.ProjectedMaintenanceHeadroom);
            Assert.IsEmpty(proposals);
        }
    }
}
