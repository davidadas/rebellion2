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
            Assert.Throws<ArgumentNullException>(() => new GameEventObserver(null, null));
        }

        [Test]
        public void Constructor_NullPlanetaryControl_ThrowsArgumentNullException()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            var random = new FixedRNG();
            GameEventCommands commands = new GameEventCommands(
                game,
                random,
                new GameActionCommands(game, random)
            );

            Assert.Throws<ArgumentNullException>(() => new GameEventObserver(commands, null));
        }

        [Test]
        public void Connect_NullResultBus_ThrowsArgumentNullException()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            var eventRandom = new FixedRNG();
            GameEventObserver observer = CreateObserver(
                game,
                new GameEventCommands(game, eventRandom, new GameActionCommands(game, eventRandom))
            );

            Assert.Throws<ArgumentNullException>(() => observer.Connect(null));
        }

        [Test]
        public void Connect_AlreadyConnected_ThrowsInvalidOperationException()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            var eventRandom = new FixedRNG();
            GameEventObserver observer = CreateObserver(
                game,
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
            GameEventObserver observer = CreateObserver(
                game,
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
            GameEventObserver observer = CreateObserver(game, commands);
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
            GameEventObserver observer = CreateObserver(game, commands);
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
            GameEventObserver observer = CreateObserver(game, commands);
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
            GameEventObserver observer = CreateObserver(
                game,
                new GameEventCommands(game, eventRandom, new GameActionCommands(game, eventRandom))
            );
            GameResultBus results = new();
            observer.Connect(results);
            observer.Dispose();

            results.Publish(new DuelResult());

            Assert.AreEqual(0, game.EventRuntime.GetVariable("triggered-event-fired"));
            Assert.IsTrue(game.GetEventPool().Contains(gameEvent));
        }

        [Test]
        public void HandleResults_CompletedTargetChangesOwnership_PreservesCompletion()
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
            gameEvent.Schedule.Every.Until.Add(
                new IsOwnedConditional
                {
                    PlanetBinding = "planet",
                    FactionInstanceID = alliance.InstanceID,
                }
            );
            game.GetEventPool().Add(gameEvent);
            var random = new FixedRNG();
            GameEventCommands commands = new(game, random, new GameActionCommands(game, random));
            GameEventObserver observer = CreateObserver(game, commands);
            commands.ProcessScheduledEvents(game.GetEventPool());
            GameEventState completed = game
                .EventRuntime.GetState(gameEvent.InstanceID)
                .ScopedStates[planet.InstanceID];
            planet.OwnerInstanceID = empire.InstanceID;

            observer.HandleResults(
                new GameResult[]
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        PreviousOwner = alliance,
                        Tick = 1,
                    },
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        NewOwner = empire,
                        Tick = 2,
                    },
                }
            );
            game.CurrentTick = 100;
            commands.ProcessScheduledEvents(game.GetEventPool());

            Assert.AreSame(
                completed,
                game.EventRuntime.GetState(gameEvent.InstanceID).ScopedStates[planet.InstanceID]
            );
            Assert.IsTrue(completed.IsComplete);
            Assert.Zero(completed.ActivationCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Connect_ScheduledOwnershipChange_PreservesTargetStateOnPublication(
            bool randomInterval
        )
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet planet = new Planet { InstanceID = "planet", IsColonized = true };
            game.AttachNode(planet, sector);
            PlanetaryControlCommands ownership = CreateOwnershipCommands(game);
            SequenceRNG random = new SequenceRNG(intValues: new[] { 40, 70, 90 });
            GameActionCommands actions = new GameActionCommands(
                game,
                random,
                planetaryControlCommands: ownership
            );
            GameEventCommands commands = new GameEventCommands(game, random, actions);
            using GameEventObserver observer = new GameEventObserver(commands, ownership);
            GameResultBus results = new GameResultBus();
            observer.Connect(results);
            game.GetEventPool()
                .Add(
                    new GameEvent
                    {
                        InstanceID = "claim",
                        Schedule = new GameEventSchedule { At = new AtTick { Tick = 100 } },
                        Actions = new List<GameAction>
                        {
                            new ChangeOwnerAction
                            {
                                FactionInstanceID = faction.InstanceID,
                                Planets = new List<GameSelector>
                                {
                                    new SelectPlanets { InstanceID = planet.InstanceID },
                                },
                            },
                        },
                    }
                );
            GameEvent scoped = CreateScopedEvent();
            scoped.Schedule = randomInterval
                ? new GameEventSchedule
                {
                    RandomInterval = new RandomInterval { MinimumTicks = 30, MaximumTicks = 1000 },
                }
                : new GameEventSchedule { Every = new EveryTicks { Ticks = 10 } };
            game.GetEventPool().Add(scoped);
            game.CurrentTick = 100;

            List<GameResult> produced = commands.ProcessScheduledEvents(game.GetEventPool());
            GameEventState state = game.EventRuntime.GetState(scoped.InstanceID).ScopedStates[
                planet.InstanceID
            ];
            results.Publish(produced);

            Assert.AreSame(
                state,
                game.EventRuntime.GetState(scoped.InstanceID).ScopedStates[planet.InstanceID]
            );
            Assert.AreEqual(randomInterval ? 140 : 110, state.NextEligibleTick);
            Assert.AreEqual(randomInterval ? 0 : 1, state.ActivationCount);
        }

        [Test]
        public void Connect_ImmediateOwnershipChange_ArmsTargetBeforeScheduledPass()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet planet = new Planet { InstanceID = "planet", IsColonized = true };
            game.AttachNode(planet, sector);
            PlanetaryControlCommands ownership = CreateOwnershipCommands(game);
            var random = new FixedRNG();
            GameEventCommands commands = new GameEventCommands(
                game,
                random,
                new GameActionCommands(game, random)
            );
            using GameEventObserver observer = new GameEventObserver(commands, ownership);
            GameResultBus results = new GameResultBus();
            observer.Connect(results);
            GameEvent scoped = CreateScopedEvent();
            game.GetEventPool().Add(scoped);
            game.CurrentTick = 100;

            PlanetOwnershipChangedResult change = ownership.TransferPlanet(planet, faction);
            GameEventState state = game.EventRuntime.GetState(scoped.InstanceID).ScopedStates[
                planet.InstanceID
            ];
            game.CurrentTick = 110;
            commands.ProcessScheduledEvents(game.GetEventPool());
            results.Publish(change);

            Assert.AreSame(
                state,
                game.EventRuntime.GetState(scoped.InstanceID).ScopedStates[planet.InstanceID]
            );
            Assert.AreEqual(130, state.NextEligibleTick);
        }

        [Test]
        public void Connect_ImmediateLeaveAndReentry_PreservesLatestTargetOnPublication()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "planet",
                IsColonized = true,
                OwnerInstanceID = faction.InstanceID,
            };
            game.AttachNode(planet, sector);
            PlanetaryControlCommands ownership = CreateOwnershipCommands(game);
            var random = new FixedRNG();
            GameEventCommands commands = new GameEventCommands(
                game,
                random,
                new GameActionCommands(game, random)
            );
            using GameEventObserver observer = new GameEventObserver(commands, ownership);
            GameResultBus results = new GameResultBus();
            observer.Connect(results);
            GameEvent scoped = CreateScopedEvent();
            game.GetEventPool().Add(scoped);
            game.CurrentTick = 100;
            commands.ProcessScheduledEvents(game.GetEventPool());
            game.CurrentTick = 110;
            PlanetOwnershipChangedResult departure = ownership.ClearPlanetOwnership(planet);
            game.CurrentTick = 120;
            PlanetOwnershipChangedResult arrival = ownership.TransferPlanet(planet, faction);
            GameEventState state = game.EventRuntime.GetState(scoped.InstanceID).ScopedStates[
                planet.InstanceID
            ];
            game.CurrentTick = 125;
            commands.ProcessScheduledEvents(game.GetEventPool());

            results.Publish(new GameResult[] { departure, arrival });

            Assert.AreSame(
                state,
                game.EventRuntime.GetState(scoped.InstanceID).ScopedStates[planet.InstanceID]
            );
            Assert.AreEqual(150, state.NextEligibleTick);
        }

        [Test]
        public void Dispose_ConnectedObserver_StopsImmediateOwnershipSynchronization()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet planet = new Planet { InstanceID = "planet", IsColonized = true };
            game.AttachNode(planet, sector);
            PlanetaryControlCommands ownership = CreateOwnershipCommands(game);
            var random = new FixedRNG();
            GameEventCommands commands = new GameEventCommands(
                game,
                random,
                new GameActionCommands(game, random)
            );
            using GameEventObserver observer = new GameEventObserver(commands, ownership);
            observer.Connect(new GameResultBus());
            GameEvent scoped = CreateScopedEvent();
            game.GetEventPool().Add(scoped);
            observer.Dispose();

            ownership.TransferPlanet(planet, faction);

            Assert.IsEmpty(game.EventRuntime.GetState(scoped.InstanceID).ScopedStates);
        }

        /// <summary>
        /// Creates an observer with independent ownership operations for the supplied game.
        /// </summary>
        /// <param name="game">The game observed by both operations.</param>
        /// <param name="commands">The event operations to observe.</param>
        /// <returns>The event observer.</returns>
        private static GameEventObserver CreateObserver(
            GameRoot game,
            GameEventCommands commands
        ) => new GameEventObserver(commands, CreateOwnershipCommands(game));

        /// <summary>
        /// Creates the planetary ownership operations for observer tests.
        /// </summary>
        /// <param name="game">The game whose ownership transitions are observed.</param>
        /// <returns>The planetary ownership operations.</returns>
        private static PlanetaryControlCommands CreateOwnershipCommands(GameRoot game) =>
            new PlanetaryControlCommands(
                game,
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );

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
