using System.Linq;
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

namespace Rebellion.Tests.AI.Scorers
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
            context.Game.Config.AI.Infrastructure.PlacementUtility.SystemCoverage.Weight = 1;

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

        [Test]
        public void RankHubDestinations_WithUnassignedAlternative_AvoidsMixingFacilityCategories()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet mixedPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "a-mixed",
                empire.InstanceID,
                energyCapacity: 10
            );
            Planet unassignedPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "z-unassigned",
                empire.InstanceID,
                energyCapacity: 10
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                mixedPlanet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Planet selected = new AIInfrastructurePlacementScorer(context)
                .RankHubDestinations(
                    new[] { mixedPlanet, unassignedPlanet },
                    null,
                    ManufacturingType.Ship,
                    BuildingType.Shipyard,
                    6,
                    planet => planet.GetAvailableEnergy()
                )
                .First();

            Assert.AreSame(unassignedPlanet, selected);
        }

        /// <summary>Reuses a same-type facility when both sites can reach the hub target.</summary>
        [Test]
        public void RankHubDestinations_WithMixedTargetFacility_PrefersExistingHub()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet existingHub = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "existing-hub",
                empire.InstanceID,
                energyCapacity: 10
            );
            Planet unassignedPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "unassigned",
                empire.InstanceID,
                energyCapacity: 10
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                existingHub,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                existingHub,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Planet selected = new AIInfrastructurePlacementScorer(context)
                .RankHubDestinations(
                    new[] { unassignedPlanet, existingHub },
                    null,
                    ManufacturingType.Ship,
                    BuildingType.Shipyard,
                    6,
                    planet => planet.GetAvailableEnergy()
                )
                .First();

            Assert.AreSame(existingHub, selected);
        }

        /// <summary>Prefers a larger achievable hub over an undersized site without other facility types.</summary>
        /// <param name="buildingType">The facility category being expanded.</param>
        /// <param name="manufacturingType">The production category supported by the facility.</param>
        /// <param name="targetCount">The preferred hub size.</param>
        [TestCase(BuildingType.ConstructionFacility, ManufacturingType.Building, 5)]
        [TestCase(BuildingType.Shipyard, ManufacturingType.Ship, 6)]
        [TestCase(BuildingType.TrainingFacility, ManufacturingType.Troop, 4)]
        public void RankHubDestinations_MixedSiteSupportsLargerHub_PrefersAchievableCapacity(
            BuildingType buildingType,
            ManufacturingType manufacturingType,
            int targetCount
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet smallSite = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "small-site",
                faction.InstanceID,
                energyCapacity: 3
            );
            Planet largeSite = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "large-site",
                faction.InstanceID,
                energyCapacity: 8
            );
            bool expandingConstruction = buildingType == BuildingType.ConstructionFacility;
            AITestSceneBuilder.AddProductionFacility(
                game,
                largeSite,
                "other-facility",
                expandingConstruction
                    ? BuildingType.TrainingFacility
                    : BuildingType.ConstructionFacility,
                expandingConstruction ? ManufacturingType.Troop : ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);

            Planet selected = new AIInfrastructurePlacementScorer(context)
                .RankHubDestinations(
                    new[] { smallSite, largeSite },
                    null,
                    manufacturingType,
                    buildingType,
                    targetCount,
                    planet => planet.GetAvailableEnergy()
                )
                .First();

            Assert.AreSame(largeSite, selected);
        }

        [Test]
        public void RankHubDestinations_WithExistingHubUnableToReachTarget_PrefersViableSite()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet constrainedHub = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "constrained-hub",
                empire.InstanceID,
                energyCapacity: 5
            );
            Planet viablePlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "viable",
                empire.InstanceID,
                energyCapacity: 10
            );
            for (int index = 0; index < 4; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    constrainedHub,
                    $"shipyard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship
                );
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Planet selected = new AIInfrastructurePlacementScorer(context)
                .RankHubDestinations(
                    new[] { constrainedHub, viablePlanet },
                    null,
                    ManufacturingType.Ship,
                    BuildingType.Shipyard,
                    6,
                    planet => planet.GetAvailableEnergy()
                )
                .First();

            Assert.AreSame(viablePlanet, selected);
        }
    }
}
