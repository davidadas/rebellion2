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
    public sealed class AIPlanetBuildingSpacePolicyTests
    {
        [Test]
        public void SelectBuildingToRemove_PlanetHasFreeCapacity_ReturnsNull()
        {
            AITurnContext context = CreateContext(energyCapacity: 2, out Planet planet, out _);

            Building selected = new AIPlanetBuildingSpacePolicy().SelectBuildingToRemove(
                context,
                planet
            );

            Assert.IsNull(selected);
        }

        [Test]
        public void SelectBuildingToRemove_UnfinishedBuildingExists_SelectsUnfinishedBuilding()
        {
            AITurnContext context = CreateContext(
                energyCapacity: 2,
                out Planet planet,
                out Building completed
            );
            Building unfinished = AITestSceneBuilder.CreateBuildingTemplate(
                "unfinished",
                BuildingType.Mine
            );
            unfinished.OwnerInstanceID = context.Faction.InstanceID;
            Assert.IsTrue(context.Manufacturing.Enqueue(planet, unfinished, planet));
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            Building selected = new AIPlanetBuildingSpacePolicy().SelectBuildingToRemove(
                context,
                planet
            );

            Assert.AreSame(unfinished, selected);
            Assert.AreNotSame(completed, selected);
        }

        [Test]
        public void SelectBuildingToRemove_DefenseAndResourceFacilities_SelectsDefense()
        {
            AITurnContext context = CreateContext(energyCapacity: 2, out Planet planet, out _);
            Building defense = AddBuilding(
                context.Game,
                planet,
                "defense",
                BuildingType.Defense,
                ManufacturingStatus.Complete
            );
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            Building selected = new AIPlanetBuildingSpacePolicy().SelectBuildingToRemove(
                context,
                planet
            );

            Assert.AreSame(defense, selected);
        }

        /// <summary>
        /// Creates an owned planet with one completed mine.
        /// </summary>
        /// <param name="energyCapacity">The planet's building capacity.</param>
        /// <param name="planet">Receives the planet.</param>
        /// <param name="mine">Receives the completed mine.</param>
        /// <returns>The configured AI turn context.</returns>
        private static AITurnContext CreateContext(
            int energyCapacity,
            out Planet planet,
            out Building mine
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "planet",
                empire.InstanceID,
                energyCapacity: energyCapacity
            );
            mine = AddBuilding(
                game,
                planet,
                "mine",
                BuildingType.Mine,
                ManufacturingStatus.Complete
            );
            return AITestSceneBuilder.CreateContext(game, empire);
        }

        /// <summary>
        /// Adds an owned building to a planet.
        /// </summary>
        /// <param name="game">The game graph.</param>
        /// <param name="planet">The destination planet.</param>
        /// <param name="instanceId">The building instance identifier.</param>
        /// <param name="buildingType">The building type.</param>
        /// <param name="status">The manufacturing status.</param>
        /// <returns>The attached building.</returns>
        private static Building AddBuilding(
            GameRoot game,
            Planet planet,
            string instanceId,
            BuildingType buildingType,
            ManufacturingStatus status
        )
        {
            Building building = AITestSceneBuilder.CreateBuildingTemplate(instanceId, buildingType);
            building.OwnerInstanceID = planet.OwnerInstanceID;
            building.ManufacturingStatus = status;
            game.AttachNode(building, planet);
            return building;
        }
    }
}
