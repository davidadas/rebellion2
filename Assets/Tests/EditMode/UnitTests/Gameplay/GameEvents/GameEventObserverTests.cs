using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Gameplay;
using Rebellion.Gameplay.GameEvents;
using Rebellion.Gameplay.Scripting;

namespace Rebellion.Tests.Gameplay.GameEvents
{
    [TestFixture]
    public sealed class GameEventObserverTests
    {
        [Test]
        public void Constructor_NullCommands_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GameEventObserver(null));
        }

        [Test]
        public void Connect_NullResultBus_ThrowsArgumentNullException()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            var eventRandom = new FixedRNG();
            GameEventObserver observer = new(
                new GameEventCommands(game, eventRandom, new GameActionCommands(game, eventRandom))
            );

            Assert.Throws<ArgumentNullException>(() => observer.Connect(null));
        }

        [Test]
        public void Connect_AlreadyConnected_ThrowsInvalidOperationException()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            var eventRandom = new FixedRNG();
            GameEventObserver observer = new(
                new GameEventCommands(game, eventRandom, new GameActionCommands(game, eventRandom))
            );
            observer.Connect(new GameResultBus());

            Assert.Throws<InvalidOperationException>(() => observer.Connect(new GameResultBus()));
        }

        [Test]
        public void Connect_MatchingResult_ActivatesTriggeredEvent()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameEvent gameEvent = CreateTriggeredEvent();
            game.GetEventPool().Add(gameEvent);
            var eventRandom = new FixedRNG();
            GameEventObserver observer = new(
                new GameEventCommands(game, eventRandom, new GameActionCommands(game, eventRandom))
            );
            GameResultBus results = new();
            observer.Connect(results);

            results.Publish(new DuelResult());

            Assert.AreEqual(1, game.EventRuntime.GetVariable("triggered-event-fired"));
            Assert.IsFalse(game.GetEventPool().Contains(gameEvent));
        }

        [Test]
        public void Connect_OwnedNeutralOwnedTransitions_RearmsScopedTargetAtLatestTransitionTick()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction alliance = new Faction { InstanceID = "alliance" };
            Faction empire = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(planet, sector);
            GameEvent gameEvent = CreateScopedEvent();
            game.GetEventPool().Add(gameEvent);
            var eventRandom = new FixedRNG();
            GameEventCommands commands = new(
                game,
                eventRandom,
                new GameActionCommands(game, eventRandom)
            );
            GameEventObserver observer = new(commands);
            GameResultBus results = new();
            observer.Connect(results);
            game.CurrentTick = 100;
            commands.ProcessScheduledEvents(game.GetEventPool());
            planet.OwnerInstanceID = empire.InstanceID;

            results.Publish(
                new GameResult[]
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        PreviousOwner = alliance,
                        Tick = 110,
                    },
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        NewOwner = empire,
                        Tick = 120,
                    },
                }
            );

            GameEventState targetState = game
                .EventRuntime.GetState(gameEvent.InstanceID)
                .ScopedStates[planet.InstanceID];
            Assert.AreEqual(150, targetState.NextEligibleTick);
            Assert.Zero(targetState.ActivationCount);
        }

        [Test]
        public void Connect_OwnedOwnerTransition_PreservesScopedTargetSchedule()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction alliance = new Faction { InstanceID = "alliance" };
            Faction empire = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(planet, sector);
            GameEvent gameEvent = CreateScopedEvent();
            game.GetEventPool().Add(gameEvent);
            var eventRandom = new FixedRNG();
            GameEventCommands commands = new(
                game,
                eventRandom,
                new GameActionCommands(game, eventRandom)
            );
            GameEventObserver observer = new(commands);
            GameResultBus results = new();
            observer.Connect(results);
            game.CurrentTick = 100;
            commands.ProcessScheduledEvents(game.GetEventPool());
            GameEventState originalState = game
                .EventRuntime.GetState(gameEvent.InstanceID)
                .ScopedStates[planet.InstanceID];
            planet.OwnerInstanceID = empire.InstanceID;

            results.Publish(
                new PlanetOwnershipChangedResult
                {
                    Planet = planet,
                    PreviousOwner = alliance,
                    NewOwner = empire,
                    Tick = 110,
                }
            );

            GameEventState targetState = game
                .EventRuntime.GetState(gameEvent.InstanceID)
                .ScopedStates[planet.InstanceID];
            Assert.AreSame(originalState, targetState);
            Assert.AreEqual(130, targetState.NextEligibleTick);
        }

        [Test]
        public void Connect_RandomIntervalOwnershipTransitions_RearmsAndPreservesRandomDraws()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction alliance = new Faction { InstanceID = "alliance" };
            Faction empire = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(planet, sector);
            GameEvent gameEvent = CreateScopedEvent();
            gameEvent.Schedule = new GameEventSchedule
            {
                RandomInterval = new RandomInterval { MinimumTicks = 30, MaximumTicks = 1000 },
            };
            game.GetEventPool().Add(gameEvent);
            SequenceRNG eventRandom = new SequenceRNG(intValues: new[] { 40, 70, 90 });
            GameEventCommands commands = new(
                game,
                eventRandom,
                new GameActionCommands(game, eventRandom)
            );
            GameEventObserver observer = new(commands);
            GameResultBus results = new();
            observer.Connect(results);
            game.CurrentTick = 100;
            commands.ProcessScheduledEvents(game.GetEventPool());
            Assert.AreEqual(
                140,
                game.EventRuntime.GetState(gameEvent.InstanceID)
                    .ScopedStates[planet.InstanceID]
                    .NextEligibleTick
            );
            planet.OwnerInstanceID = empire.InstanceID;

            results.Publish(
                new GameResult[]
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        PreviousOwner = alliance,
                        Tick = 110,
                    },
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        NewOwner = empire,
                        Tick = 120,
                    },
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        PreviousOwner = empire,
                        NewOwner = alliance,
                        Tick = 130,
                    },
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        PreviousOwner = alliance,
                        Tick = 140,
                    },
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        NewOwner = empire,
                        Tick = 150,
                    },
                }
            );

            Assert.AreEqual(
                240,
                game.EventRuntime.GetState(gameEvent.InstanceID)
                    .ScopedStates[planet.InstanceID]
                    .NextEligibleTick
            );
        }

        [Test]
        public void Dispose_ConnectedObserver_StopsTriggeredEvents()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameEvent gameEvent = CreateTriggeredEvent();
            game.GetEventPool().Add(gameEvent);
            var eventRandom = new FixedRNG();
            GameEventObserver observer = new(
                new GameEventCommands(game, eventRandom, new GameActionCommands(game, eventRandom))
            );
            GameResultBus results = new();
            observer.Connect(results);
            observer.Dispose();

            results.Publish(new DuelResult());

            Assert.AreEqual(0, game.EventRuntime.GetVariable("triggered-event-fired"));
            Assert.IsTrue(game.GetEventPool().Contains(gameEvent));
        }

        /// <summary>
        /// Creates a one-shot result-triggered event for observer boundary tests.
        /// </summary>
        /// <returns>The event definition.</returns>
        private static GameEvent CreateTriggeredEvent()
        {
            return new GameEvent
            {
                InstanceID = "triggered-event",
                MaximumActivations = 1,
                Triggers = new List<GameTrigger> { new DuelCompletedTrigger() },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "triggered-event-fired", Operand = 1 },
                },
            };
        }

        /// <summary>
        /// Creates a recurring event scoped to every owned planet.
        /// </summary>
        /// <returns>The scoped event definition.</returns>
        private static GameEvent CreateScopedEvent()
        {
            return new GameEvent
            {
                InstanceID = "scoped-event",
                ForEach = new GameEventScope
                {
                    As = "planet",
                    Selectors = new List<GameSelector> { new SelectPlanets { IsOwned = true } },
                },
                Schedule = new GameEventSchedule
                {
                    Every = new EveryTicks { Ticks = 30, InitialDelayTicks = 30 },
                },
            };
        }
    }
}
