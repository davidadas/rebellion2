using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
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

        [Test]
        public void ExecuteAll_CaptureMissionInterruptionThrows_StopsBeforeNextAction()
        {
            GameRoot game = new GameRoot();
            game.GetFactions().Add(new Faction { InstanceID = "owner" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = "owner",
                IsColonized = true,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", "owner");
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(officer, planet);
            GameActionContext context = new GameActionContext(
                game,
                game.Random,
                captureMissionInterruptor: _ =>
                    throw new InvalidOperationException("interruption failed")
            );
            RecordingAction nextAction = new RecordingAction();
            List<GameAction> actions = new List<GameAction>
            {
                new SetCaptureStatusAction
                {
                    OfficerInstanceID = officer.InstanceID,
                    IsCaptured = true,
                    CaptorFactionInstanceID = "captor",
                },
                nextAction,
            };

            GameActionSystemException exception = Assert.Throws<GameActionSystemException>(() =>
                GameAction.ExecuteAll(actions, context)
            );

            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            Assert.IsFalse(nextAction.Executed);
            Assert.IsTrue(officer.IsCaptured);
            Assert.AreEqual(1, context.Results.Count);
            Assert.IsInstanceOf<OfficerCaptureStateResult>(context.Results[0]);
        }

        private sealed class RecordingAction : GameAction
        {
            public bool Executed { get; private set; }

            /// <summary>
            /// Executes the requested operation.
            /// </summary>
            /// <param name="context">The context.</param>
            internal override void Execute(GameActionContext context)
            {
                Executed = true;
            }
        }

        private sealed class ThrowingAction : GameAction
        {
            /// <summary>
            /// Executes the requested operation.
            /// </summary>
            /// <param name="context">The context.</param>
            internal override void Execute(GameActionContext context)
            {
                throw new InvalidOperationException("test failure");
            }
        }
    }
}
