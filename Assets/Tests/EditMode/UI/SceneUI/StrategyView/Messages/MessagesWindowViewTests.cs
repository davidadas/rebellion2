using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Messages;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GameMessageType = Rebellion.Game.Messages.MessageType;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesWindowViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/MessagesWindow.prefab";

        private MessagesCommandBarView _commandBar;
        private MessagesDetailPanelView _detailPanel;
        private MessagesIndexPanelView _indexPanel;
        private Texture2D _pressedTexture;
        private Texture2D _texture;
        private MessagesWindowView _view;
        private GameObject _windowObject;
        private UIWindow _windowShell;

        [SetUp]
        public void SetUp()
        {
            _windowObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _windowObject.GetComponent<MessagesWindowView>();
            _commandBar = _windowObject.GetComponentInChildren<MessagesCommandBarView>(true);
            _indexPanel = _windowObject.GetComponentInChildren<MessagesIndexPanelView>(true);
            _detailPanel = _windowObject.GetComponentInChildren<MessagesDetailPanelView>(true);
            _windowShell = _windowObject.GetComponent<UIWindow>();
            _texture = new Texture2D(64, 32);
            _pressedTexture = new Texture2D(64, 32);
            UIComponentTestHelper.InvokeLifecycle(_commandBar, "Awake");
            UIComponentTestHelper.InvokeLifecycle(_indexPanel, "Awake");
            UIComponentTestHelper.InvokeLifecycle(_detailPanel, "Awake");
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_pressedTexture);
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_IndexPresentation_AppliesFrameAndShowsIndexPanel()
        {
            MessagesWindowRenderData data = CreateRenderData(false);

            _view.Render(data);

            RectInt rect = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(21, rect.x);
            Assert.AreEqual(27, rect.y);
            Assert.AreSame(_texture, FindComponent<RawImage>("OverlayFrameImage").texture);
            Assert.AreSame(_texture, FindComponent<RawImage>("ButtonStripImage").texture);
            Assert.IsTrue(_indexPanel.gameObject.activeSelf);
            Assert.IsFalse(_detailPanel.gameObject.activeSelf);
            Assert.AreEqual(
                "All Messages",
                FindComponent<TMPro.TextMeshProUGUI>("TitleTextField").text
            );
        }

        [Test]
        public void Render_DetailPresentation_HidesIndexAndShowsDetailPanel()
        {
            _view.Render(CreateRenderData(false));

            _view.Render(CreateRenderData(true));

            Assert.IsFalse(_indexPanel.gameObject.activeSelf);
            Assert.IsTrue(_detailPanel.gameObject.activeSelf);
            Assert.AreEqual(
                "Message detail",
                FindComponent<TMPro.TextMeshProUGUI>("DetailHeaderTextField").text
            );
        }

        [Test]
        public void CommandControls_Click_RaiseParentSemanticAndControlRequests()
        {
            int controlCount = 0;
            int closeCount = 0;
            int displayCount = 0;
            int indexCount = 0;
            int signalCount = 0;
            int targetCount = 0;
            int chatCount = 0;
            _view.ControlPressed += () => controlCount++;
            _view.CloseRequested += _ => closeCount++;
            _view.DisplayRequested += _ => displayCount++;
            _view.IndexRequested += _ => indexCount++;
            _view.NotificationToggleRequested += _ => signalCount++;
            _view.MessageTargetRequested += _ => targetCount++;
            _view.ChatRequested += _ => chatCount++;
            _view.Render(CreateRenderData(false));

            FindComponent<Button>("CloseButtonImage").onClick.Invoke();
            FindComponent<Button>("DisplayButtonImage").onClick.Invoke();
            FindComponent<Button>("IndexButtonImage").onClick.Invoke();
            FindComponent<Button>("SignalButtonImage").onClick.Invoke();
            FindComponent<Button>("SignalTargetButtonImage").onClick.Invoke();
            FindComponent<Button>("ChatCommandButtonImage").onClick.Invoke();

            Assert.AreEqual(6, controlCount);
            Assert.AreEqual(1, closeCount);
            Assert.AreEqual(1, displayCount);
            Assert.AreEqual(1, indexCount);
            Assert.AreEqual(1, signalCount);
            Assert.AreEqual(1, targetCount);
            Assert.AreEqual(1, chatCount);
        }

        [Test]
        public void IndexControlsAndRows_Interact_RaiseParentSemanticRequests()
        {
            MessagesTab? tab = null;
            int selectAllCount = 0;
            int removalCount = 0;
            string selectedId = null;
            string activatedId = null;
            PointerEventData contextEvent = null;
            _view.TabRequested += (_, requested) => tab = requested;
            _view.MessageSelectAllRequested += _ => selectAllCount++;
            _view.MessageRemovalRequested += _ => removalCount++;
            _view.MessageRowSelected += (_, messageId) => selectedId = messageId;
            _view.MessageRowActivated += (_, messageId) => activatedId = messageId;
            _view.ContextRequested += (_, eventData) => contextEvent = eventData;
            _view.Render(CreateRenderData(false));
            MessageWindowRowView row = FindRows().Single();
            PointerEventData primary = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 2,
            };
            PointerEventData secondary = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
            };

            FindComponent<Button>("FleetTabButtonImage").onClick.Invoke();
            FindComponent<Button>("SelectAllButtonImage").onClick.Invoke();
            FindComponent<Button>("RemoveSelectedButtonImage").onClick.Invoke();
            row.OnPointerDown(primary);
            row.OnPointerClick(primary);
            row.OnPointerDown(secondary);

            Assert.AreEqual(MessagesTab.Fleet, tab);
            Assert.AreEqual(1, selectAllCount);
            Assert.AreEqual(1, removalCount);
            Assert.AreEqual("message-1", selectedId);
            Assert.AreEqual("message-1", activatedId);
            Assert.AreSame(secondary, contextEvent);
        }

        [Test]
        public void DetailNavigationButtons_Click_RaiseParentPreviousAndNextRequests()
        {
            int previousCount = 0;
            int nextCount = 0;
            _view.MessagePreviousRequested += _ => previousCount++;
            _view.MessageNextRequested += _ => nextCount++;

            FindComponent<Button>("DetailPreviousButtonImage").onClick.Invoke();
            FindComponent<Button>("DetailNextButtonImage").onClick.Invoke();

            Assert.AreEqual(1, previousCount);
            Assert.AreEqual(1, nextCount);
        }

        [Test]
        public void Awake_InitializedView_DoesNotDuplicateChildBindings()
        {
            int closeCount = 0;
            _view.CloseRequested += _ => closeCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
            FindComponent<Button>("CloseButtonImage").onClick.Invoke();

            Assert.AreEqual(1, closeCount);
        }

        [Test]
        public void Update_InactiveWindow_DoesNotRaiseKeyboardRequests()
        {
            int previousCount = 0;
            int nextCount = 0;
            int selectAllCount = 0;
            int removalCount = 0;
            _view.MessagePreviousRequested += _ => previousCount++;
            _view.MessageNextRequested += _ => nextCount++;
            _view.MessageSelectAllRequested += _ => selectAllCount++;
            _view.MessageRemovalRequested += _ => removalCount++;
            _windowShell.SetActiveWindow(false);

            UIComponentTestHelper.InvokeLifecycle(_view, "Update");

            Assert.AreEqual(0, previousCount);
            Assert.AreEqual(0, nextCount);
            Assert.AreEqual(0, selectAllCount);
            Assert.AreEqual(0, removalCount);
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsChildrenAndRaisesDestroyedEvent()
        {
            MessagesWindowView destroyed = null;
            int closeCount = 0;
            int selectAllCount = 0;
            int nextCount = 0;
            _view.Destroyed += view => destroyed = view;
            _view.CloseRequested += _ => closeCount++;
            _view.MessageSelectAllRequested += _ => selectAllCount++;
            _view.MessageNextRequested += _ => nextCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            FindComponent<Button>("CloseButtonImage").onClick.Invoke();
            FindComponent<Button>("SelectAllButtonImage").onClick.Invoke();
            FindComponent<Button>("DetailNextButtonImage").onClick.Invoke();

            Assert.AreSame(_view, destroyed);
            Assert.AreEqual(0, closeCount);
            Assert.AreEqual(0, selectAllCount);
            Assert.AreEqual(0, nextCount);
        }

        private MessagesWindowRenderData CreateRenderData(bool detailVisible)
        {
            return new MessagesWindowRenderData(
                detailVisible,
                new Vector2Int(21, 27),
                _texture,
                CreateCommandBar(),
                detailVisible ? null : CreateIndex(),
                detailVisible ? CreateDetail() : null
            );
        }

        private MessagesCommandBarRenderData CreateCommandBar()
        {
            MessagesCommandButtonRenderData button = new MessagesCommandButtonRenderData(
                _texture,
                _pressedTexture,
                true,
                true
            );
            return new MessagesCommandBarRenderData(
                _texture,
                button,
                button,
                button,
                button,
                button,
                button
            );
        }

        private MessagesIndexPanelRenderData CreateIndex()
        {
            MessagesTabRenderData[] tabs = Enumerable
                .Range(0, MessagesTabCatalog.Count)
                .Select(index => new MessagesTabRenderData(
                    MessagesTabCatalog.GetAt(index),
                    _texture,
                    _pressedTexture
                ))
                .ToArray();
            MessageWindowRowRenderData row = new MessageWindowRowRenderData(
                "message-1",
                "Incoming transmission",
                GameMessageType.Mission,
                false,
                true,
                _texture,
                _texture,
                _texture,
                Color.white
            );
            return new MessagesIndexPanelRenderData(
                MessagesTab.All,
                "All Messages",
                tabs,
                new[] { row }
            );
        }

        private MessagesDetailPanelRenderData CreateDetail()
        {
            return new MessagesDetailPanelRenderData(
                "message-1",
                "Message detail",
                "Transmission text",
                _texture,
                _texture,
                _texture,
                false,
                false
            );
        }

        private MessageWindowRowView[] FindRows()
        {
            return _windowObject
                .GetComponentsInChildren<MessageWindowRowView>(true)
                .Where(row => row.name.StartsWith("MessageRow", StringComparison.Ordinal))
                .ToArray();
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _windowObject
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }
    }
}
