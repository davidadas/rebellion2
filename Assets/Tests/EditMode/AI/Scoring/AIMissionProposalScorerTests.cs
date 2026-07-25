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
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[OfficerRating.Diplomacy] = 100;
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
        public void Score_DiplomacyProposal_UsesRegimentUprisingDefense()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet undefended = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "undefended",
                empire.InstanceID
            );
            Planet defended = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defended",
                empire.InstanceID
            );
            undefended.SetPopularSupport(empire.InstanceID, 50);
            defended.SetPopularSupport(empire.InstanceID, 50);
            Regiment regiment = AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID);
            regiment.UprisingDefense = 20;
            game.AttachNode(regiment, defended);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[OfficerRating.Diplomacy] = 40;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double undefendedScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Diplomacy, undefended)
            );
            double defendedScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Diplomacy, defended)
            );

            Assert.Greater(defendedScore, undefendedScore);
        }

        [Test]
        public void Score_RecruitmentProposal_ReturnsHigherScoreForHigherSupportPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
            officer.Ratings[OfficerRating.Leadership] = 80;
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
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
        public void Score_ReconnaissanceProposal_IgnoresParticipantRating()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
        public void Score_MissionWithDistantDecoy_UsesFarthestParticipantTravelDistance()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
        public void Score_SabotageProposal_TargetingAssaultBlockingShield_AddsConfiguredBonus()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Building firstShield = AddShield(game, target, "shield-1", rebels.InstanceID);
            AddShield(game, target, "shield-2", rebels.InstanceID);
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
            game.Config.AI.MissionPlanning.SabotageAssaultBlockerBonus = 123;
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

            Assert.AreEqual(
                game.Config.AI.MissionPlanning.SabotageAssaultBlockerBonus
                    + game.Config.AI.MissionPlanning.SabotageAttackTargetBonus
                    + game.Config.AI.MissionPlanning.SabotageAttackDefenseBonus
                    - game.Config.AI.MissionPlanning.SabotageInfrastructureBonus,
                shieldScore - shipyardScore
            );
        }

        [Test]
        public void Score_SabotageProposal_UsesTacticalTargetPriorityOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
        public void Score_TargetedOfficerMission_IgnoresTargetCombatRating()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet enemyPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy",
                rebels.InstanceID
            );
            Officer actor = EntityFactory.CreateOfficer("actor", empire.InstanceID);
            actor.Ratings[OfficerRating.Combat] = 100;
            Officer weakTarget = EntityFactory.CreateOfficer("weak", rebels.InstanceID);
            weakTarget.Ratings[OfficerRating.Combat] = 10;
            Officer strongTarget = EntityFactory.CreateOfficer("strong", rebels.InstanceID);
            strongTarget.Ratings[OfficerRating.Combat] = 90;
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

            Assert.AreEqual(weakTargetScore, strongTargetScore);
        }

        [Test]
        public void Score_HostileMissionWithSpecialForcesTechnology_PrefersSpecialForces()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[OfficerRating.Combat] = 100;
            SpecialForces specialForces = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                empire.InstanceID
            );
            specialForces.AllowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);
            specialForces.Ratings[OfficerRating.Combat] = 55;
            empire.RebuildResearchCatalog(new IManufacturable[] { specialForces });
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposalScorer scorer = new AIMissionProposalScorer();

            double officerScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { officer }, MissionTypeIDs.Sabotage, target)
            );
            double specialForcesScore = scorer.Score(
                context,
                new AIMissionProposal(new[] { specialForces }, MissionTypeIDs.Sabotage, target)
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
            shield.DefenseFacilityClass = DefenseFacilityClass.Shield;
            shield.ShieldStrength = 10;
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
