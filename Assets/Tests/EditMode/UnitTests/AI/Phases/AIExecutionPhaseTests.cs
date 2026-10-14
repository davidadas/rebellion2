using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AIExecutionPhaseTests
    {
        [TestCase(
            ResearchDiscipline.ShipDesign,
            BuildingType.Shipyard,
            ManufacturingType.Ship,
            true
        )]
        [TestCase(
            ResearchDiscipline.ShipDesign,
            BuildingType.Shipyard,
            ManufacturingType.Ship,
            false
        )]
        [TestCase(
            ResearchDiscipline.FacilityDesign,
            BuildingType.ConstructionFacility,
            ManufacturingType.Building,
            true
        )]
        [TestCase(
            ResearchDiscipline.FacilityDesign,
            BuildingType.ConstructionFacility,
            ManufacturingType.Building,
            false
        )]
        [TestCase(
            ResearchDiscipline.TroopTraining,
            BuildingType.TrainingFacility,
            ManufacturingType.Troop,
            true
        )]
        [TestCase(
            ResearchDiscipline.TroopTraining,
            BuildingType.TrainingFacility,
            ManufacturingType.Troop,
            false
        )]
        public void Execute_MultipleResearchersInSameDiscipline_StartsBothMissions(
            ResearchDiscipline discipline,
            BuildingType facilityType,
            ManufacturingType manufacturingType,
            bool samePlanet
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet firstPlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "first-planet",
                faction.InstanceID
            );
            Planet secondPlanet = samePlanet
                ? firstPlanet
                : AITestSceneBuilder.AddPlanet(game, sector, "second-planet", faction.InstanceID);
            foreach (Planet planet in new[] { firstPlanet, secondPlanet }.Distinct())
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"{planet.InstanceID}-facility",
                    facilityType,
                    manufacturingType
                );
            faction.ResearchCatalog[discipline] = new List<ResearchCatalogEntry>
            {
                new ResearchCatalogEntry { Order = 1 },
            };
            Officer firstOfficer = EntityFactory.CreateOfficer("first-officer", faction.InstanceID);
            Officer secondOfficer = EntityFactory.CreateOfficer(
                "second-officer",
                faction.InstanceID
            );
            firstOfficer.SetBaseRating(Officer.GetRatingForResearchDiscipline(discipline), 60);
            secondOfficer.SetBaseRating(Officer.GetRatingForResearchDiscipline(discipline), 60);
            game.AttachNode(firstOfficer, firstPlanet);
            game.AttachNode(secondOfficer, secondPlanet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIMissionProposal first = new(
                new[] { firstOfficer },
                MissionTypeIDs.Research,
                firstPlanet,
                discipline: discipline
            );
            AIMissionProposal second = new(
                new[] { secondOfficer },
                MissionTypeIDs.Research,
                secondPlanet,
                discipline: discipline
            );
            first.SetScore(50);
            second.SetScore(40);
            Assert.IsTrue(first.CanExecute(context));
            Assert.IsTrue(second.CanExecute(context));
            context.AddProposals(new[] { first, second });
            new AISelectionPhase().Execute(context);

            new AIExecutionPhase().Execute(context);

            CollectionAssert.AreEquivalent(
                new[] { firstOfficer, secondOfficer },
                game.GetSceneNodesByType<ResearchMission>(mission =>
                        mission.OwnerInstanceID == faction.InstanceID
                        && mission.Discipline == discipline
                    )
                    .SelectMany(mission => mission.GetMainParticipants())
            );
        }

        [Test]
        public void Execute_WithSelectedExecutableProposal_ExecutesProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal proposal = new TestAIProposal(canExecute: true);
            context.SetSelectedProposals(new List<AIProposal> { proposal });

            new AIExecutionPhase().Execute(context);

            Assert.AreEqual(1, proposal.ExecuteCount);
        }

        [Test]
        public void Execute_WithSelectedNonExecutableProposal_DoesNotExecuteProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal proposal = new TestAIProposal(canExecute: false);
            context.SetSelectedProposals(new List<AIProposal> { proposal });

            new AIExecutionPhase().Execute(context);

            Assert.AreEqual(0, proposal.ExecuteCount);
        }

        [Test]
        public void ExecuteIncrementally_WithSelectedProposals_YieldsAfterEachProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal first = new TestAIProposal(canExecute: true);
            TestAIProposal second = new TestAIProposal(canExecute: true);
            context.SetSelectedProposals(new List<AIProposal> { first, second });

            IEnumerator<object> execution = new AIExecutionPhase()
                .ExecuteIncrementally(context)
                .GetEnumerator();

            Assert.IsTrue(execution.MoveNext());
            Assert.AreEqual(1, first.ExecuteCount);
            Assert.AreEqual(0, second.ExecuteCount);
            Assert.IsTrue(execution.MoveNext());
            Assert.AreEqual(1, second.ExecuteCount);
            Assert.IsFalse(execution.MoveNext());
        }

        [Test]
        public void ExecuteIncrementally_WithInvalidProposal_DoesNotExecuteProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal proposal = new TestAIProposal(canExecute: false);
            context.SetSelectedProposals(new List<AIProposal> { proposal });

            foreach (object _ in new AIExecutionPhase().ExecuteIncrementally(context)) { }

            Assert.AreEqual(0, proposal.ExecuteCount);
        }

        /// <summary>
        /// Creates context.
        /// </summary>
        /// <returns>The created context.</returns>
        private static AITurnContext CreateContext()
        {
            return new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );
        }
    }
}
