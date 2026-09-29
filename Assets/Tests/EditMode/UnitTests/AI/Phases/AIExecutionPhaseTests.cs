using System.Collections.Generic;
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
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AIExecutionPhaseTests
    {
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
        public void Execute_WithCommandAssignment_MovesAndAppointsOfficer()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, planet);
            game.AttachNode(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID), fleet);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.AllowedRanks = new[] { OfficerRank.General };
            officer.Ratings[SkillRating.Diplomacy] = 0;
            game.AttachNode(officer, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetCommandAssignments(
                new[] { (Officer: officer, Fleet: fleet, Rank: OfficerRank.General) }
            );

            new AIExecutionPhase().Execute(context);

            Assert.AreSame(fleet, officer.GetParentOfType<Fleet>());
            Assert.AreEqual(OfficerRank.General, officer.CurrentRank);
        }

        [Test]
        public void Execute_WithDiplomatCommandAssignment_DoesNotMoveOrAppointOfficer()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, planet);
            game.AttachNode(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID), fleet);
            Officer diplomat = EntityFactory.CreateOfficer("diplomat", empire.InstanceID);
            diplomat.AllowedRanks = new[] { OfficerRank.General };
            diplomat.Ratings[SkillRating.Diplomacy] = 100;
            game.AttachNode(diplomat, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetCommandAssignments(
                new[] { (Officer: diplomat, Fleet: fleet, Rank: OfficerRank.General) }
            );

            new AIExecutionPhase().Execute(context);

            Assert.AreSame(planet, diplomat.GetParentOfType<Planet>());
            Assert.AreEqual(OfficerRank.None, diplomat.CurrentRank);
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
