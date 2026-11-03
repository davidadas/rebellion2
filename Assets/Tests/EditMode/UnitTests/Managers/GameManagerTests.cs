using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class GameManagerTests
    {
        [Test]
        public void GetPlayerRequestedGameSpeed_UncoordinatedClock_ReturnsEffectiveSpeed()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.SetFactionController("human", "player", PlayerControllerType.Human);
            game.GetFactionPlayer("human").RequestedGameSpeed = TickSpeed.Paused;
            using GameSession session = new GameSession(game, TestGameData.Create(game.Config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);

            Assert.AreEqual(TickSpeed.Fast, manager.GetPlayerRequestedGameSpeed());
        }

        [TestCase(TickSpeed.Paused)]
        [TestCase(TickSpeed.VerySlow)]
        public void GetPlayerRequestedGameSpeed_DifferentOtherParticipant_ReturnsLocalPreference(
            TickSpeed otherSpeed
        )
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions()
                .AddRange(
                    new[]
                    {
                        new Faction { InstanceID = "first" },
                        new Faction { InstanceID = "second" },
                    }
                );
            game.SetFactionController("first", "first-player", PlayerControllerType.Human);
            game.SetFactionController("second", "second-player", PlayerControllerType.Human);
            game.LocalPlayerFactionID = "second";
            using GameSession session = new GameSession(game, TestGameData.Create(game.Config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            manager.EnableCoordinatedSpeeds();
            manager.ApplyPlayerGameSpeed("first", otherSpeed);

            Assert.AreEqual(otherSpeed, manager.GetGameSpeed());
            Assert.AreEqual(TickSpeed.Fast, manager.GetPlayerRequestedGameSpeed());
            game.LocalPlayerFactionID = "first";
            Assert.AreEqual(otherSpeed, manager.GetPlayerRequestedGameSpeed());
        }

        [TestCase(TickSpeed.Paused, TickSpeed.Fast, TickSpeed.Paused)]
        [TestCase(TickSpeed.Medium, TickSpeed.Fast, TickSpeed.Medium)]
        [TestCase(TickSpeed.Fast, TickSpeed.Slow, TickSpeed.Slow)]
        [TestCase(TickSpeed.Fast, TickSpeed.Paused, TickSpeed.Paused)]
        [TestCase(TickSpeed.Slow, TickSpeed.VerySlow, TickSpeed.VerySlow)]
        public void ApplyPlayerGameSpeed_OneSelectionDiffers_AppliesSlowerPreference(
            TickSpeed initial,
            TickSpeed requested,
            TickSpeed expected
        )
        {
            using GameSession session = CreateTwoPlayerSession(initial);
            GameManager manager = new(() => session.Game, session.Tick);
            manager.EnableCoordinatedSpeeds();

            manager.ApplyPlayerGameSpeed("first", requested);

            Assert.AreEqual(expected, manager.GetGameSpeed());
            Assert.AreEqual(requested, session.Game.GetFactionPlayer("first").RequestedGameSpeed);
        }

        [Test, Combinatorial]
        public void ApplyPlayerGameSpeed_AllPreferencesAndArrivalOrders_UsesSlowestSelection(
            [Values] TickSpeed first,
            [Values] TickSpeed second,
            [Values(false, true)] bool reverse
        )
        {
            using GameSession session = CreateTwoPlayerSession(TickSpeed.Fast);
            GameManager manager = new(() => session.Game, session.Tick);
            manager.EnableCoordinatedSpeeds();
            manager.ApplyPlayerGameSpeed(reverse ? "second" : "first", reverse ? second : first);
            manager.ApplyPlayerGameSpeed(reverse ? "first" : "second", reverse ? first : second);
            TickSpeed[] order =
            {
                TickSpeed.Paused,
                TickSpeed.VerySlow,
                TickSpeed.Slow,
                TickSpeed.Medium,
                TickSpeed.Fast,
            };
            TickSpeed expected =
                Array.IndexOf(order, first) <= Array.IndexOf(order, second) ? first : second;

            Assert.AreEqual(expected, manager.GetGameSpeed());
        }

        [TestCase(TickSpeed.Paused)]
        [TestCase(TickSpeed.VerySlow)]
        [TestCase(TickSpeed.Slow)]
        [TestCase(TickSpeed.Medium)]
        [TestCase(TickSpeed.Fast)]
        public void ApplyPlayerGameSpeed_BothSelectionsMatch_AppliesSelectedSpeed(
            TickSpeed requested
        )
        {
            using GameSession session = CreateTwoPlayerSession(TickSpeed.Medium);
            GameManager manager = new(() => session.Game, session.Tick);
            manager.EnableCoordinatedSpeeds();
            manager.ApplyPlayerGameSpeed("first", requested);

            manager.ApplyPlayerGameSpeed("second", requested);

            Assert.AreEqual(requested, manager.GetGameSpeed());
        }

        [TestCase(true, TickSpeed.Fast)]
        [TestCase(false, TickSpeed.Paused)]
        public void TryRespondToGameSpeed_CurrentProposal_AppliesAcceptedOrDeclinedChoice(
            bool accept,
            TickSpeed expected
        )
        {
            using GameSession session = CreateTwoPlayerSession(TickSpeed.Paused);
            GameManager manager = new(() => session.Game, session.Tick);
            manager.EnableCoordinatedSpeeds();
            manager.ApplyPlayerGameSpeed("first", TickSpeed.Fast);
            long revision = session.Game.GetFactionPlayer("first").GameSpeedSelectionRevision;

            Assert.IsTrue(
                manager.TryRespondToGameSpeed("second", "first", TickSpeed.Fast, revision, accept)
            );

            Assert.AreEqual(expected, manager.GetGameSpeed());
            Assert.AreEqual(expected, session.Game.GetFactionPlayer("first").RequestedGameSpeed);
            Assert.AreEqual(expected, session.Game.GetFactionPlayer("second").RequestedGameSpeed);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TryRespondToGameSpeed_ReplacedProposal_RejectsStaleResponse(bool accept)
        {
            using GameSession session = CreateTwoPlayerSession(TickSpeed.Paused);
            GameManager manager = new(() => session.Game, session.Tick);
            manager.EnableCoordinatedSpeeds();
            manager.ApplyPlayerGameSpeed("first", TickSpeed.Fast);
            long revision = session.Game.GetFactionPlayer("first").GameSpeedSelectionRevision;
            manager.ApplyPlayerGameSpeed("first", TickSpeed.Slow);
            manager.ApplyPlayerGameSpeed("first", TickSpeed.Fast);

            Assert.IsFalse(
                manager.TryRespondToGameSpeed("second", "first", TickSpeed.Fast, revision, accept)
            );

            Assert.AreEqual(TickSpeed.Paused, manager.GetGameSpeed());
            Assert.AreEqual(
                TickSpeed.Fast,
                session.Game.GetFactionPlayer("first").RequestedGameSpeed
            );
        }

        [TestCase("first")]
        [TestCase("unknown")]
        public void TryRespondToGameSpeed_NonOpposingActor_RejectsResponse(string actor)
        {
            using GameSession session = CreateTwoPlayerSession(TickSpeed.Paused);
            GameManager manager = new(() => session.Game, session.Tick);
            manager.EnableCoordinatedSpeeds();
            manager.ApplyPlayerGameSpeed("first", TickSpeed.Fast);

            Assert.IsFalse(
                manager.TryRespondToGameSpeed(
                    actor,
                    "first",
                    TickSpeed.Fast,
                    session.Game.GetFactionPlayer("first").GameSpeedSelectionRevision,
                    true
                )
            );
            Assert.AreEqual(TickSpeed.Paused, manager.GetGameSpeed());
        }

        [Test]
        public void TryRespondToGameSpeed_DecliningWithOwnProposal_PreservesOwnSelection()
        {
            using GameSession session = CreateTwoPlayerSession(TickSpeed.Paused);
            GameManager manager = new(() => session.Game, session.Tick);
            manager.EnableCoordinatedSpeeds();
            manager.ApplyPlayerGameSpeed("first", TickSpeed.Fast);
            manager.ApplyPlayerGameSpeed("second", TickSpeed.Slow);

            Assert.IsTrue(
                manager.TryRespondToGameSpeed(
                    "second",
                    "first",
                    TickSpeed.Fast,
                    session.Game.GetFactionPlayer("first").GameSpeedSelectionRevision,
                    false
                )
            );

            Assert.AreEqual(TickSpeed.Slow, manager.GetGameSpeed());
            Assert.AreEqual(
                TickSpeed.Slow,
                session.Game.GetFactionPlayer("second").RequestedGameSpeed
            );
        }

        [Test]
        public void StepGameSpeed_OtherPlayerRemainsPaused_AdvancesOwnSelectionThroughEverySpeed()
        {
            using GameSession session = CreateTwoPlayerSession(TickSpeed.Paused);
            GameManager manager = new(() => session.Game, session.Tick);
            manager.EnableCoordinatedSpeeds();
            manager.BindSpeedRequests(speed => manager.ApplyPlayerGameSpeed("first", speed));

            foreach (
                TickSpeed expected in new[]
                {
                    TickSpeed.VerySlow,
                    TickSpeed.Slow,
                    TickSpeed.Medium,
                    TickSpeed.Fast,
                }
            )
            {
                manager.StepGameSpeed(true);
                Assert.AreEqual(expected, manager.GetPlayerRequestedGameSpeed());
                Assert.AreEqual(TickSpeed.Paused, manager.GetGameSpeed());
            }
        }

        [Test]
        public void StepGameSpeed_OwnSelectionAboveSharedSpeed_DecreasesOwnSelection()
        {
            using GameSession session = CreateTwoPlayerSession(TickSpeed.Paused);
            GameManager manager = new(() => session.Game, session.Tick);
            manager.EnableCoordinatedSpeeds();
            manager.BindSpeedRequests(speed => manager.ApplyPlayerGameSpeed("first", speed));
            manager.ApplyPlayerGameSpeed("first", TickSpeed.Fast);

            manager.StepGameSpeed(false);

            Assert.AreEqual(TickSpeed.Medium, manager.GetPlayerRequestedGameSpeed());
        }

        [TestCase(TickSpeed.Fast, 2.5f)]
        [TestCase(TickSpeed.Medium, 12.5f)]
        [TestCase(TickSpeed.Slow, 90.5f)]
        [TestCase(TickSpeed.VerySlow, 120.5f)]
        public void SetGameSpeed_ConfiguredInterval_ControlsTickReadiness(
            TickSpeed speed,
            float interval
        )
        {
            GameConfig config = TestConfig.Create();
            config.GameSpeed.FastTickIntervalSeconds = 2.5f;
            config.GameSpeed.MediumTickIntervalSeconds = 12.5f;
            config.GameSpeed.SlowTickIntervalSeconds = 90.5f;
            config.GameSpeed.VerySlowTickIntervalSeconds = 120.5f;
            GameRoot game = TestGame.Create(config);
            using GameSession session = new GameSession(game, TestGameData.Create(config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(speed);

            Assert.IsFalse(manager.TryAdvanceTickTimer(interval / 2f));
            Assert.IsTrue(manager.TryAdvanceTickTimer(interval / 2f));
        }

        [Test]
        public void SetGameSpeed_PartialVerySlowTickChangedToMedium_PreservesFractionalProgress()
        {
            GameConfig config = new GameConfig();
            config.GameSpeed.VerySlowTickIntervalSeconds = 120f;
            config.GameSpeed.MediumTickIntervalSeconds = 10f;
            GameRoot game = TestGame.Create(config);
            GameTickProcessor tick = new GameTickProcessor(
                (results, _) => new List<GameResult>(results),
                _ => { }
            );
            GameManager manager = new GameManager(() => game, tick);
            manager.SetGameSpeed(TickSpeed.VerySlow);
            Assert.IsFalse(manager.TryAdvanceTickTimer(60f));

            manager.SetGameSpeed(TickSpeed.Medium);

            Assert.IsFalse(manager.TryAdvanceTickTimer(4.9f));
            Assert.IsTrue(manager.TryAdvanceTickTimer(0.1f));
        }

        [Test]
        public void SetGameSpeed_PartialVerySlowTickPausedThenMedium_PreservesFractionalProgress()
        {
            GameConfig config = new GameConfig();
            config.GameSpeed.VerySlowTickIntervalSeconds = 120f;
            config.GameSpeed.MediumTickIntervalSeconds = 10f;
            GameRoot game = TestGame.Create(config);
            GameTickProcessor tick = new GameTickProcessor(
                (results, _) => new List<GameResult>(results),
                _ => { }
            );
            GameManager manager = new GameManager(() => game, tick);
            manager.SetGameSpeed(TickSpeed.VerySlow);
            Assert.IsFalse(manager.TryAdvanceTickTimer(60f));
            manager.SetGameSpeed(TickSpeed.Paused);
            Assert.IsFalse(manager.TryAdvanceTickTimer(120f));

            manager.SetGameSpeed(TickSpeed.Medium);

            Assert.IsFalse(manager.TryAdvanceTickTimer(4.9f));
            Assert.IsTrue(manager.TryAdvanceTickTimer(0.1f));
        }

        [Test]
        public void TryAdvanceTickTimer_CompletedInterval_ProcessesTickAndRaisesTickCompleted()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "FACTION", DisplayName = "Faction" });
            GameSession session = TestContent.CreateGameSession(game);
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            int completedTicks = 0;
            session.Tick.TickCompleted += () => completedTicks++;

            if (manager.TryAdvanceTickTimer(config.GameSpeed.FastTickIntervalSeconds))
                session.Tick.ProcessTick();

            Assert.AreEqual(1, game.CurrentTick);
            Assert.AreEqual(1, completedTicks);
        }

        [Test]
        public void TryAdvanceTickTimer_BelowCompletedInterval_DoesNotProcessTick()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            GameSession session = TestContent.CreateGameSession(game);
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            int completedTicks = 0;
            session.Tick.TickCompleted += () => completedTicks++;

            if (manager.TryAdvanceTickTimer(config.GameSpeed.FastTickIntervalSeconds / 2f))
                session.Tick.ProcessTick();

            Assert.AreEqual(0, game.CurrentTick);
            Assert.AreEqual(0, completedTicks);
        }

        [Test]
        public void TryAdvanceTickTimer_InsideTickCompleted_RejectsTickWhileStateIsSettled()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameSession session = new(game, TestGameData.Create(game.Config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            bool? timerReady = null;
            bool? settled = null;
            session.Tick.TickCompleted += () =>
            {
                settled = session.Tick.IsSettled;
                timerReady = manager.TryAdvanceTickTimer(
                    game.Config.GameSpeed.FastTickIntervalSeconds
                );
            };

            session.Tick.ProcessTick();

            Assert.IsTrue(settled);
            Assert.IsFalse(timerReady);
        }

        [Test]
        public void TryAdvanceTickTimer_UnstartedIterator_DoesNotBlockClock()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameSession session = new(game, TestGameData.Create(game.Config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            IEnumerator tick = session.Tick.ProcessTickIncrementally();

            bool ready = manager.TryAdvanceTickTimer(game.Config.GameSpeed.FastTickIntervalSeconds);
            (tick as IDisposable)?.Dispose();

            Assert.IsTrue(ready);
            Assert.AreEqual(0, game.CurrentTick);
        }

        [Test]
        public void Reset_RejectedReplacement_KeepsClockBlockedUntilIteratorDisposal()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "AI", DisplayName = "AI" });
            GameSession session = new(game, TestGameData.Create(game.Config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            IEnumerator tick = session.Tick.ProcessTickIncrementally();
            Assert.IsTrue(tick.MoveNext());
            GameRoot replacement = TestGame.Create(game.Config);

            Assert.Throws<InvalidOperationException>(() => session.ReplaceGame(replacement));
            Assert.AreSame(game, session.Game);
            manager.Reset();
            manager.SetGameSpeed(TickSpeed.Fast);
            bool readyBeforeDisposal = manager.TryAdvanceTickTimer(
                game.Config.GameSpeed.FastTickIntervalSeconds
            );
            (tick as IDisposable)?.Dispose();
            session.ReplaceGame(replacement);
            manager.Reset();
            manager.SetGameSpeed(TickSpeed.Fast);
            bool readyAfterDisposal = manager.TryAdvanceTickTimer(
                game.Config.GameSpeed.FastTickIntervalSeconds
            );

            Assert.IsFalse(readyBeforeDisposal);
            Assert.IsTrue(readyAfterDisposal);
        }

        /// <summary>
        /// Creates an independent two-human graph at the requested agreed speed.
        /// </summary>
        /// <param name="speed">The initial shared speed.</param>
        /// <returns>The prepared session owned by the test.</returns>
        private static GameSession CreateTwoPlayerSession(TickSpeed speed)
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "first" });
            game.GetFactions().Add(new Faction { InstanceID = "second" });
            game.SetFactionController("first", "first-player", PlayerControllerType.Human);
            game.SetFactionController("second", "second-player", PlayerControllerType.Human);
            game.LocalPlayerFactionID = "first";
            game.SetGameSpeed(speed);
            return new GameSession(game, TestGameData.Create(game.Config));
        }
    }
}
