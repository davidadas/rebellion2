using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatCutsceneControllerTests
    {
        [Test]
        public void TacticalCombatCutsceneController_SuccessfulAttack_PlaysAuthoredSuccessMovie()
        {
            TacticalCombatSession session = CreateSession();
            string selected = null;
            using TacticalCombatCutsceneController controller = new(
                session,
                (path, _) =>
                {
                    selected = path;
                    return new Playback();
                },
                _ => { },
                _ => Assert.Fail("Playback should not fail."),
                completed => completed()
            );

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            Assert.AreEqual("Pack/Videos/success", selected);
        }

        [Test]
        public void TacticalCombatCutsceneController_FailedAttack_PlaysAuthoredFailureMovie()
        {
            TacticalCombatSession session = CreateSession();
            string selected = null;
            using TacticalCombatCutsceneController controller = new(
                session,
                (path, _) =>
                {
                    selected = path;
                    return new Playback();
                },
                _ => { },
                _ => Assert.Fail("Playback should not fail."),
                completed => completed()
            );

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Failed);

            Assert.AreEqual("Pack/Videos/failure", selected);
        }

        [Test]
        public void Dispose_ActiveMovie_CancelsItsPlaybackAndReleasesPause()
        {
            TacticalCombatSession session = CreateSession();
            Playback playback = new();
            TacticalCombatCutsceneController controller = new(
                session,
                (_, _) => playback,
                _ => { },
                _ => Assert.Fail("Playback should not fail."),
                completed => completed()
            );
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            controller.Dispose();

            Assert.AreEqual(1, playback.DisposeCount);
            Assert.IsFalse(session.IsPaused);
        }

        [Test]
        public void TacticalCombatCutsceneController_PlaybackCompletion_RestoresBattleVisibility()
        {
            TacticalCombatSession session = CreateSession();
            Action complete = null;
            List<bool> visibility = new();
            using TacticalCombatCutsceneController controller = new(
                session,
                (_, callback) =>
                {
                    complete = callback;
                    return new Playback();
                },
                visibility.Add,
                _ => Assert.Fail("Playback should not fail."),
                completed => completed()
            );
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            complete();

            CollectionAssert.AreEqual(new[] { true, false }, visibility);
            Assert.IsFalse(controller.IsPlaying);
        }

        [Test]
        public void TacticalCombatCutsceneController_SynchronousPlaybackCompletion_ReleasesReturnedOwnership()
        {
            TacticalCombatSession session = CreateSession();
            Playback playback = new();
            using TacticalCombatCutsceneController controller = new(
                session,
                (_, callback) =>
                {
                    callback();
                    return playback;
                },
                _ => { },
                _ => Assert.Fail("Playback should not fail."),
                completed => completed()
            );

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            Assert.AreEqual(1, playback.DisposeCount);
            Assert.IsFalse(session.IsPaused);
        }

        [Test]
        public void TacticalCombatCutsceneController_PlaybackFailure_DefersPauseReleaseUntilCommandReturns()
        {
            TacticalCombatSession session = CreateSession();
            Exception failure = new InvalidOperationException("Decoder unavailable.");
            Exception reported = null;
            Queue<Action> posted = new();
            using TacticalCombatCutsceneController controller = new(
                session,
                (_, _) => throw failure,
                _ => { },
                exception => reported = exception,
                posted.Enqueue
            );

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            Assert.AreSame(failure, reported);
            Assert.IsTrue(session.IsPaused);
            posted.Dequeue()();
            Assert.IsFalse(session.IsPaused);
        }

        [Test]
        public void TacticalCombatCutsceneController_StaleCompletion_DoesNotReleaseReplacementPause()
        {
            TacticalCombatSession session = CreateSession(reserveFighter: true);
            List<Action> completions = new();
            using TacticalCombatCutsceneController controller = new(
                session,
                (_, callback) =>
                {
                    completions.Add(callback);
                    return new Playback();
                },
                _ => { },
                _ => Assert.Fail("Playback should not fail."),
                completed => completed()
            );
            TacticalCombatAttackRun first = session.ActiveAttackRun;
            first.ApplyState(TacticalCombatAttackRunState.Failed);
            completions[0]();
            TacticalCombatGroup reserve = session.GetGroup(session.GetUnit("reserve"));
            session.SetOrder(reserve, TacticalCombatOrder.AttackBattleStation);
            for (
                int index = 0;
                index < 100 && ReferenceEquals(first, session.ActiveAttackRun);
                index++
            )
                session.Advance(50);
            Assert.AreSame(reserve, session.ActiveAttackRun.Group);
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Starting);
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Failed);
            Assert.AreEqual(2, completions.Count);

            completions[0]();

            Assert.IsTrue(session.IsPaused);
            completions[1]();
            Assert.IsFalse(session.IsPaused);
        }

        /// <summary>
        /// Records release of a fake video owned through the public playback contract.
        /// </summary>
        [Test]
        public void Dispose_ActiveModalInput_ReleasesBeforeBattleReactivation()
        {
            TacticalCombatSession session = CreateSession();
            List<string> events = new();
            using TacticalCombatMoviePresentation presentation = new(
                active => events.Add(active ? "show" : "hide"),
                () => events.Add("enter"),
                () => events.Add("leave")
            );
            Playback playback = new();
            TacticalCombatCutsceneController controller = new(
                session,
                (_, _) => playback,
                presentation.SetActive,
                _ => Assert.Fail("Playback should not fail."),
                completed => completed()
            );
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);
            events.Clear();

            controller.Dispose();

            CollectionAssert.AreEqual(new[] { "leave", "show" }, events);
            Assert.AreEqual(1, playback.DisposeCount);
            Assert.IsFalse(session.IsPaused);
        }

        [Test]
        public void TacticalCombatCutsceneController_PlaybackFailure_RetainsModalInputUntilDeferredAcknowledgement()
        {
            TacticalCombatSession session = CreateSession();
            List<string> events = new();
            using TacticalCombatMoviePresentation presentation = new(
                active => events.Add(active ? "show" : "hide"),
                () => events.Add("enter"),
                () => events.Add("leave")
            );
            Queue<Action> posted = new();
            Exception reported = null;
            using TacticalCombatCutsceneController controller = new(
                session,
                (_, _) => throw new InvalidOperationException("Decoder unavailable."),
                presentation.SetActive,
                failure => reported = failure,
                posted.Enqueue
            );
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);
            Assert.IsNotNull(reported);
            Assert.IsTrue(presentation.IsActive);
            Assert.IsTrue(session.IsPaused);
            events.Clear();

            posted.Dequeue()();

            CollectionAssert.AreEqual(new[] { "leave", "show" }, events);
            Assert.IsFalse(presentation.IsActive);
            Assert.IsFalse(session.IsPaused);
        }

        [Test]
        public void TacticalCombatCutsceneController_PlaybackCompletion_ReleasesModalInputBeforeBattleReactivation()
        {
            TacticalCombatSession session = CreateSession();
            List<string> events = new();
            using TacticalCombatMoviePresentation presentation = new(
                active => events.Add(active ? "show" : "hide"),
                () => events.Add("enter"),
                () => events.Add("leave")
            );
            Action completed = null;
            using TacticalCombatCutsceneController controller = new(
                session,
                (_, callback) =>
                {
                    completed = callback;
                    return new Playback();
                },
                presentation.SetActive,
                _ => Assert.Fail("Playback should not fail."),
                callback => callback()
            );
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);
            events.Clear();

            completed();

            CollectionAssert.AreEqual(new[] { "leave", "show" }, events);
            Assert.IsFalse(presentation.IsActive);
        }

        [Test]
        public void Dispose_PendingFailedMovie_ReleasesInputOnceBeforeStaleAcknowledgement()
        {
            TacticalCombatSession session = CreateSession();
            List<string> events = new();
            using TacticalCombatMoviePresentation presentation = new(
                active => events.Add(active ? "show" : "hide"),
                () => events.Add("enter"),
                () => events.Add("leave")
            );
            Queue<Action> posted = new();
            Exception reported = null;
            TacticalCombatCutsceneController controller = new(
                session,
                (_, _) => throw new InvalidOperationException("Decoder unavailable."),
                presentation.SetActive,
                failure => reported = failure,
                posted.Enqueue
            );
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);
            Assert.IsNotNull(reported);
            events.Clear();

            controller.Dispose();
            posted.Dequeue()();

            CollectionAssert.AreEqual(new[] { "leave", "show" }, events);
            Assert.IsFalse(presentation.IsActive);
            Assert.IsFalse(session.IsPaused);
        }

        [TestCase("completed")]
        [TestCase("cancelled")]
        [TestCase("failed")]
        public void TacticalCombatCutsceneController_MovieReleasePath_DoesNotResumePreMovieAudio(
            string releasePath
        )
        {
            TacticalCombatSession session = CreateSession();
            using TacticalCombatAudio audio = new(
                Array.Empty<TacticalCombatSoundDefinition>(),
                new SystemRandomProvider(42),
                new[]
                {
                    new TacticalCombatNotificationDefinition
                    {
                        MessageID = 32,
                        Variants = new() { new() { Path = "voice" } },
                    },
                },
                clock: () => 0
            );
            List<NotificationPlayback> sounds = new();
            audio.Playback = (_, _) =>
            {
                NotificationPlayback sound = new();
                sounds.Add(sound);
                return sound;
            };
            audio.EnqueueNotification(32);
            using TacticalCombatMoviePresentation presentation = new(
                active => audio.SetPlaybackAvailable(active),
                () => { },
                () => { }
            );
            Action completed = null;
            Queue<Action> posted = new();
            using TacticalCombatCutsceneController controller = new(
                session,
                (_, done) =>
                {
                    if (releasePath == "failed")
                        throw new InvalidOperationException("Decoder unavailable.");
                    completed = done;
                    return new Playback();
                },
                presentation.SetActive,
                _ => Assert.AreEqual("failed", releasePath),
                posted.Enqueue
            );

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            Assert.IsFalse(sounds[0].IsPlaying);
            Assert.AreEqual(1, sounds[0].StopCount);
            audio.EnqueueNotification(32);
            Assert.AreEqual(1, sounds.Count);
            if (releasePath == "completed")
                completed();
            else if (releasePath == "cancelled")
                controller.Dispose();
            else
                posted.Dequeue()();
            Assert.IsFalse(presentation.IsActive);
            audio.EnqueueNotification(32);
            Assert.AreEqual(2, sounds.Count);
            Assert.IsFalse(sounds[0].IsPlaying);
            Assert.IsTrue(sounds[1].IsPlaying);
            Assert.AreEqual(1, sounds[0].StopCount);
        }

        private sealed class NotificationPlayback : ITacticalCombatNotificationPlayback
        {
            public bool IsPlaying { get; private set; } = true;
            public int StopCount { get; private set; }

            /// <summary>
            /// Stops this independently owned notification playback.
            /// </summary>
            public void Stop()
            {
                StopCount++;
                IsPlaying = false;
            }
        }

        private sealed class Playback : IDisposable
        {
            public int DisposeCount { get; private set; }

            /// <summary>
            /// Creates an uncompleted playback ownership token.
            /// </summary>
            public Playback() { }

            /// <summary>
            /// Records release of the owned playback.
            /// </summary>
            public void Dispose() => DisposeCount++;
        }

        /// <summary>
        /// Creates a registered station attack with one surviving fighter squadron.
        /// </summary>
        /// <param name="reserveFighter">Whether to deploy an additional squadron in a separate group.</param>
        /// <returns>The battle after its attack run has entered the two-minute sequence.</returns>
        private static TacticalCombatSession CreateSession(bool reserveFighter = false)
        {
            TacticalCombatUnit fighter = new(
                new Starfighter
                {
                    InstanceID = "fighter",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    Agility = 20,
                    Torpedoes = 100,
                },
                CombatSide.Attacker,
                1
            );
            TacticalCombatUnit station = new(
                new CapitalShip
                {
                    InstanceID = "station",
                    CanDestroyPlanets = true,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    MaxHullStrength = 1000,
                    CurrentHullStrength = 1000,
                },
                CombatSide.Defender,
                1000001
            );
            TacticalCombatGroup group = new(2, new[] { fighter }, Vector3.UnitZ);
            List<TacticalCombatUnit> units = new() { fighter, station };
            List<TacticalCombatGroup> groups = new() { group };
            if (reserveFighter)
            {
                TacticalCombatUnit reserve = new(
                    new Starfighter
                    {
                        InstanceID = "reserve",
                        MaxSquadronSize = 12,
                        CurrentSquadronSize = 12,
                        Agility = 20,
                        Torpedoes = 100,
                    },
                    CombatSide.Attacker,
                    2
                );
                units.Add(reserve);
                groups.Add(new TacticalCombatGroup(3, new[] { reserve }, Vector3.UnitZ));
            }
            TacticalCombatSession session = new(
                units,
                groups,
                CreateSide(CombatSide.Attacker, 0, Vector3.UnitZ),
                CreateSide(CombatSide.Defender, 1, -Vector3.UnitZ),
                CombatSide.Attacker,
                100,
                new SystemRandomProvider(42),
                new TacticalCombatDefinition
                {
                    AttackRunSuccessMoviePath = "Pack/Videos/success",
                    AttackRunFailureMoviePath = "Pack/Videos/failure",
                }
            );
            session.SetOrder(group, TacticalCombatOrder.AttackBattleStation);
            for (int index = 0; index < 100 && session.ActiveAttackRun == null; index++)
                session.Advance(50);
            Assert.IsNotNull(session.ActiveAttackRun);
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Starting);
            return session;
        }

        /// <summary>
        /// Captures an unautomated side without command officers or retreat destinations.
        /// </summary>
        /// <param name="side">The deployment side.</param>
        /// <param name="faction">The authored faction index.</param>
        /// <param name="forward">The deployment heading.</param>
        /// <returns>The side's command state.</returns>
        private static TacticalCombatSide CreateSide(CombatSide side, int faction, Vector3 forward)
        {
            return new TacticalCombatSide(
                side,
                faction,
                forward,
                false,
                0,
                0,
                null,
                null,
                0,
                0,
                false
            );
        }
    }
}
