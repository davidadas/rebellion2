using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Tactical.Rendering;
using Rebellion.UI.SceneUI.TacticalCombat;
using Rebellion.Util.Random;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using TacticalCombatPlanner = global::Rebellion.AI.Tactical.TacticalCombatPlanner;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatControllerTests
    {
        private TacticalCombatController _controller;
        private TacticalCombatView _view;
        private TacticalCombatSession _session;

        /// <summary>
        /// Creates the controller and its test-owned screen.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _view = TacticalCombatViewFixture.Create();
            _session = CreateSession();
            _controller = new TacticalCombatController(
                _session,
                _view,
                _ => { },
                CancellationToken.None
            );
        }

        /// <summary>
        /// Releases the encounter and waits for its scene objects to be destroyed.
        /// </summary>
        /// <returns>The runtime cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _controller?.Dispose();
            if (_view != null)
                Object.Destroy(_view.gameObject);
            yield return null;
            _controller = null;
            _view = null;
            _session = null;
        }

        [UnityTest]
        public IEnumerator Dispose_DestroyedScreen_ReleasesSessionResources()
        {
            Playback playback = new();
            _session.Audio.Playback = (_, _) => playback;
            _session.Audio.Enqueue(TacticalCombatSound.CapitalLaser);
            Object.Destroy(_view.gameObject);
            yield return null;
            Assert.IsTrue(_view == null);

            Assert.DoesNotThrow(_controller.Dispose);

            Assert.AreEqual(1, playback.StopCount);
            Assert.IsNull(_session.Audio.Playback);
            Assert.Throws<ObjectDisposedException>(() => _session.Audio.Update());
            Assert.Throws<ObjectDisposedException>(() => _session.Selection.QueueDisplayMode(true));
        }

        [UnityTest]
        public IEnumerator Dispose_RendererDestroyedFirst_DestroysRemainingScreen()
        {
            Object.Destroy(_view.Renderer);
            yield return null;
            Assert.IsTrue(_view.Renderer == null);

            Assert.DoesNotThrow(_controller.Dispose);
            yield return null;

            Assert.IsTrue(_view == null);
        }

        [UnityTest]
        public IEnumerator Dispose_LiveScreen_DestroysScreenAndReleasesPlaybackOnce()
        {
            Playback playback = new();
            _session.Audio.Playback = (_, _) => playback;
            _session.Audio.Enqueue(TacticalCombatSound.CapitalLaser);

            _controller.Dispose();
            Assert.DoesNotThrow(_controller.Dispose);
            yield return null;

            Assert.IsTrue(_view == null);
            Assert.AreEqual(1, playback.StopCount);
        }

        /// <summary>
        /// Creates an encounter with one controllable audio resource and no deployed units.
        /// </summary>
        /// <returns>The isolated encounter.</returns>
        private static TacticalCombatSession CreateSession() =>
            new(
                Array.Empty<CombatUnit>(),
                Array.Empty<TacticalCombatGroup>(),
                CreateSide(CombatSide.Attacker),
                CreateSide(CombatSide.Defender),
                CombatSide.Attacker,
                100,
                new LinearCongruentialRandomProvider(42),
                TacticalCombatAudio.Create,
                TacticalCombatPlanner.Attach,
                new TacticalCombatDefinition
                {
                    Sounds = new()
                    {
                        new()
                        {
                            Kind = TacticalCombatSound.CapitalLaser,
                            Variants = new() { new() { Path = "notification" } },
                        },
                    },
                },
                presentationClock: () => 0
            );

        /// <summary>
        /// Captures the command state for one side of the encounter.
        /// </summary>
        /// <param name="side">The side to capture.</param>
        /// <returns>The side's command and orientation state.</returns>
        private static TacticalCombatSide CreateSide(CombatSide side) =>
            new(
                side,
                (int)side,
                side == CombatSide.Attacker ? Vector3N.UnitZ : -Vector3N.UnitZ,
                false,
                0,
                0,
                null,
                null,
                0,
                0,
                false
            );

        private sealed class Playback : ITacticalCombatNotificationPlayback
        {
            public int StopCount { get; private set; }
            public bool IsPlaying => StopCount == 0;

            /// <summary>
            /// Records release of the owned playback.
            /// </summary>
            public void Stop() => StopCount++;
        }
    }
}
