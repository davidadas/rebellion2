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

        [Test]
        public void SelectBuildingToRemove_RequiredProductionAndHealthyEconomy_SelectsResourceFacility()
        {
            AITurnContext context = CreateEconomyTradeoffContext(
                maintenanceHeadroomTarget: 25,
                refinedMaterialStockpile: 100,
                out Planet planet,
                out Building requiredShipyard
            );

            Building selected = new AIPlanetBuildingSpacePolicy().SelectBuildingToRemove(
                context,
                planet
            );

            Assert.AreNotSame(requiredShipyard, selected);
            Assert.That(
                selected.GetBuildingType(),
                Is.EqualTo(BuildingType.Mine).Or.EqualTo(BuildingType.Refinery)
            );
        }

        [Test]
        public void SelectBuildingToRemove_RequiredProductionAndConstrainedEconomy_SelectsProductionFacility()
        {
            AITurnContext context = CreateEconomyTradeoffContext(
                maintenanceHeadroomTarget: 100,
                refinedMaterialStockpile: 100,
                out Planet planet,
                out Building requiredShipyard
            );

            Building selected = new AIPlanetBuildingSpacePolicy().SelectBuildingToRemove(
                context,
                planet
            );

            Assert.AreSame(requiredShipyard, selected);
        }

        [Test]
        public void SelectBuildingToRemove_RequiredProductionAndScarceMaterials_SelectsProductionFacility()
        {
            AITurnContext context = CreateEconomyTradeoffContext(
                maintenanceHeadroomTarget: 25,
                refinedMaterialStockpile: 0,
                out Planet planet,
                out Building requiredShipyard
            );

            Building selected = new AIPlanetBuildingSpacePolicy().SelectBuildingToRemove(
                context,
                planet
            );

            Assert.AreSame(requiredShipyard, selected);
        }

        [Test]
        public void SelectBuildingToRemove_ActiveMineBackedOnlyByUnfinishedMine_PreservesActiveMine()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomTarget = 25;
            game.Config.AI.Selection.RefinedMaterialEconomyWarningPercent = 40;
            empire.RefinedMaterialStockpile = 100;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                empire.InstanceID,
                energyCapacity: 3,
                rawResourceNodes: 1
            );
            Building shipyard = AddBuilding(
                game,
                destination,
                "required-shipyard",
                BuildingType.Shipyard,
                ManufacturingStatus.Complete,
                ManufacturingType.Ship
            );
            AddBuilding(
                game,
                destination,
                "active-mine",
                BuildingType.Mine,
                ManufacturingStatus.Complete,
                ManufacturingType.None
            );
            AddBuilding(
                game,
                destination,
                "active-refinery",
                BuildingType.Refinery,
                ManufacturingStatus.Complete,
                ManufacturingType.None
            );
            Planet futureMinePlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "future-mine-planet",
                empire.InstanceID,
                rawResourceNodes: 1
            );
            Building unfinishedMine = AddBuilding(
                game,
                futureMinePlanet,
                "unfinished-mine",
                BuildingType.Mine,
                ManufacturingStatus.Building,
                ManufacturingType.None
            );
            futureMinePlanet.AddToManufacturingQueue(unfinishedMine);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Building selected = new AIPlanetBuildingSpacePolicy().SelectBuildingToRemove(
                context,
                destination
            );

            Assert.AreSame(shipyard, selected);
        }

        [Test]
        public void SelectBuildingToRemove_EquivalentSurplusProduction_SelectsSlowerFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "planet",
                empire.InstanceID,
                energyCapacity: 2
            );
            Building fastShipyard = AddBuilding(
                game,
                planet,
                "fast-shipyard",
                BuildingType.Shipyard,
                ManufacturingStatus.Complete,
                ManufacturingType.Ship,
                processRate: 1
            );
            Building slowShipyard = AddBuilding(
                game,
                planet,
                "slow-shipyard",
                BuildingType.Shipyard,
                ManufacturingStatus.Complete,
                ManufacturingType.Ship,
                processRate: 10
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Building selected = new AIPlanetBuildingSpacePolicy().SelectBuildingToRemove(
                context,
                planet
            );

            Assert.AreSame(slowShipyard, selected);
            Assert.AreNotSame(fastShipyard, selected);
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
        /// Creates a full planet where one required shipyard competes with a balanced resource
        /// economy for the relocation slot.
        /// </summary>
        /// <param name="maintenanceHeadroomTarget">The headroom that must remain after removal.</param>
        /// <param name="refinedMaterialStockpile">The faction's available refined materials.</param>
        /// <param name="planet">Receives the full destination planet.</param>
        /// <param name="requiredShipyard">Receives the faction's required shipyard.</param>
        /// <returns>The configured AI turn context.</returns>
        private static AITurnContext CreateEconomyTradeoffContext(
            int maintenanceHeadroomTarget,
            int refinedMaterialStockpile,
            out Planet planet,
            out Building requiredShipyard
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomTarget = maintenanceHeadroomTarget;
            game.Config.AI.Selection.RefinedMaterialEconomyWarningPercent = 40;
            empire.RefinedMaterialStockpile = refinedMaterialStockpile;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "planet",
                empire.InstanceID,
                energyCapacity: 5,
                rawResourceNodes: 2
            );
            requiredShipyard = AddBuilding(
                game,
                planet,
                "required-shipyard",
                BuildingType.Shipyard,
                ManufacturingStatus.Complete,
                ManufacturingType.Ship
            );
            AddBuilding(
                game,
                planet,
                "mine-a",
                BuildingType.Mine,
                ManufacturingStatus.Complete,
                ManufacturingType.None
            );
            AddBuilding(
                game,
                planet,
                "mine-b",
                BuildingType.Mine,
                ManufacturingStatus.Complete,
                ManufacturingType.None
            );
            AddBuilding(
                game,
                planet,
                "refinery-a",
                BuildingType.Refinery,
                ManufacturingStatus.Complete,
                ManufacturingType.None
            );
            AddBuilding(
                game,
                planet,
                "refinery-b",
                BuildingType.Refinery,
                ManufacturingStatus.Complete,
                ManufacturingType.None
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
        /// <param name="productionType">The manufacturing lane supplied by the building.</param>
        /// <param name="processRate">The building's production interval.</param>
        /// <returns>The attached building.</returns>
        private static Building AddBuilding(
            GameRoot game,
            Planet planet,
            string instanceId,
            BuildingType buildingType,
            ManufacturingStatus status,
            ManufacturingType productionType = ManufacturingType.Building,
            int processRate = 1
        )
        {
            Building building = AITestSceneBuilder.CreateBuildingTemplate(
                instanceId,
                buildingType,
                productionType
            );
            building.OwnerInstanceID = planet.OwnerInstanceID;
            building.ManufacturingStatus = status;
            building.ProcessRate = processRate;
            game.AttachNode(building, planet);
            return building;
        }
    }
}
