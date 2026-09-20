using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Scoring
{
    [TestFixture]
    public class AIFleetProposalScorerTests
    {
        /// <summary>
        /// Verifies score returning attack fleet in hostile territory returns highest score.
        /// </summary>
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

        /// <summary>
        /// Verifies evacuation receives full utility and mandatory priority.
        /// </summary>
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
            Fleet fleet = AddBattleFleet(
                game,
                hostile,
                "fleet",
                empire.InstanceID,
                combatStrength: 100
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetEvacuationProposal proposal = new AIFleetEvacuationProposal(fleet, hostile);
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double score = scorer.Score(context, proposal);

            Assert.IsTrue(scorer.CanScore(proposal));
            Assert.AreEqual(1, score);
            Assert.AreEqual(AIProposalPriority.Mandatory, proposal.Priority);
        }

        /// <summary>
        /// Verifies score attack proposal for headquarters returns higher score.
        /// </summary>
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

        /// <summary>
        /// Verifies score attack proposal with orbital advantage applies response bonus.
        /// </summary>
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

        /// <summary>
        /// Verifies score exposed bombardment target with sector leverage prioritizes capable fleet.
        /// </summary>
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

        /// <summary>
        /// Verifies get new attack score upper bound with attack proposal does not underestimate score.
        /// </summary>
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

        /// <summary>
        /// Verifies score attack proposal with older intelligence returns lower score.
        /// </summary>
        [Test]
        public void Score_AttackProposalWithOlderIntelligence_ReturnsLowerScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            GameConfig.AIFleetDeploymentConfig config = game.Config.AI.FleetDeployment;
            config.AttackUtility.StrategicValue.Weight = 1;
            config.AttackUtility.SectorSupport.Weight = 0;
            config.AttackUtility.SystemPresence.Weight = 0;
            config.AttackUtility.Readiness.Weight = 0;
            config.AttackUtility.CaptureViability.Weight = 0;
            config.AttackUtility.TravelEfficiency.Weight = 0;
            config.AttackUtility.ExpectedLossRisk.Weight = 0;
            config.AttackUtility.OpportunityCost.Weight = 0;
            config.AttackUtility.IntelAgeRisk.Weight = 1;
            config.AttackUtility.OrbitalAdvantage.Weight = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet olderTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "older",
                rebels.InstanceID
            );
            Planet freshTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fresh",
                rebels.InstanceID
            );
            AITestSceneBuilder.RevealPlanet(game, empire, olderTarget);
            game.CurrentTick = 20;
            AITestSceneBuilder.RevealPlanet(game, empire, freshTarget);
            Fleet fleet = AddBattleFleet(
                game,
                owned,
                "fleet",
                empire.InstanceID,
                combatStrength: 1000
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetProposalScorer scorer = new AIFleetProposalScorer();

            double olderScore = scorer.Score(
                context,
                new AIFleetAttackProposal(
                    fleet,
                    FleetOrderType.Attack,
                    FleetOrderStatus.Staging,
                    olderTarget
                )
            );
            double freshScore = scorer.Score(
                context,
                new AIFleetAttackProposal(
                    fleet,
                    FleetOrderType.Attack,
                    FleetOrderStatus.Staging,
                    freshTarget
                )
            );

            Assert.Greater(freshScore, olderScore);
        }

        /// <summary>
        /// Verifies score existing attack order with high opportunity cost retains order bonus.
        /// </summary>
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

        /// <summary>
        /// Verifies score attack proposal with split local defense applies opportunity cost.
        /// </summary>
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

        /// <summary>
        /// Verifies score shielded attack proposal includes starfighter bombardment.
        /// </summary>
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
            capitalOnly.GetChildren<CapitalShip>()[0].Bombardment = 10;
            Fleet combinedArms = AddAssaultFleet(game, owned, "combined", empire.InstanceID);
            combinedArms.GetChildren<CapitalShip>()[0].Bombardment = 10;
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

        /// <summary>
        /// Verifies score attack with low readiness applies floor weight.
        /// </summary>
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

        /// <summary>
        /// Verifies score ready attack applies configured bonus.
        /// </summary>
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

        /// <summary>
        /// Verifies score attack transfer with carried starfighters includes squadron strength.
        /// </summary>
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

        /// <summary>
        /// Verifies score attack transfer with projected requirements met returns zero.
        /// </summary>
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

        /// <summary>
        /// Verifies score colonization proposal with loaded regiment adds readiness bonus.
        /// </summary>
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

        /// <summary>
        /// Verifies colony campaigns favor sectors near an established faction anchor.
        /// </summary>
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

        /// <summary>
        /// Verifies score existing colonization order on colonization fleet adds continuation bonus.
        /// </summary>
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

        /// <summary>
        /// Verifies score fleet defense proposal returns configured score.
        /// </summary>
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

        /// <summary>
        /// Verifies score headquarters defense transfer returns configured defense score.
        /// </summary>
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

        /// <summary>
        /// Verifies score headquarters defense transfer with projected requirement met returns zero.
        /// </summary>
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
