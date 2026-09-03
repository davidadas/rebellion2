using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AIMissionDecoyAssignmentPhaseTests
    {
        [Test]
        public void Execute_WithOfficerLedHostileMission_AssignsDecoyIntentUnit()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            SpecialForces decoy = CreateSpecialForces("decoy", empire.InstanceID);
            game.AttachNode(officer, origin);
            game.AttachNode(decoy, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetSpecialForcesIntent(decoy, SpecialForcesIntent.Decoy);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { officer },
                MissionTypeIDs.Espionage,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionDecoyAssignmentPhase().Execute(context);

            AIMissionProposal selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .Single();
            CollectionAssert.AreEqual(new[] { decoy }, selected.DecoyParticipants);
        }

        [Test]
        public void Execute_WithSpecialForcesLedMission_DoesNotAssignDecoy()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            SpecialForces primary = CreateSpecialForces("primary", empire.InstanceID);
            SpecialForces decoy = CreateSpecialForces("decoy", empire.InstanceID);
            game.AttachNode(primary, origin);
            game.AttachNode(decoy, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetSpecialForcesIntent(primary, SpecialForcesIntent.PrimaryAgent);
            context.SetSpecialForcesIntent(decoy, SpecialForcesIntent.Decoy);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { primary },
                MissionTypeIDs.Espionage,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionDecoyAssignmentPhase().Execute(context);

            AIMissionProposal selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .Single();
            Assert.IsEmpty(selected.DecoyParticipants);
        }

        [Test]
        public void Execute_WithOfficerLedNeutralMission_DoesNotAssignDecoy()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", null);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            SpecialForces decoy = CreateSpecialForces("decoy", empire.InstanceID);
            game.AttachNode(officer, origin);
            game.AttachNode(decoy, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetSpecialForcesIntent(decoy, SpecialForcesIntent.Decoy);
            AIMissionProposal mission = new AIMissionProposal(
                new[] { officer },
                MissionTypeIDs.Diplomacy,
                target
            );
            mission.SetScore(50);
            context.SetSelectedProposals(new[] { mission });

            new AIMissionDecoyAssignmentPhase().Execute(context);

            AIMissionProposal selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .Single();
            Assert.IsEmpty(selected.DecoyParticipants);
        }

        [Test]
        public void Execute_WithScarceDecoy_AssignsHighestFoilRiskMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet firstTarget = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "first-target",
                rebels.InstanceID
            );
            Planet secondTarget = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "second-target",
                rebels.InstanceID
            );
            Officer firstOfficer = EntityFactory.CreateOfficer("first-officer", empire.InstanceID);
            Officer secondOfficer = EntityFactory.CreateOfficer(
                "second-officer",
                empire.InstanceID
            );
            SpecialForces decoy = CreateSpecialForces("decoy", empire.InstanceID);
            game.AttachNode(firstOfficer, origin);
            game.AttachNode(secondOfficer, origin);
            game.AttachNode(decoy, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetSpecialForcesIntent(decoy, SpecialForcesIntent.Decoy);
            AIMissionProposal lowerRisk = new AIMissionProposal(
                new[] { firstOfficer },
                MissionTypeIDs.Espionage,
                firstTarget
            );
            lowerRisk.SetScore(100);
            lowerRisk.SetFoilProbability(10);
            AIMissionProposal higherRisk = new AIMissionProposal(
                new[] { secondOfficer },
                MissionTypeIDs.Espionage,
                secondTarget
            );
            higherRisk.SetScore(50);
            higherRisk.SetFoilProbability(60);
            context.SetSelectedProposals(new[] { lowerRisk, higherRisk });

            new AIMissionDecoyAssignmentPhase().Execute(context);

            AIMissionProposal[] selected = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .ToArray();
            Assert.IsEmpty(
                selected.Single(proposal => proposal.TargetPlanet == firstTarget).DecoyParticipants
            );
            CollectionAssert.AreEqual(
                new[] { decoy },
                selected.Single(proposal => proposal.TargetPlanet == secondTarget).DecoyParticipants
            );
        }

        private static SpecialForces CreateSpecialForces(string instanceId, string ownerInstanceId)
        {
            return new SpecialForces
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { MissionTypeIDs.Espionage },
            };
        }
    }
}
