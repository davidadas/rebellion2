using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
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
    public class AIOrbitalEngagementPlannerTests
    {
        [Test]
        public void Plan_WithNearbyWeakerKnownEnemyFleet_AddsEngagementProposal()
        {
            EngagementScenario scenario = CreateScenario(
                friendlyStrength: 1000,
                hostileStrength: 500
            );

            AIOrbitalEngagementProposal proposal = new AIOrbitalEngagementPlanner()
                .Plan(scenario.Context)
                .OfType<AIOrbitalEngagementProposal>()
                .Single();

            Assert.AreSame(scenario.FriendlyFleet, proposal.Fleet);
            Assert.AreEqual(scenario.Target.InstanceID, proposal.TargetPlanet.InstanceID);
            Assert.AreSame(scenario.Origin, proposal.OriginPlanet);
        }

        [Test]
        public void Plan_WithStrongerKnownEnemyFleet_DoesNotAddEngagementProposal()
        {
            EngagementScenario scenario = CreateScenario(
                friendlyStrength: 500,
                hostileStrength: 1000
            );

            bool hasEngagement = new AIOrbitalEngagementPlanner()
                .Plan(scenario.Context)
                .OfType<AIOrbitalEngagementProposal>()
                .Any();

            Assert.IsFalse(hasEngagement);
        }

        [Test]
        public void Plan_LaserHeavyFleetCannotDefeatCapitalTarget_DoesNotAddEngagementProposal()
        {
            EngagementScenario scenario = CreateScenario(
                friendlyStrength: 1000,
                hostileStrength: 500
            );
            scenario
                .Context
                .Game
                .Config
                .AI
                .FleetDeployment
                .AttackStrengthPercentOfStrongestHostileFleet = 175;
            scenario.Context.Game.Config.Combat.SpaceCombat.LaserCannonCapitalDamageMultiplier =
                1d / 6d;
            CapitalShip attackingShip = scenario.FriendlyFleet.GetChildren<CapitalShip>()[0];
            ConfigureCombatShip(attackingShip, hullStrength: 1200);
            attackingShip.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[]
            {
                60,
                60,
                90,
                90,
                0,
            };
            CapitalShip defendingShip = scenario
                .Target.GetChildren<Fleet>()[0]
                .GetChildren<CapitalShip>()[0];
            ConfigureCombatShip(defendingShip, hullStrength: 1400);
            defendingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 15, 0, 30, 30, 0 };
            scenario = scenario.WithContext(
                AITestSceneBuilder.CreateContext(scenario.Context.Game, scenario.Context.Faction)
            );

            bool hasEngagement = new AIOrbitalEngagementPlanner()
                .Plan(scenario.Context)
                .OfType<AIOrbitalEngagementProposal>()
                .Any();

            Assert.IsFalse(hasEngagement);
        }

        [Test]
        public void Plan_WithUnobservedEnemyFleet_DoesNotAddEngagementProposal()
        {
            EngagementScenario scenario = CreateScenario(
                friendlyStrength: 1000,
                hostileStrength: 500,
                revealTarget: false
            );

            bool hasEngagement = new AIOrbitalEngagementPlanner()
                .Plan(scenario.Context)
                .OfType<AIOrbitalEngagementProposal>()
                .Any();

            Assert.IsFalse(hasEngagement);
        }

        [Test]
        public void Plan_WithAnotherOffensiveOrder_AddsNewEngagementProposal()
        {
            EngagementScenario scenario = CreateScenario(
                friendlyStrength: 1000,
                hostileStrength: 500
            );
            Fleet orderedFleet = AddBattleFleet(
                scenario.Context.Game,
                scenario.Origin,
                "ordered",
                scenario.Context.Faction.InstanceID,
                1000
            );
            orderedFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = scenario.Target.InstanceID,
            };
            scenario = scenario.WithContext(
                AITestSceneBuilder.CreateContext(scenario.Context.Game, scenario.Context.Faction)
            );

            bool hasNewEngagement = new AIOrbitalEngagementPlanner()
                .Plan(scenario.Context)
                .OfType<AIOrbitalEngagementProposal>()
                .Any(proposal => proposal.Fleet.Order == null);

            Assert.IsTrue(hasNewEngagement);
        }

        [Test]
        public void Plan_WithMissingOrderedTarget_AddsClearOrderProposal()
        {
            EngagementScenario scenario = CreateScenario(
                friendlyStrength: 1000,
                hostileStrength: 500
            );
            scenario.FriendlyFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Engage,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = "missing",
                OriginPlanetId = scenario.Origin.InstanceID,
            };
            scenario = scenario.WithContext(
                AITestSceneBuilder.CreateContext(scenario.Context.Game, scenario.Context.Faction)
            );

            AIClearFleetOrderProposal proposal = new AIOrbitalEngagementPlanner()
                .Plan(scenario.Context)
                .OfType<AIClearFleetOrderProposal>()
                .Single();

            Assert.AreSame(scenario.FriendlyFleet, proposal.Fleet);
        }

        /// <summary>
        /// Creates a planning scenario with friendly and hostile battle fleets.
        /// </summary>
        /// <param name="friendlyStrength">Friendly fleet combat strength.</param>
        /// <param name="hostileStrength">Hostile fleet combat strength.</param>
        /// <param name="revealTarget">Whether the acting faction knows the target.</param>
        /// <returns>The configured engagement scenario.</returns>
        private static EngagementScenario CreateScenario(
            int friendlyStrength,
            int hostileStrength,
            bool revealTarget = true
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet friendlyFleet = AddBattleFleet(
                game,
                origin,
                "friendly",
                empire.InstanceID,
                friendlyStrength
            );
            AddBattleFleet(game, target, "hostile", rebels.InstanceID, hostileStrength);
            if (revealTarget)
                AITestSceneBuilder.RevealPlanet(game, empire, target);

            return new EngagementScenario
            {
                Context = AITestSceneBuilder.CreateContext(game, empire),
                Origin = origin,
                Target = target,
                FriendlyFleet = friendlyFleet,
            };
        }

        /// <summary>
        /// Adds a battle fleet with one capital ship to a planet.
        /// </summary>
        /// <param name="game">Game receiving the fleet.</param>
        /// <param name="planet">Planet receiving the fleet.</param>
        /// <param name="instanceId">Fleet instance identifier.</param>
        /// <param name="ownerInstanceId">Owning faction instance identifier.</param>
        /// <param name="combatStrength">Capital ship combat strength.</param>
        /// <returns>The created fleet.</returns>
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
        /// Resets a synthetic ship to the supplied durability with no primary weapons.
        /// </summary>
        /// <param name="ship">The ship to configure.</param>
        /// <param name="hullStrength">The ship's current and maximum hull strength.</param>
        private static void ConfigureCombatShip(CapitalShip ship, int hullStrength)
        {
            ship.MaxHullStrength = hullStrength;
            ship.CurrentHullStrength = hullStrength;
            ship.MaxShieldStrength = 0;
            foreach (PrimaryWeaponType weaponType in ship.PrimaryWeapons.Keys.ToList())
                ship.PrimaryWeapons[weaponType] = new int[5];
        }

        private sealed class EngagementScenario
        {
            public AITurnContext Context { get; set; }

            public Planet Origin { get; set; }

            public Planet Target { get; set; }

            public Fleet FriendlyFleet { get; set; }

            /// <summary>
            /// Replaces the turn context and returns this scenario.
            /// </summary>
            /// <param name="context">Replacement turn context.</param>
            /// <returns>This scenario.</returns>
            public EngagementScenario WithContext(AITurnContext context)
            {
                Context = context;
                return this;
            }
        }
    }
}
