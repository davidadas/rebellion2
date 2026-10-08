using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class GameTickProcessorTests
    {
        private GameRoot _game;
        private GameSession _session;
        private GameTickProcessor _tick;
        private Func<IEnumerable<GameResult>, bool, List<GameResult>> _processResults;

        /// <summary>
        /// Creates a prepared game and the runtime dependencies used by tick scheduling.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.SetGameSpeed(TickSpeed.Fast);
            _session = new GameSession(_game, TestGameData.Create(_game.Config));
            _processResults = (results, _) => _session.Results.Publish(results);
            _tick = CreateTickProcessor();
        }

        /// <summary>
        /// Releases the result subscriptions owned by the test runtime.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _session.Dispose();
        }

        [Test]
        public void ProcessTick_PausedGame_DoesNotAdvanceTick()
        {
            _game.SetGameSpeed(TickSpeed.Paused);

            _tick.ProcessTick();

            Assert.AreEqual(0, _game.CurrentTick);
        }

        [Test]
        public void ProcessTick_CompletedTick_NotifiesOnce()
        {
            int completions = 0;
            _tick.TickCompleted += () => completions++;

            _tick.ProcessTick();

            Assert.AreEqual(1, completions);
            Assert.AreEqual(1, _game.CurrentTick);
        }

        [Test]
        public void ProcessTickIncrementally_MultipleReadyFacilities_YieldsAfterEachProductionPoint()
        {
            const string factionId = "EMPIRE";
            Faction faction = new Faction
            {
                InstanceID = factionId,
                DisplayName = "Empire",
                RefinedMaterialStockpile = 1000,
            };
            _game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            _game.AttachNode(sector, _game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "PLANET",
                OwnerInstanceID = factionId,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            _game.AttachNode(planet, sector);
            for (int index = 0; index < 2; index++)
            {
                _game.AttachNode(
                    new Building
                    {
                        InstanceID = $"SHIPYARD_{index}",
                        OwnerInstanceID = factionId,
                        BuildingType = BuildingType.ConstructionFacility,
                        ProductionType = ManufacturingType.Building,
                        ProcessRate = 1,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    planet
                );
            }

            Building order = new Building
            {
                InstanceID = "ORDER",
                OwnerInstanceID = factionId,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };
            Assert.IsTrue(
                _session.GetService<ManufacturingCommands>().Enqueue(planet, order, planet)
            );
            int progressNotifications = 0;
            _tick.TickProgressed += () => progressNotifications++;
            IEnumerator tick = _tick.ProcessTickIncrementally();

            Assert.IsTrue(tick.MoveNext());
            Assert.AreEqual(1, order.ManufacturingProgress);
            Assert.AreEqual(1, progressNotifications);

            Assert.IsTrue(tick.MoveNext());
            Assert.AreEqual(2, order.ManufacturingProgress);
            Assert.AreEqual(2, progressNotifications);

            while (tick.MoveNext()) { }
            Assert.IsTrue(_tick.IsSettled);
        }

        [Test]
        public void ProcessTickIncrementally_DisposedBeforeCompletion_ReleasesBusyGuard()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "AI", DisplayName = "AI" });
            IEnumerator tick = _tick.ProcessTickIncrementally();
            Assert.IsTrue(tick.MoveNext());

            (tick as IDisposable)?.Dispose();

            Assert.IsFalse(_tick.IsBusy);
            Assert.IsTrue(_tick.IsSettled);
        }

        [Test]
        public void ProcessTick_CompletionObserverThrows_RestoresIdleState()
        {
            InvalidOperationException expected = new("observer failure");
            _tick.TickCompleted += () => throw expected;

            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(
                _tick.ProcessTick
            );

            Assert.AreSame(expected, actual);
            Assert.IsFalse(_tick.IsBusy);
            Assert.IsTrue(_tick.IsSettled);
        }

        [Test]
        public void ProcessTick_ResultDeliveryThrows_ReleasesBusyGuard()
        {
            InvalidOperationException expected = new("delivery failure");
            _processResults = (_, _) => throw expected;
            _tick = CreateTickProcessor();

            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(
                _tick.ProcessTick
            );

            Assert.AreSame(expected, actual);
            Assert.IsFalse(_tick.IsBusy);
            Assert.IsTrue(_tick.IsSettled);
        }

        [Test]
        public void Reset_SuspendedIterator_PreservesBusyGuardUntilDisposal()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "AI", DisplayName = "AI" });
            IEnumerator tick = _tick.ProcessTickIncrementally();
            Assert.IsTrue(tick.MoveNext());

            _tick.Reset();

            Assert.IsTrue(_tick.IsBusy);
            Assert.IsTrue(_tick.IsSettled);
            (tick as IDisposable)?.Dispose();
            Assert.IsFalse(_tick.IsBusy);
        }

        [Test]
        public void ReconcileLoadedState_NoEncounter_DoesNotAdvanceTick()
        {
            _tick.ReconcileLoadedState();

            Assert.AreEqual(0, _game.CurrentTick);
            Assert.IsTrue(_tick.IsSettled);
        }

        /// <summary>
        /// Connects tick scheduling to the current session's services.
        /// </summary>
        /// <returns>The tick processor under test.</returns>
        private GameTickProcessor CreateTickProcessor()
        {
            GameTickProcessor tick = new GameTickProcessor(_processResults, _ => { });
            tick.ConnectRuntime(_session);
            return tick;
        }
    }
}
