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
    public class GameConditionalsTests
    {
        [Test]
        public void OfficerPairArrival_OfficerInsideArrivingFleet_MatchesPair()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "empire" };
            game.AttachNode(luke, rebelPlanet);
            game.AttachNode(fleet, empirePlanet);
            game.AttachNode(ship, fleet);
            game.AttachNode(vader, ship);
            OfficerPairArrivalConditional conditional = new OfficerPairArrivalConditional
            {
                FirstOfficerInstanceID = "luke",
                SecondOfficerInstanceID = "vader",
            };

            bool matches = conditional.IsMet(
                game,
                new UnitArrivedResult { Unit = fleet, Destination = empirePlanet }
            );

            Assert.IsTrue(matches);
        }

        [Test]
        public void UnitArrival_OfficerInsideFleetAtDestination_MatchesArrival()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            Officer emperor = EntityFactory.CreateOfficer("emperor", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "empire" };
            game.AttachNode(fleet, empirePlanet);
            game.AttachNode(ship, fleet);
            game.AttachNode(emperor, ship);
            UnitArrivalConditional conditional = new UnitArrivalConditional
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
        public void UnitArrival_WrongDestination_DoesNotMatchArrival()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer emperor = EntityFactory.CreateOfficer("emperor", "empire");
            game.AttachNode(emperor, empirePlanet);
            UnitArrivalConditional conditional = new UnitArrivalConditional
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
