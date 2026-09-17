using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Finder
{
    [TestFixture]
    public class FinderWindowViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/FinderWindow.prefab";

        private Texture2D _texture;
        private FinderWindowView _view;
        private GameObject _viewObject;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<FinderWindowView>();
            _texture = new Texture2D(72, 36);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_viewObject);
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
        /// Verifies render systems presentation applies frame search tabs and rows.
        /// </summary>
        [Test]
        public void Render_SystemsPresentation_AppliesFrameSearchTabsAndRows()
        {
            FinderWindowRenderData data = CreateRenderData(
                FinderMode.Systems,
                false,
                CreateFrame(false, CreateDialogButtons(2)),
                CreateTabs(3),
                new[]
                {
                    CreateRow("corellia", "Corellia", true, "1", "2", "3"),
                    CreateRow("coruscant", "Coruscant", false, "4"),
                }
            );

            _view.Render(data);

            RectInt rect = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(19, rect.x);
            Assert.AreEqual(27, rect.y);
            Assert.AreSame(_texture, FindComponent<RawImage>("BackgroundImage").texture);
            Assert.AreSame(_texture, FindComponent<RawImage>("OverlayFrameImage").texture);
            Assert.AreSame(_texture, FindComponent<RawImage>("ButtonStripImage").texture);
            Assert.AreEqual("Systems Finder", FindText("TitleTextField").text);
            Assert.AreEqual("System name:", FindText("LabelTextField").text);
            Assert.AreEqual("cor", FindComponent<TMP_InputField>("LabelInputField").text);
            Assert.AreEqual("All Systems", FindText("TabTitleTextField").text);
            Assert.IsTrue(FindObject("TabSlot0ButtonImage").activeSelf);
            Assert.IsTrue(FindObject("TabSlot2ButtonImage").activeSelf);
            Assert.IsFalse(FindObject("TabSlot3ButtonImage").activeSelf);
            Assert.IsTrue(FindObject("TwoButtonLayoutTargetButtonImage").activeSelf);
            Assert.IsTrue(FindObject("TwoButtonLayoutCloseButtonImage").activeSelf);
            Assert.IsFalse(FindObject("FourButtonLayoutTargetButtonImage").activeSelf);
            FinderWindowRowView[] rows = FindRows();
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("corellia", rows[0].RowId);
            Assert.AreEqual("Corellia", FindRowText(rows[0], "NameTextField").text);
            Assert.AreEqual("1", FindRowText(rows[0], "CountColumnSlot0TextField").text);
            Assert.AreEqual("3", FindRowText(rows[0], "CountColumnSlot2TextField").text);
            Assert.IsFalse(FindRowObject(rows[1], "CountColumnSlot1TextField").activeSelf);
        }

        /// <summary>
        /// Verifies render upper button layout hides button strip and uses upper slots.
        /// </summary>
        [Test]
        public void Render_UpperButtonLayout_HidesButtonStripAndUsesUpperSlots()
        {
            FinderWindowRenderData data = CreateRenderData(
                FinderMode.Fleets,
                false,
                CreateFrame(true, CreateDialogButtons(4)),
                CreateTabs(2),
                Array.Empty<FinderWindowRowRenderData>()
            );

            _view.Render(data);

            Assert.IsFalse(FindObject("ButtonStripImage").activeSelf);
            Assert.IsTrue(FindObject("UpperLayoutTargetButtonImage").activeSelf);
            Assert.IsTrue(FindObject("UpperLayoutShipButtonImage").activeSelf);
            Assert.IsTrue(FindObject("UpperLayoutFleetButtonImage").activeSelf);
            Assert.IsTrue(FindObject("UpperLayoutCloseButtonImage").activeSelf);
            Assert.IsFalse(FindObject("TwoButtonLayoutTargetButtonImage").activeSelf);
            Assert.IsFalse(FindObject("FourButtonLayoutTargetButtonImage").activeSelf);
        }

        /// <summary>
        /// Verifies render four button layout uses four lower slots.
        /// </summary>
        [Test]
        public void Render_FourButtonLayout_UsesFourLowerSlots()
        {
            FinderWindowRenderData data = CreateRenderData(
                FinderMode.Fleets,
                false,
                CreateFrame(false, CreateDialogButtons(4)),
                CreateTabs(2),
                Array.Empty<FinderWindowRowRenderData>()
            );

            _view.Render(data);

            Assert.IsTrue(FindObject("ButtonStripImage").activeSelf);
            Assert.IsTrue(FindObject("FourButtonLayoutTargetButtonImage").activeSelf);
            Assert.IsTrue(FindObject("FourButtonLayoutShipButtonImage").activeSelf);
            Assert.IsTrue(FindObject("FourButtonLayoutFleetButtonImage").activeSelf);
            Assert.IsTrue(FindObject("FourButtonLayoutCloseButtonImage").activeSelf);
            Assert.IsFalse(FindObject("TwoButtonLayoutTargetButtonImage").activeSelf);
            Assert.IsFalse(FindObject("UpperLayoutTargetButtonImage").activeSelf);
        }

        /// <summary>
        /// Verifies render command with source rect applies configured button bounds.
        /// </summary>
        [Test]
        public void Render_CommandWithSourceRect_AppliesConfiguredButtonBounds()
        {
            RectInt sourceRect = new RectInt(12, 18, 31, 29);
            FinderWindowDialogButtonRenderData[] buttons =
            {
                new FinderWindowDialogButtonRenderData(
                    FinderWindowCommand.Close,
                    _texture,
                    _texture,
                    sourceRect
                ),
                CreateDialogButton(FinderWindowCommand.Target),
            };

            _view.Render(
                CreateRenderData(
                    FinderMode.Systems,
                    false,
                    CreateFrame(false, buttons),
                    CreateTabs(1),
                    Array.Empty<FinderWindowRowRenderData>()
                )
            );

            Assert.AreEqual(
                sourceRect,
                UILayout.GetSourceRect(
                    FindComponent<RawImage>("TwoButtonLayoutCloseButtonImage").rectTransform
                )
            );
        }

        /// <summary>
        /// Verifies render none command hides its authored button slot.
        /// </summary>
        [Test]
        public void Render_NoneCommand_HidesItsAuthoredButtonSlot()
        {
            FinderWindowDialogButtonRenderData[] buttons =
            {
                new FinderWindowDialogButtonRenderData(
                    FinderWindowCommand.None,
                    _texture,
                    _texture,
                    null
                ),
                CreateDialogButton(FinderWindowCommand.Target),
            };

            _view.Render(
                CreateRenderData(
                    FinderMode.Systems,
                    false,
                    CreateFrame(false, buttons),
                    CreateTabs(1),
                    Array.Empty<FinderWindowRowRenderData>()
                )
            );

            Assert.IsFalse(FindObject("TwoButtonLayoutCloseButtonImage").activeSelf);
            Assert.IsFalse(FindComponent<Button>("TwoButtonLayoutCloseButtonImage").interactable);
            Assert.IsTrue(FindObject("TwoButtonLayoutTargetButtonImage").activeSelf);
        }

        /// <summary>
        /// Verifies render too many tabs throws missing reference exception.
        /// </summary>
        [Test]
        public void Render_TooManyTabs_ThrowsMissingReferenceException()
        {
            FinderWindowRenderData data = CreateRenderData(
                FinderMode.Systems,
                false,
                CreateFrame(false, CreateDialogButtons(2)),
                CreateTabs(6),
                Array.Empty<FinderWindowRowRenderData>()
            );

            Assert.Throws<MissingReferenceException>(() => _view.Render(data));
        }

        /// <summary>
        /// Verifies render too many upper commands throws missing reference exception.
        /// </summary>
        [Test]
        public void Render_TooManyUpperCommands_ThrowsMissingReferenceException()
        {
            FinderWindowRenderData data = CreateRenderData(
                FinderMode.Fleets,
                false,
                CreateFrame(true, CreateDialogButtons(5)),
                CreateTabs(1),
                Array.Empty<FinderWindowRowRenderData>()
            );

            Assert.Throws<MissingReferenceException>(() => _view.Render(data));
        }

        /// <summary>
        /// Verifies render troops mode uses compact tab default title and rows layout.
        /// </summary>
        [Test]
        public void Render_TroopsMode_UsesCompactTabDefaultTitleAndRowsLayout()
        {
            FinderWindowRenderData data = CreateRenderData(
                FinderMode.Troops,
                false,
                CreateFrame(false, CreateDialogButtons(2)),
                CreateTabs(2),
                new[] { CreateRow("troops", "Troopers", true, "3") }
            );

            _view.Render(data);

            Assert.AreEqual(
                UILayout.GetSourceRect(FindRect("CompactTabSlot0Template")),
                UILayout.GetSourceRect(FindComponent<RawImage>("TabSlot0ButtonImage").rectTransform)
            );
            Assert.AreEqual(
                UILayout.GetSourceRect(FindRect("DefaultTabTitleTextTemplate")),
                UILayout.GetSourceRect(FindText("TabTitleTextField").rectTransform)
            );
            Assert.AreEqual(25f, FindRows().Single().GetComponent<LayoutElement>().preferredHeight);
            Assert.AreEqual(
                new RectInt(38, 143, 348, 161),
                UILayout.GetSourceRect(FindObject("RowsScrollArea").transform as RectTransform)
            );
        }

        /// <summary>
        /// Verifies render personnel list uses personnel row and compact subjects.
        /// </summary>
        [Test]
        public void Render_PersonnelList_UsesPersonnelRowAndCompactSubjects()
        {
            FinderWindowRenderData data = CreateRenderData(
                FinderMode.Personnel,
                false,
                CreateFrame(false, CreateDialogButtons(2)),
                CreateTabs(2),
                new[] { CreateRow("leia", "Leia Organa", true, "1", "2") }
            );

            _view.Render(data);

            FinderWindowRowView row = FindRows().Single();
            Assert.AreEqual(
                UILayout.GetSourceRect(FindRect("PersonnelRowTemplate")).height,
                row.GetComponent<LayoutElement>().preferredHeight
            );
            Assert.AreEqual(
                UILayout.GetSourceRect(FindRect("CompactTabTitleTextTemplate")),
                UILayout.GetSourceRect(FindText("TabTitleTextField").rectTransform)
            );
            Assert.AreEqual(
                new RectInt(38, 132, 348, 172),
                UILayout.GetSourceRect(FindObject("RowsScrollArea").transform as RectTransform)
            );
        }

        /// <summary>
        /// Verifies render personnel panel uses panel row and default subjects.
        /// </summary>
        [Test]
        public void Render_PersonnelPanel_UsesPanelRowAndDefaultSubjects()
        {
            FinderWindowRenderData data = CreateRenderData(
                FinderMode.Personnel,
                true,
                CreateFrame(false, CreateDialogButtons(2)),
                CreateTabs(2),
                new[] { CreateRow("han", "Han Solo", true, "1", "2", "3") }
            );

            _view.Render(data);

            FinderWindowRowView row = FindRows().Single();
            Assert.AreEqual(
                UILayout.GetSourceRect(FindRect("PersonnelPanelRowTemplate")).height,
                row.GetComponent<LayoutElement>().preferredHeight
            );
            Assert.AreEqual(
                UILayout.GetSourceRect(FindRect("DefaultTabTitleTextTemplate")),
                UILayout.GetSourceRect(FindText("TabTitleTextField").rectTransform)
            );
            Assert.AreEqual(
                new RectInt(38, 142, 348, 162),
                UILayout.GetSourceRect(FindObject("RowsScrollArea").transform as RectTransform)
            );
        }

        /// <summary>
        /// Verifies render shorter row collection hides unused cached rows.
        /// </summary>
        [Test]
        public void Render_ShorterRowCollection_HidesUnusedCachedRows()
        {
            _view.Render(
                CreateRenderData(
                    FinderMode.Systems,
                    false,
                    CreateFrame(false, CreateDialogButtons(2)),
                    CreateTabs(2),
                    new[]
                    {
                        CreateRow("first", "First", true),
                        CreateRow("second", "Second", false),
                    }
                )
            );
            FinderWindowRowView secondRow = FindRows()[1];

            _view.Render(
                CreateRenderData(
                    FinderMode.Systems,
                    false,
                    CreateFrame(false, CreateDialogButtons(2)),
                    CreateTabs(2),
                    new[] { CreateRow("replacement", "Replacement", true) }
                )
            );

            Assert.IsFalse(secondRow.gameObject.activeSelf);
            Assert.AreEqual("replacement", FindRows()[0].RowId);
            Assert.AreEqual("Replacement", FindRowText(FindRows()[0], "NameTextField").text);
        }

        /// <summary>
        /// Verifies search input value changed raises normalized search request.
        /// </summary>
        [Test]
        public void SearchInput_ValueChanged_RaisesNormalizedSearchRequest()
        {
            string received = null;
            _view.SearchTextChanged += (_, value) => received = value;
            TMP_InputField input = FindComponent<TMP_InputField>("LabelInputField");

            input.onValueChanged.Invoke(null);

            Assert.AreEqual(string.Empty, received);
        }

        /// <summary>
        /// Verifies tab button press then click raises focus without control press.
        /// </summary>
        [Test]
        public void TabButton_PressThenClick_RaisesFocusWithoutControlPress()
        {
            int controlCount = 0;
            int focusCount = 0;
            int selectedTab = -1;
            _view.ControlPressed += _ => controlCount++;
            _view.FocusRequested += _ => focusCount++;
            _view.TabSelected += (_, index) => selectedTab = index;
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            FindComponent<RawImagePressVisual>("TabSlot3ButtonImage").OnPointerDown(eventData);
            FindComponent<Button>("TabSlot3ButtonImage").onClick.Invoke();

            Assert.AreEqual(0, controlCount);
            Assert.AreEqual(1, focusCount);
            Assert.AreEqual(3, selectedTab);
        }

        /// <summary>
        /// Verifies dialog button press then click raises control before rendered semantic command.
        /// </summary>
        [Test]
        public void DialogButton_PressThenClick_RaisesControlBeforeRenderedSemanticCommand()
        {
            int controlCount = 0;
            int focusCount = 0;
            FinderWindowCommand command = FinderWindowCommand.None;
            _view.ControlPressed += _ => controlCount++;
            _view.FocusRequested += _ => focusCount++;
            _view.CommandRequested += (_, requested) => command = requested;
            _view.Render(
                CreateRenderData(
                    FinderMode.Systems,
                    false,
                    CreateFrame(false, CreateDialogButtons(2)),
                    CreateTabs(1),
                    Array.Empty<FinderWindowRowRenderData>()
                )
            );
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            FindComponent<RawImagePressVisual>("TwoButtonLayoutCloseButtonImage")
                .OnPointerDown(eventData);

            Assert.AreEqual(1, controlCount);
            Assert.AreEqual(0, focusCount);
            Assert.AreEqual(FinderWindowCommand.None, command);
            FindComponent<Button>("TwoButtonLayoutCloseButtonImage").onClick.Invoke();

            Assert.AreEqual(1, controlCount);
            Assert.AreEqual(1, focusCount);
            Assert.AreEqual(FinderWindowCommand.Close, command);
        }

        /// <summary>
        /// Verifies row gestures rendered row raise selection activation and context requests.
        /// </summary>
        [Test]
        public void RowGestures_RenderedRow_RaiseSelectionActivationAndContextRequests()
        {
            int focusCount = 0;
            string selected = null;
            string activated = null;
            string contextRowId = null;
            PointerEventData contextEvent = null;
            _view.FocusRequested += _ => focusCount++;
            _view.RowSelected += (_, rowId) => selected = rowId;
            _view.RowActivated += (_, rowId) => activated = rowId;
            _view.ContextRequested += (_, rowId, eventData) =>
            {
                contextRowId = rowId;
                contextEvent = eventData;
            };
            _view.Render(
                CreateRenderData(
                    FinderMode.Systems,
                    false,
                    CreateFrame(false, CreateDialogButtons(2)),
                    CreateTabs(1),
                    new[] { CreateRow("corellia", "Corellia", true) }
                )
            );
            FinderWindowRowView row = FindRows().Single();
            PointerEventData left = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 2,
            };
            PointerEventData right = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
            };

            row.OnPointerDown(left);
            row.OnPointerClick(left);
            row.OnPointerDown(right);

            Assert.AreEqual(2, focusCount);
            Assert.AreEqual("corellia", selected);
            Assert.AreEqual("corellia", activated);
            Assert.AreEqual("corellia", contextRowId);
            Assert.AreSame(right, contextEvent);
        }

        /// <summary>
        /// Verifies on destroy initialized view unbinds controls and raises destroyed event.
        /// </summary>
        [Test]
        public void OnDestroy_InitializedView_UnbindsControlsAndRaisesDestroyedEvent()
        {
            FinderWindowView destroyed = null;
            int controlCount = 0;
            int tabCount = 0;
            int searchCount = 0;
            _view.Destroyed += view => destroyed = view;
            _view.ControlPressed += _ => controlCount++;
            _view.TabSelected += (_, _) => tabCount++;
            _view.SearchTextChanged += (_, _) => searchCount++;
            Button tab = FindComponent<Button>("TabSlot0ButtonImage");
            TMP_InputField input = FindComponent<TMP_InputField>("LabelInputField");
            _view.Render(
                CreateRenderData(
                    FinderMode.Systems,
                    false,
                    CreateFrame(false, CreateDialogButtons(2)),
                    CreateTabs(1),
                    Array.Empty<FinderWindowRowRenderData>()
                )
            );
            controlCount = 0;
            tabCount = 0;
            searchCount = 0;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            FindComponent<RawImagePressVisual>("TwoButtonLayoutCloseButtonImage")
                .OnPointerDown(
                    new PointerEventData(null) { button = PointerEventData.InputButton.Left }
                );
            tab.onClick.Invoke();
            input.onValueChanged.Invoke("ignored");

            Assert.AreSame(_view, destroyed);
            Assert.AreEqual(0, controlCount);
            Assert.AreEqual(0, tabCount);
            Assert.AreEqual(0, searchCount);
        }

        /// <summary>
        /// Creates render data.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="panel">Whether panel.</param>
        /// <param name="frame">The frame.</param>
        /// <param name="tabs">The tabs.</param>
        /// <param name="rows">The rows.</param>
        /// <returns>The created render data.</returns>
        private FinderWindowRenderData CreateRenderData(
            FinderMode mode,
            bool panel,
            FinderWindowFrameRenderData frame,
            FinderWindowTabRenderData[] tabs,
            FinderWindowRowRenderData[] rows
        )
        {
            return new FinderWindowRenderData(
                mode,
                panel,
                0,
                rows.Length == 0 ? -1 : 0,
                "cor",
                $"{mode} Finder",
                "System name:",
                mode == FinderMode.Systems ? "All Systems" : mode.ToString(),
                frame,
                tabs,
                rows
            );
        }

        /// <summary>
        /// Creates frame.
        /// </summary>
        /// <param name="useUpperButtonLayout">Whether use upper button layout.</param>
        /// <param name="buttons">The buttons.</param>
        /// <returns>The created frame.</returns>
        private FinderWindowFrameRenderData CreateFrame(
            bool useUpperButtonLayout,
            FinderWindowDialogButtonRenderData[] buttons
        )
        {
            return new FinderWindowFrameRenderData(
                19,
                27,
                470,
                331,
                true,
                useUpperButtonLayout,
                _texture,
                _texture,
                _texture,
                buttons
            );
        }

        /// <summary>
        /// Creates dialog buttons.
        /// </summary>
        /// <param name="count">The count.</param>
        /// <returns>The created dialog buttons.</returns>
        private FinderWindowDialogButtonRenderData[] CreateDialogButtons(int count)
        {
            FinderWindowCommand[] commands =
            {
                FinderWindowCommand.Close,
                FinderWindowCommand.Target,
                FinderWindowCommand.ShowShips,
                FinderWindowCommand.ShowFleets,
                FinderWindowCommand.ShowPersonnel,
            };
            return Enumerable
                .Range(0, count)
                .Select(index => CreateDialogButton(commands[index]))
                .ToArray();
        }

        /// <summary>
        /// Creates dialog button.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The created dialog button.</returns>
        private FinderWindowDialogButtonRenderData CreateDialogButton(FinderWindowCommand command)
        {
            return new FinderWindowDialogButtonRenderData(command, _texture, _texture, null);
        }

        /// <summary>
        /// Creates tabs.
        /// </summary>
        /// <param name="count">The count.</param>
        /// <returns>The created tabs.</returns>
        private FinderWindowTabRenderData[] CreateTabs(int count)
        {
            return Enumerable
                .Range(0, count)
                .Select(_ => new FinderWindowTabRenderData(_texture, _texture))
                .ToArray();
        }

        /// <summary>
        /// Creates row.
        /// </summary>
        /// <param name="rowId">The row id.</param>
        /// <param name="name">The name.</param>
        /// <param name="selected">Whether selected.</param>
        /// <param name="counts">The counts.</param>
        /// <returns>The created row.</returns>
        private static FinderWindowRowRenderData CreateRow(
            string rowId,
            string name,
            bool selected,
            params string[] counts
        )
        {
            return new FinderWindowRowRenderData(rowId, name, selected, counts);
        }

        /// <summary>
        /// Finds rows.
        /// </summary>
        /// <returns>The matching rows.</returns>
        private FinderWindowRowView[] FindRows()
        {
            return _viewObject
                .GetComponentsInChildren<FinderWindowRowView>(true)
                .Where(row => row.name.StartsWith("FinderRow", StringComparison.Ordinal))
                .OrderBy(row => row.Index)
                .ToArray();
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
            return _viewObject
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
            return _viewObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        /// <summary>
        /// Finds rect.
        /// </summary>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching rect.</returns>
        private RectTransform FindRect(string objectName)
        {
            return FindObject(objectName).transform as RectTransform;
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

        /// <summary>
        /// Finds row object.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching row object.</returns>
        private static GameObject FindRowObject(FinderWindowRowView row, string objectName)
        {
            return row.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        /// <summary>
        /// Finds row text.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching row text.</returns>
        private static TextMeshProUGUI FindRowText(FinderWindowRowView row, string objectName)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(item => item.name == objectName);
        }
    }
}
