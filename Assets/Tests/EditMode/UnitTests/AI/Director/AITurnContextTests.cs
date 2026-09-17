using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Director
{
    [TestFixture]
    public class AITurnContextTests
    {
        [Test]
        public void Constructor_WithFactionView_PreservesTurnInput()
        {
            GalaxyMap factionView = new GalaxyMap();

            AITurnContext context = new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                factionView
            );

            Assert.AreSame(factionView, context.FactionView);
        }

        [Test]
        public void AddProposal_WithNullProposal_DoesNotAddProposal()
        {
            AITurnContext context = new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );

            context.AddProposal(null);

            Assert.AreEqual(0, context.Proposals.Count);
        }

        [Test]
        public void SetSelectedProposals_WithNewBatch_ReplacesExistingSelection()
        {
            AITurnContext context = new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );
            TestAIProposal first = new TestAIProposal("first");
            TestAIProposal second = new TestAIProposal("second");
            context.SetSelectedProposals(new List<AIProposal> { first });

            context.SetSelectedProposals(new List<AIProposal> { null, second });

            Assert.AreEqual(1, context.SelectedProposals.Count);
            Assert.AreSame(second, context.SelectedProposals[0]);
        }

        [Test]
        public void AddResult_WithNullResult_DoesNotAddResult()
        {
            AITurnContext context = new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );

            context.AddResult(null);

            Assert.AreEqual(0, context.Results.Count);
        }

        [Test]
        public void AddResults_WithResultBatch_AddsNonNullResults()
        {
            AITurnContext context = new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );
            BlockadeChangedResult result = new BlockadeChangedResult();

            context.AddResults(new GameResult[] { null, result });

            Assert.AreEqual(1, context.Results.Count);
            Assert.AreSame(result, context.Results[0]);
        }

        [Test]
        public void DevelopmentAllocation_ReservesIncompleteHubEnergyFromMines()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.FacilitySectorHubTargetCount = 5;
            game.Config.AI.Infrastructure.FacilityPlanetsPerSector = 1;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet hub = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                empire.InstanceID,
                energyCapacity: 5,
                rawResourceNodes: 5
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                hub,
                "yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            int mineEnergy = context.DevelopmentAllocation.GetAvailableEnergy(
                hub,
                BuildingType.Mine
            );
            int constructionEnergy = context.DevelopmentAllocation.GetAvailableEnergy(
                hub,
                BuildingType.ConstructionFacility
            );

            Assert.AreEqual(0, mineEnergy);
            Assert.AreEqual(3, constructionEnergy);
        }

        [Test]
        public void DevelopmentAllocation_UsesDistinctPrimaryPlanetsWhenInvestmentsAreEqual()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.FacilitySectorHubTargetCount = 5;
            game.Config.AI.Infrastructure.ShipyardSectorHubTargetCount = 5;
            game.Config.AI.Infrastructure.FacilityPlanetsPerSector = 1;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet first = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "first",
                empire.InstanceID,
                energyCapacity: 5
            );
            Planet second = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "second",
                empire.InstanceID,
                energyCapacity: 5
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            bool firstConstruction = context.DevelopmentAllocation.IsPrimaryHub(
                first,
                BuildingType.ConstructionFacility
            );
            bool firstShipyard = context.DevelopmentAllocation.IsPrimaryHub(
                first,
                BuildingType.Shipyard
            );

            Assert.IsTrue(firstConstruction);
            Assert.IsFalse(firstShipyard);
            Assert.IsTrue(
                context.DevelopmentAllocation.IsPrimaryHub(second, BuildingType.Shipyard)
            );
        }

        /// <summary>
        /// Verifies a full Outer Rim seed planet assigns construction growth to a feasible local planet.
        /// </summary>
        [Test]
        public void DevelopmentAllocation_FullOuterRimSeed_UsesFeasibleLocalConstructionHub()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.FacilitySectorHubTargetCount = 5;
            game.Config.AI.Infrastructure.FacilityPlanetsPerSector = 1;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "outer-rim");
            sector.SectorType = PlanetSectorType.OuterRim;
            Planet seed = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "seed",
                empire.InstanceID,
                energyCapacity: 5
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                seed,
                "yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            foreach (
                BuildingType type in new[]
                {
                    BuildingType.Headquarters,
                    BuildingType.Mine,
                    BuildingType.Refinery,
                    BuildingType.Defense,
                }
            )
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    seed,
                    type.ToString(),
                    type,
                    ManufacturingType.None
                );
            }

            Planet expansion = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "expansion",
                empire.InstanceID,
                energyCapacity: 7
            );
            expansion.IsColonized = false;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.IsFalse(
                context.DevelopmentAllocation.IsPrimaryHub(seed, BuildingType.ConstructionFacility)
            );
            Assert.IsTrue(
                context.DevelopmentAllocation.IsPrimaryHub(
                    expansion,
                    BuildingType.ConstructionFacility
                )
            );
            Assert.IsTrue(
                context.DevelopmentAllocation.HasIncompletePrimaryHub(
                    sector.InstanceID,
                    BuildingType.ConstructionFacility
                )
            );
        }

        [Test]
        public void DevelopmentAllocation_UsesConfiguredHubUtility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            GameConfig.AIInfrastructureAllocationUtilityConfig utility = game.Config
                .AI
                .Infrastructure
                .AllocationUtility;
            utility.ExistingFacilities.Weight = 0;
            utility.UnassignedHub.Weight = 0;
            utility.FeasibleCapacity.Weight = 0;
            utility.StrategicValue.Weight = 1;
            game.Config.AI.Infrastructure.FacilityPlanetsPerSector = 1;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet lowValue = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "a-low-value",
                empire.InstanceID,
                energyCapacity: 1
            );
            Planet highValue = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "z-high-value",
                empire.InstanceID,
                energyCapacity: 5
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.IsFalse(
                context.DevelopmentAllocation.IsPrimaryHub(
                    lowValue,
                    BuildingType.ConstructionFacility
                )
            );
            Assert.IsTrue(
                context.DevelopmentAllocation.IsPrimaryHub(
                    highValue,
                    BuildingType.ConstructionFacility
                )
            );
        }
    }
}
