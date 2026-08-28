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
        [Test]
        public void Score_AttackProposalForHeadquarters_ReturnsHigherScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrategicValueWeight = 0;
            game.Config.AI.FleetDeployment.AttackReadinessWeight = 0;
            game.Config.AI.FleetDeployment.AttackCaptureViabilityWeight = 0;
            game.Config.AI.FleetDeployment.AttackTravelEfficiencyWeight = 0;
            game.Config.AI.FleetDeployment.AttackExpectedLossPenaltyWeight = 0;
            game.Config.AI.FleetDeployment.AttackOpportunityCostPenaltyWeight = 0;
            game.Config.AI.FleetDeployment.HeadquartersAttackBonus = 50;
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
            game.Config.AI.FleetDeployment.AttackStrategicValueWeight = 0;
            game.Config.AI.FleetDeployment.AttackReadinessWeight = 0;
            game.Config.AI.FleetDeployment.AttackCaptureViabilityWeight = 0;
            game.Config.AI.FleetDeployment.AttackTravelEfficiencyWeight = 0;
            game.Config.AI.FleetDeployment.AttackExpectedLossPenaltyWeight = 0;
            game.Config.AI.FleetDeployment.AttackOpportunityCostPenaltyWeight = 0;
            game.Config.AI.FleetDeployment.OrbitalResponseBonus = 250;
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

            Assert.AreEqual(game.Config.AI.FleetDeployment.OrbitalResponseBonus, score);
        }

        [Test]
        public void Score_ExistingAttackOrderWithHighOpportunityCost_RetainsOrderBonus()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrategicValueWeight = 0;
            game.Config.AI.FleetDeployment.AttackReadinessWeight = 0;
            game.Config.AI.FleetDeployment.AttackCaptureViabilityWeight = 0;
            game.Config.AI.FleetDeployment.AttackTravelEfficiencyWeight = 0;
            game.Config.AI.FleetDeployment.AttackExpectedLossPenaltyWeight = 0;
            game.Config.AI.FleetDeployment.AttackOpportunityCostPenaltyWeight = 100;
            game.Config.AI.FleetDeployment.ExistingAttackOrderBonus = 25;
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

            Assert.AreEqual(game.Config.AI.FleetDeployment.ExistingAttackOrderBonus, score);
        }

        [Test]
        public void Score_AttackProposalWithSplitLocalDefense_AppliesOpportunityCost()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrategicValueWeight = 100;
            game.Config.AI.FleetDeployment.AttackReadinessWeight = 0;
            game.Config.AI.FleetDeployment.AttackCaptureViabilityWeight = 0;
            game.Config.AI.FleetDeployment.AttackTravelEfficiencyWeight = 0;
            game.Config.AI.FleetDeployment.AttackExpectedLossPenaltyWeight = 0;
            game.Config.AI.FleetDeployment.AttackOpportunityCostPenaltyWeight = 100;
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

            Assert.AreEqual(60, score);
        }

        [Test]
        public void Score_ShieldedAttackProposal_IncludesStarfighterBombardment()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrategicValueWeight = 0;
            game.Config.AI.FleetDeployment.AttackReadinessWeight = 100;
            game.Config.AI.FleetDeployment.AttackCaptureViabilityWeight = 0;
            game.Config.AI.FleetDeployment.AttackTravelEfficiencyWeight = 0;
            game.Config.AI.FleetDeployment.AttackExpectedLossPenaltyWeight = 0;
            game.Config.AI.FleetDeployment.AttackOpportunityCostPenaltyWeight = 0;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, target, "shield-1", rebels.InstanceID, 5);
            AddShield(game, target, "shield-2", rebels.InstanceID, 5);
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

        [Test]
        public void Score_AttackTransferWithCarriedStarfighters_IncludesSquadronStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrategicValueWeight = 0;
            game.Config.AI.FleetDeployment.AttackReadinessWeight = 100;
            game.Config.AI.FleetDeployment.AttackCaptureViabilityWeight = 0;
            game.Config.AI.FleetDeployment.AttackTravelEfficiencyWeight = 0;
            game.Config.AI.FleetDeployment.AttackExpectedLossPenaltyWeight = 0;
            game.Config.AI.FleetDeployment.AttackOpportunityCostPenaltyWeight = 0;
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
            game.Config.AI.FleetDeployment.AttackStrategicValueWeight = 0;
            game.Config.AI.FleetDeployment.AttackReadinessWeight = 100;
            game.Config.AI.FleetDeployment.AttackTravelEfficiencyWeight = 0;
            game.Config.AI.FleetDeployment.AttackOpportunityCostPenaltyWeight = 0;
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
            game.Config.AI.FleetDeployment.ColonizationBaseScore = 10;
            game.Config.AI.FleetDeployment.ColonizationStrategicValueWeight = 0;
            game.Config.AI.FleetDeployment.ColonizationTravelEfficiencyWeight = 0;
            game.Config.AI.FleetDeployment.ColonizationReadyFleetBonus = 30;
            game.Config.AI.FleetDeployment.ColonizationOpportunityCostPenaltyWeight = 0;
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

            Assert.AreEqual(10, unloadedScore);
            Assert.AreEqual(40, loadedScore);
        }

        [Test]
        public void Score_ExistingColonizationOrderOnColonizationFleet_AddsContinuationBonus()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.ColonizationBaseScore = 10;
            game.Config.AI.FleetDeployment.ColonizationStrategicValueWeight = 0;
            game.Config.AI.FleetDeployment.ColonizationTravelEfficiencyWeight = 0;
            game.Config.AI.FleetDeployment.ColonizationReadyFleetBonus = 0;
            game.Config.AI.FleetDeployment.ColonizationOpportunityCostPenaltyWeight = 0;
            game.Config.AI.FleetDeployment.ExistingColonizationOrderBonus = 20;
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

            Assert.AreEqual(30, score);
        }

        [Test]
        public void Score_FleetDefenseProposal_ReturnsConfiguredScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.FleetDefenseScore = 700;
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

            Assert.AreEqual(game.Config.AI.FleetDeployment.FleetDefenseScore, score);
        }

        [Test]
        public void Score_HeadquartersDefenseTransfer_ReturnsConfiguredDefenseScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.FleetDefenseScore = 700;
            game.Config.AI.FleetDeployment.AttackReadinessWeight = 0;
            game.Config.AI.FleetDeployment.AttackTravelEfficiencyWeight = 0;
            game.Config.AI.FleetDeployment.AttackOpportunityCostPenaltyWeight = 0;
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

            Assert.AreEqual(game.Config.AI.FleetDeployment.FleetDefenseScore, score);
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
            shield.DefenseFacilityClass = DefenseFacilityClass.Shield;
            shield.ShieldStrength = strength;
            game.AttachNode(shield, planet);
        }
    }
}
