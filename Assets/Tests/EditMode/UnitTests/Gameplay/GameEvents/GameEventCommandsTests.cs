using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Gameplay.GameEvents;
using Rebellion.Gameplay.Scripting;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.Gameplay.GameEvents
{
    [TestFixture]
    public class GameEventCommandsTests
    {
        private GameRoot _game;
        private GameEventCommands _system;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            var eventRandom = new FixedRandomProvider(new[] { 0.5 });
            _system = new GameEventCommands(
                _game,
                eventRandom,
                new GameActionCommands(_game, eventRandom)
            );
        }

        [Test]
        public void ProcessScheduledEvents_AbsoluteSchedule_StoresAbsoluteTick()
        {
            _game.CurrentTick = 10;
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "absolute",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 25 } },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(25, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
        }

        [Test]
        public void ProcessScheduledEvents_FixedIntervalFirstActivation_UsesInitialDelay()
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

            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(5, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
        }

        [TestCase(0.0, 10)]
        [TestCase(0.9999, 30)]
        public void ProcessScheduledEvents_RandomFirstActivation_UsesInclusiveRangeEndpoints(
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
            var eventRandom = new QueueRNG(roll);
            GameEventCommands executor = new GameEventCommands(
                _game,
                eventRandom,
                new GameActionCommands(_game, eventRandom)
            );

            executor.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(
                expected,
                _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick
            );
        }

        [TestCase(0.0, 50)]
        [TestCase(0.9999, 70)]
        public void ProcessScheduledEvents_RandomRepeat_UsesInclusiveRangeFromCurrentTick(
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
            var eventRandom = new QueueRNG(roll);
            GameEventCommands executor = new GameEventCommands(
                _game,
                eventRandom,
                new GameActionCommands(_game, eventRandom)
            );

            executor.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(
                expected,
                _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick
            );
        }

        /// <summary>
        /// Verifies that categorized events retain their action order and message delivery.
        /// </summary>
        /// <param name="category">The classification that must not affect execution.</param>
        [TestCase(GameEventCategory.None)]
        [TestCase(GameEventCategory.Story)]
        [TestCase(GameEventCategory.Disaster)]
        public void ProcessScheduledEvents_MessageBeforeRename_ResolvesTemplateAfterLaterAction(
            GameEventCategory category
        )
        {
            (GameEvent gameEvent, Planet planet, Faction faction) = CreateMessageEvent();
            gameEvent.Category = category;
            GameEventCommands system = new GameEventCommands(
                _game,
                _game.Random,
                new GameActionCommands(
                    _game,
                    _game.Random,
                    messageCommands: new MessageCommands(_game, new MessageFactory(null))
                )
            );

            List<GameResult> results = system.ProcessScheduledEvents(_game.GetEventPool());

            MessageDeliveredResult delivery = results.OfType<MessageDeliveredResult>().Single();
            Assert.AreEqual("After", delivery.Message.Title);
            Assert.AreSame(delivery.Message, faction.Messages[MessageType.Advice].Single());
        }

        [Test]
        public void ProcessScheduledEvents_MissingCommands_PreservesActionsWithoutRecordingActivation()
        {
            (GameEvent gameEvent, Planet planet, Faction _) = CreateMessageEvent();

            Assert.Throws<InvalidOperationException>(() =>
                _system.ProcessScheduledEvents(_game.GetEventPool())
            );

            Assert.AreEqual("After", planet.DisplayName);
            Assert.AreEqual(0, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
            Assert.Contains(gameEvent, _game.GetEventPool());
        }

        [Test]
        public void ProcessScheduledEvents_DeferredMessageFails_DeliversNextMessageAndRecordsActivation()
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
            GameEventCommands system = new GameEventCommands(
                _game,
                _game.Random,
                new GameActionCommands(
                    _game,
                    _game.Random,
                    messageCommands: new MessageCommands(_game, new MessageFactory(null))
                )
            );
            LogAssert.Expect(
                LogType.Error,
                new Regex("Event 'MESSAGE_ORDER' deferred action 'SendMessageAction' failed:")
            );

            List<GameResult> results = system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(
                "After",
                results.OfType<MessageDeliveredResult>().Single().Message.Title
            );
            Assert.AreEqual(1, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        [Test]
        public void ProcessScheduledEvents_MissingDuelCommands_DeliversFollowingMessage()
        {
            (GameRoot game, GameEvent gameEvent, Officer _, Officer _) = CreateDeferredDuelEvent();
            GameEventCommands executor = new GameEventCommands(
                game,
                game.Random,
                new GameActionCommands(
                    game,
                    game.Random,
                    messageCommands: new MessageCommands(game, new MessageFactory(null))
                )
            );
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Event 'deferred-duel' deferred action 'TriggerDuelAction' failed: System.InvalidOperationException:"
                )
            );

            List<GameResult> results = executor.ProcessScheduledEvents(game.GetEventPool());

            Assert.AreEqual(
                "Following",
                results.OfType<MessageDeliveredResult>().Single().Message.Title
            );
            Assert.AreEqual(1, game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        [Test]
        public void ProcessScheduledEvents_DuelFailsAfterCapture_RetainsCaptureWithoutPublishingPartialResults()
        {
            (GameRoot game, GameEvent gameEvent, Officer encountered, Officer opposing) =
                CreateDeferredDuelEvent();
            GameEventCommands executor = new GameEventCommands(
                game,
                game.Random,
                new GameActionCommands(
                    game,
                    game.Random,
                    duelCommands: new DuelCommands(game, new CaptureThenThrowRandom()),
                    messageCommands: new MessageCommands(game, new MessageFactory(null))
                )
            );
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Event 'deferred-duel' deferred action 'TriggerDuelAction' failed: System.InvalidOperationException:"
                )
            );

            List<GameResult> results = executor.ProcessScheduledEvents(game.GetEventPool());

            Assert.IsTrue(encountered.IsCaptured);
            Assert.AreEqual(opposing.OwnerInstanceID, encountered.CaptorInstanceID);
            Assert.IsInstanceOf<MessageDeliveredResult>(results.Single());
            Assert.AreEqual(gameEvent.InstanceID, results.Single().SourceEventInstanceID);
        }

        [Test]
        public void ProcessScheduledEvents_DuelBeforeCapture_SkipsDeferredDuelWithoutRolling()
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
            GameEventCommands executor = new GameEventCommands(
                game,
                game.Random,
                new GameActionCommands(
                    game,
                    game.Random,
                    duelCommands: new DuelCommands(game, new ThrowingRNG())
                )
            );

            List<GameResult> results = executor.ProcessScheduledEvents(
                new List<GameEvent> { gameEvent }
            );

            Assert.IsInstanceOf<OfficerCaptureStateResult>(results.Single());
            Assert.AreEqual(gameEvent.InstanceID, results.Single().SourceEventInstanceID);
        }

        [Test]
        public void ProcessScheduledEvents_OwnershipBeforeDisabling_TransfersPreviouslySelectedOfficer()
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
                        Units = new List<GameSelector>
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
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            GameEventCommands executor = new GameEventCommands(
                game,
                game.Random,
                new GameActionCommands(game, game.Random, planetaryControlCommands: control)
            );

            List<GameResult> results = executor.ProcessScheduledEvents(
                new List<GameEvent> { gameEvent }
            );

            Assert.IsFalse(officer.IsActive());
            Assert.AreEqual("rebels", officer.OwnerInstanceID);
            UnitOwnershipChangedResult ownership = results
                .OfType<UnitOwnershipChangedResult>()
                .Single();
            Assert.AreSame(officer, ownership.Unit);
            Assert.AreEqual(gameEvent.InstanceID, ownership.SourceEventInstanceID);
        }

        [Test]
        public void ProcessScheduledEvents_MessageBeforeLocalChange_ReturnsLocalFactBeforeDelivery()
        {
            (GameEvent gameEvent, Planet planet, Faction _) = CreateMessageEvent();
            gameEvent.Actions.Add(
                new ChangeRawResourceNodesAction
                {
                    PlanetInstanceID = planet.InstanceID,
                    Amount = 1,
                }
            );
            GameEventCommands executor = new GameEventCommands(
                _game,
                _game.Random,
                new GameActionCommands(
                    _game,
                    _game.Random,
                    messageCommands: new MessageCommands(_game, new MessageFactory(null))
                )
            );

            List<GameResult> results = executor.ProcessScheduledEvents(_game.GetEventPool());

            CollectionAssert.AreEqual(
                new[] { typeof(PlanetStatChangedResult), typeof(MessageDeliveredResult) },
                results.Select(result => result.GetType())
            );
        }

        [Test]
        public void ProcessScheduledEvents_PlacementBeforeTransit_UsesPlacedPlanetAsDeparture()
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
            GameEventCommands executor = new GameEventCommands(
                game,
                game.Random,
                new GameActionCommands(game, game.Random, movementCommands: movement)
            );

            executor.ProcessScheduledEvents(new List<GameEvent> { gameEvent });

            Assert.AreSame(destination, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
            Assert.AreEqual(middle.GetPosition(), officer.Movement.OriginPosition);
            Assert.AreEqual(gameEvent.InstanceID, officer.Movement.SourceEventInstanceID);
        }

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

        [Test]
        public void ValidateEvents_DuplicateBindingAlias_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "INVALID_BINDING",
                Triggers = new List<GameTrigger>
                {
                    new UnitArrivedTrigger { Bindings = TriggerBindings(("Unit", "target")) },
                },
                Bindings = new List<GameBinding>
                {
                    new GameBinding
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

        [Test]
        public void ValidateEvents_BindingWithoutAlias_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "INVALID_BINDING",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 1 } },
                Bindings = new List<GameBinding>
                {
                    new GameBinding
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

        [Test]
        public void ValidateEvents_BindingWithoutSource_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "INVALID_BINDING",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 1 } },
                Bindings = new List<GameBinding> { new GameBinding { As = "target" } },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _system.ValidateEvents(new[] { gameEvent })
            );

            StringAssert.Contains("requires exactly one source", exception.Message);
        }

        [Test]
        public void ValidateEvents_MultipleTriggersWithDifferentAliases_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "MULTI_TRIGGER",
                Triggers = new List<GameTrigger>
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

        [Test]
        public void ValidateEvents_MultipleFilteredTriggersWithSameAlias_DoesNotThrow()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "MULTI_TRIGGER",
                Triggers = new List<GameTrigger>
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

        [Test]
        public void ProcessScheduledEvents_UnmetOneShotEvent_RemainsPending()
        {
            GameEvent gameEvent = CreateTickEvent("PENDING", targetTick: 10, repeatable: false);
            _game.CurrentTick = 9;
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.Contains(gameEvent, _game.GetEventPool().ToList());
            Assert.IsFalse(_game.EventRuntime.GetState(gameEvent.InstanceID).IsComplete);
        }

        [Test]
        public void ProcessScheduledEvents_MetOneShotEvent_CompletesAndLeavesPool()
        {
            GameEvent gameEvent = CreateTickEvent("ONE_SHOT", targetTick: 10, repeatable: false);
            _game.CurrentTick = 11;
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.IsFalse(_game.GetEventPool().Contains(gameEvent));
            Assert.IsTrue(_game.EventRuntime.GetState(gameEvent.InstanceID).IsComplete);
        }

        [Test]
        public void ProcessScheduledEvents_MetRepeatableEvent_CompletesAndRemainsActive()
        {
            GameEvent gameEvent = CreateTickEvent("REPEATABLE", targetTick: 10, repeatable: true);
            _game.CurrentTick = 11;
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.Contains(gameEvent, _game.GetEventPool().ToList());
            Assert.IsFalse(_game.EventRuntime.GetState(gameEvent.InstanceID).IsComplete);
        }

        [Test]
        public void ProcessScheduledEvents_RecurringScheduleUntilMet_CompletesAndRemovesEvent()
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

            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(0, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
            Assert.IsTrue(_game.EventRuntime.GetState(gameEvent.InstanceID).IsComplete);
            Assert.IsFalse(_game.GetEventPool().Contains(gameEvent));
        }

        [Test]
        public void ProcessScheduledEvents_RecurringScheduleUntilMet_UsesEvaluationBinding()
        {
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet" };
            _game.AttachNode(sector, _game.Galaxy);
            _game.AttachNode(planet, sector);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "BOUND_UNTIL",
                Bindings = new List<GameBinding>
                {
                    new GameBinding
                    {
                        As = "planet",
                        Selectors = new List<GameSelector>
                        {
                            new SelectPlanets { InstanceID = planet.InstanceID },
                        },
                    },
                    new GameBinding
                    {
                        As = "rawResourceNodes",
                        Sources = new List<GameBindingSource>
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

            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(0, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
            Assert.IsTrue(_game.EventRuntime.GetState(gameEvent.InstanceID).IsComplete);
            Assert.IsFalse(_game.GetEventPool().Contains(gameEvent));
        }

        [Test]
        public void ProcessScheduledEvents_MaximumActivationsFive_ActivatesFiveTimes()
        {
            GameEvent gameEvent = CreateTickEvent("FIVE_RUNS", targetTick: 0, repeatable: false);
            gameEvent.MaximumActivations = 5;
            _game.CurrentTick = 1;
            _game.GetEventPool().Add(gameEvent);

            for (int iteration = 0; iteration < 6; iteration++)
                _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(5, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        [Test]
        public void ProcessScheduledEvents_MaximumActivationsThree_ActivatesThreeTimes()
        {
            GameEvent gameEvent = CreateTickEvent("THREE_RUNS", targetTick: 0, repeatable: false);
            gameEvent.MaximumActivations = 3;
            _game.CurrentTick = 1;
            _game.GetEventPool().Add(gameEvent);

            for (int iteration = 0; iteration < 4; iteration++)
                _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(3, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        [Test]
        public void ProcessScheduledEvents_RandomDelay_WaitsUntilRolledAbsoluteTick()
        {
            GameEvent gameEvent = CreateTickEvent("DELAYED", targetTick: 0, repeatable: false);
            gameEvent.MaximumActivations = null;
            gameEvent.Schedule = new GameEventSchedule
            {
                RandomDelay = new RandomDelay { MinimumTicks = 10, MaximumTicks = 14 },
            };
            _game.GetEventPool().Add(gameEvent);

            _game.CurrentTick = 11;
            _system.ProcessScheduledEvents(_game.GetEventPool());
            Assert.Contains(gameEvent, _game.GetEventPool().ToList());

            _game.CurrentTick = 12;
            _system.ProcessScheduledEvents(_game.GetEventPool());
            Assert.IsFalse(_game.GetEventPool().Contains(gameEvent));
            Assert.AreEqual(
                12,
                _game.EventRuntime.GetState(gameEvent.InstanceID).LastActivationTick
            );
        }

        [Test]
        public void ProcessScheduledEvents_RepeatDelay_PreventsActivationUntilCooldownExpires()
        {
            GameEvent gameEvent = CreateTickEvent("COOLDOWN", targetTick: 0, repeatable: true);
            gameEvent.Schedule = new GameEventSchedule { Every = new EveryTicks { Ticks = 5 } };
            _game.GetEventPool().Add(gameEvent);

            _game.CurrentTick = 1;
            _system.ProcessScheduledEvents(_game.GetEventPool());
            _game.CurrentTick = 5;
            _system.ProcessScheduledEvents(_game.GetEventPool());
            Assert.AreEqual(1, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);

            _game.CurrentTick = 6;
            _system.ProcessScheduledEvents(_game.GetEventPool());
            Assert.AreEqual(2, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        [Test]
        public void ProcessScheduledEvents_AfterSchedule_DelaysFromPredecessorActivation()
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
            _system.ProcessScheduledEvents(_game.GetEventPool());

            _game.CurrentTick = 24;
            _system.ProcessScheduledEvents(_game.GetEventPool());
            Assert.Contains(pending, _game.GetEventPool());

            _game.CurrentTick = 25;
            _system.ProcessScheduledEvents(_game.GetEventPool());
            Assert.IsFalse(_game.GetEventPool().Contains(pending));
        }

        [Test]
        public void ProcessScheduledEvents_AfterAllScheduleBeforeFinalDelay_KeepsEventPending()
        {
            GameEvent pending = CreateDependentEvent("AFTER_ALL", afterAll: true);
            _game.GetEventPool().Add(pending);

            _game.CurrentTick = 24;
            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.Contains(pending, _game.GetEventPool());
        }

        [Test]
        public void ProcessScheduledEvents_AfterAllScheduleAtFinalDelay_ActivatesEvent()
        {
            GameEvent pending = CreateDependentEvent("AFTER_ALL", afterAll: true);
            _game.GetEventPool().Add(pending);

            _game.CurrentTick = 25;
            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.IsFalse(_game.GetEventPool().Contains(pending));
        }

        [Test]
        public void ProcessScheduledEvents_AfterAnyScheduleBeforeFirstDelay_KeepsEventPending()
        {
            GameEvent pending = CreateDependentEvent("AFTER_ANY", afterAll: false);
            _game.GetEventPool().Add(pending);

            _game.CurrentTick = 14;
            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.Contains(pending, _game.GetEventPool());
        }

        [Test]
        public void ProcessScheduledEvents_AfterAnyScheduleAtFirstDelay_ActivatesEvent()
        {
            GameEvent pending = CreateDependentEvent("AFTER_ANY", afterAll: false);
            _game.GetEventPool().Add(pending);

            _game.CurrentTick = 15;
            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.IsFalse(_game.GetEventPool().Contains(pending));
        }

        [Test]
        public void ProcessScheduledEvents_ResultTriggeredEvent_DoesNotRunDuringScheduledPolling()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "RESULT_ONLY",
                Triggers = new List<GameTrigger> { new DuelCompletedTrigger() },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "unexpected", Operand = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.Zero(_game.EventRuntime.GetVariable("unexpected"));
            Assert.Contains(gameEvent, _game.GetEventPool().ToList());
        }

        [Test]
        public void ProcessScheduledEvents_TargetedPlanet_UsesOnePersistedSchedule()
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

                Bindings = new List<GameBinding>
                {
                    new GameBinding
                    {
                        As = "target",
                        Selectors = new List<GameSelector>
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
            _system.ProcessScheduledEvents(_game.GetEventPool());
            Assert.AreEqual(10, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
            Assert.AreEqual(10, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);

            _game.CurrentTick = 10;
            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(1, first.NumRawResourceNodes);
            Assert.Zero(second.NumRawResourceNodes);
            Assert.AreEqual(30, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
            Assert.AreEqual(30, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
        }

        [Test]
        public void ProcessScheduledEvents_EachOwnedPlanetTarget_ArmsWhenNeutralPlanetBecomesOwned()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "alliance" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            Planet planet = new Planet { InstanceID = "planet" };
            _game.AttachNode(planet, sector);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "OWNED_ONLY",

                Bindings = new List<GameBinding>
                {
                    new GameBinding
                    {
                        As = "target",
                        Selectors = new List<GameSelector> { new SelectPlanets() },
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
            _system.ProcessScheduledEvents(_game.GetEventPool());
            Assert.IsTrue(_game.EventRuntime.GetState(gameEvent.InstanceID).IsInitialized);

            planet.OwnerInstanceID = "alliance";
            _game.CurrentTick = 120;
            _system.ProcessScheduledEvents(_game.GetEventPool());

            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            Assert.AreEqual(150, state.NextEligibleTick);
            Assert.AreEqual(1, state.ActivationCount);
        }

        [Test]
        public void ProcessScheduledEvents_EachOwnedPlanetTarget_RearmsAfterNeutralInterval()
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

                Bindings = new List<GameBinding>
                {
                    new GameBinding
                    {
                        As = "target",
                        Selectors = new List<GameSelector> { new SelectPlanets() },
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
            _system.ProcessScheduledEvents(_game.GetEventPool());
            planet.OwnerInstanceID = null;
            _game.CurrentTick = 110;
            _system.ProcessScheduledEvents(_game.GetEventPool());
            planet.OwnerInstanceID = "empire";
            _game.CurrentTick = 120;
            _system.ProcessScheduledEvents(_game.GetEventPool());

            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            Assert.AreEqual(130, state.NextEligibleTick);
            Assert.AreEqual(1, state.ActivationCount);
        }

        [Test]
        public void ProcessScheduledEvents_OneShotTarget_ActivatesTargetOnce()
        {
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet" };
            _game.AttachNode(sector, _game.Galaxy);
            _game.AttachNode(planet, sector);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "ONE_SHOT_PER_PLANET",
                MaximumActivations = 1,
                Bindings = new List<GameBinding>
                {
                    new GameBinding
                    {
                        As = "target",
                        Selectors = new List<GameSelector> { new SelectPlanets() },
                    },
                },
                Actions = new List<GameAction>
                {
                    new ChangeRawResourceNodesAction { PlanetBinding = "target", Amount = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessScheduledEvents(_game.GetEventPool());
            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(1, planet.NumRawResourceNodes);
        }

        [Test]
        public void ProcessScheduledEvents_RandomTargetBeforeScheduledTick_DoesNotSelectTarget()
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
                Bindings = new List<GameBinding>
                {
                    new GameBinding
                    {
                        As = "target",
                        Selectors = new List<GameSelector>
                        {
                            new SelectRandom
                            {
                                Count = 1,
                                Selectors = new List<GameSelector>
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

            _system.ProcessScheduledEvents(_game.GetEventPool());

            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            Assert.IsTrue(state.IsInitialized);
            Assert.AreEqual(10, state.NextEligibleTick);
        }

        [Test]
        public void ProcessTriggeredEvents_MatchingEncounter_ActivatesResultTriggeredEventOnce()
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

            _system.ProcessTriggeredEvents(
                new[]
                {
                    new DuelResult { EncounteredOfficer = luke, OpposingOfficer = vader },
                }
            );

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("luke.heritage.revealed"));
            Assert.IsFalse(_game.GetEventPool().Contains(gameEvent));
            Assert.AreEqual(1, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        [Test]
        public void ProcessTriggeredEvents_StableTriggerId_ActivatesWithoutClrTypeName()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "ARRIVAL_REACTION",
                Triggers = new List<GameTrigger>
                {
                    new UnitArrivedTrigger
                    {
                        Bindings = TriggerBindings(
                            ("Unit", "arrivedUnit"),
                            ("Destination", "arrivalDestination")
                        ),
                    },
                },
                Bindings = new List<GameBinding>
                {
                    new GameBinding
                    {
                        As = "arrivalRating",
                        Sources = new List<GameBindingSource>
                        {
                            new SkillRatingBindingSource
                            {
                                OfficerBinding = "arrivedUnit",
                                Rating = SkillRating.Combat,
                            },
                        },
                    },
                    new GameBinding
                    {
                        As = "arrivalResources",
                        Sources = new List<GameBindingSource>
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

            _system.ProcessTriggeredEvents(
                new[]
                {
                    new UnitArrivedResult { Unit = officer, Destination = destination },
                }
            );

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("arrival.triggered"));
        }

        /// <summary>
        /// Verifies that categorized events retain their alternative arrival triggers.
        /// </summary>
        /// <param name="category">The classification that must not affect execution.</param>
        [TestCase(GameEventCategory.None)]
        [TestCase(GameEventCategory.Story)]
        [TestCase(GameEventCategory.Disaster)]
        public void ProcessTriggeredEvents_SecondUnitArrivedAlternativeMatches_ActivatesOnce(
            GameEventCategory category
        )
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "ALTERNATE_ARRIVALS",
                Category = category,
                Triggers = new List<GameTrigger>
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

            _system.ProcessTriggeredEvents(
                new[] { new UnitArrivedResult { Unit = new Officer { InstanceID = "second" } } }
            );

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("arrival.count"));
            Assert.AreEqual(1, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        [Test]
        public void ProcessTriggeredEvents_MatchingOptionalSourceBinding_ActivatesEvent()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "SOURCE_FILTERED_ARRIVAL",
                Triggers = new List<GameTrigger>
                {
                    new UnitArrivedTrigger { SourceEventInstanceID = "EXPECTED_SOURCE" },
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "source.arrival.triggered", Operand = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessTriggeredEvents(
                new[] { new UnitArrivedResult { SourceEventInstanceID = "EXPECTED_SOURCE" } }
            );

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("source.arrival.triggered"));
        }

        [Test]
        public void ProcessTriggeredEvents_WithoutSuppression_PreservesTriggerAndSiblingMessages()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "HIDDEN_MISSION_REPORT",
                Triggers = new List<GameTrigger> { new MissionCompletedTrigger() },
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

            List<GameResult> reactions = _system.ProcessTriggeredEvents(
                new GameResult[] { release, completion }
            );

            Assert.IsEmpty(reactions);
        }

        [Test]
        public void ProcessTriggeredEvents_RepeatableEncounterEffect_ActivatesForEveryEncounter()
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

            _system.ProcessTriggeredEvents(new[] { encounter });
            _system.ProcessTriggeredEvents(new[] { encounter });

            Assert.Contains(gameEvent, _game.GetEventPool().ToList());
            Assert.AreEqual(2, _game.EventRuntime.GetVariable("encounter.count"));
            Assert.AreEqual(2, _game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        [Test]
        public void ProcessScheduledEvents_MaximumActivationsReached_DoesNotActivate()
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

            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(0, _game.EventRuntime.GetVariable("activated"));
            Assert.AreEqual(3, state.ActivationCount);
        }

        [Test]
        public void ProcessScheduledEvents_UnlimitedEvent_ActivatesAgain()
        {
            GameEvent gameEvent = new GameEvent { InstanceID = "UNLIMITED" };
            _game.GetEventPool().Add(gameEvent);
            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            state.ActivationCount = 100;

            _system.ProcessScheduledEvents(_game.GetEventPool());

            Assert.AreEqual(101, state.ActivationCount);
        }

        [Test]
        public void ProcessScheduledEvents_PlaceUnitsMixedSources_PlacesExistingAndSpawnedUnits()
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
                Units = new List<GameSelector>
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

        [Test]
        public void ProcessScheduledEvents_ChangeOwnerUnitSelectors_TransfersSelectedUnit()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = new Officer { InstanceID = "officer", OwnerInstanceID = "empire" };
            game.AttachNode(officer, planet);
            ChangeOwnerAction action = new ChangeOwnerAction
            {
                FactionInstanceID = "rebels",
                Units = new List<GameSelector>
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

        [Test]
        public void ProcessScheduledEvents_DuelOfficersAtDifferentPlanets_ReturnsNoDuel()
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

        [Test]
        public void ProcessTriggeredEvents_DuelSecondOfficerParticipated_ReversesAuthoredOrder()
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

        [Test]
        public void ProcessScheduledEvents_DuelValidOfficers_ProducesDuelOutcome()
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

        [Test]
        public void ProcessScheduledEvents_SendMessageExplicitRecipient_EmitsResolvedResult()
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

        [Test]
        public void ProcessScheduledEvents_SendMessageOfficerSubject_DoesNotIncludeSubjectImageByDefault()
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

        [Test]
        public void ProcessScheduledEvents_SendMessageShowSubjectImage_IncludesOfficerMessageImage()
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

        [Test]
        public void ProcessScheduledEvents_SendMessageExplicitOverlayImage_UsesAuthoredImage()
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

        [Test]
        public void ProcessScheduledEvents_SendMessageInactiveSubject_EmitsResolvedResult()
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

        [Test]
        public void ProcessTriggeredEvents_SendMessageAudioBinding_UsesTriggerBindingPath()
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
                Bindings = new List<GameBinding>
                {
                    new GameBinding { Argument = "AudioPath", As = "audioPath" },
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

        [Test]
        public void ProcessScheduledEvents_SendMessageOfficerVoicePreset_UsesSubjectVoiceSet()
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

        [Test]
        public void ProcessScheduledEvents_SendUnitsFirstDestinationRejected_UsesNextCandidate()
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
                Destination = new List<GameSelector>
                {
                    new SelectFirst
                    {
                        Selectors = new List<GameSelector>
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

        [Test]
        public void ProcessScheduledEvents_FutureActivation_EvaluatesBindingRoll()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            QueueRNG random = new QueueRNG(0.25, 0.75);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "future",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 10 } },
                Bindings = new List<GameBinding>
                {
                    new GameBinding
                    {
                        As = "count",
                        RollInteger = new RollInteger { Minimum = 0, Maximum = 3 },
                    },
                },
            };
            game.GetEventPool().Add(gameEvent);

            new GameEventCommands(
                game,
                random,
                new GameActionCommands(game, random)
            ).ProcessScheduledEvents(game.GetEventPool());

            Assert.AreEqual(0.75, random.NextDouble());
            Assert.Zero(game.EventRuntime.GetState(gameEvent.InstanceID).ActivationCount);
        }

        [Test]
        public void ProcessScheduledEvents_BindingThrows_DoesNotExecuteFollowingEvent()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameEvent invalid = new GameEvent
            {
                InstanceID = "invalid-binding",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 0 } },
                Bindings = new List<GameBinding> { new GameBinding { As = "missing" } },
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
            GameEventCommands executor = new GameEventCommands(
                game,
                game.Random,
                new GameActionCommands(game, game.Random)
            );

            Assert.Throws<InvalidOperationException>(() =>
                executor.ProcessScheduledEvents(game.GetEventPool())
            );

            Assert.Zero(game.EventRuntime.GetVariable("after"));
            Assert.Zero(game.EventRuntime.GetState(invalid.InstanceID).ActivationCount);
        }

        /// <summary>
        /// Builds an eligible duel followed by an authored message for deferred failure tests.
        /// </summary>
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

        /// <summary>
        /// Fails after capture to exercise partial-mutation failure handling.
        /// </summary>
        private sealed class CaptureThenThrowRandom : IRandomNumberProvider
        {
            private int _rolls;

            /// <summary>
            /// Rejects unexpected floating-point draws in the capture path.
            /// </summary>
            /// <returns>No value; this path always throws.</returns>
            public double NextDouble() =>
                throw new InvalidOperationException("Unexpected random draw.");

            /// <summary>
            /// Fails capture avoidance, avoids the initial injury, then throws on fallback injury.
            /// </summary>
            /// <param name="min">The requested inclusive lower bound.</param>
            /// <param name="max">The requested exclusive upper bound.</param>
            /// <returns>The maximum allowed value on the first two calls.</returns>
            public int NextInt(int min, int max) =>
                _rolls++ < 2 ? max - 1 : throw new InvalidOperationException("Injury roll failed.");
        }

        /// <summary>
        /// Executes one authored action through the public event-processing contract.
        /// </summary>
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
            GameTrigger trigger = null
        )
        {
            random ??= new FixedRNG(0.99);
            MovementCommands movement = CreateMovementCommands(game);
            PlanetaryControlCommands control = new PlanetaryControlCommands(
                game,
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            GameEventCommands executor = new GameEventCommands(
                game,
                random,
                new GameActionCommands(
                    game,
                    random,
                    unitFactory,
                    movement,
                    control,
                    new DuelCommands(game, random),
                    new MessageCommands(game, new MessageFactory(null))
                )
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
                ? executor.ProcessTriggeredEvents(new[] { triggerResult })
                : executor.ProcessScheduledEvents(game.GetEventPool());
        }

        /// <summary>
        /// Builds movement dependencies for authored movement and ownership tests.
        /// </summary>
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
        private static List<GameTrigger> EncounterTrigger() =>
            new List<GameTrigger>
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
        private static List<GameBinding> TriggerBindings(
            params (string Argument, string As)[] bindings
        ) =>
            bindings
                .Select(binding => new GameBinding { Argument = binding.Argument, As = binding.As })
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
            GameRoot game = TestGame.Create(config);
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
    }
}
