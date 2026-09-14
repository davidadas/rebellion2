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

        /// <summary>
        /// Sets up.
        /// </summary>
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

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_rootObject != null)
                UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        /// <summary>
        /// Verifies constructor null dependency throws argument null exception.
        /// </summary>
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

        /// <summary>
        /// Verifies constructor configured bounds applies movement bounds to manager.
        /// </summary>
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

        /// <summary>
        /// Verifies refresh movement bounds changed bounds reapplies movement bounds to manager.
        /// </summary>
        [Test]
        public void RefreshMovementBounds_ChangedBounds_ReappliesMovementBoundsToManager()
        {
            SourceRectLayout replacementBounds = new SourceRectLayout
            {
                X = 127,
                Y = 41,
                Width = 693,
                Height = 349,
            };
            _placements.WindowBounds = replacementBounds;
            Vector2Int windowSize = new Vector2Int(100, 80);

            _controller.RefreshMovementBounds();

            Assert.AreEqual(
                new Vector2Int(replacementBounds.X, replacementBounds.Y),
                _windowManager.ClampPosition(int.MinValue, int.MinValue, windowSize)
            );
            Assert.AreEqual(
                new Vector2Int(
                    replacementBounds.X + replacementBounds.Width - windowSize.x,
                    replacementBounds.Y + replacementBounds.Height - windowSize.y
                ),
                _windowManager.ClampPosition(int.MaxValue, int.MaxValue, windowSize)
            );
        }

        /// <summary>
        /// Verifies get sector window position configured slots returns authored positions.
        /// </summary>
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

        /// <summary>
        /// Verifies get sector window position configured slots do not overlap.
        /// </summary>
        [Test]
        public void GetSectorWindowPosition_ConfiguredSlots_DoNotOverlap()
        {
            int windowWidth = _windowLayer.GetWindowSize(_windowLayer.PlanetSectorWindowPrefab).x;
            Vector2Int left = _controller.GetSectorWindowPosition(SectorWindowPositions.Left);
            Vector2Int middle = _controller.GetSectorWindowPosition(SectorWindowPositions.Middle);
            Vector2Int right = _controller.GetSectorWindowPosition(SectorWindowPositions.Right);

            Assert.GreaterOrEqual(middle.x, left.x + windowWidth);
            Assert.GreaterOrEqual(right.x, middle.x + windowWidth);
        }

        /// <summary>
        /// Verifies get sector window position unknown slot throws argument out of range exception.
        /// </summary>
        [Test]
        public void GetSectorWindowPosition_UnknownSlot_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _controller.GetSectorWindowPosition(-1)
            );
        }

        /// <summary>
        /// Verifies that each saved authored coordinate resolves to its semantic sector slot.
        /// </summary>
        [Test]
        public void TryGetSectorWindowSlot_AuthoredCoordinates_ReturnsMatchingSlots()
        {
            foreach (
                int expectedSlot in new[]
                {
                    SectorWindowPositions.Left,
                    SectorWindowPositions.Middle,
                    SectorWindowPositions.Right,
                }
            )
            {
                int x = _controller.GetSectorWindowPosition(expectedSlot).x;

                bool found = _controller.TryGetSectorWindowSlot(x, out int actualSlot);

                Assert.IsTrue(found);
                Assert.AreEqual(expectedSlot, actualSlot);
            }
        }

        /// <summary>
        /// Verifies that an unauthored coordinate does not resolve to a sector slot.
        /// </summary>
        [Test]
        public void TryGetSectorWindowSlot_UnknownCoordinate_ReturnsFalse()
        {
            bool found = _controller.TryGetSectorWindowSlot(int.MinValue, out int slot);

            Assert.IsFalse(found);
            Assert.AreEqual(-1, slot);
        }

        /// <summary>
        /// Verifies get utility window position configured theme returns authored position.
        /// </summary>
        [Test]
        public void GetUtilityWindowPosition_ConfiguredTheme_ReturnsAuthoredPosition()
        {
            Vector2Int position = _controller.GetUtilityWindowPosition();

            Assert.AreEqual(_placements.UtilityWindowPosition.ToVector2Int(), position);
        }

        /// <summary>
        /// Verifies centered window positions authored prefabs center within movement bounds.
        /// </summary>
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

        /// <summary>
        /// Verifies get mission create window position authored prefab and offset centers on surface.
        /// </summary>
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

        /// <summary>
        /// Verifies get construction window position source position applies offset and clamps.
        /// </summary>
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

        /// <summary>
        /// Verifies clamp planet window position known icons uses matching prefab size.
        /// </summary>
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

        /// <summary>
        /// Verifies clamp planet window position unknown icon preserves requested position.
        /// </summary>
        [Test]
        public void ClampPlanetWindowPosition_UnknownIcon_PreservesRequestedPosition()
        {
            Vector2Int position = _controller.ClampPlanetWindowPosition(PlanetIcon.None, 123, 456);

            Assert.AreEqual(new Vector2Int(123, 456), position);
        }

        /// <summary>
        /// Creates context.
        /// </summary>
        /// <returns>The created context.</returns>
        private UIContext CreateContext()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _playerFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            return TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
        }

        /// <summary>
        /// Gets centered position.
        /// </summary>
        /// <param name="prefab">The prefab.</param>
        /// <returns>The requested centered position.</returns>
        private Vector2Int GetCenteredPosition(MonoBehaviour prefab)
        {
            SourceRectLayout bounds = _placements.WindowBounds;
            Vector2Int size = _windowLayer.GetWindowSize(prefab);
            return new Vector2Int(
                bounds.X + Mathf.RoundToInt((bounds.Width - size.x) / 2f),
                bounds.Y + Mathf.RoundToInt((bounds.Height - size.y) / 2f)
            );
        }

        /// <summary>
        /// Gets maximum position.
        /// </summary>
        /// <param name="prefab">The prefab.</param>
        /// <returns>The requested maximum position.</returns>
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
