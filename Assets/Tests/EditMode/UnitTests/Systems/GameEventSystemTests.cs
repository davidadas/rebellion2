using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Sectors
{
    [TestFixture]
    public class GameEventSystemTests
    {
        private GameRoot _game;
        private GameEventSystem _system;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _system = new GameEventSystem(_game, new FixedRandomProvider(new[] { 0.5 }));
        }

        /// <summary>
        /// Verifies validate events multiple schedule modes throws invalid operation exception.
        /// </summary>
        [Test]
        public void ValidateEvents_MultipleScheduleModes_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "INVALID_SCHEDULE",
                Schedule = new GameEventScheduler
                {
                    At = new AtTick { Tick = 25 },
                    Every = new EveryTicks { Ticks = 5 },
                },
            };

            TestDelegate validate = () => _system.ValidateEvents(new[] { gameEvent });

            Assert.Throws<InvalidOperationException>(validate);
        }

        /// <summary>
        /// Verifies validate events one shot schedule without maximum activations does not throw.
        /// </summary>
        [Test]
        public void ValidateEvents_OneShotScheduleWithoutMaximumActivations_DoesNotThrow()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "ONE_SHOT",
                Schedule = new GameEventScheduler { At = new AtTick { Tick = 25 } },
            };

            Assert.DoesNotThrow(() => _system.ValidateEvents(new[] { gameEvent }));
        }

        /// <summary>
        /// Verifies validate events duplicate binding alias throws invalid operation exception.
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
        /// Verifies validate events binding without alias throws invalid operation exception.
        /// </summary>
        [Test]
        public void ValidateEvents_BindingWithoutAlias_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "INVALID_BINDING",
                Schedule = new GameEventScheduler { At = new AtTick { Tick = 1 } },
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
        /// Verifies validate events binding without source throws invalid operation exception.
        /// </summary>
        [Test]
        public void ValidateEvents_BindingWithoutSource_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "INVALID_BINDING",
                Schedule = new GameEventScheduler { At = new AtTick { Tick = 1 } },
                Bindings = new List<GameEventBinding> { new GameEventBinding { As = "target" } },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _system.ValidateEvents(new[] { gameEvent })
            );

            StringAssert.Contains("requires exactly one source", exception.Message);
        }

        /// <summary>
        /// Verifies validate events multiple triggers with different aliases throws invalid operation exception.
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
        /// Verifies validate events multiple filtered triggers with same alias does not throw.
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
        /// Verifies validate events dependency completed and removed from pool does not throw.
        /// </summary>
        [Test]
        public void ValidateEvents_DependencyCompletedAndRemovedFromPool_DoesNotThrow()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "FOLLOW_UP",
                Schedule = new GameEventScheduler
                {
                    After = new AfterEvent { EventInstanceID = "COMPLETED_EVENT", DelayTicks = 10 },
                },
            };
            _game.EventRuntime.GetState("COMPLETED_EVENT").IsComplete = true;

            Assert.DoesNotThrow(() => _system.ValidateEvents(new[] { gameEvent }));
        }

        /// <summary>
        /// Verifies validate events dependency missing from pool and not completed throws invalid operation exception.
        /// </summary>
        [Test]
        public void ValidateEvents_DependencyMissingFromPoolAndNotCompleted_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "FOLLOW_UP",
                Schedule = new GameEventScheduler
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
        /// Verifies process events unmet one shot event remains pending.
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
        /// Verifies process events met one shot event completes and leaves pool.
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
        /// Verifies process events met repeatable event completes and remains active.
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
        /// Verifies process events recurring schedule until met completes and removes event.
        /// </summary>
        [Test]
        public void ProcessEvents_RecurringScheduleUntilMet_CompletesAndRemovesEvent()
        {
            GameEvent gameEvent = CreateTickEvent("UNTIL_MET", targetTick: 0, repeatable: true);
            gameEvent.Schedule = new GameEventScheduler
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
        /// Verifies process events recurring schedule until met uses evaluation binding.
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
                Schedule = new GameEventScheduler
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
        /// Verifies process events maximum activations five activates five times.
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
        /// Verifies process events maximum activations three activates three times.
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
        /// Verifies process events random delay waits until rolled absolute tick.
        /// </summary>
        [Test]
        public void ProcessEvents_RandomDelay_WaitsUntilRolledAbsoluteTick()
        {
            GameEvent gameEvent = CreateTickEvent("DELAYED", targetTick: 0, repeatable: false);
            gameEvent.MaximumActivations = null;
            gameEvent.Schedule = new GameEventScheduler
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
        /// Verifies process events repeat delay prevents activation until cooldown expires.
        /// </summary>
        [Test]
        public void ProcessEvents_RepeatDelay_PreventsActivationUntilCooldownExpires()
        {
            GameEvent gameEvent = CreateTickEvent("COOLDOWN", targetTick: 0, repeatable: true);
            gameEvent.Schedule = new GameEventScheduler { Every = new EveryTicks { Ticks = 5 } };
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
        /// Verifies process events after schedule delays from predecessor activation.
        /// </summary>
        [Test]
        public void ProcessEvents_AfterSchedule_DelaysFromPredecessorActivation()
        {
            GameEvent predecessor = CreateTickEvent("DEPARTURE", targetTick: 19, repeatable: false);
            GameEvent pending = CreateTickEvent("PENDING_RETURN", targetTick: 0, repeatable: false);
            pending.Schedule = new GameEventScheduler
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
        /// Verifies process events after all schedule before final delay keeps event pending.
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
        /// Verifies process events after all schedule at final delay activates event.
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
        /// Verifies process events after any schedule before first delay keeps event pending.
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
        /// Verifies process events after any schedule at first delay activates event.
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
        /// Verifies process events result triggered event does not run during scheduled polling.
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
        /// Verifies process events targeted planet uses one persisted schedule.
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
                Schedule = new GameEventScheduler
                {
                    Every = new EveryTicks { Ticks = 20, InitialDelayTicks = 10 },
                },
                Actions = new List<GameAction> { new RecordScopedPlanetAction() },
            };
            _game.GetEventPool().Add(gameEvent);

            _game.CurrentTick = 0;
            _system.ProcessEvents(_game.GetEventPool());
            Assert.AreEqual(10, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
            Assert.AreEqual(10, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);

            _game.CurrentTick = 10;
            _system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("scope.first"));
            Assert.Zero(_game.EventRuntime.GetVariable("scope.second"));
            Assert.AreEqual(30, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
            Assert.AreEqual(30, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
        }

        /// <summary>
        /// Verifies process events each owned planet target arms when neutral planet becomes owned.
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
                Schedule = new GameEventScheduler
                {
                    Every = new EveryTicks { Ticks = 30, InitialDelayTicks = 30 },
                },
                Actions = new List<GameAction> { new RecordScopedPlanetAction() },
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
        /// Verifies process events each owned planet target rearms after neutral interval.
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
                Schedule = new GameEventScheduler
                {
                    Every = new EveryTicks { Ticks = 30, InitialDelayTicks = 30 },
                },
                Actions = new List<GameAction> { new RecordScopedPlanetAction() },
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
        /// Verifies process events one shot target activates target once.
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
                Actions = new List<GameAction> { new RecordScopedPlanetAction() },
            };
            _game.GetEventPool().Add(gameEvent);

            _system.ProcessEvents(_game.GetEventPool());
            _system.ProcessEvents(_game.GetEventPool());

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("scope.planet"));
        }

        /// <summary>
        /// Verifies process events random target before scheduled tick does not select target.
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
                Schedule = new GameEventScheduler { At = new AtTick { Tick = 10 } },
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
        /// Verifies handle results matching encounter activates result triggered event once.
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
        /// Verifies handle results stable trigger id activates without clr type name.
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
                Conditionals = new List<GameConditional> { new HasArrivalBindingsConditional() },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "arrival.triggered", Operand = 1 },
                },
            };
            _game.GetEventPool().Add(gameEvent);
            Planet destination = new Planet { InstanceID = "destination" };
            Officer officer = new Officer { InstanceID = "officer" };

            _system.HandleResults(
                new[]
                {
                    new UnitArrivedResult { Unit = officer, Destination = destination },
                }
            );

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("arrival.triggered"));
        }

        /// <summary>
        /// Verifies handle results second unit arrived alternative matches activates once.
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
        /// Verifies handle results matching optional source binding activates event.
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
        /// Verifies handle results without suppression preserves trigger and sibling messages.
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
        /// Verifies handle results repeatable encounter effect activates for every encounter.
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
        /// Verifies execute nested actions later action observes earlier result.
        /// </summary>
        [Test]
        public void Execute_NestedActions_LaterActionObservesEarlierResult()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "COMPOSITE_RESULTS",
                Actions = new List<GameAction>
                {
                    new IfAction
                    {
                        Actions = new List<GameAction>
                        {
                            new EmitTestResultAction(),
                            new ObserveTestResultAction(),
                        },
                    },
                },
            };

            gameEvent.ExecuteActions(
                _game,
                _game.Random,
                new GameEventEvaluationContext(gameEvent, new GameEventState(), null)
            );

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("result.observed"));
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
            gameEvent.Schedule = new GameEventScheduler();
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

        private sealed class RecordScopedPlanetAction : GameAction
        {
            /// <summary>
            /// Executes the requested operation.
            /// </summary>
            /// <param name="context">The context.</param>
            internal override void Execute(GameActionContext context)
            {
                GameRoot game = context.Game;
                Planet planet = context.Evaluation.GetBinding<Planet>("target");
                game.EventRuntime.SetVariable(
                    $"scope.{planet.InstanceID}",
                    game.EventRuntime.GetVariable($"scope.{planet.InstanceID}") + 1
                );
            }
        }

        private sealed class HasArrivalBindingsConditional : GameConditional
        {
            /// <summary>
            /// Checks whether the condition is met.
            /// </summary>
            /// <param name="context">The context.</param>
            /// <returns>True when the condition is met; otherwise false.</returns>
            public override bool IsMet(GameConditionContext context) =>
                context.Evaluation?.GetBinding<IGameEntity>("arrivedUnit") is Officer
                && context.Evaluation.GetBinding<Planet>("arrivalDestination") != null;
        }

        private sealed class EmitTestResultAction : GameAction
        {
            /// <summary>
            /// Executes the requested operation.
            /// </summary>
            /// <param name="context">The context.</param>
            internal override void Execute(GameActionContext context) =>
                context.Record(new PlanetStatChangedResult());
        }

        private sealed class ObserveTestResultAction : GameAction
        {
            /// <summary>
            /// Executes the requested operation.
            /// </summary>
            /// <param name="context">The context.</param>
            internal override void Execute(GameActionContext context)
            {
                if (context.Evaluation.Results.Any())
                    context.Game.EventRuntime.SetVariable("result.observed", 1);
            }
        }
    }
}
