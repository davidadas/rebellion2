using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Gameplay.Scripting;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Gameplay.Scripting
{
    [TestFixture]
    public class GameSelectorResolverTests
    {
        [Test]
        public void Select_FirstCandidateMatches_DoesNotEvaluateLaterInvalidSelector()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
            SelectFirst selector = new SelectFirst
            {
                Selectors = { new SelectPlanets(), new SpawnUnits() },
            };

            ISceneNode selected = GameSelectorResolver
                .Select(selector, game, new StubRNG(), null)
                .Single();

            Assert.AreSame(planet, selected);
        }

        [Test]
        public void Select_PlanetDestroyedBeforeEnumeration_ExcludesPlanet()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
            IEnumerable<ISceneNode> selected = GameSelectorResolver.Select(
                new SelectPlanets(),
                game,
                new StubRNG(),
                null
            );
            planet.IsDestroyed = true;

            Assert.IsEmpty(selected);
        }

        [Test]
        public void Select_RandomCandidatesOutOfOrder_RollsInIdentityOrderBeforeEnumeration()
        {
            GameRoot game = BuildSelectionGame(out Planet first);
            Planet second = new Planet { InstanceID = "a-planet" };
            game.AttachNode(second, first.GetParent());
            QueueRNG provider = new QueueRNG(0.25, 0.75, 0.875);
            SelectRandom selector = new SelectRandom
            {
                ChancePercent = 50,
                Selectors = { new SelectPlanets(), new SelectPlanets() },
            };

            IEnumerable<ISceneNode> selected = GameSelectorResolver.Select(
                selector,
                game,
                provider,
                null
            );

            Assert.AreEqual(0.875, provider.NextDouble());
            CollectionAssert.AreEqual(new[] { second }, selected);
        }

        [Test]
        public void Select_SpawnSource_RejectsWithoutRandomDraw()
        {
            GameRoot game = BuildSelectionGame(out _);
            QueueRNG provider = new QueueRNG(0.25);

            Assert.Throws<InvalidOperationException>(() =>
                GameSelectorResolver.Select(new SpawnUnits(), game, provider, null)
            );

            Assert.AreEqual(0.25, provider.NextDouble());
        }

        [Test]
        public void Select_PlanetsMatchingInstanceID_ReturnsPlanet()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
            SelectPlanets selector = new SelectPlanets { InstanceID = planet.InstanceID };

            Planet selected = GameSelectorResolver
                .Select(selector, game, new StubRNG(), null)
                .Cast<Planet>()
                .Single();

            Assert.AreSame(planet, selected);
        }

        [Test]
        public void Select_PlanetsDestroyedPlanet_ReturnsNothing()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
            planet.IsDestroyed = true;
            SelectPlanets selector = new SelectPlanets { InstanceID = planet.InstanceID };

            bool any = GameSelectorResolver.Select(selector, game, new StubRNG(), null).Any();

            Assert.IsFalse(any);
        }

        [Test]
        public void Select_PlanetsNoFilters_ReturnsEverySurvivingPlanet()
        {
            GameRoot game = BuildSelectionGame(out Planet firstPlanet);
            Planet secondPlanet = new Planet { InstanceID = "second-planet" };
            game.AttachNode(secondPlanet, firstPlanet.GetParent());
            SelectPlanets selector = new SelectPlanets();

            Planet[] selected = GameSelectorResolver
                .Select(selector, game, new StubRNG(), null)
                .Cast<Planet>()
                .ToArray();

            CollectionAssert.AreEqual(new[] { firstPlanet, secondPlanet }, selected);
        }

        [Test]
        public void Select_RandomFilteredPlanetSet_ReturnsRequestedCount()
        {
            GameRoot game = BuildSelectionGame(out _);
            PlanetSector rimSector = new PlanetSector
            {
                InstanceID = "rim-sector",
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

            Planet selected = GameSelectorResolver
                .Select(selector, game, new StubRNG(), null)
                .Cast<Planet>()
                .Single();

            Assert.AreSame(rimPlanet, selected);
        }

        [Test]
        public void Select_ManufacturingOrdersMatchingPlanet_ReturnsQueuedProduct()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
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

            IManufacturable selected = GameSelectorResolver
                .Select(selector, game, new StubRNG(), null)
                .Cast<IManufacturable>()
                .Single();

            Assert.AreSame(building, selected);
        }

        [Test]
        public void Select_CapitalShipsIncludeInactive_ReturnsCapitalShip()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "capital-ship",
                OwnerInstanceID = "faction",
            };
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "faction" };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            ship.IsEnabled = false;
            SelectCapitalShips selector = new SelectCapitalShips
            {
                InstanceID = ship.InstanceID,
                IncludeInactive = true,
            };

            ISceneNode selected = GameSelectorResolver
                .Select(selector, game, new FixedRNG(0), null)
                .Single();

            Assert.AreSame(ship, selected);
        }

        [Test]
        public void Select_OfficersIncludeInactiveAtCurrentPlanet_ReturnsOfficer()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            officer.IsCaptured = true;
            game.AttachNode(officer, planet);
            officer.IsEnabled = false;
            SelectOfficers selector = new SelectOfficers
            {
                PlanetInstanceID = planet.InstanceID,
                OwnerFactionInstanceID = "faction",
                IsCaptured = true,
                IncludeInactive = true,
            };

            List<ISceneNode> selected = GameSelectorResolver
                .Select(selector, game, new FixedRNG(0), null)
                .ToList();

            CollectionAssert.AreEqual(new ISceneNode[] { officer }, selected);
        }

        [Test]
        public void Select_MissionsMatchingOwner_ReturnsOwnedMission()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
            game.GetFactions().Add(new Faction { InstanceID = "other-faction" });
            DiplomacyMission ownedMission = new DiplomacyMission
            {
                InstanceID = "owned-mission",
                OwnerInstanceID = "faction",
            };
            DiplomacyMission otherMission = new DiplomacyMission
            {
                InstanceID = "other-mission",
                OwnerInstanceID = "other-faction",
            };
            game.AttachNode(ownedMission, planet);
            game.AttachNode(otherMission, planet);
            SelectMissions selector = new SelectMissions { OwnerFactionInstanceID = "faction" };

            List<ISceneNode> selected = GameSelectorResolver
                .Select(selector, game, new FixedRNG(0), null)
                .ToList();

            CollectionAssert.AreEqual(new ISceneNode[] { ownedMission }, selected);
        }

        [Test]
        public void Select_BindingStaleReferenceWithRegisteredInstanceID_ReturnsCanonicalNode()
        {
            GameRoot game = BuildSelectionGame(out Planet origin);
            Officer canonical = EntityFactory.CreateOfficer("han", "faction");
            game.AttachNode(canonical, origin);
            Officer stale = EntityFactory.CreateOfficer(canonical.InstanceID, "faction");
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("officer", stale);

            ISceneNode selected = GameSelectorResolver
                .Select(new SelectBinding { Binding = "officer" }, game, new FixedRNG(0), context)
                .Single();

            Assert.AreSame(canonical, selected);
        }

        [Test]
        public void Select_BindingInactiveRegisteredNode_ReturnsCanonicalNode()
        {
            GameRoot game = BuildSelectionGame(out Planet origin);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, origin);
            officer.IsEnabled = false;
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("officer", officer);

            ISceneNode selected = GameSelectorResolver
                .Select(new SelectBinding { Binding = "officer" }, game, new FixedRNG(0), context)
                .Single();

            Assert.AreSame(officer, selected);
        }

        [Test]
        public void Select_PreviousLocationInactiveUnit_ReturnsPreviousLocation()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, planet);
            officer.LastParentInstanceID = planet.InstanceID;
            officer.IsEnabled = false;
            SelectPreviousLocation selector = new SelectPreviousLocation
            {
                UnitInstanceID = officer.InstanceID,
            };

            ISceneNode selected = GameSelectorResolver
                .Select(selector, game, new FixedRNG(0), null)
                .Single();

            Assert.AreSame(planet, selected);
        }

        /// <summary>
        /// Builds game.
        /// </summary>
        /// <param name="planet">Receives the planet.</param>
        /// <returns>The constructed game.</returns>
        private static GameRoot BuildSelectionGame(out Planet planet)
        {
            GameRoot game = TestGame.Create(new GameConfig());
            game.GetFactions().Add(new Faction { InstanceID = "faction" });
            PlanetSector sector = new PlanetSector
            {
                InstanceID = "core-sector",
                SectorType = PlanetSectorType.Core,
            };
            planet = new Planet
            {
                InstanceID = "core-planet",
                OwnerInstanceID = "faction",
                IsColonized = true,
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            return game;
        }
    }
}
