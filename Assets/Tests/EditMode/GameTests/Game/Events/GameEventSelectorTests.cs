using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

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
        public void SelectRandom_FilteredPlanetSet_ReturnsRequestedCount()
        {
            GameRoot game = BuildGame(out _);
            PlanetSector rimSector = new PlanetSector
            {
                InstanceID = "rim-system",
                SectorType = PlanetSectorType.OuterRim,
            };
            Planet rimPlanet = new Planet { InstanceID = "rim-planet" };
            game.AttachNode(rimSector, game.Galaxy);
            game.AttachNode(rimPlanet, rimSector);
            SelectRandom selector = new SelectRandom
            {
                Count = 1,
                Selectors = { new SelectPlanets { SectorType = PlanetSectorType.OuterRim } },
            };

            Planet selected = selector.Select(game, new StubRNG(), null).Cast<Planet>().Single();

            Assert.AreSame(rimPlanet, selected);
        }

        [Test]
        public void SelectManufacturingOrders_MatchingPlanet_ReturnsQueuedProduct()
        {
            GameRoot game = BuildGame(out Planet planet);
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

        [Test]
        public void SelectOfficers_IncludeRetainedAtRecordedPlanet_ReturnsVoidOfficer()
        {
            GameRoot game = BuildGame(out Planet origin);
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            game.ChangeOwnership(origin, "rebels");
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            han.IsCaptured = true;
            game.AttachNode(han, origin);
            game.AddToVoid(han);
            SelectOfficers selector = new SelectOfficers
            {
                PlanetInstanceID = origin.InstanceID,
                OwnerFactionInstanceID = "rebels",
                IsCaptured = true,
                IncludeRetained = true,
            };

            List<ISceneNode> selected = selector.Select(game, new FixedRNG(0), null).ToList();

            CollectionAssert.AreEqual(new ISceneNode[] { han }, selected);
        }

        [Test]
        public void SelectBinding_StaleReferenceWithRegisteredInstanceID_ReturnsCanonicalNode()
        {
            GameRoot game = BuildGame(out Planet origin);
            Officer canonical = EntityFactory.CreateOfficer("han", "faction");
            game.AttachNode(canonical, origin);
            Officer stale = EntityFactory.CreateOfficer(canonical.InstanceID, "faction");
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                null,
                null
            );
            context.Bind("officer", stale);

            ISceneNode selected = new SelectBinding { Binding = "$officer" }
                .Select(game, new FixedRNG(0), context)
                .Single();

            Assert.AreSame(canonical, selected);
        }

        [Test]
        public void SelectBinding_DetachedRegisteredNode_ReturnsCanonicalNode()
        {
            GameRoot game = BuildGame(out Planet origin);
            Officer officer = EntityFactory.CreateOfficer("han", "faction");
            game.AttachNode(officer, origin);
            game.AddToVoid(officer);
            game.RemoveFromVoid(officer);
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                null,
                null
            );
            context.Bind("officer", officer);

            ISceneNode selected = new SelectBinding { Binding = "$officer" }
                .Select(game, new FixedRNG(0), context)
                .Single();

            Assert.AreSame(officer, selected);
        }

        [Test]
        public void SelectCapitalShips_ChildOfVoidUnit_ExcludesRetainedSubtree()
        {
            GameRoot game = BuildGame(out Planet planet);
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "faction" };
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "faction" };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AddToVoid(fleet);
            SelectCapitalShips selector = new SelectCapitalShips { InstanceID = ship.InstanceID };

            List<ISceneNode> selected = selector.Select(game, new FixedRNG(0), null).ToList();

            Assert.IsEmpty(selected);
        }

        private static GameRoot BuildGame(out Planet planet)
        {
            GameRoot game = new GameRoot(new GameConfig());
            game.Factions.Add(new Faction { InstanceID = "faction" });
            PlanetSector system = new PlanetSector
            {
                InstanceID = "core-system",
                SectorType = PlanetSectorType.Core,
            };
            planet = new Planet
            {
                InstanceID = "core-planet",
                OwnerInstanceID = "faction",
                IsColonized = true,
            };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);
            return game;
        }
    }
}
