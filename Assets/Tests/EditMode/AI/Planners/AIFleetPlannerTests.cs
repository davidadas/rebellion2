using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public class AIFleetPlannerTests
    {
        [Test]
        public void Plan_WithIdleBattleFleetAndEnemyPlanet_AddsAttackProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID, "fleet");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIFleetAttackProposal>()
                    .Any(proposal =>
                        proposal.Fleet == fleet
                        && proposal.TargetPlanet.InstanceID == enemy.InstanceID
                        && proposal.OrderType == FleetOrderType.Attack
                    )
            );
        }

        [Test]
        public void Plan_WithAssemblingCampaign_AddsAttackOrderForDifferentSystem()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem firstSystem = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                firstSystem,
                "owned",
                empire.InstanceID
            );
            Planet firstEnemy = AITestSceneBuilder.AddPlanet(
                game,
                firstSystem,
                "first-enemy",
                rebels.InstanceID
            );
            PlanetSystem secondSystem = AITestSceneBuilder.AddSystem(game, "sys2");
            Planet secondEnemy = AITestSceneBuilder.AddPlanet(
                game,
                secondSystem,
                "second-enemy",
                rebels.InstanceID
            );
            AITestSceneBuilder.RevealPlanet(game, empire, firstEnemy);
            AITestSceneBuilder.RevealPlanet(game, empire, secondEnemy);
            Fleet assemblingFleet = AddBattleFleet(game, owned, empire.InstanceID, "assembling");
            assemblingFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = firstEnemy.InstanceID,
            };
            Fleet idleFleet = AddBattleFleet(game, owned, empire.InstanceID, "idle");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIFleetAttackProposal>()
                    .Any(proposal =>
                        proposal.Fleet == idleFleet
                        && proposal.TargetPlanet.InstanceID == secondEnemy.InstanceID
                    )
            );
        }

        [Test]
        public void Plan_WithEnemySystems_PrioritizesGreatestFriendlyPresence()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem establishedSystem = AITestSceneBuilder.AddSystem(
                game,
                "established-system"
            );
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                establishedSystem,
                "fleet-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddPlanet(
                game,
                establishedSystem,
                "established-owned-2",
                empire.InstanceID
            );
            AITestSceneBuilder.AddPlanet(
                game,
                establishedSystem,
                "established-owned-3",
                empire.InstanceID
            );
            Planet establishedEnemy = AITestSceneBuilder.AddPlanet(
                game,
                establishedSystem,
                "established-enemy",
                rebels.InstanceID
            );
            PlanetSystem remoteSystem = AITestSceneBuilder.AddSystem(game, "remote-system");
            AITestSceneBuilder.AddPlanet(game, remoteSystem, "remote-owned", empire.InstanceID);
            Planet remoteEnemy = AITestSceneBuilder.AddPlanet(
                game,
                remoteSystem,
                "remote-enemy",
                rebels.InstanceID
            );
            AITestSceneBuilder.RevealPlanet(game, empire, establishedEnemy);
            AITestSceneBuilder.RevealPlanet(game, empire, remoteEnemy);
            Fleet fleet = AddBattleFleet(game, fleetPlanet, empire.InstanceID, "fleet");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIFleetAttackProposal> proposals = new AIFleetPlanner()
                .Plan(context)
                .OfType<AIFleetAttackProposal>()
                .Where(proposal => proposal.Fleet == fleet)
                .ToList();

            Assert.IsNotEmpty(proposals);
            Assert.IsTrue(
                proposals.All(proposal =>
                    proposal.TargetPlanet.InstanceID == establishedEnemy.InstanceID
                )
            );
        }

        [Test]
        public void Plan_WithStagedAttackOrder_AddsAlternativeTargetProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet firstEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "first-enemy",
                rebels.InstanceID
            );
            Planet secondEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "second-enemy",
                rebels.InstanceID
            );
            AITestSceneBuilder.RevealPlanet(game, empire, firstEnemy);
            AITestSceneBuilder.RevealPlanet(game, empire, secondEnemy);
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID, "fleet");
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = firstEnemy.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIFleetAttackProposal> proposals = new AIFleetPlanner()
                .Plan(context)
                .OfType<AIFleetAttackProposal>()
                .Where(proposal => proposal.Fleet == fleet)
                .ToList();

            CollectionAssert.AreEquivalent(
                new[] { firstEnemy.InstanceID, secondEnemy.InstanceID },
                proposals.Select(proposal => proposal.TargetPlanet.InstanceID)
            );
        }

        [Test]
        public void Plan_WithAttackFleetInTransit_DoesNotAddAlternativeTargetProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet firstEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "first-enemy",
                rebels.InstanceID
            );
            Planet secondEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "second-enemy",
                rebels.InstanceID
            );
            AITestSceneBuilder.RevealPlanet(game, empire, firstEnemy);
            AITestSceneBuilder.RevealPlanet(game, empire, secondEnemy);
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID, "fleet");
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Readying,
                TargetPlanetId = firstEnemy.InstanceID,
            };
            fleet.Movement = new MovementState { TransitTicks = 10 };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIFleetAttackProposal> proposals = new AIFleetPlanner()
                .Plan(context)
                .OfType<AIFleetAttackProposal>()
                .Where(proposal => proposal.Fleet == fleet)
                .ToList();

            Assert.AreEqual(1, proposals.Count);
            Assert.AreEqual(firstEnemy.InstanceID, proposals[0].TargetPlanet.InstanceID);
        }

        [Test]
        public void Plan_WithInvalidAttackOrder_DefersOrderCleanupUntilExecution()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID, "fleet");
            FleetOrder staleOrder = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = "missing-target",
            };
            fleet.Order = staleOrder;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIClearFleetOrderProposal proposal = new AIFleetPlanner()
                .Plan(context)
                .OfType<AIClearFleetOrderProposal>()
                .Single();

            Assert.AreSame(staleOrder, fleet.Order);

            proposal.Execute(context);

            Assert.IsNull(fleet.Order);
        }

        [Test]
        public void Plan_WithMaximumAttackOrdersAssigned_DoesNotAddAnotherAttackOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MaximumConcurrentAttackOrders = 1;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet assignedFleet = AddBattleFleet(game, owned, empire.InstanceID, "assigned");
            assignedFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            Fleet idleFleet = AddBattleFleet(game, owned, empire.InstanceID, "idle");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIFleetAttackProposal>()
                    .Any(proposal => proposal.Fleet == idleFleet)
            );
        }

        [Test]
        public void Plan_WithMaximumAttackOrdersAndFavorableOrbitalTarget_AddsResponseProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MaximumConcurrentAttackOrders = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet assignedTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "assigned-target",
                rebels.InstanceID
            );
            Planet responseTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "response-target",
                rebels.InstanceID
            );
            AITestSceneBuilder.RevealPlanet(game, empire, assignedTarget);
            AddBattleFleet(game, responseTarget, rebels.InstanceID, "hostile", combatStrength: 500);
            AITestSceneBuilder.RevealPlanet(game, empire, responseTarget);
            Fleet assignedFleet = AddBattleFleet(game, owned, empire.InstanceID, "assigned");
            assignedFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = assignedTarget.InstanceID,
            };
            Fleet responseFleet = AddBattleFleet(
                game,
                owned,
                empire.InstanceID,
                "response",
                combatStrength: 1000
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIFleetAttackProposal>()
                    .Any(proposal =>
                        proposal.Fleet == responseFleet
                        && proposal.TargetPlanet.InstanceID == responseTarget.InstanceID
                    )
            );
        }

        [Test]
        public void Plan_WithInsufficientFleetAssignedToOrbitalTarget_AddsCapableResponseProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AddBattleFleet(game, target, rebels.InstanceID, "hostile", combatStrength: 1000);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet assignedFleet = AddBattleFleet(
                game,
                owned,
                empire.InstanceID,
                "assigned",
                combatStrength: 1000
            );
            assignedFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            Fleet responseFleet = AddBattleFleet(
                game,
                owned,
                empire.InstanceID,
                "response",
                combatStrength: 1500
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIFleetAttackProposal>()
                    .Any(proposal =>
                        proposal.Fleet == responseFleet
                        && proposal.TargetPlanet.InstanceID == target.InstanceID
                    )
            );
        }

        [Test]
        public void Plan_WithCapableFleetAssignedToOrbitalTarget_DoesNotAddAnotherResponseProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AddBattleFleet(game, target, rebels.InstanceID, "hostile", combatStrength: 1000);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet assignedFleet = AddBattleFleet(
                game,
                owned,
                empire.InstanceID,
                "assigned",
                combatStrength: 1500
            );
            assignedFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Readying,
                TargetPlanetId = target.InstanceID,
            };
            Fleet responseFleet = AddBattleFleet(
                game,
                owned,
                empire.InstanceID,
                "response",
                combatStrength: 1500
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIFleetAttackProposal>()
                    .Any(proposal =>
                        proposal.Fleet == responseFleet
                        && proposal.TargetPlanet.InstanceID == target.InstanceID
                    )
            );
        }

        [Test]
        public void Plan_WithInboundCapitalShipFillingAttackNeed_DoesNotAddTransferProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet targetFleet = AddBattleFleet(game, owned, empire.InstanceID, "target-fleet");
            targetFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            CapitalShip targetShip = targetFleet.CapitalShips.Single();
            targetShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100, 0, 0, 0 };
            CapitalShip inboundShip = AITestSceneBuilder.CreateCapitalShip(
                "inbound-ship",
                empire.InstanceID,
                combatStrength: 400
            );
            inboundShip.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(inboundShip, targetFleet);
            Fleet sourceFleet = AddBattleFleet(game, owned, empire.InstanceID, "source-fleet");
            CapitalShip donor = AITestSceneBuilder.CreateCapitalShip(
                "donor",
                empire.InstanceID,
                combatStrength: 400
            );
            game.AttachNode(donor, sourceFleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AITransferUnitProposal>()
                    .Any(proposal => proposal.TargetFleet == targetFleet)
            );
        }

        [Test]
        public void Plan_WithInboundRegimentFillingAttackNeed_DoesNotAddTransferProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment(
                    "defender",
                    rebels.InstanceID,
                    defenseRating: 100
                ),
                enemy
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet targetFleet = AddBattleFleet(game, owned, empire.InstanceID, "target-fleet");
            targetFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
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
            Fleet sourceFleet = AddBattleFleet(game, owned, empire.InstanceID, "source-fleet");
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("donor", empire.InstanceID),
                sourceFleet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AITransferUnitProposal> proposals = new AIFleetPlanner()
                .Plan(context)
                .OfType<AITransferUnitProposal>()
                .Where(proposal => proposal.TargetFleet == targetFleet)
                .ToList();

            Assert.IsEmpty(proposals);
        }

        [Test]
        public void Plan_WithCarriedStarfightersProvidingMissingCombat_AddsTransferProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet targetFleet = AddBattleFleet(game, owned, empire.InstanceID, "target-fleet");
            targetFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            Fleet sourceFleet = AddBattleFleet(game, owned, empire.InstanceID, "source-fleet");
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
            game.AttachNode(carrier, sourceFleet);
            game.AttachNode(fighter, carrier);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AITransferUnitProposal> proposals = new AIFleetPlanner()
                .Plan(context)
                .OfType<AITransferUnitProposal>()
                .ToList();

            Assert.IsTrue(proposals.Any(proposal => proposal.Unit == carrier));
        }

        [Test]
        public void Plan_WithCarriedStarfightersRequiredForLocalDefense_DoesNotTransferCarrier()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet targetFleet = AddBattleFleet(game, staging, empire.InstanceID, "target-fleet");
            targetFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            Fleet sourceFleet = AddBattleFleet(game, staging, empire.InstanceID, "source-fleet");
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier",
                empire.InstanceID,
                combatStrength: 100,
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
            AddBattleFleet(game, staging, rebels.InstanceID, "hostile-fleet", combatStrength: 300);
            game.AttachNode(carrier, sourceFleet);
            game.AttachNode(fighter, carrier);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AITransferUnitProposal> proposals = new AIFleetPlanner()
                .Plan(context)
                .OfType<AITransferUnitProposal>()
                .ToList();

            Assert.IsFalse(proposals.Any(proposal => proposal.Unit == carrier));
        }

        [Test]
        public void Plan_WithSplitLocalDefenseBelowHostileFleet_DoesNotTransferCapitalShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet targetFleet = AddBattleFleet(
                game,
                staging,
                empire.InstanceID,
                "target-fleet",
                combatStrength: 100
            );
            targetFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            Fleet sourceFleet = AddBattleFleet(
                game,
                staging,
                empire.InstanceID,
                "source-fleet",
                combatStrength: 100
            );
            CapitalShip donor = AITestSceneBuilder.CreateCapitalShip(
                "donor",
                empire.InstanceID,
                combatStrength: 300
            );
            game.AttachNode(donor, sourceFleet);
            AddBattleFleet(game, staging, empire.InstanceID, "local-fleet", combatStrength: 400);
            AddBattleFleet(game, staging, rebels.InstanceID, "hostile-fleet", combatStrength: 500);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AITransferUnitProposal> proposals = new AIFleetPlanner()
                .Plan(context)
                .OfType<AITransferUnitProposal>()
                .ToList();

            Assert.IsFalse(proposals.Any(proposal => proposal.Unit == donor));
        }

        [Test]
        public void Plan_WithPendingCarriedStarfighter_NotCountedAsCurrentSourceDefense()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet targetFleet = AddBattleFleet(
                game,
                staging,
                empire.InstanceID,
                "target-fleet",
                combatStrength: 200
            );
            targetFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            Fleet sourceFleet = AddBattleFleet(game, staging, empire.InstanceID, "source-fleet");
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 0,
                starfighterCapacity: 1
            );
            Starfighter pendingFighter = new Starfighter
            {
                InstanceID = "pending-fighter",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Building,
                LaserCannon = 400,
                MaxSquadronSize = 1,
            };
            AddBattleFleet(game, staging, rebels.InstanceID, "hostile-fleet");
            game.AttachNode(carrier, sourceFleet);
            game.AttachNode(pendingFighter, carrier);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AITransferUnitProposal> proposals = new AIFleetPlanner()
                .Plan(context)
                .OfType<AITransferUnitProposal>()
                .ToList();

            Assert.IsTrue(proposals.Any(proposal => proposal.Unit == carrier));
        }

        [Test]
        public void Plan_WithCapitalShipProductionAvailable_StillAddsTransferProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.AI.Selection.MinimumMaintenanceHeadroomAfterProduction = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                staging,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet targetFleet = AddBattleFleet(game, staging, empire.InstanceID, "target-fleet");
            targetFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            Fleet sourceFleet = AddBattleFleet(game, staging, empire.InstanceID, "source-fleet");
            CapitalShip donor = AITestSceneBuilder.CreateCapitalShip(
                "donor",
                empire.InstanceID,
                combatStrength: 400
            );
            game.AttachNode(donor, sourceFleet);
            CapitalShip template = AITestSceneBuilder.CreateCapitalShip(
                "template",
                empire.InstanceID,
                combatStrength: 400
            );
            template.TypeID = "capital-template";
            template.MaintenanceCost = 0;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(template),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AITransferUnitProposal> proposals = new AIFleetPlanner()
                .Plan(context)
                .OfType<AITransferUnitProposal>()
                .ToList();

            Assert.IsTrue(proposals.Any(proposal => proposal.Unit == donor));
        }

        [Test]
        public void Plan_WithIdleBattleFleetAndUncolonizedPlanet_AddsColonizationProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID, "fleet");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIColonizationProposal>()
                    .Any(proposal =>
                        proposal.Fleet == fleet
                        && proposal.TargetPlanet.InstanceID == target.InstanceID
                    )
            );
        }

        [Test]
        public void Plan_WithExistingColonizationOrder_AddsContinuationProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID, "fleet");
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Colonize,
                Status = FleetOrderStatus.Readying,
                TargetPlanetId = target.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            AIColonizationProposal proposal = proposals.OfType<AIColonizationProposal>().Single();
            Assert.AreSame(fleet, proposal.Fleet);
            Assert.AreEqual(target.InstanceID, proposal.TargetPlanet.InstanceID);
        }

        [Test]
        public void Plan_WithMaximumColonizationOrdersAssigned_DoesNotAddAnotherColonizationOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MaximumConcurrentColonizationOrders = 1;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet firstTarget = AITestSceneBuilder.AddPlanet(game, system, "target-1", null);
            firstTarget.IsColonized = false;
            Planet secondTarget = AITestSceneBuilder.AddPlanet(game, system, "target-2", null);
            secondTarget.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, firstTarget);
            AITestSceneBuilder.RevealPlanet(game, empire, secondTarget);
            Fleet assignedFleet = AddBattleFleet(game, owned, empire.InstanceID, "assigned");
            assignedFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Colonize,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = firstTarget.InstanceID,
            };
            Fleet idleFleet = AddBattleFleet(game, owned, empire.InstanceID, "idle");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIColonizationProposal>()
                    .Any(proposal => proposal.Fleet == idleFleet)
            );
        }

        [Test]
        public void Plan_WithUnguardedHeadquarters_AddsNearestSufficientDefenseFleetProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            PlanetSystem headquartersSystem = AITestSceneBuilder.AddSystem(game, "hq-system");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                headquartersSystem,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            PlanetSystem nearSystem = AITestSceneBuilder.AddSystem(
                game,
                "near-system",
                positionX: 5
            );
            Planet nearPlanet = AITestSceneBuilder.AddPlanet(
                game,
                nearSystem,
                "near",
                empire.InstanceID
            );
            PlanetSystem farSystem = AITestSceneBuilder.AddSystem(
                game,
                "far-system",
                positionX: 100
            );
            Planet farPlanet = AITestSceneBuilder.AddPlanet(
                game,
                farSystem,
                "far",
                empire.InstanceID
            );
            Fleet nearFleet = AddBattleFleet(
                game,
                nearPlanet,
                empire.InstanceID,
                "near-fleet",
                combatStrength: 1000
            );
            AddBattleFleet(game, farPlanet, empire.InstanceID, "far-fleet", combatStrength: 2000);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIFleetDefenseProposal proposal = new AIFleetPlanner()
                .Plan(context)
                .OfType<AIFleetDefenseProposal>()
                .Single();

            Assert.AreSame(nearFleet, proposal.Fleet);
            Assert.AreSame(headquarters, proposal.TargetPlanet);
        }

        [Test]
        public void Plan_WithHostileFleetAtHeadquarters_AddsSufficientDefenseFleetProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSystem headquartersSystem = AITestSceneBuilder.AddSystem(game, "hq-system");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                headquartersSystem,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            AddBattleFleet(
                game,
                headquarters,
                rebels.InstanceID,
                "hostile-fleet",
                combatStrength: 2000
            );
            PlanetSystem nearSystem = AITestSceneBuilder.AddSystem(
                game,
                "near-system",
                positionX: 5
            );
            Planet nearPlanet = AITestSceneBuilder.AddPlanet(
                game,
                nearSystem,
                "near",
                empire.InstanceID
            );
            PlanetSystem farSystem = AITestSceneBuilder.AddSystem(
                game,
                "far-system",
                positionX: 100
            );
            Planet farPlanet = AITestSceneBuilder.AddPlanet(
                game,
                farSystem,
                "far",
                empire.InstanceID
            );
            AddBattleFleet(game, nearPlanet, empire.InstanceID, "near-fleet", combatStrength: 1000);
            Fleet sufficientFleet = AddBattleFleet(
                game,
                farPlanet,
                empire.InstanceID,
                "far-fleet",
                combatStrength: 3000
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIFleetDefenseProposal proposal = new AIFleetPlanner()
                .Plan(context)
                .OfType<AIFleetDefenseProposal>()
                .Single();

            Assert.AreSame(sufficientFleet, proposal.Fleet);
        }

        [Test]
        public void Plan_WithOnlyInsufficientFleetForThreat_AddsStagingDefenseProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSystem headquartersSystem = AITestSceneBuilder.AddSystem(game, "hq-system");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                headquartersSystem,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            AddBattleFleet(
                game,
                headquarters,
                rebels.InstanceID,
                "hostile-fleet",
                combatStrength: 2000
            );
            Planet reservePlanet = AITestSceneBuilder.AddPlanet(
                game,
                headquartersSystem,
                "reserve",
                empire.InstanceID
            );
            Fleet reserveFleet = AddBattleFleet(
                game,
                reservePlanet,
                empire.InstanceID,
                "reserve-fleet",
                combatStrength: 2000
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIFleetDefenseProposal proposal = new AIFleetPlanner()
                .Plan(context)
                .OfType<AIFleetDefenseProposal>()
                .Single();

            Assert.AreSame(reserveFleet, proposal.Fleet);
            Assert.AreSame(headquarters, proposal.TargetPlanet);
        }

        [Test]
        public void Plan_WithExistingHeadquartersDefenseOrder_AddsContinuationProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "system");
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
            Fleet fleet = AddBattleFleet(
                game,
                fleetPlanet,
                empire.InstanceID,
                "defense-fleet",
                combatStrength: 1000
            );
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = headquarters.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIFleetDefenseProposal proposal = new AIFleetPlanner()
                .Plan(context)
                .OfType<AIFleetDefenseProposal>()
                .Single();

            Assert.AreSame(fleet, proposal.Fleet);
            Assert.AreEqual(headquarters.InstanceID, proposal.TargetPlanet.InstanceID);
        }

        [Test]
        public void Plan_WithThreatenedOwnedPlanet_AddsNearestSufficientDefenseProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSystem targetSystem = AITestSceneBuilder.AddSystem(game, "target-system");
            Planet targetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                targetSystem,
                "target",
                empire.InstanceID
            );
            AddBattleFleet(
                game,
                targetPlanet,
                rebels.InstanceID,
                "hostile-fleet",
                combatStrength: 500
            );
            PlanetSystem nearSystem = AITestSceneBuilder.AddSystem(
                game,
                "near-system",
                positionX: 5
            );
            Planet nearPlanet = AITestSceneBuilder.AddPlanet(
                game,
                nearSystem,
                "near",
                empire.InstanceID
            );
            Fleet nearFleet = AddBattleFleet(
                game,
                nearPlanet,
                empire.InstanceID,
                "near-fleet",
                combatStrength: 700
            );
            PlanetSystem farSystem = AITestSceneBuilder.AddSystem(
                game,
                "far-system",
                positionX: 100
            );
            Planet farPlanet = AITestSceneBuilder.AddPlanet(
                game,
                farSystem,
                "far",
                empire.InstanceID
            );
            AddBattleFleet(game, farPlanet, empire.InstanceID, "far-fleet", combatStrength: 1000);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIFleetDefenseProposal proposal = new AIFleetPlanner()
                .Plan(context)
                .OfType<AIFleetDefenseProposal>()
                .Single();

            Assert.AreSame(nearFleet, proposal.Fleet);
            Assert.AreSame(targetPlanet, proposal.TargetPlanet);
        }

        [Test]
        public void Plan_WithInboundHostileFleet_DispatchesDefenseBeforeArrival()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "system");
            Planet targetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "target",
                empire.InstanceID
            );
            Fleet hostileFleet = AddBattleFleet(
                game,
                targetPlanet,
                rebels.InstanceID,
                "hostile-fleet",
                combatStrength: 500
            );
            hostileFleet.Movement = new MovementState { TransitTicks = 10 };
            Planet reservePlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "reserve",
                empire.InstanceID,
                positionX: 5
            );
            Fleet reserveFleet = AddBattleFleet(
                game,
                reservePlanet,
                empire.InstanceID,
                "reserve-fleet",
                combatStrength: 700
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIFleetDefenseProposal proposal = new AIFleetPlanner()
                .Plan(context)
                .OfType<AIFleetDefenseProposal>()
                .Single();

            Assert.AreSame(reserveFleet, proposal.Fleet);
            Assert.AreSame(targetPlanet, proposal.TargetPlanet);
            Assert.AreEqual(0, context.Assessment.GetRequiredOrbitalStrength(targetPlanet));
            Assert.AreEqual(625, context.Assessment.GetRequiredPlanetDefenseStrength(targetPlanet));
        }

        [Test]
        public void Plan_WithThreatenedOwnedPlanetAndNoSufficientFleet_DoesNotAddDefenseProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "system");
            Planet targetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "target",
                empire.InstanceID
            );
            AddBattleFleet(
                game,
                targetPlanet,
                rebels.InstanceID,
                "hostile-fleet",
                combatStrength: 1000
            );
            Planet reservePlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "reserve",
                empire.InstanceID
            );
            AddBattleFleet(
                game,
                reservePlanet,
                empire.InstanceID,
                "reserve-fleet",
                combatStrength: 1000
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIFleetDefenseProposal>()
                    .Any(proposal => proposal.TargetPlanet == targetPlanet)
            );
        }

        [Test]
        public void Plan_WithAttackFleetRequiredAtHeadquarters_AddsOrderCleanupProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "system");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet fleet = AddBattleFleet(
                game,
                headquarters,
                empire.InstanceID,
                "attack-fleet",
                combatStrength: 1000
            );
            FleetOrder order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            fleet.Order = order;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIClearFleetOrderProposal proposal = new AIFleetPlanner()
                .Plan(context)
                .OfType<AIClearFleetOrderProposal>()
                .Single();

            Assert.AreSame(order, fleet.Order);

            proposal.Execute(context);

            Assert.IsNull(fleet.Order);
        }

        [Test]
        public void Plan_WithUnderstrengthHeadquartersDefenseFleet_AddsTransferProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "system");
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
            Fleet defenseFleet = AddBattleFleet(
                game,
                staging,
                empire.InstanceID,
                "defense-fleet",
                combatStrength: 100
            );
            defenseFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = headquarters.InstanceID,
            };
            Fleet sourceFleet = AddBattleFleet(
                game,
                staging,
                empire.InstanceID,
                "source-fleet",
                combatStrength: 100
            );
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "reinforcement",
                    empire.InstanceID,
                    combatStrength: 500
                ),
                sourceFleet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AITransferUnitProposal proposal = new AIFleetPlanner()
                .Plan(context)
                .OfType<AITransferUnitProposal>()
                .FirstOrDefault(candidate => candidate.TargetFleet == defenseFleet);

            Assert.IsNotNull(proposal);
            Assert.AreSame(sourceFleet, proposal.SourceContainer);
            Assert.AreEqual(headquarters.InstanceID, proposal.TargetPlanet.InstanceID);
        }

        [Test]
        public void Plan_WithProjectedHeadquartersDefenseStrengthMet_DoesNotAddTransferProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "system");
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
            Fleet defenseFleet = AddBattleFleet(
                game,
                staging,
                empire.InstanceID,
                "defense-fleet",
                combatStrength: 100
            );
            defenseFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = headquarters.InstanceID,
            };
            CapitalShip inboundShip = AITestSceneBuilder.CreateCapitalShip(
                "inbound-ship",
                empire.InstanceID,
                combatStrength: 900
            );
            inboundShip.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(inboundShip, defenseFleet);
            Fleet sourceFleet = AddBattleFleet(
                game,
                staging,
                empire.InstanceID,
                "source-fleet",
                combatStrength: 100
            );
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "reinforcement",
                    empire.InstanceID,
                    combatStrength: 500
                ),
                sourceFleet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AITransferUnitProposal> proposals = new AIFleetPlanner()
                .Plan(context)
                .OfType<AITransferUnitProposal>()
                .Where(proposal => proposal.TargetFleet == defenseFleet)
                .ToList();

            Assert.IsEmpty(proposals);
        }

        [Test]
        public void Plan_WithInboundHeadquartersDefense_DoesNotAddAnotherDefenseProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "system");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Fleet inboundFleet = AddBattleFleet(
                game,
                headquarters,
                empire.InstanceID,
                "inbound-fleet",
                combatStrength: 1000
            );
            inboundFleet.Movement = new MovementState { TransitTicks = 10 };
            Planet reservePlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "reserve",
                empire.InstanceID
            );
            AddBattleFleet(
                game,
                reservePlanet,
                empire.InstanceID,
                "reserve-fleet",
                combatStrength: 1000
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsFalse(proposals.OfType<AIFleetDefenseProposal>().Any());
        }

        private static Fleet AddBattleFleet(
            GameRoot game,
            Planet planet,
            string ownerInstanceId,
            string fleetId,
            int combatStrength = 100
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(fleetId, ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                $"{fleetId}-ship",
                ownerInstanceId,
                combatStrength
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, planet);
            return fleet;
        }
    }
}
