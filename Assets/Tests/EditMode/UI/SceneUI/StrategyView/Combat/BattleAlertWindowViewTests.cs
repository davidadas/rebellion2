using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Combat
{
    [TestFixture]
    public class BattleAlertWindowViewTests
    {
        private const string _prefabPath =
            "Assets/Prefabs/UI/StrategyView/BattleAlertWindow.prefab";

        private Texture2D _texture;
        private BattleAlertWindowView _view;
        private GameObject _viewObject;

        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<BattleAlertWindowView>();
            _texture = new Texture2D(96, 48);
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
        public void Render_PendingSummary_AppliesWindowSummaryAndButtons()
        {
            BattleAlertPendingRenderData pending = CreatePending(BattleAlertPanel.Summary);
            BattleAlertWindowRenderData data = CreateWindowData(
                BattleAlertWindowMode.Pending,
                pending,
                null
            );

            _view.Render(data);

            RectInt rect = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(21, rect.x);
            Assert.AreEqual(34, rect.y);
            Assert.AreSame(_texture, FindComponent<RawImage>("PanelBackgroundImage").texture);
            Assert.AreSame(_texture, FindComponent<RawImage>("FrameImage").texture);
            Assert.AreEqual("Battle at Corellia", FindText("TitleTextField").text);
            Assert.AreEqual("The battle is pending.", FindText("SummaryTextField").text);
            Assert.IsTrue(FindObject("SummaryTextField").activeSelf);
            Assert.IsFalse(FindObject("HeaderTextField").activeSelf);
            Assert.IsFalse(FindObject("RowsScrollArea").activeSelf);
            Assert.IsTrue(FindObject("RetreatButtonImage").activeSelf);
            Assert.IsTrue(FindComponent<Button>("RetreatButtonImage").interactable);
            Assert.IsFalse(FindComponent<Button>("AutoResolveButtonImage").interactable);
        }

        [Test]
        public void Render_PendingRows_AppliesHeaderIconsLabelsAndCachedVisibility()
        {
            BattleAlertPendingRenderData twoRows = CreatePending(
                BattleAlertPanel.FirstForces,
                new[]
                {
                    new BattleAlertRowRenderData("First Fleet", _texture),
                    new BattleAlertRowRenderData("Second Fleet", null),
                }
            );
            BattleAlertPendingRenderData oneRow = CreatePending(
                BattleAlertPanel.FirstForces,
                new[] { new BattleAlertRowRenderData("Replacement Fleet", _texture) }
            );

            _view.Render(CreateWindowData(BattleAlertWindowMode.Pending, twoRows, null));
            BattleAlertRowView[] rows = FindRows();
            BattleAlertRowView secondRow = rows[1];
            _view.Render(CreateWindowData(BattleAlertWindowMode.Pending, oneRow, null));

            Assert.AreEqual("Friendly Forces", FindText("HeaderTextField").text);
            Assert.IsTrue(FindObject("HeaderTextField").activeSelf);
            Assert.IsFalse(FindObject("SummaryTextField").activeSelf);
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("Replacement Fleet", FindRowText(rows[0], "TextField").text);
            Assert.AreSame(_texture, FindRowImage(rows[0], "IconImage").texture);
            Assert.IsTrue(FindRowObject(rows[0], "IconImage").activeSelf);
            Assert.IsFalse(secondRow.gameObject.activeSelf);
        }

        [Test]
        public void Render_HiddenAfterPending_ClearsAndDeactivatesWindow()
        {
            _view.Render(
                CreateWindowData(
                    BattleAlertWindowMode.Pending,
                    CreatePending(BattleAlertPanel.Summary),
                    null
                )
            );

            _view.Render(CreateWindowData(BattleAlertWindowMode.Hidden, null, null));

            Assert.IsFalse(_viewObject.activeSelf);
            Assert.IsNull(FindComponent<RawImage>("PanelBackgroundImage").texture);
            Assert.IsNull(FindComponent<RawImage>("FrameImage").texture);
            Assert.IsFalse(FindObject("SummaryButtonImage").activeSelf);
            Assert.IsFalse(FindObject("RetreatButtonImage").activeSelf);
        }

        [Test]
        public void Render_ResultSummary_AppliesSummaryAndCloseControl()
        {
            BattleAlertResultRenderData result = CreateResult(
                BattleResultPanel.Summary,
                planetary: true
            );

            _view.Render(CreateWindowData(BattleAlertWindowMode.Result, null, result));

            TextMeshProUGUI title = FindText("ResultPlanetaryTitleTextField");
            TextMeshProUGUI summary = FindText("ResultSummaryTextField");
            Assert.AreEqual("Battle at Corellia", title.text);
            Assert.AreEqual(
                new RectInt(12, 13, 400, 24),
                UILayout.GetSourceRect(title.rectTransform)
            );
            Assert.AreEqual(24f, title.fontSize);
            Assert.AreEqual(FontStyles.Normal, title.fontStyle);
            Assert.AreEqual(TextAlignmentOptions.Top, title.alignment);
            Assert.IsFalse(title.GetComponent<Shadow>().enabled);
            Assert.AreEqual("Victory", summary.text);
            Assert.AreEqual(
                new RectInt(25, 225, 350, 70),
                UILayout.GetSourceRect(summary.rectTransform)
            );
            Assert.AreEqual(18f, summary.fontSize);
            Assert.AreEqual(FontStyles.Normal, summary.fontStyle);
            Assert.AreEqual(TextAlignmentOptions.TopLeft, summary.alignment);
            Assert.AreEqual(TextWrappingModes.Normal, summary.textWrappingMode);
            Assert.IsTrue(summary.GetComponent<Shadow>().enabled);
            Assert.IsTrue(FindObject("ResultSummaryTextField").activeSelf);
            Assert.IsTrue(FindObject("ResultCloseButtonImage").activeSelf);
            Assert.AreEqual(
                new RectInt(418, 88, 41, 41),
                UILayout.GetSourceRect(FindComponent<RawImage>("SummaryButtonImage").rectTransform)
            );
            Assert.IsFalse(FindObject("ResultRowsScrollArea").activeSelf);
            Assert.IsFalse(FindObject("ResultCapitalShipsButtonImage").activeSelf);
            Assert.IsFalse(FindObject("ResultDirectSystemButtonImage").activeSelf);
        }

        [Test]
        public void Render_StandardResultDetail_AppliesHeadersCategoriesAndBothColumns()
        {
            BattleResultTableRenderData table = new BattleResultTableRenderData(
                new[]
                {
                    new BattleResultItemRenderData(
                        "Operational Ship",
                        _texture,
                        _texture,
                        _texture,
                        _texture
                    ),
                },
                new[] { new BattleResultItemRenderData("Destroyed Ship", _texture) }
            );
            BattleAlertResultRenderData result = CreateResult(
                BattleResultPanel.FirstForces,
                BattleResultCategory.CapitalShips,
                table
            );

            _view.Render(CreateWindowData(BattleAlertWindowMode.Result, null, result));

            TextMeshProUGUI title = FindText("ResultFleetTitleTextField");
            TextMeshProUGUI forceHeader = FindText("ResultFleetForceHeaderTextField");
            TextMeshProUGUI filters = FindText("ResultFleetFiltersTextField");
            TextMeshProUGUI tableTitle = FindText("ResultFleetTableTitleTextField");
            TextMeshProUGUI operationalHeader = FindText("ResultFleetOperationalHeaderTextField");
            TextMeshProUGUI destroyedHeader = FindText("ResultFleetDestroyedHeaderTextField");
            Assert.AreEqual("Battle at Corellia", title.text);
            Assert.AreEqual(
                new RectInt(12, 13, 400, 24),
                UILayout.GetSourceRect(title.rectTransform)
            );
            Assert.AreEqual("Alliance Forces", forceHeader.text);
            Assert.AreEqual(
                new RectInt(12, 36, 347, 20),
                UILayout.GetSourceRect(forceHeader.rectTransform)
            );
            Assert.AreEqual(18f, forceHeader.fontSize);
            Assert.AreEqual(FontStyles.Normal, forceHeader.fontStyle);
            Assert.AreEqual(TextAlignmentOptions.Top, forceHeader.alignment);
            Assert.IsFalse(forceHeader.GetComponent<Shadow>().enabled);
            Assert.AreEqual(
                new RectInt(86, 70, 165, 18),
                UILayout.GetSourceRect(filters.rectTransform)
            );
            Assert.AreEqual(16f, filters.fontSize);
            Assert.AreEqual("Capital Ships", tableTitle.text);
            Assert.AreEqual(
                new RectInt(38, 100, 400, 18),
                UILayout.GetSourceRect(tableTitle.rectTransform)
            );
            Assert.AreEqual(16f, tableTitle.fontSize);
            Assert.AreEqual("Operational", operationalHeader.text);
            Assert.AreEqual(
                new RectInt(38, 117, 165, 18),
                UILayout.GetSourceRect(operationalHeader.rectTransform)
            );
            Assert.AreEqual("Destroyed", destroyedHeader.text);
            Assert.AreEqual(
                new RectInt(204, 117, 165, 18),
                UILayout.GetSourceRect(destroyedHeader.rectTransform)
            );
            Assert.IsTrue(FindObject("ResultStandardOperationalColumn").activeSelf);
            Assert.IsTrue(FindObject("ResultStandardDestroyedColumn").activeSelf);
            Assert.IsFalse(FindObject("ResultPersonnelOperationalColumn").activeSelf);
            Assert.IsTrue(FindObject("ResultCapitalShipsButtonImage").activeSelf);
            Assert.IsFalse(FindComponent<Button>("ResultPersonnelButtonImage").interactable);
            Assert.IsTrue(FindObject("ResultFleetFiltersTextField").activeSelf);
            Assert.IsFalse(FindObject("ResultPlanetaryForceHeaderTextField").activeSelf);
            BattleResultItemView operational = FindResultItems("ResultStandardOperationalColumn")
                .Single();
            BattleResultItemView destroyed = FindResultItems("ResultStandardDestroyedColumn")
                .Single();
            Assert.AreEqual("Operational Ship", FindItemText(operational, "NameTextField").text);
            Assert.AreSame(_texture, FindItemImage(operational, "BaseImage").texture);
            Assert.AreSame(_texture, FindItemImage(operational, "WithdrawingOverlayImage").texture);
            Assert.AreSame(_texture, FindItemImage(operational, "DamagedOverlayImage").texture);
            Assert.AreSame(_texture, FindItemImage(operational, "CapturedOverlayImage").texture);
            TextMeshProUGUI operationalName = FindItemText(operational, "NameTextField");
            Assert.AreEqual(14f, operationalName.fontSize);
            Assert.AreEqual(FontStyles.Normal, operationalName.fontStyle);
            Assert.IsFalse(operationalName.GetComponent<Shadow>().enabled);
            Assert.AreEqual("Destroyed Ship", FindItemText(destroyed, "NameTextField").text);
        }

        [Test]
        public void Render_PlanetaryResultDetail_UsesSourceLabelsWithoutFilters()
        {
            BattleAlertResultRenderData result = CreateResult(
                BattleResultPanel.FirstForces,
                BattleResultCategory.Manufacturing,
                planetary: true
            );

            _view.Render(CreateWindowData(BattleAlertWindowMode.Result, null, result));

            TextMeshProUGUI forceHeader = FindText("ResultPlanetaryForceHeaderTextField");
            TextMeshProUGUI tableTitle = FindText("ResultPlanetaryTableTitleTextField");
            TextMeshProUGUI operationalHeader = FindText(
                "ResultPlanetaryOperationalHeaderTextField"
            );
            TextMeshProUGUI destroyedHeader = FindText("ResultPlanetaryDestroyedHeaderTextField");
            Assert.IsFalse(FindObject("ResultFleetFiltersTextField").activeSelf);
            Assert.IsFalse(FindObject("ResultFleetForceHeaderTextField").activeSelf);
            Assert.AreEqual("Alliance Forces", forceHeader.text);
            Assert.AreEqual(
                new RectInt(12, 36, 347, 20),
                UILayout.GetSourceRect(forceHeader.rectTransform)
            );
            Assert.AreEqual(18f, forceHeader.fontSize);
            Assert.AreEqual(FontStyles.Normal, forceHeader.fontStyle);
            Assert.AreEqual("Capital Ships", tableTitle.text);
            Assert.AreEqual(
                new RectInt(38, 100, 400, 18),
                UILayout.GetSourceRect(tableTitle.rectTransform)
            );
            Assert.AreEqual(16f, tableTitle.fontSize);
            Assert.AreEqual(
                new RectInt(38, 117, 165, 18),
                UILayout.GetSourceRect(operationalHeader.rectTransform)
            );
            Assert.AreEqual(
                new RectInt(204, 117, 165, 18),
                UILayout.GetSourceRect(destroyedHeader.rectTransform)
            );
            Assert.AreEqual(
                new RectInt(36, 59, 49, 41),
                UILayout.GetSourceRect(
                    FindComponent<RawImage>("ResultCapitalShipsButtonImage").rectTransform
                )
            );
            Assert.AreEqual(
                new RectInt(348, 59, 49, 41),
                UILayout.GetSourceRect(
                    FindComponent<RawImage>("ResultPersonnelButtonImage").rectTransform
                )
            );
        }

        [Test]
        public void Render_PersonnelResultDetail_UsesPersonnelHeadersAndColumns()
        {
            BattleResultTableRenderData table = new BattleResultTableRenderData(
                new[] { new BattleResultItemRenderData("Luke", _texture) },
                new[] { new BattleResultItemRenderData("No Casualties", null) }
            );
            BattleAlertResultRenderData result = CreateResult(
                BattleResultPanel.SecondForces,
                BattleResultCategory.Personnel,
                table
            );

            _view.Render(CreateWindowData(BattleAlertWindowMode.Result, null, result));

            Assert.IsFalse(FindObject("ResultStandardOperationalColumn").activeSelf);
            Assert.IsFalse(FindObject("ResultStandardDestroyedColumn").activeSelf);
            Assert.IsTrue(FindObject("ResultPersonnelOperationalColumn").activeSelf);
            Assert.IsTrue(FindObject("ResultPersonnelDestroyedColumn").activeSelf);
            Assert.AreEqual("Survivors", FindText("ResultFleetSurvivorsHeaderTextField").text);
            Assert.AreEqual("Captured", FindText("ResultFleetCapturedHeaderTextField").text);
            Assert.AreEqual("Killed", FindText("ResultFleetKilledHeaderTextField").text);
            BattleResultItemView emptyItem = FindResultItems("ResultPersonnelDestroyedColumn")
                .Single();
            Assert.IsFalse(FindItemObject(emptyItem, "NameTextField").activeSelf);
            Assert.IsTrue(FindItemObject(emptyItem, "EmptyTextField").activeSelf);
            TextMeshProUGUI emptyText = FindItemText(emptyItem, "EmptyTextField");
            Assert.AreEqual("No Casualties", emptyText.text);
            Assert.AreEqual(18f, emptyText.fontSize);
            Assert.AreEqual(FontStyles.Normal, emptyText.fontStyle);
            Assert.AreEqual(TextAlignmentOptions.Center, emptyText.alignment);
        }

        [Test]
        public void Render_ResultDetailWithoutTable_HidesResultColumns()
        {
            BattleAlertResultRenderData result = CreateResult(
                BattleResultPanel.FirstForces,
                BattleResultCategory.CapitalShips,
                null
            );

            _view.Render(CreateWindowData(BattleAlertWindowMode.Result, null, result));

            Assert.IsFalse(FindObject("ResultRowsScrollArea").activeSelf);
            Assert.IsFalse(FindObject("ResultStandardOperationalColumn").activeSelf);
            Assert.IsFalse(FindObject("ResultStandardDestroyedColumn").activeSelf);
            Assert.IsFalse(FindObject("ResultPersonnelOperationalColumn").activeSelf);
            Assert.IsFalse(FindObject("ResultPersonnelDestroyedColumn").activeSelf);
        }

        [Test]
        public void Render_SwitchingResultLayouts_HidesCachedItemsFromInactiveLayout()
        {
            BattleResultTableRenderData standardTable = new BattleResultTableRenderData(
                new[] { new BattleResultItemRenderData("Ship", _texture) },
                Array.Empty<BattleResultItemRenderData>()
            );
            BattleResultTableRenderData personnelTable = new BattleResultTableRenderData(
                new[] { new BattleResultItemRenderData("Officer", _texture) },
                Array.Empty<BattleResultItemRenderData>()
            );
            _view.Render(
                CreateWindowData(
                    BattleAlertWindowMode.Result,
                    null,
                    CreateResult(
                        BattleResultPanel.FirstForces,
                        BattleResultCategory.CapitalShips,
                        standardTable
                    )
                )
            );
            BattleResultItemView standardItem = FindResultItems("ResultStandardOperationalColumn")
                .Single();

            _view.Render(
                CreateWindowData(
                    BattleAlertWindowMode.Result,
                    null,
                    CreateResult(
                        BattleResultPanel.FirstForces,
                        BattleResultCategory.Personnel,
                        personnelTable
                    )
                )
            );

            Assert.IsFalse(standardItem.gameObject.activeSelf);
            Assert.AreEqual(
                "Officer",
                FindItemText(
                    FindResultItems("ResultPersonnelOperationalColumn").Single(),
                    "NameTextField"
                ).text
            );
        }

        [Test]
        public void Render_ShorterResultColumns_HidesUnusedCachedItems()
        {
            BattleResultTableRenderData twoItems = new BattleResultTableRenderData(
                new[]
                {
                    new BattleResultItemRenderData("First", _texture),
                    new BattleResultItemRenderData("Second", _texture),
                },
                Array.Empty<BattleResultItemRenderData>()
            );
            BattleResultTableRenderData oneItem = new BattleResultTableRenderData(
                new[] { new BattleResultItemRenderData("Replacement", _texture) },
                Array.Empty<BattleResultItemRenderData>()
            );
            _view.Render(
                CreateWindowData(
                    BattleAlertWindowMode.Result,
                    null,
                    CreateResult(
                        BattleResultPanel.FirstForces,
                        BattleResultCategory.CapitalShips,
                        twoItems
                    )
                )
            );
            BattleResultItemView secondItem = FindResultItems("ResultStandardOperationalColumn")[1];

            _view.Render(
                CreateWindowData(
                    BattleAlertWindowMode.Result,
                    null,
                    CreateResult(
                        BattleResultPanel.FirstForces,
                        BattleResultCategory.CapitalShips,
                        oneItem
                    )
                )
            );

            Assert.IsFalse(secondItem.gameObject.activeSelf);
            Assert.AreEqual(
                "Replacement",
                FindItemText(
                    FindResultItems("ResultStandardOperationalColumn")[0],
                    "NameTextField"
                ).text
            );
        }

        [Test]
        public void Render_DirectResult_AppliesSummaryAndNavigationControls()
        {
            BattleAlertResultRenderData result = CreateResult(BattleResultPanel.Direct);

            _view.Render(CreateWindowData(BattleAlertWindowMode.Result, null, result));

            Assert.AreEqual("Victory", FindText("ResultSummaryTextField").text);
            Assert.IsTrue(FindObject("ResultDirectSystemButtonImage").activeSelf);
            Assert.IsTrue(FindObject("ResultDirectFleetButtonImage").activeSelf);
            Assert.IsFalse(FindObject("ResultCapitalShipsButtonImage").activeSelf);
            Assert.IsFalse(FindObject("ResultRowsScrollArea").activeSelf);
        }

        [Test]
        public void PrimaryPanelButton_Click_RaisesControlPressAndOrderedPanelRequest()
        {
            int pressedCount = 0;
            BattleAlertPanel? requested = null;
            _view.ControlPressed += () => pressedCount++;
            _view.PrimaryPanelRequested += (_, panel) => requested = panel;
            Button button = FindComponent<Button>("SecondForcesButtonImage");

            button.onClick.Invoke();

            Assert.AreEqual(1, pressedCount);
            Assert.AreEqual(BattleAlertPanel.SecondForces, requested);
        }

        [Test]
        public void CommandButton_Click_RaisesOrderedChoiceRequest()
        {
            BattleAlertChoice? requested = null;
            _view.ChoiceRequested += (_, choice) => requested = choice;
            Button button = FindComponent<Button>("AutoResolveButtonImage");

            button.onClick.Invoke();

            Assert.AreEqual(BattleAlertChoice.AutoResolve, requested);
        }

        [Test]
        public void ResultCategoryButton_Click_RaisesOrderedCategoryRequest()
        {
            BattleResultCategory? requested = null;
            _view.ResultCategoryRequested += (_, category) => requested = category;
            Button button = FindComponent<Button>("ResultTroopsButtonImage");

            button.onClick.Invoke();

            Assert.AreEqual(BattleResultCategory.Troops, requested);
        }

        [Test]
        public void ResultControls_Click_RaiseCloseAndNavigationRequestsWithControlPresses()
        {
            int pressedCount = 0;
            int closeCount = 0;
            int systemCount = 0;
            int fleetCount = 0;
            _view.ControlPressed += () => pressedCount++;
            _view.CloseRequested += _ => closeCount++;
            _view.OpenSystemRequested += _ => systemCount++;
            _view.OpenFleetRequested += _ => fleetCount++;

            FindComponent<Button>("ResultCloseButtonImage").onClick.Invoke();
            FindComponent<Button>("ResultDirectSystemButtonImage").onClick.Invoke();
            FindComponent<Button>("ResultDirectFleetButtonImage").onClick.Invoke();

            Assert.AreEqual(3, pressedCount);
            Assert.AreEqual(1, closeCount);
            Assert.AreEqual(1, systemCount);
            Assert.AreEqual(1, fleetCount);
        }

        [Test]
        public void ChildViews_NullRenderData_ThrowArgumentNullException()
        {
            BattleAlertRowView rowTemplate = _viewObject
                .GetComponentsInChildren<BattleAlertRowView>(true)
                .Single(row => row.name == "RowTemplate");
            BattleResultItemView[] itemTemplates = _viewObject
                .GetComponentsInChildren<BattleResultItemView>(true)
                .Where(item => item.name.EndsWith("ItemTemplate", StringComparison.Ordinal))
                .ToArray();

            Assert.Throws<ArgumentNullException>(() => rowTemplate.Render(null));
            Assert.AreEqual(2, itemTemplates.Length);
            Assert.Throws<ArgumentNullException>(() => itemTemplates[0].Render(null));
            Assert.Throws<ArgumentNullException>(() => itemTemplates[1].Render(null));
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsControlsAndRaisesDestroyedEvent()
        {
            BattleAlertWindowView destroyed = null;
            int panelRequestCount = 0;
            _view.Destroyed += view => destroyed = view;
            _view.PrimaryPanelRequested += (_, _) => panelRequestCount++;
            Button button = FindComponent<Button>("SummaryButtonImage");

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            button.onClick.Invoke();

            Assert.AreSame(_view, destroyed);
            Assert.AreEqual(0, panelRequestCount);
        }

        private BattleAlertWindowRenderData CreateWindowData(
            BattleAlertWindowMode mode,
            BattleAlertPendingRenderData pending,
            BattleAlertResultRenderData result
        )
        {
            return new BattleAlertWindowRenderData(
                mode,
                21,
                34,
                _texture,
                _texture,
                Color.yellow,
                CreateViewButtons(mode),
                pending,
                result
            );
        }

        private BattleAlertPendingRenderData CreatePending(
            BattleAlertPanel panel,
            BattleAlertRowRenderData[] rows = null
        )
        {
            BattleAlertButtonRenderData[] buttons =
            {
                CreateButton(true),
                CreateButton(false),
                CreateButton(true),
            };
            return new BattleAlertPendingRenderData(
                panel,
                "Battle at Corellia",
                "Friendly Forces",
                "The battle is pending.",
                rows ?? Array.Empty<BattleAlertRowRenderData>(),
                buttons
            );
        }

        private BattleAlertResultRenderData CreateResult(
            BattleResultPanel panel,
            BattleResultCategory category = BattleResultCategory.CapitalShips,
            BattleResultTableRenderData table = null,
            bool planetary = false
        )
        {
            string[] headers =
                category == BattleResultCategory.Personnel
                    ? new[] { "Survivors", "Captured", "Killed" }
                    : new[] { "Operational", "Destroyed" };
            BattleResultCategory[] categoryOrder = planetary
                ? BattleResultCategoryCatalog.Ordered.ToArray()
                : new[]
                {
                    BattleResultCategory.CapitalShips,
                    BattleResultCategory.Starfighters,
                    BattleResultCategory.Troops,
                    BattleResultCategory.Personnel,
                };
            int[] categoryXs = planetary
                ? new[] { 36, 98, 160, 222, 284, 348 }
                : new[] { 130, 179, 228, 277 };
            BattleResultCategoryRenderData[] categories = categoryOrder
                .Select(
                    (value, index) =>
                        new BattleResultCategoryRenderData(
                            value,
                            CreateButton(index != 3, new RectInt(categoryXs[index], 59, 49, 41))
                        )
                )
                .ToArray();
            return new BattleAlertResultRenderData(
                panel,
                category,
                "Battle at Corellia",
                "Victory",
                CreateButton(true),
                "Alliance Forces",
                Color.green,
                category == BattleResultCategory.Personnel ? "Personnel" : "Capital Ships",
                headers,
                categories,
                planetary,
                CreateButtons(2),
                table
            );
        }

        private BattleAlertButtonRenderData[] CreateViewButtons(BattleAlertWindowMode mode)
        {
            int[] yPositions =
                mode == BattleAlertWindowMode.Result
                    ? new[] { 88, 141, 196, 250 }
                    : new[] { 21, 81, 141, 201 };
            return yPositions
                .Select(y => CreateButton(true, new RectInt(418, y, 41, 41)))
                .ToArray();
        }

        private BattleAlertButtonRenderData[] CreateButtons(int count)
        {
            return Enumerable.Range(0, count).Select(_ => CreateButton(true)).ToArray();
        }

        private BattleAlertButtonRenderData CreateButton(bool interactable, RectInt? bounds = null)
        {
            return new BattleAlertButtonRenderData(interactable, _texture, _texture, bounds);
        }

        private BattleAlertRowView[] FindRows()
        {
            return _viewObject
                .GetComponentsInChildren<BattleAlertRowView>(true)
                .Where(row => row.name.StartsWith("BattleAlertRow", StringComparison.Ordinal))
                .OrderBy(row => row.name)
                .ToArray();
        }

        private BattleResultItemView[] FindResultItems(string columnName)
        {
            Transform column = FindObject(columnName).transform;
            return column
                .GetComponentsInChildren<BattleResultItemView>(true)
                .Where(item =>
                    item.name.StartsWith("BattleResultTableItem", StringComparison.Ordinal)
                )
                .OrderBy(item => UILayout.GetSourceRect(item.transform as RectTransform).y)
                .ToArray();
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _viewObject
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        private TextMeshProUGUI FindText(string objectName)
        {
            return FindComponent<TextMeshProUGUI>(objectName);
        }

        private GameObject FindObject(string objectName)
        {
            return _viewObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        private static RawImage FindItemImage(BattleResultItemView item, string objectName)
        {
            return item.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == objectName);
        }

        private static GameObject FindItemObject(BattleResultItemView item, string objectName)
        {
            return item.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == objectName)
                .gameObject;
        }

        private static TextMeshProUGUI FindItemText(BattleResultItemView item, string objectName)
        {
            return item.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == objectName);
        }

        private static RawImage FindRowImage(BattleAlertRowView row, string objectName)
        {
            return row.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == objectName);
        }

        private static GameObject FindRowObject(BattleAlertRowView row, string objectName)
        {
            return row.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == objectName)
                .gameObject;
        }

        private static TextMeshProUGUI FindRowText(BattleAlertRowView row, string objectName)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == objectName);
        }
    }
}
