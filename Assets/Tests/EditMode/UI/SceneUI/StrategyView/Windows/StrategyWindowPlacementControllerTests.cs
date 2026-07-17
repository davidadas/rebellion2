using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Windows
{
    [TestFixture]
    public class StrategyWindowPlacementControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private GameObject _rootObject;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;
        private StrategyWindowPlacements _placements;
        private StrategyWindowPlacementController _controller;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _uiContext = CreateContext();
            _placements = _uiContext.GetPlayerFactionTheme().StrategyWindowPlacements;
            _controller = new StrategyWindowPlacementController(
                _uiContext,
                _windowLayer,
                _windowManager
            );
        }

        [TearDown]
        public void TearDown()
        {
            if (_rootObject != null)
                UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullDependency_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyWindowPlacementController(null, _windowLayer, _windowManager)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyWindowPlacementController(_uiContext, null, _windowManager)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyWindowPlacementController(_uiContext, _windowLayer, null)
            );
        }

        [Test]
        public void Constructor_ConfiguredBounds_AppliesMovementBoundsToManager()
        {
            SourceRectLayout bounds = _placements.WindowBounds;
            Vector2Int windowSize = new Vector2Int(100, 80);

            Vector2Int minimum = _windowManager.ClampPosition(
                int.MinValue,
                int.MinValue,
                windowSize
            );
            Vector2Int maximum = _windowManager.ClampPosition(
                int.MaxValue,
                int.MaxValue,
                windowSize
            );

            Assert.AreEqual(new Vector2Int(bounds.X, bounds.Y), minimum);
            Assert.AreEqual(
                new Vector2Int(bounds.X + bounds.Width - 100, bounds.Y + bounds.Height - 80),
                maximum
            );
        }

        [Test]
        public void GetSectorWindowPosition_ConfiguredSlots_ReturnsAuthoredPositions()
        {
            Vector2Int left = _controller.GetSectorWindowPosition(SectorWindowPositions.Left);
            Vector2Int middle = _controller.GetSectorWindowPosition(SectorWindowPositions.Middle);
            Vector2Int right = _controller.GetSectorWindowPosition(SectorWindowPositions.Right);

            Assert.AreEqual(_placements.SectorLeftPosition.ToVector2Int(), left);
            Assert.AreEqual(_placements.SectorMiddlePosition.ToVector2Int(), middle);
            Assert.AreEqual(_placements.SectorRightPosition.ToVector2Int(), right);
        }

        [Test]
        public void GetSectorWindowPosition_UnknownSlot_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _controller.GetSectorWindowPosition(-1)
            );
        }

        [Test]
        public void GetUtilityWindowPosition_ConfiguredTheme_ReturnsAuthoredPosition()
        {
            Vector2Int position = _controller.GetUtilityWindowPosition();

            Assert.AreEqual(_placements.UtilityWindowPosition.ToVector2Int(), position);
        }

        [Test]
        public void CenteredWindowPositions_AuthoredPrefabs_CenterWithinMovementBounds()
        {
            Assert.AreEqual(
                GetCenteredPosition(_windowLayer.MessagesWindowPrefab),
                _controller.GetMessagesWindowPosition()
            );
            Assert.AreEqual(
                GetCenteredPosition(_windowLayer.FinderWindowPrefab),
                _controller.GetFinderWindowPosition()
            );
            Assert.AreEqual(
                GetCenteredPosition(_windowLayer.StatusWindowPrefab),
                _controller.GetStatusWindowPosition()
            );
            Assert.AreEqual(
                GetCenteredPosition(_windowLayer.AdvisorReportWindowPrefab),
                _controller.GetAdvisorReportWindowPosition()
            );
            Assert.AreEqual(
                GetCenteredPosition(_windowLayer.EncyclopediaWindowPrefab),
                _controller.GetEncyclopediaWindowPosition()
            );
            Assert.AreEqual(
                GetCenteredPosition(_windowLayer.ConfirmDialogWindowPrefab),
                _controller.GetConfirmDialogWindowPosition()
            );
            Assert.AreEqual(
                GetCenteredPosition(_windowLayer.BattleAlertWindowPrefab),
                _controller.GetBattleAlertWindowPosition()
            );
        }

        [Test]
        public void GetMissionCreateWindowPosition_AuthoredPrefabAndOffset_CentersOnSurface()
        {
            Vector2Int surfaceSize = _windowLayer.GetSurfaceSize();
            Vector2Int windowSize = _windowLayer.GetWindowSize(
                _windowLayer.MissionCreateWindowPrefab
            );
            Vector2Int offset = _placements.MissionCreateOffset.ToVector2Int();
            Vector2Int expected = new Vector2Int(
                Mathf.RoundToInt(surfaceSize.x / 2f - windowSize.x / 2f + offset.x),
                Mathf.RoundToInt(surfaceSize.y / 2f - windowSize.y / 2f + offset.y)
            );

            Vector2Int position = _controller.GetMissionCreateWindowPosition();

            Assert.AreEqual(expected, position);
        }

        [Test]
        public void GetConstructionWindowPosition_SourcePosition_AppliesOffsetAndClamps()
        {
            Vector2Int offset = _windowLayer.ConstructionWindowOffset;
            Vector2Int size = _windowLayer.GetWindowSize(_windowLayer.ConstructionWindowPrefab);
            Vector2Int expected = _windowManager.ClampPosition(
                100000 + offset.x,
                -100000 + offset.y,
                size
            );

            Vector2Int position = _controller.GetConstructionWindowPosition(100000, -100000);

            Assert.AreEqual(expected, position);
        }

        [Test]
        public void ClampPlanetWindowPosition_KnownIcons_UsesMatchingPrefabSize()
        {
            Vector2Int facility = _controller.ClampPlanetWindowPosition(
                PlanetIcon.Facility,
                int.MaxValue,
                int.MaxValue
            );
            Vector2Int defense = _controller.ClampPlanetWindowPosition(
                PlanetIcon.Defense,
                int.MaxValue,
                int.MaxValue
            );
            Vector2Int fleet = _controller.ClampPlanetWindowPosition(
                PlanetIcon.Fleet,
                int.MaxValue,
                int.MaxValue
            );
            Vector2Int mission = _controller.ClampPlanetWindowPosition(
                PlanetIcon.Mission,
                int.MaxValue,
                int.MaxValue
            );

            Assert.AreEqual(GetMaximumPosition(_windowLayer.FacilityWindowPrefab), facility);
            Assert.AreEqual(GetMaximumPosition(_windowLayer.DefenseWindowPrefab), defense);
            Assert.AreEqual(GetMaximumPosition(_windowLayer.FleetWindowPrefab), fleet);
            Assert.AreEqual(GetMaximumPosition(_windowLayer.MissionsWindowPrefab), mission);
        }

        [Test]
        public void ClampPlanetWindowPosition_UnknownIcon_PreservesRequestedPosition()
        {
            Vector2Int position = _controller.ClampPlanetWindowPosition(PlanetIcon.None, 123, 456);

            Assert.AreEqual(new Vector2Int(123, 456), position);
        }

        private UIContext CreateContext()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _playerFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            return new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
        }

        private Vector2Int GetCenteredPosition(MonoBehaviour prefab)
        {
            SourceRectLayout bounds = _placements.WindowBounds;
            Vector2Int size = _windowLayer.GetWindowSize(prefab);
            return new Vector2Int(
                bounds.X + Mathf.RoundToInt((bounds.Width - size.x) / 2f),
                bounds.Y + Mathf.RoundToInt((bounds.Height - size.y) / 2f)
            );
        }

        private Vector2Int GetMaximumPosition(MonoBehaviour prefab)
        {
            SourceRectLayout bounds = _placements.WindowBounds;
            Vector2Int size = _windowLayer.GetWindowSize(prefab);
            return new Vector2Int(
                bounds.X + bounds.Width - size.x,
                bounds.Y + bounds.Height - size.y
            );
        }
    }
}
