using System.Collections.Generic;
using System.Drawing;
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
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Tests.AI.Helpers;
using Rebellion.Util.Random;

namespace Rebellion.Tests.AI.Fleets
{
    [TestFixture]
    public class AIFleetAttackProposalTests
    {
        [TestCase(20, false)]
        [TestCase(21, true)]
        public void Execute_KnownEnemyReinforcement_UsesArrivalDemandForLaunch(
            int inboundTicks,
            bool expectedLaunch
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            ConfigureMinimalAttackRequirements(game);
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 100;
            game.Config.Movement.MinTransitTicks = 20;
            game.Config.Movement.SameSectorMinTransitTicks = 20;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "staging",
                faction.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "target",
                enemy.InstanceID,
                positionX: 1
            );
            Fleet attacker = AddBattleFleet(game, staging, faction.InstanceID, regimentCount: 6);
            CapitalShip attackingShip = attacker.GetChildren<CapitalShip>().Single();
            attackingShip.MaxHullStrength = 1000;
            attackingShip.CurrentHullStrength = 1000;
            foreach (PrimaryWeaponType weapon in attackingShip.PrimaryWeapons.Keys.ToArray())
                attackingShip.PrimaryWeapons[weapon] = new int[5];
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[]
            {
                250,
                250,
                250,
                250,
                0,
            };
            Fleet defender = EntityFactory.CreateFleet("defender", enemy.InstanceID);
            game.AttachNode(defender, target);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("ready", enemy.InstanceID, 100),
                defender
            );
            CapitalShip inbound = AITestSceneBuilder.CreateCapitalShip(
                "inbound",
                enemy.InstanceID,
                1000
            );
            inbound.Movement = new MovementState { TransitTicks = inboundTicks };
            game.AttachNode(inbound, defender);
            AITestSceneBuilder.RevealPlanet(game, faction, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                attacker,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            proposal.Execute(context);

            Assert.AreEqual(expectedLaunch, attacker.Movement != null);
        }

        [TestCase(true, 50, 50, true)]
        [TestCase(true, 51, 50, false)]
        [TestCase(true, 51, 60, true)]
        [TestCase(true, 31, 30, false)]
        [TestCase(false, 51, 50, true)]
        public void Execute_TargetIntelAge_EnforcesHeadquartersLimit(
            bool headquarters,
            int intelAge,
            int maximumAge,
            bool expectedLaunch
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            ConfigureMinimalAttackRequirements(game);
            game.Config.AI.FleetDeployment.HeadquartersAttackMaximumIntelAgeTicks = maximumAge;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "staging",
                faction.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", enemy.InstanceID);
            target.IsHeadquarters = headquarters;
            Fleet fleet = AddBattleFleet(game, staging, faction.InstanceID, regimentCount: 6);
            AITestSceneBuilder.RevealPlanet(game, faction, target);
            game.CurrentTick = intelAge;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            proposal.Execute(context);

            Assert.AreEqual(expectedLaunch, fleet.Movement != null);
        }

        [TestCase(FleetOrderStatus.Building, false, false)]
        [TestCase(FleetOrderStatus.Returning, false, true)]
        [TestCase(FleetOrderStatus.Readying, true, true)]
        public void CanExecute_HeadquartersIntelExpires_RespectsCommittedMovementAndReturn(
            FleetOrderStatus status,
            bool travelling,
            bool expected
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "staging",
                faction.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", enemy.InstanceID);
            target.IsHeadquarters = true;
            Fleet fleet = AddBattleFleet(game, staging, faction.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = status,
                TargetPlanetId = target.InstanceID,
            };
            if (travelling)
                fleet.Movement = new MovementState { TransitTicks = 100 };
            AITestSceneBuilder.RevealPlanet(game, faction, target);
            game.CurrentTick = 51;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                status,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            Assert.AreEqual(expected, proposal.CanExecute(context));
        }

