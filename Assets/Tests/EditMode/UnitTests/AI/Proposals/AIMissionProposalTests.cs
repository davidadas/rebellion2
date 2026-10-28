using System.Collections.Generic;
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
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIMissionProposalTests
    {
        [Test]
        public void CanSelect_WithCapturedOfficer_ReturnsFalse()
        {
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            officer.IsCaptured = true;
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "empire" };
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { officer },
                MissionTypeIDs.Diplomacy,
                planet
            );

            bool canSelect = proposal.CanSelect(null);

            Assert.IsFalse(canSelect);
        }

        [Test]
        public void CanSelect_WithParticipantInMovingFleet_ReturnsFalse()
        {
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            fleet.Movement = new MovementState();
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "empire",
            };
            fleet.AddChild(capitalShip);
            capitalShip.SetParent(fleet);
            capitalShip.AddChild(officer);
            officer.SetParent(capitalShip);
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "empire" };
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { officer },
                MissionTypeIDs.Diplomacy,
                planet
            );

            bool canSelect = proposal.CanSelect(null);

            Assert.IsFalse(canSelect);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CanSelect_ReservationAddedForParticipant_ReturnsFalse(bool reserveDecoy)
        {
            (GameRoot game, Planet _, Planet target, Officer main, FogOfWarCommands _) =
                MissionSceneBuilder.Build();
            Faction owner = game.GetFactions()
                .Find(faction => faction.InstanceID == main.OwnerInstanceID);
            Officer decoy = EntityFactory.CreateOfficer("decoy", owner.InstanceID);
            game.AttachNode(decoy, main.GetParent());
            target.AddVisitor(owner.InstanceID);
            HashSet<string> reserved = new();
            AITurnContext context = CreateContext(game, owner, reserved);
            AIMissionProposal proposal = new(
                new[] { main },
                MissionTypeIDs.Espionage,
                target,
                decoyParticipants: new[] { decoy }
            );
            Assert.IsTrue(proposal.CanSelect(context));
            reserved.Add(reserveDecoy ? decoy.InstanceID : main.InstanceID);

            bool canSelect = proposal.CanSelect(context);

            Assert.IsFalse(canSelect);
        }

        [Test]
        public void CanExecute_ReservationAddedAfterSelection_ReturnsFalse()
        {
            (GameRoot game, Planet _, Planet target, Officer officer, FogOfWarCommands _) =
                MissionSceneBuilder.Build();
            Faction owner = game.GetFactions()
                .Find(faction => faction.InstanceID == officer.OwnerInstanceID);
            target.AddVisitor(owner.InstanceID);
            HashSet<string> reserved = new();
            AITurnContext context = CreateContext(game, owner, reserved);
            AIMissionProposal proposal = new(new[] { officer }, MissionTypeIDs.Espionage, target);
            Assert.IsTrue(proposal.CanExecute(context));
            reserved.Add(officer.InstanceID);

            bool canExecute = proposal.CanExecute(context);

            Assert.IsFalse(canExecute);
        }

        /// <summary>
        /// Creates real mission services with a live synthetic reservation set.
        /// </summary>
        /// <param name="game">The fixture graph.</param>
        /// <param name="owner">The faction considering the mission.</param>
        /// <param name="reserved">The current AI reservation identities.</param>
        /// <returns>The AI context whose proposals recheck reservations.</returns>
        private static AITurnContext CreateContext(
            GameRoot game,
            Faction owner,
            HashSet<string> reserved
        )
        {
            AITurnContext services = AITestSceneBuilder.CreateContext(game, owner);
            AIAssessment assessment = new(game, owner, services.FactionView, reserved);
            return new AITurnContext(
                game,
                owner,
                services.Missions,
                services.Movement,
                services.Manufacturing,
                services.Bombardment,
                services.PlanetaryAssault,
                services.Random,
                assessment,
                new AIStrategicPlan(game, assessment),
                services.FactionView,
                services.Maintenance,
                services.Headquarters
            );
        }
    }
}
