using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Scoring
{
    [TestFixture]
    public class AIMissionProposalScorerTests
    {
        [Test]
        public void Score_DiplomacyProposal_ReturnsHigherScoreForLowerSupportPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            lowSupport.SetPopularSupport(empire.InstanceID, 10);
            highSupport.SetPopularSupport(empire.InstanceID, 90);
            lowSupport.AddVisitor(empire.InstanceID);
            highSupport.AddVisitor(empire.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[OfficerRating.Diplomacy] = 100;
            game.AttachNode(officer, lowSupport);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double lowSupportScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Diplomacy, lowSupport)
            );
            double highSupportScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Diplomacy, highSupport)
            );

            Assert.Greater(lowSupportScore, highSupportScore);
        }

        [Test]
        public void Score_RecruitmentProposal_ReturnsHigherScoreForHigherSupportPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            lowSupport.SetPopularSupport(empire.InstanceID, 20);
            highSupport.SetPopularSupport(empire.InstanceID, 80);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.IsMain = true;
            officer.Ratings[OfficerRating.Leadership] = 80;
            game.AttachNode(officer, lowSupport);
            Officer recruit = EntityFactory.CreateOfficer("recruit", null);
            recruit.RecruitingFactionInstanceIDs = new List<string> { empire.InstanceID };
            game.GetUnrecruitedOfficers().Add(recruit);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double lowSupportScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Recruitment, lowSupport)
            );
            double highSupportScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Recruitment, highSupport)
            );

            Assert.Greater(highSupportScore, lowSupportScore);
        }

        [Test]
        public void Score_ReconnaissanceProposal_WithDistantTarget_RemainsSelectable()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "target",
                null,
                positionX: 600
            );
            SpecialForces probe = AITestSceneBuilder.CreateSpecialForces(
                "probe",
                empire.InstanceID
            );
            probe.AllowedMissionTypeIDs.Add(MissionTypeIDs.Reconnaissance);
            probe.Ratings[OfficerRating.Espionage] = 30;
            game.AttachNode(probe, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double score = new AIMissionProposalScorer().Score(
                context,
                new AIMissionProposal(new[] { probe }, MissionTypeIDs.Reconnaissance, target)
            );

            Assert.Greater(score, 0);
        }

        [Test]
        public void Score_SubdueUprisingBelowProbabilityFloor_ReturnsZeroDespitePriorityBonus()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "uprising",
                empire.InstanceID
            );
            planet.BeginUprising();
            planet.SetPopularSupport(empire.InstanceID, 0);
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "participant",
                empire.InstanceID
            );
            participant.AllowedMissionTypeIDs.Add(MissionTypeIDs.SubdueUprising);
            participant.Ratings[OfficerRating.Leadership] = 0;
            game.AttachNode(participant, planet);
            game.Config.ProbabilityTables.Mission.SubdueUprising = new Dictionary<int, int>
            {
                { -1000, 19 },
            };
            game.Config.AI.MissionPlanning.MinimumUprisingMissionSuccessPercent = 20;
            game.Config.AI.MissionPlanning.SubdueUprisingPriorityBonus = 120;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIMissionProposal proposal = new AIMissionProposal(
                new[] { participant },
                MissionTypeIDs.SubdueUprising,
                planet
            );

            Assert.IsTrue(proposal.CanExecute(context));
            MissionOdds odds = context.Missions.GetMissionOdds(proposal.CreateRequest());
            Assert.IsNotNull(odds);
            Assert.AreEqual(19, odds.ObjectiveSuccessProbability, 0.0001);
            double score = new AIMissionProposalScorer().Score(context, proposal);

            Assert.AreEqual(0, score);
        }

        [Test]
        public void GetScoreUpperBound_ExecutableProposal_DoesNotUnderestimateScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.AddVisitor(empire.InstanceID);
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "participant",
                empire.InstanceID
            );
            participant.AllowedMissionTypeIDs.Add(MissionTypeIDs.Espionage);
            participant.Ratings[OfficerRating.Espionage] = 60;
            game.AttachNode(participant, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { participant },
                MissionTypeIDs.Espionage,
                target
            );
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double score = scorer.Score(context, proposal);
            double upperBound = scorer.GetScoreUpperBound(context, proposal);

            Assert.GreaterOrEqual(upperBound, score);
        }

        [Test]
        public void Score_ReconnaissanceProposal_IgnoresParticipantRating()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            SpecialForces lowRatedProbe = AITestSceneBuilder.CreateSpecialForces(
                "low-probe",
                empire.InstanceID
            );
            SpecialForces highRatedProbe = AITestSceneBuilder.CreateSpecialForces(
                "high-probe",
                empire.InstanceID
            );
            lowRatedProbe.Ratings[OfficerRating.Espionage] = 1;
            highRatedProbe.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(lowRatedProbe, origin);
            game.AttachNode(highRatedProbe, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double lowRatedScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { lowRatedProbe },
                    MissionTypeIDs.Reconnaissance,
                    target
                )
            );
            double highRatedScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { highRatedProbe },
                    MissionTypeIDs.Reconnaissance,
                    target
                )
            );

            Assert.AreEqual(lowRatedScore, highRatedScore);
        }

        [Test]
        public void Score_MultipleMainParticipants_UsesCombinedMissionSuccessProbability()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.AddVisitor(empire.InstanceID);
            SpecialForces firstParticipant = AITestSceneBuilder.CreateSpecialForces(
                "first",
                empire.InstanceID
            );
            SpecialForces secondParticipant = AITestSceneBuilder.CreateSpecialForces(
                "second",
                empire.InstanceID
            );
            firstParticipant.AllowedMissionTypeIDs.Add(MissionTypeIDs.Espionage);
            secondParticipant.AllowedMissionTypeIDs.Add(MissionTypeIDs.Espionage);
            firstParticipant.Ratings[OfficerRating.Espionage] = 0;
            secondParticipant.Ratings[OfficerRating.Espionage] = 0;
            game.AttachNode(firstParticipant, origin);
            game.AttachNode(secondParticipant, origin);
            game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { 0, 50 },
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double singleParticipantScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { firstParticipant }, MissionTypeIDs.Espionage, target)
            );
            double multipleParticipantScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { firstParticipant, secondParticipant },
                    MissionTypeIDs.Espionage,
                    target
                )
            );

            Assert.AreEqual(25, multipleParticipantScore - singleParticipantScore);
        }

        [Test]
        public void Score_MissionWithDistantDecoy_UsesFarthestParticipantTravelDistance()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet distantOrigin = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "distant-origin",
                empire.InstanceID,
                positionX: 600
            );
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "target",
                rebels.InstanceID,
                positionX: 100
            );
            target.AddVisitor(empire.InstanceID);
            Officer mainParticipant = EntityFactory.CreateOfficer("main", empire.InstanceID);
            Officer nearDecoy = EntityFactory.CreateOfficer("near-decoy", empire.InstanceID);
            Officer distantDecoy = EntityFactory.CreateOfficer("distant-decoy", empire.InstanceID);
            mainParticipant.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(mainParticipant, origin);
            game.AttachNode(nearDecoy, origin);
            game.AttachNode(distantDecoy, distantOrigin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double nearScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { mainParticipant },
                    MissionTypeIDs.Espionage,
                    target,
                    decoyParticipants: new[] { nearDecoy }
                )
            );
            double distantScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { mainParticipant },
                    MissionTypeIDs.Espionage,
                    target,
                    decoyParticipants: new[] { distantDecoy }
                )
            );

            Assert.Greater(nearScore, distantScore);
        }

        [Test]
        public void Score_HostileMissionWithEffectiveDecoy_ReturnsHigherScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.AddVisitor(empire.InstanceID);
            Regiment detector = AITestSceneBuilder.CreateRegiment("detector", rebels.InstanceID);
            detector.DetectionRating = 50;
            game.AttachNode(detector, target);
            Officer participant = EntityFactory.CreateOfficer("participant", empire.InstanceID);
            Officer decoy = EntityFactory.CreateOfficer("decoy", empire.InstanceID);
            participant.Ratings[OfficerRating.Espionage] = 50;
            decoy.Ratings[OfficerRating.Espionage] = 50;
            game.AttachNode(participant, origin);
            game.AttachNode(decoy, origin);
            game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { -1000, 50 },
            };
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int> { { -1000, 80 } };
            game.Config.ProbabilityTables.Mission.PlanetaryDecoy = new Dictionary<int, int>
            {
                { -1000, 75 },
            };
            game.Config.AI.MissionPlanning.MissionFoilRiskWeight = 1;
            game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability = 100;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double soloScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { participant }, MissionTypeIDs.Espionage, target)
            );
            double decoyedScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Espionage,
                    target,
                    decoyParticipants: new[] { decoy }
                )
            );

            Assert.AreEqual(60, decoyedScore - soloScore, 0.0001);
        }

        [Test]
        public void Score_OfficerMissionAbovePersonnelLossLimit_ReturnsZero()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Regiment detector = AITestSceneBuilder.CreateRegiment("detector", rebels.InstanceID);
            detector.DetectionRating = 100;
            game.AttachNode(detector, target);
            Officer participant = EntityFactory.CreateOfficer("participant", empire.InstanceID);
            participant.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            game.Config.ProbabilityTables.Mission.Evasion = new Dictionary<int, int>
            {
                { -1000, 0 },
            };
            game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability = 5;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double score = new AIMissionProposalScorer().Score(
                context,
                new AIMissionProposal(new[] { participant }, MissionTypeIDs.Espionage, target)
            );

            Assert.AreEqual(0, score);
        }

        [Test]
        public void Score_SpecialForcesMissionAbovePersonnelLossLimit_RemainsSelectable()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Regiment detector = AITestSceneBuilder.CreateRegiment("detector", rebels.InstanceID);
            detector.DetectionRating = 100;
            game.AttachNode(detector, target);
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "participant",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            participant.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(participant, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability = 0;
            game.Config.AI.MissionPlanning.MissionFoilRiskWeight = 0;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double score = new AIMissionProposalScorer().Score(
                context,
                new AIMissionProposal(new[] { participant }, MissionTypeIDs.Espionage, target)
            );

            Assert.Greater(score, 0);
        }

        [Test]
        public void Score_SabotageProposal_AddsTargetPriorityBonus()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Building firstShield = AddShield(game, target, "shield-1", rebels.InstanceID);
            AddShield(game, target, "shield-2", rebels.InstanceID);
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
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
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                empire.InstanceID
            );
            participant.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);
            participant.Ratings[OfficerRating.Combat] = 60;
            game.AttachNode(participant, origin);
            game.Config.AI.MissionPlanning.SabotageShieldBonus = 123;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double shieldScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Sabotage,
                    target,
                    selectedTarget: firstShield
                )
            );
            double shipyardScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Sabotage,
                    target,
                    selectedTarget: shipyard
                )
            );

            double expectedDifference =
                context.SabotageTargets.GetPriorityBonus(target, firstShield)
                - context.SabotageTargets.GetPriorityBonus(target, shipyard);

            Assert.AreEqual(expectedDifference, shieldScore - shipyardScore);
        }

        [Test]
        public void Score_SabotageProposal_UsesTacticalTargetPriorityOrder()
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
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                empire.InstanceID
            );
            participant.Ratings[OfficerRating.Combat] = 60;
            game.AttachNode(participant, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double defenseScore = ScoreSabotage(scorer, context, participant, target, defense);
            double regimentScore = ScoreSabotage(scorer, context, participant, target, regiment);
            double starfighterScore = ScoreSabotage(
                scorer,
                context,
                participant,
                target,
                starfighter
            );
            double shipyardScore = ScoreSabotage(scorer, context, participant, target, shipyard);

            Assert.Greater(defenseScore, regimentScore);
            Assert.Greater(regimentScore, starfighterScore);
            Assert.Greater(starfighterScore, shipyardScore);
        }

        [Test]
        public void Score_SabotageProposal_FavorsRegimentWhereOppositionHasMajoritySupport()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet favored = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "favored",
                rebels.InstanceID
            );
            Planet unfavored = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "unfavored",
                rebels.InstanceID
            );
            favored.SetPopularSupport(rebels.InstanceID, 40);
            favored.SetPopularSupport(empire.InstanceID, 60);
            unfavored.SetPopularSupport(rebels.InstanceID, 60);
            unfavored.SetPopularSupport(empire.InstanceID, 40);
            Regiment favoredRegiment = AITestSceneBuilder.CreateRegiment(
                "favored-regiment",
                rebels.InstanceID
            );
            Regiment unfavoredRegiment = AITestSceneBuilder.CreateRegiment(
                "unfavored-regiment",
                rebels.InstanceID
            );
            game.AttachNode(favoredRegiment, favored);
            game.AttachNode(unfavoredRegiment, unfavored);
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                empire.InstanceID
            );
            participant.Ratings[OfficerRating.Combat] = 60;
            game.AttachNode(participant, origin);
            game.Config.AI.MissionPlanning.SabotageFavoredSupportRegimentBonus = 37;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double favoredScore = ScoreSabotage(
                scorer,
                context,
                participant,
                favored,
                favoredRegiment
            );
            double unfavoredScore = ScoreSabotage(
                scorer,
                context,
                participant,
                unfavored,
                unfavoredRegiment
            );

            Assert.AreEqual(37, favoredScore - unfavoredScore);
        }

        [Test]
        public void Score_TargetedOfficerMission_WithWeakerTarget_ReturnsHigherScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet enemyPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy",
                rebels.InstanceID
            );
            enemyPlanet.AddVisitor(empire.InstanceID);
            Officer actor = EntityFactory.CreateOfficer("actor", empire.InstanceID);
            actor.Ratings[OfficerRating.Combat] = 100;
            Officer weakTarget = EntityFactory.CreateOfficer("weak", rebels.InstanceID);
            weakTarget.Ratings[OfficerRating.Combat] = 10;
            Officer strongTarget = EntityFactory.CreateOfficer("strong", rebels.InstanceID);
            strongTarget.Ratings[OfficerRating.Combat] = 90;
            game.AttachNode(actor, origin);
            game.AttachNode(weakTarget, enemyPlanet);
            game.AttachNode(strongTarget, enemyPlanet);
            AITestSceneBuilder.RevealPlanet(game, empire, enemyPlanet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double weakTargetScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { actor },
                    MissionTypeIDs.Abduction,
                    enemyPlanet,
                    selectedTarget: weakTarget,
                    targetOfficer: weakTarget
                )
            );
            double strongTargetScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { actor },
                    MissionTypeIDs.Abduction,
                    enemyPlanet,
                    selectedTarget: strongTarget,
                    targetOfficer: strongTarget
                )
            );

            Assert.Greater(weakTargetScore, strongTargetScore);
        }

        [Test]
        public void Score_HostileMissionWithSpecialForcesTechnology_PrefersSpecialForces()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Building sabotageTarget = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[OfficerRating.Combat] = 100;
            officer.Ratings[OfficerRating.Espionage] = 100;
            SpecialForces specialForces = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                empire.InstanceID
            );
            specialForces.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);
            specialForces.Ratings[OfficerRating.Combat] = 100;
            specialForces.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(officer, origin);
            game.AttachNode(specialForces, origin);
            empire.RebuildResearchCatalog(new IManufacturable[] { specialForces });
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double officerScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { officer },
                    MissionTypeIDs.Sabotage,
                    target,
                    selectedTarget: sabotageTarget
                )
            );
            double specialForcesScore = scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { specialForces },
                    MissionTypeIDs.Sabotage,
                    target,
                    selectedTarget: sabotageTarget
                )
            );

            Assert.Greater(specialForcesScore, officerScore);
        }

        [Test]
        public void Score_EnemyEspionageWithSpecialForcesTechnology_PrefersSpecialForces()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.AddVisitor(empire.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[OfficerRating.Espionage] = 100;
            SpecialForces specialForces = AITestSceneBuilder.CreateSpecialForces(
                "spy",
                empire.InstanceID
            );
            specialForces.AllowedMissionTypeIDs.Add(MissionTypeIDs.Espionage);
            specialForces.Ratings[OfficerRating.Espionage] = 100;
            game.AttachNode(officer, origin);
            game.AttachNode(specialForces, origin);
            empire.RebuildResearchCatalog(new IManufacturable[] { specialForces });
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double officerScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Espionage, target)
            );
            double specialForcesScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { specialForces }, MissionTypeIDs.Espionage, target)
            );

            Assert.Greater(specialForcesScore, officerScore);
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
            shield.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(shield, planet);
            return shield;
        }

        private static double ScoreSabotage(
            AIMissionProposalScorer scorer,
            AITurnContext context,
            IMissionParticipant participant,
            Planet planet,
            IManufacturable target
        )
        {
            if (
                participant is SpecialForces specialForces
                && !specialForces.AllowedMissionTypeIDs.Contains(MissionTypeIDs.Sabotage)
            )
                specialForces.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);

            return scorer.Score(
                context,
                new AIMissionProposal(
                    new[] { participant },
                    MissionTypeIDs.Sabotage,
                    planet,
                    selectedTarget: target
                )
            );
        }
    }
}
