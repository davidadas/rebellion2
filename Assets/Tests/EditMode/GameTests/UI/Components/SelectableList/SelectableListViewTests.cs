using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rebellion.Tests.UI.Components.SelectableList
{
    [TestFixture]
    public class SelectableListViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/FinderWindow.prefab";

        private GameObject _rootObject;
        private ScrollAreaView _scrollArea;
        private FinderWindowRowView _rowTemplate;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _scrollArea = _rootObject.GetComponentsInChildren<ScrollAreaView>(true).Single();
            _rowTemplate = _rootObject
                .GetComponentsInChildren<FinderWindowRowView>(true)
                .Single(row => row.name == "RowTemplate");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullScrollArea_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SelectableListView<FinderWindowRowView, FinderWindowRowRenderData>(
                    null,
                    _rowTemplate,
                    "FinderRow",
                    null,
                    null
                )
            );
        }

        [Test]
        public void Constructor_NullRowTemplate_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SelectableListView<FinderWindowRowView, FinderWindowRowRenderData>(
                    _scrollArea,
                    null,
                    "FinderRow",
                    null,
                    null
                )
            );
        }

        [Test]
        public void Render_Rows_CreatesNamedViewsWithStableIndexesAndGeometry()
        {
            SelectableListView<FinderWindowRowView, FinderWindowRowRenderData> list = CreateList();
            FinderWindowRowRenderData[] rows =
            {
                new FinderWindowRowRenderData("first", "First", false, Array.Empty<string>()),
                new FinderWindowRowRenderData("second", "Second", true, Array.Empty<string>()),
            };

            list.Render(rows, 50, 25, true, 25, RenderRow);

            FinderWindowRowView[] renderedRows = FindRenderedRows();
            Assert.AreEqual(2, renderedRows.Length);
            Assert.AreEqual("SelectableRow0", renderedRows[0].name);
            Assert.AreEqual("SelectableRow1", renderedRows[1].name);
            Assert.AreEqual(0, renderedRows[0].Index);
            Assert.AreEqual(1, renderedRows[1].Index);
            Assert.AreEqual(
                new RectInt(0, 25, Mathf.RoundToInt(_scrollArea.ViewportWidth), 25),
                GetSourceRect(renderedRows[1])
            );
        }

        [Test]
        public void Render_ShorterCollection_ReusesFirstRowAndHidesRemainingRows()
        {
            SelectableListView<FinderWindowRowView, FinderWindowRowRenderData> list = CreateList();
            FinderWindowRowRenderData[] initialRows =
            {
                new FinderWindowRowRenderData("first", "First", false, Array.Empty<string>()),
                new FinderWindowRowRenderData("second", "Second", false, Array.Empty<string>()),
            };
            list.Render(initialRows, 50, 25, true, 25, RenderRow);
            FinderWindowRowView[] initialViews = FindRenderedRows();

            list.Render(
                new[]
                {
                    new FinderWindowRowRenderData(
                        "replacement",
                        "Replacement",
                        true,
                        Array.Empty<string>()
                    ),
                },
                25,
                25,
                false,
                25,
                RenderRow
            );

            Assert.AreSame(initialViews[0], FindRenderedRows()[0]);
            Assert.AreEqual("replacement", initialViews[0].RowId);
            Assert.IsFalse(initialViews[1].gameObject.activeSelf);
        }

        [Test]
        public void Render_NullCollection_HidesCachedRows()
        {
            SelectableListView<FinderWindowRowView, FinderWindowRowRenderData> list = CreateList();
            list.Render(
                new[]
                {
                    new FinderWindowRowRenderData("first", "First", false, Array.Empty<string>()),
                },
                25,
                25,
                true,
                25,
                RenderRow
            );
            FinderWindowRowView row = FindRenderedRows().Single();

            list.Render(null, 0, 25, false, 25, RenderRow);

            Assert.IsFalse(row.gameObject.activeSelf);
        }

        [Test]
        public void Hide_RenderedRows_HidesEveryCachedView()
        {
            SelectableListView<FinderWindowRowView, FinderWindowRowRenderData> list = CreateList();
            list.Render(
                new[]
                {
                    new FinderWindowRowRenderData("first", "First", false, Array.Empty<string>()),
                },
                25,
                25,
                true,
                25,
                RenderRow
            );
            FinderWindowRowView row = FindRenderedRows().Single();

            list.Hide();

            Assert.IsFalse(row.gameObject.activeSelf);
        }

        [Test]
        public void Clear_RenderedRow_DetachesSelectionCallback()
        {
            int selectionCount = 0;
            SelectableListView<FinderWindowRowView, FinderWindowRowRenderData> list = CreateList(
                (_, _) => selectionCount++
            );
            list.Render(
                new[]
                {
                    new FinderWindowRowRenderData("first", "First", false, Array.Empty<string>()),
                },
                25,
                25,
                true,
                25,
                RenderRow
            );
            FinderWindowRowView row = FindRenderedRows().Single();
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            list.Clear();
            row.OnPointerDown(eventData);

            Assert.AreEqual(0, selectionCount);
        }

        private SelectableListView<FinderWindowRowView, FinderWindowRowRenderData> CreateList(
            Action<FinderWindowRowView, PointerEventData> rowSelected = null
        )
        {
            return new SelectableListView<FinderWindowRowView, FinderWindowRowRenderData>(
                _scrollArea,
                _rowTemplate,
                "SelectableRow",
                rowSelected,
                null
            );
        }

        private FinderWindowRowView[] FindRenderedRows()
        {
            return _rootObject
                .GetComponentsInChildren<FinderWindowRowView>(true)
                .Where(row => row.name.StartsWith("SelectableRow", StringComparison.Ordinal))
                .OrderBy(row => row.Index)
                .ToArray();
        }

        private static RectInt GetSourceRect(FinderWindowRowView row)
        {
            return UILayout.GetSourceRect(row.transform as RectTransform);
        }

        private static void RenderRow(
            FinderWindowRowView row,
            FinderWindowRowRenderData data,
            int index
        )
        {
            row.Render(index, data, 25);
        }
    }
}
