using NUnit.Framework;
using Rebellion.AI.Core;
using Rebellion.AI.Production;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Core.Helpers;

namespace Rebellion.Tests.AI.Production
{
    [TestFixture]
    public sealed class AIProductionCapacityRequirementsTests
    {
        [Test]
        public void GetDesiredFacilityCount_WithShipyardRatio_ReturnsFactionWideRequirement()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerShipyard = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 5; index++)
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            int desired = new AIProductionCapacityRequirements().GetDesiredFacilityCount(
                context,
                BuildingType.Shipyard
            );

            Assert.AreEqual(3, desired);
        }

        [Test]
        public void GetDesiredFacilityCount_WithConstructionLaneFloor_PreservesMinimum()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerConstructionFacility = 100;
            game.Config.AI.Infrastructure.MinimumConstructionFacilityLanes = 4;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 5; index++)
                AITestSceneBuilder.AddPlanet(game, sector, $"planet-{index}", empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            int desired = new AIProductionCapacityRequirements().GetDesiredFacilityCount(
                context,
                BuildingType.ConstructionFacility
            );

            Assert.AreEqual(4, desired);
        }
    }
}
