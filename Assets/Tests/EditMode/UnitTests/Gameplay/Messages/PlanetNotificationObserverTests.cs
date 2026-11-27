using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Gameplay.Scripting;

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
        private JediCommands _jediCommands;
        private MovementCommands _movement;
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
        /// Releases the fixture's result subscriptions.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _observer.Dispose();
            _jediCommands.Dispose();
        }

        [Test]
        public void HandleResults_AuthoredSupportChange_ReportsSmugglingOnceWithoutTickPolling()
        {
            List<GameResult> first = SetOpposingSupport(85);
            List<GameResult> second = _results.Publish(Array.Empty<GameResult>());

            Assert.That(first.OfType<SmugglingChangedResult>().Single().NewPercent, Is.EqualTo(75));
            Assert.That(second.OfType<SmugglingChangedResult>(), Is.Empty);
            Assert.That(_game.CurrentTick, Is.Zero);
        }

        [Test]
        public void HandleResults_UnobservedMutation_DoesNotInventNotificationOrChangeProductionRules()
        {
            _planet.SetPopularSupport("other", 85);

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
            CapitalShip ship = new()
            {
                InstanceID = "ship",
                OwnerInstanceID = "owner",
                ManufacturingStatus = ManufacturingStatus.Complete,
                Hyperdrive = 50,
            };
            _game.AttachNode(ship, fleet);
            Connect();

            List<GameResult> notifications = deleteShip
                ? PublishAction(
                    new DestroyUnitsAction
                    {
                        Selectors = new()
                        {
                            new SelectCapitalShips { InstanceID = ship.InstanceID },
                        },
                    }
                )
                : MoveFleet(fleet, destination);

            PlanetStatChangedResult change = notifications
                .OfType<PlanetStatChangedResult>()
                .Single(result =>
                    result.Planet == _planet && result.Category == PlanetChangeCategory.Smuggling
                );
            Assert.That(change.NewValue, Is.EqualTo(75));
            Assert.That(change.OldValue, Is.LessThan(change.NewValue));
        }

        [Test]
        public void HandleResults_FleetFormedFromCapturedShip_ReportsSmugglingChange()
        {
            _planet.SetPopularSupport("other", 85);
            Fleet otherFleet = new() { InstanceID = "other-fleet", OwnerInstanceID = "other" };
            _game.AttachNode(otherFleet, _planet);
            CapitalShip ship = new()
            {
                InstanceID = "captured-ship",
                OwnerInstanceID = "other",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(ship, otherFleet);
            _game.ChangeOwnership(ship, "owner");
            Connect();

            List<GameResult> results = new();
            Fleet formed = new FleetCommands(_game).CreateFleet(new[] { ship }, "owner", results);
            List<GameResult> completed = _results.Publish(results);

            Assert.That(ship.GetParent(), Is.SameAs(formed));
            PlanetStatChangedResult change = completed
                .OfType<PlanetStatChangedResult>()
                .Single(result => result.Category == PlanetChangeCategory.Smuggling);
            Assert.That(change.Planet, Is.SameAs(_planet));
            Assert.That(change.NewValue, Is.LessThan(change.OldValue));
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

            List<GameResult> first = PublishAction(
                new DestroyUnitsAction
                {
                    Selectors = new() { new SelectRegiments { InstanceID = "troop-0" } },
                }
            );
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
            List<GameResult> notifications = SetOpposingSupport(85);

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
                    Hyperdrive = 50,
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

            List<GameResult> notifications = MoveFleet(fleet, destination);

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

            List<GameResult> disabled = PublishAction(
                new SetNodeStateAction
                {
                    InstanceID = fleet.InstanceID,
                    State = SceneNodeState.Inactive,
                }
            );
            List<GameResult> enabled = PublishAction(
                new SetNodeStateAction
                {
                    InstanceID = fleet.InstanceID,
                    State = SceneNodeState.Active,
                }
            );

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
        public void HandleResults_ActivatedSector_ReportsSmugglingOnPreviouslyInactivePlanets()
        {
            _game.SetNodeEnabled(_planet.GetParent(), false);
            Connect();
            _planet.SetPopularSupport("other", 85);
            List<GameResult> notifications = PublishAction(
                new SetNodeStateAction
                {
                    InstanceID = _planet.GetParent().InstanceID,
                    State = SceneNodeState.Active,
                }
            );

            Assert.That(
                notifications.OfType<SmugglingChangedResult>().Single().Planet,
                Is.SameAs(_planet)
            );
        }

        [Test]
        public void Dispose_LaterSupportChange_DetachesResultObserver()
        {
            _observer.Dispose();

            Assert.That(SetOpposingSupport(85).OfType<SmugglingChangedResult>(), Is.Empty);
        }

        /// <summary>
        /// Applies an authored support change through the result bus.
        /// </summary>
        /// <param name="support">The rival faction's new support.</param>
        /// <returns>The completed support change and its notifications.</returns>
        private List<GameResult> SetOpposingSupport(int support) =>
            PublishAction(
                new SetPopularSupportAction
                {
                    PlanetInstanceID = _planet.InstanceID,
                    FactionInstanceID = "other",
                    Support = support,
                }
            );

        /// <summary>
        /// Publishes the results of a completed game action.
        /// </summary>
        /// <param name="action">The authored action.</param>
        /// <returns>The results and resulting notifications.</returns>
        private List<GameResult> PublishAction(GameAction action) =>
            _results.Publish(
                new GameActionCommands(_game, _game.Random).Execute(
                    new[] { action },
                    evaluation: null
                )
            );

        /// <summary>
        /// Issues a fleet movement order and publishes its immediate results.
        /// </summary>
        /// <param name="fleet">The fleet leaving port.</param>
        /// <param name="destination">The destination planet.</param>
        /// <returns>The departure results and resulting notifications.</returns>
        private List<GameResult> MoveFleet(Fleet fleet, Planet destination)
        {
            Assert.IsTrue(_movement.TryRequestMove(fleet, destination));
            return _results.Publish(_movement.TakePendingResults());
        }

        /// <summary>
        /// Reconnects with silent baselines matching the prepared fixture world.
        /// </summary>
        private void Connect()
        {
            _observer?.Dispose();
            _jediCommands?.Dispose();
            _smuggling = new SmugglingCommands(_game);
            _jediCommands = new JediCommands(_game, new ThrowingRNG());
            PlanetaryControlCommands control = new(
                _game,
                new PlanetaryControlQueries(_game),
                new FogOfWarQueries(_game)
            );
            _movement = new MovementCommands(
                _game,
                new FogOfWarCommands(_game),
                new FleetCommands(_game),
                new FogOfWarQueries(_game),
                new MovementQueries(_game)
            );
            UprisingResolver uprising = new(_game, new ThrowingRNG(), control, _jediCommands);
            _observer = new PlanetNotificationObserver(_smuggling, uprising);
            _results = new GameResultBus();
            _observer.Connect(_results);
        }
    }
}
