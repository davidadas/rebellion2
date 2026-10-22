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
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Fleets
{
    [TestFixture]
    public class AIFleetProposalScorerTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Score_BombardmentDonorOvercomesShields_ValuesCompletedGroundRequirements(
            bool assaultBlocked
        )
        {
            AITurnContext context = AITestSceneBuilder.CreateBombardmentDonorContext(
                assaultBlocked,
                5,
                out Fleet receiver,
                out CapitalShip donor
            );
            context.Game.Config.AI.FleetDeployment.AttackUtility =
                new GameConfig.AIAttackUtilityConfig
                {
                    Readiness = new GameConfig.AIConsiderationConfig { Weight = 1 },
                };
            Planet target = context.Assessment.GetKnownPlanet(receiver.Order.TargetPlanetId);
            var demand = context.GetAttackDemand(target);
            double before =
                (
                    1d
                    + (double)context.Assessment.GetFleetLoadedRegimentCount(receiver)
                        / demand.RegimentCount
                    + (double)context.Assessment.GetFleetRegimentCapacity(receiver)
                        / demand.RegimentCount
                    + (double)context.Assessment.GetProjectedFleetRegimentAttackStrength(receiver)
                        / demand.RegimentStrength
                    + (
                        assaultBlocked
                            ? (double)
                                context.Assessment.GetProjectedFleetBombardmentStrength(receiver)
                                / demand.BombardmentStrength
                            : 0
                    )
                ) / (assaultBlocked ? 5 : 4);
            double expected = (1 - before) / (2 - before);

            double score = new AIFleetProposalScorer().Score(
                context,
                new AITransferUnitProposal(
                    donor.GetParentOfType<Fleet>(),
                    receiver,
                    donor,
                    receiver,
                    target
                )
            );

            Assert.AreEqual(expected, score, 0.000001);
        }

        [Test]
        public void Score_OccupationReinforcementCompletesBombardingFleet_UsesRemainingGroundRequirement()
        {
            AITurnContext context = AITestSceneBuilder.CreateBombardmentReinforcementContext(
                false,
                false,
                out Fleet bombardFleet,
                out _,
                out _
            );
            context = AITestSceneBuilder.AddReinforcementDonor(context, true, out CapitalShip ship);
            context.Game.Config.AI.FleetDeployment.AttackUtility =
                new GameConfig.AIAttackUtilityConfig
                {
                    Readiness = new GameConfig.AIConsiderationConfig { Weight = 1 },
                };

            double score = new AIFleetProposalScorer().Score(
                context,
                new AITransferUnitProposal(
                    ship.GetParentOfType<Fleet>(),
                    bombardFleet,
                    ship,
                    bombardFleet,
                    context.Assessment.GetKnownPlanet(bombardFleet.Order.TargetPlanetId)
                )
            );

            Assert.AreEqual(0.2, score, 0.000001);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void Score_FleetHasNoTroops_ValuesImmediateCombatOverDistantPreparation(
            bool atTarget,
            bool hasValue
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            game.Config.Combat.SpaceCombat.AdmiralLeadershipDivisor = 100;
            game.Config.AI.FleetDeployment.OfficerCommandUtility =
                new GameConfig.AIOfficerCommandUtilityConfig
                {
                    ForceImprovement = new GameConfig.AIConsiderationConfig { Weight = 1 },
                };
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = AddBattleFleet(
                game,
                atTarget ? target : origin,
                "fleet",
                empire.InstanceID,
                100
            );
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = target.InstanceID,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.AllowedRanks = new[] { OfficerRank.Admiral };
            officer.SetBaseRating(SkillRating.Leadership, 100);
            game.AttachNode(officer, fleet.GetChildren<CapitalShip>().Single());
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double score = new AIFleetProposalScorer().Score(
                context,
                new AIOfficerCommandProposal(officer, fleet, target, OfficerRank.Admiral)
            );

            Assert.AreEqual(hasValue, score > 0);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void Score_OnlyBombardmentImproves_RequiresGroundOpposition(
            bool hasDefenders,
            bool hasValue
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            game.Config.Combat.SpaceCombat.AdmiralLeadershipDivisor = 0;
            game.Config.Combat.Bombardment.AttackerLeadershipDivisor = 10;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 1;
            game.Config.AI.FleetDeployment.OfficerCommandUtility =
                new GameConfig.AIOfficerCommandUtilityConfig
                {
                    ForceImprovement = new GameConfig.AIConsiderationConfig { Weight = 1 },
                };
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            if (hasDefenders)
                game.AttachNode(
                    AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID),
                    target
                );
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = AddBattleFleet(game, origin, "fleet", empire.InstanceID, 100);
            CapitalShip ship = fleet.GetChildren<CapitalShip>().Single();
            ship.Bombardment = 100;
            game.AttachNode(AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID), ship);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = target.InstanceID,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.AllowedRanks = new[] { OfficerRank.Admiral };
            officer.SetBaseRating(SkillRating.Leadership, 100);
            game.AttachNode(officer, ship);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double score = new AIFleetProposalScorer().Score(
                context,
                new AIOfficerCommandProposal(officer, fleet, target, OfficerRank.Admiral)
            );

            Assert.AreEqual(hasValue, score > 0);
        }

        [TestCase(0, false, false)]
        [TestCase(100, false, true)]
        [TestCase(100, true, true)]
        public void Score_FleetExceedsAttackDemand_ValuesEffectiveCommand(
            int leadership,
            bool alreadyAssigned,
            bool hasValue
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            game.Config.Combat.SpaceCombat.AdmiralLeadershipDivisor = 100;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 1;
            game.Config.AI.FleetDeployment.OfficerCommandUtility =
                new GameConfig.AIOfficerCommandUtilityConfig
                {
                    ForceImprovement = new GameConfig.AIConsiderationConfig { Weight = 1 },
                };
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = AddBattleFleet(game, origin, "fleet", empire.InstanceID, 100);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID),
                fleet.GetChildren<CapitalShip>().Single()
            );
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.AllowedRanks = new[] { OfficerRank.Admiral };
            officer.SetBaseRating(SkillRating.Leadership, leadership);
            officer.CurrentRank = alreadyAssigned ? OfficerRank.Admiral : OfficerRank.None;
            game.AttachNode(officer, fleet.GetChildren<CapitalShip>().Single());
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIOfficerCommandProposal proposal = new(officer, fleet, target, OfficerRank.Admiral);

            double score = new AIFleetProposalScorer().Score(context, proposal);

            Assert.AreEqual(hasValue, score > 0);
        }

        [TestCase(OfficerRank.Commander, false)]
        [TestCase(OfficerRank.Commander, true)]
        [TestCase(OfficerRank.General, false)]
        [TestCase(OfficerRank.General, true)]
        public void Score_FleetExceedsPersonnelStrengthDemand_ValuesEffectiveCommand(
            OfficerRank rank,
            bool alreadyAssigned
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            game.Config.Combat.SpaceCombat.CommanderCombatDivisor = 20;
            game.Config.Combat.PlanetaryAssault.GeneralLeadershipDivisor = 30;
            game.Config.AI.FleetDeployment.OfficerCommandUtility =
                new GameConfig.AIOfficerCommandUtilityConfig
                {
                    ForceImprovement = new GameConfig.AIConsiderationConfig { Weight = 1 },
                };
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID),
                target
            );
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = AddBattleFleet(game, origin, "fleet", empire.InstanceID, 100);
            CapitalShip ship = fleet.GetChildren<CapitalShip>().Single();
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID, 100),
                ship
            );
            game.AttachNode(
                AITestSceneBuilder.CreateStarfighter("fighter", empire.InstanceID),
                ship
            );
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = target.InstanceID,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.AllowedRanks = new[] { rank };
            officer.SetBaseRating(SkillRating.Leadership, 100);
            officer.SetBaseRating(SkillRating.Combat, 100);
            officer.CurrentRank = alreadyAssigned ? rank : OfficerRank.None;
            game.AttachNode(officer, ship);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double score = new AIFleetProposalScorer().Score(
                context,
                new AIOfficerCommandProposal(officer, fleet, target, rank)
            );

            Assert.Greater(score, 0);
        }

        [TestCase(0.03, false)]
        [TestCase(0.15, true)]
        public void Score_StrongerSectorOwnershipPreference_FavorsConsolidationOverShorterTravel(
            double weight,
            bool prefersConsolidation
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackUtility = new GameConfig.AIAttackUtilityConfig
            {
                SystemPresence = new GameConfig.AIConsiderationConfig { Weight = weight },
                TravelEfficiency = new GameConfig.AIConsiderationConfig { Weight = 0.1 },
            };
            PlanetSector originSector = AITestSceneBuilder.AddSector(game, "origin-sector");
            Planet origin = AITestSceneBuilder.AddPlanet(
                game,
                originSector,
                "origin",
                empire.InstanceID
            );
            PlanetSector distantSector = AITestSceneBuilder.AddSector(game, "distant-sector");
            for (int index = 0; index < 3; index++)
                AITestSceneBuilder.AddPlanet(
                    game,
                    distantSector,
                    "friendly-" + index,
                    empire.InstanceID
                );
            Planet consolidationTarget = AITestSceneBuilder.AddPlanet(
                game,
                distantSector,
                "last-enemy",
                rebels.InstanceID,
                positionX: 2000
            );
            PlanetSector nearbySector = AITestSceneBuilder.AddSector(game, "nearby-sector");
            Planet nearbyTarget = AITestSceneBuilder.AddPlanet(
                game,
                nearbySector,
                "nearby-enemy",
                rebels.InstanceID,
                positionX: 1000
            );
            AITestSceneBuilder.RevealPlanet(game, empire, consolidationTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, nearbyTarget);
            Fleet fleet = AddBattleFleet(
                game,
                origin,
                "fleet",
                empire.InstanceID,
                combatStrength: 1000
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double consolidationScore = scorer.Score(
                context,
                new AIFleetAttackProposal(
                    fleet,
                    FleetOrderType.Attack,
                    FleetOrderStatus.Staging,
                    consolidationTarget
                )
            );
            double nearbyScore = scorer.Score(
                context,
                new AIFleetAttackProposal(
                    fleet,
                    FleetOrderType.Attack,
                    FleetOrderStatus.Staging,
                    nearbyTarget
                )
            );

            Assert.AreEqual(prefersConsolidation, consolidationScore > nearbyScore);
        }

        [Test]
        public void Score_ReturningAttackFleetInHostileTerritory_ReturnsHighestScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(
                game,
                target,
                "fleet",
                empire.InstanceID,
                combatStrength: 100
            );
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = target.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Returning,
                target
            );

            double score = new AIFleetProposalScorer().Score(context, proposal);

            Assert.AreEqual(1, score);
            Assert.AreEqual(AIProposalPriority.Mandatory, proposal.Priority);
        }

        [Test]
        public void Score_FleetEvacuation_ReturnsHighestScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet hostile = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "hostile",
                rebels.InstanceID
            );
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "destination",
                empire.InstanceID
            );
            Fleet fleet = AddBattleFleet(
                game,
                hostile,
                "fleet",
                empire.InstanceID,
                combatStrength: 100
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetEvacuationProposal proposal = new AIFleetEvacuationProposal(
                fleet,
                hostile,
                destination
            );
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double score = scorer.Score(context, proposal);

            Assert.IsTrue(scorer.CanScore(proposal));
            Assert.AreEqual(1, score);
            Assert.AreEqual(AIProposalPriority.Mandatory, proposal.Priority);
        }

        [Test]
        public void Score_FleetReinforcementRedirection_ReturnsHighestScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet current = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "current",
                empire.InstanceID
            );
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "destination",
                empire.InstanceID
            );
            Fleet fleet = AddBattleFleet(
                game,
                current,
                "fleet",
                empire.InstanceID,
                combatStrength: 100
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetReinforcementRedirectionProposal proposal =
                new AIFleetReinforcementRedirectionProposal(fleet, current, destination);
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double score = scorer.Score(context, proposal);

            Assert.IsTrue(scorer.CanScore(proposal));
            Assert.AreEqual(1, score);
            Assert.AreEqual(AIProposalPriority.Mandatory, proposal.Priority);
        }

        [Test]
        public void Score_AttackProposalForHeadquarters_ReturnsHigherScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackUtility.StrategicValue.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.SystemPresence.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.Readiness.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.CaptureViability.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.TravelEfficiency.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.ExpectedLossRisk.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.OpportunityCost.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.Headquarters.Weight = 1;
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "owned",
                empire.InstanceID
            );
            Planet normalTarget = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "normal",
                rebels.InstanceID
            );
            Planet headquartersTarget = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "hq",
                rebels.InstanceID
            );
            headquartersTarget.IsHeadquarters = true;
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.AddChild(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID));
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double normalScore = scorer.Score(
                context,
                new AIFleetAttackProposal(
                    fleet,
                    FleetOrderType.Attack,
                    FleetOrderStatus.Staging,
                    normalTarget
                )
            );
            double headquartersScore = scorer.Score(
                context,
                new AIFleetAttackProposal(
                    fleet,
                    FleetOrderType.Attack,
                    FleetOrderStatus.Staging,
                    headquartersTarget
                )
            );

            Assert.Greater(headquartersScore, normalScore);
        }

        [Test]
        public void Score_AttackProposalWithOrbitalAdvantage_AppliesResponseBonus()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackUtility.StrategicValue.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.SystemPresence.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.Readiness.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.CaptureViability.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.TravelEfficiency.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.ExpectedLossRisk.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.OpportunityCost.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.OrbitalAdvantage.Weight = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(
                game,
                owned,
                "friendly",
                empire.InstanceID,
                combatStrength: 1000
            );
            AddBattleFleet(game, target, "hostile", rebels.InstanceID, combatStrength: 500);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );

            double score = new AIFleetProposalScorer().Score(context, proposal);

            Assert.Greater(score, 0);
            Assert.LessOrEqual(score, 1);
        }

        [Test]
        public void Score_ExposedBombardmentTargetWithSectorLeverage_PrioritizesCapableFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            GameConfig.AIFleetDeploymentConfig config = game.Config.AI.FleetDeployment;
            config.AttackUtility.StrategicValue.Weight = 0;
            config.AttackUtility.SectorSupport.Weight = 0;
            config.AttackUtility.SystemPresence.Weight = 0;
            config.AttackUtility.Readiness.Weight = 0;
            config.AttackUtility.CaptureViability.Weight = 0;
            config.AttackUtility.TravelEfficiency.Weight = 0;
            config.AttackUtility.ExpectedLossRisk.Weight = 0;
            config.AttackUtility.OpportunityCost.Weight = 0;
            config.AttackUtility.OrbitalAdvantage.Weight = 0;
            config.AttackUtility.ExposedBombardment.Weight = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, 1);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID),
                target
            );
            Fleet fleet = AddBattleFleet(
                game,
                owned,
                "fleet",
                empire.InstanceID,
                combatStrength: 1000
            );
            fleet.GetChildren<CapitalShip>().Single().Bombardment = 1;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double score = scorer.Score(
                context,
                new AIFleetAttackProposal(
                    fleet,
                    FleetOrderType.Attack,
                    FleetOrderStatus.Staging,
                    target
                )
            );

            Assert.Greater(score, 0);
            Assert.LessOrEqual(score, 1);
            Assert.GreaterOrEqual(scorer.GetNewAttackScoreUpperBound(context, target), score);
        }

        [Test]
        public void GetNewAttackScoreUpperBound_WithAttackProposal_DoesNotUnderestimateScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(
                game,
                owned,
                "friendly",
                empire.InstanceID,
                combatStrength: 1000
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double score = scorer.Score(context, proposal);
            double upperBound = scorer.GetNewAttackScoreUpperBound(context, target);

            Assert.GreaterOrEqual(upperBound, score);
        }

        [TestCase(false, 0)]
        [TestCase(false, 200)]
        [TestCase(true, 0)]
        [TestCase(true, 200)]
        public void Score_OffensiveTargetWithinIntelGracePeriod_PreservesPriority(
            bool engagement,
            int intelAge
        )
        {
            var (context, fleet, target, _) = CreateIntelAgeContext(intelAge);
            AIProposal proposal = CreateOffensiveProposal(context, fleet, target, engagement);
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double score = scorer.Score(context, proposal);
            context.Game.Config.AI.FleetDeployment.AttackUtility.IntelAgeRisk.Weight = 0;

            Assert.AreEqual(scorer.Score(context, proposal), score);
        }

        [TestCase(false, 300, 0.525)]
        [TestCase(false, 400, 0.05)]
        [TestCase(false, 683, 0.05)]
        [TestCase(true, 300, 0.525)]
        [TestCase(true, 400, 0.05)]
        [TestCase(true, 683, 0.05)]
        public void Score_OffensiveTargetBeyondIntelGracePeriod_ReducesPriority(
            bool engagement,
            int intelAge,
            double retainedPriority
        )
        {
            var (context, fleet, target, _) = CreateIntelAgeContext(intelAge);
            AIProposal proposal = CreateOffensiveProposal(context, fleet, target, engagement);
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double score = scorer.Score(context, proposal);
            context.Game.Config.AI.FleetDeployment.AttackUtility.IntelAgeRisk.Weight = 0;

            Assert.AreEqual(scorer.Score(context, proposal) * retainedPriority, score, 1e-9);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Select_StaleOffensiveTargetWithoutAlternative_RemainsSelectable(bool engagement)
        {
            var (context, fleet, target, _) = CreateIntelAgeContext(683);
            AIProposal proposal = CreateOffensiveProposal(context, fleet, target, engagement);
            proposal.SetScore(new AIFleetProposalScorer().Score(context, proposal));
            context.AddProposal(proposal);

            var selected = new AISelectionPhase().Select(context);

            Assert.That(selected, Does.Contain(proposal));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Select_EquivalentFreshAndStaleOffensiveTargets_PrefersFreshTarget(
            bool engagement
        )
        {
            var (context, fleet, target, freshTarget) = CreateIntelAgeContext(683);
            AIProposal stale = CreateOffensiveProposal(context, fleet, target, engagement);
            AIProposal fresh = CreateOffensiveProposal(context, fleet, freshTarget, engagement);
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();
            stale.SetScore(scorer.Score(context, stale));
            fresh.SetScore(scorer.Score(context, fresh));
            context.AddProposal(stale);
            context.AddProposal(fresh);

            var selected = new AISelectionPhase().Select(context);

            Assert.That(selected, Is.EquivalentTo(new[] { fresh }));
        }

        [Test]
        public void Score_ExistingAttackOrderWithHighOpportunityCost_RetainsOrderBonus()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackUtility.StrategicValue.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.Readiness.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.CaptureViability.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.TravelEfficiency.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.ExpectedLossRisk.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.OpportunityCost.Weight = 1;
            game.Config.AI.FleetDeployment.AttackUtility.ExistingOrder.Weight = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            fleet.AddChild(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID));
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                enemy
            );

            double score = new AIFleetProposalScorer().Score(context, proposal);

            Assert.Greater(score, 0);
            Assert.LessOrEqual(score, 1);
        }

        [Test]
        public void Score_AttackProposalWithSplitLocalDefense_AppliesOpportunityCost()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackUtility.StrategicValue.Weight = 1;
            game.Config.AI.FleetDeployment.AttackUtility.SystemPresence.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.Readiness.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.CaptureViability.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.TravelEfficiency.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.ExpectedLossRisk.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.OpportunityCost.Weight = 1;
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet attackingFleet = AddBattleFleet(
                game,
                owned,
                "attacking-fleet",
                empire.InstanceID,
                combatStrength: 100
            );
            AddBattleFleet(game, owned, "defender-one", empire.InstanceID, combatStrength: 600);
            AddBattleFleet(game, owned, "defender-two", empire.InstanceID, combatStrength: 600);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                attackingFleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                enemy
            );

            double score = new AIFleetProposalScorer().Score(context, proposal);

            Assert.Greater(score, 0);
            Assert.Less(score, 1);
        }

        [Test]
        public void Score_ShieldedAttackProposal_IncludesStarfighterBombardment()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackUtility.StrategicValue.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.Readiness.Weight = 1;
            game.Config.AI.FleetDeployment.AttackUtility.CaptureViability.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.TravelEfficiency.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.ExpectedLossRisk.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.OpportunityCost.Weight = 0;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, target, "shield-1", rebels.InstanceID, 50);
            AddShield(game, target, "shield-2", rebels.InstanceID, 50);
            Fleet capitalOnly = AddAssaultFleet(game, owned, "capital-only", empire.InstanceID);
            capitalOnly.GetChildren<CapitalShip>()[0].Bombardment = 100;
            Fleet combinedArms = AddAssaultFleet(game, owned, "combined", empire.InstanceID);
            combinedArms.GetChildren<CapitalShip>()[0].Bombardment = 100;
            Starfighter bomber = new Starfighter
            {
                InstanceID = "bomber",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Bombardment = 1,
                MaxSquadronSize = 1,
                CurrentSquadronSize = 1,
            };
            game.AttachNode(bomber, combinedArms.GetChildren<CapitalShip>()[0]);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double capitalOnlyScore = scorer.Score(
                context,
                new AIFleetAttackProposal(
                    capitalOnly,
                    FleetOrderType.Attack,
                    FleetOrderStatus.Staging,
                    target
                )
            );
            double combinedArmsScore = scorer.Score(
                context,
                new AIFleetAttackProposal(
                    combinedArms,
                    FleetOrderType.Attack,
                    FleetOrderStatus.Staging,
                    target
                )
            );

            Assert.Greater(combinedArmsScore, capitalOnlyScore);
        }

        [Test]
        public void Score_AttackWithLowReadiness_AppliesFloorWeight()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            GameConfig.AIFleetDeploymentConfig config = game.Config.AI.FleetDeployment;
            config.AttackUtility.StrategicValue.Weight = 0;
            config.AttackUtility.SectorSupport.Weight = 0;
            config.AttackUtility.SystemPresence.Weight = 0;
            config.AttackUtility.Readiness.Weight = 1;
            config.AttackUtility.Ready.Weight = 0;
            config.AttackUtility.CaptureViability.Weight = 0;
            config.AttackUtility.TravelEfficiency.Weight = 0;
            config.AttackUtility.ExpectedLossRisk.Weight = 0;
            config.AttackUtility.OpportunityCost.Weight = 0;
            config.MinimumAttackStrength = 1;
            config.MinimumPlanetaryAssaultRegimentCount = 1;
            config.AttackStrengthPercentOfDefense = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AddShield(game, target, "shield", rebels.InstanceID, 100);
            Fleet fleet = AddAssaultFleet(game, owned, "fleet", empire.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            config.AttackReadinessFloorWeight = 0;
            double averageOnlyScore = scorer.Score(context, proposal);
            config.AttackReadinessFloorWeight = 10;
            double bottleneckWeightedScore = scorer.Score(context, proposal);

            Assert.Less(bottleneckWeightedScore, averageOnlyScore);
        }

        [Test]
        public void Score_ReadyAttack_AppliesConfiguredBonus()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            GameConfig.AIFleetDeploymentConfig config = game.Config.AI.FleetDeployment;
            config.AttackUtility.StrategicValue.Weight = 0;
            config.AttackUtility.SectorSupport.Weight = 0;
            config.AttackUtility.SystemPresence.Weight = 0;
            config.AttackUtility.Readiness.Weight = 1;
            config.AttackReadinessFloorWeight = 0;
            config.AttackUtility.CaptureViability.Weight = 0;
            config.AttackUtility.TravelEfficiency.Weight = 0;
            config.AttackUtility.ExpectedLossRisk.Weight = 0;
            config.AttackUtility.OpportunityCost.Weight = 0;
            config.MinimumAttackStrength = 1;
            config.MinimumPlanetaryAssaultRegimentCount = 1;
            config.AttackStrengthPercentOfDefense = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            Fleet fleet = AddAssaultFleet(game, owned, "fleet", empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            config.AttackUtility.Ready.Weight = 0;
            double unbonusedScore = scorer.Score(context, proposal);
            config.AttackUtility.Ready.Weight = 1;
            double bonusedScore = scorer.Score(context, proposal);

            Assert.Greater(bonusedScore, unbonusedScore);
        }

        [Test]
        public void Score_AttackTransferWithCarriedStarfighters_IncludesSquadronStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackUtility.StrategicValue.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.Readiness.Weight = 1;
            game.Config.AI.FleetDeployment.AttackUtility.CaptureViability.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.TravelEfficiency.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.ExpectedLossRisk.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.OpportunityCost.Weight = 0;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddBattleFleet(game, target, "hostile", rebels.InstanceID, 400);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet sourceFleet = EntityFactory.CreateFleet("source", empire.InstanceID);
            Fleet targetFleet = EntityFactory.CreateFleet("target", empire.InstanceID);
            targetFleet.RoleType = FleetRoleType.Battle;
            targetFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier",
                empire.InstanceID,
                combatStrength: 0,
                regimentCapacity: 0,
                starfighterCapacity: 1
            );
            Starfighter fighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                LaserCannon = 400,
                MaxSquadronSize = 1,
                CurrentSquadronSize = 1,
            };
            game.AttachNode(sourceFleet, staging);
            game.AttachNode(carrier, sourceFleet);
            game.AttachNode(fighter, carrier);
            game.AttachNode(targetFleet, staging);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "target-ship",
                    empire.InstanceID,
                    combatStrength: 100,
                    regimentCapacity: 1
                ),
                targetFleet
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("target-regiment", empire.InstanceID),
                targetFleet.GetChildren<CapitalShip>().Single()
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AITransferUnitProposal proposal = new AITransferUnitProposal(
                sourceFleet,
                targetFleet,
                carrier,
                targetFleet,
                target
            );

            double score = new AIFleetProposalScorer().Score(context, proposal);

            Assert.Greater(score, 0);
        }

        [Test]
        public void Score_AttackTransferWithProjectedRequirementsMet_ReturnsZero()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackUtility.StrategicValue.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.Readiness.Weight = 1;
            game.Config.AI.FleetDeployment.AttackUtility.TravelEfficiency.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.OpportunityCost.Weight = 0;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment(
                    "defender",
                    rebels.InstanceID,
                    defenseRating: 100
                ),
                target
            );
            Fleet sourceFleet = EntityFactory.CreateFleet("source", empire.InstanceID);
            Fleet targetFleet = EntityFactory.CreateFleet("target", empire.InstanceID);
            targetFleet.RoleType = FleetRoleType.Battle;
            targetFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            CapitalShip reinforcement = AITestSceneBuilder.CreateCapitalShip(
                "reinforcement",
                empire.InstanceID
            );
            game.AttachNode(sourceFleet, staging);
            game.AttachNode(reinforcement, sourceFleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment(
                    "reinforcement-regiment",
                    empire.InstanceID,
                    attackRating: 100
                ),
                reinforcement
            );
            game.AttachNode(targetFleet, staging);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("target-ship", empire.InstanceID),
                targetFleet
            );
            CapitalShip inboundShip = AITestSceneBuilder.CreateCapitalShip(
                "inbound-ship",
                empire.InstanceID
            );
            inboundShip.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(inboundShip, targetFleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment(
                    "inbound-regiment",
                    empire.InstanceID,
                    attackRating: 100
                ),
                inboundShip
            );
            CapitalShip secondInboundShip = AITestSceneBuilder.CreateCapitalShip(
                "second-inbound-ship",
                empire.InstanceID
            );
            secondInboundShip.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(secondInboundShip, targetFleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment(
                    "second-inbound-regiment",
                    empire.InstanceID,
                    attackRating: 100
                ),
                secondInboundShip
            );
            CapitalShip thirdInboundShip = AITestSceneBuilder.CreateCapitalShip(
                "third-inbound-ship",
                empire.InstanceID
            );
            thirdInboundShip.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(thirdInboundShip, targetFleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment(
                    "third-inbound-regiment",
                    empire.InstanceID,
                    attackRating: 100
                ),
                thirdInboundShip
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AITransferUnitProposal proposal = new AITransferUnitProposal(
                sourceFleet,
                targetFleet,
                reinforcement,
                targetFleet,
                target
            );

            double score = new AIFleetProposalScorer().Score(context, proposal);

            Assert.Zero(score);
        }

        [Test]
        public void Score_ColonizationProposalWithLoadedRegiment_AddsReadinessBonus()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.ColonizationUtility.Base.Weight = 1;
            game.Config.AI.FleetDeployment.ColonizationUtility.StrategicValue.Weight = 0;
            game.Config.AI.FleetDeployment.ColonizationUtility.TravelEfficiency.Weight = 0;
            game.Config.AI.FleetDeployment.ColonizationUtility.Ready.Weight = 1;
            game.Config.AI.FleetDeployment.ColonizationUtility.OpportunityCost.Weight = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Colonization;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID);
            game.AttachNode(fleet, owned);
            game.AttachNode(ship, fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            Planet knownTarget = context.Assessment.GetKnownPlanet(target.InstanceID);
            AIColonizationProposal proposal = new AIColonizationProposal(
                fleet,
                FleetOrderStatus.Staging,
                knownTarget
            );
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double unloadedScore = scorer.Score(context, proposal);
            game.AttachNode(AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID), ship);
            double loadedScore = scorer.Score(context, proposal);

            Assert.Greater(loadedScore, unloadedScore);
        }

        [Test]
        public void Score_ColonizationCampaignNearHeadquarters_ReturnsHigherScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            GameConfig.AIColonizationUtilityConfig utility = game.Config
                .AI
                .FleetDeployment
                .ColonizationUtility;
            utility.Base.Weight = 0;
            utility.TravelEfficiency.Weight = 0;
            utility.AnchorProximity.Weight = 1;
            utility.Ready.Weight = 0;
            utility.ExistingOrder.Weight = 0;
            PlanetSector core = AITestSceneBuilder.AddSector(game, "core");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                core,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            headquarters.PositionX = 0;
            headquarters.PositionY = 0;
            empire.HQInstanceID = headquarters.InstanceID;
            PlanetSector nearSector = AITestSceneBuilder.AddSector(game, "near-sector");
            nearSector.SectorType = PlanetSectorType.OuterRim;
            Planet near = AITestSceneBuilder.AddPlanet(game, nearSector, "near", null);
            near.IsColonized = false;
            near.PositionX = 10;
            near.PositionY = 0;
            PlanetSector farSector = AITestSceneBuilder.AddSector(game, "far-sector");
            farSector.SectorType = PlanetSectorType.OuterRim;
            Planet far = AITestSceneBuilder.AddPlanet(game, farSector, "far", null);
            far.IsColonized = false;
            far.PositionX = 100;
            far.PositionY = 0;
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Colonization;
            game.AttachNode(fleet, headquarters);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double nearScore = scorer.Score(
                context,
                new AIColonizationCampaignProposal(fleet, nearSector.InstanceID, new[] { near })
            );
            double farScore = scorer.Score(
                context,
                new AIColonizationCampaignProposal(fleet, farSector.InstanceID, new[] { far })
            );

            Assert.Greater(nearScore, farScore);
        }

        [Test]
        public void Score_ExistingColonizationOrderOnColonizationFleet_AddsContinuationBonus()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.ColonizationUtility.Base.Weight = 1;
            game.Config.AI.FleetDeployment.ColonizationUtility.StrategicValue.Weight = 0;
            game.Config.AI.FleetDeployment.ColonizationUtility.TravelEfficiency.Weight = 0;
            game.Config.AI.FleetDeployment.ColonizationUtility.Ready.Weight = 0;
            game.Config.AI.FleetDeployment.ColonizationUtility.OpportunityCost.Weight = 0;
            game.Config.AI.FleetDeployment.ColonizationUtility.ExistingOrder.Weight = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Colonization;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Colonize,
                Status = FleetOrderStatus.Readying,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(fleet, owned);
            game.AttachNode(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID), fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            Planet knownTarget = context.Assessment.GetKnownPlanet(target.InstanceID);
            AIColonizationProposal proposal = new AIColonizationProposal(
                fleet,
                FleetOrderStatus.Readying,
                knownTarget
            );

            double score = new AIFleetProposalScorer().Score(context, proposal);

            Assert.AreEqual(2.0 / 3, score);
        }

        [Test]
        public void Score_OrdinaryDefenseWithHigherPlanetValue_IncreasesPriority()
        {
            AITurnContext context = CreateDefenseContext(out Fleet fleet, out Planet target);
            var scorer = new AIFleetProposalScorer();
            double lowerValue = scorer.Score(context, new AIFleetDefenseProposal(fleet, target));
            target.EnergyCapacity = 1000;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            double higherValue = scorer.Score(context, new AIFleetDefenseProposal(fleet, target));

            Assert.Greater(higherValue, lowerValue);
        }

        [Test]
        public void Score_OrdinaryDefenseWithLongerTravel_DecreasesPriority()
        {
            AITurnContext context = CreateDefenseContext(out Fleet fleet, out Planet target);
            context.Game.Config.AI.FleetDeployment.DefenseUtility.TravelTime.Weight = 0.5;
            target.PositionX = 10;
            var scorer = new AIFleetProposalScorer();
            double nearby = scorer.Score(context, new AIFleetDefenseProposal(fleet, target));
            target.PositionX = 100;

            double distant = scorer.Score(context, new AIFleetDefenseProposal(fleet, target));

            Assert.Less(distant, nearby);
        }

        [Test]
        public void Score_OrdinaryDefenseWithSlowerHyperdrive_DecreasesPriority()
        {
            AITurnContext context = CreateDefenseContext(out Fleet fleet, out Planet target);
            context.Game.Config.AI.FleetDeployment.DefenseUtility.TravelTime.Weight = 0.5;
            target.EnergyCapacity = 1000;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            target.PositionX = 50;
            CapitalShip ship = fleet.GetChildren<CapitalShip>().Single();
            ship.Hyperdrive = 100;
            var scorer = new AIFleetProposalScorer();
            double faster = scorer.Score(context, new AIFleetDefenseProposal(fleet, target));
            ship.Hyperdrive = 200;

            double slower = scorer.Score(context, new AIFleetDefenseProposal(fleet, target));

            Assert.Less(slower, faster);
        }

        [Test]
        public void Score_DefenseAlreadyAtDestination_DoesNotApplyTravelCost()
        {
            AITurnContext context = CreateDefenseContext(out Fleet fleet, out Planet target);
            context.Game.MoveNode(fleet, target);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            var scorer = new AIFleetProposalScorer();
            double before = scorer.Score(context, new AIFleetDefenseProposal(fleet, target));
            context.Game.Config.AI.FleetDeployment.DefenseUtility.TravelTime.Weight = 1;

            double after = scorer.Score(context, new AIFleetDefenseProposal(fleet, target));

            Assert.That(after, Is.EqualTo(before).Within(1e-12));
        }

        [Test]
        public void Score_OrdinaryDefenseInterruptingStagedAttack_DecreasesPriority()
        {
            AITurnContext context = CreateDefenseContext(out Fleet fleet, out Planet target);
            context.Game.Config.AI.FleetDeployment.DefenseUtility.OpportunityCost.Weight = 0.5;
            FleetOrder order = fleet.Order;
            var scorer = new AIFleetProposalScorer();
            double committed = scorer.Score(context, new AIFleetDefenseProposal(fleet, target));
            fleet.Order = null;

            double idle = scorer.Score(context, new AIFleetDefenseProposal(fleet, target));

            Assert.AreEqual(FleetOrderType.Attack, order.OrderType);
            Assert.Less(committed, idle);
        }

        [Test]
        public void Score_HeadquartersDefenseWithDiversionCosts_PreservesPriority()
        {
            AITurnContext context = CreateDefenseContext(out Fleet fleet, out Planet target);
            target.IsHeadquarters = true;
            context.Faction.HQInstanceID = target.InstanceID;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            var scorer = new AIFleetProposalScorer();
            double before = scorer.Score(context, new AIFleetDefenseProposal(fleet, target));
            context.Game.Config.AI.FleetDeployment.DefenseUtility.TravelTime.Weight = 1;
            context.Game.Config.AI.FleetDeployment.DefenseUtility.OpportunityCost.Weight = 1;
            target.PositionX = 1000;

            double after = scorer.Score(context, new AIFleetDefenseProposal(fleet, target));

            Assert.AreEqual(before, after);
        }

        [TestCase(10, 100, 1, false)]
        [TestCase(1000, 1, 0.2, true)]
        public void Score_CompetingDefenseAndStagedAttack_SelectsHigherValueOrder(
            int targetValue,
            int distance,
            double attackWeight,
            bool selectsDefense
        )
        {
            AITurnContext context = CreateDefenseContext(out Fleet fleet, out Planet target);
            target.EnergyCapacity = targetValue;
            target.PositionX = distance;
            context.Game.Config.AI.FleetDeployment.DefenseUtility.TravelTime.Weight = 0.5;
            context.Game.Config.AI.FleetDeployment.DefenseUtility.OpportunityCost.Weight = 0.5;
            context.Game.Config.AI.FleetDeployment.AttackUtility.ExistingOrder.Weight =
                attackWeight;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            var defense = new AIFleetDefenseProposal(fleet, target);
            var attack = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                context.Assessment.GetKnownPlanet(fleet.Order.TargetPlanetId)
            );
            context.AddProposal(defense);
            context.AddProposal(attack);
            new AIScoringPhase().Execute(context);

            var selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(
                new AIProposal[] { selectsDefense ? defense : attack },
                selected
            );
        }

        [Test]
        public void Score_FleetDefenseProposal_ReturnsConfiguredScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.DefenseUtility.Base.Weight = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fleet-world",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, fleetPlanet);
            game.AttachNode(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID), fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double score = new AIFleetProposalScorer().Score(
                context,
                new AIFleetDefenseProposal(fleet, headquarters)
            );

            Assert.Greater(score, 0);
            Assert.LessOrEqual(score, 1);
        }

        [Test]
        public void Score_HeadquartersDefenseTransfer_ReturnsConfiguredDefenseScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.DefenseUtility.Base.Weight = 1;
            game.Config.AI.FleetDeployment.AttackUtility.Readiness.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.TravelEfficiency.Weight = 0;
            game.Config.AI.FleetDeployment.AttackUtility.OpportunityCost.Weight = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Fleet sourceFleet = EntityFactory.CreateFleet("source", empire.InstanceID);
            Fleet defenseFleet = EntityFactory.CreateFleet("defense", empire.InstanceID);
            defenseFleet.RoleType = FleetRoleType.Battle;
            defenseFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = headquarters.InstanceID,
            };
            CapitalShip reinforcement = AITestSceneBuilder.CreateCapitalShip(
                "reinforcement",
                empire.InstanceID,
                combatStrength: 500
            );
            game.AttachNode(sourceFleet, staging);
            game.AttachNode(reinforcement, sourceFleet);
            game.AttachNode(defenseFleet, staging);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "defender",
                    empire.InstanceID,
                    combatStrength: 100
                ),
                defenseFleet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AITransferUnitProposal proposal = new AITransferUnitProposal(
                sourceFleet,
                defenseFleet,
                reinforcement,
                defenseFleet,
                headquarters
            );

            double score = new AIFleetProposalScorer().Score(context, proposal);

            Assert.AreEqual(0.5, score);
        }

        [Test]
        public void Score_HeadquartersDefenseTransferWithProjectedRequirementMet_ReturnsZero()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Fleet sourceFleet = EntityFactory.CreateFleet("source", empire.InstanceID);
            Fleet defenseFleet = EntityFactory.CreateFleet("defense", empire.InstanceID);
            defenseFleet.RoleType = FleetRoleType.Battle;
            defenseFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = headquarters.InstanceID,
            };
            CapitalShip reinforcement = AITestSceneBuilder.CreateCapitalShip(
                "reinforcement",
                empire.InstanceID,
                combatStrength: 500
            );
            CapitalShip inboundShip = AITestSceneBuilder.CreateCapitalShip(
                "inbound",
                empire.InstanceID,
                combatStrength: 900
            );
            inboundShip.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(sourceFleet, staging);
            game.AttachNode(reinforcement, sourceFleet);
            game.AttachNode(defenseFleet, staging);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "defender",
                    empire.InstanceID,
                    combatStrength: 100
                ),
                defenseFleet
            );
            game.AttachNode(inboundShip, defenseFleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AITransferUnitProposal proposal = new AITransferUnitProposal(
                sourceFleet,
                defenseFleet,
                reinforcement,
                defenseFleet,
                headquarters
            );

            double score = new AIFleetProposalScorer().Score(context, proposal);

            Assert.Zero(score);
        }

        /// <summary>
        /// Creates equally valuable targets with different intelligence ages.
        /// </summary>
        /// <param name="intelAge">The older target's intelligence age.</param>
        /// <returns>The context, available fleet, older target, and fresh target.</returns>
        private static (
            AITurnContext Context,
            Fleet Fleet,
            Planet Target,
            Planet FreshTarget
        ) CreateIntelAgeContext(int intelAge)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            var config = game.Config.AI.FleetDeployment;
            config.AttackIntelAgeGraceTicks = 200;
            config.AttackUtility = new GameConfig.AIAttackUtilityConfig
            {
                StrategicValue = new GameConfig.AIConsiderationConfig { Weight = 1 },
                OrbitalAdvantage = new GameConfig.AIConsiderationConfig { Weight = 1 },
                IntelAgeRisk = new GameConfig.AIConsiderationConfig
                {
                    Weight = 0.95,
                    SaturationValue = 200,
                },
            };
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            Planet fresh = AITestSceneBuilder.AddPlanet(game, sector, "fresh", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(game, origin, "fleet", empire.InstanceID, 10000);
            fleet.GetChildren<CapitalShip>().Single().HasGravityWell = true;
            AddBattleFleet(game, target, "hostile", rebels.InstanceID, 100);
            AddBattleFleet(game, fresh, "fresh-hostile", rebels.InstanceID, 100);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick = intelAge;
            AITestSceneBuilder.RevealPlanet(game, empire, fresh);
            return (AITestSceneBuilder.CreateContext(game, empire), fleet, target, fresh);
        }

        /// <summary>
        /// Creates an invasion or orbital engagement for the supplied fleet and target.
        /// </summary>
        /// <param name="context">The faction turn context.</param>
        /// <param name="fleet">The fleet to assign.</param>
        /// <param name="target">The known target.</param>
        /// <param name="engagement">Whether the proposal engages orbital defenders only.</param>
        /// <returns>The offensive proposal.</returns>
        private static AIProposal CreateOffensiveProposal(
            AITurnContext context,
            Fleet fleet,
            Planet target,
            bool engagement
        )
        {
            return engagement
                ? new AIOrbitalEngagementProposal(
                    fleet,
                    target,
                    context.Assessment.GetFleetPlanet(fleet)
                )
                : new AIFleetAttackProposal(
                    fleet,
                    FleetOrderType.Attack,
                    FleetOrderStatus.Staging,
                    target
                );
        }

        /// <summary>
        /// Creates a threatened planet and a fleet assembling for an alternative attack.
        /// </summary>
        /// <param name="fleet">Receives the available battle fleet.</param>
        /// <param name="target">Receives the threatened owned planet.</param>
        /// <returns>The faction turn containing both competing destinations.</returns>
        private static AITurnContext CreateDefenseContext(out Fleet fleet, out Planet target)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            game.Config.Movement.DistanceDivisor = 1;
            game.Config.Movement.SameSectorMinTransitTicks = 1;
            game.Config.AI.FleetDeployment.DefenseUtility = new GameConfig.AIDefenseUtilityConfig
            {
                Base = new GameConfig.AIConsiderationConfig { Weight = 1 },
                TravelTime = new GameConfig.AIConsiderationConfig { SaturationValue = 100 },
            };
            game.Config.AI.FleetDeployment.AttackUtility = new GameConfig.AIAttackUtilityConfig
            {
                ExistingOrder = new GameConfig.AIConsiderationConfig { Weight = 1 },
            };
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "origin",
                faction.InstanceID,
                energyCapacity: 1000
            );
            target = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "defense",
                faction.InstanceID,
                energyCapacity: 100
            );
            Planet attack = AITestSceneBuilder.AddPlanet(game, sector, "attack", enemy.InstanceID);
            fleet = AddAssaultFleet(game, origin, "fleet", faction.InstanceID);
            fleet.GetChildren<CapitalShip>().Single().Hyperdrive = 100;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = attack.InstanceID,
            };
            AddBattleFleet(game, target, "enemy", enemy.InstanceID, 10);
            AITestSceneBuilder.RevealPlanet(game, faction, attack);
            return AITestSceneBuilder.CreateContext(game, faction);
        }

        /// <summary>
        /// Adds assault fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <returns>The result of add assault fleet.</returns>
        private static Fleet AddAssaultFleet(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(instanceId, ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                $"{instanceId}-ship",
                ownerInstanceId
            );
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment($"{instanceId}-regiment", ownerInstanceId),
                ship
            );
            return fleet;
        }

        /// <summary>
        /// Adds battle fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="combatStrength">The combat strength.</param>
        /// <returns>The result of add battle fleet.</returns>
        private static Fleet AddBattleFleet(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId,
            int combatStrength
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(instanceId, ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    $"{instanceId}-ship",
                    ownerInstanceId,
                    combatStrength
                ),
                fleet
            );
            return fleet;
        }

        /// <summary>
        /// Adds shield.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="strength">The strength.</param>
        private static void AddShield(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId,
            int strength
        )
        {
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                instanceId,
                BuildingType.Defense
            );
            shield.OwnerInstanceID = ownerInstanceId;
            shield.ShieldStrength = strength;
            game.AttachNode(shield, planet);
        }
    }
}
