using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class SmugglingSystemTests
    {
        private GameRoot _game;
        private Faction _controller;
        private Planet _planet;
        private SmugglingSystem _system;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestContent.Data.GameConfig) { Random = new StubRNG() };
            _controller = new Faction { InstanceID = "FACTION1" };
            _game.Factions.Add(_controller);
            _game.Factions.Add(new Faction { InstanceID = "FACTION2" });
            PlanetSector planetSector = new PlanetSector { InstanceID = "SYSTEM1" };
            _game.AttachNode(planetSector, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "PLANET1",
                OwnerInstanceID = _controller.InstanceID,
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { _controller.InstanceID, 100 } },
            };
            _game.AttachNode(_planet, planetSector);
            _system = new SmugglingSystem(_game);
        }

        [Test]
        public void ProcessTick_LowSupport_StartsConfiguredSmugglingLossPercentage()
        {
            SetSupport(15, 85);

            List<GameResult> results = _system.ProcessTick();

            PlanetStatChangedResult stat = results.OfType<PlanetStatChangedResult>().Single();
            Assert.AreEqual(75, stat.NewValue);
            SmugglingChangedResult changed = results.OfType<SmugglingChangedResult>().Single();
            Assert.AreSame(_controller, changed.Controller);
            Assert.AreEqual("FACTION2", changed.Beneficiary.InstanceID);
        }

        [Test]
        public void ProcessTick_ExistingSmugglingState_DoesNotRepeatStartNotification()
        {
            SetSupport(15, 85);
            _system = new SmugglingSystem(_game);

            List<GameResult> results = _system.ProcessTick();

            Assert.IsEmpty(results.OfType<SmugglingChangedResult>());
        }

        [Test]
        public void ProcessTick_GarrisonAndFleetPresence_ReduceSmugglingPercentage()
        {
            SetSupport(15, 85);
            _game.AttachNode(Active(new Regiment { InstanceID = "REGIMENT1" }), _planet);
            _game.AttachNode(Active(new Starfighter { InstanceID = "FIGHTER1" }), _planet);
            Fleet fleet = new Fleet
            {
                InstanceID = "FLEET1",
                OwnerInstanceID = _controller.InstanceID,
            };
            _game.AttachNode(fleet, _planet);
            _game.AttachNode(Active(new CapitalShip { InstanceID = "SHIP1" }), fleet);

            PlanetStatChangedResult result = _system
                .ProcessTick()
                .OfType<PlanetStatChangedResult>()
                .Single();

            Assert.AreEqual(58, result.NewValue);
        }

        [Test]
        public void ProcessTick_PlanetDestroyingShipPresent_FullySuppressesSmuggling()
        {
            SetSupport(15, 85);
            Fleet fleet = new Fleet
            {
                InstanceID = "FLEET1",
                OwnerInstanceID = _controller.InstanceID,
            };
            _game.AttachNode(fleet, _planet);
            _game.AttachNode(
                Active(
                    new CapitalShip { InstanceID = "PLANET_DESTROYER", CanDestroyPlanets = true }
                ),
                fleet
            );

            List<GameResult> results = _system.ProcessTick();

            Assert.IsEmpty(results);
        }

        [Test]
        public void ProcessTick_ControlChanged_EndsOldSmugglingAndStartsNewRelationship()
        {
            SetSupport(20, 20);
            _system.ProcessTick();
            _planet.OwnerInstanceID = "FACTION2";

            SmugglingChangedResult[] changes = _system
                .ProcessTick()
                .OfType<SmugglingChangedResult>()
                .ToArray();

            Assert.AreEqual(2, changes.Length);
            Assert.AreEqual("FACTION1", changes[0].Controller.InstanceID);
            Assert.AreEqual(0, changes[0].NewPercent);
            Assert.AreEqual("FACTION2", changes[1].Controller.InstanceID);
            Assert.AreEqual(0, changes[1].OldPercent);
        }

        [Test]
        public void ProcessTick_DiversionChangesWithinRelationship_OnlyReportsStatChange()
        {
            SetSupport(15, 85);
            _system.ProcessTick();
            SetSupport(25, 75);

            List<GameResult> results = _system.ProcessTick();

            PlanetStatChangedResult stat = results.OfType<PlanetStatChangedResult>().Single();
            Assert.AreEqual(75, stat.OldValue);
            Assert.AreEqual(50, stat.NewValue);
            Assert.IsEmpty(results.OfType<SmugglingChangedResult>());
        }

        private void SetSupport(int controllerSupport, int beneficiarySupport)
        {
            _planet.PopularSupport = new Dictionary<string, int>
            {
                { "FACTION1", controllerSupport },
                { "FACTION2", beneficiarySupport },
            };
        }

        private T Active<T>(T unit)
            where T : BaseSceneNode, IManufacturable
        {
            unit.OwnerInstanceID = _controller.InstanceID;
            unit.ManufacturingStatus = ManufacturingStatus.Complete;
            return unit;
        }
    }
}
