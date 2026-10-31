using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class PlanetNotificationObserverTests
    {
        private GameRoot _game;
        private Faction _owner;
        private Planet _planet;
        private Building _mine;
        private SmugglingCommands _smuggling;
        private PlanetNotificationObserver _observer;
        private GameResultBus _results;

        /// <summary>
        /// Creates a subscribed world with two factions and an initially stable planet.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.Random = new FixedRNG();
            _owner = new Faction { InstanceID = "owner" };
            _game
                .GetFactions()
                .AddRange(
                    new[]
                    {
                        _owner,
                        new Faction { InstanceID = "other" },
                    }
                );
            PlanetSector sector = new() { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = "owner",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            _planet.SetPopularSupport("owner", 100);
            _game.AttachNode(_planet, sector);
            _mine = new Building
            {
                InstanceID = "mine",
                OwnerInstanceID = "owner",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(_mine, _planet);
            Connect();
        }

        /// <summary>
        /// Releases the fixture's model and result subscriptions.
        /// </summary>
        [TearDown]
        public void TearDown() => _observer.Dispose();

        [Test]
        public void HandleResults_ObservableSupportChange_ReportsSmugglingOnceWithoutTickPolling()
        {
            _planet.SetPopularSupport("owner", 15);
            _planet.SetPopularSupport("other", 85);

            List<GameResult> first = _results.Publish(Array.Empty<GameResult>());
            List<GameResult> second = _results.Publish(Array.Empty<GameResult>());

            Assert.That(first.OfType<SmugglingChangedResult>().Single().NewPercent, Is.EqualTo(75));
            Assert.That(second.OfType<SmugglingChangedResult>(), Is.Empty);
            Assert.That(_game.CurrentTick, Is.Zero);
        }

        [Test]
        public void HandleResults_UnobservedMutation_DoesNotInventNotificationOrChangeProductionRules()
        {
            _planet.PopularSupport["owner"] = 15;
            _planet.PopularSupport["other"] = 85;

            List<GameResult> notifications = _results.Publish(Array.Empty<GameResult>());
            _smuggling.BeginProduction();
            (Faction beneficiary, int diverted) = _smuggling.ResolveProductionBatch(_mine, 1);

            Assert.That(notifications, Is.Empty);
            Assert.That(beneficiary.InstanceID, Is.EqualTo("other"));
            Assert.That(diverted, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HandleResults_ForceDeparture_ReconcilesTheSourceWithoutArrivalResult(
            bool deleteShip
        )
        {
            _planet.SetPopularSupport("owner", 15);
            _planet.SetPopularSupport("other", 85);
            Planet destination = new()
            {
                InstanceID = "destination",
                OwnerInstanceID = "owner",
                IsColonized = true,
            };
            destination.SetPopularSupport("owner", 100);
            _game.AttachNode(destination, _planet.GetParent());
            Fleet fleet = new() { InstanceID = "fleet", OwnerInstanceID = "owner" };
            _game.AttachNode(fleet, _planet);
            _game.AttachNode(
                new CapitalShip
                {
                    InstanceID = "ship",
                    OwnerInstanceID = "owner",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                fleet
            );
            Connect();

            if (deleteShip)
                _game.DeleteNode(fleet.GetChildren<CapitalShip>().Single());
            else
                _game.MoveNode(fleet, destination);
            List<GameResult> notifications = _results.Publish(Array.Empty<GameResult>());

            PlanetStatChangedResult change = notifications
                .OfType<PlanetStatChangedResult>()
                .Single(result =>
                    result.Planet == _planet && result.Category == PlanetChangeCategory.Smuggling
                );
            Assert.That(change.NewValue, Is.EqualTo(75));
            Assert.That(change.OldValue, Is.LessThan(change.NewValue));
        }

        [Test]
        public void HandleResults_GarrisonFallsToRequirement_ReportsOneNewWarning()
        {
            _planet.SetPopularSupport("owner", 10);
            _planet.SetPopularSupport("other", 90);
            int required = UprisingQueries.CalculateGarrisonRequirement(
                _planet,
                _owner,
                _game.Config.AI.Garrison
            );
            for (int index = 0; index <= required; index++)
                _game.AttachNode(
                    new Regiment
                    {
                        InstanceID = "troop-" + index,
                        OwnerInstanceID = "owner",
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    _planet
                );
            Connect();

            _game.DetachNode(_planet.GetChildren<Regiment>()[0]);
            List<GameResult> first = _results.Publish(Array.Empty<GameResult>());
            List<GameResult> second = _results.Publish(Array.Empty<GameResult>());

            Assert.That(first.OfType<PlanetNearUprisingResult>().Count(), Is.EqualTo(1));
            Assert.That(second.OfType<PlanetNearUprisingResult>(), Is.Empty);
        }

        [Test]
        public void HandleResults_ObservedSupportChange_DoesNotScanAnUnrelatedPlanet()
        {
            Planet unrelated = new()
            {
                InstanceID = "unrelated",
                OwnerInstanceID = "owner",
                IsColonized = true,
            };
            unrelated.SetPopularSupport("owner", 100);
            _game.AttachNode(unrelated, _planet.GetParent());
            Connect();
            unrelated.PopularSupport["owner"] = 15;
            unrelated.PopularSupport["other"] = 85;
            _planet.SetPopularSupport("owner", 15);
            _planet.SetPopularSupport("other", 85);

            List<GameResult> notifications = _results.Publish(Array.Empty<GameResult>());

            Assert.That(
                notifications.OfType<SmugglingChangedResult>().Single().Planet,
                Is.SameAs(_planet)
            );
        }

        [Test]
        public void HandleResults_SuppressionShipLeavesSector_RechecksOtherAffectedGarrisons()
        {
            Planet affected = new()
            {
                InstanceID = "affected",
                OwnerInstanceID = "owner",
                IsColonized = true,
            };
            affected.SetPopularSupport("owner", 10);
            affected.SetPopularSupport("other", 90);
            _game.AttachNode(affected, _planet.GetParent());
            int required = UprisingQueries.CalculateGarrisonRequirement(
                affected,
                _owner,
                _game.Config.AI.Garrison
            );
            for (int index = 0; index < required; index++)
                _game.AttachNode(
                    new Regiment
                    {
                        InstanceID = "garrison-" + index,
                        OwnerInstanceID = "owner",
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    affected
                );
            Fleet fleet = new() { InstanceID = "suppression-fleet", OwnerInstanceID = "owner" };
            _game.AttachNode(fleet, _planet);
            _game.AttachNode(
                new CapitalShip
                {
                    InstanceID = "suppression-ship",
                    OwnerInstanceID = "owner",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    SectorSuppression = 2,
                },
                fleet
            );
            PlanetSector destinationSector = new() { InstanceID = "destination-sector" };
            _game.AttachNode(destinationSector, _game.Galaxy);
            Planet destination = new()
            {
                InstanceID = "destination",
                OwnerInstanceID = "owner",
                IsColonized = true,
            };
            destination.SetPopularSupport("owner", 100);
            _game.AttachNode(destination, destinationSector);
            Connect();

            _game.MoveNode(fleet, destination);
            List<GameResult> notifications = _results.Publish(Array.Empty<GameResult>());

            Assert.That(
                notifications.OfType<PlanetNearUprisingResult>().Single().Planet,
                Is.SameAs(affected)
            );
        }

        [Test]
        public void HandleResults_ForceActivationChanges_ReconcilesStationedSuppression()
        {
            _planet.SetPopularSupport("owner", 15);
            _planet.SetPopularSupport("other", 85);
            Fleet fleet = new() { InstanceID = "fleet", OwnerInstanceID = "owner" };
            _game.AttachNode(fleet, _planet);
            _game.AttachNode(
                new CapitalShip
                {
                    InstanceID = "ship",
                    OwnerInstanceID = "owner",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                fleet
            );
            Connect();

            _game.SetNodeEnabled(fleet, false);
            List<GameResult> disabled = _results.Publish(Array.Empty<GameResult>());
            _game.SetNodeEnabled(fleet, true);
            List<GameResult> enabled = _results.Publish(Array.Empty<GameResult>());

            Assert.That(
                disabled.OfType<PlanetStatChangedResult>().Single().NewValue,
                Is.EqualTo(75)
            );
            Assert.That(
                enabled.OfType<PlanetStatChangedResult>().Single().NewValue,
                Is.LessThan(75)
            );
        }

        [Test]
        public void HandleResults_ActivatedSector_SubscribesPreviouslyInactivePlanets()
        {
            _game.SetNodeEnabled(_planet.GetParent(), false);
            Connect();
            _game.SetNodeEnabled(_planet.GetParent(), true);
            _results.Publish(Array.Empty<GameResult>());
            _planet.SetPopularSupport("owner", 15);
            _planet.SetPopularSupport("other", 85);

            List<GameResult> notifications = _results.Publish(Array.Empty<GameResult>());

            Assert.That(
                notifications.OfType<SmugglingChangedResult>().Single().Planet,
                Is.SameAs(_planet)
            );
        }

        [Test]
        public void Dispose_LaterSupportChange_DetachesModelAndResultObservers()
        {
            _observer.Dispose();
            _planet.SetPopularSupport("owner", 15);
            _planet.SetPopularSupport("other", 85);

            Assert.That(_observer.HasPendingChanges, Is.False);
            Assert.That(_results.Publish(Array.Empty<GameResult>()), Is.Empty);
        }

        /// <summary>
        /// Reconnects with silent baselines matching the prepared fixture world.
        /// </summary>
        private void Connect()
        {
            _observer?.Dispose();
            _smuggling = new SmugglingCommands(_game);
            PlanetaryControlCommands control = new(
                _game,
                new PlanetaryControlQueries(_game),
                new FogOfWarQueries(_game)
            );
            UprisingResolver uprising = new(_game, new ThrowingRNG(), control);
            _observer = new PlanetNotificationObserver(_game, _smuggling, uprising);
            _results = new GameResultBus();
            _observer.Connect(_results);
        }
    }
}
