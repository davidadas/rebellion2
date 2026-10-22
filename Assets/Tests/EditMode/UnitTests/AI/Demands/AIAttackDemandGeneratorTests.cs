using NUnit.Framework;
using Rebellion.AI;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Demands
{
    [TestFixture]
    public sealed class AIAttackDemandGeneratorTests
    {
        [TestCase(19, 0, 1100)]
        [TestCase(20, 0, 1100)]
        [TestCase(21, 0, 100)]
        [TestCase(30, 10, 1100)]
        [TestCase(31, 10, 100)]
        public void Generate_KnownInboundShip_CountsOnlyArrivalsByAttacker(
            int inboundTicks,
            int intelAge,
            int expectedStrength
        )
        {
            AITurnContext context = CreateArrivalContext(
                inboundTicks,
                intelAge,
                true,
                true,
                out Fleet attacker,
                out Planet target,
                out CapitalShip inbound
            );

            Assert.AreEqual(
                expectedStrength,
                context.GetAttackDemand(target, attacker).CombatStrength
            );
        }

        [Test]
        public void Generate_UnobservedReinforcement_DoesNotReadLiveEnemyState()
        {
            AITurnContext context = CreateArrivalContext(
                10,
                0,
                false,
                true,
                out Fleet attacker,
                out Planet target,
                out _
            );

            Assert.AreEqual(100, context.GetAttackDemand(target, attacker).CombatStrength);
        }

        [TestCase(20, 1000)]
        [TestCase(21, 100)]
        public void Generate_InboundEnemyFleet_UsesFleetArrivalTime(
            int fleetTicks,
            int expectedStrength
        )
        {
            AITurnContext context = CreateArrivalContext(
                10,
                0,
                true,
                true,
                out Fleet attacker,
                out Planet target,
                out CapitalShip inbound
            );
            Fleet arrivingFleet = EntityFactory.CreateFleet(
                "arriving-fleet",
                inbound.OwnerInstanceID
            );
            arrivingFleet.Movement = new MovementState { TransitTicks = fleetTicks };
            context.Game.AttachNode(arrivingFleet, target);
            context.Game.MoveNode(inbound, arrivingFleet);
            inbound.Movement = null;
            AITestSceneBuilder.RevealPlanet(context.Game, context.Faction, target);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            Assert.AreEqual(
                expectedStrength,
                context.GetAttackDemand(target, attacker).CombatStrength
            );
        }

        [Test]
        public void Generate_CarriedFightersArriveWithCarrier_IncludesTheirCapabilities()
        {
            AITurnContext context = CreateArrivalContext(
                10,
                0,
                true,
                true,
                out Fleet attacker,
                out Planet target,
                out CapitalShip inbound
            );
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "carried",
                inbound.OwnerInstanceID
            );
            context.Game.AttachNode(fighter, inbound);
            AITestSceneBuilder.RevealPlanet(context.Game, context.Faction, target);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            Assert.AreEqual(1, context.GetAttackDemand(target, attacker).StarfighterCount);
            Assert.Greater(
                context.GetAttackDemand(target, attacker).StarfighterFirepower,
                context.GetAttackDemand(target).StarfighterFirepower
            );
        }

        [Test]
        public void Generate_DifferentAttackerArrivalTimes_KeepsRequirementsSeparate()
        {
            AITurnContext context = CreateArrivalContext(
                15,
                0,
                true,
                true,
                out Fleet slow,
                out Planet target,
                out _
            );
            Fleet fast = EntityFactory.CreateFleet("fast", context.Faction.InstanceID);
            fast.RoleType = FleetRoleType.Battle;
            fast.Movement = new MovementState { TransitTicks = 5 };
            context.Game.AttachNode(fast, target);
            context.Game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("fast-ship", context.Faction.InstanceID, 1000),
                fast
            );
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            Assert.AreEqual(100, context.GetAttackDemand(target, fast).CombatStrength);
            Assert.AreEqual(1100, context.GetAttackDemand(target, slow).CombatStrength);
        }

        [Test]
        public void Generate_UnfinishedReinforcement_DoesNotAssumeManufacturingCompletion()
        {
            AITurnContext context = CreateArrivalContext(
                10,
                0,
                true,
                false,
                out Fleet attacker,
                out Planet target,
                out _
            );

            Assert.AreEqual(100, context.GetAttackDemand(target, attacker).CombatStrength);
        }

        [Test]
        public void Generate_ProjectedArrival_DoesNotChangeObservedOrLiveMovement()
        {
            AITurnContext context = CreateArrivalContext(
                20,
                0,
                true,
                true,
                out Fleet attacker,
                out Planet target,
                out CapitalShip inbound
            );
            Planet known = context.Assessment.GetKnownPlanet(target.InstanceID);
            CapitalShip observed = known.GetChildren<Fleet>()[0].GetChildren<CapitalShip>()[1];

            Assert.AreEqual(20, inbound.Movement.TicksRemaining());
            Assert.AreEqual(20, observed.Movement.TicksRemaining());
        }

        [Test]
        public void BuildDemands_WithoutOrbitalDefenders_UsesMinimumCombatStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1400;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(1400, context.GetAttackDemand(target).CombatStrength);
        }

        [Test]
        public void BuildDemands_WithStaleIntelligence_IncreasesCombatStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1000;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick =
                game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks * 3;

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.Greater(context.GetAttackDemand(target).CombatStrength, 1000);
        }

        [Test]
        public void BuildDemands_WithExtremelyStaleIntelligence_CapsCombatStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1000;
            game.Config.AI.FleetDeployment.StaleIntelMaximumAttackStrengthPercent = 250;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick = 10000;

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(2500, context.GetAttackDemand(target).CombatStrength);
        }

        [Test]
        public void BuildDemands_WithHostileFleetElsewhereInSystem_UsesTargetDefense()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 150;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Planet defended = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defended",
                rebels.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", rebels.InstanceID);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                rebels.InstanceID,
                combatStrength: 1000
            );
            game.AttachNode(fleet, defended);
            game.AttachNode(ship, fleet);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITestSceneBuilder.RevealPlanet(game, empire, defended);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(100, context.GetAttackDemand(target).CombatStrength);
        }

        [Test]
        public void BuildDemands_WithHostileFleetAtTarget_UsesLocalDefense()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 150;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", rebels.InstanceID);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                rebels.InstanceID,
                combatStrength: 1000
            );
            game.AttachNode(fleet, target);
            game.AttachNode(ship, fleet);
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(1500, context.GetAttackDemand(target).CombatStrength);
        }

        [Test]
        public void BuildDemands_WithPlanetaryStarfighter_UsesAssaultCompositionTargets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                rebels.InstanceID
            );
            game.AttachNode(fighter, target);
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(0, context.GetAttackDemand(target).CapitalShipFirepower);
            Assert.AreEqual(180, context.GetAttackDemand(target).StarfighterFirepower);
            Assert.AreEqual(1, context.GetAttackDemand(target).StarfighterCount);
        }

        /// <summary>
        /// Creates a known defender with one ready ship and one incoming reinforcement.
        /// </summary>
        /// <param name="inboundTicks">The observed reinforcement travel time.</param>
        /// <param name="intelAge">Ticks elapsed since observation.</param>
        /// <param name="observeInbound">Whether the reinforcement is included in intelligence.</param>
        /// <param name="completed">Whether the reinforcement has completed manufacturing.</param>
        /// <param name="attacker">The ready attacking fleet, twenty travel ticks away.</param>
        /// <param name="target">The hostile target.</param>
        /// <param name="inbound">The incoming capital ship.</param>
        /// <returns>The assessed turn context.</returns>
        private static AITurnContext CreateArrivalContext(
            int inboundTicks,
            int intelAge,
            bool observeInbound,
            bool completed,
            out Fleet attacker,
            out Planet target,
            out CapitalShip inbound
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 100;
            game.Config.AI.FleetDeployment.AssaultFleetCompositionReservePercent = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent = 0;
            game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks = 1000;
            game.Config.Movement.MinTransitTicks = 20;
            game.Config.Movement.SameSectorMinTransitTicks = 20;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "staging",
                faction.InstanceID
            );
            target = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "target",
                enemy.InstanceID,
                positionX: 1
            );
            attacker = EntityFactory.CreateFleet("attacker", faction.InstanceID);
            attacker.RoleType = FleetRoleType.Battle;
            CapitalShip attackingShip = AITestSceneBuilder.CreateCapitalShip(
                "attacking-ship",
                faction.InstanceID,
                1000,
                regimentCapacity: 6
            );
            game.AttachNode(attacker, staging);
            game.AttachNode(attackingShip, attacker);
            for (int i = 0; i < 6; i++)
                game.AttachNode(
                    AITestSceneBuilder.CreateRegiment("troop-" + i, faction.InstanceID),
                    attackingShip
                );
            Fleet defender = EntityFactory.CreateFleet("defender", enemy.InstanceID);
            game.AttachNode(defender, target);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("ready", enemy.InstanceID, 100),
                defender
            );
            if (!observeInbound)
                AITestSceneBuilder.RevealPlanet(game, faction, target);
            inbound = AITestSceneBuilder.CreateCapitalShip("inbound", enemy.InstanceID, 1000);
            inbound.ManufacturingStatus = completed
                ? ManufacturingStatus.Delivering
                : ManufacturingStatus.Building;
            inbound.Movement = new MovementState { TransitTicks = inboundTicks };
            game.AttachNode(inbound, defender);
            if (observeInbound)
                AITestSceneBuilder.RevealPlanet(game, faction, target);
            game.CurrentTick = intelAge;
            return AITestSceneBuilder.CreateContext(game, faction);
        }
    }
}
