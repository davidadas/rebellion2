using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;
using Rebellion.Util.Random;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class GameEventExecutorTests
    {
        private GameRoot _game;
        private GameEventExecutor _system;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _system = new GameEventExecutor(_game, new FixedRandomProvider(new[] { 0.5 }));
        }

        /// <summary>Verifies that an absolute schedule is not offset by the current campaign tick.</summary>
        [Test]
        public void ProcessEvents_AbsoluteSchedule_StoresAbsoluteTick()
        {
            _game.CurrentTick = 10;
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "absolute",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 25 } },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(25, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
        }

        /// <summary>Verifies that a fixed recurring schedule starts at its authored initial delay.</summary>
        [Test]
        public void ProcessEvents_FixedIntervalFirstActivation_UsesInitialDelay()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "fixed",
                Schedule = new GameEventSchedule
                {
                    Every = new EveryTicks { Ticks = 20, InitialDelayTicks = 5 },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(5, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
        }

        /// <summary>Verifies both endpoints of the authored first-activation delay.</summary>
        /// <param name="roll">The random fraction used to select the delay.</param>
        /// <param name="expected">The expected first eligible tick.</param>
        [TestCase(0.0, 10)]
        [TestCase(0.9999, 30)]
        public void ProcessEvents_RandomFirstActivation_UsesInclusiveRangeEndpoints(
            double roll,
            int expected
        )
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "random-delay",
                Schedule = new GameEventSchedule
                {
                    RandomDelay = new RandomDelay { MinimumTicks = 10, MaximumTicks = 30 },
                },
            };
            _game.GetEventPool().Add(gameEvent);
            GameEventExecutor executor = new GameEventExecutor(_game, new QueueRNG(roll));

            executor.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(
                expected,
                _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick
            );
        }

        /// <summary>Verifies both endpoints of the repeat delay measured from the activation tick.</summary>
        /// <param name="roll">The random fraction used to select the delay.</param>
        /// <param name="expected">The expected next eligible tick.</param>
        [TestCase(0.0, 50)]
        [TestCase(0.9999, 70)]
        public void ProcessEvents_RandomRepeat_UsesInclusiveRangeFromCurrentTick(
            double roll,
            int expected
        )
        {
            _game.CurrentTick = 40;
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "random-interval",
                Schedule = new GameEventSchedule
                {
                    RandomInterval = new RandomInterval { MinimumTicks = 10, MaximumTicks = 30 },
                },
            };
            _game.GetEventPool().Add(gameEvent);
            _game.EventRuntime.GetState(gameEvent.InstanceID).IsInitialized = true;
            GameEventExecutor executor = new GameEventExecutor(_game, new QueueRNG(roll));

            executor.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(
                expected,
                _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick
            );
        }

        /// <summary>
        /// Verifies deferred message templates observe mutations made by later authored actions.
        /// </summary>
        [Test]
        public void ProcessEvents_MessageBeforeRename_ResolvesTemplateAfterLaterAction()
        {
            (GameEvent gameEvent, Planet planet, Faction faction) = CreateMessageEvent();
            GameEventExecutor system = new GameEventExecutor(
                _game,
                _game.Random,
                messageCommands: new MessageCommands(_game, new MessageFactory(null))
            );

            List<GameResult> results = system.ProcessEvents(_game.GetEventPool());

            MessageDeliveredResult delivery = results.OfType<MessageDeliveredResult>().Single();
            Assert.AreEqual("After", delivery.Message.Title);
            Assert.AreSame(delivery.Message, faction.Messages[MessageType.Advice].Single());
        }

        /// <summary>
        /// Verifies missing deferred execution does not roll back earlier authored actions.
        /// </summary>
        [Test]
        public void ProcessEvents_MissingCommands_PreservesActionsWithoutRecordingActivation()
        {
            (GameEvent gameEvent, Planet planet, Faction _) = CreateMessageEvent();

            Assert.Throws<InvalidOperationException>(() =>
                _system.ProcessEvents(_game.GetEventPool())
            );

            Assert.AreEqual("After", planet.DisplayName);
            Assert.AreEqual(0, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
            Assert.Contains(gameEvent, _game.GetEventPool());
        }

        /// <summary>
        /// Verifies a failed deferred message does not suppress the next request or activation history.
        /// </summary>
        [Test]
        public void ProcessEvents_DeferredMessageFails_DeliversNextMessageAndRecordsActivation()
        {
            (GameEvent gameEvent, Planet planet, Faction faction) = CreateMessageEvent();
            gameEvent.Actions.Insert(
                0,
                new SendMessageAction
                {
                    RecipientFactionInstanceID = faction.InstanceID,
                    Subject = "{unknown}",
                }
            );
            GameEventExecutor system = new GameEventExecutor(
                _game,
                _game.Random,
                messageCommands: new MessageCommands(_game, new MessageFactory(null))
            );
            LogAssert.Expect(
                LogType.Error,
                new Regex("Event 'MESSAGE_ORDER' deferred action 'SendMessageAction' failed:")
            );

            List<GameResult> results = system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(
                "After",
                results.OfType<MessageDeliveredResult>().Single().Message.Title
            );
            Assert.AreEqual(1, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        /// <summary>Verifies that one missing command does not suppress later configured operations.</summary>
        [Test]
        public void ProcessEvents_MissingDuelCommands_DeliversFollowingMessage()
        {
            (GameRoot game, GameEvent gameEvent, Officer _, Officer _) = CreateDeferredDuelEvent();
            GameEventExecutor executor = new GameEventExecutor(
                game,
                game.Random,
                messageCommands: new MessageCommands(game, new MessageFactory(null))
            );
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Event 'deferred-duel' deferred action 'TriggerDuelAction' failed: System.InvalidOperationException:"
                )
            );

            List<GameResult> results = executor.ProcessEvents(game.GetEventPool());

            Assert.AreEqual(
                "Following",
                results.OfType<MessageDeliveredResult>().Single().Message.Title
            );
            Assert.AreEqual(1, game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        /// <summary>Verifies that a failing deferred operation retains mutations but does not release its unfinished facts.</summary>
        [Test]
        public void ProcessEvents_DuelFailsAfterCapture_RetainsCaptureWithoutPublishingPartialResults()
        {
            (GameRoot game, GameEvent gameEvent, Officer encountered, Officer opposing) =
                CreateDeferredDuelEvent();
            GameEventExecutor executor = new GameEventExecutor(
                game,
                game.Random,
                duelCommands: new DuelCommands(game, new CaptureThenThrowRandom()),
                messageCommands: new MessageCommands(game, new MessageFactory(null))
            );
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Event 'deferred-duel' deferred action 'TriggerDuelAction' failed: System.InvalidOperationException:"
                )
            );

            List<GameResult> results = executor.ProcessEvents(game.GetEventPool());

            Assert.IsTrue(encountered.IsCaptured);
            Assert.AreEqual(opposing.OwnerInstanceID, encountered.CaptorInstanceID);
            Assert.IsInstanceOf<MessageDeliveredResult>(results.Single());
            Assert.AreEqual(gameEvent.InstanceID, results.Single().SourceEventInstanceID);
        }

        /// <summary>Verifies that a later capture invalidates an earlier queued duel before it consumes randomness.</summary>
        [Test]
        public void ProcessEvents_DuelBeforeCapture_SkipsDeferredDuelWithoutRolling()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer first = EntityFactory.CreateOfficer("first", "empire");
            Officer second = EntityFactory.CreateOfficer("second", "rebels");
            game.AttachNode(first, planet);
            second.IsCaptured = true;
            game.AttachNode(second, planet);
            second.IsCaptured = false;
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "capture-before-resolution",
                Actions = new List<GameAction>
                {
                    new TriggerDuelAction
                    {
                        FirstOfficerInstanceID = first.InstanceID,
                        SecondOfficerInstanceID = second.InstanceID,
                    },
                    new SetCaptureStatusAction
                    {
                        OfficerInstanceID = first.InstanceID,
                        IsCaptured = true,
                        CaptorFactionInstanceID = second.OwnerInstanceID,
                    },
                },
            };
            GameEventExecutor executor = new GameEventExecutor(
                game,
                game.Random,
                duelCommands: new DuelCommands(game, new ThrowingRNG())
            );

            List<GameResult> results = executor.ProcessEvents(new List<GameEvent> { gameEvent });

            Assert.IsInstanceOf<OfficerCaptureStateResult>(results.Single());
            Assert.AreEqual(gameEvent.InstanceID, results.Single().SourceEventInstanceID);
        }

        /// <summary>Verifies that deferred ownership keeps the selection made before a later action disables the officer.</summary>
        [Test]
        public void ProcessEvents_OwnershipBeforeDisabling_TransfersPreviouslySelectedOfficer()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("selected", "empire");
            game.AttachNode(officer, planet);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "selected-before-disabled",
                Actions = new List<GameAction>
                {
                    new ChangeOwnerAction
                    {
                        FactionInstanceID = "rebels",
                        Units = new List<GameEventSelector>
                        {
                            new SelectOfficers { InstanceID = officer.InstanceID },
                        },
                    },
                    new SetNodeStateAction
                    {
                        InstanceID = officer.InstanceID,
                        State = SceneNodeState.Inactive,
                    },
                },
            };
            MovementCommands movement = CreateMovementCommands(game);
            PlanetaryControlCommands control = new PlanetaryControlCommands(
                game,
                movement,
                new ManufacturingCommands(
                    game,
                    new FleetCommands(game),
                    new ManufacturingQueries(game)
                ),
                new FogOfWarCommands(game),
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            GameEventExecutor executor = new GameEventExecutor(
                game,
                game.Random,
                planetaryControlCommands: control
            );

            List<GameResult> results = executor.ProcessEvents(new List<GameEvent> { gameEvent });

            Assert.IsFalse(officer.IsActive());
            Assert.AreEqual("rebels", officer.OwnerInstanceID);
            UnitOwnershipChangedResult ownership = results
                .OfType<UnitOwnershipChangedResult>()
                .Single();
            Assert.AreSame(officer, ownership.Unit);
            Assert.AreEqual(gameEvent.InstanceID, ownership.SourceEventInstanceID);
        }

        /// <summary>Verifies that local action results precede deferred results despite the reverse authored order.</summary>
        [Test]
        public void ProcessEvents_MessageBeforeLocalChange_ReturnsLocalFactBeforeDelivery()
        {
            (GameEvent gameEvent, Planet planet, Faction _) = CreateMessageEvent();
            gameEvent.Actions.Add(
                new ChangeRawResourceNodesAction
                {
                    PlanetInstanceID = planet.InstanceID,
                    Amount = 1,
                }
            );
            GameEventExecutor executor = new GameEventExecutor(
                _game,
                _game.Random,
                messageCommands: new MessageCommands(_game, new MessageFactory(null))
            );

            List<GameResult> results = executor.ProcessEvents(_game.GetEventPool());

            CollectionAssert.AreEqual(
                new[] { typeof(PlanetStatChangedResult), typeof(MessageDeliveredResult) },
                results.Select(result => result.GetType())
            );
        }

        /// <summary>Verifies that deferred placement finishes before a following transit order chooses its departure.</summary>
        [Test]
        public void ProcessEvents_PlacementBeforeTransit_UsesPlacedPlanetAsDeparture()
        {
            GameRoot game = BuildGame(out Planet origin, out _);
            Planet middle = new Planet
            {
                InstanceID = "middle",
                OwnerInstanceID = origin.OwnerInstanceID,
                IsColonized = true,
                PositionX = 100,
            };
            Planet destination = new Planet
            {
                InstanceID = "destination",
                OwnerInstanceID = origin.OwnerInstanceID,
                IsColonized = true,
                PositionX = 200,
            };
            game.AttachNode(middle, origin.GetParent());
            game.AttachNode(destination, origin.GetParent());
            Officer officer = EntityFactory.CreateOfficer("traveler", origin.OwnerInstanceID);
            game.AttachNode(officer, origin);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "place-then-send",
                Actions = new List<GameAction>
                {
                    new PlaceUnitsAction
                    {
                        UnitInstanceID = officer.InstanceID,
                        DestinationInstanceID = middle.InstanceID,
                    },
                    new SendUnitsAction
                    {
                        UnitInstanceID = officer.InstanceID,
                        DestinationInstanceID = destination.InstanceID,
                    },
                },
            };
            MovementCommands movement = CreateMovementCommands(game);
            GameEventExecutor executor = new GameEventExecutor(
                game,
                game.Random,
                movementCommands: movement
            );

            executor.ProcessEvents(new List<GameEvent> { gameEvent });

            Assert.AreSame(destination, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
            Assert.AreEqual(middle.GetPosition(), officer.Movement.OriginPosition);
            Assert.AreEqual(gameEvent.InstanceID, officer.Movement.SourceEventInstanceID);
        }

        /// <summary>
        /// Verifies multiple schedule modes throws invalid operation exception.
        /// </summary>
        [Test]
        public void ValidateEvents_MultipleScheduleModes_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "INVALID_SCHEDULE",
                Schedule = new GameEventSchedule
                {
                    At = new AtTick { Tick = 25 },
                    Every = new EveryTicks { Ticks = 5 },
                },
            };

            TestDelegate validate = () => _system.ValidateEvents(new[] { gameEvent });

            Assert.Throws<InvalidOperationException>(validate);
        }

        /// <summary>
        /// Verifies one shot schedule without maximum activations does not throw.
        /// </summary>
        [Test]
        public void ValidateEvents_OneShotScheduleWithoutMaximumActivations_DoesNotThrow()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "ONE_SHOT",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 25 } },
            };

            Assert.DoesNotThrow(() => _system.ValidateEvents(new[] { gameEvent }));
        }

        /// <summary>
        /// Verifies duplicate binding alias throws invalid operation exception.
        /// </summary>
        [Test]
        public void ValidateEvents_DuplicateBindingAlias_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "INVALID_BINDING",
                Triggers = new List<GameEventTrigger>
                {
                    new UnitArrivedTrigger { Bindings = TriggerBindings(("Unit", "target")) },
                },
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "target",
                        RollInteger = new RollInteger { Minimum = 1, Maximum = 1 },
                    },
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _system.ValidateEvents(new[] { gameEvent })
            );

            StringAssert.Contains("duplicate binding alias 'target'", exception.Message);
        }

        /// <summary>
        /// Verifies binding without alias throws invalid operation exception.
        /// </summary>
        [Test]
        public void ValidateEvents_BindingWithoutAlias_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "INVALID_BINDING",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 1 } },
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        RollInteger = new RollInteger { Minimum = 1, Maximum = 1 },
                    },
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _system.ValidateEvents(new[] { gameEvent })
            );

            StringAssert.Contains("missing alias", exception.Message);
        }

        /// <summary>
        /// Verifies binding without source throws invalid operation exception.
        /// </summary>
        [Test]
        public void ValidateEvents_BindingWithoutSource_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "INVALID_BINDING",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 1 } },
                Bindings = new List<GameEventBinding> { new GameEventBinding { As = "target" } },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _system.ValidateEvents(new[] { gameEvent })
            );

            StringAssert.Contains("requires exactly one source", exception.Message);
        }

        /// <summary>
        /// Verifies multiple triggers with different aliases throws invalid operation exception.
        /// </summary>
        [Test]
        public void ValidateEvents_MultipleTriggersWithDifferentAliases_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "MULTI_TRIGGER",
                Triggers = new List<GameEventTrigger>
                {
                    new UnitArrivedTrigger { Bindings = TriggerBindings(("Unit", "result")) },
                    new DuelCompletedTrigger(),
                },
            };
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _system.ValidateEvents(new[] { gameEvent })
            );

            StringAssert.Contains("same trigger bindings and value types", exception.Message);
        }

        /// <summary>
        /// Verifies multiple filtered triggers with same alias does not throw.
        /// </summary>
        [Test]
        public void ValidateEvents_MultipleFilteredTriggersWithSameAlias_DoesNotThrow()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "MULTI_TRIGGER",
                Triggers = new List<GameEventTrigger>
                {
                    new UnitArrivedTrigger
                    {
                        UnitInstanceID = "first",
                        Bindings = TriggerBindings(("Unit", "arrival")),
                    },
                    new UnitArrivedTrigger
                    {
                        UnitInstanceID = "second",
                        Bindings = TriggerBindings(("Unit", "arrival")),
                    },
                },
            };

            Assert.DoesNotThrow(() => _system.ValidateEvents(new[] { gameEvent }));
        }

        /// <summary>
        /// Verifies dependency completed and removed from pool does not throw.
        /// </summary>
        [Test]
        public void ValidateEvents_DependencyCompletedAndRemovedFromPool_DoesNotThrow()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "FOLLOW_UP",
                Schedule = new GameEventSchedule
                {
                    After = new AfterEvent { EventInstanceID = "COMPLETED_EVENT", DelayTicks = 10 },
                },
            };
            _game.EventRuntime.GetState("COMPLETED_EVENT").IsComplete = true;

            Assert.DoesNotThrow(() => _system.ValidateEvents(new[] { gameEvent }));
        }

        /// <summary>
        /// Verifies dependency missing from pool and not completed throws invalid operation exception.
        /// </summary>
        [Test]
        public void ValidateEvents_DependencyMissingFromPoolAndNotCompleted_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "FOLLOW_UP",
                Schedule = new GameEventSchedule
                {
                    After = new AfterEvent { EventInstanceID = "UNKNOWN_EVENT", DelayTicks = 10 },
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _system.ValidateEvents(new[] { gameEvent })
            );

            StringAssert.Contains("references unknown event 'UNKNOWN_EVENT'", exception.Message);
        }

        /// <summary>
        /// Verifies unmet one shot event remains pending.
        /// </summary>
        [Test]
        public void ProcessEvents_UnmetOneShotEvent_RemainsPending()
        {
            GameEvent gameEvent = CreateTickEvent("PENDING", targetTick: 10, repeatable: false);
            _game.CurrentTick = 9;
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessEvents(_game.GetEventPool());

            Assert.Contains(gameEvent, _game.GetEventPool().ToList());
            Assert.IsFalse(_game.EventRuntime.GetState(gameEvent.InstanceID).IsComplete);
        }

        /// <summary>
        /// Verifies met one shot event completes and leaves pool.
        /// </summary>
        [Test]
        public void ProcessEvents_MetOneShotEvent_CompletesAndLeavesPool()
        {
            GameEvent gameEvent = CreateTickEvent("ONE_SHOT", targetTick: 10, repeatable: false);
            _game.CurrentTick = 11;
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessEvents(_game.GetEventPool());

            Assert.IsFalse(_game.GetEventPool().Contains(gameEvent));
            Assert.IsTrue(_game.EventRuntime.GetState(gameEvent.InstanceID).IsComplete);
        }

        /// <summary>
        /// Verifies met repeatable event completes and remains active.
        /// </summary>
        [Test]
        public void ProcessEvents_MetRepeatableEvent_CompletesAndRemainsActive()
        {
            GameEvent gameEvent = CreateTickEvent("REPEATABLE", targetTick: 10, repeatable: true);
            _game.CurrentTick = 11;
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessEvents(_game.GetEventPool());

            Assert.Contains(gameEvent, _game.GetEventPool().ToList());
            Assert.IsFalse(_game.EventRuntime.GetState(gameEvent.InstanceID).IsComplete);
        }

        /// <summary>
        /// Verifies recurring schedule until met completes and removes event.
        /// </summary>
        [Test]
        public void ProcessEvents_RecurringScheduleUntilMet_CompletesAndRemovesEvent()
        {
            GameEvent gameEvent = CreateTickEvent("UNTIL_MET", targetTick: 0, repeatable: true);
            gameEvent.Schedule = new GameEventSchedule
            {
                Every = new EveryTicks
                {
                    Ticks = 5,
                    Until = new List<GameConditional>
                    {
                        new TickCountConditional
                        {
                            Comparison = ComparisonOperator.GreaterThanOrEqual,
                            Ticks = 10,
                        },
                    },
                },
            };
            _game.CurrentTick = 10;
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(0, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
            Assert.IsTrue(_game.EventRuntime.GetState(gameEvent.InstanceID).IsComplete);
            Assert.IsFalse(_game.GetEventPool().Contains(gameEvent));
        }

        /// <summary>
        /// Verifies recurring schedule until met uses evaluation binding.
        /// </summary>
        [Test]
        public void ProcessEvents_RecurringScheduleUntilMet_UsesEvaluationBinding()
        {
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet" };
            _game.AttachNode(sector, _game.Galaxy);
            _game.AttachNode(planet, sector);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "BOUND_UNTIL",
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "planet",
                        Selectors = new List<GameEventSelector>
                        {
                            new SelectPlanets { InstanceID = planet.InstanceID },
                        },
                    },
                    new GameEventBinding
                    {
                        As = "rawResourceNodes",
                        Sources = new List<GameEventBindingSource>
                        {
                            new PlanetStatBindingSource
                            {
                                PlanetBinding = "planet",
                                Stat = PlanetStat.RawResourceNodes,
                            },
                        },
                    },
                },
                Schedule = new GameEventSchedule
                {
                    Every = new EveryTicks
                    {
                        Ticks = 5,
                        Until = new List<GameConditional>
                        {
                            new EvaluateBindingConditional
                            {
                                Binding = "rawResourceNodes",
                                Comparison = ComparisonOperator.Equal,
                                CompareTo = "0",
                            },
                        },
                    },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(0, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
            Assert.IsTrue(_game.EventRuntime.GetState(gameEvent.InstanceID).IsComplete);
            Assert.IsFalse(_game.GetEventPool().Contains(gameEvent));
        }

        /// <summary>
        /// Verifies maximum activations five activates five times.
        /// </summary>
        [Test]
        public void ProcessEvents_MaximumActivationsFive_ActivatesFiveTimes()
        {
            GameEvent gameEvent = CreateTickEvent("FIVE_RUNS", targetTick: 0, repeatable: false);
            gameEvent.MaximumActivations = 5;
            _game.CurrentTick = 1;
            _game.GetEventPool().Add(gameEvent);

            for (int iteration = 0; iteration < 6; iteration++)
                _system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(5, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        /// <summary>
        /// Verifies maximum activations three activates three times.
        /// </summary>
        [Test]
        public void ProcessEvents_MaximumActivationsThree_ActivatesThreeTimes()
        {
            GameEvent gameEvent = CreateTickEvent("THREE_RUNS", targetTick: 0, repeatable: false);
            gameEvent.MaximumActivations = 3;
            _game.CurrentTick = 1;
            _game.GetEventPool().Add(gameEvent);

            for (int iteration = 0; iteration < 4; iteration++)
                _system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(3, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        /// <summary>
        /// Verifies random delay waits until rolled absolute tick.
        /// </summary>
        [Test]
        public void ProcessEvents_RandomDelay_WaitsUntilRolledAbsoluteTick()
        {
            GameEvent gameEvent = CreateTickEvent("DELAYED", targetTick: 0, repeatable: false);
            gameEvent.MaximumActivations = null;
            gameEvent.Schedule = new GameEventSchedule
            {
                RandomDelay = new RandomDelay { MinimumTicks = 10, MaximumTicks = 14 },
            };
            _game.GetEventPool().Add(gameEvent);

            _game.CurrentTick = 11;
            _system.ProcessEvents(_game.GetEventPool());
            Assert.Contains(gameEvent, _game.GetEventPool().ToList());

            _game.CurrentTick = 12;
            _system.ProcessEvents(_game.GetEventPool());
            Assert.IsFalse(_game.GetEventPool().Contains(gameEvent));
            Assert.AreEqual(
                12,
                _game.EventRuntime.GetState(gameEvent.InstanceID).LastActivationTick
            );
        }

        /// <summary>
        /// Verifies repeat delay prevents activation until cooldown expires.
        /// </summary>
        [Test]
        public void ProcessEvents_RepeatDelay_PreventsActivationUntilCooldownExpires()
        {
            GameEvent gameEvent = CreateTickEvent("COOLDOWN", targetTick: 0, repeatable: true);
            gameEvent.Schedule = new GameEventSchedule { Every = new EveryTicks { Ticks = 5 } };
            _game.GetEventPool().Add(gameEvent);

            _game.CurrentTick = 1;
            _system.ProcessEvents(_game.GetEventPool());
            _game.CurrentTick = 5;
            _system.ProcessEvents(_game.GetEventPool());
            Assert.AreEqual(1, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);

            _game.CurrentTick = 6;
            _system.ProcessEvents(_game.GetEventPool());
            Assert.AreEqual(2, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        /// <summary>
        /// Verifies after schedule delays from predecessor activation.
        /// </summary>
        [Test]
        public void ProcessEvents_AfterSchedule_DelaysFromPredecessorActivation()
        {
            GameEvent predecessor = CreateTickEvent("DEPARTURE", targetTick: 19, repeatable: false);
            GameEvent pending = CreateTickEvent("PENDING_RETURN", targetTick: 0, repeatable: false);
            pending.Schedule = new GameEventSchedule
            {
                After = new AfterEvent { EventInstanceID = predecessor.InstanceID, DelayTicks = 5 },
            };
            _game.GetEventPool().Add(predecessor);
            _game.GetEventPool().Add(pending);
            _game.CurrentTick = 20;
            _system.ProcessEvents(_game.GetEventPool());

            _game.CurrentTick = 24;
            _system.ProcessEvents(_game.GetEventPool());
            Assert.Contains(pending, _game.GetEventPool());

            _game.CurrentTick = 25;
            _system.ProcessEvents(_game.GetEventPool());
            Assert.IsFalse(_game.GetEventPool().Contains(pending));
        }

        /// <summary>
        /// Verifies after all schedule before final delay keeps event pending.
        /// </summary>
        [Test]
        public void ProcessEvents_AfterAllScheduleBeforeFinalDelay_KeepsEventPending()
        {
            GameEvent pending = CreateDependentEvent("AFTER_ALL", afterAll: true);
            _game.GetEventPool().Add(pending);

            _game.CurrentTick = 24;
            _system.ProcessEvents(_game.GetEventPool());

            Assert.Contains(pending, _game.GetEventPool());
        }

        /// <summary>
        /// Verifies after all schedule at final delay activates event.
        /// </summary>
        [Test]
        public void ProcessEvents_AfterAllScheduleAtFinalDelay_ActivatesEvent()
        {
            GameEvent pending = CreateDependentEvent("AFTER_ALL", afterAll: true);
            _game.GetEventPool().Add(pending);

            _game.CurrentTick = 25;
            _system.ProcessEvents(_game.GetEventPool());

            Assert.IsFalse(_game.GetEventPool().Contains(pending));
        }

        /// <summary>
        /// Verifies after any schedule before first delay keeps event pending.
        /// </summary>
        [Test]
        public void ProcessEvents_AfterAnyScheduleBeforeFirstDelay_KeepsEventPending()
        {
            GameEvent pending = CreateDependentEvent("AFTER_ANY", afterAll: false);
            _game.GetEventPool().Add(pending);

            _game.CurrentTick = 14;
            _system.ProcessEvents(_game.GetEventPool());

            Assert.Contains(pending, _game.GetEventPool());
        }

        /// <summary>
        /// Verifies after any schedule at first delay activates event.
        /// </summary>
        [Test]
        public void ProcessEvents_AfterAnyScheduleAtFirstDelay_ActivatesEvent()
        {
            GameEvent pending = CreateDependentEvent("AFTER_ANY", afterAll: false);
            _game.GetEventPool().Add(pending);

            _game.CurrentTick = 15;
            _system.ProcessEvents(_game.GetEventPool());

            Assert.IsFalse(_game.GetEventPool().Contains(pending));
        }

        /// <summary>
        /// Verifies result triggered event does not run during scheduled polling.
        /// </summary>
        [Test]
        public void ProcessEvents_ResultTriggeredEvent_DoesNotRunDuringScheduledPolling()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "RESULT_ONLY",
                Triggers = new List<GameEventTrigger> { new DuelCompletedTrigger() },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "unexpected", Operand = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessEvents(_game.GetEventPool());

            Assert.Zero(_game.EventRuntime.GetVariable("unexpected"));
            Assert.Contains(gameEvent, _game.GetEventPool().ToList());
        }

        /// <summary>
        /// Verifies targeted planet uses one persisted schedule.
        /// </summary>
        [Test]
        public void ProcessEvents_TargetedPlanet_UsesOnePersistedSchedule()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "alliance" });
            _game.GetFactions().Add(new Faction { InstanceID = "empire" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            Planet first = new Planet { InstanceID = "first" };
            Planet second = new Planet { InstanceID = "second" };
            _game.AttachNode(first, sector);
            _game.AttachNode(second, sector);
            first.OwnerInstanceID = "alliance";
            second.OwnerInstanceID = "empire";
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "SCOPED",

                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "target",
                        Selectors = new List<GameEventSelector>
                        {
                            new SelectPlanets { InstanceID = first.InstanceID },
                        },
                    },
                },
                Conditionals = new List<GameConditional>
                {
                    new IsOwnedConditional { PlanetBinding = "target" },
                },
                Schedule = new GameEventSchedule
                {
                    Every = new EveryTicks { Ticks = 20, InitialDelayTicks = 10 },
                },
                Actions = new List<GameAction>
                {
                    new ChangeRawResourceNodesAction { PlanetBinding = "target", Amount = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _game.CurrentTick = 0;
            _system.ProcessEvents(_game.GetEventPool());
            Assert.AreEqual(10, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
            Assert.AreEqual(10, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);

            _game.CurrentTick = 10;
            _system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(1, first.NumRawResourceNodes);
            Assert.Zero(second.NumRawResourceNodes);
            Assert.AreEqual(30, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
            Assert.AreEqual(30, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
        }

        /// <summary>
        /// Verifies each owned planet target arms when neutral planet becomes owned.
        /// </summary>
        [Test]
        public void ProcessEvents_EachOwnedPlanetTarget_ArmsWhenNeutralPlanetBecomesOwned()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "alliance" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            Planet planet = new Planet { InstanceID = "planet" };
            _game.AttachNode(planet, sector);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "OWNED_ONLY",

                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "target",
                        Selectors = new List<GameEventSelector> { new SelectPlanets() },
                    },
                },
                Conditionals = new List<GameConditional>
                {
                    new IsOwnedConditional { PlanetBinding = "target" },
                },
                Schedule = new GameEventSchedule
                {
                    Every = new EveryTicks { Ticks = 30, InitialDelayTicks = 30 },
                },
                Actions = new List<GameAction>
                {
                    new ChangeRawResourceNodesAction { PlanetBinding = "target", Amount = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _game.CurrentTick = 100;
            _system.ProcessEvents(_game.GetEventPool());
            Assert.IsTrue(_game.EventRuntime.GetState(gameEvent.InstanceID).IsInitialized);

            planet.OwnerInstanceID = "alliance";
            _game.CurrentTick = 120;
            _system.ProcessEvents(_game.GetEventPool());

            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            Assert.AreEqual(150, state.NextEligibleTick);
            Assert.AreEqual(1, state.ActivationCount);
        }

        /// <summary>
        /// Verifies each owned planet target rearms after neutral interval.
        /// </summary>
        [Test]
        public void ProcessEvents_EachOwnedPlanetTarget_RearmsAfterNeutralInterval()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "alliance" });
            _game.GetFactions().Add(new Faction { InstanceID = "empire" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            Planet planet = new Planet { InstanceID = "planet" };
            _game.AttachNode(planet, sector);
            planet.OwnerInstanceID = "alliance";
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "OWNED_ONLY",

                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "target",
                        Selectors = new List<GameEventSelector> { new SelectPlanets() },
                    },
                },
                Conditionals = new List<GameConditional>
                {
                    new IsOwnedConditional { PlanetBinding = "target" },
                },
                Schedule = new GameEventSchedule
                {
                    Every = new EveryTicks { Ticks = 30, InitialDelayTicks = 30 },
                },
                Actions = new List<GameAction>
                {
                    new ChangeRawResourceNodesAction { PlanetBinding = "target", Amount = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _game.CurrentTick = 100;
            _system.ProcessEvents(_game.GetEventPool());
            planet.OwnerInstanceID = null;
            _game.CurrentTick = 110;
            _system.ProcessEvents(_game.GetEventPool());
            planet.OwnerInstanceID = "empire";
            _game.CurrentTick = 120;
            _system.ProcessEvents(_game.GetEventPool());

            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            Assert.AreEqual(130, state.NextEligibleTick);
            Assert.AreEqual(1, state.ActivationCount);
        }

        /// <summary>
        /// Verifies one shot target activates target once.
        /// </summary>
        [Test]
        public void ProcessEvents_OneShotTarget_ActivatesTargetOnce()
        {
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet" };
            _game.AttachNode(sector, _game.Galaxy);
            _game.AttachNode(planet, sector);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "ONE_SHOT_PER_PLANET",
                MaximumActivations = 1,
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "target",
                        Selectors = new List<GameEventSelector> { new SelectPlanets() },
                    },
                },
                Actions = new List<GameAction>
                {
                    new ChangeRawResourceNodesAction { PlanetBinding = "target", Amount = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessEvents(_game.GetEventPool());
            _system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(1, planet.NumRawResourceNodes);
        }

        /// <summary>
        /// Verifies random target before scheduled tick does not select target.
        /// </summary>
        [Test]
        public void ProcessEvents_RandomTargetBeforeScheduledTick_DoesNotSelectTarget()
        {
            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector",
                SectorType = PlanetSectorType.Core,
            };
            _game.AttachNode(sector, _game.Galaxy);
            _game.AttachNode(new Planet { InstanceID = "planet" }, sector);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "DELAYED_RANDOM_TARGET",
                MaximumActivations = 1,
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 10 } },
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "target",
                        Selectors = new List<GameEventSelector>
                        {
                            new SelectRandom
                            {
                                Count = 1,
                                Selectors = new List<GameEventSelector>
                                {
                                    new SelectPlanets { SectorType = PlanetSectorType.Core },
                                },
                            },
                        },
                    },
                },
            };
            _game.GetEventPool().Add(gameEvent);
            _game.CurrentTick = 9;

            _system.ProcessEvents(_game.GetEventPool());

            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            Assert.IsTrue(state.IsInitialized);
            Assert.AreEqual(10, state.NextEligibleTick);
        }

        /// <summary>
        /// Verifies matching encounter activates result triggered event once.
        /// </summary>
        [Test]
        public void HandleResults_MatchingEncounter_ActivatesResultTriggeredEventOnce()
        {
            Officer luke = new Officer { InstanceID = "luke" };
            Officer vader = new Officer { InstanceID = "vader" };
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "HERITAGE",
                MaximumActivations = 1,
                Triggers = EncounterTrigger(),
                Conditionals = new List<GameConditional>
                {
                    BindingEquals("firstOfficerInstanceID", luke.InstanceID),
                    BindingEquals("secondOfficerInstanceID", vader.InstanceID),
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "luke.heritage.revealed", Operand = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.HandleResults(
                new[]
                {
                    new DuelResult { EncounteredOfficer = luke, OpposingOfficer = vader },
                }
            );

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("luke.heritage.revealed"));
            Assert.IsFalse(_game.GetEventPool().Contains(gameEvent));
            Assert.AreEqual(1, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        /// <summary>
        /// Verifies stable trigger id activates without clr type name.
        /// </summary>
        [Test]
        public void HandleResults_StableTriggerId_ActivatesWithoutClrTypeName()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "ARRIVAL_REACTION",
                Triggers = new List<GameEventTrigger>
                {
                    new UnitArrivedTrigger
                    {
                        Bindings = TriggerBindings(
                            ("Unit", "arrivedUnit"),
                            ("Destination", "arrivalDestination")
                        ),
                    },
                },
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "arrivalRating",
                        Sources = new List<GameEventBindingSource>
                        {
                            new SkillRatingBindingSource
                            {
                                OfficerBinding = "arrivedUnit",
                                Rating = SkillRating.Combat,
                            },
                        },
                    },
                    new GameEventBinding
                    {
                        As = "arrivalResources",
                        Sources = new List<GameEventBindingSource>
                        {
                            new PlanetStatBindingSource
                            {
                                PlanetBinding = "arrivalDestination",
                                Stat = PlanetStat.RawResourceNodes,
                            },
                        },
                    },
                },
                Conditionals = new List<GameConditional>
                {
                    BindingEquals("arrivalRating", "40"),
                    BindingEquals("arrivalResources", "7"),
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "arrival.triggered", Operand = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);
            Planet destination = new Planet { InstanceID = "destination", NumRawResourceNodes = 7 };
            Officer officer = new Officer { InstanceID = "officer" };
            officer.SetBaseRating(SkillRating.Combat, 40);

            _system.HandleResults(
                new[]
                {
                    new UnitArrivedResult { Unit = officer, Destination = destination },
                }
            );

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("arrival.triggered"));
        }

        /// <summary>
        /// Verifies second unit arrived alternative matches activates once.
        /// </summary>
        [Test]
        public void HandleResults_SecondUnitArrivedAlternativeMatches_ActivatesOnce()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "ALTERNATE_ARRIVALS",
                Triggers = new List<GameEventTrigger>
                {
                    new UnitArrivedTrigger { UnitInstanceID = "first" },
                    new UnitArrivedTrigger { UnitInstanceID = "second" },
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "arrival.count", Operand = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.HandleResults(
                new[] { new UnitArrivedResult { Unit = new Officer { InstanceID = "second" } } }
            );

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("arrival.count"));
            Assert.AreEqual(1, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        /// <summary>
        /// Verifies matching optional source binding activates event.
        /// </summary>
        [Test]
        public void HandleResults_MatchingOptionalSourceBinding_ActivatesEvent()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "SOURCE_FILTERED_ARRIVAL",
                Triggers = new List<GameEventTrigger>
                {
                    new UnitArrivedTrigger { SourceEventInstanceID = "EXPECTED_SOURCE" },
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "source.arrival.triggered", Operand = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.HandleResults(
                new[] { new UnitArrivedResult { SourceEventInstanceID = "EXPECTED_SOURCE" } }
            );

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("source.arrival.triggered"));
        }

        /// <summary>
        /// Verifies without suppression preserves trigger and sibling messages.
        /// </summary>
        [Test]
        public void HandleResults_WithoutSuppression_PreservesTriggerAndSiblingMessages()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "HIDDEN_MISSION_REPORT",
                Triggers = new List<GameEventTrigger> { new MissionCompletedTrigger() },
            };
            _game.GetEventPool().Add(gameEvent);
            OfficerCaptureStateResult release = new OfficerCaptureStateResult
            {
                SourceEventInstanceID = "PALACE_RESCUE",
            };
            MissionCompletedResult completion = new MissionCompletedResult
            {
                SourceEventInstanceID = "PALACE_RESCUE",
            };

            List<GameResult> reactions = _system.HandleResults(
                new GameResult[] { release, completion }
            );

            Assert.IsEmpty(reactions);
        }

        /// <summary>
        /// Verifies repeatable encounter effect activates for every encounter.
        /// </summary>
        [Test]
        public void HandleResults_RepeatableEncounterEffect_ActivatesForEveryEncounter()
        {
            Officer luke = new Officer { InstanceID = "luke" };
            Officer vader = new Officer { InstanceID = "vader" };
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "RECURRING_ENCOUNTER_EFFECTS",

                Triggers = EncounterTrigger(),
                Conditionals = new List<GameConditional>
                {
                    BindingEquals("firstOfficerInstanceID", luke.InstanceID),
                    BindingEquals("secondOfficerInstanceID", vader.InstanceID),
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "encounter.count",
                        Operation = EventVariableOperation.Add,
                        Operand = 1,
                    },
                },
            };
            _game.GetEventPool().Add(gameEvent);
            DuelResult encounter = new DuelResult
            {
                EncounteredOfficer = luke,
                OpposingOfficer = vader,
            };

            _system.HandleResults(new[] { encounter });
            _system.HandleResults(new[] { encounter });

            Assert.Contains(gameEvent, _game.GetEventPool().ToList());
            Assert.AreEqual(2, _game.EventRuntime.GetVariable("encounter.count"));
            Assert.AreEqual(2, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        /// <summary>
        /// Verifies that reaching the activation limit prevents another activation.
        /// </summary>
        [Test]
        public void ProcessEvents_MaximumActivationsReached_DoesNotActivate()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "LIMITED",
                MaximumActivations = 3,
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "activated", Operand = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);
            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            state.ActivationCount = 3;

            _system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(0, _game.EventRuntime.GetVariable("activated"));
            Assert.AreEqual(3, state.ActivationCount);
        }

        /// <summary>
        /// Verifies that an unlimited event can activate after many previous activations.
        /// </summary>
        [Test]
        public void ProcessEvents_UnlimitedEvent_ActivatesAgain()
        {
            GameEvent gameEvent = new GameEvent { InstanceID = "UNLIMITED" };
            _game.GetEventPool().Add(gameEvent);
            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            state.ActivationCount = 100;

            _system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(101, state.ActivationCount);
        }

        /// <summary>Verifies that authored placement attaches the complete existing-and-spawned group.</summary>
        [Test]
        public void ProcessEvents_PlaceUnitsMixedSources_PlacesExistingAndSpawnedUnits()
        {
            GameRoot game = BuildGame(out Planet destination, out _);
            Officer officer = new Officer
            {
                InstanceID = "existing-officer",
                OwnerInstanceID = "empire",
            };
            game.AttachNode(officer, destination);
            Starfighter fighterTemplate = new Starfighter
            {
                TypeID = "X_WING",
                DisplayName = "X-Wing",
            };
            Regiment regimentTemplate = new Regiment
            {
                TypeID = "ALLIANCE_REGIMENT",
                DisplayName = "Alliance Regiment",
            };
            UnitFactory factory = new UnitFactory(
                Array.Empty<Building>(),
                Array.Empty<CapitalShip>(),
                new[] { fighterTemplate },
                new[] { regimentTemplate },
                Array.Empty<SpecialForces>()
            );
            PlaceUnitsAction action = new PlaceUnitsAction
            {
                DestinationInstanceID = destination.InstanceID,
                Units = new List<GameEventSelector>
                {
                    new SelectOfficers { InstanceID = officer.InstanceID },
                    new SpawnUnits
                    {
                        TypeID = "X_WING",
                        Count = 2,
                        OwnerFactionInstanceID = "empire",
                    },
                    new SpawnUnits
                    {
                        TypeID = "ALLIANCE_REGIMENT",
                        OwnerFactionInstanceID = "empire",
                    },
                },
            };

            RunAuthoredAction(game, action, unitFactory: factory);

            List<ISceneNode> units = destination
                .GetChildren<Officer>()
                .Cast<ISceneNode>()
                .Concat(destination.GetChildren<Starfighter>())
                .Concat(destination.GetChildren<Regiment>())
                .ToList();
            Assert.AreEqual(4, units.Count);
            Assert.AreSame(officer, units.OfType<Officer>().Single());
            Assert.AreEqual(2, units.OfType<Starfighter>().Count());
            Assert.AreEqual(1, units.OfType<Regiment>().Count());
            Assert.IsTrue(units.All(unit => unit.GetParent() == destination));
            Assert.IsTrue(units.All(unit => unit.OwnerInstanceID == "empire"));
        }

        /// <summary>Verifies execute action place units inactive existing unit throws invalid operation exception.</summary>
        [Test]
        public void ExecuteAction_PlaceUnitsInactiveExistingUnit_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out Planet destination, out _);
            Officer officer = new Officer
            {
                InstanceID = "inactive-officer",
                OwnerInstanceID = "empire",
                IsEnabled = false,
            };
            game.AttachNode(officer, destination);
            PlaceUnitsAction action = new PlaceUnitsAction
            {
                UnitInstanceID = officer.InstanceID,
                DestinationInstanceID = destination.InstanceID,
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("requires existing units to be active", exception.Message);
        }

        /// <summary>Verifies that authored ownership selects and transfers the intended unit.</summary>
        [Test]
        public void ProcessEvents_ChangeOwnerUnitSelectors_TransfersSelectedUnit()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = new Officer { InstanceID = "officer", OwnerInstanceID = "empire" };
            game.AttachNode(officer, planet);
            ChangeOwnerAction action = new ChangeOwnerAction
            {
                FactionInstanceID = "rebels",
                Units = new List<GameEventSelector>
                {
                    new SelectOfficers { InstanceID = officer.InstanceID },
                },
            };

            UnitOwnershipChangedResult result = RunAuthoredAction(game, action)
                .OfType<UnitOwnershipChangedResult>()
                .Single();

            Assert.AreEqual("rebels", result.NewOwner.InstanceID);
            Assert.AreSame(officer, result.Unit);
            Assert.AreEqual("rebels", officer.OwnerInstanceID);
        }

        /// <summary>Verifies execute action change owner with planets and units rejects ambiguous request.</summary>
        [Test]
        public void ExecuteAction_ChangeOwnerWithPlanetsAndUnits_RejectsAmbiguousRequest()
        {
            GameRoot game = BuildGame(out _, out _);
            ChangeOwnerAction action = new ChangeOwnerAction
            {
                FactionInstanceID = "rebels",
                Planets = new List<GameEventSelector> { new SelectPlanets() },
                Units = new List<GameEventSelector> { new SelectOfficers() },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("exactly one", exception.Message);
        }

        /// <summary>Verifies execute action set node state inactive disables officer without detaching it.</summary>
        [Test]
        public void ExecuteAction_SetNodeStateInactive_DisablesOfficerWithoutDetachingIt()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            game.AttachNode(officer, rebelPlanet);

            new SetNodeStateAction
            {
                InstanceID = officer.InstanceID,
                State = SceneNodeState.Inactive,
            }.Execute(game);

            Assert.AreSame(rebelPlanet, officer.GetParent());
            Assert.IsFalse(officer.IsActive());
        }

        /// <summary>Verifies execute action set node state inactive mission participant throws invalid operation exception.</summary>
        [Test]
        public void ExecuteAction_SetNodeStateInactiveMissionParticipant_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = "mission",
                OwnerInstanceID = "rebels",
                LocationInstanceID = rebelPlanet.InstanceID,
            };
            game.AttachNode(officer, rebelPlanet);
            game.AttachNode(mission, rebelPlanet);
            game.MoveNode(officer, mission);
            mission.Initiate(100);

            TestDelegate execute = () =>
                new SetNodeStateAction
                {
                    InstanceID = officer.InstanceID,
                    State = SceneNodeState.Inactive,
                }.Execute(game);

            Assert.Throws<InvalidOperationException>(execute);
            Assert.AreSame(mission, officer.GetParent());
            Assert.IsTrue(officer.IsActive());
        }

        /// <summary>Verifies execute action set node state selector disables every matching officer.</summary>
        [Test]
        public void ExecuteAction_SetNodeStateSelector_DisablesEveryMatchingOfficer()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer first = EntityFactory.CreateOfficer("first", "rebels");
            Officer second = EntityFactory.CreateOfficer("second", "rebels");
            game.AttachNode(first, rebelPlanet);
            game.AttachNode(second, rebelPlanet);

            new SetNodeStateAction
            {
                State = SceneNodeState.Inactive,
                Selectors = new List<GameEventSelector>
                {
                    new SelectOfficers { PlanetInstanceID = rebelPlanet.InstanceID },
                },
            }.Execute(game);

            Assert.IsFalse(first.IsActive());
            Assert.IsFalse(second.IsActive());
        }

        /// <summary>Verifies execute action set node state active enables officer at existing parent.</summary>
        [Test]
        public void ExecuteAction_SetNodeStateActive_EnablesOfficerAtExistingParent()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            game.AttachNode(officer, rebelPlanet);
            officer.IsEnabled = false;

            List<GameResult> results = new SetNodeStateAction
            {
                InstanceID = officer.InstanceID,
                State = SceneNodeState.Active,
            }.Execute(game);

            Assert.IsEmpty(results);
            Assert.AreSame(rebelPlanet, officer.GetParent());
            Assert.IsTrue(officer.IsActive());
        }

        /// <summary>Verifies execute action set node state planet disables non movable node.</summary>
        [Test]
        public void ExecuteAction_SetNodeStatePlanet_DisablesNonMovableNode()
        {
            GameRoot game = BuildGame(out _, out Planet planet);

            new SetNodeStateAction
            {
                InstanceID = planet.InstanceID,
                State = SceneNodeState.Inactive,
            }.Execute(game);

            Assert.IsFalse(planet.IsActive());
        }

        /// <summary>Verifies execute action set node state inactive officer selector enables matching officer.</summary>
        [Test]
        public void ExecuteAction_SetNodeStateInactiveOfficerSelector_EnablesMatchingOfficer()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            officer.IsCaptured = true;
            game.AttachNode(officer, rebelPlanet);
            officer.IsEnabled = false;
            SetNodeStateAction action = new SetNodeStateAction
            {
                State = SceneNodeState.Active,
                Selectors = new List<GameEventSelector>
                {
                    new SelectOfficers
                    {
                        PlanetInstanceID = rebelPlanet.InstanceID,
                        IsCaptured = true,
                        IncludeInactive = true,
                    },
                },
            };

            action.Execute(game);

            Assert.AreSame(rebelPlanet, officer.GetParent());
            Assert.IsTrue(officer.IsActive());
        }

        /// <summary>Verifies that resolving valid references does not bypass the duel's location check.</summary>
        [Test]
        public void ProcessEvents_DuelOfficersAtDifferentPlanets_ReturnsNoDuel()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer attacker = EntityFactory.CreateOfficer("a1", "empire");
            Officer defender = EntityFactory.CreateOfficer("d1", "rebels");
            attacker.ForceValue = 100;
            defender.ForceValue = 100;
            game.AttachNode(attacker, empirePlanet);
            game.AttachNode(defender, rebelPlanet);

            TriggerDuelAction action = new TriggerDuelAction
            {
                FirstOfficerInstanceID = "a1",
                SecondOfficerInstanceID = "d1",
            };

            List<GameResult> results = RunAuthoredAction(game, action, random: new ThrowingRNG());

            Assert.IsEmpty(results);
        }

        /// <summary>Verifies that the actual duel outcome retains the triggering participant's role.</summary>
        [Test]
        public void HandleResults_DuelSecondOfficerParticipated_ReversesAuthoredOrder()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            luke.ForceValue = 100;
            vader.ForceValue = 100;
            game.AttachNode(luke, rebelPlanet);
            vader.IsCaptured = true;
            game.AttachNode(vader, rebelPlanet);
            vader.IsCaptured = false;
            MissionCompletedResult completion = new MissionCompletedResult
            {
                Participants = new List<IMissionParticipant> { vader },
            };
            TriggerDuelAction action = new TriggerDuelAction
            {
                FirstOfficerInstanceID = "luke",
                SecondOfficerInstanceID = "vader",
                AudioPath = "encounter-voice",
            };
            DuelResult result = RunAuthoredAction(
                    game,
                    action,
                    triggerResult: completion,
                    trigger: new MissionCompletedTrigger()
                )
                .OfType<DuelResult>()
                .Single();

            Assert.AreSame(vader, result.EncounteredOfficer);
            Assert.AreSame(luke, result.OpposingOfficer);
            Assert.AreEqual("encounter-voice", result.AudioPath);
        }

        /// <summary>Verifies that an authored encounter resolves an eligible officer pair.</summary>
        [Test]
        public void ProcessEvents_DuelValidOfficers_ProducesDuelOutcome()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.ForceValue = 60;
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            vader.ForceValue = 60;
            luke.IsCaptured = true;
            game.AttachNode(luke, empirePlanet);
            luke.IsCaptured = false;
            game.AttachNode(vader, empirePlanet);
            TriggerDuelAction action = new TriggerDuelAction
            {
                FirstOfficerInstanceID = luke.InstanceID,
                SecondOfficerInstanceID = vader.InstanceID,
            };

            List<GameResult> results = RunAuthoredAction(game, action);

            Assert.AreEqual(1, results.OfType<DuelResult>().Count());
        }

        /// <summary>Verifies execute action reveal to faction selected officer emits concrete observation.</summary>
        [Test]
        public void ExecuteAction_RevealToFactionSelectedOfficer_EmitsConcreteObservation()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            game.AttachNode(officer, empirePlanet);
            RevealToFactionAction action = new RevealToFactionAction
            {
                FactionInstanceID = "rebels",
                Targets = new List<GameEventSelector>
                {
                    new SelectOfficers { InstanceID = officer.InstanceID },
                },
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent { InstanceID = "INFORMANTS" },
                new GameEventState()
            );

            List<GameResult> results = action.Execute(game, game.Random, context);

            IntelligenceRevealedResult intelligence = results
                .OfType<IntelligenceRevealedResult>()
                .Single();
            Assert.AreEqual("rebels", intelligence.Recipient.InstanceID);
            CollectionAssert.AreEqual(new[] { officer }, intelligence.Observations);
        }

        /// <summary>Verifies delivery of an authored message with explicit recipient emits resolved result.</summary>
        [Test]
        public void ProcessEvents_SendMessageExplicitRecipient_EmitsResolvedResult()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.DisplayName = "Luke Skywalker";
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
                MessageType = MessageType.Advice,
                Subject = "A message for {subject}",
                Body = "Report from {location}",
                BackgroundAudio = new MessageAudio { Path = "Audio/Luke/dialogue" },
            };

            MessageDeliveredResult delivery = RunAuthoredAction(game, action)
                .OfType<MessageDeliveredResult>()
                .Single();
            Message result = delivery.Message;

            Assert.AreEqual("rebels", delivery.Recipient.InstanceID);
            Assert.AreEqual(luke.InstanceID, result.NavigationTargetInstanceID);
            Assert.AreEqual(rebelPlanet.InstanceID, result.EventLocationInstanceID);
            Assert.AreEqual("Audio/Luke/dialogue", result.BackgroundAudioPath);
        }

        /// <summary>Verifies delivery of an authored message with officer subject does not include subject image by default.</summary>
        [Test]
        public void ProcessEvents_SendMessageOfficerSubject_DoesNotIncludeSubjectImageByDefault()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.MessageImagePath = "Officers/Luke/message";
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
            };

            MessageDeliveredResult delivery = RunAuthoredAction(game, action)
                .OfType<MessageDeliveredResult>()
                .Single();
            Message result = delivery.Message;

            Assert.IsNull(result.OverlayImagePath);
        }

        /// <summary>Verifies delivery of an authored message with show subject image includes officer message image.</summary>
        [Test]
        public void ProcessEvents_SendMessageShowSubjectImage_IncludesOfficerMessageImage()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.MessageImagePath = "Officers/Luke/message";
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
                ShowSubjectImage = true,
            };

            MessageDeliveredResult delivery = RunAuthoredAction(game, action)
                .OfType<MessageDeliveredResult>()
                .Single();
            Message result = delivery.Message;

            Assert.AreEqual("Officers/Luke/message", result.OverlayImagePath);
        }

        /// <summary>Verifies delivery of an authored message with explicit overlay image uses authored image.</summary>
        [Test]
        public void ProcessEvents_SendMessageExplicitOverlayImage_UsesAuthoredImage()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.MessageImagePath = "Officers/Luke/message";
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
                OverlayImage = new MessageImage { Path = "Story/portrait" },
            };

            MessageDeliveredResult delivery = RunAuthoredAction(game, action)
                .OfType<MessageDeliveredResult>()
                .Single();
            Message result = delivery.Message;

            Assert.AreEqual("Story/portrait", result.OverlayImagePath);
        }

        /// <summary>Verifies execute action send message recipient omitted throws exception.</summary>
        [Test]
        public void ExecuteAction_SendMessageRecipientOmitted_ThrowsException()
        {
            GameRoot game = BuildGame(out _, out _);
            SendMessageAction action = new SendMessageAction();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            Assert.AreEqual("SendMessage requires RecipientFactionInstanceID.", exception.Message);
        }

        /// <summary>Verifies delivery of an authored message with inactive subject emits resolved result.</summary>
        [Test]
        public void ProcessEvents_SendMessageInactiveSubject_EmitsResolvedResult()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            luke.IsEnabled = false;
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
                Subject = "Rescue failed",
                Body = "Luke remains captured.",
            };

            MessageDeliveredResult delivery = RunAuthoredAction(game, action)
                .OfType<MessageDeliveredResult>()
                .Single();
            Message result = delivery.Message;

            Assert.AreEqual("rebels", delivery.Recipient.InstanceID);
            Assert.AreEqual(luke.InstanceID, result.NavigationTargetInstanceID);
            Assert.AreEqual(rebelPlanet.InstanceID, result.EventLocationInstanceID);
        }

        /// <summary>Verifies delivery of an authored message with audio binding uses trigger binding path.</summary>
        [Test]
        public void HandleResults_SendMessageAudioBinding_UsesTriggerBindingPath()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
                BackgroundAudio = new MessageAudio { Binding = "audioPath" },
            };
            DuelResult encounter = new DuelResult
            {
                EncounteredOfficer = luke,
                AudioPath = "selected-encounter-voice",
            };
            DuelCompletedTrigger trigger = new DuelCompletedTrigger
            {
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding { Argument = "AudioPath", As = "audioPath" },
                },
            };

            MessageDeliveredResult delivery = RunAuthoredAction(
                    game,
                    action,
                    triggerResult: encounter,
                    trigger: trigger
                )
                .OfType<MessageDeliveredResult>()
                .Single();
            Message result = delivery.Message;

            Assert.AreEqual("selected-encounter-voice", result.BackgroundAudioPath);
        }

        /// <summary>Verifies delivery of an authored message with officer voice preset uses subject voice set.</summary>
        [Test]
        public void ProcessEvents_SendMessageOfficerVoicePreset_UsesSubjectVoiceSet()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.VoiceSet.MissionSuccessPaths.Add("luke-success");
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
                OfficerVoice = new MessageOfficerVoice
                {
                    Preset = OfficerVoiceLineType.MissionSuccess,
                },
            };

            MessageDeliveredResult delivery = RunAuthoredAction(
                    game,
                    action,
                    random: new FixedRNG(0)
                )
                .OfType<MessageDeliveredResult>()
                .Single();
            Message result = delivery.Message;

            Assert.AreEqual("luke-success", result.OfficerVoicePath);
        }

        /// <summary>Verifies execute action send message multiple background sources throws exception.</summary>
        [Test]
        public void ExecuteAction_SendMessageMultipleBackgroundSources_ThrowsException()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", rebelPlanet.OwnerInstanceID);
            game.AttachNode(officer, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = officer.InstanceID,
                BackgroundImage = new MessageBackgroundImage
                {
                    Key = "advice",
                    Path = "custom-background",
                },
            };

            Assert.Throws<InvalidOperationException>(() => action.Execute(game));
        }

        /// <summary>Verifies execute action if action event variable selects branch and persists mutation.</summary>
        [Test]
        public void ExecuteAction_IfActionEventVariable_SelectsBranchAndPersistsMutation()
        {
            GameRoot game = BuildGame(out _, out _);
            game.EventRuntime.SetVariable("luke.stage", 2);
            IfAction action = new IfAction
            {
                Conditionals = new List<GameConditional>
                {
                    new EvaluateEventVariableConditional
                    {
                        Key = "luke.stage",
                        Comparison = ComparisonOperator.GreaterThanOrEqual,
                        CompareTo = 2,
                    },
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "luke.stage",
                        Operation = EventVariableOperation.Add,
                        Operand = 1,
                    },
                },
                Else = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "wrong", Operand = 1 },
                },
            };

            List<GameResult> results = action.Execute(game, new FixedRandomProvider(new[] { 0d }));

            Assert.IsEmpty(results);
            Assert.AreEqual(3, game.EventRuntime.GetVariable("luke.stage"));
            Assert.AreEqual(0, game.EventRuntime.GetVariable("wrong"));
        }

        /// <summary>Verifies that interpreting movement alone does not mutate the unit before deferred execution.</summary>
        [Test]
        public void ExecuteAction_SendUnitsValidReferences_DefersMovementUntilActivationCompletes()
        {
            GameRoot game = BuildGame(out Planet destination, out Planet origin);
            Officer officer = EntityFactory.CreateOfficer("traveler", "rebels");
            game.AttachNode(officer, origin);
            SendUnitsAction action = new SendUnitsAction
            {
                UnitInstanceID = officer.InstanceID,
                DestinationInstanceID = destination.InstanceID,
            };

            action.Execute(game);

            Assert.AreSame(origin, officer.GetParent());
            Assert.IsNull(officer.Movement);
        }

        /// <summary>Verifies execute action send units incompatible selector throws precise error.</summary>
        [Test]
        public void ExecuteAction_SendUnitsIncompatibleSelector_ThrowsPreciseError()
        {
            GameRoot game = BuildGame(out Planet destination, out _);
            SendUnitsAction action = new SendUnitsAction
            {
                DestinationInstanceID = destination.InstanceID,
                Units = new List<GameEventSelector>
                {
                    new SelectPlanets { InstanceID = destination.InstanceID },
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("only movable units", exception.Message);
        }

        /// <summary>Verifies that authored destination fallback tries the next candidate after rejection.</summary>
        [Test]
        public void ProcessEvents_SendUnitsFirstDestinationRejected_UsesNextCandidate()
        {
            GameRoot game = BuildGame(out Planet first, out Planet origin);
            Planet second = new Planet
            {
                InstanceID = "second",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(second, first.GetParent());
            Officer officer = EntityFactory.CreateOfficer("traveler", "rebels");
            game.AttachNode(officer, origin);
            SendUnitsAction action = new SendUnitsAction
            {
                UnitInstanceID = officer.InstanceID,
                Destination = new List<GameEventSelector>
                {
                    new SelectFirst
                    {
                        Selectors = new List<GameEventSelector>
                        {
                            new SelectPlanets { InstanceID = first.InstanceID },
                            new SelectPlanets { InstanceID = second.InstanceID },
                        },
                    },
                },
            };

            RunAuthoredAction(game, action);

            Assert.AreSame(second, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
        }

        /// <summary>Verifies execute action set capture status incompatible selector throws precise error.</summary>
        [Test]
        public void ExecuteAction_SetCaptureStatusIncompatibleSelector_ThrowsPreciseError()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                IsCaptured = true,
                CaptorFactionInstanceID = "rebels",
                Selectors = new List<GameEventSelector>
                {
                    new SelectPlanets { InstanceID = planet.InstanceID },
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("only officers", exception.Message);
        }

        /// <summary>Verifies execute action set capture status normal capture allows escape.</summary>
        [Test]
        public void ExecuteAction_SetCaptureStatusNormalCapture_AllowsEscape()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            game.AttachNode(officer, planet);
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = true,
                CaptorFactionInstanceID = "empire",
            };

            OfficerCaptureStateResult result = action
                .Execute(game)
                .OfType<OfficerCaptureStateResult>()
                .Single();

            Assert.IsTrue(officer.IsCaptured);
            Assert.AreEqual("empire", officer.CaptorInstanceID);
            Assert.IsTrue(officer.CanEscape);
            Assert.AreSame(officer, result.TargetOfficer);
        }

        /// <summary>Verifies execute action set capture status authored non escaping capture disables escape.</summary>
        [Test]
        public void ExecuteAction_SetCaptureStatusAuthoredNonEscapingCapture_DisablesEscape()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            game.AttachNode(officer, planet);
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = true,
                CaptorFactionInstanceID = "empire",
                CanEscape = false,
            };

            action.Execute(game);

            Assert.IsFalse(officer.CanEscape);
        }

        /// <summary>Verifies execute action set capture status release clears captor and capture only state.</summary>
        [Test]
        public void ExecuteAction_SetCaptureStatusRelease_ClearsCaptorAndCaptureOnlyState()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "empire";
            officer.CanEscape = false;
            game.AttachNode(officer, planet);
            officer.IsEnabled = false;
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = false,
            };

            action.Execute(game);

            Assert.IsFalse(officer.IsCaptured);
            Assert.IsNull(officer.CaptorInstanceID);
            Assert.IsTrue(officer.CanEscape);
        }

        /// <summary>Verifies execute action set capture status recapture after release restores default escape state.</summary>
        [Test]
        public void ExecuteAction_SetCaptureStatusRecaptureAfterRelease_RestoresDefaultEscapeState()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "empire";
            officer.CanEscape = false;
            game.AttachNode(officer, planet);
            new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = false,
            }.Execute(game);

            new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = true,
                CaptorFactionInstanceID = "empire",
            }.Execute(game);

            Assert.IsTrue(officer.IsCaptured);
            Assert.IsTrue(officer.CanEscape);
        }

        /// <summary>Verifies execute action set display name capital ship marks name as assigned.</summary>
        [Test]
        public void ExecuteAction_SetDisplayNameCapitalShip_MarksNameAsAssigned()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Fleet fleet = new Fleet
            {
                InstanceID = "fleet",
                OwnerInstanceID = planet.OwnerInstanceID,
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                DisplayName = "Generic Ship",
                OwnerInstanceID = planet.OwnerInstanceID,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            SetDisplayNameAction action = new SetDisplayNameAction
            {
                TargetInstanceID = ship.InstanceID,
                Name = "Named Ship",
            };

            action.Execute(game);

            Assert.AreEqual("Named Ship", ship.DisplayName);
            Assert.IsTrue(ship.HasAssignedName);
        }

        /// <summary>Verifies execute action set officer images configured values updates officer.</summary>
        [Test]
        public void ExecuteAction_SetOfficerImagesConfiguredValues_UpdatesOfficer()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            SetOfficerImagesAction action = new SetOfficerImagesAction
            {
                OfficerInstanceID = luke.InstanceID,
                DisplayImagePath = "jedi-display",
                SmallDisplayImagePath = "jedi-small-display",
                MessageImagePath = "jedi-message",
                EncyclopediaImagePath = "jedi-encyclopedia",
            };

            Assert.IsEmpty(action.Execute(game));

            Assert.AreEqual("jedi-display", luke.DisplayImagePath);
            Assert.AreEqual("jedi-small-display", luke.SmallDisplayImagePath);
            Assert.AreEqual("jedi-message", luke.MessageImagePath);
            Assert.AreEqual("jedi-encyclopedia", luke.EncyclopediaImagePath);
        }

        /// <summary>Verifies execute action set officer voice set configured values replaces selected voice pools.</summary>
        [Test]
        public void ExecuteAction_SetOfficerVoiceSetConfiguredValues_ReplacesSelectedVoicePools()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.VoiceSet.PersonnelArrivedPaths.Add("old");
            game.AttachNode(luke, rebelPlanet);
            SetOfficerVoiceSetAction action = new SetOfficerVoiceSetAction
            {
                OfficerInstanceID = luke.InstanceID,
                PersonnelArrived = new List<string> { "jedi-arrived" },
            };

            Assert.IsEmpty(action.Execute(game));

            CollectionAssert.AreEqual(
                new[] { "jedi-arrived" },
                luke.VoiceSet.PersonnelArrivedPaths
            );
        }

        /// <summary>Verifies execute action increase force rank percent of effective rank adjusts force rating.</summary>
        [Test]
        public void ExecuteAction_IncreaseForceRankPercentOfEffectiveRank_AdjustsForceRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.ForceValue = 40;
            game.AttachNode(luke, rebelPlanet);
            IncreaseForceRankAction action = new IncreaseForceRankAction
            {
                OfficerInstanceID = luke.InstanceID,
                PercentOfEffective = 25,
            };

            List<GameResult> results = action.Execute(game);

            Assert.IsEmpty(results);
            Assert.AreEqual(50, luke.ForceValue);
        }

        /// <summary>Verifies execute action change officer rating amount adjusts stored rating.</summary>
        [Test]
        public void ExecuteAction_ChangeOfficerRatingAmount_AdjustsStoredRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(SkillRating.Diplomacy, 40);
            game.AttachNode(luke, rebelPlanet);

            Assert.IsEmpty(
                new ChangeOfficerRatingAction
                {
                    OfficerInstanceID = luke.InstanceID,
                    Rating = SkillRating.Diplomacy,
                    Amount = 5,
                }.Execute(game)
            );

            Assert.AreEqual(45, luke.GetBaseRating(SkillRating.Diplomacy));
        }

        /// <summary>Verifies execute action change officer rating percent of stored rating adjusts stored rating.</summary>
        [Test]
        public void ExecuteAction_ChangeOfficerRatingPercentOfStoredRating_AdjustsStoredRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(SkillRating.ShipResearch, 40);
            game.AttachNode(luke, rebelPlanet);

            new ChangeOfficerRatingAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = SkillRating.ShipResearch,
                PercentOfStored = -25,
            }.Execute(game);

            Assert.AreEqual(30, luke.GetBaseRating(SkillRating.ShipResearch));
        }

        /// <summary>Verifies execute action change officer rating multiple adjustment modes throws.</summary>
        [Test]
        public void ExecuteAction_ChangeOfficerRatingMultipleAdjustmentModes_Throws()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);

            Assert.Throws<InvalidOperationException>(() =>
                new ChangeOfficerRatingAction
                {
                    OfficerInstanceID = luke.InstanceID,
                    Rating = SkillRating.Combat,
                    Amount = 5,
                    PercentOfStored = 10,
                }.Execute(game)
            );
        }

        /// <summary>Verifies execute action perform skill check successful roll executes success actions.</summary>
        [Test]
        public void ExecuteAction_PerformSkillCheckSuccessfulRoll_ExecutesSuccessActions()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(SkillRating.Combat, 50);
            game.AttachNode(luke, rebelPlanet);
            luke.IsEnabled = false;
            game.Config.ProbabilityTables.Mission.Rescue = new Dictionary<int, int> { [50] = 60 };
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = SkillRating.Combat,
                ProbabilityTable = RescueMission.MissionTypeID,
                OnSuccess = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "result",
                        Operation = EventVariableOperation.Set,
                        Operand = 1,
                    },
                },
                OnFailure = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "result",
                        Operation = EventVariableOperation.Set,
                        Operand = -1,
                    },
                },
            };

            action.Execute(game, new FixedRNG(0.59));

            Assert.AreEqual(1, game.EventRuntime.GetVariable("result"));
        }

        /// <summary>Verifies execute action perform skill check failed roll executes failure actions.</summary>
        [Test]
        public void ExecuteAction_PerformSkillCheckFailedRoll_ExecutesFailureActions()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(SkillRating.Combat, 50);
            game.AttachNode(luke, rebelPlanet);
            game.Config.ProbabilityTables.Mission.Rescue = new Dictionary<int, int> { [50] = 60 };
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = SkillRating.Combat,
                ProbabilityTable = RescueMission.MissionTypeID,
                OnSuccess = new List<GameAction>(),
                OnFailure = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "result",
                        Operation = EventVariableOperation.Set,
                        Operand = -1,
                    },
                },
            };

            action.Execute(game, new FixedRNG(0.60));

            Assert.AreEqual(-1, game.EventRuntime.GetVariable("result"));
        }

        /// <summary>Verifies execute action perform skill check injured officer uses effective rating.</summary>
        [Test]
        public void ExecuteAction_PerformSkillCheckInjuredOfficer_UsesEffectiveRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(SkillRating.Combat, 50);
            luke.InjuryPoints = 20;
            game.AttachNode(luke, rebelPlanet);
            game.Config.ProbabilityTables.Mission.Rescue = new Dictionary<int, int>
            {
                [0] = 0,
                [50] = 100,
            };
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = SkillRating.Combat,
                ProbabilityTable = RescueMission.MissionTypeID,
                OnFailure = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "failed", Operand = 1 },
                },
            };

            action.Execute(game, new FixedRNG(0.5));

            Assert.AreEqual(1, game.EventRuntime.GetVariable("failed"));
        }

        /// <summary>Verifies execute action perform skill check negative rating multiplier uses scaled score.</summary>
        [Test]
        public void ExecuteAction_PerformSkillCheckNegativeRatingMultiplier_UsesScaledScore()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            han.SetBaseRating(SkillRating.Combat, 50);
            game.AttachNode(han, rebelPlanet);
            game.Config.ProbabilityTables.Mission.Abduction = new Dictionary<int, int>
            {
                [-51] = 0,
                [-50] = 100,
            };
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = han.InstanceID,
                Rating = SkillRating.Combat,
                ProbabilityTable = AbductionMission.MissionTypeID,
                RatingMultiplier = -1,
                OnSuccess = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "succeeded", Operand = 1 },
                },
            };

            List<GameResult> results = action.Execute(game, new FixedRNG(0.99));

            Assert.AreEqual(1, game.EventRuntime.GetVariable("succeeded"));
            Assert.IsEmpty(results);
        }

        /// <summary>Verifies execute action perform skill check missing officer throws invalid operation exception.</summary>
        [Test]
        public void ExecuteAction_PerformSkillCheckMissingOfficer_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out _);
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = "missing",
                Rating = SkillRating.Combat,
                ProbabilityTable = RescueMission.MissionTypeID,
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("could not resolve officer", exception.Message);
        }

        /// <summary>Verifies execute action perform skill check missing probability table throws invalid operation exception.</summary>
        [Test]
        public void ExecuteAction_PerformSkillCheckMissingProbabilityTable_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = SkillRating.Combat,
                ProbabilityTable = "missing",
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("could not resolve probability table", exception.Message);
        }

        /// <summary>Verifies execute action set force eligible eligibility transition initializes force once.</summary>
        [Test]
        public void ExecuteAction_SetForceEligibleEligibilityTransition_InitializesForceOnce()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer leia = EntityFactory.CreateOfficer("leia", "rebels");
            leia.IsForceSensitive = false;
            leia.IsForceEligible = false;
            leia.JediLevel = 10;
            leia.JediLevelVariance = 5;
            game.AttachNode(leia, rebelPlanet);
            SetForceSensitiveAction sensitivity = new SetForceSensitiveAction
            {
                OfficerInstanceID = leia.InstanceID,
            };
            SetForceEligibleAction eligibility = new SetForceEligibleAction
            {
                OfficerInstanceID = leia.InstanceID,
            };

            sensitivity.Execute(game);
            List<GameResult> results = eligibility.Execute(
                game,
                new FixedRandomProvider(new[] { 0.5 })
            );

            Assert.IsTrue(leia.IsForceSensitive);
            Assert.IsTrue(leia.IsForceEligible);
            Assert.AreEqual(13, leia.ForceValue);
            Assert.IsEmpty(results);
            eligibility.Execute(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(13, leia.ForceValue);
        }

        /// <summary>Verifies execute action apply officer injury inclusive range applies rolled severity.</summary>
        [Test]
        public void ExecuteAction_ApplyOfficerInjuryInclusiveRange_AppliesRolledSeverity()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            ApplyOfficerInjuryAction action = new ApplyOfficerInjuryAction
            {
                OfficerInstanceID = luke.InstanceID,
                MinimumInjury = 1,
                MaximumInjury = 100,
            };

            OfficerInjuredResult result = action
                .Execute(game, new FixedRandomProvider(new[] { 0.49 }))
                .OfType<OfficerInjuredResult>()
                .Single();

            Assert.AreEqual(50, result.Severity);
            Assert.AreEqual(50, luke.InjuryPoints);
        }

        /// <summary>Verifies execute action change raw resource nodes default increases explicit amount.</summary>
        [Test]
        public void ExecuteAction_ChangeRawResourceNodesDefault_IncreasesExplicitAmount()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            ChangeRawResourceNodesAction action = new ChangeRawResourceNodesAction
            {
                PlanetBinding = "target",
                Amount = 1,
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null
            );
            context.Bind("target", planet);

            List<GameResult> results = action.Execute(game, new SequenceRNG(), context);

            Assert.AreEqual(5, planet.NumRawResourceNodes);
            Assert.AreEqual(
                PlanetChangeCategory.RawMaterial,
                results.OfType<PlanetStatChangedResult>().Single().Category
            );
        }

        /// <summary>Verifies execute action change raw resource nodes neutral planet reports no faction.</summary>
        [Test]
        public void ExecuteAction_ChangeRawResourceNodesNeutralPlanet_ReportsNoFaction()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.OwnerInstanceID = null;
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            ChangeRawResourceNodesAction action = new ChangeRawResourceNodesAction
            {
                PlanetBinding = "target",
                Amount = 1,
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null
            );
            context.Bind("target", planet);

            PlanetStatChangedResult result = action
                .Execute(game, new SequenceRNG(), context)
                .OfType<PlanetStatChangedResult>()
                .Single();

            Assert.IsNull(result.Faction);
            Assert.AreEqual(5, planet.NumRawResourceNodes);
        }

        /// <summary>Verifies execute action change raw resource nodes bound amount applies reused integer.</summary>
        [Test]
        public void ExecuteAction_ChangeRawResourceNodesBoundAmount_AppliesReusedInteger()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 4;
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null
            );
            context.Bind("change", -2);
            ChangeRawResourceNodesAction action = new ChangeRawResourceNodesAction
            {
                PlanetInstanceID = planet.InstanceID,
                AmountBinding = "change",
            };

            action.Execute(game, new ThrowingRNG(), context);

            Assert.AreEqual(2, planet.NumRawResourceNodes);
        }

        /// <summary>Verifies execute action change energy capacity rolled amount applies inclusive integer roll.</summary>
        [Test]
        public void ExecuteAction_ChangeEnergyCapacityRolledAmount_AppliesInclusiveIntegerRoll()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.EnergyCapacity = 8;
            ChangeEnergyCapacityAction action = new ChangeEnergyCapacityAction
            {
                PlanetInstanceID = planet.InstanceID,
                RollInteger = new RollInteger { Minimum = -3, Maximum = -1 },
            };

            action.Execute(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(6, planet.EnergyCapacity);
        }

        /// <summary>Verifies execute action change popular support increase rebalances other faction.</summary>
        [Test]
        public void ExecuteAction_ChangePopularSupportIncrease_RebalancesOtherFaction()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.SetPopularSupport("empire", 60);
            planet.SetPopularSupport("rebels", 40);
            ChangePopularSupportAction action = new ChangePopularSupportAction
            {
                PlanetInstanceID = planet.InstanceID,
                FactionInstanceID = "empire",
                Amount = 10,
            };

            List<GameResult> results = action.Execute(game);

            Assert.AreEqual(70, planet.GetPopularSupport("empire"));
            Assert.AreEqual(30, planet.GetPopularSupport("rebels"));
            Assert.AreEqual(2, results.OfType<PlanetStatChangedResult>().Count());
            Assert.IsTrue(
                results
                    .OfType<PlanetStatChangedResult>()
                    .All(result => result.Category == PlanetChangeCategory.Loyalty)
            );
        }

        /// <summary>Verifies execute action set popular support absolute value preserves unallocated support.</summary>
        [Test]
        public void ExecuteAction_SetPopularSupportAbsoluteValue_PreservesUnallocatedSupport()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.SetPopularSupport("empire", 60);
            planet.SetPopularSupport("rebels", 40);
            SetPopularSupportAction action = new SetPopularSupportAction
            {
                PlanetInstanceID = planet.InstanceID,
                FactionInstanceID = "rebels",
                Support = 20,
            };

            action.Execute(game);

            Assert.AreEqual(60, planet.GetPopularSupport("empire"));
            Assert.AreEqual(20, planet.GetPopularSupport("rebels"));
        }

        /// <summary>Verifies execute action damage planet resources minimum loss guarantees one point loss.</summary>
        [Test]
        public void ExecuteAction_DamagePlanetResourcesMinimumLoss_GuaranteesOnePointLoss()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 3;
            planet.EnergyCapacity = 3;
            DamagePlanetResourcesAction action = new DamagePlanetResourcesAction
            {
                PlanetBinding = "target",
                LossProbabilityPerResource = 0,
                MinimumTotalLoss = 1,
            };
            GameEvent gameEvent = new GameEvent { InstanceID = "disaster" };
            GameEventEvaluationContext context = new GameEventEvaluationContext(gameEvent, null);
            context.Bind("target", planet);

            List<GameResult> results = action.Execute(game, new FixedRNG(0.99), context);

            Assert.AreEqual(2, planet.NumRawResourceNodes);
            Assert.AreEqual(3, planet.EnergyCapacity);
            Assert.AreEqual(1, results.OfType<PlanetStatChangedResult>().Count());
        }

        /// <summary>Verifies execute action damage planet resources na nprobability with no resources throws invalid operation exception.</summary>
        [Test]
        public void ExecuteAction_DamagePlanetResourcesNaNProbabilityWithNoResources_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 0;
            planet.EnergyCapacity = 0;
            DamagePlanetResourcesAction action = new DamagePlanetResourcesAction
            {
                PlanetInstanceID = planet.InstanceID,
                LossProbabilityPerResource = double.NaN,
            };

            TestDelegate execute = () => action.Execute(game);

            Assert.Throws<InvalidOperationException>(execute);
        }

        /// <summary>Verifies execute action damage planet resources negative minimum loss with no resources throws invalid operation exception.</summary>
        [Test]
        public void ExecuteAction_DamagePlanetResourcesNegativeMinimumLossWithNoResources_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 0;
            planet.EnergyCapacity = 0;
            DamagePlanetResourcesAction action = new DamagePlanetResourcesAction
            {
                PlanetInstanceID = planet.InstanceID,
                LossProbabilityPerResource = 0.5,
                MinimumTotalLoss = -1,
            };

            TestDelegate execute = () => action.Execute(game);

            Assert.Throws<InvalidOperationException>(execute);
        }

        /// <summary>Verifies execute action roll chance rolled probability executes actions on success.</summary>
        [Test]
        public void ExecuteAction_RollChanceRolledProbability_ExecutesActionsOnSuccess()
        {
            GameRoot game = BuildGame(out _, out _);
            RollChanceAction action = new RollChanceAction
            {
                RollDouble = new RollDouble { Minimum = 0.7, Maximum = 0.8 },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "success", Operand = 1 },
                },
            };

            action.Execute(game, new SequenceRNG(doubleValues: new[] { 0.5, 0.6 }));

            Assert.AreEqual(1, game.EventRuntime.GetVariable("success"));
        }

        /// <summary>Verifies execute action roll chance na nprobability throws invalid operation exception.</summary>
        [Test]
        public void ExecuteAction_RollChanceNaNProbability_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out _);
            RollChanceAction action = new RollChanceAction { Probability = double.NaN };

            TestDelegate execute = () => action.Execute(game);

            Assert.Throws<InvalidOperationException>(execute);
        }

        /// <summary>Verifies execute action roll chance failed probability does not execute actions.</summary>
        [Test]
        public void ExecuteAction_RollChanceFailedProbability_DoesNotExecuteActions()
        {
            GameRoot game = BuildGame(out _, out _);
            RollChanceAction action = new RollChanceAction
            {
                Probability = 0.25,
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "failure", Operand = 1 },
                },
            };

            action.Execute(game, new SequenceRNG(doubleValues: new[] { 0.5 }));

            Assert.Zero(game.EventRuntime.GetVariable("failure"));
        }

        /// <summary>Verifies execute action roll outcome weighted selection executes every action in selected outcome.</summary>
        [Test]
        public void ExecuteAction_RollOutcomeWeightedSelection_ExecutesEveryActionInSelectedOutcome()
        {
            GameRoot game = BuildGame(out _, out _);
            RollOutcomeAction action = new RollOutcomeAction
            {
                Outcomes = new List<RandomOutcome>
                {
                    new RandomOutcome
                    {
                        Weight = 1,
                        Actions = new List<GameAction>
                        {
                            new SetEventVariableAction { Key = "wrong", Operand = 1 },
                        },
                    },
                    new RandomOutcome
                    {
                        Weight = 3,
                        Actions = new List<GameAction>
                        {
                            new SetEventVariableAction { Key = "first", Operand = 1 },
                            new SetEventVariableAction { Key = "second", Operand = 2 },
                        },
                    },
                },
            };

            action.Execute(game, new SequenceRNG(new[] { 3 }));

            Assert.Zero(game.EventRuntime.GetVariable("wrong"));
            Assert.AreEqual(1, game.EventRuntime.GetVariable("first"));
            Assert.AreEqual(2, game.EventRuntime.GetVariable("second"));
        }

        /// <summary>Verifies execute action destroy units selected unit deletes unit from game.</summary>
        [Test]
        public void ExecuteAction_DestroyUnitsSelectedUnit_DeletesUnitFromGame()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                OwnerInstanceID = planet.OwnerInstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, planet);
            DestroyUnitsAction action = new DestroyUnitsAction
            {
                Selectors = new List<GameEventSelector>
                {
                    new SelectRegiments { InstanceID = regiment.InstanceID },
                },
            };

            action.Execute(game, new FixedRNG(0), null);

            Assert.IsNull(regiment.GetParent());
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        /// <summary>Verifies execute action destroy units parent and child selected destroys subtree once.</summary>
        [Test]
        public void ExecuteAction_DestroyUnitsParentAndChildSelected_DestroysSubtreeOnce()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "empire" };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            DestroyUnitsAction action = new DestroyUnitsAction
            {
                Selectors = new List<GameEventSelector>
                {
                    new SelectFleets { InstanceID = fleet.InstanceID },
                    new SelectCapitalShips { InstanceID = ship.InstanceID },
                },
            };

            List<GameResult> results = action.Execute(game);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID));
            Assert.IsNull(game.GetSceneNodeByInstanceID<CapitalShip>(ship.InstanceID));
            CollectionAssert.AreEquivalent(
                new ISceneNode[] { fleet, ship },
                results.OfType<GameObjectDestroyedResult>().Select(result => result.DestroyedObject)
            );
        }

        /// <summary>
        /// Verifies that an invalid authored action does not suppress the surrounding actions.
        /// </summary>
        [Test]
        public void ExecuteActions_ActionThrows_ExecutesRemainingActions()
        {
            GameRoot game = new GameRoot();
            GameEvent gameEvent = new GameEvent { InstanceID = "test-event" };
            GameEventEvaluationContext evaluation = new GameEventEvaluationContext(
                gameEvent,
                new GameEventState()
            );
            GameActionContext context = new GameActionContext(game, game.Random, evaluation);
            SetEventVariableAction firstAction = new SetEventVariableAction
            {
                Key = "first",
                Operand = 1,
            };
            SetEventVariableAction finalAction = new SetEventVariableAction
            {
                Key = "last",
                Operand = 1,
            };
            List<GameAction> actions = new List<GameAction>
            {
                firstAction,
                new SetEventVariableAction { Key = "invalid" },
                finalAction,
            };
            LogAssert.Expect(
                LogType.Error,
                new Regex("Event 'test-event' action 'SetEventVariableAction' failed:")
            );

            GameEventExecutor.ExecuteActions(actions, context);

            Assert.AreEqual(1, game.EventRuntime.GetVariable("first"));
            Assert.AreEqual(1, game.EventRuntime.GetVariable("last"));
        }

        /// <summary>Verifies that bindings are evaluated even before the scheduled activation tick.</summary>
        [Test]
        public void ProcessEvents_FutureActivation_EvaluatesBindingRoll()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            QueueRNG random = new QueueRNG(0.25, 0.75);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "future",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 10 } },
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "count",
                        RollInteger = new RollInteger { Minimum = 0, Maximum = 3 },
                    },
                },
            };
            game.GetEventPool().Add(gameEvent);

            new GameEventExecutor(game, random).ProcessEvents(game.GetEventPool());

            Assert.AreEqual(0.75, random.NextDouble());
            Assert.Zero(game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        /// <summary>Verifies that a binding failure propagates rather than continuing to the next event.</summary>
        [Test]
        public void ProcessEvents_BindingThrows_DoesNotExecuteFollowingEvent()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            GameEvent invalid = new GameEvent
            {
                InstanceID = "invalid-binding",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 0 } },
                Bindings = new List<GameEventBinding> { new GameEventBinding { As = "missing" } },
            };
            GameEvent following = new GameEvent
            {
                InstanceID = "following",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 0 } },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "after", Operand = 1 },
                },
            };
            game.GetEventPool().Add(invalid);
            game.GetEventPool().Add(following);
            GameEventExecutor executor = new GameEventExecutor(game, game.Random);

            Assert.Throws<InvalidOperationException>(() =>
                executor.ProcessEvents(game.GetEventPool())
            );

            Assert.Zero(game.EventRuntime.GetVariable("after"));
            Assert.Zero(game.EventRuntime.GetState(invalid.InstanceID).ActivationCount);
        }

        /// <summary>Verifies that a null action is logged without suppressing the next action.</summary>
        [Test]
        public void ExecuteActions_NullAction_ExecutesFollowingAction()
        {
            GameRoot game = new GameRoot();
            GameActionContext context = new GameActionContext(game, game.Random);
            LogAssert.Expect(LogType.Error, new Regex("Event 'unknown' action 'null' failed:"));

            GameEventExecutor.ExecuteActions(
                new GameAction[]
                {
                    null,
                    new SetEventVariableAction { Key = "after", Operand = 1 },
                },
                context
            );

            Assert.AreEqual(1, game.EventRuntime.GetVariable("after"));
        }

        /// <summary>Verifies that a later target failure does not roll back an action's earlier mutation.</summary>
        [Test]
        public void ExecuteActions_LaterTargetThrows_PreservesEarlierMutation()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer idle = EntityFactory.CreateOfficer("idle", planet.OwnerInstanceID);
            Officer assigned = EntityFactory.CreateOfficer("assigned", planet.OwnerInstanceID);
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = "mission",
                OwnerInstanceID = planet.OwnerInstanceID,
                LocationInstanceID = planet.InstanceID,
            };
            game.AttachNode(idle, planet);
            game.AttachNode(mission, planet);
            game.AttachNode(assigned, mission);
            mission.Initiate(100);
            GameActionContext context = new GameActionContext(game, game.Random);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Event 'unknown' action 'SetNodeStateAction' failed:")
            );

            GameEventExecutor.ExecuteActions(
                new[]
                {
                    new SetNodeStateAction
                    {
                        State = SceneNodeState.Inactive,
                        Selectors = new List<GameEventSelector>
                        {
                            new SelectOfficers { InstanceID = idle.InstanceID },
                            new SelectOfficers { InstanceID = assigned.InstanceID },
                        },
                    },
                },
                context
            );

            Assert.IsFalse(idle.IsEnabled);
            Assert.IsTrue(assigned.IsEnabled);
        }

        /// <summary>
        /// Verifies that a nested action's result is available before the following action rolls.
        /// </summary>
        [Test]
        public void ExecuteActions_NestedActions_RecordsResultBeforeNextRoll()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet" };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            GameEvent gameEvent = new GameEvent { InstanceID = "nested" };
            GameEventEvaluationContext evaluation = new GameEventEvaluationContext(
                gameEvent,
                new GameEventState()
            );
            RecordingRandom provider = new RecordingRandom(evaluation);
            GameActionContext context = new GameActionContext(game, provider, evaluation);

            GameEventExecutor.ExecuteActions(
                new[]
                {
                    new IfAction
                    {
                        Actions = new List<GameAction>
                        {
                            new ChangeRawResourceNodesAction
                            {
                                PlanetInstanceID = planet.InstanceID,
                                Amount = 1,
                            },
                            new RollChanceAction { Probability = 0.5 },
                        },
                    },
                },
                context
            );

            Assert.AreEqual(1, provider.ResultsAtRoll);
        }

        /// <summary>Verifies that duplicate aliases fail after consuming the binding's random draw.</summary>
        [Test]
        public void Bind_DuplicateAlias_ConsumesRollBeforeRejectingAlias()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null
            );
            context.Bind("count", 42);
            QueueRNG random = new QueueRNG(0.25, 0.75);
            GameEventBinding binding = new GameEventBinding
            {
                As = "count",
                RollInteger = new RollInteger { Minimum = 0, Maximum = 3 },
            };

            Assert.Throws<InvalidOperationException>(() =>
                GameEventExecutor.Bind(binding, game, random, context)
            );

            Assert.AreEqual(0.75, random.NextDouble());
            Assert.AreEqual(42, context.GetBinding<int>("count"));
        }

        /// <summary>Verifies bind numeric ranges stores rolled values.</summary>
        [Test]
        public void Bind_NumericRanges_StoresRolledValues()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            GameEvent gameEvent = new GameEvent();
            GameEventEvaluationContext context = new GameEventEvaluationContext(gameEvent, null);
            GameEventBinding integerBinding = new GameEventBinding
            {
                As = "count",
                RollInteger = new RollInteger { Minimum = 1, Maximum = 5 },
            };
            GameEventBinding doubleBinding = new GameEventBinding
            {
                As = "probability",
                RollDouble = new RollDouble { Minimum = 0.1, Maximum = 0.9 },
            };
            IRandomNumberProvider random = new FixedRandomProvider(new[] { 0.5, 0.5 });
            GameEventExecutor.Bind(integerBinding, game, random, context);
            GameEventExecutor.Bind(doubleBinding, game, random, context);

            Assert.AreEqual(3, context.GetBinding<int>("count"));
            Assert.AreEqual(0.5, context.GetBinding<double>("probability"), 0.0001);
        }

        /// <summary>Verifies bind typed sources stores resolved values.</summary>
        [Test]
        public void Bind_TypedSources_StoresResolvedValues()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "faction" };
            game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
                NumRawResourceNodes = 7,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", faction.InstanceID);
            officer.SetBaseRating(SkillRating.Combat, 82);
            officer.ForceValue = 41;
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(officer, planet);
            int expectedCombatRating = officer.GetEffectiveRating(SkillRating.Combat);
            int expectedForceRank = officer.ForceRank;
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null
            );
            IRandomNumberProvider random = new FixedRandomProvider(new[] { 0.5 });
            GameEventExecutor.Bind(
                new GameEventBinding
                {
                    As = "combat",
                    Sources = new List<GameEventBindingSource>
                    {
                        new SkillRatingBindingSource
                        {
                            OfficerInstanceID = officer.InstanceID,
                            Rating = SkillRating.Combat,
                        },
                    },
                },
                game,
                random,
                context
            );
            GameEventExecutor.Bind(
                new GameEventBinding
                {
                    As = "force",
                    Sources = new List<GameEventBindingSource>
                    {
                        new OfficerForceBindingSource { OfficerInstanceID = officer.InstanceID },
                    },
                },
                game,
                random,
                context
            );
            GameEventExecutor.Bind(
                new GameEventBinding
                {
                    As = "resources",
                    Sources = new List<GameEventBindingSource>
                    {
                        new PlanetStatBindingSource
                        {
                            PlanetInstanceID = planet.InstanceID,
                            Stat = PlanetStat.RawResourceNodes,
                        },
                    },
                },
                game,
                random,
                context
            );
            GameEventExecutor.Bind(
                new GameEventBinding
                {
                    As = "officerCount",
                    Sources = new List<GameEventBindingSource>
                    {
                        new SelectionCountBindingSource
                        {
                            Selectors = new List<GameEventSelector>
                            {
                                new SelectOfficers { PlanetInstanceID = planet.InstanceID },
                            },
                        },
                    },
                },
                game,
                random,
                context
            );

            Assert.AreEqual(expectedCombatRating, context.GetBinding<int>("combat"));
            Assert.AreEqual(expectedForceRank, context.GetBinding<int>("force"));
            Assert.AreEqual(7, context.GetBinding<int>("resources"));
            Assert.AreEqual(1, context.GetBinding<int>("officerCount"));
        }

        /// <summary>Verifies bind typed officer sources with inactive officer stores resolved values.</summary>
        [Test]
        public void Bind_TypedOfficerSourcesWithInactiveOfficer_StoresResolvedValues()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "faction" };
            game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", faction.InstanceID);
            officer.SetBaseRating(SkillRating.Combat, 82);
            officer.ForceValue = 41;
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(officer, planet);
            officer.IsEnabled = false;
            int expectedCombatRating = officer.GetEffectiveRating(SkillRating.Combat);
            int expectedForceRank = officer.ForceRank;
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null
            );
            IRandomNumberProvider random = new FixedRandomProvider(new[] { 0.5 });
            GameEventExecutor.Bind(
                new GameEventBinding
                {
                    As = "combat",
                    Sources = new List<GameEventBindingSource>
                    {
                        new SkillRatingBindingSource
                        {
                            OfficerInstanceID = officer.InstanceID,
                            Rating = SkillRating.Combat,
                        },
                    },
                },
                game,
                random,
                context
            );
            GameEventExecutor.Bind(
                new GameEventBinding
                {
                    As = "force",
                    Sources = new List<GameEventBindingSource>
                    {
                        new OfficerForceBindingSource { OfficerInstanceID = officer.InstanceID },
                    },
                },
                game,
                random,
                context
            );

            Assert.AreEqual(expectedCombatRating, context.GetBinding<int>("combat"));
            Assert.AreEqual(expectedForceRank, context.GetBinding<int>("force"));
        }

        /// <summary>Verifies bind planet stat with inactive planet stores resolved value.</summary>
        [Test]
        public void Bind_PlanetStatWithInactivePlanet_StoresResolvedValue()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet", NumRawResourceNodes = 7 };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            planet.IsEnabled = false;
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null
            );
            GameEventBinding binding = new GameEventBinding
            {
                As = "resources",
                Sources = new List<GameEventBindingSource>
                {
                    new PlanetStatBindingSource
                    {
                        PlanetInstanceID = planet.InstanceID,
                        Stat = PlanetStat.RawResourceNodes,
                    },
                },
            };
            GameEventExecutor.Bind(binding, game, new FixedRandomProvider(new[] { 0.5 }), context);

            Assert.AreEqual(7, context.GetBinding<int>("resources"));
        }

        /// <summary>Verifies roll double extreme finite range returns finite value.</summary>
        [Test]
        public void Roll_ExtremeFiniteDoubleRange_ReturnsFiniteValue()
        {
            RollDouble roll = new RollDouble
            {
                Minimum = -double.MaxValue,
                Maximum = double.MaxValue,
            };

            double result = GameEventExecutor.Roll(roll, new FixedRNG(0.5));

            Assert.IsFalse(double.IsNaN(result));
            Assert.IsFalse(double.IsInfinity(result));
            Assert.AreEqual(0, result);
        }

        /// <summary>Verifies that short-circuiting compositions do not consume a later condition's random draw.</summary>
        /// <param name="operation">The authored composition to evaluate.</param>
        /// <param name="firstMatches">Whether the first condition matches.</param>
        [TestCase("All", false)]
        [TestCase("Any", true)]
        [TestCase("Not", true)]
        public void IsMet_DecisiveFirstOperand_DoesNotRollFollowingCondition(
            string operation,
            bool firstMatches
        )
        {
            GameRoot game = BuildConditionGame(out Planet planet, out _);
            planet.SetPopularSupport("empire", 50);
            game.Random = new QueueRNG(0.25, 0.75);
            List<GameConditional> conditions = new List<GameConditional>
            {
                new TickCountConditional { Ticks = firstMatches ? 0 : 1 },
                new RollAgainstPopularSupportConditional
                {
                    FactionInstanceID = "empire",
                    PlanetInstanceID = planet.InstanceID,
                },
            };
            GameConditional conditional = operation switch
            {
                "All" => new AllConditional { Conditionals = conditions },
                "Any" => new AnyConditional { Conditionals = conditions },
                "Not" => new NotConditional { Conditionals = conditions },
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
            GameEventExecutor.IsMet(conditional, game);

            Assert.AreEqual(0.25, game.Random.NextDouble());
        }

        /// <summary>Verifies that XOR evaluates every operand even after two conditions match.</summary>
        [Test]
        public void IsMet_XorAlreadyHasTwoMatches_StillRollsFollowingCondition()
        {
            GameRoot game = BuildConditionGame(out Planet planet, out _);
            planet.SetPopularSupport("empire", 50);
            game.Random = new QueueRNG(0.25, 0.75);
            XorConditional conditional = new XorConditional
            {
                Conditionals = new List<GameConditional>
                {
                    new TickCountConditional { Ticks = 0 },
                    new TickCountConditional { Ticks = 0 },
                    new RollAgainstPopularSupportConditional
                    {
                        FactionInstanceID = "empire",
                        PlanetInstanceID = planet.InstanceID,
                    },
                },
            };

            bool result = GameEventExecutor.IsMet(conditional, game);

            Assert.IsFalse(result);
            Assert.AreEqual(0.75, game.Random.NextDouble());
        }

        /// <summary>Verifies evaluate binding null binding returns false.</summary>
        [Test]
        public void IsMet_EvaluateBindingNullBinding_ReturnsFalse()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "sourceEventInstanceID",
                Comparison = ComparisonOperator.Equal,
                CompareTo = "expected-event",
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("sourceEventInstanceID", null);

            bool result = GameEventExecutor.IsMet(conditional, game, context);

            Assert.IsFalse(result);
        }

        /// <summary>Verifies evaluate binding object binding throws invalid operation exception.</summary>
        [Test]
        public void IsMet_EvaluateBindingObjectBinding_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildConditionGame(out Planet empirePlanet, out _);
            Officer emperor = EntityFactory.CreateOfficer("emperor", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "empire" };
            game.AttachNode(fleet, empirePlanet);
            game.AttachNode(ship, fleet);
            game.AttachNode(emperor, ship);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "unit",
                Comparison = ComparisonOperator.Equal,
                CompareTo = emperor.InstanceID,
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("unit", fleet);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                GameEventExecutor.IsMet(conditional, game, context)
            );

            StringAssert.Contains("cannot be compared", exception.Message);
        }

        /// <summary>Verifies evaluate binding different object binding throws invalid operation exception.</summary>
        [Test]
        public void IsMet_EvaluateBindingDifferentObjectBinding_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildConditionGame(out Planet empirePlanet, out Planet rebelPlanet);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "destination",
                Comparison = ComparisonOperator.Equal,
                CompareTo = empirePlanet.InstanceID,
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("destination", rebelPlanet);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                GameEventExecutor.IsMet(conditional, game, context)
            );

            StringAssert.Contains("cannot be compared", exception.Message);
        }

        /// <summary>Verifies evaluate binding compatible binding comparison returns true.</summary>
        [Test]
        public void IsMet_EvaluateBindingCompatibleBindingComparison_ReturnsTrue()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "first",
                Comparison = ComparisonOperator.GreaterThan,
                CompareToBinding = "second",
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("first", 80);
            context.Bind("second", 60);

            bool result = GameEventExecutor.IsMet(conditional, game, context);

            Assert.IsTrue(result);
        }

        /// <summary>Verifies evaluate binding incompatible binding comparison throws invalid operation exception.</summary>
        [Test]
        public void IsMet_EvaluateBindingIncompatibleBindingComparison_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "first",
                Comparison = ComparisonOperator.Equal,
                CompareToBinding = "second",
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("first", 80);
            context.Bind("second", "80");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                GameEventExecutor.IsMet(conditional, game, context)
            );

            StringAssert.Contains("incompatible value types", exception.Message);
        }

        /// <summary>Verifies evaluate binding enum literal comparison returns true.</summary>
        [Test]
        public void IsMet_EvaluateBindingEnumLiteralComparison_ReturnsTrue()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "comparison",
                Comparison = ComparisonOperator.Equal,
                CompareTo = "GreaterThan",
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("comparison", ComparisonOperator.GreaterThan);

            Assert.IsTrue(GameEventExecutor.IsMet(conditional, game, context));
        }

        /// <summary>Verifies evaluate binding enum and string bindings throws invalid operation exception.</summary>
        [Test]
        public void IsMet_EvaluateBindingEnumAndStringBindings_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "first",
                Comparison = ComparisonOperator.Equal,
                CompareToBinding = "second",
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("first", ComparisonOperator.GreaterThan);
            context.Bind("second", "GreaterThan");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                GameEventExecutor.IsMet(conditional, game, context)
            );

            StringAssert.Contains("incompatible value types", exception.Message);
        }

        /// <summary>Verifies evaluate binding ordered enum comparison throws invalid operation exception.</summary>
        [Test]
        public void IsMet_EvaluateBindingOrderedEnumComparison_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "comparison",
                Comparison = ComparisonOperator.GreaterThan,
                CompareTo = "Equal",
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("comparison", ComparisonOperator.GreaterThan);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                GameEventExecutor.IsMet(conditional, game, context)
            );

            StringAssert.Contains("ordered comparisons only for numeric values", exception.Message);
        }

        /// <summary>Verifies evaluate binding null optional binding uses predicate semantics.</summary>
        /// <param name="comparison">The authored comparison operator.</param>
        /// <param name="expected">The expected match result.</param>
        [TestCase(ComparisonOperator.Equal, false)]
        [TestCase(ComparisonOperator.NotEqual, true)]
        [TestCase(ComparisonOperator.GreaterThan, false)]
        [TestCase(ComparisonOperator.GreaterThanOrEqual, false)]
        [TestCase(ComparisonOperator.LessThan, false)]
        [TestCase(ComparisonOperator.LessThanOrEqual, false)]
        public void IsMet_EvaluateBindingNullOptionalBinding_UsesPredicateSemantics(
            ComparisonOperator comparison,
            bool expected
        )
        {
            GameRoot game = BuildConditionGame(out _, out _);
            EvaluateBindingConditional conditional = new EvaluateBindingConditional
            {
                Binding = "sourceEventInstanceID",
                Comparison = comparison,
                CompareTo = "EXPECTED_SOURCE",
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            context.Bind("sourceEventInstanceID", null);

            Assert.AreEqual(expected, GameEventExecutor.IsMet(conditional, game, context));
        }

        /// <summary>Verifies has event activated activation recorded returns true.</summary>
        [Test]
        public void IsMet_HasEventActivatedActivationRecorded_ReturnsTrue()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            game.EventRuntime.GetState("activated").ActivationCount = 1;
            HasEventActivatedConditional conditional = new HasEventActivatedConditional
            {
                EventInstanceID = "activated",
            };

            Assert.IsTrue(GameEventExecutor.IsMet(conditional, game));
        }

        /// <summary>Verifies is event complete persisted completion state returns true.</summary>
        [Test]
        public void IsMet_IsEventCompletePersistedCompletionState_ReturnsTrue()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            game.EventRuntime.GetState("limited").IsComplete = true;
            IsEventCompleteConditional conditional = new IsEventCompleteConditional
            {
                EventInstanceID = "limited",
            };

            bool isComplete = GameEventExecutor.IsMet(conditional, game);

            Assert.IsTrue(isComplete);
        }

        /// <summary>Verifies is event complete loaded unlimited event returns false.</summary>
        [Test]
        public void IsMet_IsEventCompleteLoadedUnlimitedEvent_ReturnsFalse()
        {
            GameRoot game = BuildConditionGame(out _, out _);
            game.GetEventPool().Add(new GameEvent { InstanceID = "unlimited" });
            game.EventRuntime.GetState("unlimited").ActivationCount = 10;
            IsEventCompleteConditional conditional = new IsEventCompleteConditional
            {
                EventInstanceID = "unlimited",
            };

            bool isComplete = GameEventExecutor.IsMet(conditional, game);

            Assert.IsFalse(isComplete);
        }

        /// <summary>Verifies roll against popular support roll below support returns true.</summary>
        [Test]
        public void IsMet_RollAgainstPopularSupportRollBelowSupport_ReturnsTrue()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            planet.PopularSupport["faction"] = 20;
            game.Random = new FixedRandomProvider(new[] { 0.19 });
            RollAgainstPopularSupportConditional conditional =
                new RollAgainstPopularSupportConditional
                {
                    FactionInstanceID = "faction",
                    PlanetBinding = "target",
                };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent { InstanceID = "INFORMANTS" },
                new GameEventState()
            );
            context.Bind("target", planet);

            bool result = GameEventExecutor.IsMet(
                conditional,
                new GameConditionContext(game, context)
            );

            Assert.IsTrue(result);
        }

        /// <summary>Verifies share parent different immediate parents does not match.</summary>
        [Test]
        public void IsMet_ShareParentDifferentImmediateParents_DoesNotMatch()
        {
            GameRoot game = BuildHierarchy(
                out Planet planet,
                out Fleet fleet,
                out CapitalShip ship
            );
            Officer planetOfficer = EntityFactory.CreateOfficer("planet-officer", "faction");
            Officer shipOfficer = EntityFactory.CreateOfficer("ship-officer", "faction");
            game.AttachNode(planetOfficer, planet);
            game.AttachNode(shipOfficer, ship);
            ShareParentConditional condition = new ShareParentConditional
            {
                Units = References(planetOfficer, shipOfficer),
            };

            bool isMet = GameEventExecutor.IsMet(condition, game);

            Assert.IsFalse(isMet);
            Assert.AreSame(planet, fleet.GetParent());
        }

        /// <summary>Verifies share ancestor same planet with different immediate parents matches.</summary>
        [Test]
        public void IsMet_ShareAncestorSamePlanetWithDifferentImmediateParents_Matches()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out CapitalShip ship);
            Officer planetOfficer = EntityFactory.CreateOfficer("planet-officer", "faction");
            Officer shipOfficer = EntityFactory.CreateOfficer("ship-officer", "faction");
            game.AttachNode(planetOfficer, planet);
            game.AttachNode(shipOfficer, ship);
            ShareAncestorConditional condition = new ShareAncestorConditional
            {
                Type = SceneAncestorType.Planet,
                Units = References(planetOfficer, shipOfficer),
            };

            bool isMet = GameEventExecutor.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        /// <summary>Verifies is captured with captor uncaptured officer with stale captor does not match.</summary>
        [Test]
        public void IsMet_IsCapturedWithCaptor_UncapturedOfficerWithStaleCaptorDoesNotMatch()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            officer.IsCaptured = false;
            officer.CaptorInstanceID = "captor";
            game.AttachNode(officer, planet);
            IsCapturedConditional condition = new IsCapturedConditional
            {
                OfficerInstanceID = officer.InstanceID,
                CaptorFactionInstanceID = "captor",
            };

            bool isMet = GameEventExecutor.IsMet(condition, game);

            Assert.IsFalse(isMet);
        }

        /// <summary>Verifies is captured optional captor qualifies captured officer when provided.</summary>
        [Test]
        public void IsMet_IsCapturedOptionalCaptor_QualifiesCapturedOfficerWhenProvided()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "captor";
            game.AttachNode(officer, planet);

            Assert.IsTrue(
                GameEventExecutor.IsMet(
                    new IsCapturedConditional { OfficerInstanceID = officer.InstanceID },
                    game
                )
            );
            Assert.IsFalse(
                GameEventExecutor.IsMet(
                    new IsCapturedConditional
                    {
                        OfficerInstanceID = officer.InstanceID,
                        CaptorFactionInstanceID = "other",
                    },
                    game
                )
            );
        }

        /// <summary>Verifies is killed inactive killed officer matches by registered identity.</summary>
        [Test]
        public void IsMet_IsKilledInactiveKilledOfficer_MatchesByRegisteredIdentity()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, planet);
            new PersonnelCommands(new PersonnelQueries(game)).KillOfficer(officer);
            IsKilledConditional condition = new IsKilledConditional
            {
                OfficerInstanceID = officer.InstanceID,
            };

            bool isMet = GameEventExecutor.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        /// <summary>Verifies is active inactive officer returns false without losing identity.</summary>
        [Test]
        public void IsMet_IsActiveInactiveOfficer_ReturnsFalseWithoutLosingIdentity()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, planet);
            officer.IsEnabled = false;
            IsActiveConditional condition = new IsActiveConditional
            {
                NodeInstanceID = officer.InstanceID,
            };

            bool isMet = GameEventExecutor.IsMet(condition, game);

            Assert.IsFalse(isMet);
            Assert.AreSame(
                officer,
                game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID, includeDisabled: true)
            );
        }

        /// <summary>Verifies is active active officer returns true.</summary>
        [Test]
        public void IsMet_IsActiveActiveOfficer_ReturnsTrue()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, planet);
            IsActiveConditional condition = new IsActiveConditional
            {
                NodeInstanceID = officer.InstanceID,
            };

            bool isMet = GameEventExecutor.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        /// <summary>Verifies has building type inactive planet with enabled building returns true.</summary>
        [Test]
        public void IsMet_HasBuildingTypeInactivePlanetWithEnabledBuilding_ReturnsTrue()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            planet.EnergyCapacity = 1;
            Building building = new Building
            {
                InstanceID = "building",
                OwnerInstanceID = "faction",
                BuildingType = BuildingType.Defense,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, planet);
            planet.IsEnabled = false;
            HasBuildingTypeConditional condition = new HasBuildingTypeConditional
            {
                PlanetInstanceID = planet.InstanceID,
                Type = BuildingType.Defense,
            };

            bool isMet = GameEventExecutor.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        /// <summary>Verifies has building type disabled building returns false.</summary>
        [Test]
        public void IsMet_HasBuildingTypeDisabledBuilding_ReturnsFalse()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            planet.EnergyCapacity = 1;
            Building building = new Building
            {
                InstanceID = "building",
                OwnerInstanceID = "faction",
                BuildingType = BuildingType.Defense,
                ManufacturingStatus = ManufacturingStatus.Complete,
                IsEnabled = false,
            };
            game.AttachNode(building, planet);
            HasBuildingTypeConditional condition = new HasBuildingTypeConditional
            {
                PlanetInstanceID = planet.InstanceID,
                Type = BuildingType.Defense,
            };

            bool isMet = GameEventExecutor.IsMet(condition, game);

            Assert.IsFalse(isMet);
        }

        /// <summary>Verifies has force rank configured semantic rank uses configured minimum.</summary>
        [Test]
        public void IsMet_HasForceRankConfiguredSemanticRank_UsesConfiguredMinimum()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            officer.ForceValue = game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight);
            game.AttachNode(officer, planet);
            HasForceRankConditional condition = new HasForceRankConditional
            {
                OfficerInstanceID = officer.InstanceID,
                Comparison = ComparisonOperator.GreaterThanOrEqual,
                Rank = ForceRankLabel.ForceKnight,
            };

            bool isMet = GameEventExecutor.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        /// <summary>Verifies has force rank inactive officer uses configured minimum.</summary>
        [Test]
        public void IsMet_HasForceRankInactiveOfficer_UsesConfiguredMinimum()
        {
            GameRoot game = BuildHierarchy(out Planet planet, out _, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            officer.ForceValue = game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight);
            game.AttachNode(officer, planet);
            officer.IsEnabled = false;
            HasForceRankConditional condition = new HasForceRankConditional
            {
                OfficerInstanceID = officer.InstanceID,
                Comparison = ComparisonOperator.GreaterThanOrEqual,
                Rank = ForceRankLabel.ForceKnight,
            };

            bool isMet = GameEventExecutor.IsMet(condition, game);

            Assert.IsTrue(isMet);
        }

        /// <summary>Verifies that choosing the first match does not evaluate later selectors.</summary>
        [Test]
        public void Select_FirstCandidateMatches_DoesNotEvaluateLaterInvalidSelector()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
            SelectFirst selector = new SelectFirst
            {
                Selectors = { new SelectPlanets(), new SpawnUnits() },
            };

            ISceneNode selected = GameEventExecutor
                .Select(selector, game, new StubRNG(), null)
                .Single();

            Assert.AreSame(planet, selected);
        }

        /// <summary>Verifies that ordinary selection reads filter state when enumerated.</summary>
        [Test]
        public void Select_PlanetDestroyedBeforeEnumeration_ExcludesPlanet()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
            IEnumerable<ISceneNode> selected = GameEventExecutor.Select(
                new SelectPlanets(),
                game,
                new StubRNG(),
                null
            );
            planet.IsDestroyed = true;

            Assert.IsEmpty(selected);
        }

        /// <summary>Verifies that random selection rolls once per distinct candidate in identity order.</summary>
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

            IEnumerable<ISceneNode> selected = GameEventExecutor.Select(
                selector,
                game,
                provider,
                null
            );

            Assert.AreEqual(0.875, provider.NextDouble());
            CollectionAssert.AreEqual(new[] { second }, selected);
        }

        /// <summary>Verifies that selecting a spawn source rejects it before consuming randomness.</summary>
        [Test]
        public void Select_SpawnSource_RejectsWithoutRandomDraw()
        {
            GameRoot game = BuildSelectionGame(out _);
            QueueRNG provider = new QueueRNG(0.25);

            Assert.Throws<InvalidOperationException>(() =>
                GameEventExecutor.Select(new SpawnUnits(), game, provider, null)
            );

            Assert.AreEqual(0.25, provider.NextDouble());
        }

        /// <summary>Verifies select planets matching instance id returns planet.</summary>
        [Test]
        public void Select_PlanetsMatchingInstanceID_ReturnsPlanet()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
            SelectPlanets selector = new SelectPlanets { InstanceID = planet.InstanceID };

            Planet selected = GameEventExecutor
                .Select(selector, game, new StubRNG(), null)
                .Cast<Planet>()
                .Single();

            Assert.AreSame(planet, selected);
        }

        /// <summary>Verifies select planets destroyed planet returns nothing.</summary>
        [Test]
        public void Select_PlanetsDestroyedPlanet_ReturnsNothing()
        {
            GameRoot game = BuildSelectionGame(out Planet planet);
            planet.IsDestroyed = true;
            SelectPlanets selector = new SelectPlanets { InstanceID = planet.InstanceID };

            bool any = GameEventExecutor.Select(selector, game, new StubRNG(), null).Any();

            Assert.IsFalse(any);
        }

        /// <summary>Verifies select planets no filters returns every surviving planet.</summary>
        [Test]
        public void Select_PlanetsNoFilters_ReturnsEverySurvivingPlanet()
        {
            GameRoot game = BuildSelectionGame(out Planet firstPlanet);
            Planet secondPlanet = new Planet { InstanceID = "second-planet" };
            game.AttachNode(secondPlanet, firstPlanet.GetParent());
            SelectPlanets selector = new SelectPlanets();

            Planet[] selected = GameEventExecutor
                .Select(selector, game, new StubRNG(), null)
                .Cast<Planet>()
                .ToArray();

            CollectionAssert.AreEqual(new[] { firstPlanet, secondPlanet }, selected);
        }

        /// <summary>Verifies select random filtered planet set returns requested count.</summary>
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

            Planet selected = GameEventExecutor
                .Select(selector, game, new StubRNG(), null)
                .Cast<Planet>()
                .Single();

            Assert.AreSame(rimPlanet, selected);
        }

        /// <summary>Verifies select manufacturing orders matching planet returns queued product.</summary>
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

            IManufacturable selected = GameEventExecutor
                .Select(selector, game, new StubRNG(), null)
                .Cast<IManufacturable>()
                .Single();

            Assert.AreSame(building, selected);
        }

        /// <summary>Verifies select capital ships include inactive returns capital ship.</summary>
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

            ISceneNode selected = GameEventExecutor
                .Select(selector, game, new FixedRNG(0), null)
                .Single();

            Assert.AreSame(ship, selected);
        }

        /// <summary>Verifies select officers include inactive at current planet returns officer.</summary>
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

            List<ISceneNode> selected = GameEventExecutor
                .Select(selector, game, new FixedRNG(0), null)
                .ToList();

            CollectionAssert.AreEqual(new ISceneNode[] { officer }, selected);
        }

        /// <summary>Verifies select binding stale reference with registered instance id returns canonical node.</summary>
        [Test]
        public void Select_BindingStaleReferenceWithRegisteredInstanceID_ReturnsCanonicalNode()
        {
            GameRoot game = BuildSelectionGame(out Planet origin);
            Officer canonical = EntityFactory.CreateOfficer("han", "faction");
            game.AttachNode(canonical, origin);
            Officer stale = EntityFactory.CreateOfficer(canonical.InstanceID, "faction");
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null,
                null
            );
            context.Bind("officer", stale);

            ISceneNode selected = GameEventExecutor
                .Select(new SelectBinding { Binding = "officer" }, game, new FixedRNG(0), context)
                .Single();

            Assert.AreSame(canonical, selected);
        }

        /// <summary>Verifies select binding inactive registered node returns canonical node.</summary>
        [Test]
        public void Select_BindingInactiveRegisteredNode_ReturnsCanonicalNode()
        {
            GameRoot game = BuildSelectionGame(out Planet origin);
            Officer officer = EntityFactory.CreateOfficer("officer", "faction");
            game.AttachNode(officer, origin);
            officer.IsEnabled = false;
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null,
                null
            );
            context.Bind("officer", officer);

            ISceneNode selected = GameEventExecutor
                .Select(new SelectBinding { Binding = "officer" }, game, new FixedRNG(0), context)
                .Single();

            Assert.AreSame(officer, selected);
        }

        /// <summary>Verifies select previous location inactive unit returns previous location.</summary>
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

            ISceneNode selected = GameEventExecutor
                .Select(selector, game, new FixedRNG(0), null)
                .Single();

            Assert.AreSame(planet, selected);
        }

        /// <summary>Verifies that a later invalid argument leaves earlier trigger bindings intact.</summary>
        [Test]
        public void Bind_LaterArgumentInvalid_RetainsEarlierBinding()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            DuelResult result = new DuelResult { EncounteredOfficer = officer };
            DuelCompletedTrigger trigger = new DuelCompletedTrigger
            {
                Bindings =
                {
                    new GameEventBinding { Argument = "FirstOfficer", As = "officer" },
                    new GameEventBinding { Argument = "Missing", As = "missing" },
                },
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState()
            );

            Assert.Throws<InvalidOperationException>(() =>
                GameEventExecutor.Bind(trigger, context, result)
            );

            Assert.AreSame(officer, context.GetBinding<Officer>("officer"));
        }

        /// <summary>Verifies that a duplicate alias leaves its first trigger argument unchanged.</summary>
        [Test]
        public void Bind_DuplicateAlias_RetainsFirstArgument()
        {
            Officer first = new Officer { InstanceID = "first" };
            Officer second = new Officer { InstanceID = "second" };
            DuelResult result = new DuelResult
            {
                EncounteredOfficer = first,
                OpposingOfficer = second,
            };
            DuelCompletedTrigger trigger = new DuelCompletedTrigger
            {
                Bindings =
                {
                    new GameEventBinding { Argument = "FirstOfficer", As = "officer" },
                    new GameEventBinding { Argument = "SecondOfficer", As = "officer" },
                },
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState()
            );

            Assert.Throws<InvalidOperationException>(() =>
                GameEventExecutor.Bind(trigger, context, result)
            );

            Assert.AreSame(first, context.GetBinding<Officer>("officer"));
        }

        /// <summary>Verifies matches planet ownership changed trigger applies ownership filters.</summary>
        [Test]
        public void Matches_PlanetOwnershipChangedTrigger_AppliesOwnershipFilters()
        {
            PlanetOwnershipChangedTrigger trigger = new PlanetOwnershipChangedTrigger
            {
                PlanetInstanceID = "planet",
                NewOwnerFactionInstanceID = "alliance",
                Reason = PlanetOwnershipChangeReason.PopularSupport,
            };
            PlanetOwnershipChangedResult result = new PlanetOwnershipChangedResult
            {
                Planet = new Planet { InstanceID = "planet" },
                NewOwner = new Faction { InstanceID = "alliance" },
                Reason = PlanetOwnershipChangeReason.PopularSupport,
            };

            Assert.IsTrue(GameEventExecutor.Matches(trigger, result));
            result.Reason = PlanetOwnershipChangeReason.None;
            Assert.IsFalse(GameEventExecutor.Matches(trigger, result));
        }

        /// <summary>Verifies matches intelligence revealed trigger applies recipient and observation filters.</summary>
        [Test]
        public void Matches_IntelligenceRevealedTrigger_AppliesRecipientAndObservationFilters()
        {
            IntelligenceRevealedTrigger trigger = new IntelligenceRevealedTrigger
            {
                RecipientFactionInstanceID = "alliance",
                ObservationInstanceID = "planet",
            };
            IntelligenceRevealedResult result = new IntelligenceRevealedResult
            {
                Recipient = new Faction { InstanceID = "alliance" },
                Observations = new List<ISceneNode> { new Planet { InstanceID = "planet" } },
            };

            Assert.IsTrue(GameEventExecutor.Matches(trigger, result));
            result.Observations.Clear();
            Assert.IsFalse(GameEventExecutor.Matches(trigger, result));
        }

        /// <summary>Verifies matches maintenance required trigger applies faction filter.</summary>
        [Test]
        public void Matches_MaintenanceRequiredTrigger_AppliesFactionFilter()
        {
            MaintenanceRequiredTrigger trigger = new MaintenanceRequiredTrigger
            {
                FactionInstanceID = "alliance",
            };
            MaintenanceRequiredResult result = new MaintenanceRequiredResult
            {
                Faction = new Faction { InstanceID = "alliance" },
            };

            Assert.IsTrue(GameEventExecutor.Matches(trigger, result));
            result.Faction.InstanceID = "empire";
            Assert.IsFalse(GameEventExecutor.Matches(trigger, result));
        }

        /// <summary>Verifies matches officer capture changed trigger applies officer and state filters.</summary>
        [Test]
        public void Matches_OfficerCaptureChangedTrigger_AppliesOfficerAndStateFilters()
        {
            OfficerCaptureChangedTrigger trigger = new OfficerCaptureChangedTrigger
            {
                OfficerInstanceID = "han",
                IsCaptured = true,
            };
            OfficerCaptureStateResult result = new OfficerCaptureStateResult
            {
                TargetOfficer = new Officer { InstanceID = "han" },
                IsCaptured = true,
            };

            Assert.IsTrue(GameEventExecutor.Matches(trigger, result));
            result.IsCaptured = false;
            Assert.IsFalse(GameEventExecutor.Matches(trigger, result));
        }

        /// <summary>Verifies matches force discovery changed trigger applies officer and event type filters.</summary>
        [Test]
        public void Matches_ForceDiscoveryChangedTrigger_AppliesOfficerAndEventTypeFilters()
        {
            ForceDiscoveryChangedTrigger trigger = new ForceDiscoveryChangedTrigger
            {
                OfficerInstanceID = "luke",
                EventType = ForceEventType.ForceUserDiscovered,
            };
            ForceDiscoveryResult result = new ForceDiscoveryResult
            {
                Officer = new Officer { InstanceID = "luke" },
                EventType = ForceEventType.ForceUserDiscovered,
            };

            Assert.IsTrue(GameEventExecutor.Matches(trigger, result));
            result.EventType = ForceEventType.DiscoveringForceUser;
            Assert.IsFalse(GameEventExecutor.Matches(trigger, result));
        }

        /// <summary>Verifies matches unit arrived trigger applies identity and destination filters.</summary>
        [Test]
        public void Matches_UnitArrivedTrigger_AppliesIdentityAndDestinationFilters()
        {
            UnitArrivedTrigger trigger = new UnitArrivedTrigger
            {
                UnitInstanceID = "officer",
                DestinationInstanceID = "planet",
            };
            UnitArrivedResult result = new UnitArrivedResult
            {
                Unit = new Officer { InstanceID = "officer" },
                Destination = new Planet { InstanceID = "planet" },
            };

            Assert.IsTrue(GameEventExecutor.Matches(trigger, result));
            result.Destination.InstanceID = "elsewhere";
            Assert.IsFalse(GameEventExecutor.Matches(trigger, result));
        }

        /// <summary>Verifies matches unit destroyed trigger covers every destruction path.</summary>
        /// <param name="result">The destruction result to match.</param>
        /// <param name="reason">The authored destruction-reason filter.</param>
        [TestCaseSource(nameof(UnitDestructionResults))]
        public void Matches_UnitDestroyedTrigger_CoversEveryDestructionPath(
            GameObjectDestroyedResult result,
            UnitDestructionReason reason
        )
        {
            UnitDestroyedTrigger trigger = new UnitDestroyedTrigger
            {
                UnitInstanceID = "unit",
                Reason = reason,
            };

            Assert.IsTrue(GameEventExecutor.Matches(trigger, result));
        }

        /// <summary>Verifies matches duel completed trigger applies officer and source filters.</summary>
        [Test]
        public void Matches_DuelCompletedTrigger_AppliesOfficerAndSourceFilters()
        {
            DuelCompletedTrigger trigger = new DuelCompletedTrigger
            {
                FirstOfficerInstanceID = "luke",
                SecondOfficerInstanceID = "vader",
                SourceEventInstanceID = "encounter",
            };
            DuelResult result = new DuelResult
            {
                EncounteredOfficer = new Officer { InstanceID = "luke" },
                OpposingOfficer = new Officer { InstanceID = "vader" },
                SourceEventInstanceID = "encounter",
            };

            Assert.IsTrue(GameEventExecutor.Matches(trigger, result));
            result.SourceEventInstanceID = "other";
            Assert.IsFalse(GameEventExecutor.Matches(trigger, result));
        }

        /// <summary>Verifies matches bombardment completed trigger applies outcome filters.</summary>
        [Test]
        public void Matches_BombardmentCompletedTrigger_AppliesOutcomeFilters()
        {
            BombardmentCompletedTrigger trigger = new BombardmentCompletedTrigger
            {
                PlanetInstanceID = "planet",
                Type = BombardmentType.DestroyPlanet,
                PlanetDestroyed = true,
            };
            BombardmentResult result = new BombardmentResult
            {
                Planet = new Planet { InstanceID = "planet" },
                Type = BombardmentType.DestroyPlanet,
                PlanetDestroyed = true,
            };

            Assert.IsTrue(GameEventExecutor.Matches(trigger, result));
            result.PlanetDestroyed = false;
            Assert.IsFalse(GameEventExecutor.Matches(trigger, result));
        }

        /// <summary>Builds an eligible duel followed by an authored message for deferred failure tests.</summary>
        /// <returns>The game, event, and ordered duel participants.</returns>
        private (
            GameRoot Game,
            GameEvent Event,
            Officer Encountered,
            Officer Opposing
        ) CreateDeferredDuelEvent()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            game.Config.DuelResolution.CombatCaptureAvoidance = new Dictionary<int, int>
            {
                { 0, 50 },
            };
            Officer encountered = EntityFactory.CreateOfficer("encountered", "empire");
            Officer opposing = EntityFactory.CreateOfficer("opposing", "rebels");
            encountered.SetBaseRating(SkillRating.Combat, 50);
            opposing.SetBaseRating(SkillRating.Combat, 50);
            game.AttachNode(encountered, planet);
            opposing.IsCaptured = true;
            game.AttachNode(opposing, planet);
            opposing.IsCaptured = false;
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "deferred-duel",
                Actions = new List<GameAction>
                {
                    new TriggerDuelAction
                    {
                        FirstOfficerInstanceID = encountered.InstanceID,
                        SecondOfficerInstanceID = opposing.InstanceID,
                    },
                    new SendMessageAction
                    {
                        RecipientFactionInstanceID = encountered.OwnerInstanceID,
                        Subject = "Following",
                    },
                },
            };
            game.GetEventPool().Add(gameEvent);
            return (game, gameEvent, encountered, opposing);
        }

        /// <summary>Fails after the first capture roll to exercise partial-mutation failure handling.</summary>
        private sealed class CaptureThenThrowRandom : IRandomNumberProvider
        {
            private int _rolls;

            /// <summary>Rejects unexpected floating-point draws in the capture path.</summary>
            /// <returns>No value; this path always throws.</returns>
            public double NextDouble() =>
                throw new InvalidOperationException("Unexpected random draw.");

            /// <summary>Fails the first capture-avoidance roll, then throws during injury resolution.</summary>
            /// <param name="min">The requested inclusive lower bound.</param>
            /// <param name="max">The requested exclusive upper bound.</param>
            /// <returns>The maximum allowed value on the first call only.</returns>
            public int NextInt(int min, int max) =>
                _rolls++ == 0
                    ? max - 1
                    : throw new InvalidOperationException("Injury roll failed.");
        }

        /// <summary>Executes one authored action through the public event-processing contract.</summary>
        /// <param name="game">The state in which the event executes.</param>
        /// <param name="action">The authored operation to perform.</param>
        /// <param name="random">The random stream shared by event execution and duel resolution.</param>
        /// <param name="unitFactory">The templates used by spawning actions.</param>
        /// <param name="triggerResult">The fact activating a triggered event, when applicable.</param>
        /// <param name="trigger">The trigger and its binding definitions, when applicable.</param>
        /// <returns>The facts produced by the completed activation.</returns>
        private static List<GameResult> RunAuthoredAction(
            GameRoot game,
            GameAction action,
            IRandomNumberProvider random = null,
            UnitFactory unitFactory = null,
            GameResult triggerResult = null,
            GameEventTrigger trigger = null
        )
        {
            random ??= new FixedRNG(0.99);
            MovementCommands movement = CreateMovementCommands(game);
            PlanetaryControlCommands control = new PlanetaryControlCommands(
                game,
                movement,
                new ManufacturingCommands(
                    game,
                    new FleetCommands(game),
                    new ManufacturingQueries(game)
                ),
                new FogOfWarCommands(game),
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            GameEventExecutor executor = new GameEventExecutor(
                game,
                random,
                unitFactory,
                movement,
                control,
                new DuelCommands(game, random),
                new MessageCommands(game, new MessageFactory(null))
            );
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "authored-action",
                Actions = new List<GameAction> { action },
            };
            if (trigger != null)
                gameEvent.Triggers.Add(trigger);
            game.GetEventPool().Add(gameEvent);
            return trigger != null
                ? executor.HandleResults(new[] { triggerResult })
                : executor.ProcessEvents(game.GetEventPool());
        }

        /// <summary>Builds movement dependencies for authored movement and ownership tests.</summary>
        /// <param name="game">The state used by each movement dependency.</param>
        /// <returns>The movement commands for that state.</returns>
        private static MovementCommands CreateMovementCommands(GameRoot game)
        {
            return new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
        }

        /// <summary>
        /// Creates an event that queues a message before changing its subject's display name.
        /// </summary>
        /// <returns>The event, its subject planet, and its message recipient.</returns>
        private (GameEvent Event, Planet Planet, Faction Faction) CreateMessageEvent()
        {
            Faction faction = new Faction { InstanceID = "faction", DisplayName = "Faction" };
            _game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet", DisplayName = "Before" };
            _game.AttachNode(sector, _game.Galaxy);
            _game.AttachNode(planet, sector);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "MESSAGE_ORDER",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 0 } },
                Actions = new List<GameAction>
                {
                    new SendMessageAction
                    {
                        RecipientFactionInstanceID = faction.InstanceID,
                        SubjectInstanceID = planet.InstanceID,
                        Subject = "{subject}",
                    },
                    new SetDisplayNameAction
                    {
                        TargetInstanceID = planet.InstanceID,
                        Name = "After",
                    },
                },
            };
            _game.GetEventPool().Add(gameEvent);
            return (gameEvent, planet, faction);
        }

        /// <summary>
        /// Creates dependent event.
        /// </summary>
        /// <param name="instanceID">The instance id.</param>
        /// <param name="afterAll">Whether after all.</param>
        /// <returns>The created dependent event.</returns>
        private GameEvent CreateDependentEvent(string instanceID, bool afterAll)
        {
            AfterEvents dependencies = new AfterEvents
            {
                DelayTicks = 5,
                Events = new List<EventDependency>
                {
                    new EventDependency { EventInstanceID = "FIRST" },
                    new EventDependency { EventInstanceID = "SECOND" },
                },
            };
            GameEventState first = _game.EventRuntime.GetState("FIRST");
            first.ActivationCount = 1;
            first.LastActivationTick = 10;
            GameEventState second = _game.EventRuntime.GetState("SECOND");
            second.ActivationCount = afterAll ? 1 : 0;
            second.LastActivationTick = afterAll ? 20 : 0;

            GameEvent gameEvent = CreateTickEvent(instanceID, targetTick: 0, repeatable: false);
            gameEvent.Schedule = new GameEventSchedule();
            if (afterAll)
                gameEvent.Schedule.AfterAll = dependencies;
            else
                gameEvent.Schedule.AfterAny = dependencies;
            return gameEvent;
        }

        /// <summary>
        /// Creates tick event.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="targetTick">The target tick.</param>
        /// <param name="repeatable">Whether repeatable.</param>
        /// <returns>The created tick event.</returns>
        private static GameEvent CreateTickEvent(string instanceId, int targetTick, bool repeatable)
        {
            return new GameEvent
            {
                InstanceID = instanceId,
                MaximumActivations = repeatable ? null : 1,
                Conditionals = new List<GameConditional>
                {
                    new TickCountConditional
                    {
                        Comparison = ComparisonOperator.GreaterThan,
                        Ticks = targetTick,
                    },
                },
            };
        }

        /// <summary>
        /// Executes encounter trigger.
        /// </summary>
        /// <returns>The result of encounter trigger.</returns>
        private static List<GameEventTrigger> EncounterTrigger() =>
            new List<GameEventTrigger>
            {
                new DuelCompletedTrigger
                {
                    Bindings = TriggerBindings(
                        ("FirstOfficer", "firstOfficer"),
                        ("SecondOfficer", "secondOfficer"),
                        ("FirstOfficerInstanceID", "firstOfficerInstanceID"),
                        ("SecondOfficerInstanceID", "secondOfficerInstanceID")
                    ),
                },
            };

        /// <summary>
        /// Executes trigger bindings.
        /// </summary>
        /// <param name="bindings">The bindings.</param>
        /// <returns>The result of trigger bindings.</returns>
        private static List<GameEventBinding> TriggerBindings(
            params (string Argument, string As)[] bindings
        ) =>
            bindings
                .Select(binding => new GameEventBinding
                {
                    Argument = binding.Argument,
                    As = binding.As,
                })
                .ToList();

        /// <summary>
        /// Executes binding equals.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <returns>The result of binding equals.</returns>
        private static EvaluateBindingConditional BindingEquals(string name, string value) =>
            new EvaluateBindingConditional
            {
                Binding = name,
                Comparison = ComparisonOperator.Equal,
                CompareTo = value,
            };

        /// <summary>
        /// Builds game.
        /// </summary>
        /// <param name="empirePlanet">Receives the empire planet.</param>
        /// <param name="rebelPlanet">Receives the rebel planet.</param>
        /// <returns>The constructed game.</returns>
        private GameRoot BuildGame(out Planet empirePlanet, out Planet rebelPlanet)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.Galaxy);
            empirePlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(empirePlanet, sector);
            rebelPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(rebelPlanet, sector);
            return game;
        }

        /// <summary>
        /// Builds game.
        /// </summary>
        /// <param name="empirePlanet">Receives the empire planet.</param>
        /// <param name="rebelPlanet">Receives the rebel planet.</param>
        /// <returns>The constructed game.</returns>
        private static GameRoot BuildConditionGame(out Planet empirePlanet, out Planet rebelPlanet)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            empirePlanet = new Planet
            {
                InstanceID = "empire-planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            rebelPlanet = new Planet
            {
                InstanceID = "rebel-planet",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(empirePlanet, sector);
            game.AttachNode(rebelPlanet, sector);
            return game;
        }

        /// <summary>
        /// Builds hierarchy.
        /// </summary>
        /// <param name="planet">Receives the planet.</param>
        /// <param name="fleet">Receives the fleet.</param>
        /// <param name="ship">Receives the ship.</param>
        /// <returns>The constructed hierarchy.</returns>
        private static GameRoot BuildHierarchy(
            out Planet planet,
            out Fleet fleet,
            out CapitalShip ship
        )
        {
            GameConfig config = new GameConfig();
            config.Jedi.RankLabelByMinimumForceRank[100] = (int)ForceRankLabel.ForceKnight;
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "faction" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = "faction",
                IsColonized = true,
            };
            fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "faction" };
            ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "faction",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return game;
        }

        /// <summary>
        /// Executes references.
        /// </summary>
        /// <param name="nodes">The nodes.</param>
        /// <returns>The result of references.</returns>
        private static List<EventUnitReference> References(params ISceneNode[] nodes) =>
            new List<EventUnitReference>(
                System.Array.ConvertAll(
                    nodes,
                    node => new EventUnitReference { UnitInstanceID = node.InstanceID }
                )
            );

        /// <summary>
        /// Builds game.
        /// </summary>
        /// <param name="planet">Receives the planet.</param>
        /// <returns>The constructed game.</returns>
        private static GameRoot BuildSelectionGame(out Planet planet)
        {
            GameRoot game = new GameRoot(new GameConfig());
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

        /// <summary>
        /// Executes unit destruction results.
        /// </summary>
        /// <returns>The result of unit destruction results.</returns>
        private static IEnumerable<TestCaseData> UnitDestructionResults()
        {
            Officer unit = new Officer { InstanceID = "unit" };
            yield return new TestCaseData(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = unit,
                    Reason = UnitDestructionReason.Direct,
                },
                UnitDestructionReason.Direct
            );
            yield return new TestCaseData(
                new GameObjectDestroyedOnArrivalResult { DestroyedObject = unit },
                UnitDestructionReason.Arrival
            );
            yield return new TestCaseData(
                new GameObjectAutoscrappedResult { DestroyedObject = unit },
                UnitDestructionReason.Maintenance
            );
            yield return new TestCaseData(
                new GameObjectSabotagedResult { DestroyedObject = unit },
                UnitDestructionReason.Sabotage
            );
        }

        private sealed class RecordingRandom : IRandomNumberProvider
        {
            private readonly GameEventEvaluationContext _evaluation;
            public int ResultsAtRoll { get; private set; } = -1;

            /// <summary>
            /// Captures the evaluation whose local result visibility is observed.
            /// </summary>
            /// <param name="evaluation">The active event evaluation.</param>
            internal RecordingRandom(GameEventEvaluationContext evaluation)
            {
                _evaluation = evaluation;
            }

            /// <summary>
            /// Records the current local result count before returning the fixed roll.
            /// </summary>
            /// <returns>The fixed probability sample.</returns>
            public double NextDouble()
            {
                ResultsAtRoll = _evaluation.Results.Count;
                return 0;
            }

            /// <summary>
            /// Rejects unexpected integer rolls in this action sequence.
            /// </summary>
            /// <param name="min">The lower bound requested.</param>
            /// <param name="max">The upper bound requested.</param>
            /// <returns>No value; an unexpected roll fails the test.</returns>
            public int NextInt(int min, int max) =>
                throw new InvalidOperationException("Unexpected integer roll.");
        }
    }
}
