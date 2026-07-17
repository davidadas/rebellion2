using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Encyclopedia
{
    [TestFixture]
    public class EncyclopediaWindowViewTests
    {
        private const string _prefabPath =
            "Assets/Prefabs/UI/StrategyView/EncyclopediaWindow.prefab";

        private EncyclopediaDetailPanelView _detailPanel;
        private EncyclopediaIndexPanelView _indexPanel;
        private Texture2D _texture;
        private EncyclopediaWindowView _view;
        private GameObject _viewObject;

        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<EncyclopediaWindowView>();
            _indexPanel = _viewObject.GetComponentInChildren<EncyclopediaIndexPanelView>(true);
            _detailPanel = _viewObject.GetComponentInChildren<EncyclopediaDetailPanelView>(true);
            _texture = new Texture2D(88, 44);
            UIComponentTestHelper.InvokeLifecycle(_indexPanel, "Awake");
            UIComponentTestHelper.InvokeLifecycle(_detailPanel, "Awake");
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
        public void Render_IndexPanel_AppliesFrameSearchTabsTitleAndRows()
        {
            EncyclopediaWindowRenderData data = CreateRenderData(
                false,
                CreateFrame(false),
                CreateIndex(
                    new[]
                    {
                        CreateRow("100", "Corellia", true),
                        CreateRow("101", "Coruscant", false),
                    }
                ),
                CreateDetail()
            );

            _view.Render(data);

            RectInt rect = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(16, rect.x);
            Assert.AreEqual(24, rect.y);
            Assert.AreSame(_texture, FindComponent<RawImage>("OverlayFrameImage").texture);
            Assert.AreSame(_texture, FindComponent<RawImage>("ButtonStripImage").texture);
            Assert.IsTrue(FindObject("IndexPanel").activeSelf);
            Assert.IsFalse(FindObject("DetailPanel").activeSelf);
            Assert.AreEqual("cor", FindComponent<TMP_InputField>("EntryNameInputField").text);
            Assert.AreEqual("System Database", FindText("TabTitleTextField").text);
            Assert.IsTrue(FindObject("AllDatabasesTabButtonImage").activeSelf);
            Assert.IsTrue(FindObject("PersonnelTabButtonImage").activeSelf);
            Assert.AreSame(_texture, FindComponent<RawImage>("SystemsTabButtonImage").texture);
            Assert.IsTrue(FindObject("LowerLayoutCloseButtonImage").activeSelf);
            Assert.IsFalse(FindObject("UpperLayoutCloseButtonImage").activeSelf);
            EncyclopediaWindowRowView[] rows = FindRows();
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("100", rows[0].EntryTypeId);
            Assert.AreEqual("Corellia", FindRowText(rows[0]).text);
            Assert.AreEqual(Color.white, FindRowText(rows[0]).color);
            Assert.AreEqual(new Color32(128, 128, 128, 255), (Color32)FindRowText(rows[1]).color);
        }

        [Test]
        public void Render_UpperButtonLayout_HidesStripAndUsesUpperSlots()
        {
            EncyclopediaWindowRenderData data = CreateRenderData(
                false,
                CreateFrame(true),
                CreateIndex(Array.Empty<EncyclopediaWindowRowRenderData>()),
                CreateDetail()
            );

            _view.Render(data);

            Assert.IsFalse(FindObject("ButtonStripImage").activeSelf);
            Assert.IsTrue(FindObject("UpperLayoutCloseButtonImage").activeSelf);
            Assert.IsTrue(FindObject("UpperLayoutTopicButtonImage").activeSelf);
            Assert.IsTrue(FindObject("UpperLayoutIndexButtonImage").activeSelf);
            Assert.IsFalse(FindObject("LowerLayoutCloseButtonImage").activeSelf);
        }

        [Test]
        public void Render_CommandSourceRect_AppliesConfiguredBounds()
        {
            RectInt sourceRect = new RectInt(13, 19, 33, 27);
            EncyclopediaDialogButtonRenderData[] buttons =
            {
                new EncyclopediaDialogButtonRenderData(
                    EncyclopediaWindowCommand.Close,
                    _texture,
                    _texture,
                    sourceRect
                ),
                CreateDialogButton(EncyclopediaWindowCommand.ShowTopic),
                CreateDialogButton(EncyclopediaWindowCommand.ShowIndex),
            };
            EncyclopediaWindowFrameRenderData frame = CreateFrame(false, buttons);

            _view.Render(
                CreateRenderData(
                    false,
                    frame,
                    CreateIndex(Array.Empty<EncyclopediaWindowRowRenderData>()),
                    CreateDetail()
                )
            );

            Assert.AreEqual(
                sourceRect,
                UILayout.GetSourceRect(
                    FindComponent<RawImage>("LowerLayoutCloseButtonImage").rectTransform
                )
            );
        }

        [Test]
        public void Render_TooManyCommands_ThrowsMissingReferenceException()
        {
            EncyclopediaDialogButtonRenderData[] buttons =
            {
                CreateDialogButton(EncyclopediaWindowCommand.Close),
                CreateDialogButton(EncyclopediaWindowCommand.ShowTopic),
                CreateDialogButton(EncyclopediaWindowCommand.ShowIndex),
                CreateDialogButton(EncyclopediaWindowCommand.Close),
            };
            EncyclopediaWindowRenderData data = CreateRenderData(
                false,
                CreateFrame(false, buttons),
                CreateIndex(Array.Empty<EncyclopediaWindowRowRenderData>()),
                CreateDetail()
            );

            Assert.Throws<MissingReferenceException>(() => _view.Render(data));
        }

        [Test]
        public void Render_TooManyTabs_ThrowsMissingReferenceException()
        {
            EncyclopediaTabRenderData[] tabs = Enumerable
                .Range(0, EncyclopediaWindowTabCatalog.Count + 1)
                .Select(index => new EncyclopediaTabRenderData(
                    EncyclopediaWindowTabCatalog.GetTab(index % EncyclopediaWindowTabCatalog.Count),
                    _texture
                ))
                .ToArray();
            EncyclopediaWindowIndexRenderData indexData = new EncyclopediaWindowIndexRenderData(
                EncyclopediaWindowTab.Systems,
                -1,
                string.Empty,
                "System Database",
                tabs,
                Array.Empty<EncyclopediaWindowRowRenderData>()
            );
            EncyclopediaWindowRenderData data = CreateRenderData(
                false,
                CreateFrame(false),
                indexData,
                CreateDetail()
            );

            Assert.Throws<MissingReferenceException>(() => _view.Render(data));
        }

        [Test]
        public void Render_ShorterIndexCollection_HidesUnusedCachedRows()
        {
            _view.Render(
                CreateRenderData(
                    false,
                    CreateFrame(false),
                    CreateIndex(
                        new[]
                        {
                            CreateRow("100", "Corellia", true),
                            CreateRow("101", "Coruscant", false),
                        }
                    ),
                    CreateDetail()
                )
            );
            EncyclopediaWindowRowView secondRow = FindRows()[1];

            _view.Render(
                CreateRenderData(
                    false,
                    CreateFrame(false),
                    CreateIndex(new[] { CreateRow("102", "Kessel", true) }),
                    CreateDetail()
                )
            );

            Assert.IsFalse(secondRow.gameObject.activeSelf);
            Assert.AreEqual("102", FindRows()[0].EntryTypeId);
            Assert.AreEqual("Kessel", FindRowText(FindRows()[0]).text);
        }

        [Test]
        public void Render_DetailPanel_AppliesCardNavigationTitleAndStructuredText()
        {
            EncyclopediaWindowDetailRenderData detail = new EncyclopediaWindowDetailRenderData(
                "Corellian Corvette",
                " Header\0\n\tIndented\nColumn One\t\tColumn Two\n\nAnother\tValue ",
                _texture,
                true,
                false
            );
            EncyclopediaWindowRenderData data = CreateRenderData(
                true,
                CreateFrame(false),
                CreateIndex(new[] { CreateRow("200", "Corellian Corvette", true) }),
                detail
            );

            _view.Render(data);

            Assert.IsFalse(FindObject("IndexPanel").activeSelf);
            Assert.IsTrue(FindObject("DetailPanel").activeSelf);
            Assert.AreSame(_texture, FindComponent<RawImage>("DetailCardImage").texture);
            Assert.AreEqual("Corellian Corvette", FindText("DetailTitleTextField").text);
            Assert.IsFalse(FindComponent<Button>("DetailPreviousButtonImage").interactable);
            Assert.IsFalse(FindComponent<RawImage>("DetailPreviousButtonImage").raycastTarget);
            Assert.IsTrue(FindComponent<Button>("DetailNextButtonImage").interactable);
            Assert.IsTrue(FindComponent<RawImage>("DetailNextButtonImage").raycastTarget);
            TextMeshProUGUI[] lines = FindDetailLines();
            CollectionAssert.AreEquivalent(
                new[] { "Header", "Indented", "Column One", "Column Two", "Another", "Value" },
                lines.Select(line => line.text).ToArray()
            );
            TextMeshProUGUI header = lines.Single(line => line.text == "Header");
            TextMeshProUGUI indented = lines.Single(line => line.text == "Indented");
            TextMeshProUGUI firstColumn = lines.Single(line => line.text == "Column One");
            TextMeshProUGUI secondColumn = lines.Single(line => line.text == "Column Two");
            Assert.Greater(
                UILayout.GetSourceRect(indented.rectTransform).x,
                UILayout.GetSourceRect(header.rectTransform).x
            );
            Assert.Greater(
                UILayout.GetSourceRect(secondColumn.rectTransform).x,
                UILayout.GetSourceRect(firstColumn.rectTransform).x
            );
        }

        [Test]
        public void Render_ShorterDetailText_HidesUnusedCachedTextFields()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    CreateFrame(false),
                    CreateIndex(new[] { CreateRow("200", "Ship", true) }),
                    new EncyclopediaWindowDetailRenderData(
                        "Ship",
                        "First\nSecond\nThird",
                        _texture,
                        false,
                        false
                    )
                )
            );
            TextMeshProUGUI thirdLine = FindDetailLines().Single(line => line.text == "Third");

            _view.Render(
                CreateRenderData(
                    true,
                    CreateFrame(false),
                    CreateIndex(new[] { CreateRow("201", "Facility", true) }),
                    new EncyclopediaWindowDetailRenderData(
                        "Facility",
                        "Replacement",
                        _texture,
                        false,
                        true
                    )
                )
            );

            Assert.IsFalse(thirdLine.gameObject.activeSelf);
            Assert.AreEqual("Replacement", FindDetailLines().Single().text);
        }

        [Test]
        public void Render_SwitchingToIndex_HidesCachedDetailText()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    CreateFrame(false),
                    CreateIndex(new[] { CreateRow("200", "Ship", true) }),
                    CreateDetail()
                )
            );
            TextMeshProUGUI detailLine = FindDetailLines().Single();

            _view.Render(
                CreateRenderData(
                    false,
                    CreateFrame(false),
                    CreateIndex(new[] { CreateRow("200", "Ship", true) }),
                    CreateDetail()
                )
            );

            Assert.IsFalse(detailLine.gameObject.activeSelf);
            Assert.IsFalse(FindObject("DetailPanel").activeSelf);
            Assert.IsTrue(FindObject("IndexPanel").activeSelf);
        }

        [Test]
        public void SearchInput_NullValue_RaisesNormalizedSearchRequest()
        {
            string search = null;
            _view.SearchTextChanged += (_, value) => search = value;

            FindComponent<TMP_InputField>("EntryNameInputField").onValueChanged.Invoke(null);

            Assert.AreEqual(string.Empty, search);
        }

        [Test]
        public void TabButton_Clicked_RaisesFocusAndSemanticTabRequests()
        {
            int focusCount = 0;
            EncyclopediaWindowTab? selectedTab = null;
            _view.FocusRequested += _ => focusCount++;
            _view.TabSelected += (_, tab) => selectedTab = tab;

            FindComponent<Button>("MissionsTabButtonImage").onClick.Invoke();

            Assert.AreEqual(1, focusCount);
            Assert.AreEqual(EncyclopediaWindowTab.Missions, selectedTab);
        }

        [Test]
        public void IndexRowGestures_RenderedRow_RaiseFocusSelectionActivationAndContext()
        {
            int focusCount = 0;
            string selected = null;
            string activated = null;
            string contextId = null;
            PointerEventData contextEvent = null;
            _view.FocusRequested += _ => focusCount++;
            _view.RowSelected += (_, id) => selected = id;
            _view.RowActivated += (_, id) => activated = id;
            _view.ContextRequested += (_, id, eventData) =>
            {
                contextId = id;
                contextEvent = eventData;
            };
            _view.Render(
                CreateRenderData(
                    false,
                    CreateFrame(false),
                    CreateIndex(new[] { CreateRow("100", "Corellia", true) }),
                    CreateDetail()
                )
            );
            EncyclopediaWindowRowView row = FindRows().Single();
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

            Assert.AreEqual(2, focusCount);
            Assert.AreEqual("100", selected);
            Assert.AreEqual("100", activated);
            Assert.AreEqual("100", contextId);
            Assert.AreSame(secondary, contextEvent);
        }

        [Test]
        public void DetailNavigationButtons_Click_RaiseFocusAndPreviousNextRequests()
        {
            int focusCount = 0;
            int previousCount = 0;
            int nextCount = 0;
            _view.FocusRequested += _ => focusCount++;
            _view.PreviousRequested += _ => previousCount++;
            _view.NextRequested += _ => nextCount++;

            FindComponent<Button>("DetailPreviousButtonImage").onClick.Invoke();
            FindComponent<Button>("DetailNextButtonImage").onClick.Invoke();

            Assert.AreEqual(2, focusCount);
            Assert.AreEqual(1, previousCount);
            Assert.AreEqual(1, nextCount);
        }

        [Test]
        public void DialogButton_Click_RaisesFocusAndRenderedSemanticCommand()
        {
            int focusCount = 0;
            EncyclopediaWindowCommand command = EncyclopediaWindowCommand.None;
            _view.FocusRequested += _ => focusCount++;
            _view.CommandRequested += (_, requested) => command = requested;
            _view.Render(
                CreateRenderData(
                    false,
                    CreateFrame(false),
                    CreateIndex(Array.Empty<EncyclopediaWindowRowRenderData>()),
                    CreateDetail()
                )
            );

            FindComponent<Button>("LowerLayoutTopicButtonImage").onClick.Invoke();

            Assert.AreEqual(1, focusCount);
            Assert.AreEqual(EncyclopediaWindowCommand.ShowTopic, command);
        }

        [Test]
        public void ChildPanels_NullData_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _indexPanel.Render(null, true));
            Assert.Throws<ArgumentNullException>(() => _detailPanel.Render(null, 0));
        }

        [Test]
        public void ChildPanelOnDestroy_InitializedPanels_UnbindTheirOwnControls()
        {
            int searchCount = 0;
            int tabCount = 0;
            int previousCount = 0;
            int nextCount = 0;
            _indexPanel.SearchTextChanged += _ => searchCount++;
            _indexPanel.TabSelected += _ => tabCount++;
            _detailPanel.PreviousRequested += () => previousCount++;
            _detailPanel.NextRequested += () => nextCount++;

            UIComponentTestHelper.InvokeLifecycle(_indexPanel, "OnDestroy");
            UIComponentTestHelper.InvokeLifecycle(_detailPanel, "OnDestroy");
            FindComponent<TMP_InputField>("EntryNameInputField").onValueChanged.Invoke("ignored");
            FindComponent<Button>("SystemsTabButtonImage").onClick.Invoke();
            FindComponent<Button>("DetailPreviousButtonImage").onClick.Invoke();
            FindComponent<Button>("DetailNextButtonImage").onClick.Invoke();

            Assert.AreEqual(0, searchCount);
            Assert.AreEqual(0, tabCount);
            Assert.AreEqual(0, previousCount);
            Assert.AreEqual(0, nextCount);
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsParentControlsAndRaisesDestroyedEvent()
        {
            EncyclopediaWindowView destroyed = null;
            int commandCount = 0;
            int rowCount = 0;
            int nextCount = 0;
            _view.Destroyed += view => destroyed = view;
            _view.CommandRequested += (_, _) => commandCount++;
            _view.RowSelected += (_, _) => rowCount++;
            _view.NextRequested += _ => nextCount++;
            _view.Render(
                CreateRenderData(
                    false,
                    CreateFrame(false),
                    CreateIndex(new[] { CreateRow("100", "Corellia", true) }),
                    CreateDetail()
                )
            );
            EncyclopediaWindowRowView row = FindRows().Single();

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            FindComponent<Button>("LowerLayoutCloseButtonImage").onClick.Invoke();
            row.OnPointerDown(
                new PointerEventData(null) { button = PointerEventData.InputButton.Left }
            );
            FindComponent<Button>("DetailNextButtonImage").onClick.Invoke();

            Assert.AreSame(_view, destroyed);
            Assert.AreEqual(0, commandCount);
            Assert.AreEqual(0, rowCount);
            Assert.AreEqual(0, nextCount);
        }

        private EncyclopediaWindowRenderData CreateRenderData(
            bool panel,
            EncyclopediaWindowFrameRenderData frame,
            EncyclopediaWindowIndexRenderData index,
            EncyclopediaWindowDetailRenderData detail
        )
        {
            return new EncyclopediaWindowRenderData(panel, frame, index, detail);
        }

        private EncyclopediaWindowFrameRenderData CreateFrame(
            bool useUpperLayout,
            EncyclopediaDialogButtonRenderData[] buttons = null
        )
        {
            return new EncyclopediaWindowFrameRenderData(
                16,
                24,
                470,
                331,
                true,
                useUpperLayout,
                _texture,
                useUpperLayout ? null : _texture,
                buttons ?? CreateDialogButtons()
            );
        }

        private EncyclopediaDialogButtonRenderData[] CreateDialogButtons()
        {
            return new[]
            {
                CreateDialogButton(EncyclopediaWindowCommand.Close),
                CreateDialogButton(EncyclopediaWindowCommand.ShowTopic),
                CreateDialogButton(EncyclopediaWindowCommand.ShowIndex),
            };
        }

        private EncyclopediaDialogButtonRenderData CreateDialogButton(
            EncyclopediaWindowCommand command
        )
        {
            return new EncyclopediaDialogButtonRenderData(command, _texture, _texture, null);
        }

        private EncyclopediaWindowIndexRenderData CreateIndex(
            EncyclopediaWindowRowRenderData[] rows
        )
        {
            EncyclopediaTabRenderData[] tabs = Enumerable
                .Range(0, EncyclopediaWindowTabCatalog.Count)
                .Select(index => new EncyclopediaTabRenderData(
                    EncyclopediaWindowTabCatalog.GetTab(index),
                    _texture
                ))
                .ToArray();
            return new EncyclopediaWindowIndexRenderData(
                EncyclopediaWindowTab.Systems,
                rows.Length == 0 ? -1 : 0,
                "cor",
                "System Database",
                tabs,
                rows
            );
        }

        private EncyclopediaWindowDetailRenderData CreateDetail()
        {
            return new EncyclopediaWindowDetailRenderData(
                "Corellia",
                "A core world.",
                _texture,
                false,
                false
            );
        }

        private static EncyclopediaWindowRowRenderData CreateRow(
            string entryTypeId,
            string name,
            bool selected
        )
        {
            return new EncyclopediaWindowRowRenderData(entryTypeId, name, selected);
        }

        private EncyclopediaWindowRowView[] FindRows()
        {
            return _viewObject
                .GetComponentsInChildren<EncyclopediaWindowRowView>(true)
                .Where(row => row.name.StartsWith("EncyclopediaRow", StringComparison.Ordinal))
                .OrderBy(row => row.Index)
                .ToArray();
        }

        private TextMeshProUGUI[] FindDetailLines()
        {
            return _viewObject
                .GetComponentsInChildren<TextMeshProUGUI>(true)
                .Where(text =>
                    text.name.StartsWith("DetailLineTextField", StringComparison.Ordinal)
                    && text.name != "DetailLineTextTemplate"
                    && text.gameObject.activeSelf
                )
                .OrderBy(text => UILayout.GetSourceRect(text.rectTransform).y)
                .ThenBy(text => UILayout.GetSourceRect(text.rectTransform).x)
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

        private TextMeshProUGUI FindText(string objectName)
        {
            return FindComponent<TextMeshProUGUI>(objectName);
        }

        private static TextMeshProUGUI FindRowText(EncyclopediaWindowRowView row)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "NameTextField");
        }
    }
}
