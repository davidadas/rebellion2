using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Messages;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GameMessageType = Rebellion.Game.Messages.MessageType;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesIndexPanelViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/MessagesWindow.prefab";

        private Texture2D _normalIconTexture;
        private Texture2D _pressedTexture;
        private Texture2D _selectedIconTexture;
        private Texture2D _selectionTexture;
        private Texture2D _texture;
        private MessagesIndexPanelView _view;
        private GameObject _windowObject;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _windowObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _windowObject.GetComponentInChildren<MessagesIndexPanelView>(true);
            _texture = new Texture2D(32, 24);
            _pressedTexture = new Texture2D(32, 24);
            _selectionTexture = new Texture2D(64, 16);
            _selectedIconTexture = new Texture2D(16, 16);
            _normalIconTexture = new Texture2D(16, 16);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
            _view.Initialize(() => false, _windowObject.transform);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_normalIconTexture);
            UnityEngine.Object.DestroyImmediate(_selectedIconTexture);
            UnityEngine.Object.DestroyImmediate(_selectionTexture);
            UnityEngine.Object.DestroyImmediate(_pressedTexture);
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        /// <summary>
        /// Verifies initialize null navigation predicate throws argument null exception.
        /// </summary>
        [Test]
        public void Initialize_NullNavigationPredicate_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _view.Initialize(null, _windowObject.transform)
            );
        }

        /// <summary>
        /// Verifies initialize null navigation scope throws argument null exception.
        /// </summary>
        [Test]
        public void Initialize_NullNavigationScope_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Initialize(() => true, null));
        }

        /// <summary>
        /// Verifies render null data throws argument null exception.
        /// </summary>
        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        /// <summary>
        /// Verifies render index applies tabs title rows and selection artwork.
        /// </summary>
        [Test]
        public void Render_Index_AppliesTabsTitleRowsAndSelectionArtwork()
        {
            MessagesIndexPanelRenderData data = CreateIndex(
                new[]
                {
                    CreateRow("message-1", "First transmission", true, Color.white),
                    CreateRow("message-2", "Second transmission", false, Color.gray),
                }
            );

            _view.Render(data);

            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.AreEqual("Mission Messages", FindText("TitleTextField").text);
            Assert.AreSame(_texture, FindComponent<RawImage>("AllTabButtonImage").texture);
            Assert.IsTrue(FindComponent<Button>("AllTabButtonImage").interactable);
            Assert.AreSame(_texture, FindComponent<RawImage>("AdviceTabButtonImage").texture);
            MessageWindowRowView[] rows = FindRows();
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("message-1", rows[0].MessageId);
            Assert.AreEqual("First transmission", FindRowText(rows[0]).text);
            Assert.AreSame(
                _selectionTexture,
                FindRowComponent<RawImage>(rows[0], "SelectionImage").texture
            );
            Assert.AreSame(
                _selectedIconTexture,
                FindRowComponent<RawImage>(rows[0], "IconImage").texture
            );
            Assert.IsNull(FindRowComponent<RawImage>(rows[1], "SelectionImage").texture);
            Assert.AreSame(
                _normalIconTexture,
                FindRowComponent<RawImage>(rows[1], "IconImage").texture
            );
        }

        /// <summary>
        /// Verifies render tab without texture disables authored control.
        /// </summary>
        [Test]
        public void Render_TabWithoutTexture_DisablesAuthoredControl()
        {
            MessagesTabRenderData[] tabs = CreateTabs();
            tabs[2] = new MessagesTabRenderData(MessagesTab.Fleet, null, null);
            MessagesIndexPanelRenderData data = new MessagesIndexPanelRenderData(
                MessagesTab.Fleet,
                "Fleet Messages",
                tabs,
                Array.Empty<MessageWindowRowRenderData>()
            );

            _view.Render(data);

            Assert.IsFalse(FindObject("FleetTabButtonImage").activeSelf);
            Assert.IsFalse(FindComponent<Button>("FleetTabButtonImage").interactable);
            Assert.IsFalse(FindComponent<RawImage>("FleetTabButtonImage").raycastTarget);
        }

        /// <summary>
        /// Verifies render incomplete tabs throws argument exception.
        /// </summary>
        [Test]
        public void Render_IncompleteTabs_ThrowsArgumentException()
        {
            MessagesIndexPanelRenderData data = new MessagesIndexPanelRenderData(
                MessagesTab.All,
                "All Messages",
                CreateTabs().Take(MessagesTabCatalog.Count - 1).ToArray(),
                Array.Empty<MessageWindowRowRenderData>()
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
        }

        /// <summary>
        /// Verifies render tabs outside authored order throws argument exception.
        /// </summary>
        [Test]
        public void Render_TabsOutsideAuthoredOrder_ThrowsArgumentException()
        {
            MessagesTabRenderData[] tabs = CreateTabs();
            tabs[0] = new MessagesTabRenderData(MessagesTab.Support, _texture, _pressedTexture);
            MessagesIndexPanelRenderData data = new MessagesIndexPanelRenderData(
                MessagesTab.All,
                "All Messages",
                tabs,
                Array.Empty<MessageWindowRowRenderData>()
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
        }

        /// <summary>
        /// Verifies render shorter rows hides unused cached row views.
        /// </summary>
        [Test]
        public void Render_ShorterRows_HidesUnusedCachedRowViews()
        {
            _view.Render(
                CreateIndex(
                    new[]
                    {
                        CreateRow("message-1", "First", false, Color.white),
                        CreateRow("message-2", "Second", false, Color.white),
                    }
                )
            );
            MessageWindowRowView secondRow = FindRows()[1];

            _view.Render(
                CreateIndex(new[] { CreateRow("message-3", "Replacement", true, Color.white) })
            );

            Assert.IsFalse(secondRow.gameObject.activeSelf);
            Assert.AreEqual("message-3", FindRows().Single().MessageId);
            Assert.AreEqual("Replacement", FindRowText(FindRows().Single()).text);
        }

        /// <summary>
        /// Verifies index controls click raise tab select all and remove requests.
        /// </summary>
        [Test]
        public void IndexControls_Click_RaiseTabSelectAllAndRemoveRequests()
        {
            MessagesTab? tab = null;
            int selectAllCount = 0;
            int removeCount = 0;
            _view.TabRequested += requested => tab = requested;
            _view.SelectAllRequested += () => selectAllCount++;
            _view.RemoveSelectedRequested += () => removeCount++;

            FindComponent<Button>("MissionTabButtonImage").onClick.Invoke();
            FindComponent<Button>("SelectAllButtonImage").onClick.Invoke();
            FindComponent<Button>("RemoveSelectedButtonImage").onClick.Invoke();

            Assert.AreEqual(MessagesTab.Mission, tab);
            Assert.AreEqual(1, selectAllCount);
            Assert.AreEqual(1, removeCount);
        }

        /// <summary>
        /// Verifies row gestures rendered row raise selection activation and context requests.
        /// </summary>
        [Test]
        public void RowGestures_RenderedRow_RaiseSelectionActivationAndContextRequests()
        {
            string selected = null;
            string activated = null;
            PointerEventData context = null;
            _view.RowClicked += messageId => selected = messageId;
            _view.RowDoubleClicked += messageId => activated = messageId;
            _view.ContextRequested += eventData => context = eventData;
            _view.Render(
                CreateIndex(new[] { CreateRow("message-4", "Report", false, Color.white) })
            );
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

            row.OnPointerDown(primary);
            row.OnPointerClick(primary);
            row.OnPointerDown(secondary);

            Assert.AreEqual("message-4", selected);
            Assert.AreEqual("message-4", activated);
            Assert.AreSame(secondary, context);
        }

        /// <summary>
        /// Verifies hide visible panel deactivates panel.
        /// </summary>
        [Test]
        public void Hide_VisiblePanel_DeactivatesPanel()
        {
            _view.Render(CreateIndex(Array.Empty<MessageWindowRowRenderData>()));

            _view.Hide();

            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        /// <summary>
        /// Verifies on destroy initialized view unbinds index controls.
        /// </summary>
        [Test]
        public void OnDestroy_InitializedView_UnbindsIndexControls()
        {
            int tabCount = 0;
            int selectAllCount = 0;
            int removeCount = 0;
            _view.TabRequested += _ => tabCount++;
            _view.SelectAllRequested += () => selectAllCount++;
            _view.RemoveSelectedRequested += () => removeCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            FindComponent<Button>("MissionTabButtonImage").onClick.Invoke();
            FindComponent<Button>("SelectAllButtonImage").onClick.Invoke();
            FindComponent<Button>("RemoveSelectedButtonImage").onClick.Invoke();

            Assert.AreEqual(0, tabCount);
            Assert.AreEqual(0, selectAllCount);
            Assert.AreEqual(0, removeCount);
        }

        /// <summary>
        /// Creates index.
        /// </summary>
        /// <param name="rows">The rows.</param>
        /// <returns>The created index.</returns>
        private MessagesIndexPanelRenderData CreateIndex(MessageWindowRowRenderData[] rows)
        {
            return new MessagesIndexPanelRenderData(
                MessagesTab.Mission,
                "Mission Messages",
                CreateTabs(),
                rows
            );
        }

        /// <summary>
        /// Creates tabs.
        /// </summary>
        /// <returns>The created tabs.</returns>
        private MessagesTabRenderData[] CreateTabs()
        {
            return Enumerable
                .Range(0, MessagesTabCatalog.Count)
                .Select(index => new MessagesTabRenderData(
                    MessagesTabCatalog.GetAt(index),
                    _texture,
                    _pressedTexture
                ))
                .ToArray();
        }

        /// <summary>
        /// Creates row.
        /// </summary>
        /// <param name="messageId">The message id.</param>
        /// <param name="header">The header.</param>
        /// <param name="selected">Whether selected.</param>
        /// <param name="headerColor">The header color.</param>
        /// <returns>The created row.</returns>
        private MessageWindowRowRenderData CreateRow(
            string messageId,
            string header,
            bool selected,
            Color32 headerColor
        )
        {
            return new MessageWindowRowRenderData(
                messageId,
                header,
                GameMessageType.Mission,
                selected,
                false,
                _selectionTexture,
                _selectedIconTexture,
                _normalIconTexture,
                headerColor
            );
        }

        /// <summary>
        /// Finds rows.
        /// </summary>
        /// <returns>The matching rows.</returns>
        private MessageWindowRowView[] FindRows()
        {
            return _windowObject
                .GetComponentsInChildren<MessageWindowRowView>(true)
                .Where(row => row.name.StartsWith("MessageRow", StringComparison.Ordinal))
                .Where(row => row.gameObject.activeSelf)
                .OrderBy(row => row.Index)
                .ToArray();
        }

        /// <summary>
        /// Finds row text.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns>The matching row text.</returns>
        private static TextMeshProUGUI FindRowText(MessageWindowRowView row)
        {
            return FindRowComponent<TextMeshProUGUI>(row, "HeaderTextField");
        }

        /// <summary>
        /// Finds row component.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="objectName">The object name.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The matching row component.</returns>
        private static T FindRowComponent<T>(MessageWindowRowView row, string objectName)
            where T : Component
        {
            return row.GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        /// <summary>
        /// Finds component.
        /// </summary>
        /// <param name="objectName">The object name.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The matching component.</returns>
        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _windowObject
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        /// <summary>
        /// Finds object.
        /// </summary>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching object.</returns>
        private GameObject FindObject(string objectName)
        {
            return _windowObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        /// <summary>
        /// Finds text.
        /// </summary>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching text.</returns>
        private TextMeshProUGUI FindText(string objectName)
        {
            return FindComponent<TextMeshProUGUI>(objectName);
        }
    }
}
