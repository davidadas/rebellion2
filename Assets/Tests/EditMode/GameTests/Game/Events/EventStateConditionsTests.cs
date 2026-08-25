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
        public void EvaluateBinding_NullBinding_ReturnsFalse()
        {
            GameRoot game = BuildGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "$sourceEventInstanceID",
                Comparison = ComparisonOperator.Equal,
                CompareTo = "expected-event",
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("sourceEventInstanceID", null);

            bool result = conditional.IsMet(game, context);

            Assert.IsFalse(result);
        }

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
            GameEventEvaluationContext context = new GameEventEvaluationContext(
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
            GameEventEvaluationContext context = new GameEventEvaluationContext(
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
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("sourceEventInstanceID", null);

            Assert.AreEqual(expected, conditional.IsMet(game, context));
        }

        [Test]
        public void HasEventActivated_ActivationRecorded_ReturnsTrue()
        {
            GameRoot game = BuildGame(out _, out _);
            game.EventRuntime.GetState("activated").ActivationCount = 1;
            HasEventActivatedConditional conditional = new HasEventActivatedConditional
            {
                EventInstanceID = "activated",
            };

            Assert.IsTrue(conditional.IsMet(game));
        }

        [Test]
        public void IsEventComplete_PersistedCompletionState_ReturnsTrue()
        {
            GameRoot game = BuildGame(out _, out _);
            game.EventRuntime.GetState("limited").IsComplete = true;
            IsEventCompleteConditional conditional = new IsEventCompleteConditional
            {
                EventInstanceID = "limited",
            };

            bool isComplete = conditional.IsMet(game);

            Assert.IsTrue(isComplete);
        }

        [Test]
        public void IsEventComplete_LoadedUnlimitedEvent_ReturnsFalse()
        {
            GameRoot game = BuildGame(out _, out _);
            game.GetEventPool().Add(new GameEvent { InstanceID = "unlimited" });
            game.EventRuntime.GetState("unlimited").ActivationCount = 10;
            IsEventCompleteConditional conditional = new IsEventCompleteConditional
            {
                EventInstanceID = "unlimited",
            };

            bool isComplete = conditional.IsMet(game);

            Assert.IsFalse(isComplete);
        }

        private static GameRoot BuildGame(out Planet empirePlanet, out Planet rebelPlanet)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
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
            game.AttachNode(empirePlanet, sector);
            game.AttachNode(rebelPlanet, sector);
            return game;
        }
    }
}
