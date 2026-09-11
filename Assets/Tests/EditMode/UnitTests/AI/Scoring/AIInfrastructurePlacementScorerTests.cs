using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Scoring
{
    [TestFixture]
    public class AIInfrastructurePlacementScorerTests
    {
        [Test]
        public void SelectDestination_WithUnrepresentedSystem_PrefersSystemCoverage()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector developedSystem = AITestSceneBuilder.AddSector(game, "developed-system");
            PlanetSector uncoveredSystem = AITestSceneBuilder.AddSector(game, "uncovered-system");
            Planet existingHub = AITestSceneBuilder.AddPlanet(
                game,
                developedSystem,
                "existing-hub",
                empire.InstanceID
            );
            Planet uncoveredPlanet = AITestSceneBuilder.AddPlanet(
                game,
                uncoveredSystem,
                "uncovered-planet",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                existingHub,
                "existing-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.Game.Config.AI.Infrastructure.FacilitySystemCoverageWeight = 1000;

            Planet selected = new AIInfrastructurePlacementScorer(context).SelectDestination(
                new[] { existingHub, uncoveredPlanet },
                null,
                ManufacturingType.Ship,
                planet => planet.GetAvailableEnergy()
            );

            Assert.AreSame(uncoveredPlanet, selected);
        }

        [Test]
        public void SelectDestination_WithinRepresentedSystem_PrefersExistingHub()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet existingHub = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "existing-hub",
                empire.InstanceID
            );
            Planet undevelopedPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "undeveloped-planet",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                existingHub,
                "existing-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Planet selected = new AIInfrastructurePlacementScorer(context).SelectDestination(
                new[] { existingHub, undevelopedPlanet },
                null,
                ManufacturingType.Ship,
                planet => planet.GetAvailableEnergy()
            );

            Assert.AreSame(existingHub, selected);
        }

        [Test]
        public void SelectDestination_ForConstructionFacility_PrefersCompoundingHub()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector developedSystem = AITestSceneBuilder.AddSector(game, "developed-system");
            PlanetSector uncoveredSystem = AITestSceneBuilder.AddSector(game, "uncovered-system");
            Planet existingHub = AITestSceneBuilder.AddPlanet(
                game,
                developedSystem,
                "existing-hub",
                empire.InstanceID
            );
            Planet uncoveredPlanet = AITestSceneBuilder.AddPlanet(
                game,
                uncoveredSystem,
                "uncovered-planet",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                existingHub,
                "existing-construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Planet selected = new AIInfrastructurePlacementScorer(context).SelectDestination(
                new[] { existingHub, uncoveredPlanet },
                null,
                ManufacturingType.Building,
                planet => planet.GetAvailableEnergy()
            );

            Assert.AreSame(existingHub, selected);
        }

        [Test]
        public void SelectDestination_WithEquivalentShipyardSites_PreservesResourceWorld()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet resourceWorld = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "resource-world",
                empire.InstanceID,
                rawResourceNodes: 8
            );
            Planet industrialWorld = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "industrial-world",
                empire.InstanceID
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Planet selected = new AIInfrastructurePlacementScorer(context).SelectDestination(
                new[] { resourceWorld, industrialWorld },
                null,
                ManufacturingType.Ship,
                planet => planet.GetAvailableEnergy()
            );

            Assert.AreSame(industrialWorld, selected);
        }
    }
}
