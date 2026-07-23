using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Targeting
{
    [TestFixture]
    public class StrategyWindowItemDragControllerTests
    {
        private const string _playerFactionId = "player";

        private GameObject _windowObject;
        private UIWindow _window;
        private Texture2D _texture;
        private TargetingController _targetingController;
        private RecordingWindowCommands _commands;
        private IReadOnlyList<ISceneNode> _contextItems;
        private DragPreview _preview;
        private bool _hasPreview;
        private StrategyMissionTarget _dropTarget;

        [SetUp]
        public void SetUp()
        {
            _windowObject = new GameObject(
                "Window",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(UIWindow)
            );
            _window = _windowObject.GetComponent<UIWindow>();
            _texture = new Texture2D(1, 1);
            _targetingController = new TargetingController();
            _commands = new RecordingWindowCommands();
            _contextItems = Array.Empty<ISceneNode>();
            _preview = new DragPreview(_texture, 20, 30, 2, 3);
            _hasPreview = false;
            _dropTarget = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_texture != null)
                UnityEngine.Object.DestroyImmediate(_texture);
            if (_windowObject != null)
                UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_NullDependency_ThrowsArgumentNullException()
        {
            TargetingController targetingController = new TargetingController();
            DragController dragController = new DragController(5);
            Func<UIWindow, IReadOnlyList<ISceneNode>> getItems = _ => _contextItems;
            StrategyWindowDragPreviewResolver getPreview = ResolvePreview;
            Func<PointerEventData, StrategyMissionTarget> getTarget = _ => _dropTarget;
            Func<string> getFaction = () => _playerFactionId;

            Assert.Throws<ArgumentNullException>(() =>
                new StrategyWindowItemDragController(
                    null,
                    dragController,
                    getItems,
                    getPreview,
                    getTarget,
                    getFaction,
                    _commands
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyWindowItemDragController(
                    targetingController,
                    null,
                    getItems,
                    getPreview,
                    getTarget,
                    getFaction,
                    _commands
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyWindowItemDragController(
                    targetingController,
                    dragController,
                    null,
                    getPreview,
                    getTarget,
                    getFaction,
                    _commands
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyWindowItemDragController(
                    targetingController,
                    dragController,
                    getItems,
                    null,
                    getTarget,
                    getFaction,
                    _commands
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyWindowItemDragController(
                    targetingController,
                    dragController,
                    getItems,
                    getPreview,
                    null,
                    getFaction,
                    _commands
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyWindowItemDragController(
                    targetingController,
                    dragController,
                    getItems,
                    getPreview,
                    getTarget,
                    null,
                    _commands
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyWindowItemDragController(
                    targetingController,
                    dragController,
                    getItems,
                    getPreview,
                    getTarget,
                    getFaction,
                    null
                )
            );
        }

        [Test]
        public void StartCandidate_NullWindow_DoesNotCreateCandidate()
        {
            StrategyWindowItemDragController controller = CreateController();

            controller.StartCandidate(null, 10, 20);

            Assert.IsFalse(controller.HasCandidate);
            Assert.IsFalse(controller.SourceDragActive);
        }

        [Test]
        public void TryStartMoveDragFromCandidate_BelowThreshold_PreservesCandidate()
        {
            _contextItems = new ISceneNode[] { CreateOfficer(_playerFactionId) };
            StrategyWindowItemDragController controller = CreateController();
            controller.StartCandidate(_window, 10, 20);

            StrategyWindowItemDragStartResult result = controller.TryStartMoveDragFromCandidate(
                12,
                22
            );

            Assert.AreEqual(StrategyWindowItemDragStartResult.None, result);
            Assert.IsTrue(controller.HasCandidate);
            Assert.IsFalse(controller.SourceDragActive);
        }

        [Test]
        public void TryStartMoveDragFromCandidate_ValidPreview_StartsSourceDrag()
        {
            Officer officer = CreateOfficer(_playerFactionId);
            _contextItems = new ISceneNode[] { officer };
            _hasPreview = true;
            StrategyWindowItemDragController controller = CreateController();
            controller.StartCandidate(_window, 10, 20);

            StrategyWindowItemDragStartResult result = controller.TryStartMoveDragFromCandidate(
                13,
                24
            );
            bool hasOverlay = controller.TryGetOverlay(out Texture texture, out RectInt bounds);
            StrategyWindowTargetingSource source =
                _targetingController.ActiveRequest?.Source as StrategyWindowTargetingSource;

            Assert.AreEqual(StrategyWindowItemDragStartResult.SourceDragStarted, result);
            Assert.IsFalse(controller.HasCandidate);
            Assert.IsTrue(controller.SourceDragActive);
            Assert.IsTrue(hasOverlay);
            Assert.AreSame(_texture, texture);
            Assert.AreEqual(new RectInt(11, 21, 20, 30), bounds);
            Assert.IsNotNull(source);
            Assert.AreSame(_window, source.Window);
            Assert.AreEqual(10, source.SourceX);
            Assert.AreEqual(20, source.SourceY);
            Assert.AreEqual(1, source.Items.Count);
            Assert.AreSame(officer, source.Items[0]);
        }

        [Test]
        public void TryStartMoveDragFromCandidate_ItemsWithoutPreview_StartsTargeting()
        {
            Officer officer = CreateOfficer(_playerFactionId);
            _contextItems = new ISceneNode[] { officer };
            StrategyWindowItemDragController controller = CreateController();
            controller.StartCandidate(_window, 10, 20);

            StrategyWindowItemDragStartResult result = controller.TryStartMoveDragFromCandidate(
                13,
                24
            );
            StrategyWindowTargetingSource source =
                _targetingController.ActiveRequest?.Source as StrategyWindowTargetingSource;

            Assert.AreEqual(StrategyWindowItemDragStartResult.TargetingStarted, result);
            Assert.IsFalse(controller.HasCandidate);
            Assert.IsFalse(controller.SourceDragActive);
            Assert.IsTrue(_targetingController.IsTargeting);
            Assert.AreEqual("Select move destination", _targetingController.ActiveRequest.Prompt);
            Assert.IsNotNull(source);
            Assert.AreSame(officer, source.Items[0]);
        }

        [Test]
        public void TryStartMoveDragFromCandidate_InvalidPreviewWithItems_StartsTargeting()
        {
            _contextItems = new ISceneNode[] { CreateOfficer(_playerFactionId) };
            _preview = new DragPreview(_texture, 0, 30, 2, 3);
            _hasPreview = true;
            StrategyWindowItemDragController controller = CreateController();
            controller.StartCandidate(_window, 10, 20);

            StrategyWindowItemDragStartResult result = controller.TryStartMoveDragFromCandidate(
                13,
                24
            );

            Assert.AreEqual(StrategyWindowItemDragStartResult.TargetingStarted, result);
            Assert.IsTrue(_targetingController.IsTargeting);
            Assert.IsFalse(controller.SourceDragActive);
        }

        [Test]
        public void TryStartMoveDragFromCandidate_NoItemsOrPreview_ClearsCandidate()
        {
            StrategyWindowItemDragController controller = CreateController();
            controller.StartCandidate(_window, 10, 20);

            StrategyWindowItemDragStartResult result = controller.TryStartMoveDragFromCandidate(
                13,
                24
            );

            Assert.AreEqual(StrategyWindowItemDragStartResult.CandidateCleared, result);
            Assert.IsFalse(controller.HasCandidate);
            Assert.IsFalse(_targetingController.IsTargeting);
        }

        [Test]
        public void TryMoveSourceDrag_ActiveDrag_UpdatesOverlay()
        {
            _contextItems = new ISceneNode[] { CreateOfficer(_playerFactionId) };
            _hasPreview = true;
            StrategyWindowItemDragController controller = CreateController();
            StartSourceDrag(controller);

            bool moved = controller.TryMoveSourceDrag(50, 60);
            bool hasOverlay = controller.TryGetOverlay(out _, out RectInt bounds);

            Assert.IsTrue(moved);
            Assert.IsTrue(hasOverlay);
            Assert.AreEqual(new RectInt(48, 57, 20, 30), bounds);
        }

        [Test]
        public void TryHandleSourceDragPointerUp_FriendlyPlanet_ExecutesMove()
        {
            Officer officer = CreateOfficer(_playerFactionId);
            _contextItems = new ISceneNode[] { officer };
            _hasPreview = true;
            _dropTarget = CreateMissionTarget("destination", _playerFactionId);
            StrategyWindowItemDragController controller = CreateController();
            StartSourceDrag(controller);

            bool handled = controller.TryHandleSourceDragPointerUp(null, 50, 60);

            Assert.IsTrue(handled);
            Assert.IsFalse(controller.SourceDragActive);
            Assert.IsFalse(_targetingController.IsTargeting);
            Assert.AreEqual(1, _commands.MoveCount);
            Assert.AreSame(_window, _commands.LastWindow);
            Assert.AreSame(_dropTarget, _commands.LastTarget);
            Assert.AreEqual(1, _commands.LastItems.Count);
            Assert.AreSame(officer, _commands.LastItems[0]);
            Assert.AreEqual(0, _commands.MissionCount);
        }

        [Test]
        public void TryHandleSourceDragPointerUp_EnemyPlanetAndOfficer_OpensMissionCreation()
        {
            Officer officer = CreateOfficer(_playerFactionId);
            _contextItems = new ISceneNode[] { officer };
            _hasPreview = true;
            _dropTarget = CreateMissionTarget("destination", "opponent");
            StrategyWindowItemDragController controller = CreateController();
            StartSourceDrag(controller);

            bool handled = controller.TryHandleSourceDragPointerUp(null, 50, 60);

            Assert.IsTrue(handled);
            Assert.AreEqual(1, _commands.MissionCount);
            Assert.AreSame(_dropTarget, _commands.LastTarget);
            Assert.AreSame(officer, _commands.LastItems[0]);
            Assert.AreEqual(0, _commands.MoveCount);
        }

        [Test]
        public void TryHandleSourceDragPointerUp_MissingDropTarget_CancelsTargeting()
        {
            _contextItems = new ISceneNode[] { CreateOfficer(_playerFactionId) };
            _hasPreview = true;
            StrategyWindowItemDragController controller = CreateController();
            StartSourceDrag(controller);

            bool handled = controller.TryHandleSourceDragPointerUp(null, 50, 60);

            Assert.IsTrue(handled);
            Assert.IsFalse(_targetingController.IsTargeting);
            Assert.AreEqual(0, _commands.MoveCount);
            Assert.AreEqual(0, _commands.MissionCount);
        }

        [Test]
        public void TryHandleSourceDragPointerUp_NoSourceDrag_ReturnsFalse()
        {
            StrategyWindowItemDragController controller = CreateController();

            bool handled = controller.TryHandleSourceDragPointerUp(null, 10, 20);

            Assert.IsFalse(handled);
        }

        [Test]
        public void ClearWindow_OwnedCandidateAndSourceDrag_ClearsMatchingState()
        {
            _contextItems = new ISceneNode[] { CreateOfficer(_playerFactionId) };
            _hasPreview = true;
            StrategyWindowItemDragController candidateController = CreateController();
            candidateController.StartCandidate(_window, 10, 20);

            candidateController.ClearWindow(_window);

            Assert.IsFalse(candidateController.HasCandidate);

            StrategyWindowItemDragController sourceController = CreateController();
            StartSourceDrag(sourceController);

            sourceController.ClearWindow(_window);

            Assert.IsFalse(sourceController.SourceDragActive);
        }

        [Test]
        public void Clear_CandidateAndSourceDrag_ClearsAllState()
        {
            _contextItems = new ISceneNode[] { CreateOfficer(_playerFactionId) };
            _hasPreview = true;
            StrategyWindowItemDragController controller = CreateController();
            controller.StartCandidate(_window, 10, 20);
            StartSourceDrag(controller);
            controller.StartCandidate(_window, 30, 40);

            controller.Clear();

            Assert.IsFalse(controller.HasCandidate);
            Assert.IsFalse(controller.SourceDragActive);
            Assert.IsFalse(controller.TryGetOverlay(out _, out _));
        }

        [Test]
        public void OnTargetSelected_InvalidRequestOrTarget_DoesNotExecuteCommand()
        {
            StrategyWindowItemDragController controller = CreateController();
            StrategyMissionTarget target = CreateMissionTarget("planet", "player");
            TargetingRequest invalidSource = new TargetingRequest(
                "Target",
                new object(),
                controller
            );
            TargetingRequest invalidAction = new TargetingRequest(
                "Target",
                new StrategyWindowTargetingSource(
                    _window,
                    StrategyMenuAction.Status,
                    0,
                    0,
                    Array.Empty<ISceneNode>()
                ),
                controller
            );

            controller.OnTargetSelected(null, target);
            controller.OnTargetSelected(invalidSource, target);
            controller.OnTargetSelected(invalidAction, target);

            Assert.AreEqual(0, _commands.MoveCount);
            Assert.AreEqual(0, _commands.MissionCount);
        }

        private StrategyWindowItemDragController CreateController()
        {
            return new StrategyWindowItemDragController(
                _targetingController,
                new DragController(5),
                _ => _contextItems,
                ResolvePreview,
                _ => _dropTarget,
                () => _playerFactionId,
                _commands
            );
        }

        private bool ResolvePreview(
            UIWindow window,
            int sourceX,
            int sourceY,
            out DragPreview preview
        )
        {
            preview = _preview;
            return _hasPreview;
        }

        private static Officer CreateOfficer(string ownerId)
        {
            return new Officer { OwnerInstanceID = ownerId };
        }

        private static StrategyMissionTarget CreateMissionTarget(string instanceId, string ownerId)
        {
            Planet planet = new Planet { InstanceID = instanceId, OwnerInstanceID = ownerId };
            GalaxyMapPlanet mapPlanet = new GalaxyMapPlanet(
                new GamePlanetSystem(),
                planet,
                string.Empty
            );
            return new StrategyMissionTarget(mapPlanet, null);
        }

        private void StartSourceDrag(StrategyWindowItemDragController controller)
        {
            controller.StartCandidate(_window, 10, 20);
            controller.TryStartMoveDragFromCandidate(13, 24);
        }

        private sealed class RecordingWindowCommands : IStrategyWindowCommandActions
        {
            public IReadOnlyList<ISceneNode> LastItems { get; private set; }
            public StrategyMissionTarget LastTarget { get; private set; }
            public UIWindow LastWindow { get; private set; }
            public int MissionCount { get; private set; }
            public int MoveCount { get; private set; }

            public void ExecuteTargetedCommand(
                StrategyWindowTargetingSource source,
                StrategyMissionTarget target
            ) { }

            public void OpenMissionCreateWindow(
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                MissionCount++;
                LastTarget = target;
                LastItems = items;
            }

            public bool TryExecuteMove(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                MoveCount++;
                LastWindow = sourceWindow;
                LastTarget = target;
                LastItems = items;
                return true;
            }

            public void OpenMoveConfirmWindow(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            ) { }
        }
    }
}
