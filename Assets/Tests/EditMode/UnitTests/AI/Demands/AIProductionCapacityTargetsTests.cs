using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Demands
{
    [TestFixture]
    public sealed class AIProductionCapacityTargetsTests
    {
        [Test]
        public void GetDesiredCount_WithShipyardRatio_ReturnsFactionWideRequirement()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerShipyard = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 5; index++)
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            int desired = AIProductionCapacityTargets.GetDesiredCount(
                context,
                BuildingType.Shipyard
            );

            Assert.AreEqual(3, desired);
        }

        [Test]
        public void GetDesiredCount_WithConstructionLaneFloor_PreservesMinimum()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerConstructionFacility = 100;
            game.Config.AI.Infrastructure.MinimumConstructionFacilityLanes = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 5; index++)
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            int desired = AIProductionCapacityTargets.GetDesiredCount(
                context,
                BuildingType.ConstructionFacility
            );

            Assert.AreEqual(4, desired);
        }
    }
}
