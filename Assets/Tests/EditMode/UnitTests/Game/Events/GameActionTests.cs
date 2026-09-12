using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameActionTests
    {
        [Test]
        public void ExecuteAll_ActionThrows_ExecutesRemainingActions()
        {
            GameRoot game = new GameRoot();
            GameEvent gameEvent = new GameEvent { InstanceID = "test-event" };
            GameEventEvaluationContext evaluation = new GameEventEvaluationContext(
                gameEvent,
                new GameEventState()
            );
            GameActionContext context = new GameActionContext(game, game.Random, evaluation);
            RecordingAction firstAction = new RecordingAction();
            RecordingAction finalAction = new RecordingAction();
            List<GameAction> actions = new List<GameAction>
            {
                firstAction,
                new ThrowingAction(),
                finalAction,
            };
            LogAssert.Expect(
                LogType.Error,
                new Regex("Event 'test-event' action 'ThrowingAction' failed:")
            );

            GameAction.ExecuteAll(actions, context);

            Assert.IsTrue(firstAction.Executed);
            Assert.IsTrue(finalAction.Executed);
        }

        private sealed class RecordingAction : GameAction
        {
            public bool Executed { get; private set; }

            internal override void Execute(GameActionContext context)
            {
                Executed = true;
            }
        }

        private sealed class ThrowingAction : GameAction
        {
            internal override void Execute(GameActionContext context)
            {
                throw new InvalidOperationException("test failure");
            }
        }
    }
}
