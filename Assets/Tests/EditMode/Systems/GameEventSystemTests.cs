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
            Assert.IsFalse(_game.IsEventComplete(gameEvent.InstanceID));
        }

        [Test]
        public void ProcessEvents_MetOneShotEvent_CompletesAndLeavesPool()
        {
            GameEvent gameEvent = CreateTickEvent("ONE_SHOT", targetTick: 10, repeatable: false);
            _game.CurrentTick = 11;
            _game.EventPool.Add(gameEvent);

            _system.ProcessEvents(_game.EventPool);

            Assert.IsFalse(_game.EventPool.Contains(gameEvent));
            Assert.IsTrue(_game.IsEventComplete(gameEvent.InstanceID));
        }

        [Test]
        public void ProcessEvents_MetRepeatableEvent_CompletesAndRemainsActive()
        {
            GameEvent gameEvent = CreateTickEvent("REPEATABLE", targetTick: 10, repeatable: true);
            _game.CurrentTick = 11;
            _game.EventPool.Add(gameEvent);

            _system.ProcessEvents(_game.EventPool);

            Assert.Contains(gameEvent, _game.EventPool);
            Assert.IsTrue(_game.IsEventComplete(gameEvent.InstanceID));
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
            Assert.AreEqual(12, _game.GetEventState(gameEvent.InstanceID).LastExecutionTick);
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
            Assert.AreEqual(1, _game.GetEventState(gameEvent.InstanceID).ExecutionCount);

            _game.CurrentTick = 6;
            _system.ProcessEvents(_game.EventPool);
            Assert.AreEqual(2, _game.GetEventState(gameEvent.InstanceID).ExecutionCount);
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
        public void HandleResults_MatchingEncounter_ExecutesResultTriggeredEventOnce()
        {
            Officer luke = new Officer { InstanceID = "luke" };
            Officer vader = new Officer { InstanceID = "vader" };
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "HERITAGE",
                RunsOnce = true,
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
                    new OfficerEncounterResult
                    {
                        EncounteredOfficer = luke,
                        OpposingOfficer = vader,
                    },
                }
            );

            Assert.AreEqual(1, _game.GetEventVariable("luke.heritage.revealed"));
            Assert.IsFalse(_game.EventPool.Contains(gameEvent));
            Assert.AreEqual(1, _game.GetEventState(gameEvent.InstanceID).ExecutionCount);
        }

        [Test]
        public void HandleResults_StableTriggerId_ExecutesWithoutClrTypeName()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "ARRIVAL_REACTION",
                Triggers = new List<GameEventTrigger>
                {
                    new GameEventTrigger
                    {
                        Event = "core:unit.arrived",
                        Bindings = new List<GameEventTriggerBinding>
                        {
                            new GameEventTriggerBinding { Argument = "Unit", As = "unit" },
                            new GameEventTriggerBinding
                            {
                                Argument = "Destination",
                                As = "destination",
                            },
                        },
                    },
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

            Assert.AreEqual(1, _game.GetEventVariable("arrival.triggered"));
        }

        [Test]
        public void HandleResults_SuppressNextMessage_EmitsExplicitInstruction()
        {
            Officer luke = new Officer { InstanceID = "luke" };
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "JABBA_CAPTURES_LUKE",
                RunsOnce = true,
                Triggers = new List<GameEventTrigger>
                {
                    new GameEventTrigger
                    {
                        Event = "core:officer.capture-changed",
                        Bindings = new List<GameEventTriggerBinding>
                        {
                            new GameEventTriggerBinding { Argument = "Officer", As = "officer" },
                            new GameEventTriggerBinding
                            {
                                Argument = "IsCaptured",
                                As = "isCaptured",
                            },
                            new GameEventTriggerBinding
                            {
                                Argument = "SourceEventInstanceID",
                                As = "sourceEvent",
                            },
                        },
                    },
                },
                Conditionals = new List<GameConditional>
                {
                    BindingEquals("sourceEvent", "LUKE_RESCUES_HAN_FROM_JABBA"),
                    BindingEquals("officer", luke.InstanceID),
                    BindingEquals("isCaptured", "true"),
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "jabba.captured.luke", Operand = 1 },
                    new SuppressNextMessageAction
                    {
                        MessageType = MessageResultType.OfficerCaptured,
                    },
                },
            };
            _game.EventPool.Add(gameEvent);
            OfficerCaptureStateResult unrelatedCapture = new OfficerCaptureStateResult
            {
                TargetOfficer = luke,
                IsCaptured = true,
                SourceEventInstanceID = "UNRELATED_MISSION",
            };
            OfficerCaptureStateResult palaceCapture = new OfficerCaptureStateResult
            {
                TargetOfficer = luke,
                IsCaptured = true,
                SourceEventInstanceID = "LUKE_RESCUES_HAN_FROM_JABBA",
            };
            MissionCompletedResult palaceMission = new MissionCompletedResult
            {
                SourceEventInstanceID = "LUKE_RESCUES_HAN_FROM_JABBA",
            };

            _system.HandleResults(new[] { unrelatedCapture });
            List<GameResult> reactions = _system.HandleResults(
                new GameResult[] { palaceCapture, palaceMission }
            );

            Assert.IsFalse(unrelatedCapture.SuppressDefaultMessage);
            Assert.IsFalse(palaceCapture.SuppressDefaultMessage);
            Assert.IsFalse(palaceMission.SuppressDefaultMessage);
            Assert.AreEqual(
                MessageResultType.OfficerCaptured,
                reactions.OfType<SuppressNextMessageResult>().Single().MessageType
            );
            Assert.AreEqual(1, _game.GetEventVariable("jabba.captured.luke"));
            Assert.IsFalse(_game.EventPool.Contains(gameEvent));
        }

        [Test]
        public void HandleResults_WithoutSuppression_PreservesTriggerAndSiblingMessages()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "HIDDEN_MISSION_REPORT",
                Triggers = new List<GameEventTrigger>
                {
                    new GameEventTrigger { Event = "core:mission.completed" },
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

            _system.HandleResults(new GameResult[] { release, completion });

            Assert.IsFalse(release.SuppressDefaultMessage);
            Assert.IsFalse(completion.SuppressDefaultMessage);
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
            OfficerEncounterResult encounter = new OfficerEncounterResult
            {
                EncounteredOfficer = luke,
                OpposingOfficer = vader,
            };

            _system.HandleResults(new[] { encounter });
            _system.HandleResults(new[] { encounter });

            Assert.Contains(gameEvent, _game.EventPool);
            Assert.AreEqual(2, _game.GetEventVariable("encounter.count"));
            Assert.AreEqual(2, _game.GetEventState(gameEvent.InstanceID).ExecutionCount);
        }

        [Test]
        public void ProcessEvents_ResultTriggeredEvent_DoesNotRunDuringScheduledPolling()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "RESULT_ONLY",
                Triggers = new List<GameEventTrigger>
                {
                    new GameEventTrigger { Event = "core:officer.encountered" },
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "unexpected", Operand = 1 },
                },
            };
            _game.EventPool.Add(gameEvent);

            _system.ProcessEvents(_game.EventPool);

            Assert.Zero(_game.GetEventVariable("unexpected"));
            Assert.Contains(gameEvent, _game.EventPool);
        }

        [Test]
        public void ProcessEvents_PlanetScope_MaintainsIndependentPersistedSchedules()
        {
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
                Scope = GameEventScope.EachPlanet,
                PlanetScopeOwnership = PlanetScopeOwnership.Owned,
                Schedule = new GameEventScheduler
                {
                    Every = new EveryTicks { Ticks = 20, InitialDelayTicks = 10 },
                },
                Actions = new List<GameAction> { new RecordScopedPlanetAction() },
            };
            _game.EventPool.Add(gameEvent);

            _game.CurrentTick = 100;
            _system.ProcessEvents(_game.EventPool);
            Assert.AreEqual(
                110,
                _game.GetEventState(gameEvent.InstanceID, first.InstanceID).NextEligibleTick
            );
            Assert.AreEqual(
                110,
                _game.GetEventState(gameEvent.InstanceID, second.InstanceID).NextEligibleTick
            );

            _game.CurrentTick = 110;
            _system.ProcessEvents(_game.EventPool);

            Assert.AreEqual(1, _game.GetEventVariable("scope.first"));
            Assert.AreEqual(1, _game.GetEventVariable("scope.second"));
            Assert.AreEqual(
                130,
                _game.GetEventState(gameEvent.InstanceID, first.InstanceID).NextEligibleTick
            );
            Assert.AreEqual(
                130,
                _game.GetEventState(gameEvent.InstanceID, second.InstanceID).NextEligibleTick
            );
        }

        [Test]
        public void ProcessEvents_OwnedPlanetScope_ArmsWhenNeutralPlanetBecomesOwned()
        {
            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            _game.AttachNode(system, _game.Galaxy);
            Planet planet = new Planet { InstanceID = "planet" };
            _game.AttachNode(planet, system);
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "OWNED_ONLY",
                Scope = GameEventScope.EachPlanet,
                PlanetScopeOwnership = PlanetScopeOwnership.Owned,
                Schedule = new GameEventScheduler
                {
                    Every = new EveryTicks { Ticks = 30, InitialDelayTicks = 30 },
                },
                Actions = new List<GameAction> { new RecordScopedPlanetAction() },
            };
            _game.EventPool.Add(gameEvent);

            _game.CurrentTick = 100;
            _system.ProcessEvents(_game.EventPool);
            Assert.IsFalse(_game.EventStates.ContainsKey("OWNED_ONLY"));

            planet.OwnerInstanceID = "alliance";
            _game.CurrentTick = 120;
            _system.ProcessEvents(_game.EventPool);

            GameEventState state = _game.GetEventState(gameEvent.InstanceID, planet.InstanceID);
            Assert.AreEqual(150, state.NextEligibleTick);
            Assert.Zero(state.ExecutionCount);
        }

        [Test]
        public void ProcessEvents_OwnedPlanetScope_RearmsAfterNeutralInterval()
        {
            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            _game.AttachNode(system, _game.Galaxy);
            Planet planet = new Planet { InstanceID = "planet" };
            _game.AttachNode(planet, system);
            planet.OwnerInstanceID = "alliance";
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "OWNED_ONLY",
                Scope = GameEventScope.EachPlanet,
                PlanetScopeOwnership = PlanetScopeOwnership.Owned,
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

            GameEventState state = _game.GetEventState(gameEvent.InstanceID, planet.InstanceID);
            Assert.IsTrue(state.IsScopeActive);
            Assert.AreEqual(150, state.NextEligibleTick);
            Assert.Zero(state.ExecutionCount);
        }

        private static GameEvent CreateTickEvent(string instanceId, int targetTick, bool repeatable)
        {
            return new GameEvent
            {
                InstanceID = instanceId,
                RunsOnce = !repeatable,
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
                new GameEventTrigger
                {
                    Event = "core:officer.encountered",
                    Bindings = new List<GameEventTriggerBinding>
                    {
                        new GameEventTriggerBinding { Argument = "Officer", As = "officer" },
                        new GameEventTriggerBinding { Argument = "Opponent", As = "opponent" },
                    },
                },
            };

        private static EvaluateBindingConditional BindingEquals(string name, string value) =>
            new EvaluateBindingConditional
            {
                Name = name,
                Comparison = EventVariableComparison.Equal,
                Value = value,
            };

        private sealed class RecordScopedPlanetAction : GameAction
        {
            public override List<GameResult> Execute(GameRoot game) => new List<GameResult>();

            public override List<GameResult> Execute(
                GameRoot game,
                IRandomNumberProvider provider,
                GameEventExecutionContext context
            )
            {
                Planet planet = context.GetScopeTarget<Planet>();
                game.SetEventVariable(
                    $"scope.{planet.InstanceID}",
                    game.GetEventVariable($"scope.{planet.InstanceID}") + 1
                );
                return new List<GameResult>();
            }
        }

        private sealed class HasArrivalBindingsConditional : GameConditional
        {
            public override bool IsMet(GameRoot game) => false;

            public override bool IsMet(GameRoot game, GameEventExecutionContext context) =>
                context.GetBinding<Officer>("unit") != null
                && context.GetBinding<Planet>("destination") != null;
        }
    }
}
