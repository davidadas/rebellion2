using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.ContextMenus
{
    [TestFixture]
    public class StrategyContextMenuRouterTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private GameObject _rootObject;
        private StrategyContextMenuPresenter _presenter;
        private ContextMenuController _menuController;
        private UIWindowManager _windowManager;
        private UIWindow _window;
        private RecordingProvider _provider;
        private StrategyContextMenuRouter _router;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _presenter = _rootObject.GetComponentInChildren<StrategyContextMenuPresenter>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _menuController = new ContextMenuController();
            _provider = new RecordingProvider();
            _window = CreateRegisteredWindow();
            _router = CreateRouter(_provider);
        }

        [TearDown]
        public void TearDown()
        {
            if (_rootObject != null)
                UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullDependencyOrProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyContextMenuRouter(
                    null,
                    _menuController,
                    _windowManager,
                    Array.Empty<IStrategyContextMenuProvider>()
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyContextMenuRouter(
                    _presenter,
                    null,
                    _windowManager,
                    Array.Empty<IStrategyContextMenuProvider>()
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyContextMenuRouter(
                    _presenter,
                    _menuController,
                    null,
                    Array.Empty<IStrategyContextMenuProvider>()
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyContextMenuRouter(_presenter, _menuController, _windowManager, null)
            );
            Assert.Throws<ArgumentException>(() =>
                new StrategyContextMenuRouter(
                    _presenter,
                    _menuController,
                    _windowManager,
                    new IStrategyContextMenuProvider[] { null }
                )
            );
        }

        [Test]
        public void OpenContextMenu_KnownWindow_UsesFirstHandlingProvider()
        {
            RecordingReceiver receiver = new RecordingReceiver();
            StrategyMenuCommand command = CreateCommand("Status");
            ContextMenuRequest request = new ContextMenuRequest(
                _window,
                new IContextMenuCommand[] { command },
                receiver
            );
            _provider.Handle = true;
            _provider.Request = request;
            _provider.Width = 137;
            PointerEventData eventData = new PointerEventData(null);

            _router.OpenContextMenu(_window, eventData, 12, 34);

            Assert.IsTrue(_provider.Called);
            Assert.AreSame(_window, _provider.LastContext.Window);
            Assert.AreSame(eventData, _provider.LastContext.EventData);
            Assert.AreEqual(12, _provider.LastContext.X);
            Assert.AreEqual(34, _provider.LastContext.Y);
            Assert.AreSame(request, _menuController.ActiveRequest);
            Assert.IsTrue(_presenter.Open);
            Assert.AreSame(_window, _presenter.Window);
            Assert.IsTrue(_router.IsOpen);
        }

        [Test]
        public void OpenContextMenu_PointerWindow_ResolvesRegisteredWindow()
        {
            _provider.Handle = true;
            _provider.Request = new ContextMenuRequest(
                _window,
                new IContextMenuCommand[] { CreateCommand("Status") },
                new RecordingReceiver()
            );
            PointerEventData eventData = new PointerEventData(null)
            {
                pointerCurrentRaycast = new RaycastResult { gameObject = _window.gameObject },
            };

            _router.OpenContextMenu(eventData, 21, 43);

            Assert.IsTrue(_provider.Called);
            Assert.AreSame(_window, _provider.LastContext.Window);
            Assert.AreEqual(21, _provider.LastContext.X);
            Assert.AreEqual(43, _provider.LastContext.Y);
        }

        [Test]
        public void OpenContextMenu_NoProviderHandles_ShowsDisabledFallbackCommands()
        {
            _router.OpenContextMenu(_window, null, 10, 20);
            _presenter.RenderCurrent();
            ContextMenuCommandView[] rows = FindRenderedRows();

            Assert.IsFalse(_menuController.IsOpen);
            Assert.IsTrue(_presenter.Open);
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("Encyclopedia", FindCommandText(rows[0]).text);
            Assert.AreEqual("Status", FindCommandText(rows[1]).text);
        }

        [Test]
        public void OpenContextMenu_NullWindow_CancelsRequestAndResetsPresenter()
        {
            RecordingReceiver receiver = new RecordingReceiver();
            ContextMenuRequest request = new ContextMenuRequest(
                _window,
                new IContextMenuCommand[] { CreateCommand("Status") },
                receiver
            );
            _router.OpenRuntimeContextMenu(request, 10, 20, 100);

            _router.OpenContextMenu(null, null, 0, 0);

            Assert.AreEqual(1, receiver.CancelledCount);
            Assert.IsFalse(_menuController.IsOpen);
            Assert.IsFalse(_presenter.Open);
            Assert.IsFalse(_router.IsOpen);
        }

        [Test]
        public void OpenRuntimeContextMenu_StrategyCommands_OpensRequestAndPresentation()
        {
            StrategyMenuCommand command = CreateCommand("Status");
            ContextMenuRequest request = new ContextMenuRequest(
                new object(),
                new IContextMenuCommand[] { command },
                new RecordingReceiver()
            );

            _router.OpenRuntimeContextMenu(request, 11, 22, 123);

            Assert.AreSame(request, _menuController.ActiveRequest);
            Assert.IsTrue(_presenter.Open);
            Assert.IsNull(_presenter.Window);
            Assert.IsTrue(_router.IsOpen);
        }

        [Test]
        public void OpenRuntimeContextMenu_NullOrForeignCommand_Throws()
        {
            ContextMenuRequest foreignRequest = new ContextMenuRequest(
                new object(),
                new IContextMenuCommand[] { new ForeignCommand() },
                new RecordingReceiver()
            );

            Assert.Throws<ArgumentNullException>(() =>
                _router.OpenRuntimeContextMenu(null, 0, 0, 100)
            );
            Assert.Throws<ArgumentException>(() =>
                _router.OpenRuntimeContextMenu(foreignRequest, 0, 0, 100)
            );
            Assert.IsFalse(_menuController.IsOpen);
            Assert.IsFalse(_presenter.Open);
        }

        [Test]
        public void SelectRuntimeContextMenu_IncludedCommand_NotifiesAndClosesBothLayers()
        {
            RecordingReceiver receiver = new RecordingReceiver();
            StrategyMenuCommand command = CreateCommand("Status");
            ContextMenuRequest request = new ContextMenuRequest(
                new object(),
                new IContextMenuCommand[] { command },
                receiver
            );
            _router.OpenRuntimeContextMenu(request, 0, 0, 100);

            _router.SelectRuntimeContextMenu(command);

            Assert.AreEqual(1, receiver.SelectedCount);
            Assert.AreSame(request, receiver.LastRequest);
            Assert.AreSame(command, receiver.LastCommand);
            Assert.IsFalse(_menuController.IsOpen);
            Assert.IsFalse(_presenter.Open);
        }

        [Test]
        public void SelectRuntimeContextMenu_InvalidCommand_CancelsAndClosesBothLayers()
        {
            RecordingReceiver receiver = new RecordingReceiver();
            StrategyMenuCommand included = CreateCommand("Included");
            ContextMenuRequest request = new ContextMenuRequest(
                new object(),
                new IContextMenuCommand[] { included },
                receiver
            );
            _router.OpenRuntimeContextMenu(request, 0, 0, 100);

            _router.SelectRuntimeContextMenu(CreateCommand("Missing"));

            Assert.AreEqual(1, receiver.CancelledCount);
            Assert.AreSame(request, receiver.LastRequest);
            Assert.IsFalse(_menuController.IsOpen);
            Assert.IsFalse(_presenter.Open);
        }

        [Test]
        public void TryCancel_OpenThenClosedMenu_ReturnsMatchingState()
        {
            RecordingReceiver receiver = new RecordingReceiver();
            ContextMenuRequest request = new ContextMenuRequest(
                new object(),
                new IContextMenuCommand[] { CreateCommand("Status") },
                receiver
            );
            _router.OpenRuntimeContextMenu(request, 0, 0, 100);

            bool first = _router.TryCancel();
            bool second = _router.TryCancel();

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.AreEqual(1, receiver.CancelledCount);
            Assert.IsFalse(_router.IsOpen);
        }

        private StrategyContextMenuRouter CreateRouter(
            params IStrategyContextMenuProvider[] providers
        )
        {
            return new StrategyContextMenuRouter(
                _presenter,
                _menuController,
                _windowManager,
                providers
            );
        }

        private UIWindow CreateRegisteredWindow()
        {
            GameObject windowObject = new GameObject(
                "Window",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(UIWindow)
            );
            windowObject.transform.SetParent(_windowManager.transform, false);
            UIWindow window = windowObject.GetComponent<UIWindow>();
            window.Configure(100, 0, 0, 100, 100, false, true, true);
            _windowManager.Register(window, false);
            return window;
        }

        private ContextMenuCommandView[] FindRenderedRows()
        {
            ContextMenuPanelView panel = _rootObject
                .GetComponentsInChildren<ContextMenuPanelView>(true)
                .Single(view =>
                    view.name.StartsWith("Panel", StringComparison.Ordinal)
                    && view.name != "PanelTemplate"
                    && view.gameObject.activeSelf
                );
            return panel
                .GetComponentsInChildren<ContextMenuCommandView>(true)
                .Where(row =>
                    row.name.StartsWith("Command", StringComparison.Ordinal)
                    && row.name != "CommandTemplate"
                    && row.gameObject.activeSelf
                )
                .OrderBy(row => row.name)
                .ToArray();
        }

        private static TextMeshProUGUI FindCommandText(ContextMenuCommandView row)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "CommandTextField");
        }

        private static StrategyMenuCommand CreateCommand(string text)
        {
            return new StrategyMenuCommand(StrategyContextMenuActions.Status, text, true);
        }

        private sealed class RecordingProvider : IStrategyContextMenuProvider
        {
            public bool Called { get; private set; }
            public bool Handle { get; set; }
            public StrategyContextMenuProviderContext LastContext { get; private set; }
            public ContextMenuRequest Request { get; set; }
            public int Width { get; set; }

            public bool TryCreateContextMenu(
                StrategyContextMenuProviderContext context,
                out ContextMenuRequest request,
                out int width
            )
            {
                Called = true;
                LastContext = context;
                request = Request;
                width = Width;
                return Handle;
            }
        }

        private sealed class RecordingReceiver : IContextMenuReceiver
        {
            public int CancelledCount { get; private set; }
            public IContextMenuCommand LastCommand { get; private set; }
            public ContextMenuRequest LastRequest { get; private set; }
            public int SelectedCount { get; private set; }

            public void OnContextMenuCommandSelected(
                ContextMenuRequest request,
                IContextMenuCommand command
            )
            {
                SelectedCount++;
                LastRequest = request;
                LastCommand = command;
            }

            public void OnContextMenuCancelled(ContextMenuRequest request)
            {
                CancelledCount++;
                LastRequest = request;
            }
        }

        private sealed class ForeignCommand : IContextMenuCommand
        {
            public string Text => "Foreign";
            public bool Enabled => true;
        }
    }
}
