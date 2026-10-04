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
using OfficerRating = Rebellion.Game.Units.SkillRating;

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
                AllowedMissionTypeIDs = new List<string> { ReconnaissanceMission.MissionTypeID },
            };
            game.AttachNode(reconnaissanceTeam, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIMissionPlanner().Plan(context);

            AIMissionProposal proposal = proposals
                .OfType<AIMissionProposal>()
                .Where(candidate => candidate.MissionTypeID == ReconnaissanceMission.MissionTypeID)
                .OrderByDescending(candidate => candidate.Score)
                .First();
            Assert.AreEqual(nearTarget.InstanceID, proposal.TargetPlanet.InstanceID);
        }

        [Test]
        public void Plan_WithUnexploredOuterRimPlanet_AddsReconnaissanceProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector core = AITestSceneBuilder.AddSector(game, "core");
            PlanetSector outerRim = AITestSceneBuilder.AddSector(game, "outer-rim");
            outerRim.SectorType = PlanetSectorType.OuterRim;
            Planet origin = AITestSceneBuilder.AddPlanet(game, core, "origin", empire.InstanceID);
            AITestSceneBuilder.AddPlanet(
                game,
                core,
                "core-target",
                rebels.InstanceID,
                positionX: 100
            );
            Planet outerRimTarget = AITestSceneBuilder.AddPlanet(
                game,
                outerRim,
                "outer-rim-target",
                rebels.InstanceID,
                positionX: 10
            );
            SpecialForces reconnaissanceTeam = new SpecialForces
            {
                InstanceID = "recon",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { MissionTypeIDs.Reconnaissance },
            };
            game.AttachNode(reconnaissanceTeam, origin);

            List<AIMissionProposal> proposals = new AIMissionPlanner()
                .Plan(AITestSceneBuilder.CreateContext(game, empire))
                .OfType<AIMissionProposal>()
                .Where(candidate => candidate.MissionTypeID == MissionTypeIDs.Reconnaissance)
                .ToList();

            Assert.IsTrue(proposals.Count > 0);
            Assert.IsTrue(
                proposals.Any(proposal =>
                    proposal.TargetPlanet.InstanceID == outerRimTarget.InstanceID
                )
            );
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
                    .Any(proposal => proposal.MissionTypeID == RecruitmentMission.MissionTypeID)
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
                    .Any(proposal => proposal.MissionTypeID == RecruitmentMission.MissionTypeID)
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
            diplomat.Ratings[SkillRating.Diplomacy] = 100;
            Officer recruiter = CreateRecruiter("recruiter", empire.InstanceID, isMain: true);
            recruiter.Ratings[SkillRating.Diplomacy] = 20;
            game.AttachNode(diplomat, planet);
            game.AttachNode(recruiter, planet);
            AddRecruitableOfficer(game, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            string[] recruiterIds = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == RecruitmentMission.MissionTypeID)
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

            CollectionAssert.AreEqual(new[] { RecruitmentMission.MissionTypeID }, missionTypeIds);
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
                .Where(proposal => proposal.MissionTypeID == RecruitmentMission.MissionTypeID)
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
                SabotageMission.MissionTypeID
            );
            participant.Ratings[SkillRating.Espionage] = 100;
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .First(candidate => candidate.MissionTypeID == SabotageMission.MissionTypeID);

            Assert.AreEqual(building.InstanceID, proposal.SelectedTarget.InstanceID);
            Assert.IsTrue(proposal.CanExecute(context));

            proposal.Execute(context);

            SabotageMission mission = game.GetSceneNodesByType<SabotageMission>().Single();
            Assert.AreEqual(building.InstanceID, mission.SabotageTargetInstanceID);
        }

        [Test]
        public void Plan_WithDuplicateSabotageTargetTypes_OffersOneRepresentative()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Building first = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "shipyard-a",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Building second = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "shipyard-b",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            first.TypeID = "SHIPYARD";
            second.TypeID = first.TypeID;
            SpecialForces participant = CreateSpecialForces(
                "saboteur",
                empire.InstanceID,
                SabotageMission.MissionTypeID
            );
            participant.Ratings[SkillRating.Espionage] = 100;
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == SabotageMission.MissionTypeID)
                .ToArray();

            Assert.AreEqual(1, proposals.Length);
            Assert.AreEqual(first.InstanceID, proposals[0].SelectedTarget.InstanceID);
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
                activeMission.ConfigKey = InciteUprisingMission.MissionTypeID;
                game.AttachNode(activeMission, target);
            }

            SpecialForces participant = CreateSpecialForces(
                "saboteur",
                empire.InstanceID,
                SabotageMission.MissionTypeID
            );
            participant.Ratings[SkillRating.Espionage] = 100;
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(candidate => candidate.MissionTypeID == SabotageMission.MissionTypeID)
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
                SabotageMission.MissionTypeID
            );
            SpecialForces availableParticipant = CreateSpecialForces(
                "available-saboteur",
                empire.InstanceID,
                SabotageMission.MissionTypeID
            );
            availableParticipant.Ratings[SkillRating.Espionage] = 100;
            game.AttachNode(activeParticipant, origin);
            game.AttachNode(availableParticipant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext initialContext = AITestSceneBuilder.CreateContext(game, empire);
            new AIMissionProposal(
                new[] { activeParticipant },
                SabotageMission.MissionTypeID,
                target,
                selectedTarget: activeTarget
            ).Execute(initialContext);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal =>
                    proposal.MissionTypeID == SabotageMission.MissionTypeID
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
                SabotageMission.MissionTypeID
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, attackTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, largerTarget);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(candidate => candidate.MissionTypeID == SabotageMission.MissionTypeID)
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
                SabotageMission.MissionTypeID
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == SabotageMission.MissionTypeID)
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
                SabotageMission.MissionTypeID
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == SabotageMission.MissionTypeID)
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
                SabotageMission.MissionTypeID
            );
            participant.Ratings[SkillRating.Espionage] = 100;
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate => candidate.MissionTypeID == SabotageMission.MissionTypeID);

            Assert.AreEqual(regiment.InstanceID, proposal.SelectedTarget.InstanceID);
        }

        [Test]
        public void Plan_WithDecoyIntents_RetainsOneUnitAsPrimaryAgent()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            SpecialForces leadSpy = CreateSpecialForces(
                "lead-spy",
                empire.InstanceID,
                EspionageMission.MissionTypeID
            );
            leadSpy.Ratings[SkillRating.Espionage] = 90;
            SpecialForces specialForcesDecoy = CreateSpecialForces(
                "special-forces-decoy",
                empire.InstanceID,
                EspionageMission.MissionTypeID
            );
            specialForcesDecoy.Ratings[SkillRating.Espionage] = 60;
            Officer officerDecoy = EntityFactory.CreateOfficer("officer-decoy", empire.InstanceID);
            officerDecoy.Ratings[SkillRating.Espionage] = 100;
            game.AttachNode(leadSpy, origin);
            game.AttachNode(specialForcesDecoy, origin);
            game.AttachNode(officerDecoy, origin);

            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick = game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionPlanner.AssignSpecialForcesIntent(context);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(candidate => candidate.MissionTypeID == EspionageMission.MissionTypeID)
                .ToArray();

            Assert.AreEqual(
                SpecialForcesIntent.PrimaryAgent,
                context.GetSpecialForcesIntent(leadSpy)
            );
            Assert.AreEqual(
                SpecialForcesIntent.Decoy,
                context.GetSpecialForcesIntent(specialForcesDecoy)
            );
            Assert.IsTrue(proposals.Any(proposal => proposal.Participant == leadSpy));
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
                EspionageMission.MissionTypeID
            );
            Officer officerDecoy = EntityFactory.CreateOfficer("officer-decoy", empire.InstanceID);
            officerDecoy.Ratings[SkillRating.Espionage] = 80;
            game.AttachNode(leadSpy, origin);
            game.AttachNode(officerDecoy, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick = game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate =>
                    candidate.MissionTypeID == EspionageMission.MissionTypeID
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
            leadSpy.Ratings[SkillRating.Espionage] = 80;
            SpecialForces decoy = CreateSpecialForces(
                "decoy",
                empire.InstanceID,
                EspionageMission.MissionTypeID
            );
            SpecialForces primaryAgent = CreateSpecialForces(
                "primary-agent",
                empire.InstanceID,
                EspionageMission.MissionTypeID
            );
            decoy.Ratings[SkillRating.Espionage] = 60;
            primaryAgent.Ratings[SkillRating.Espionage] = 40;
            game.AttachNode(leadSpy, origin);
            game.AttachNode(decoy, origin);
            game.AttachNode(primaryAgent, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick = game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionPlanner.AssignSpecialForcesIntent(context);
            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate =>
                    candidate.MissionTypeID == EspionageMission.MissionTypeID
                    && candidate.Participant == leadSpy
                );
            context.SetSelectedProposals(new[] { proposal });
            new AIMissionSelector(new AISelectionState()).FinalizeSelection(context);
            proposal = context.SelectedProposals.OfType<AIMissionProposal>().Single();

            proposal.Execute(context);

            EspionageMission mission = game.GetSceneNodesByType<EspionageMission>().Single();
            CollectionAssert.AreEqual(new[] { leadSpy }, mission.GetMainParticipants());
            CollectionAssert.AreEqual(new[] { primaryAgent }, mission.GetDecoyParticipants());
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
                EspionageMission.MissionTypeID,
                SabotageMission.MissionTypeID,
                InciteUprisingMission.MissionTypeID
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
                proposals.Any(proposal => proposal.MissionTypeID == EspionageMission.MissionTypeID)
            );
            Assert.IsFalse(
                proposals.Any(proposal => proposal.MissionTypeID == SabotageMission.MissionTypeID)
            );
            Assert.IsFalse(
                proposals.Any(proposal =>
                    proposal.MissionTypeID == InciteUprisingMission.MissionTypeID
                )
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
                SabotageMission.MissionTypeID
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
                    proposal.MissionTypeID == SabotageMission.MissionTypeID
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
                CreateSpecialForces("spy-1", empire.InstanceID, EspionageMission.MissionTypeID),
                origin
            );
            game.AttachNode(
                CreateSpecialForces("spy-2", empire.InstanceID, EspionageMission.MissionTypeID),
                origin
            );
            AITestSceneBuilder.RevealPlanet(game, empire, firstTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, secondTarget);
            game.CurrentTick = game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            string[] targetIds = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == EspionageMission.MissionTypeID)
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
                candidate.MissionTypeID == JediTrainingMission.MissionTypeID
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
            trainer.Ratings[SkillRating.Diplomacy] = 100;
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
            trainer.Ratings[SkillRating.Diplomacy] = 100;
            Officer student = CreateJedi("student", empire.InstanceID, 20, isTrainer: false);
            game.AttachNode(trainer, trainerPlanet);
            game.AttachNode(student, studentPlanet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .ToArray();

            Assert.IsFalse(
                proposals.Any(proposal =>
                    proposal.MissionTypeID == JediTrainingMission.MissionTypeID
                )
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
                RescueMission.MissionTypeID
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, prison);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate => candidate.MissionTypeID == RescueMission.MissionTypeID);

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
                RescueMission.MissionTypeID
            );
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, prison);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Single(candidate => candidate.MissionTypeID == RescueMission.MissionTypeID);

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
                    .Any(proposal => proposal.MissionTypeID == RescueMission.MissionTypeID)
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
                RescueMission.MissionTypeID
            );
            game.AttachNode(participant, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIMissionPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIMissionProposal>()
                    .Any(proposal => proposal.MissionTypeID == RescueMission.MissionTypeID)
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
                    candidate.MissionTypeID == ResearchMission.MissionTypeID
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
            diplomat.Ratings[SkillRating.Diplomacy] = 100;
            game.AttachNode(diplomat, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, supportedTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, shipyardTarget);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal[] proposals = new AIMissionPlanner()
                .Plan(context)
                .OfType<AIMissionProposal>()
                .Where(candidate => candidate.MissionTypeID == DiplomacyMission.MissionTypeID)
                .ToArray();

            Assert.IsTrue(
                proposals.Any(proposal =>
                    proposal.TargetPlanet.InstanceID == shipyardTarget.InstanceID
                )
            );
        }

        [Test]
        public void Plan_WithHealthyMaintenance_DoesNotPrioritizeDiplomacyResources()
        {
            GameRoot game = CreateDiplomacyPriorityScene(
                maintenanceReserve: 0,
                out Faction empire,
                out Planet lexicalTarget,
                out Planet _
            );

            Planet firstTarget = new AIMissionPlanner()
                .Plan(AITestSceneBuilder.CreateContext(game, empire))
                .OfType<AIMissionProposal>()
                .First(proposal => proposal.MissionTypeID == MissionTypeIDs.Diplomacy)
                .TargetPlanet;

            Assert.AreEqual(lexicalTarget.InstanceID, firstTarget.InstanceID);
        }

        [Test]
        public void Plan_WithMaintenancePressure_PrioritizesDiplomacyResources()
        {
            GameRoot game = CreateDiplomacyPriorityScene(
                maintenanceReserve: int.MaxValue,
                out Faction empire,
                out Planet _,
                out Planet resourceTarget
            );

            Planet firstTarget = new AIMissionPlanner()
                .Plan(AITestSceneBuilder.CreateContext(game, empire))
                .OfType<AIMissionProposal>()
                .First(proposal => proposal.MissionTypeID == MissionTypeIDs.Diplomacy)
                .TargetPlanet;

            Assert.AreEqual(resourceTarget.InstanceID, firstTarget.InstanceID);
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
            diplomat.Ratings[SkillRating.Diplomacy] = 50;
            diplomat.Ratings[SkillRating.Espionage] = 100;
            diplomat.Ratings[SkillRating.Combat] = 100;
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

            CollectionAssert.AreEqual(new[] { DiplomacyMission.MissionTypeID }, missionTypeIds);
        }

        /// <summary>
        /// Creates two otherwise equal diplomacy targets whose identifiers and resource values
        /// expose whether maintenance pressure affects candidate priority.
        /// </summary>
        /// <param name="maintenanceReserve">The maintenance reserve used by AI selection.</param>
        /// <param name="empire">The AI faction created for the scene.</param>
        /// <param name="lexicalTarget">The target favored by the stable identifier tie-breaker.</param>
        /// <param name="resourceTarget">The target favored when resources have strategic value.</param>
        /// <returns>The configured game scene.</returns>
        private static GameRoot CreateDiplomacyPriorityScene(
            int maintenanceReserve,
            out Faction empire,
            out Planet lexicalTarget,
            out Planet resourceTarget
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = maintenanceReserve;
            game.Config.AI.MissionPlanning.RetainedAlternativesPerMission = 2;
            game.Config.AI.MissionPlanning.Utility.Diplomacy.ResourceNode.Weight = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            origin.SetPopularSupport(empire.InstanceID, 100);
            lexicalTarget = AITestSceneBuilder.AddPlanet(game, system, "a-target", null);
            resourceTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "z-resource-target",
                null,
                rawResourceNodes: 15
            );
            lexicalTarget.SetPopularSupport(empire.InstanceID, 50);
            resourceTarget.SetPopularSupport(empire.InstanceID, 50);
            Officer diplomat = EntityFactory.CreateOfficer("diplomat", empire.InstanceID);
            diplomat.Ratings[OfficerRating.Diplomacy] = 100;
            game.AttachNode(diplomat, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, lexicalTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, resourceTarget);
            return game;
        }

        /// <summary>
        /// Creates recruiter.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="isMain">Whether is main.</param>
        /// <returns>The created recruiter.</returns>
        private static Officer CreateRecruiter(
            string instanceId,
            string ownerInstanceId,
            bool isMain
        )
        {
            Officer officer = EntityFactory.CreateOfficer(instanceId, ownerInstanceId);
            officer.IsMain = isMain;
            officer.Ratings[SkillRating.Leadership] = 100;
            officer.Ratings[SkillRating.Diplomacy] = 0;
            officer.Ratings[SkillRating.Combat] = 0;
            officer.Ratings[SkillRating.Espionage] = 0;
            return officer;
        }

        /// <summary>
        /// Adds shield.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <returns>The result of add shield.</returns>
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

        /// <summary>
        /// Adds recruitable officer.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        private static void AddRecruitableOfficer(GameRoot game, string ownerInstanceId)
        {
            Officer target = EntityFactory.CreateOfficer("recruitable", "neutral");
            target.RecruitingFactionInstanceIDs = new List<string> { ownerInstanceId };
            game.GetUnrecruitedOfficers().Add(target);
        }

        /// <summary>
        /// Creates special forces.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="missionTypeIds">The mission type ids.</param>
        /// <returns>The created special forces.</returns>
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

        /// <summary>
        /// Creates jedi.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="forceRank">The force rank.</param>
        /// <param name="isTrainer">Whether is trainer.</param>
        /// <returns>The created jedi.</returns>
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
