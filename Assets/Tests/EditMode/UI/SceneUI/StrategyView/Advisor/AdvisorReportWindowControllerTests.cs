using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Advisor
{
    [TestFixture]
    public class AdvisorReportWindowControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private AdvisorReportWindowController _controller;
        private int _dirtyCount;
        private GameObject _rootObject;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        [SetUp]
        public void SetUp()
        {
            _dirtyCount = 0;
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Player" }
            );
            game.Summary.PlayerFactionID = _playerFactionId;
            UIContext uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _controller = new AdvisorReportWindowController(
                () => uiContext,
                _windowLayer,
                _windowManager,
                () => new Vector2Int(102, 58),
                _windowManager.DestroyWindow,
                () => _dirtyCount++
            );
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AdvisorReportWindowController(
                    null,
                    _windowLayer,
                    _windowManager,
                    () => Vector2Int.zero,
                    _ => { },
                    () => { }
                )
            );
        }

        [Test]
        public void Open_ClosedReport_CreatesNamedBoundWindowAtConfiguredPosition()
        {
            _controller.Open(AdvisorReportMode.GalaxyOverview);

            Assert.AreEqual(1, _windowManager.Windows.Count);
            UIWindow window = _windowManager.Windows.Single();
            Assert.AreEqual("AdvisorReportWindow-GalaxyOverview", window.Content.name);
            Assert.AreEqual(new Vector2Int(102, 58), new Vector2Int(window.X, window.Y));
            Assert.IsTrue(
                _windowManager.TryGetWindowView(window, out AdvisorReportWindowView reportView)
            );
            Assert.AreEqual(AdvisorReportMode.GalaxyOverview, _controller.GetMode(reportView));
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void Open_ExistingReport_ChangesModeWithoutCreatingAnotherWindow()
        {
            _controller.Open(AdvisorReportMode.GalaxyOverview);
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out AdvisorReportWindowView reportView);

            _controller.Open(AdvisorReportMode.Objectives);

            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.AreSame(window, _windowManager.ActiveWindow);
            Assert.AreEqual(AdvisorReportMode.Objectives, _controller.GetMode(reportView));
            Assert.AreEqual(2, _dirtyCount);
        }

        [Test]
        public void GetMode_UnboundView_ThrowsInvalidOperationException()
        {
            AdvisorReportWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.AdvisorReportWindowPrefab,
                _rootObject.transform
            );

            Assert.Throws<InvalidOperationException>(() => _controller.GetMode(view));
        }

        [TestCase(AdvisorReportMode.GalaxyOverview, "Galaxy Overview")]
        [TestCase(AdvisorReportMode.Objectives, "Objectives")]
        public void GetTitle_KnownMode_ReturnsDisplayedTitle(
            AdvisorReportMode mode,
            string expected
        )
        {
            Assert.AreEqual(expected, AdvisorReportWindowController.GetTitle(mode));
        }

        [Test]
        public void GetTitle_UnknownMode_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AdvisorReportWindowController.GetTitle((AdvisorReportMode)int.MaxValue)
            );
        }
    }
}
