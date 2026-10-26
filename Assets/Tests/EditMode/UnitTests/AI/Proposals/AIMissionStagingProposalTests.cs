using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIMissionStagingProposalTests
    {
        [Test]
        public void CanSelect_WaitingForInboundLeader_ReturnsTrue()
        {
            var (context, waiting) = CreateWaitingContext();

            Assert.IsTrue(waiting.CanSelect(context));
            CollectionAssert.AreEqual(
                new[] { waiting.Mission.Participants[1] },
                waiting.Participants
            );
        }

        [Test]
        public void Execute_Waiting_PreservesInboundMovement()
        {
            var (context, waiting) = CreateWaitingContext();
            MovementState movement = waiting.Mission.Participants[0].Movement;

            waiting.Execute(context);

            Assert.AreSame(movement, waiting.Mission.Participants[0].Movement);
            Assert.AreEqual(0, movement.TicksElapsed);
            Assert.IsEmpty(context.Game.GetSceneNodesByType<Mission>());
        }

        [Test]
        public void CanSelect_AllTeammatesArrived_ReturnsFalse()
        {
            var (context, waiting) = CreateWaitingContext();
            waiting.Mission.Participants[0].Movement = null;

            Assert.IsFalse(waiting.CanSelect(context));
        }

        [Test]
        public void CanSelect_InboundTeammateRedirected_ReturnsFalse()
        {
            var (context, waiting) = CreateWaitingContext();
            context.Game.MoveNode(
                waiting.Mission.Participants[0],
                context.Game.GetSceneNodeByInstanceID<Planet>("first-origin")
            );

            Assert.IsFalse(waiting.CanSelect(context));
        }

        [Test]
        public void Execute_Waiting_KeepsPresentPersonnelEligibleForNextTurn()
        {
            var (context, waiting) = CreateWaitingContext();

            waiting.Execute(context);

            Assert.IsTrue(context.Faction.IsAvailableMissionParticipant(waiting.Participants[0]));
        }

        [Test]
        public void CanSelect_TeamAtDifferentOriginsAndShorterRoundTrip_ReturnsTrue()
        {
            var (context, proposal) = CreateStagingContext();

            Assert.IsTrue(proposal.CanSelect(context));
        }

        [Test]
        public void CanSelect_AlreadyAtDeparturePlanet_ReturnsFalse()
        {
            var (context, proposal) = CreateStagingContext();
            foreach (var participant in proposal.Mission.Participants)
                context.Game.MoveNode(participant, proposal.Destination);

            Assert.IsFalse(proposal.CanSelect(context));
        }

        [Test]
        public void CanSelect_DeparturePlanetCaptured_ReturnsFalse()
        {
            var (context, proposal) = CreateStagingContext();
            proposal.Destination.OwnerInstanceID = "rebels";

            Assert.IsFalse(proposal.CanSelect(context));
        }

        [Test]
        public void CanSelect_DeparturePlanetBlockaded_ReturnsFalse()
        {
            var (context, proposal) = CreateStagingContext();
            Fleet enemyFleet = EntityFactory.CreateFleet("blockade", "rebels");
            context.Game.AttachNode(enemyFleet, proposal.Destination);
            context.Game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("blockader", "rebels", 100),
                enemyFleet
            );

            Assert.IsFalse(proposal.CanSelect(context));
        }

        [Test]
        public void CanSelect_DetourCostsMoreThanDirectRoundTrip_ReturnsFalse()
        {
            var (context, proposal) = CreateStagingContext();
            proposal.Destination.PositionX = -1000;

            Assert.IsFalse(proposal.CanSelect(context));
        }

        [Test]
        public void Execute_TeamAtDifferentOrigins_MovesBothGroupsWithoutStartingMission()
        {
            var (context, proposal) = CreateStagingContext();

            proposal.Execute(context);

            foreach (var participant in proposal.Mission.Participants)
            {
                Assert.AreSame(proposal.Destination, participant.GetParent());
                Assert.IsNotNull(participant.Movement);
                Assert.IsNull(participant.MissionReturnLocationInstanceID);
            }
            Assert.IsEmpty(context.Game.GetSceneNodesByType<Mission>());
        }

        [Test]
        public void Execute_ParticipantAlreadyAtBase_MovesOnlyRemainingGroup()
        {
            var (context, proposal) = CreateStagingContext();
            var waiting = proposal.Mission.Participants[0];
            context.Game.MoveNode(waiting, proposal.Destination);

            proposal.Execute(context);

            Assert.IsNull(waiting.Movement);
            Assert.IsNotNull(proposal.Mission.Participants[1].Movement);
        }

        [Test]
        public void Execute_LocalTeammateStartsAnotherMission_StillRelocatesRemotePersonnel()
        {
            var (context, proposal) = CreateStagingContext();
            var local = proposal.Mission.Participants[0];
            context.Game.MoveNode(local, proposal.Destination);
            AIMissionStagingProposal relocation = new(proposal.Mission, proposal.Destination);
            EspionageMission otherMission = new() { InstanceID = "other-mission" };
            context.Game.AttachNode(otherMission, proposal.Destination);
            context.Game.MoveNode(local, otherMission);

            relocation.Execute(context);

            CollectionAssert.AreEqual(
                new[] { proposal.Mission.Participants[1] },
                relocation.Participants
            );
            Assert.AreSame(otherMission, local.GetParent());
            Assert.IsNotNull(relocation.Participants[0].Movement);
            Assert.AreSame(relocation.Destination, relocation.Participants[0].GetParent());
        }

        /// <summary>
        /// Creates present personnel and an inbound leader with a known arrival time.
        /// </summary>
        /// <returns>The faction turn and its one-turn waiting proposal.</returns>
        private static (
            AITurnContext Context,
            AIMissionStagingProposal Proposal
        ) CreateWaitingContext()
        {
            var (context, staging) = CreateStagingContext();
            foreach (var participant in staging.Mission.Participants)
                context.Game.MoveNode(participant, staging.Destination);
            staging.Mission.Participants[0].Movement = new MovementState { TransitTicks = 13 };
            return (
                context,
                AIMissionStagingProposal.WaitForArrivals(staging.Mission, staging.Destination)
            );
        }

        /// <summary>
        /// Creates a remote sabotage team with a friendly planet near the enemy objective.
        /// </summary>
        /// <returns>The current faction turn and its proposed team relocation.</returns>
        private static (
            AITurnContext Context,
            AIMissionStagingProposal Proposal
        ) CreateStagingContext()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet firstOrigin = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "first-origin",
                empire.InstanceID
            );
            Planet secondOrigin = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "second-origin",
                empire.InstanceID,
                positionX: 100
            );
            Planet departure = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "departure",
                empire.InstanceID,
                positionX: 900
            );
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "target",
                rebels.InstanceID,
                positionX: 910
            );
            Building facility = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "yard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Officer first = EntityFactory.CreateOfficer("first", empire.InstanceID);
            Officer second = EntityFactory.CreateOfficer("second", empire.InstanceID);
            game.AttachNode(first, firstOrigin);
            game.AttachNode(second, secondOrigin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AIMissionProposal mission = new(
                new[] { first },
                MissionTypeIDs.Sabotage,
                target,
                selectedTarget: facility,
                decoyParticipants: new[] { second },
                teamIsFinal: true
            );
            return (
                AITestSceneBuilder.CreateContext(game, empire),
                new AIMissionStagingProposal(mission, departure)
            );
        }
    }
}
