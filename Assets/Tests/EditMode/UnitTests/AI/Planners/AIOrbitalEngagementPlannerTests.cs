using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
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
