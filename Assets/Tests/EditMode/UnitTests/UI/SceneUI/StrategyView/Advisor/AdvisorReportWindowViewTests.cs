using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Advisor
{
    [TestFixture]
    public class AdvisorReportWindowViewTests
    {
        private const string _prefabPath =
            "Assets/Prefabs/UI/StrategyView/AdvisorReportWindow.prefab";

        private Texture2D _backgroundTexture;
        private Texture2D _galaxyTexture;
        private Texture2D _rowTexture;
        private AdvisorReportWindowView _view;
        private GameObject _viewObject;

        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<AdvisorReportWindowView>();
            _backgroundTexture = new Texture2D(420, 300);
            _galaxyTexture = new Texture2D(160, 120);
            _rowTexture = new Texture2D(48, 48);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rowTexture);
            UnityEngine.Object.DestroyImmediate(_galaxyTexture);
            UnityEngine.Object.DestroyImmediate(_backgroundTexture);
            UnityEngine.Object.DestroyImmediate(_viewObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_GalaxyOverview_AppliesFrameAndOverviewRows()
        {
            AdvisorReportWindowRenderData data = CreateRenderData(
                AdvisorReportMode.GalaxyOverview,
                "Galaxy Overview",
                new[]
                {
                    new AdvisorReportRowRenderData(_rowTexture, "Planets", "12"),
                    new AdvisorReportRowRenderData(null, "Fleets", "4"),
                }
            );

            _view.Render(data);

            RectInt windowRect = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(22, windowRect.x);
            Assert.AreEqual(34, windowRect.y);
            Assert.AreSame(_backgroundTexture, FindComponent<RawImage>("BackgroundImage").texture);
            Assert.AreSame(_galaxyTexture, FindComponent<RawImage>("GalaxyImage").texture);
            Assert.AreEqual("Galaxy Overview", FindText("TitleTextField").text);
            AdvisorReportRowView[] rows = FindActiveRows();
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("Planets", FindRowText(rows[0], "PrimaryTextField").text);
            Assert.AreEqual("12", FindRowText(rows[0], "SecondaryTextField").text);
            Assert.AreSame(_rowTexture, FindRowImage(rows[0]).texture);
            Assert.AreEqual("Fleets", FindRowText(rows[1], "PrimaryTextField").text);
            Assert.IsFalse(FindRowImage(rows[1]).gameObject.activeSelf);
        }

        [Test]
        public void Render_Objectives_UsesObjectiveTemplateAndHidesOverviewRows()
        {
            _view.Render(
                CreateRenderData(
                    AdvisorReportMode.GalaxyOverview,
                    "Overview",
                    new[] { new AdvisorReportRowRenderData(_rowTexture, "Planets", "12") }
                )
            );
            AdvisorReportRowView overviewRow = FindActiveRows().Single();

            _view.Render(
                CreateRenderData(
                    AdvisorReportMode.Objectives,
                    "Objectives",
                    new[]
                    {
                        new AdvisorReportRowRenderData(_rowTexture, "Capture Coruscant", "Open"),
                    }
                )
            );

            AdvisorReportRowView objectiveRow = FindActiveRows().Single();
            Assert.AreNotSame(overviewRow, objectiveRow);
            Assert.IsFalse(overviewRow.gameObject.activeSelf);
            Assert.AreEqual(
                "Capture Coruscant",
                FindRowText(objectiveRow, "PrimaryTextField").text
            );
            Assert.IsFalse(
                objectiveRow
                    .GetComponentsInChildren<TextMeshProUGUI>(true)
                    .Any(text => text.name == "SecondaryTextField")
            );
        }

        [Test]
        public void Render_ShorterSameMode_ReusesRowsAndHidesUnusedRows()
        {
            _view.Render(
                CreateRenderData(
                    AdvisorReportMode.GalaxyOverview,
                    "Overview",
                    new[]
                    {
                        new AdvisorReportRowRenderData(_rowTexture, "First", "1"),
                        new AdvisorReportRowRenderData(_rowTexture, "Second", "2"),
                    }
                )
            );
            AdvisorReportRowView[] originalRows = FindActiveRows();

            _view.Render(
                CreateRenderData(
                    AdvisorReportMode.GalaxyOverview,
                    "Updated",
                    new[] { new AdvisorReportRowRenderData(_rowTexture, "Only", "3") }
                )
            );

            AdvisorReportRowView activeRow = FindActiveRows().Single();
            Assert.AreSame(originalRows[0], activeRow);
            Assert.AreEqual("Only", FindRowText(activeRow, "PrimaryTextField").text);
            Assert.IsFalse(originalRows[1].gameObject.activeSelf);
        }

        [Test]
        public void Render_EmptyRows_HidesCachedRows()
        {
            _view.Render(
                CreateRenderData(
                    AdvisorReportMode.Objectives,
                    "Objectives",
                    new[] { new AdvisorReportRowRenderData(_rowTexture, "Objective", "Open") }
                )
            );
            AdvisorReportRowView row = FindActiveRows().Single();

            _view.Render(
                CreateRenderData(
                    AdvisorReportMode.Objectives,
                    "Objectives",
                    Array.Empty<AdvisorReportRowRenderData>()
                )
            );

            Assert.IsFalse(row.gameObject.activeSelf);
            Assert.AreEqual(0, FindActiveRows().Length);
        }

        [Test]
        public void Render_InvalidMode_ThrowsArgumentOutOfRangeException()
        {
            AdvisorReportWindowRenderData data = CreateRenderData(
                (AdvisorReportMode)99,
                "Invalid",
                Array.Empty<AdvisorReportRowRenderData>()
            );

            Assert.Throws<ArgumentOutOfRangeException>(() => _view.Render(data));
        }

        [Test]
        public void RequestClose_SubscribedHandler_EmitsSemanticRequest()
        {
            AdvisorReportWindowView requestedView = null;
            _view.CloseRequested += view => requestedView = view;

            _view.RequestClose();

            Assert.AreSame(_view, requestedView);
        }

        [Test]
        public void AuthoredCloseButton_Click_EmitsSemanticRequest()
        {
            int closeCount = 0;
            _view.CloseRequested += _ => closeCount++;

            FindComponent<Button>("CloseButtonImage").onClick.Invoke();

            Assert.AreEqual(1, closeCount);
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsCloseButtonAndRaisesDestroyedEvent()
        {
            AdvisorReportWindowView destroyedView = null;
            int closeCount = 0;
            _view.Destroyed += view => destroyedView = view;
            _view.CloseRequested += _ => closeCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            FindComponent<Button>("CloseButtonImage").onClick.Invoke();

            Assert.AreSame(_view, destroyedView);
            Assert.AreEqual(0, closeCount);
        }

        private AdvisorReportWindowRenderData CreateRenderData(
            AdvisorReportMode mode,
            string title,
            AdvisorReportRowRenderData[] rows
        )
        {
            return new AdvisorReportWindowRenderData(
                22,
                34,
                mode,
                _backgroundTexture,
                _galaxyTexture,
                title,
                rows
            );
        }

        private AdvisorReportRowView[] FindActiveRows()
        {
            return _viewObject
                .GetComponentsInChildren<AdvisorReportRowView>(true)
                .Where(row => row.name.StartsWith("AdvisorReportRow", StringComparison.Ordinal))
                .Where(row => row.gameObject.activeSelf)
                .OrderBy(row => row.name)
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

        private static RawImage FindRowImage(AdvisorReportRowView row)
        {
            return row.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "Image");
        }

        private static TextMeshProUGUI FindRowText(AdvisorReportRowView row, string objectName)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == objectName);
        }
    }
}
