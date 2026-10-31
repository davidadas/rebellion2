using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class SmugglingCommandsTests
    {
        private GameRoot _game;
        private Faction _controller;
        private Planet _planet;
        private SmugglingCommands _system;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestContent.Data.GameConfig);
            _game.Random = new StubRNG();
            _controller = new Faction { InstanceID = "FACTION1" };
            _game.GetFactions().Add(_controller);
            _game.GetFactions().Add(new Faction { InstanceID = "FACTION2" });
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR1" };
            _game.AttachNode(sector, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "PLANET1",
                OwnerInstanceID = _controller.InstanceID,
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { _controller.InstanceID, 100 } },
            };
            _game.AttachNode(_planet, sector);
            _system = new SmugglingCommands(_game);
        }

        [Test]
        public void ResolveProductionRecipient_DetachedFacility_ReturnsControllerWithoutRolling()
        {
            _game.Random = new ThrowingRNG();

            Faction recipient = _system.ResolveProductionRecipient(_controller, new Building());

            Assert.AreSame(_controller, recipient);
        }

        [Test]
        public void ResolveProductionRecipient_NoSmuggling_ReturnsControllerWithoutRolling()
        {
            Building facility = AddFacility();
            _game.Random = new ThrowingRNG();

            Faction recipient = _system.ResolveProductionRecipient(_controller, facility);

            Assert.AreSame(_controller, recipient);
        }

        [TestCase(0.74, "FACTION2")]
        [TestCase(0.75, "FACTION1")]
        public void ResolveProductionRecipient_ThresholdBoundary_ReturnsExpectedFaction(
            double roll,
            string recipientID
        )
        {
            Building facility = AddFacility();
            SetSupport(15, 85);
            RefreshPlanet();
            _game.Random = new QueueRNG(roll);

            Faction recipient = _system.ResolveProductionRecipient(_controller, facility);

            Assert.AreEqual(recipientID, recipient.InstanceID);
        }

        [Test]
        public void ResolveProductionBatch_MixedRolls_ReturnsDivertedQuantity()
        {
            Building facility = AddFacility();
            SetSupport(15, 85);
            RefreshPlanet();
            _game.Random = new QueueRNG(0.5, 0.8, 0.1);

            (Faction beneficiary, int divertedQuantity) = _system.ResolveProductionBatch(
                facility,
                3
            );

            Assert.AreEqual("FACTION2", beneficiary.InstanceID);
            Assert.AreEqual(2, divertedQuantity);
        }

        [Test]
        public void RefreshPlanet_LowSupport_StartsConfiguredSmugglingLossPercentage()
        {
            SetSupport(15, 85);

            IReadOnlyList<GameResult> results = RefreshPlanet();

            PlanetStatChangedResult stat = results.OfType<PlanetStatChangedResult>().Single();
            Assert.AreEqual(75, stat.NewValue);
            SmugglingChangedResult changed = results.OfType<SmugglingChangedResult>().Single();
            Assert.AreSame(_controller, changed.Controller);
            Assert.AreEqual("FACTION2", changed.Beneficiary.InstanceID);
        }

        [Test]
        public void RefreshPlanet_ExistingSmugglingState_DoesNotRepeatStartNotification()
        {
            SetSupport(15, 85);
            _system = new SmugglingCommands(_game);

            IReadOnlyList<GameResult> results = RefreshPlanet();

            Assert.IsEmpty(results.OfType<SmugglingChangedResult>());
        }

        [Test]
        public void RefreshPlanet_GarrisonAndFleetPresence_ReduceSmugglingPercentage()
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

            PlanetStatChangedResult result = RefreshPlanet()
                .OfType<PlanetStatChangedResult>()
                .Single();

            Assert.AreEqual(58, result.NewValue);
        }

        [Test]
        public void RefreshPlanet_PlanetDestroyingShipPresent_FullySuppressesSmuggling()
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

            IReadOnlyList<GameResult> results = RefreshPlanet();

            Assert.IsEmpty(results);
        }

        [Test]
        public void RefreshPlanet_ControlChanged_EndsOldSmugglingAndStartsNewRelationship()
        {
            SetSupport(20, 20);
            RefreshPlanet();
            _planet.OwnerInstanceID = "FACTION2";

            SmugglingChangedResult[] changes = RefreshPlanet()
                .OfType<SmugglingChangedResult>()
                .ToArray();

            Assert.AreEqual(2, changes.Length);
            Assert.AreEqual("FACTION1", changes[0].Controller.InstanceID);
            Assert.AreEqual(0, changes[0].NewPercent);
            Assert.AreEqual("FACTION2", changes[1].Controller.InstanceID);
            Assert.AreEqual(0, changes[1].OldPercent);
        }

        [Test]
        public void RefreshPlanet_DiversionChangesWithinRelationship_OnlyReportsStatChange()
        {
            SetSupport(15, 85);
            RefreshPlanet();
            SetSupport(25, 75);

            IReadOnlyList<GameResult> results = RefreshPlanet();

            PlanetStatChangedResult stat = results.OfType<PlanetStatChangedResult>().Single();
            Assert.AreEqual(75, stat.OldValue);
            Assert.AreEqual(50, stat.NewValue);
            Assert.IsEmpty(results.OfType<SmugglingChangedResult>());
        }

        /// <summary>
        /// Reconciles the fixture's changed planet through the smuggling command contract.
        /// </summary>
        /// <returns>The selected notification facts.</returns>
        private IReadOnlyList<GameResult> RefreshPlanet()
        {
            List<GameResult> results = new();
            _system.RefreshPlanet(_planet, results);
            return results;
        }

        /// <summary>
        /// Attaches one completed facility to the controlled planet.
        /// </summary>
        /// <returns>The attached facility.</returns>
        private Building AddFacility()
        {
            _planet.EnergyCapacity = 1;
            Building facility = Active(new Building { InstanceID = "FACILITY" });
            _game.AttachNode(facility, _planet);
            return facility;
        }

        /// <summary>
        /// Sets support.
        /// </summary>
        /// <param name="controllerSupport">The controller support.</param>
        /// <param name="beneficiarySupport">The beneficiary support.</param>
        private void SetSupport(int controllerSupport, int beneficiarySupport)
        {
            _planet.PopularSupport = new Dictionary<string, int>
            {
                { "FACTION1", controllerSupport },
                { "FACTION2", beneficiarySupport },
            };
        }

        /// <summary>
        /// Executes active.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The result of active.</returns>
        private T Active<T>(T unit)
            where T : BaseSceneNode, IManufacturable
        {
            unit.OwnerInstanceID = _controller.InstanceID;
            unit.ManufacturingStatus = ManufacturingStatus.Complete;
            return unit;
        }
    }
}
