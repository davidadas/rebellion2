using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public class AIMissionPlannerTests
    {
        [Test]
        public void Plan_WithReconnaissanceTeam_AddsProposalForNearestUnexploredPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet nearTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "near-target",
                rebels.InstanceID,
                positionX: 10
            );
            AITestSceneBuilder.AddPlanet(
                game,
                system,
                "far-target",
                rebels.InstanceID,
                positionX: 100
            );
            SpecialForces reconnaissanceTeam = new SpecialForces
            {
                InstanceID = "recon",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { MissionTypeIDs.Reconnaissance },
            };
            game.AttachNode(reconnaissanceTeam, origin);
            game.Config.AI.MissionPlanning.ReconnaissanceCandidatePlanetLimit = 1;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIMissionPlanner().Plan(context);

            AIMissionProposal proposal = proposals
                .OfType<AIMissionProposal>()
                .Single(candidate => candidate.MissionTypeID == MissionTypeIDs.Reconnaissance);
            Assert.AreEqual(nearTarget.InstanceID, proposal.TargetPlanet.InstanceID);
        }

        [Test]
        public void Plan_WithNonMainRecruiter_DoesNotAddRecruitmentProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "p1", empire.InstanceID);
            Officer officer = CreateRecruiter("officer", empire.InstanceID, isMain: false);
            game.AttachNode(officer, planet);
            AddRecruitableOfficer(game, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIMissionPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIMissionProposal>()
                    .Any(proposal => proposal.MissionTypeID == MissionTypeIDs.Recruitment)
            );
        }

        [Test]
        public void Plan_WithMainRecruiter_AddsRecruitmentProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "p1", empire.InstanceID);
            Officer officer = CreateRecruiter("officer", empire.InstanceID, isMain: true);
            game.AttachNode(officer, planet);
            AddRecruitableOfficer(game, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIMissionPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIMissionProposal>()
                    .Any(proposal => proposal.MissionTypeID == MissionTypeIDs.Recruitment)
            );
        }

        [Test]
        public void Plan_WithKnownSabotageTarget_AddsExecutableTargetedProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Building building = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "target-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            SpecialForces participant = CreateSpecialForces(
                "saboteur",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate => candidate.MissionTypeID == MissionTypeIDs.Sabotage);

            Assert.AreEqual(building.InstanceID, proposal.SelectedTarget.InstanceID);
            Assert.IsTrue(proposal.CanExecute(context));

            proposal.Execute(context);

            SabotageMission mission = game.GetSceneNodesByType<SabotageMission>().Single();
            Assert.AreEqual(building.InstanceID, mission.SabotageTargetInstanceID);
        }

        [Test]
        public void Plan_WithActiveSabotageMission_ExcludesItsSelectedTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Building activeTarget = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "active-target",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Building availableTarget = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "available-target",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            SpecialForces activeParticipant = CreateSpecialForces(
                "active-saboteur",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            SpecialForces availableParticipant = CreateSpecialForces(
                "available-saboteur",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            game.AttachNode(activeParticipant, origin);
            game.AttachNode(availableParticipant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext initialContext = AITestSceneBuilder.CreateContext(game, empire);
            new AIMissionProposal(
                new[] { activeParticipant },
                MissionTypeIDs.Sabotage,
                target,
                selectedTarget: activeTarget
            ).Execute(initialContext);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal =>
                    proposal.MissionTypeID == MissionTypeIDs.Sabotage
                    && proposal.Participant == availableParticipant
                )
                .ToArray();

            Assert.IsFalse(
                proposals.Any(proposal =>
                    proposal.SelectedTarget.InstanceID == activeTarget.InstanceID
                )
            );
            Assert.IsTrue(
                proposals.Any(proposal =>
                    proposal.SelectedTarget.InstanceID == availableTarget.InstanceID
                )
            );
        }

        [Test]
        public void Plan_WithShieldBlockedAttack_PrioritizesBlockingShield()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet attackTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "attack-target",
                rebels.InstanceID
            );
            Planet largerTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "larger-target",
                rebels.InstanceID
            );
            AddShield(game, attackTarget, "shield-1", rebels.InstanceID);
            AddShield(game, attackTarget, "shield-2", rebels.InstanceID);
            for (int index = 0; index < 3; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    largerTarget,
                    $"shipyard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship
                );
            }

            Fleet attackFleet = EntityFactory.CreateFleet("attack-fleet", empire.InstanceID);
            attackFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = attackTarget.InstanceID,
            };
            game.AttachNode(attackFleet, origin);
            SpecialForces participant = CreateSpecialForces(
                "saboteur",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, attackTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, largerTarget);
            game.Config.AI.MissionPlanning.SabotageCandidatePlanetLimit = 1;
            game.Config.AI.MissionPlanning.SabotageTargetsPerPlanetLimit = 1;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate => candidate.MissionTypeID == MissionTypeIDs.Sabotage);

            Assert.AreEqual(attackTarget.InstanceID, proposal.TargetPlanet.InstanceID);
            Assert.AreEqual(
                DefenseFacilityClass.Shield,
                ((Building)proposal.SelectedTarget).DefenseFacilityClass
            );
        }

        [Test]
        public void Plan_WithShieldBlockedAttack_AssignsMissionDecoys()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AddShield(game, target, "shield-1", rebels.InstanceID);
            AddShield(game, target, "shield-2", rebels.InstanceID);

            Fleet attackFleet = EntityFactory.CreateFleet("attack-fleet", empire.InstanceID);
            attackFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(attackFleet, origin);

            SpecialForces leadSpy = CreateSpecialForces(
                "lead-spy",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            leadSpy.Ratings[OfficerRating.Espionage] = 90;
            SpecialForces spyDecoy = CreateSpecialForces(
                "spy-decoy",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            spyDecoy.Ratings[OfficerRating.Espionage] = 80;
            SpecialForces leadSaboteur = CreateSpecialForces(
                "lead-saboteur",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            leadSaboteur.Ratings[OfficerRating.Combat] = 90;
            leadSaboteur.Ratings[OfficerRating.Espionage] = 20;
            SpecialForces sabotageDecoy = CreateSpecialForces(
                "sabotage-decoy",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            sabotageDecoy.Ratings[OfficerRating.Combat] = 70;
            sabotageDecoy.Ratings[OfficerRating.Espionage] = 80;
            game.AttachNode(leadSpy, origin);
            game.AttachNode(spyDecoy, origin);
            game.AttachNode(leadSaboteur, origin);
            game.AttachNode(sabotageDecoy, origin);

            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick = game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .ToArray();

            AIMissionProposal espionageProposal = proposals.Single(proposal =>
                proposal.MissionTypeID == MissionTypeIDs.Espionage
                && proposal.Participant == leadSpy
            );
            AIMissionProposal[] sabotageProposals = proposals
                .Where(proposal =>
                    proposal.MissionTypeID == MissionTypeIDs.Sabotage
                    && proposal.Participant == leadSaboteur
                )
                .ToArray();

            CollectionAssert.AreEqual(new[] { spyDecoy }, espionageProposal.DecoyParticipants);
            Assert.IsNotEmpty(sabotageProposals);
            Assert.IsTrue(
                sabotageProposals.All(proposal =>
                    proposal.DecoyParticipants.SequenceEqual(new[] { sabotageDecoy })
                )
            );

            espionageProposal.Execute(context);

            EspionageMission mission = game.GetSceneNodesByType<EspionageMission>().Single();
            CollectionAssert.AreEqual(new[] { leadSpy }, mission.MainParticipants);
            CollectionAssert.AreEqual(new[] { spyDecoy }, mission.DecoyParticipants);
        }

        [Test]
        public void Plan_WithStaleEnemyIntel_AddsEspionageWithoutHostileMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "target-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            target.AddVisitor(empire.InstanceID);
            SpecialForces participant = CreateSpecialForces(
                "agent",
                empire.InstanceID,
                MissionTypeIDs.Espionage,
                MissionTypeIDs.Sabotage,
                MissionTypeIDs.InciteUprising
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick =
                game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks
                + game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .ToArray();

            Assert.IsTrue(
                proposals.Any(proposal => proposal.MissionTypeID == MissionTypeIDs.Espionage)
            );
            Assert.IsFalse(
                proposals.Any(proposal => proposal.MissionTypeID == MissionTypeIDs.Sabotage)
            );
            Assert.IsFalse(
                proposals.Any(proposal => proposal.MissionTypeID == MissionTypeIDs.InciteUprising)
            );
        }

        [Test]
        public void Plan_WithQualifiedTrainerAndStudent_AddsTeamTrainingProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "p1", empire.InstanceID);
            Officer trainer = CreateJedi("trainer", empire.InstanceID, 100, isTrainer: true);
            Officer student = CreateJedi("student", empire.InstanceID, 20, isTrainer: false);
            game.AttachNode(trainer, planet);
            game.AttachNode(student, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .ToArray();
            AIMissionProposal proposal = proposals.Single(candidate =>
                candidate.MissionTypeID == MissionTypeIDs.JediTraining
            );

            CollectionAssert.AreEquivalent(new[] { trainer, student }, proposal.Participants);
            Assert.IsTrue(proposal.CanExecute(context));
            Assert.IsFalse(
                proposals.Any(candidate =>
                    candidate != proposal && candidate.Participants.Contains(trainer)
                )
            );
        }

        [Test]
        public void Plan_WithQualifiedTrainerAndNoKnownStudent_AllowsOtherMissionProposals()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "p1", empire.InstanceID);
            planet.AddVisitor(empire.InstanceID);
            Officer trainer = CreateJedi("trainer", empire.InstanceID, 100, isTrainer: true);
            trainer.Ratings[OfficerRating.Diplomacy] = 100;
            game.AttachNode(trainer, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .ToArray();

            Assert.IsTrue(proposals.Any(proposal => proposal.Participants.Contains(trainer)));
        }

        [Test]
        public void Plan_WithQualifiedTrainerAndRemoteStudent_AllowsOtherMissionProposals()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet trainerPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "trainer-planet",
                empire.InstanceID
            );
            Planet studentPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "student-planet",
                empire.InstanceID
            );
            trainerPlanet.AddVisitor(empire.InstanceID);
            Officer trainer = CreateJedi("trainer", empire.InstanceID, 100, isTrainer: true);
            trainer.Ratings[OfficerRating.Diplomacy] = 100;
            Officer student = CreateJedi("student", empire.InstanceID, 20, isTrainer: false);
            game.AttachNode(trainer, trainerPlanet);
            game.AttachNode(student, studentPlanet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .ToArray();

            Assert.IsFalse(
                proposals.Any(proposal => proposal.MissionTypeID == MissionTypeIDs.JediTraining)
            );
            Assert.IsTrue(proposals.Any(proposal => proposal.Participants.Contains(trainer)));
        }

        [Test]
        public void Plan_WithCapturedFriendlyOfficer_AddsRescueProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet prison = AITestSceneBuilder.AddPlanet(game, system, "prison", rebels.InstanceID);
            Officer prisoner = EntityFactory.CreateOfficer("prisoner", empire.InstanceID);
            prisoner.IsCaptured = true;
            prisoner.CaptorInstanceID = rebels.InstanceID;
            game.AttachNode(prisoner, prison);
            SpecialForces participant = CreateSpecialForces(
                "rescuer",
                empire.InstanceID,
                MissionTypeIDs.Rescue
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, prison);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate => candidate.MissionTypeID == MissionTypeIDs.Rescue);

            Assert.AreEqual(prisoner.InstanceID, proposal.TargetOfficer.InstanceID);
            Assert.IsTrue(proposal.CanExecute(context));
        }

        [Test]
        public void Plan_WithCapturedFriendlyOfficerAboardFleet_AddsRescueProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet prison = AITestSceneBuilder.AddPlanet(game, system, "prison", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", rebels.InstanceID);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", rebels.InstanceID);
            Officer prisoner = EntityFactory.CreateOfficer("prisoner", empire.InstanceID);
            prisoner.IsCaptured = true;
            prisoner.CaptorInstanceID = rebels.InstanceID;
            game.AttachNode(fleet, prison);
            game.AttachNode(ship, fleet);
            game.AttachNode(prisoner, ship);
            SpecialForces participant = CreateSpecialForces(
                "rescuer",
                empire.InstanceID,
                MissionTypeIDs.Rescue
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, prison);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate => candidate.MissionTypeID == MissionTypeIDs.Rescue);

            Assert.AreEqual(prisoner.InstanceID, proposal.TargetOfficer.InstanceID);
            Assert.IsTrue(proposal.CanExecute(context));
        }

        [Test]
        public void Plan_WithOnlyOfficerAvailable_DoesNotAddRescueProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet prison = AITestSceneBuilder.AddPlanet(game, system, "prison", rebels.InstanceID);
            Officer prisoner = EntityFactory.CreateOfficer("prisoner", empire.InstanceID);
            prisoner.IsCaptured = true;
            prisoner.CaptorInstanceID = rebels.InstanceID;
            game.AttachNode(prisoner, prison);
            Officer rescuer = EntityFactory.CreateOfficer("rescuer", empire.InstanceID);
            game.AttachNode(rescuer, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, prison);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIMissionPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIMissionProposal>()
                    .Any(proposal => proposal.MissionTypeID == MissionTypeIDs.Rescue)
            );
        }

        [Test]
        public void Plan_WithCapturedOfficerInTransit_DoesNotAddRescueProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet prison = AITestSceneBuilder.AddPlanet(game, system, "prison", rebels.InstanceID);
            Officer prisoner = EntityFactory.CreateOfficer("prisoner", empire.InstanceID);
            prisoner.IsCaptured = true;
            prisoner.CaptorInstanceID = rebels.InstanceID;
            prisoner.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(prisoner, prison);
            SpecialForces participant = CreateSpecialForces(
                "rescuer",
                empire.InstanceID,
                MissionTypeIDs.Rescue
            );
            game.AttachNode(participant, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIMissionPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIMissionProposal>()
                    .Any(proposal => proposal.MissionTypeID == MissionTypeIDs.Rescue)
            );
        }

        [Test]
        public void Plan_WithAvailableResearch_AddsMatchingDisciplineProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "p1", empire.InstanceID);
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            empire.ResearchCatalog[ResearchDiscipline.ShipDesign] = new List<ResearchCatalogEntry>
            {
                new ResearchCatalogEntry { Order = 1 },
            };
            Officer researcher = EntityFactory.CreateOfficer("researcher", empire.InstanceID);
            researcher.ShipResearch = 60;
            game.AttachNode(researcher, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate =>
                    candidate.MissionTypeID == MissionTypeIDs.Research
                    && candidate.Discipline == ResearchDiscipline.ShipDesign
                );

            Assert.AreEqual(researcher, proposal.Participant);
            Assert.IsTrue(proposal.CanExecute(context));
        }

        private static Officer CreateRecruiter(
            string instanceId,
            string ownerInstanceId,
            bool isMain
        )
        {
            Officer officer = EntityFactory.CreateOfficer(instanceId, ownerInstanceId);
            officer.IsMain = isMain;
            officer.Ratings[OfficerRating.Leadership] = 100;
            officer.Ratings[OfficerRating.Diplomacy] = 0;
            officer.Ratings[OfficerRating.Combat] = 0;
            officer.Ratings[OfficerRating.Espionage] = 0;
            return officer;
        }

        private static Building AddShield(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId
        )
        {
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                instanceId,
                BuildingType.Defense
            );
            shield.OwnerInstanceID = ownerInstanceId;
            shield.DefenseFacilityClass = DefenseFacilityClass.Shield;
            shield.ShieldStrength = 10;
            game.AttachNode(shield, planet);
            return shield;
        }

        private static void AddRecruitableOfficer(GameRoot game, string ownerInstanceId)
        {
            Officer target = EntityFactory.CreateOfficer("recruitable", "neutral");
            target.AllowedOwnerInstanceIDs = new List<string> { ownerInstanceId };
            game.UnrecruitedOfficers.Add(target);
        }

        private static SpecialForces CreateSpecialForces(
            string instanceId,
            string ownerInstanceId,
            params string[] missionTypeIds
        )
        {
            return new SpecialForces
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = missionTypeIds.ToList(),
            };
        }

        private static Officer CreateJedi(
            string instanceId,
            string ownerInstanceId,
            int forceRank,
            bool isTrainer
        )
        {
            Officer officer = EntityFactory.CreateOfficer(instanceId, ownerInstanceId);
            officer.IsJedi = true;
            officer.IsKnownJedi = true;
            officer.IsForceEligible = true;
            officer.IsJediTrainer = isTrainer;
            officer.ForceValue = forceRank;
            return officer;
        }
    }
}
