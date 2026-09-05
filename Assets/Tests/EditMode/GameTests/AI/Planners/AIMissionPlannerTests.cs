using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
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
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIMissionPlanner().Plan(context);

            AIMissionProposal proposal = proposals
                .OfType<AIMissionProposal>()
                .Where(candidate => candidate.MissionTypeID == MissionTypeIDs.Reconnaissance)
                .OrderByDescending(candidate => candidate.Score)
                .First();
            Assert.AreEqual(nearTarget.InstanceID, proposal.TargetPlanet.InstanceID);
        }

        [Test]
        public void Plan_WithNonMainRecruiter_DoesNotAddRecruitmentProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "p1",
                empire.InstanceID
            );
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
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "p1",
                empire.InstanceID
            );
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
        public void Plan_WithMultipleQualifiedRecruiters_UsesLowestDiplomacyOfficer()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "p1",
                empire.InstanceID
            );
            Officer diplomat = CreateRecruiter("diplomat", empire.InstanceID, isMain: true);
            diplomat.Ratings[OfficerRating.Diplomacy] = 100;
            Officer recruiter = CreateRecruiter("recruiter", empire.InstanceID, isMain: true);
            recruiter.Ratings[OfficerRating.Diplomacy] = 20;
            game.AttachNode(diplomat, planet);
            game.AttachNode(recruiter, planet);
            AddRecruitableOfficer(game, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            string[] recruiterIds = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == MissionTypeIDs.Recruitment)
                .Select(proposal => proposal.Participant.InstanceID)
                .Distinct()
                .ToArray();

            CollectionAssert.AreEqual(new[] { recruiter.InstanceID }, recruiterIds);
        }

        [Test]
        public void Plan_WithPreferredRecruiter_OnlyAssignsRecruitmentToOfficer()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "p1",
                empire.InstanceID
            );
            Officer recruiter = CreateRecruiter("recruiter", empire.InstanceID, isMain: true);
            game.AttachNode(recruiter, planet);
            AddRecruitableOfficer(game, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            string[] missionTypeIds = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.Participant == recruiter)
                .Select(proposal => proposal.MissionTypeID)
                .Distinct()
                .ToArray();

            CollectionAssert.AreEqual(new[] { MissionTypeIDs.Recruitment }, missionTypeIds);
        }

        [Test]
        public void Plan_WithLimitedRecruitmentFrontier_RetainsHighestScoringPlanets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet lowestSupport = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "lowest-support",
                empire.InstanceID
            );
            Planet lowSupport = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "low-support",
                empire.InstanceID
            );
            Planet highSupport = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "high-support",
                empire.InstanceID
            );
            Planet highestSupport = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "highest-support",
                empire.InstanceID
            );
            lowestSupport.SetPopularSupport(empire.InstanceID, 10);
            lowSupport.SetPopularSupport(empire.InstanceID, 30);
            highSupport.SetPopularSupport(empire.InstanceID, 70);
            highestSupport.SetPopularSupport(empire.InstanceID, 90);
            Officer officer = CreateRecruiter("officer", empire.InstanceID, isMain: true);
            game.AttachNode(officer, lowestSupport);
            AddRecruitableOfficer(game, empire.InstanceID);
            game.Config.AI.MissionPlanning.RetainedAlternativesPerMission = 2;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            string[] targetIds = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == MissionTypeIDs.Recruitment)
                .OrderByDescending(proposal => proposal.Score)
                .Select(proposal => proposal.TargetPlanet.InstanceID)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { highestSupport.InstanceID, highSupport.InstanceID },
                targetIds
            );
        }

        [Test]
        public void Plan_WithKnownSabotageTarget_AddsExecutableTargetedProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            participant.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .First(candidate => candidate.MissionTypeID == MissionTypeIDs.Sabotage);

            Assert.AreEqual(building.InstanceID, proposal.SelectedTarget.InstanceID);
            Assert.IsTrue(proposal.CanExecute(context));

            proposal.Execute(context);

            SabotageMission mission = game.GetSceneNodesByType<SabotageMission>().Single();
            Assert.AreEqual(building.InstanceID, mission.SabotageTargetInstanceID);
        }

        [Test]
        public void Plan_WithSeveralActiveHostileMissions_AddsAdditionalSabotageProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Building building = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "target-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            for (int index = 0; index < 3; index++)
            {
                StubMission activeMission = EntityFactory.CreateMission(
                    $"active-hostile-mission-{index}",
                    empire.InstanceID,
                    target.InstanceID
                );
                activeMission.ConfigKey = MissionTypeIDs.InciteUprising;
                game.AttachNode(activeMission, target);
            }

            SpecialForces participant = CreateSpecialForces(
                "saboteur",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            participant.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(candidate => candidate.MissionTypeID == MissionTypeIDs.Sabotage)
                .OrderByDescending(candidate => candidate.Score)
                .First();

            Assert.AreEqual(building.InstanceID, proposal.SelectedTarget.InstanceID);
        }

        [Test]
        public void Plan_WithActiveSabotageMission_ExcludesItsSelectedTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            availableParticipant.Ratings[OfficerRating.Espionage] = 100;
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
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(candidate => candidate.MissionTypeID == MissionTypeIDs.Sabotage)
                .OrderByDescending(candidate => candidate.Score)
                .First();

            Assert.AreEqual(attackTarget.InstanceID, proposal.TargetPlanet.InstanceID);
            Assert.IsTrue(((Building)proposal.SelectedTarget).IsPlanetaryShieldGenerator());
        }

        [Test]
        public void Plan_WithAttackPreparationTargets_OffersOnlyHighestPriorityTargets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(rebels.InstanceID, 40);
            target.SetPopularSupport(empire.InstanceID, 60);

            Building defense = AITestSceneBuilder.CreateBuildingTemplate(
                "defense",
                BuildingType.Weapon
            );
            defense.OwnerInstanceID = rebels.InstanceID;
            game.AttachNode(defense, target);
            Regiment regiment = AITestSceneBuilder.CreateRegiment("regiment", rebels.InstanceID);
            game.AttachNode(regiment, target);
            Starfighter starfighter = AITestSceneBuilder.CreateStarfighter(
                "starfighter",
                rebels.InstanceID
            );
            game.AttachNode(starfighter, target);
            Building shipyard = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );

            Fleet attackFleet = EntityFactory.CreateFleet("attack-fleet", empire.InstanceID);
            attackFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(attackFleet, origin);
            SpecialForces participant = CreateSpecialForces(
                "saboteur",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == MissionTypeIDs.Sabotage)
                .ToArray();

            Assert.AreEqual(1, proposals.Length);
            Assert.AreEqual(defense.InstanceID, proposals[0].SelectedTarget.InstanceID);
        }

        [Test]
        public void Plan_WithMixedSabotageTargets_OffersOnlyShieldGenerators()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Building shield = AddShield(game, target, "shield", rebels.InstanceID);
            Building battery = AITestSceneBuilder.CreateBuildingTemplate(
                "battery",
                BuildingType.Weapon
            );
            battery.OwnerInstanceID = rebels.InstanceID;
            game.AttachNode(battery, target);
            Regiment regiment = AITestSceneBuilder.CreateRegiment("regiment", rebels.InstanceID);
            game.AttachNode(regiment, target);
            SpecialForces participant = CreateSpecialForces(
                "saboteur",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == MissionTypeIDs.Sabotage)
                .ToArray();

            Assert.AreEqual(1, proposals.Length);
            Assert.AreEqual(shield.InstanceID, proposals[0].SelectedTarget.InstanceID);
        }

        [Test]
        public void Plan_WithOnlyGarrisonedRegiment_AddsSabotageProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Regiment regiment = AITestSceneBuilder.CreateRegiment("regiment", rebels.InstanceID);
            game.AttachNode(regiment, target);
            SpecialForces participant = CreateSpecialForces(
                "saboteur",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            participant.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate => candidate.MissionTypeID == MissionTypeIDs.Sabotage);

            Assert.AreEqual(regiment.InstanceID, proposal.SelectedTarget.InstanceID);
        }

        [Test]
        public void Plan_WithDecoyIntents_DoesNotOfferUnitsAsPrimaryAgents()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            SpecialForces leadSpy = CreateSpecialForces(
                "lead-spy",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            leadSpy.Ratings[OfficerRating.Espionage] = 90;
            SpecialForces specialForcesDecoy = CreateSpecialForces(
                "special-forces-decoy",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            specialForcesDecoy.Ratings[OfficerRating.Espionage] = 60;
            Officer officerDecoy = EntityFactory.CreateOfficer("officer-decoy", empire.InstanceID);
            officerDecoy.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(leadSpy, origin);
            game.AttachNode(specialForcesDecoy, origin);
            game.AttachNode(officerDecoy, origin);

            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick = game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            new AISpecialForcesIntentPhase().Execute(context);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(candidate => candidate.MissionTypeID == MissionTypeIDs.Espionage)
                .ToArray();

            Assert.AreEqual(SpecialForcesIntent.Decoy, context.GetSpecialForcesIntent(leadSpy));
            Assert.AreEqual(
                SpecialForcesIntent.Decoy,
                context.GetSpecialForcesIntent(specialForcesDecoy)
            );
            Assert.IsFalse(proposals.Any(proposal => proposal.Participant == leadSpy));
            Assert.IsFalse(proposals.Any(proposal => proposal.Participant == specialForcesDecoy));
        }

        [Test]
        public void Plan_WithoutQualifiedSpecialForces_DoesNotAssignOfficerDecoy()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            SpecialForces leadSpy = CreateSpecialForces(
                "lead-spy",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            Officer officerDecoy = EntityFactory.CreateOfficer("officer-decoy", empire.InstanceID);
            officerDecoy.Ratings[OfficerRating.Espionage] = 80;
            game.AttachNode(leadSpy, origin);
            game.AttachNode(officerDecoy, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick = game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate =>
                    candidate.MissionTypeID == MissionTypeIDs.Espionage
                    && candidate.Participant == leadSpy
                );

            Assert.IsEmpty(proposal.DecoyParticipants);
        }

        [Test]
        public void Execute_WithDecoy_CreatesMissionWithSeparateParticipantRoles()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Officer leadSpy = EntityFactory.CreateOfficer("lead-spy", empire.InstanceID);
            leadSpy.Ratings[OfficerRating.Espionage] = 80;
            SpecialForces decoy = CreateSpecialForces(
                "decoy",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            SpecialForces primaryAgent = CreateSpecialForces(
                "primary-agent",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            decoy.Ratings[OfficerRating.Espionage] = 60;
            primaryAgent.Ratings[OfficerRating.Espionage] = 40;
            game.AttachNode(leadSpy, origin);
            game.AttachNode(decoy, origin);
            game.AttachNode(primaryAgent, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick = game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            new AISpecialForcesIntentPhase().Execute(context);
            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate =>
                    candidate.MissionTypeID == MissionTypeIDs.Espionage
                    && candidate.Participant == leadSpy
                );
            context.SetSelectedProposals(new[] { proposal });
            new AIMissionDecoyAssignmentPhase().Execute(context);
            proposal = context.SelectedProposals.OfType<AIMissionProposal>().Single();

            proposal.Execute(context);

            EspionageMission mission = game.GetSceneNodesByType<EspionageMission>().Single();
            CollectionAssert.AreEqual(new[] { leadSpy }, mission.GetMainParticipants());
            CollectionAssert.AreEqual(new[] { decoy }, mission.GetDecoyParticipants());
        }

        [Test]
        public void Plan_WithStaleEnemyIntel_AddsEspionageWithoutHostileMission()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
        public void Plan_WithStaleShieldBlockedAttack_AddsSabotageProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AddShield(game, target, "shield-1", rebels.InstanceID);
            AddShield(game, target, "shield-2", rebels.InstanceID);
            Fleet attackFleet = EntityFactory.CreateFleet("attack-fleet", empire.InstanceID);
            attackFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(attackFleet, origin);
            SpecialForces participant = CreateSpecialForces(
                "saboteur",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick =
                game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks + 1;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .ToArray();

            Assert.IsTrue(
                proposals.Any(proposal =>
                    proposal.MissionTypeID == MissionTypeIDs.Sabotage
                    && proposal.TargetPlanet.InstanceID == target.InstanceID
                    && ((Building)proposal.SelectedTarget).IsPlanetaryShieldGenerator()
                )
            );
        }

        [Test]
        public void Plan_WithMultipleSpies_OffersEveryDistinctTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet firstTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "target-1",
                rebels.InstanceID
            );
            Planet secondTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "target-2",
                rebels.InstanceID
            );
            game.AttachNode(
                CreateSpecialForces("spy-1", empire.InstanceID, MissionTypeIDs.Espionage),
                origin
            );
            game.AttachNode(
                CreateSpecialForces("spy-2", empire.InstanceID, MissionTypeIDs.Espionage),
                origin
            );
            AITestSceneBuilder.RevealPlanet(game, empire, firstTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, secondTarget);
            game.CurrentTick = game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            string[] targetIds = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == MissionTypeIDs.Espionage)
                .Select(proposal => proposal.TargetPlanet.InstanceID)
                .Distinct()
                .OrderBy(instanceId => instanceId)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { firstTarget.InstanceID, secondTarget.InstanceID },
                targetIds
            );
        }

        [Test]
        public void Plan_WithQualifiedTrainerAndStudent_AddsTeamTrainingProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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

        [Test]
        public void Plan_WithMultipleDiplomacyTargets_OffersProductionInfrastructure()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            origin.SetPopularSupport(empire.InstanceID, 100);
            Planet supportedTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "supported-target",
                null
            );
            Planet shipyardTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-target",
                null
            );
            supportedTarget.SetPopularSupport(empire.InstanceID, 30);
            shipyardTarget.SetPopularSupport(empire.InstanceID, 10);
            AITestSceneBuilder.AddProductionFacility(
                game,
                shipyardTarget,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Officer diplomat = EntityFactory.CreateOfficer("diplomat", empire.InstanceID);
            diplomat.Ratings[OfficerRating.Diplomacy] = 100;
            game.AttachNode(diplomat, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, supportedTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, shipyardTarget);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(candidate => candidate.MissionTypeID == MissionTypeIDs.Diplomacy)
                .ToArray();

            Assert.IsTrue(
                proposals.Any(proposal =>
                    proposal.TargetPlanet.InstanceID == shipyardTarget.InstanceID
                )
            );
        }

        [Test]
        public void Plan_WithQualifiedDiplomatAndValidTarget_OffersOnlyDiplomacy()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet diplomacyTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "diplomacy-target",
                null
            );
            diplomacyTarget.SetPopularSupport(empire.InstanceID, 50);
            Planet sabotageTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "sabotage-target",
                rebels.InstanceID
            );
            AddShield(game, sabotageTarget, "shield", rebels.InstanceID);
            Officer diplomat = EntityFactory.CreateOfficer("diplomat", empire.InstanceID);
            diplomat.Ratings[OfficerRating.Diplomacy] = 50;
            diplomat.Ratings[OfficerRating.Espionage] = 100;
            diplomat.Ratings[OfficerRating.Combat] = 100;
            game.AttachNode(diplomat, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, diplomacyTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, sabotageTarget);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            string[] missionTypeIds = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.Participant == diplomat)
                .Select(proposal => proposal.MissionTypeID)
                .Distinct()
                .ToArray();

            CollectionAssert.AreEqual(new[] { MissionTypeIDs.Diplomacy }, missionTypeIds);
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
            shield.ShieldStrength = 10;
            game.AttachNode(shield, planet);
            return shield;
        }

        private static void AddRecruitableOfficer(GameRoot game, string ownerInstanceId)
        {
            Officer target = EntityFactory.CreateOfficer("recruitable", "neutral");
            target.RecruitingFactionInstanceIDs = new List<string> { ownerInstanceId };
            game.GetUnrecruitedOfficers().Add(target);
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
            officer.IsForceSensitive = true;
            officer.IsKnownJedi = true;
            officer.IsForceEligible = true;
            officer.IsJediTrainer = isTrainer;
            officer.ForceValue = forceRank;
            return officer;
        }
    }
}
