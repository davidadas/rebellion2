using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIOrbitalEngagementProposalTests
    {
        [Test]
        public void Execute_NewEngagement_MovesFleetAndRecordsOrigin()
        {
            EngagementScenario scenario = CreateScenario(includeHostileFleet: true);
            AIOrbitalEngagementProposal proposal = new AIOrbitalEngagementProposal(
                scenario.Fleet,
                scenario.Target,
                scenario.Origin
            );

            proposal.Execute(scenario.Context);

            Assert.IsNotNull(scenario.Fleet.Movement);
            Assert.AreEqual(FleetOrderType.Engage, scenario.Fleet.Order.OrderType);
            Assert.AreEqual(scenario.Target.InstanceID, scenario.Fleet.Order.TargetPlanetId);
            Assert.AreEqual(scenario.Origin.InstanceID, scenario.Fleet.Order.OriginPlanetId);
        }

        [Test]
        public void Execute_EngagementCompletedWithoutInvasionForce_ReturnsToOrigin()
        {
            EngagementScenario scenario = CreateScenario(includeHostileFleet: false);
            scenario.Context.Game.MoveNode(scenario.Fleet, scenario.Target);
            scenario.Fleet.Order = CreateOrder(scenario);
            RefreshContext(scenario);
            AIOrbitalEngagementProposal proposal = new AIOrbitalEngagementProposal(
                scenario.Fleet,
                scenario.Target,
                scenario.Origin
            );

            proposal.Execute(scenario.Context);

            Assert.IsNotNull(scenario.Fleet.Movement);
            Assert.AreEqual(FleetOrderStatus.Returning, scenario.Fleet.Order.Status);
        }

        [Test]
        public void Execute_EngagementCompletedWithInvasionForce_ConvertsToAttackOrder()
        {
            EngagementScenario scenario = CreateScenario(includeHostileFleet: false);
            scenario.Context.Game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            scenario.Context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount =
                1;
            scenario.Context.Game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            scenario.Context.Game.MoveNode(scenario.Fleet, scenario.Target);
            scenario.Fleet.Order = CreateOrder(scenario);
            CapitalShip ship = scenario.Fleet.GetChildren<CapitalShip>()[0];
            ship.Bombardment = 10;
            ship.RegimentCapacity = 100;
            for (int index = 0; index < 20; index++)
            {
                scenario.Context.Game.AttachNode(
                    AITestSceneBuilder.CreateRegiment(
                        $"regiment-{index}",
                        scenario.Context.Faction.InstanceID
                    ),
                    ship
                );
            }
            scenario = scenario.WithContext(
                AITestSceneBuilder.CreateContext(scenario.Context.Game, scenario.Context.Faction)
            );
            AIOrbitalEngagementProposal proposal = new AIOrbitalEngagementProposal(
                scenario.Fleet,
                scenario.Context.Assessment.GetKnownPlanet(scenario.Target.InstanceID),
                scenario.Origin
            );

            proposal.Execute(scenario.Context);

            Assert.AreEqual(FleetOrderType.Attack, scenario.Fleet.Order.OrderType);
            Assert.AreEqual(FleetOrderStatus.Ready, scenario.Fleet.Order.Status);
        }

        [Test]
        public void Execute_ReturnedFleet_ClearsEngagementOrder()
        {
            EngagementScenario scenario = CreateScenario(includeHostileFleet: false);
            scenario.Fleet.Order = CreateOrder(scenario);
            scenario.Fleet.Order.Status = FleetOrderStatus.Returning;
            AIOrbitalEngagementProposal proposal = new AIOrbitalEngagementProposal(
                scenario.Fleet,
                scenario.Target,
                scenario.Origin
            );

            proposal.Execute(scenario.Context);

            Assert.IsNull(scenario.Fleet.Order);
        }

        [Test]
        public void Execute_TargetNoLongerHasKnownHostileFleet_ClearsOrderBeforeDeparture()
        {
            EngagementScenario scenario = CreateScenario(includeHostileFleet: false);
            scenario.Fleet.Order = CreateOrder(scenario);
            AIOrbitalEngagementProposal proposal = new AIOrbitalEngagementProposal(
                scenario.Fleet,
                scenario.Target,
                scenario.Origin
            );

            proposal.Execute(scenario.Context);

            Assert.IsNull(scenario.Fleet.Order);
            Assert.IsNull(scenario.Fleet.Movement);
        }

        [Test]
        public void Execute_ReturningFleetStillInEnemyTerritory_ContinuesReturning()
        {
            EngagementScenario scenario = CreateScenario(includeHostileFleet: false);
            scenario.Context.Game.MoveNode(scenario.Fleet, scenario.Target);
            scenario.Fleet.Order = CreateOrder(scenario);
            scenario.Fleet.Order.Status = FleetOrderStatus.Returning;
            AIOrbitalEngagementProposal proposal = new AIOrbitalEngagementProposal(
                scenario.Fleet,
                scenario.Target,
                scenario.Origin
            );

            proposal.Execute(scenario.Context);

            Assert.IsNotNull(scenario.Fleet.Order);
            Assert.IsNotNull(scenario.Fleet.Movement);
        }

        /// <summary>
        /// Creates an engagement scenario with optional hostile orbital forces.
        /// </summary>
        /// <param name="includeHostileFleet">Whether to add a hostile fleet at the target.</param>
        /// <returns>The configured engagement scenario.</returns>
        private static EngagementScenario CreateScenario(bool includeHostileFleet)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, origin);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID, 1000),
                fleet
            );
            if (includeHostileFleet)
            {
                Fleet hostileFleet = EntityFactory.CreateFleet("hostile", rebels.InstanceID);
                game.AttachNode(hostileFleet, target);
                game.AttachNode(
                    AITestSceneBuilder.CreateCapitalShip("hostile-ship", rebels.InstanceID, 100),
                    hostileFleet
                );
            }
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            return new EngagementScenario
            {
                Context = AITestSceneBuilder.CreateContext(game, empire),
                Origin = origin,
                Target = target,
                Fleet = fleet,
            };
        }

        /// <summary>
        /// Creates an active engagement order for the scenario fleet.
        /// </summary>
        /// <param name="scenario">Scenario supplying the fleet route.</param>
        /// <returns>The engagement order.</returns>
        private static FleetOrder CreateOrder(EngagementScenario scenario)
        {
            return new FleetOrder
            {
                OrderType = FleetOrderType.Engage,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = scenario.Target.InstanceID,
                OriginPlanetId = scenario.Origin.InstanceID,
            };
        }

        /// <summary>
        /// Rebuilds the turn context after the live game state changes.
        /// </summary>
        /// <param name="scenario">Scenario whose context should be rebuilt.</param>
        private static void RefreshContext(EngagementScenario scenario)
        {
            scenario.Context = AITestSceneBuilder.CreateContext(
                scenario.Context.Game,
                scenario.Context.Faction
            );
        }

        private sealed class EngagementScenario
        {
            public AITurnContext Context { get; set; }

            public Planet Origin { get; set; }

            public Planet Target { get; set; }

            public Fleet Fleet { get; set; }

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
