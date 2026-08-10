using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class TriggerResultConditionsTests
    {
        [Test]
        public void UnitArrived_OfficerInsideFleetAtDestination_MatchesArrival()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            Officer emperor = EntityFactory.CreateOfficer("emperor", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "empire" };
            game.AttachNode(fleet, empirePlanet);
            game.AttachNode(ship, fleet);
            game.AttachNode(emperor, ship);
            UnitArrivedConditional conditional = new UnitArrivedConditional
            {
                UnitInstanceID = emperor.InstanceID,
                DestinationInstanceID = empirePlanet.InstanceID,
            };

            bool matches = conditional.IsMet(
                game,
                new UnitArrivedResult { Unit = fleet, Destination = empirePlanet }
            );

            Assert.IsTrue(matches);
        }

        [Test]
        public void UnitArrived_WrongDestination_DoesNotMatchArrival()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer emperor = EntityFactory.CreateOfficer("emperor", "empire");
            game.AttachNode(emperor, empirePlanet);
            UnitArrivedConditional conditional = new UnitArrivedConditional
            {
                UnitInstanceID = emperor.InstanceID,
                DestinationInstanceID = empirePlanet.InstanceID,
            };

            bool matches = conditional.IsMet(
                game,
                new UnitArrivedResult { Unit = emperor, Destination = rebelPlanet }
            );

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
