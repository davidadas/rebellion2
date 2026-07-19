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

        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<FinderWindowView>();
            _texture = new Texture2D(72, 36);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_viewObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

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

        [Test]
        public void Render_PersonnelList_UsesPersonnelRowAndCompactTitleTemplates()
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

        [Test]
        public void Render_PersonnelPanel_UsesPanelRowAndDefaultTitleTemplates()
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

        [Test]
        public void SearchInput_ValueChanged_RaisesNormalizedSearchRequest()
        {
            string received = null;
            _view.SearchTextChanged += (_, value) => received = value;
            TMP_InputField input = FindComponent<TMP_InputField>("LabelInputField");

            input.onValueChanged.Invoke(null);

            Assert.AreEqual(string.Empty, received);
        }

        [Test]
        public void TabButton_Click_RaisesFocusAndStableTabIndex()
        {
            int focusCount = 0;
            int selectedTab = -1;
            _view.FocusRequested += _ => focusCount++;
            _view.TabSelected += (_, index) => selectedTab = index;

            FindComponent<Button>("TabSlot3ButtonImage").onClick.Invoke();

            Assert.AreEqual(1, focusCount);
            Assert.AreEqual(3, selectedTab);
        }

        [Test]
        public void DialogButton_Click_RaisesFocusAndRenderedSemanticCommand()
        {
            int focusCount = 0;
            FinderWindowCommand command = FinderWindowCommand.None;
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

            FindComponent<Button>("TwoButtonLayoutCloseButtonImage").onClick.Invoke();

            Assert.AreEqual(1, focusCount);
            Assert.AreEqual(FinderWindowCommand.Close, command);
        }

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

        [Test]
        public void OnDestroy_InitializedView_UnbindsControlsAndRaisesDestroyedEvent()
        {
            FinderWindowView destroyed = null;
            int tabCount = 0;
            int searchCount = 0;
            _view.Destroyed += view => destroyed = view;
            _view.TabSelected += (_, _) => tabCount++;
            _view.SearchTextChanged += (_, _) => searchCount++;
            Button tab = FindComponent<Button>("TabSlot0ButtonImage");
            TMP_InputField input = FindComponent<TMP_InputField>("LabelInputField");

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            tab.onClick.Invoke();
            input.onValueChanged.Invoke("ignored");

            Assert.AreSame(_view, destroyed);
            Assert.AreEqual(0, tabCount);
            Assert.AreEqual(0, searchCount);
        }

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

        private FinderWindowDialogButtonRenderData CreateDialogButton(FinderWindowCommand command)
        {
            return new FinderWindowDialogButtonRenderData(command, _texture, _texture, null);
        }

        private FinderWindowTabRenderData[] CreateTabs(int count)
        {
            return Enumerable
                .Range(0, count)
                .Select(_ => new FinderWindowTabRenderData(_texture, _texture))
                .ToArray();
        }

        private static FinderWindowRowRenderData CreateRow(
            string rowId,
            string name,
            bool selected,
            params string[] counts
        )
        {
            return new FinderWindowRowRenderData(rowId, name, selected, counts);
        }

        private FinderWindowRowView[] FindRows()
        {
            return _viewObject
                .GetComponentsInChildren<FinderWindowRowView>(true)
                .Where(row => row.name.StartsWith("FinderRow", StringComparison.Ordinal))
                .OrderBy(row => row.Index)
                .ToArray();
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _viewObject
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        private GameObject FindObject(string objectName)
        {
            return _viewObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        private RectTransform FindRect(string objectName)
        {
            return FindObject(objectName).transform as RectTransform;
        }

        private TextMeshProUGUI FindText(string objectName)
        {
            return FindComponent<TextMeshProUGUI>(objectName);
        }

        private static GameObject FindRowObject(FinderWindowRowView row, string objectName)
        {
            return row.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        private static TextMeshProUGUI FindRowText(FinderWindowRowView row, string objectName)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(item => item.name == objectName);
        }
    }
}
