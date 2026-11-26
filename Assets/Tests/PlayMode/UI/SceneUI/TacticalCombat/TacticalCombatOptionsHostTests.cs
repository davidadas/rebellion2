using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Tactical.Rendering;
using Rebellion.UI.SceneUI.TacticalCombat;
using Rebellion.Util.Random;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TacticalCombatPlanner = global::Rebellion.AI.Tactical.TacticalCombatPlanner;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatOptionsHostTests
    {
        private GameObject _sourceRoot;
        private TacticalCombatView _view;
        private TacticalCombatSession _session;
        private TacticalCombatController _combat;
        private TacticalCombatOptionsHost _host;
        private TestOptions _options;

        /// <summary>
        /// Creates a test battle view and an independent application window desktop.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _sourceRoot = new GameObject(
                "Application canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler)
            );
            CanvasScaler scaler = _sourceRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(853.3333f, 480);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            GameObject desktop = new GameObject("Application desktop", typeof(RectTransform));
            desktop.transform.SetParent(_sourceRoot.transform, false);
            RectTransform desktopRect = desktop.GetComponent<RectTransform>();
            desktopRect.sizeDelta = new Vector2(853.3333f, 480);
            _view = TacticalCombatViewFixture.Create();
            _session = new TacticalCombatSession(
                Array.Empty<CombatUnit>(),
                Array.Empty<TacticalCombatGroup>(),
                CreateSide(CombatSide.Attacker),
                CreateSide(CombatSide.Defender),
                CombatSide.Attacker,
                100,
                new SystemRandomProvider(42),
                TacticalCombatAudio.Create,
                TacticalCombatPlanner.Attach
            );
            _combat = new TacticalCombatController(_session, _view, _ => { }, default);
            _options = new TestOptions();
            _host = new TacticalCombatOptionsHost(
                _combat,
                _session,
                _view,
                desktopRect,
                null,
                (_, _, _) => _options
            );
        }

        /// <summary>
        /// Releases the modal, battle view, and desktop.
        /// </summary>
        /// <returns>The runtime cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _host?.Dispose();
            _combat?.Dispose();
            if (_view != null)
                UnityEngine.Object.Destroy(_view.gameObject);
            if (_sourceRoot != null)
                UnityEngine.Object.Destroy(_sourceRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Open_BattleView_KeepsPausedBattleVisibleBelowModalCanvas()
        {
            Canvas canvas = _view.Viewport.GetComponentInParent<Canvas>();

            _host.Open();
            yield return null;

            Assert.IsTrue(canvas.enabled);
            Assert.IsTrue(_session.IsPaused);
            Assert.IsTrue(_options.IsOpen);
            Canvas overlay = _view.transform.Find("Combat application menu").GetComponent<Canvas>();
            Assert.Greater(overlay.sortingOrder, canvas.sortingOrder);
            Assert.IsTrue(overlay.gameObject.activeInHierarchy);
            Assert.IsTrue(overlay.GetComponentInChildren<Image>().raycastTarget);
        }

        [UnityTest]
        public IEnumerator Render_MenuCloses_ReturnsToPausedBattle()
        {
            _host.Open();

            _options.Close();
            _host.Render();
            yield return null;

            Assert.IsTrue(_session.IsPaused);
            Assert.IsTrue(_view.Viewport.GetComponentInParent<Canvas>().enabled);
        }

        [UnityTest]
        public IEnumerator Render_PreviouslyPausedBattle_RetainsPauseAfterOptionsCloses()
        {
            _session.IsPaused = true;
            _host.Open();

            _options.Close();
            _host.Render();
            yield return null;

            Assert.IsTrue(_session.IsPaused);
            Assert.IsTrue(_view.Viewport.GetComponentInParent<Canvas>().enabled);
        }

        [UnityTest]
        public IEnumerator Dispose_OpenMenu_ReleasesModalOwnershipOnce()
        {
            List<bool> ownership = new();
            _host.VisibilityChanged += ownership.Add;
            _host.Open();

            _host.Dispose();
            _host.Dispose();
            yield return null;

            CollectionAssert.AreEqual(new[] { true, false }, ownership);
            Assert.IsFalse(_options.IsOpen);
            Assert.IsTrue(_session.IsPaused);
            Assert.IsTrue(_view.Viewport.GetComponentInParent<Canvas>().enabled);
        }

        [UnityTest]
        public IEnumerator Open_RepeatedRequest_TransfersModalOwnershipOnlyOnce()
        {
            List<bool> ownership = new();
            _host.VisibilityChanged += ownership.Add;

            _host.Open();
            _host.Open();
            _options.Close();
            _host.Render();
            yield return null;

            CollectionAssert.AreEqual(new[] { true, false }, ownership);
        }

        [UnityTest]
        public IEnumerator Open_MenuFails_RestoresBattleVisibility()
        {
            _options.FailOpening = true;

            Assert.Throws<InvalidOperationException>(() => _host.Open());
            yield return null;

            Assert.IsTrue(_view.Viewport.GetComponentInParent<Canvas>().enabled);
        }

        /// <summary>
        /// Supplies the isolated encounter's explicit side state.
        /// </summary>
        /// <param name="side">The side being created.</param>
        /// <returns>The captured side state.</returns>
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

        private sealed class TestOptions : IOptionsMenuPresenter
        {
            public bool IsOpen { get; private set; }
            public bool FailOpening;

            /// <summary>
            /// Opens the simulated application presenter or reports its configured failure.
            /// </summary>
            /// <param name="initialTab">The requested initial page.</param>
            public void Open(OptionsMenuTab initialTab = OptionsMenuTab.Gameplay)
            {
                if (FailOpening)
                    throw new InvalidOperationException("Opening failed.");
                IsOpen = true;
            }

            /// <summary>
            /// Closes the simulated application presenter.
            /// </summary>
            public void Close() => IsOpen = false;

            /// <summary>
            /// Completes a render request without allocating additional windows.
            /// </summary>
            public void RenderWindows() { }

            /// <summary>
            /// Releases the simulated presenter's visible state.
            /// </summary>
            public void Dispose() => Close();
        }
    }
}