        [Test]
        public void CanSelect_HeadquartersIntelRefreshed_AllowsAttackAgain()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "staging",
                faction.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", enemy.InstanceID);
            target.IsHeadquarters = true;
            Fleet fleet = AddBattleFleet(game, staging, faction.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, faction, target);
            game.CurrentTick = 51;
            AITurnContext staleContext = AITestSceneBuilder.CreateContext(game, faction);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                staleContext.Assessment.GetKnownPlanet(target.InstanceID)
            );
            Assert.IsFalse(proposal.CanSelect(staleContext));
            AITestSceneBuilder.RevealPlanet(game, faction, target);
            AITurnContext refreshedContext = AITestSceneBuilder.CreateContext(game, faction);

            Assert.IsTrue(proposal.CanSelect(refreshedContext));
        }

        [Test]
        public void Execute_WithFleetNotReady_AssignsBuildingOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "owned",
                empire.InstanceID
            );
            Planet enemy = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "enemy",
                rebels.InstanceID
            );
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                enemy
            );

            proposal.Execute(context);

            Assert.IsNotNull(fleet.Order);
            Assert.AreEqual(FleetOrderType.Attack, fleet.Order.OrderType);
            Assert.AreEqual(FleetOrderStatus.Building, fleet.Order.Status);
            Assert.AreEqual(enemy.InstanceID, fleet.Order.TargetPlanetId);
        }

        [Test]
        public void Execute_WithStaleTargetIntelligence_AppliesUncertaintyReserve()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            ConfigureMinimalAttackRequirements(game);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(game, staging, empire.InstanceID);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = fleet.GetCombatValue();
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick =
                game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks + 1;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            proposal.Execute(context);

            Assert.IsNull(fleet.Movement);
            Assert.AreEqual(FleetOrderStatus.Building, fleet.Order.Status);
        }

        [Test]
        public void Execute_WithInboundShipArrivingAtTargetAfterFleet_LaunchesWithLaterArrival()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            ConfigureMinimalAttackRequirements(game);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            staging.PositionX = 0;
            target.PositionX = 10000;
            Fleet fleet = AddBattleFleet(game, staging, empire.InstanceID, regimentCount: 6);
            CapitalShip inbound = AITestSceneBuilder.CreateCapitalShip(
                "inbound",
                empire.InstanceID,
                combatStrength: 100
            );
            inbound.Movement = new MovementState
            {
                TransitTicks = 100,
                CurrentPosition = new Point(-10000, 0),
            };
            game.AttachNode(inbound, fleet);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );

            proposal.Execute(context);

            Assert.IsNotNull(fleet.Movement);
            Assert.IsNotNull(inbound.Movement);
            Assert.Greater(inbound.Movement.TransitTicks, fleet.Movement.TransitTicks);
            Assert.AreEqual(FleetOrderStatus.Readying, fleet.Order.Status);
        }

        [Test]
        public void Execute_WithInboundShipArrivingAtTargetBeforeFleet_PreservesEarlierArrival()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            ConfigureMinimalAttackRequirements(game);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            staging.PositionX = 0;
            target.PositionX = 10000;
            Fleet fleet = AddBattleFleet(game, staging, empire.InstanceID, regimentCount: 6);
            CapitalShip inbound = AITestSceneBuilder.CreateCapitalShip(
                "inbound",
                empire.InstanceID,
                combatStrength: 100
            );
            inbound.Movement = new MovementState
            {
                TransitTicks = 100,
                CurrentPosition = new Point(9000, 0),
            };
            game.AttachNode(inbound, fleet);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );

            proposal.Execute(context);

            Assert.IsNotNull(fleet.Movement);
            Assert.IsNotNull(inbound.Movement);
            Assert.Less(inbound.Movement.TransitTicks, fleet.Movement.TransitTicks);
        }

        [Test]
        public void Execute_WithInboundShipArrivingAtTargetWithFleet_LaunchesAttack()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            ConfigureMinimalAttackRequirements(game);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            staging.PositionX = 0;
            target.PositionX = 10000;
            Fleet fleet = AddBattleFleet(game, staging, empire.InstanceID, regimentCount: 6);
            CapitalShip inbound = AITestSceneBuilder.CreateCapitalShip(
                "inbound",
                empire.InstanceID,
                combatStrength: 100
            );
            inbound.Movement = new MovementState
            {
                TransitTicks = 100,
                CurrentPosition = new Point(0, 0),
            };
            game.AttachNode(inbound, fleet);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );

            proposal.Execute(context);

            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(FleetOrderStatus.Readying, fleet.Order.Status);
        }

        [Test]
        public void Execute_WithSharedTravelOfficerGroup_PreservesIndependentArrivalTimes()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            ConfigureMinimalAttackRequirements(game);
            game.Config.Movement.DistanceDivisor = 5;
            game.Config.Movement.MinTransitTicks = 1;
            game.Config.Movement.SameSectorMinTransitTicks = 1;
            game.Config.Movement.DefaultPersonnelHyperdrive = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            staging.PositionX = 0;
            target.PositionX = 10000;
            Fleet fleet = AddBattleFleet(game, staging, empire.InstanceID, regimentCount: 6);
            CapitalShip carrier = fleet.GetChildren<CapitalShip>().Single();
            carrier.Hyperdrive = 100;
            OfficerTraitDefinition trait = new OfficerTraitDefinition
            {
                ID = "shared-travel",
                PersonnelTravelTimePercent = 50,
                SharesPersonnelTravelTimeModifier = true,
            };
            IReadOnlyDictionary<string, OfficerTraitDefinition> definitions = new Dictionary<
                string,
                OfficerTraitDefinition
            >
            {
                { trait.ID, trait },
            };
            Officer smuggler = EntityFactory.CreateOfficer("smuggler", empire.InstanceID);
            smuggler.Traits.Add(trait.ID);
            smuggler.BindTraitDefinitions(definitions);
            Officer companion = EntityFactory.CreateOfficer("companion", empire.InstanceID);
            companion.BindTraitDefinitions(definitions);
            foreach (Officer officer in new[] { smuggler, companion })
            {
                officer.Movement = new MovementState
                {
                    TransitTicks = 100,
                    MovementGroupID = "personnel-group",
                    CurrentPosition = new Point(-5000, 0),
                };
                game.AttachNode(officer, carrier);
            }
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            Assert.IsTrue(
                context.MovementQueries.TryGetTransitTicks(
                    new List<IMovable> { fleet },
                    target,
                    out int fleetTransitTicks
                )
            );
            Assert.IsTrue(
                context.MovementQueries.TryEstimateRetargetedTransitTicks(
                    companion,
                    target,
                    out int companionTransitTicks
                )
            );
            Assert.IsTrue(
                context.MovementQueries.TryEstimateRetargetedTransitTicks(
                    new List<IMovable> { smuggler, companion },
                    target,
                    out int groupTransitTicks
                )
            );
            Assert.Greater(companionTransitTicks, fleetTransitTicks);
            Assert.LessOrEqual(groupTransitTicks, fleetTransitTicks);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );

            proposal.Execute(context);

            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(FleetOrderStatus.Readying, fleet.Order.Status);
            Assert.AreEqual(fleetTransitTicks, fleet.Movement.TransitTicks);
            Assert.AreEqual(groupTransitTicks, smuggler.Movement.TransitTicks);
            Assert.AreEqual(groupTransitTicks, companion.Movement.TransitTicks);
        }

        [Test]
        public void Execute_WithCompletedAttackOrder_ClearsOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "owned",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "target",
                empire.InstanceID
            );
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );

            proposal.Execute(context);

            Assert.IsNull(fleet.Order);
        }

        [Test]
        public void CanExecute_WithFriendlyTarget_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "owned",
                empire.InstanceID
            );
            Planet friendlyTarget = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "friendly",
                empire.InstanceID
            );
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                friendlyTarget
            );

            bool canExecute = proposal.CanExecute(context);

            Assert.IsFalse(canExecute);
        }

        [Test]
        public void Execute_WithReturningDeliveryOnlyFleet_UsesPlayerValidatedMovementOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet friendly = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "friendly",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            foreach (
                Planet planet in empire
                    .GetOwnedColonizedPlanets()
                    .Where(planet => planet != friendly)
            )
                planet.PositionX = -100000;
            friendly.PositionX = 0;
            target.PositionX = 10000;
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            CapitalShip inbound = fleet.GetChildren<CapitalShip>().Single();
            inbound.ManufacturingStatus = ManufacturingStatus.Delivering;
            inbound.Movement = new MovementState
            {
                TransitTicks = 20,
                TicksElapsed = 5,
                CurrentPosition = new Point(7500, 0),
            };
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

            proposal.Execute(context);

            Assert.AreEqual(friendly.InstanceID, fleet.GetParentOfType<Planet>()?.InstanceID);
            Assert.IsNotNull(fleet.Movement);
            Assert.IsNotNull(inbound.Movement);
            Assert.AreEqual(0, inbound.Movement.TicksElapsed);
            Assert.AreEqual(new Point(7500, 0), inbound.Movement.OriginPosition);
        }

        [Test]
        public void Execute_CaptureAndFollowOnAttack_DoesNotRevealFleetToFormerOwner()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            ConfigureMinimalAttackRequirements(game);
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, 60);
            target.SetPopularSupport(rebels.InstanceID, 40);
            Planet nextTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "next-target",
                rebels.InstanceID,
                positionX: 10000
            );
            nextTarget.SetPopularSupport(empire.InstanceID, 0);
            nextTarget.SetPopularSupport(rebels.InstanceID, 100);
            AITestSceneBuilder.RevealPlanet(game, empire, nextTarget);
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID, regimentCount: 1);
            fleet.GetChildren<CapitalShip>().Single().Bombardment = 1;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID),
                target
            );
            game.Random = new SequenceRNG(intValues: new[] { 1, 0, 10 });
            using GameSession session = new GameSession(game, TestGameData.Create(game.Config));
            IReadOnlyList<GameResult> resolvedResults = null;
            session.Pipeline.ResultsResolved += results => resolvedResults = results;
            AITurnContext context = AITestSceneBuilder.CreateContext(
                game,
                empire,
                bombardment: session.GetService<BombardmentCommands>(),
                random: game.Random
            );
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );

            proposal.Execute(context);

            Assert.AreEqual(1, resolvedResults.OfType<BombardmentResult>().Count());
            Assert.AreEqual(empire.InstanceID, target.GetOwnerInstanceID());
            Assert.AreEqual(FleetOrderStatus.Staging, fleet.Order?.Status);
            Assert.AreEqual(target.InstanceID, fleet.Order?.TargetPlanetId);
            Assert.AreEqual(rebels.InstanceID, nextTarget.GetOwnerInstanceID());

            AITurnContext nextTurn = AITestSceneBuilder.CreateContext(game, empire);
            nextTurn.AddProposals(new AIFleetPlanner().Plan(nextTurn));
            new AIScoringPhase().Execute(nextTurn);
            new AISelectionPhase().Execute(nextTurn);
            new AIExecutionPhase().Execute(nextTurn);
            Planet remembered = new FogOfWarQueries(game)
                .BuildFactionView(rebels)
                .GetChildren<PlanetSector>()
                .SelectMany(sector => sector.GetChildren<Planet>())
                .Single(planet => planet.InstanceID == target.InstanceID);

            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(nextTarget.InstanceID, fleet.Order.TargetPlanetId);
            Assert.IsFalse(
                remembered.GetChildren<Fleet>().Any(item => item.InstanceID == fleet.InstanceID)
            );
            Assert.AreEqual(empire.InstanceID, remembered.GetOwnerInstanceID());
        }

        [Test]
        public void Execute_WithImpenetrableShields_ReturnsOrderToBuilding()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            AddShield(game, target, "shield-1", rebels.InstanceID);
            AddShield(game, target, "shield-2", rebels.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );

            proposal.Execute(context);

            Assert.AreEqual(FleetOrderStatus.Building, fleet.Order.Status);
        }

        [Test]
        public void Execute_WithNoViableBombardmentOrAssault_ReturnsOrderToBuilding()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            AddShield(game, target, "shield", rebels.InstanceID);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID),
                target
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );

            proposal.Execute(context);

            Assert.AreEqual(FleetOrderStatus.Building, fleet.Order.Status);
            Assert.IsEmpty(context.Results.OfType<BombardmentResult>());
            Assert.IsEmpty(context.Results.OfType<PlanetaryAssaultResult>());
        }

        [Test]
        public void Execute_WithSuccessfulPlanetaryAssault_DefersResultsUntilTurnCompletion()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, 100);
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            CapitalShip ship = fleet.GetChildren<CapitalShip>().Single();
            game.AttachNode(AITestSceneBuilder.CreateRegiment("attacker", empire.InstanceID), ship);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );
            List<GameResult> published = new List<GameResult>();
            context.PlanetaryAssault.ResultsProduced += results => published.AddRange(results);

            proposal.Execute(context);

            Assert.IsEmpty(published);
            PlanetaryAssaultResult assault = context
                .Results.OfType<PlanetaryAssaultResult>()
                .Single();
            Assert.IsTrue(assault.Success);
            Assert.AreSame(
                target,
                context.Results.OfType<PlanetGarrisonChangedResult>().Single().Planet
            );
        }

        [TestCase(false, true, true, false)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, false, false)]
        [TestCase(true, true, true, true)]
        public void CanExecute_NeutralTarget_RequiresStationaryOccupationOrder(
            bool ordered,
            bool atTarget,
            bool hasTroops,
            bool expected
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "origin",
                faction.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", null);
            AITestSceneBuilder.RevealPlanet(game, faction, target);
            Fleet fleet = AddBattleFleet(
                game,
                atTarget ? target : origin,
                faction.InstanceID,
                hasTroops ? 1 : 0
            );
            if (ordered)
                fleet.Order = new FleetOrder
                {
                    OrderType = FleetOrderType.Attack,
                    Status = FleetOrderStatus.Ready,
                    TargetPlanetId = target.InstanceID,
                };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIFleetAttackProposal proposal = new(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );

            Assert.AreEqual(expected, proposal.CanExecute(context));
        }

        [Test]
        public void Execute_BombardmentNeutralizesTarget_CompletesOccupationOnNextTurn()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            ConfigureMinimalAttackRequirements(game);
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 1;
            game.Config.AI.Garrison.SupportThreshold = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", enemy.InstanceID);
            Fleet fleet = AddBattleFleet(game, target, faction.InstanceID, regimentCount: 1);
            fleet.GetChildren<CapitalShip>().Single().Bombardment = 1;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", enemy.InstanceID),
                target
            );
            game.Random = new SequenceRNG(intValues: new[] { 1, 0, 10 });
            using GameSession session = new(game, TestGameData.Create(game.Config));
            AITurnContext bombardment = AITestSceneBuilder.CreateContext(
                game,
                faction,
                bombardment: session.GetService<BombardmentCommands>(),
                random: game.Random
            );

            new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            ).Execute(bombardment);

            Assert.IsTrue(string.IsNullOrEmpty(target.GetOwnerInstanceID()));
            Assert.IsNotNull(fleet.Order);
            game.CurrentTick++;
            AITurnContext occupation = AITestSceneBuilder.CreateContext(
                game,
                faction,
                movement: session.GetService<MovementCommands>(),
                planetaryAssault: session.GetService<PlanetaryAssaultCommands>()
            );
            occupation.AddProposals(new AIFleetPlanner().Plan(occupation));
            new AIScoringPhase().Execute(occupation);
            new AISelectionPhase().Execute(occupation);
            new AIExecutionPhase().Execute(occupation);
            session.Pipeline.ProcessResults(occupation.Results, processMessages: false);

            Assert.AreEqual(faction.InstanceID, target.GetOwnerInstanceID());
            Regiment landed = target.GetAllRegiments().Single();
            Assert.AreEqual(faction.InstanceID, landed.GetOwnerInstanceID());
            Assert.IsNull(landed.Movement);
            Assert.AreEqual(ManufacturingStatus.Complete, landed.ManufacturingStatus);
        }

        [Test]
        public void CanExecute_OccupationTroopsRemovedAfterAssessment_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", null);
            Fleet fleet = AddBattleFleet(game, target, faction.InstanceID, regimentCount: 1);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            Assert.AreEqual(1, context.Assessment.GetReadyFleetRegimentCount(fleet));
            game.DeleteNode(fleet.GetRegiments().Single());

            bool canExecute = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            ).CanExecute(context);

            Assert.IsFalse(canExecute);
        }

        [Test]
        public void Execute_WithSuccessfulPlanetaryAssault_RefreshesManufacturingBudget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "target",
                rebels.InstanceID,
                rawResourceNodes: 1
            );
            target.SetPopularSupport(empire.InstanceID, 100);
            AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "mine",
                BuildingType.Mine,
                ManufacturingType.None
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "refinery",
                BuildingType.Refinery,
                ManufacturingType.None
            );
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            CapitalShip ship = fleet.GetChildren<CapitalShip>().Single();
            game.AttachNode(AITestSceneBuilder.CreateRegiment("attacker", empire.InstanceID), ship);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );
            Assert.AreEqual(0, context.AvailableProjectedMaintenanceHeadroom);

            context.SetSelectedProposals(new[] { proposal });
            new AIExecutionPhase().Execute(context);

            Assert.AreEqual(empire.InstanceID, target.GetOwnerInstanceID());
            Assert.AreEqual(50, context.AvailableProjectedMaintenanceHeadroom);
        }

        /// <summary>
        /// Adds battle fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="regimentCount">The number of ready regiments to load.</param>
        /// <returns>The result of add battle fleet.</returns>
        private static Fleet AddBattleFleet(
            GameRoot game,
            Planet planet,
            string ownerInstanceId,
            int regimentCount = 0
        )
        {
            Fleet fleet = EntityFactory.CreateFleet("fleet", ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                ownerInstanceId,
                regimentCapacity: System.Math.Max(1, regimentCount)
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            for (int index = 0; index < regimentCount; index++)
            {
                Regiment regiment = AITestSceneBuilder.CreateRegiment(
                    $"regiment-{index}",
                    ownerInstanceId
                );
                ship.AddChild(regiment);
                regiment.SetParent(ship);
            }
            game.AttachNode(fleet, planet);
            return fleet;
        }

        /// <summary>
        /// Configures attack readiness to depend only on available fleet combat strength.
        /// </summary>
        /// <param name="game">The game to configure.</param>
        private static void ConfigureMinimalAttackRequirements(GameRoot game)
        {
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent = 0;
        }

        /// <summary>
        /// Adds shield.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        private static void AddShield(
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
        }
    }
}
