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
        public void EvaluateBinding_OfficerInsideBoundFleet_DoesNotMatchInstanceID()
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
                Name = "unit",
                Comparison = EventVariableComparison.Equal,
                ExpectedValue = emperor.InstanceID,
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("unit", fleet);

            bool matches = conditional.IsMet(game, context);

            Assert.IsFalse(matches);
        }

        [Test]
        public void EvaluateBinding_DifferentEntity_DoesNotMatchInstanceID()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Name = "destination",
                Comparison = EventVariableComparison.Equal,
                ExpectedValue = empirePlanet.InstanceID,
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("destination", rebelPlanet);

            bool matches = conditional.IsMet(game, context);

            Assert.IsFalse(matches);
        }

        private static GameRoot BuildGame(out Planet empirePlanet, out Planet rebelPlanet)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
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
