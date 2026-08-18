using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventSelectorTests
    {
        [Test]
        public void SelectPlanets_MatchingInstanceID_ReturnsPlanet()
        {
            GameRoot game = BuildGame(out Planet planet);
            SelectPlanets selector = new SelectPlanets { InstanceID = planet.InstanceID };

            Planet selected = selector.Select(game, new StubRNG(), null).Cast<Planet>().Single();

            Assert.AreSame(planet, selected);
        }

        [Test]
        public void SelectPlanets_DestroyedPlanet_ReturnsNothing()
        {
            GameRoot game = BuildGame(out Planet planet);
            planet.IsDestroyed = true;
            SelectPlanets selector = new SelectPlanets { InstanceID = planet.InstanceID };

            bool any = selector.Select(game, new StubRNG(), null).Any();

            Assert.IsFalse(any);
        }

        [Test]
        public void SelectRandom_FilteredPlanetSet_ReturnsRequestedCount()
        {
            GameRoot game = BuildGame(out _);
            PlanetSystem rimSystem = new PlanetSystem
            {
                InstanceID = "rim-system",
                SystemType = PlanetSystemType.OuterRim,
            };
            Planet rimPlanet = new Planet { InstanceID = "rim-planet" };
            game.AttachNode(rimSystem, game.Galaxy);
            game.AttachNode(rimPlanet, rimSystem);
            SelectRandom selector = new SelectRandom
            {
                Count = 1,
                Selectors = { new SelectPlanets { SystemType = PlanetSystemType.OuterRim } },
            };

            Planet selected = selector.Select(game, new StubRNG(), null).Cast<Planet>().Single();

            Assert.AreSame(rimPlanet, selected);
        }

        [Test]
        public void SelectPlanets_NoFilters_ReturnsEverySurvivingPlanet()
        {
            GameRoot game = BuildGame(out Planet firstPlanet);
            Planet secondPlanet = new Planet { InstanceID = "second-planet" };
            game.AttachNode(secondPlanet, firstPlanet.GetParent());
            SelectPlanets selector = new SelectPlanets();

            Planet[] selected = selector.Select(game, new StubRNG(), null).Cast<Planet>().ToArray();

            CollectionAssert.AreEqual(new[] { firstPlanet, secondPlanet }, selected);
        }

        [Test]
        public void SelectNearestParent_OfficerInFleet_ReturnsContainingPlanet()
        {
            GameRoot game = BuildGame(out Planet planet);
            game.GetFactions().Add(new Faction { InstanceID = "faction" });
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "faction" };
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "faction" };
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(officer, ship);
            SelectNearestParent selector = new SelectNearestParent
            {
                Type = SceneAncestorType.Planet,
                Selectors = { new SelectOfficers { InstanceID = officer.InstanceID } },
            };

            Planet selected = selector.Select(game, new StubRNG(), null).Cast<Planet>().Single();

            Assert.AreSame(planet, selected);
        }

        [Test]
        public void SelectManufacturingOrders_MatchingPlanet_ReturnsQueuedProduct()
        {
            GameRoot game = BuildGame(out Planet planet);
            game.GetFactions().Add(new Faction { InstanceID = "faction" });
            planet.OwnerInstanceID = "faction";
            planet.EnergyCapacity = 1;
            Building building = new Building
            {
                InstanceID = "queued-building",
                OwnerInstanceID = "faction",
                ProducerOwnerID = "faction",
                ProducerPlanetID = planet.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(building, planet);
            planet.AddToManufacturingQueue(building);
            SelectManufacturingOrders selector = new SelectManufacturingOrders
            {
                PlanetInstanceID = planet.InstanceID,
                ManufacturingType = ManufacturingType.Building,
            };

            IManufacturable selected = selector
                .Select(game, new StubRNG(), null)
                .Cast<IManufacturable>()
                .Single();

            Assert.AreSame(building, selected);
        }

        private static GameRoot BuildGame(out Planet planet)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "core-system",
                SystemType = PlanetSystemType.CoreSystem,
            };
            planet = new Planet { InstanceID = "core-planet" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);
            return game;
        }
    }
}
