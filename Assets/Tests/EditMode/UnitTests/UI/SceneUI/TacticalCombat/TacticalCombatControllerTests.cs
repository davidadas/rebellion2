using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
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
        /// Creates the controller and its owned screen in an isolated runtime scene.
        /// </summary>
        /// <returns>The play-mode setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/StrategyView/TacticalCombat.prefab"
            );
            Assert.IsNotNull(prefab);
            _view = Object.Instantiate(prefab).GetComponent<TacticalCombatView>();
            _session = CreateSession();
            _controller = new TacticalCombatController(
                _session,
                _view,
                _ => { },
                CancellationToken.None
            );
        }

        /// <summary>
        /// Releases the encounter before leaving its runtime scene.
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
            if (Application.isPlaying)
                yield return new ExitPlayMode();
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
                Array.Empty<TacticalCombatUnit>(),
                Array.Empty<TacticalCombatGroup>(),
                CreateSide(CombatSide.Attacker),
                CreateSide(CombatSide.Defender),
                CombatSide.Attacker,
                100,
                new TacticalCombatRandom(42),
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
