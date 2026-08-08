using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Galaxy;
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
            gameEvent.InitialDelayTicks = 10;
            gameEvent.InitialDelayRandomTicks = 4;
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
            gameEvent.RepeatDelayTicks = 5;
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
        public void HandleResults_MatchingEncounter_ExecutesResultTriggeredEventOnce()
        {
            Officer luke = new Officer { InstanceID = "luke" };
            Officer vader = new Officer { InstanceID = "vader" };
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "HERITAGE",
                TriggerResultType = nameof(OfficerEncounterResult),
                Conditionals = new List<GameConditional>
                {
                    new OfficerEncounterParticipantsConditional
                    {
                        EncounteredOfficerInstanceID = luke.InstanceID,
                        OpposingOfficerInstanceID = vader.InstanceID,
                    },
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "luke.heritage.revealed", Value = 1 },
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
        public void HandleResults_MatchingStoryCaptureOutcome_ExecutesAuthoredReaction()
        {
            Officer han = new Officer { InstanceID = "han" };
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "BOUNTY_FAILED",
                TriggerResultType = nameof(StoryCaptureResolvedResult),
                Conditionals = new List<GameConditional>
                {
                    new StoryCaptureOutcomeConditional
                    {
                        TargetOfficerInstanceID = han.InstanceID,
                        WasCaptured = false,
                    },
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "han.evaded", Value = 1 },
                },
            };
            _game.EventPool.Add(gameEvent);

            _system.HandleResults(
                new[]
                {
                    new StoryCaptureResolvedResult { Target = han, WasCaptured = false },
                }
            );

            Assert.AreEqual(1, _game.GetEventVariable("han.evaded"));
            Assert.IsFalse(_game.EventPool.Contains(gameEvent));
        }

        [Test]
        public void ProcessEvents_ResultTriggeredEvent_DoesNotRunDuringScheduledPolling()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "RESULT_ONLY",
                TriggerResultType = nameof(OfficerEncounterResult),
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "unexpected", Value = 1 },
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
                IsRepeatable = true,
                InitialDelayTicks = 10,
                RepeatDelayTicks = 20,
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
                IsRepeatable = true,
                InitialDelayTicks = 30,
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
                IsRepeatable = true,
                InitialDelayTicks = 30,
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
                IsRepeatable = repeatable,
                Conditionals = new List<GameConditional>
                {
                    new TickCountConditional
                    {
                        ConditionalType = "GreaterThan",
                        ConditionalValue = targetTick.ToString(),
                    },
                },
            };
        }

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
    }
}
