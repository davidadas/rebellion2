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

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class GameEventSystemTests
    {
        private GameRoot _game;
        private GameEventSystem _system;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _system = new GameEventSystem(_game, new FixedRandomProvider(new[] { 0.5 }));
        }

        [Test]
        public void ProcessEvents_UnmetOneShotEvent_RemainsPending()
        {
            GameEvent gameEvent = CreateTickEvent("PENDING", targetTick: 10, repeatable: false);
            _game.CurrentTick = 9;
            _game.EventPool.Add(gameEvent);

            _system.ProcessEvents(_game.EventPool);

            Assert.Contains(gameEvent, _game.EventPool);
            Assert.IsFalse(_game.EventRuntime.GetState(gameEvent.InstanceID).IsExhausted);
        }

        [Test]
        public void ProcessEvents_MetOneShotEvent_CompletesAndLeavesPool()
        {
            GameEvent gameEvent = CreateTickEvent("ONE_SHOT", targetTick: 10, repeatable: false);
            _game.CurrentTick = 11;
            _game.EventPool.Add(gameEvent);

            _system.ProcessEvents(_game.EventPool);

            Assert.IsFalse(_game.EventPool.Contains(gameEvent));
            Assert.IsTrue(_game.EventRuntime.GetState(gameEvent.InstanceID).IsExhausted);
        }

        [Test]
        public void ProcessEvents_MetRepeatableEvent_CompletesAndRemainsActive()
        {
            GameEvent gameEvent = CreateTickEvent("REPEATABLE", targetTick: 10, repeatable: true);
            _game.CurrentTick = 11;
            _game.EventPool.Add(gameEvent);

            _system.ProcessEvents(_game.EventPool);

            Assert.Contains(gameEvent, _game.EventPool);
            Assert.IsFalse(_game.EventRuntime.GetState(gameEvent.InstanceID).IsExhausted);
        }

        [Test]
        public void ProcessEvents_TriggerCountFive_ExecutesFiveTimes()
        {
            GameEvent gameEvent = CreateTickEvent("FIVE_RUNS", targetTick: 0, repeatable: false);
            gameEvent.TriggerCount = 5;
            _game.CurrentTick = 1;
            _game.EventPool.Add(gameEvent);

            for (int iteration = 0; iteration < 6; iteration++)
                _system.ProcessEvents(_game.EventPool);

            Assert.AreEqual(5, _game.EventRuntime.GetState(gameEvent.InstanceID).ExecutionCount);
        }

        [Test]
        public void ProcessEvents_TriggerCountThree_ExecutesThreeTimes()
        {
            GameEvent gameEvent = CreateTickEvent("THREE_RUNS", targetTick: 0, repeatable: false);
            gameEvent.TriggerCount = 3;
            _game.CurrentTick = 1;
            _game.EventPool.Add(gameEvent);

            for (int iteration = 0; iteration < 4; iteration++)
                _system.ProcessEvents(_game.EventPool);

            Assert.AreEqual(3, _game.EventRuntime.GetState(gameEvent.InstanceID).ExecutionCount);
        }

        [Test]
        public void ProcessEvents_InitialRandomDelay_WaitsUntilRolledAbsoluteTick()
        {
            GameEvent gameEvent = CreateTickEvent("DELAYED", targetTick: 0, repeatable: false);
            gameEvent.Schedule = new GameEventScheduler
            {
                Random = new RandomTickRange { MinimumTicks = 10, MaximumTicks = 14 },
            };
            _game.EventPool.Add(gameEvent);

            _game.CurrentTick = 11;
            _system.ProcessEvents(_game.EventPool);
            Assert.Contains(gameEvent, _game.EventPool);

            _game.CurrentTick = 12;
            _system.ProcessEvents(_game.EventPool);
            Assert.IsFalse(_game.EventPool.Contains(gameEvent));
            Assert.AreEqual(
                12,
                _game.EventRuntime.GetState(gameEvent.InstanceID).LastExecutionTick
            );
        }

        [Test]
        public void ProcessEvents_RepeatDelay_PreventsExecutionUntilCooldownExpires()
        {
            GameEvent gameEvent = CreateTickEvent("COOLDOWN", targetTick: 0, repeatable: true);
            gameEvent.Schedule = new GameEventScheduler { Every = new EveryTicks { Ticks = 5 } };
            _game.EventPool.Add(gameEvent);

            _game.CurrentTick = 1;
            _system.ProcessEvents(_game.EventPool);
            _game.CurrentTick = 5;
            _system.ProcessEvents(_game.EventPool);
            Assert.AreEqual(1, _game.EventRuntime.GetState(gameEvent.InstanceID).ExecutionCount);

            _game.CurrentTick = 6;
            _system.ProcessEvents(_game.EventPool);
            Assert.AreEqual(2, _game.EventRuntime.GetState(gameEvent.InstanceID).ExecutionCount);
        }

        [Test]
        public void ProcessEvents_AfterSchedule_DelaysFromPredecessorExecution()
        {
            GameEvent predecessor = CreateTickEvent("DEPARTURE", targetTick: 19, repeatable: false);
            GameEvent pending = CreateTickEvent("PENDING_RETURN", targetTick: 0, repeatable: false);
            pending.Schedule = new GameEventScheduler
            {
                After = new AfterEvent { EventInstanceID = predecessor.InstanceID, DelayTicks = 5 },
            };
            _game.EventPool.Add(predecessor);
            _game.EventPool.Add(pending);
            _game.CurrentTick = 20;
            _system.ProcessEvents(_game.EventPool);

            _game.CurrentTick = 24;
            _system.ProcessEvents(_game.EventPool);
            Assert.Contains(pending, _game.EventPool);

            _game.CurrentTick = 25;
            _system.ProcessEvents(_game.EventPool);
            Assert.IsFalse(_game.EventPool.Contains(pending));
        }

        [Test]
        public void ProcessEvents_AfterAllScheduleBeforeFinalDelay_KeepsEventPending()
        {
            GameEvent pending = CreateDependentEvent("AFTER_ALL", afterAll: true);
            _game.EventPool.Add(pending);

            _game.CurrentTick = 24;
            _system.ProcessEvents(_game.EventPool);

            Assert.Contains(pending, _game.EventPool);
        }

        [Test]
        public void ProcessEvents_AfterAllScheduleAtFinalDelay_ExecutesEvent()
        {
            GameEvent pending = CreateDependentEvent("AFTER_ALL", afterAll: true);
            _game.EventPool.Add(pending);

            _game.CurrentTick = 25;
            _system.ProcessEvents(_game.EventPool);

            Assert.IsFalse(_game.EventPool.Contains(pending));
        }

        [Test]
        public void ProcessEvents_AfterAnyScheduleBeforeFirstDelay_KeepsEventPending()
        {
            GameEvent pending = CreateDependentEvent("AFTER_ANY", afterAll: false);
            _game.EventPool.Add(pending);

            _game.CurrentTick = 14;
            _system.ProcessEvents(_game.EventPool);

            Assert.Contains(pending, _game.EventPool);
        }

        [Test]
        public void ProcessEvents_AfterAnyScheduleAtFirstDelay_ExecutesEvent()
        {
            GameEvent pending = CreateDependentEvent("AFTER_ANY", afterAll: false);
            _game.EventPool.Add(pending);

            _game.CurrentTick = 15;
            _system.ProcessEvents(_game.EventPool);

            Assert.IsFalse(_game.EventPool.Contains(pending));
        }

        [Test]
        public void HandleResults_MatchingEncounter_ExecutesResultTriggeredEventOnce()
        {
            Officer luke = new Officer { InstanceID = "luke" };
            Officer vader = new Officer { InstanceID = "vader" };
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "HERITAGE",
                TriggerCount = 1,
                Triggers = EncounterTrigger(),
                Conditionals = new List<GameConditional>
                {
                    BindingEquals("officer", luke.InstanceID),
                    BindingEquals("opponent", vader.InstanceID),
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "luke.heritage.revealed", Operand = 1 },
                },
            };
            _game.EventPool.Add(gameEvent);

            _system.HandleResults(
                new[]
                {
                    new DuelResult { EncounteredOfficer = luke, OpposingOfficer = vader },
                }
            );

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("luke.heritage.revealed"));
            Assert.IsFalse(_game.EventPool.Contains(gameEvent));
            Assert.AreEqual(1, _game.EventRuntime.GetState(gameEvent.InstanceID).ExecutionCount);
        }

        [Test]
        public void HandleResults_StableTriggerId_ExecutesWithoutClrTypeName()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "ARRIVAL_REACTION",
                Triggers = new List<GameEventTrigger>
                {
                    new GameEventTrigger(
                        "core:unit.arrived",
                        ("Unit", "unit"),
                        ("Destination", "destination")
                    ),
                },
                Conditionals = new List<GameConditional> { new HasArrivalBindingsConditional() },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "arrival.triggered", Operand = 1 },
                },
            };
            _game.EventPool.Add(gameEvent);
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

        [Test]
        public void HandleResults_MultipleTriggersWithDifferentAliases_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "MULTI_TRIGGER",
                Triggers = new List<GameEventTrigger>
                {
                    new GameEventTrigger(
                        "core:unit.arrived",
                        ("UnitInstanceID", "subjectInstanceID")
                    ),
                    new GameEventTrigger("core:duel.completed"),
                },
            };
            _game.EventPool.Add(gameEvent);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _system.HandleResults(new[] { new UnitArrivedResult() })
            );

            StringAssert.Contains("same binding aliases", exception.Message);
        }

        [Test]
        public void AvailableArguments_UnitArrivalTrigger_ExposesTypedContractMetadata()
        {
            GameEventTrigger trigger = new GameEventTrigger("core:unit.arrived");

            IReadOnlyDictionary<string, Type> arguments = trigger.AvailableArguments;

            Assert.AreEqual(typeof(IGameEntity), arguments["Unit"]);
            Assert.AreEqual(typeof(string), arguments["UnitInstanceID"]);
            Assert.AreEqual(typeof(Planet), arguments["Destination"]);
            Assert.AreEqual(typeof(string), arguments["DestinationInstanceID"]);
        }

        [Test]
        public void HandleResults_WithoutSuppression_PreservesTriggerAndSiblingMessages()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "HIDDEN_MISSION_REPORT",
                Triggers = new List<GameEventTrigger>
                {
                    new GameEventTrigger("core:mission.completed"),
                },
            };
            _game.EventPool.Add(gameEvent);
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

        [Test]
        public void HandleResults_RepeatableEncounterEffect_ExecutesForEveryEncounter()
        {
            Officer luke = new Officer { InstanceID = "luke" };
            Officer vader = new Officer { InstanceID = "vader" };
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "RECURRING_ENCOUNTER_EFFECTS",

                Triggers = EncounterTrigger(),
                Conditionals = new List<GameConditional>
                {
                    BindingEquals("officer", luke.InstanceID),
                    BindingEquals("opponent", vader.InstanceID),
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
            _game.EventPool.Add(gameEvent);
            DuelResult encounter = new DuelResult
            {
                EncounteredOfficer = luke,
                OpposingOfficer = vader,
            };

            _system.HandleResults(new[] { encounter });
            _system.HandleResults(new[] { encounter });

            Assert.Contains(gameEvent, _game.EventPool);
            Assert.AreEqual(2, _game.EventRuntime.GetVariable("encounter.count"));
            Assert.AreEqual(2, _game.EventRuntime.GetState(gameEvent.InstanceID).ExecutionCount);
        }

        [Test]
        public void ProcessEvents_ResultTriggeredEvent_DoesNotRunDuringScheduledPolling()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "RESULT_ONLY",
                Triggers = new List<GameEventTrigger>
                {
                    new GameEventTrigger("core:duel.completed"),
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "unexpected", Operand = 1 },
                },
            };
            _game.EventPool.Add(gameEvent);

            _system.ProcessEvents(_game.EventPool);

            Assert.Zero(_game.EventRuntime.GetVariable("unexpected"));
            Assert.Contains(gameEvent, _game.EventPool);
        }

        [Test]
        public void ProcessEvents_ForEachPlanets_UsesOnePersistedSchedule()
        {
            _game.Factions.Add(new Faction { InstanceID = "alliance" });
            _game.Factions.Add(new Faction { InstanceID = "empire" });
            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            _game.AttachNode(system, _game.Galaxy);
            Planet first = new Planet { InstanceID = "first" };
            Planet second = new Planet { InstanceID = "second" };
            _game.AttachNode(first, system);
            _game.AttachNode(second, system);
            first.OwnerInstanceID = "alliance";
            second.OwnerInstanceID = "empire";
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "SCOPED",

                ForEach = new GameEventForEach
                {
                    Selectors = new List<GameEventSelector> { new SelectPlanets() },
                },
                Conditionals = new List<GameConditional>
                {
                    new IsOwnedConditional { PlanetBinding = "$target" },
                },
                Schedule = new GameEventScheduler
                {
                    Every = new EveryTicks { Ticks = 20, InitialDelayTicks = 10 },
                },
                Actions = new List<GameAction> { new RecordScopedPlanetAction() },
            };
            _game.EventPool.Add(gameEvent);

            _game.CurrentTick = 0;
            _system.ProcessEvents(_game.EventPool);
            Assert.AreEqual(10, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
            Assert.AreEqual(10, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);

            _game.CurrentTick = 10;
            _system.ProcessEvents(_game.EventPool);

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("scope.first"));
            Assert.AreEqual(1, _game.EventRuntime.GetVariable("scope.second"));
            Assert.AreEqual(30, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
            Assert.AreEqual(30, _game.EventRuntime.GetState(gameEvent.InstanceID).NextEligibleTick);
        }

        [Test]
        public void ProcessEvents_EachOwnedPlanetTarget_ArmsWhenNeutralPlanetBecomesOwned()
        {
            _game.Factions.Add(new Faction { InstanceID = "alliance" });
            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            _game.AttachNode(system, _game.Galaxy);
            Planet planet = new Planet { InstanceID = "planet" };
            _game.AttachNode(planet, system);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "OWNED_ONLY",

                ForEach = new GameEventForEach
                {
                    Selectors = new List<GameEventSelector> { new SelectPlanets() },
                },
                Conditionals = new List<GameConditional>
                {
                    new IsOwnedConditional { PlanetBinding = "$target" },
                },
                Schedule = new GameEventScheduler
                {
                    Every = new EveryTicks { Ticks = 30, InitialDelayTicks = 30 },
                },
                Actions = new List<GameAction> { new RecordScopedPlanetAction() },
            };
            _game.EventPool.Add(gameEvent);

            _game.CurrentTick = 100;
            _system.ProcessEvents(_game.EventPool);
            Assert.IsTrue(_game.EventRuntime.GetState(gameEvent.InstanceID).IsInitialized);

            planet.OwnerInstanceID = "alliance";
            _game.CurrentTick = 120;
            _system.ProcessEvents(_game.EventPool);

            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            Assert.AreEqual(150, state.NextEligibleTick);
            Assert.AreEqual(1, state.ExecutionCount);
        }

        [Test]
        public void ProcessEvents_EachOwnedPlanetTarget_RearmsAfterNeutralInterval()
        {
            _game.Factions.Add(new Faction { InstanceID = "alliance" });
            _game.Factions.Add(new Faction { InstanceID = "empire" });
            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            _game.AttachNode(system, _game.Galaxy);
            Planet planet = new Planet { InstanceID = "planet" };
            _game.AttachNode(planet, system);
            planet.OwnerInstanceID = "alliance";
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "OWNED_ONLY",

                ForEach = new GameEventForEach
                {
                    Selectors = new List<GameEventSelector> { new SelectPlanets() },
                },
                Conditionals = new List<GameConditional>
                {
                    new IsOwnedConditional { PlanetBinding = "$target" },
                },
                Schedule = new GameEventScheduler
                {
                    Every = new EveryTicks { Ticks = 30, InitialDelayTicks = 30 },
                },
                Actions = new List<GameAction> { new RecordScopedPlanetAction() },
            };
            _game.EventPool.Add(gameEvent);

            _game.CurrentTick = 100;
            _system.ProcessEvents(_game.EventPool);
            planet.OwnerInstanceID = null;
            _game.CurrentTick = 110;
            _system.ProcessEvents(_game.EventPool);
            planet.OwnerInstanceID = "empire";
            _game.CurrentTick = 120;
            _system.ProcessEvents(_game.EventPool);

            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            Assert.AreEqual(130, state.NextEligibleTick);
            Assert.AreEqual(1, state.ExecutionCount);
        }

        [Test]
        public void ProcessEvents_OneShotForEachPlanets_ExecutesEachPlanetOnce()
        {
            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            Planet planet = new Planet { InstanceID = "planet" };
            _game.AttachNode(system, _game.Galaxy);
            _game.AttachNode(planet, system);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "ONE_SHOT_PER_PLANET",
                TriggerCount = 1,
                ForEach = new GameEventForEach
                {
                    Selectors = new List<GameEventSelector> { new SelectPlanets() },
                },
                Actions = new List<GameAction> { new RecordScopedPlanetAction() },
            };
            _game.EventPool.Add(gameEvent);

            _system.ProcessEvents(_game.EventPool);
            _system.ProcessEvents(_game.EventPool);

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("scope.planet"));
        }

        [Test]
        public void ProcessEvents_RandomTargetBeforeScheduledTick_DoesNotSelectTarget()
        {
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "system",
                SystemType = PlanetSystemType.CoreSystem,
            };
            _game.AttachNode(system, _game.Galaxy);
            _game.AttachNode(new Planet { InstanceID = "planet" }, system);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "DELAYED_RANDOM_TARGET",
                TriggerCount = 1,
                Schedule = new GameEventScheduler { At = new AtTick { Tick = 10 } },
                ForEach = new GameEventForEach
                {
                    Selectors = new List<GameEventSelector>
                    {
                        new SelectRandom
                        {
                            Count = 1,
                            Selectors = new List<GameEventSelector>
                            {
                                new SelectPlanets { SystemType = PlanetSystemType.CoreSystem },
                            },
                        },
                    },
                },
            };
            _game.EventPool.Add(gameEvent);
            _game.CurrentTick = 9;

            _system.ProcessEvents(_game.EventPool);

            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            Assert.IsTrue(state.IsInitialized);
            Assert.AreEqual(10, state.NextEligibleTick);
        }

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

            gameEvent.Execute(
                _game,
                _game.Random,
                new GameEventExecutionContext(gameEvent, new GameEventState(), null)
            );

            Assert.AreEqual(1, _game.EventRuntime.GetVariable("result.observed"));
        }

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
            first.ExecutionCount = 1;
            first.LastExecutionTick = 10;
            GameEventState second = _game.EventRuntime.GetState("SECOND");
            second.ExecutionCount = afterAll ? 1 : 0;
            second.LastExecutionTick = afterAll ? 20 : 0;

            GameEvent gameEvent = CreateTickEvent(instanceID, targetTick: 0, repeatable: false);
            gameEvent.Schedule = new GameEventScheduler();
            if (afterAll)
                gameEvent.Schedule.AfterAll = dependencies;
            else
                gameEvent.Schedule.AfterAny = dependencies;
            return gameEvent;
        }

        private static GameEvent CreateTickEvent(string instanceId, int targetTick, bool repeatable)
        {
            return new GameEvent
            {
                InstanceID = instanceId,
                TriggerCount = repeatable ? null : 1,
                Conditionals = new List<GameConditional>
                {
                    new TickCountConditional
                    {
                        Comparison = EventVariableComparison.GreaterThan,
                        Ticks = targetTick,
                    },
                },
            };
        }

        private static List<GameEventTrigger> EncounterTrigger() =>
            new List<GameEventTrigger>
            {
                new GameEventTrigger(
                    "core:duel.completed",
                    ("OfficerInstanceID", "officer"),
                    ("OpponentInstanceID", "opponent")
                ),
            };

        private static EvaluateBindingConditional BindingEquals(string name, string value) =>
            new EvaluateBindingConditional
            {
                Binding = "$" + name,
                Comparison = EventVariableComparison.Equal,
                CompareTo = value,
            };

        private sealed class RecordScopedPlanetAction : GameAction
        {
            public override List<GameResult> Execute(GameActionContext context)
            {
                GameRoot game = context.Game;
                Planet planet = context.Activation.GetTarget<Planet>();
                game.EventRuntime.SetVariable(
                    $"scope.{planet.InstanceID}",
                    game.EventRuntime.GetVariable($"scope.{planet.InstanceID}") + 1
                );
                return new List<GameResult>();
            }
        }

        private sealed class HasArrivalBindingsConditional : GameConditional
        {
            public override bool IsMet(GameConditionContext context) =>
                context.Activation?.GetBinding<Officer>("unit") != null
                && context.Activation.GetBinding<Planet>("destination") != null;
        }

        private sealed class EmitTestResultAction : GameAction
        {
            public override List<GameResult> Execute(GameActionContext context) =>
                new List<GameResult> { new PlanetStatChangedResult() };
        }

        private sealed class ObserveTestResultAction : GameAction
        {
            public override List<GameResult> Execute(GameActionContext context)
            {
                if (context.Activation.Results.Any())
                    context.Game.EventRuntime.SetVariable("result.observed", 1);
                return new List<GameResult>();
            }
        }
    }
}
