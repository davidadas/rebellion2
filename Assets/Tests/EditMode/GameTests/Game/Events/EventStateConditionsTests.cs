using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class EventStateConditionsTests
    {
        [Test]
        public void EvaluateBinding_ObjectBinding_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            Officer emperor = EntityFactory.CreateOfficer("emperor", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "empire" };
            game.AttachNode(fleet, empirePlanet);
            game.AttachNode(ship, fleet);
            game.AttachNode(emperor, ship);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "$unit",
                Comparison = ComparisonOperator.Equal,
                CompareTo = emperor.InstanceID,
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("unit", fleet);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                conditional.IsMet(game, context)
            );

            StringAssert.Contains("cannot be compared", exception.Message);
        }

        [Test]
        public void EvaluateBinding_DifferentObjectBinding_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "$destination",
                Comparison = ComparisonOperator.Equal,
                CompareTo = empirePlanet.InstanceID,
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("destination", rebelPlanet);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                conditional.IsMet(game, context)
            );

            StringAssert.Contains("cannot be compared", exception.Message);
        }

        [TestCase(ComparisonOperator.Equal, false)]
        [TestCase(ComparisonOperator.NotEqual, true)]
        [TestCase(ComparisonOperator.GreaterThan, false)]
        [TestCase(ComparisonOperator.GreaterThanOrEqual, false)]
        [TestCase(ComparisonOperator.LessThan, false)]
        [TestCase(ComparisonOperator.LessThanOrEqual, false)]
        public void EvaluateBinding_NullOptionalBinding_UsesPredicateSemantics(
            ComparisonOperator comparison,
            bool expected
        )
        {
            GameRoot game = BuildGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "$sourceEventInstanceID",
                Comparison = comparison,
                CompareTo = "EXPECTED_SOURCE",
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("sourceEventInstanceID", null);

            Assert.AreEqual(expected, conditional.IsMet(game, context));
        }

        [Test]
        public void IsEventExhausted_LoadedCountAtLimit_ReturnsTrueBeforeEventProcessing()
        {
            GameRoot game = BuildGame(out _, out _);
            game.GetEventPool().Add(new GameEvent { InstanceID = "limited", TriggerCount = 2 });
            game.EventRuntime.GetState("limited").ExecutionCount = 2;
            IsEventExhaustedConditional conditional = new IsEventExhaustedConditional
            {
                EventInstanceID = "limited",
            };

            bool exhausted = conditional.IsMet(game);

            Assert.IsTrue(exhausted);
        }

        [Test]
        public void IsEventExhausted_LoadedUnlimitedEvent_ReturnsFalse()
        {
            GameRoot game = BuildGame(out _, out _);
            game.GetEventPool().Add(new GameEvent { InstanceID = "unlimited" });
            game.EventRuntime.GetState("unlimited").ExecutionCount = 10;
            IsEventExhaustedConditional conditional = new IsEventExhaustedConditional
            {
                EventInstanceID = "unlimited",
            };

            bool exhausted = conditional.IsMet(game);

            Assert.IsFalse(exhausted);
        }

        private static GameRoot BuildGame(out Planet empirePlanet, out Planet rebelPlanet)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            game.AttachNode(system, game.Galaxy);
            empirePlanet = new Planet
            {
                InstanceID = "empire-planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            rebelPlanet = new Planet
            {
                InstanceID = "rebel-planet",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(empirePlanet, system);
            game.AttachNode(rebelPlanet, system);
            return game;
        }
    }
}
