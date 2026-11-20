using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalacticInformationLegendViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";
        private const int _sourceDesktopHeight = 480;
        private const int _sourceDesktopWidth = 853;

        private Texture2D _frameTexture;
        private Texture2D _legendTexture;
        private GameObject _rootObject;
        private GalacticInformationLegendView _view;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<GalacticInformationLegendView>(true);
            _legendTexture = new Texture2D(180, 135);
            _frameTexture = new Texture2D(18, 18);
            RectTransform parent = (_view.transform as RectTransform).parent as RectTransform;
            parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _sourceDesktopWidth);
            parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _sourceDesktopHeight);
            UIComponentTestHelper.InvokeLifecycle(
                _view.GetComponentInChildren<GalacticInformationFrameView>(true),
                "Awake"
            );
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_frameTexture);
            UnityEngine.Object.DestroyImmediate(_legendTexture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_Legend_AppliesNativeTextMarkersFrameAndCloseControl()
        {
            GalacticInformationLegendRenderData data = CreateLegend(new Vector2Int(150, 120));

            _view.Render(data);

            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.AreEqual(
                new RectInt(150, 120, 180, 135),
                UILayout.GetSourceRect(_view.transform as RectTransform)
            );
            Image background = FindComponent<Image>("BackgroundImage");
            Assert.AreEqual(Color.black, background.color);
            Assert.IsTrue(background.raycastTarget);
            Assert.AreEqual(
                new RectInt(0, 0, 180, 135),
                UILayout.GetSourceRect(background.rectTransform)
            );
            Assert.AreEqual("Mines", FindComponent<TextMeshProUGUI>("TitleTextField").text);
            Assert.AreEqual(
                "Marker colors",
                FindComponent<TextMeshProUGUI>("KeyHeadingTextField").text
            );
            var rows = _view
                .GetComponentsInChildren<GalacticInformationLegendRowView>(true)
                .Where(row => row.gameObject.activeSelf)
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "6+", "3–5" },
                rows.Select(row => row.GetComponentInChildren<TextMeshProUGUI>(true).text)
            );
            Assert.AreSame(_legendTexture, rows[0].GetComponentInChildren<RawImage>(true).texture);
            TextMeshProUGUI close = FindComponent<TextMeshProUGUI>("CloseTextField");
            Assert.AreEqual(
                new RectInt(160, 8, 12, 12),
                UILayout.GetSourceRect(close.rectTransform)
            );
            Assert.AreEqual(
                new RectInt(160, 8, 12, 12),
                UILayout.GetSourceRect(
                    FindComponent<UIRaycastArea>("CloseHitArea").transform as RectTransform
                )
            );
        }

        [Test]
        public void Render_SubsequentBounds_PreservesInitialSourcePosition()
        {
            _view.Render(CreateLegend(new Vector2Int(150, 120)));

            _view.Render(CreateLegend(new Vector2Int(300, 250)));

            RectInt bounds = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(150, bounds.x);
            Assert.AreEqual(120, bounds.y);
            Assert.AreEqual(180, bounds.width);
            Assert.AreEqual(135, bounds.height);
        }

        [Test]
        public void Render_InitialPositionOutsideParent_ClampsToParentBounds()
        {
            RectTransform parent = (_view.transform as RectTransform).parent as RectTransform;
            int expectedX = Mathf.Max(0, Mathf.RoundToInt(parent.rect.width) - 180);
            int expectedY = Mathf.Max(0, Mathf.RoundToInt(parent.rect.height) - 135);

            _view.Render(CreateLegend(new Vector2Int(999, 999)));

            RectInt bounds = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(expectedX, bounds.x);
            Assert.AreEqual(expectedY, bounds.y);
        }

        [Test]
        public void Render_FewerRows_HidesUnusedMarkerRows()
        {
            _view.Render(CreateLegend(new Vector2Int(150, 120)));
            GalacticInformationLegendRenderData data = new GalacticInformationLegendRenderData(
                new RectInt(150, 120, 180, 135),
                new GalacticInformationTextRenderData(
                    "Uprisings",
                    Color.cyan,
                    new RectInt(10, 6, 140, 22)
                ),
                Color.black,
                new GalacticInformationTextRenderData(
                    "Marker colors",
                    Color.cyan,
                    new RectInt(10, 80, 160, 20)
                ),
                new[]
                {
                    new GalacticInformationLegendRowRenderData(
                        new RectInt(10, 32, 160, 22),
                        _legendTexture,
                        "In uprising"
                    ),
                },
                CreateFrame(),
                new RectInt(160, 8, 12, 12)
            );

            _view.Render(data);

            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.AreEqual(
                1,
                _view
                    .GetComponentsInChildren<GalacticInformationLegendRowView>(true)
                    .Count(row => row.gameObject.activeSelf)
            );
            Assert.AreEqual("Uprisings", FindComponent<TextMeshProUGUI>("TitleTextField").text);
        }

        [Test]
        public void Render_NullData_HidesVisibleLegend()
        {
            _view.Render(CreateLegend(new Vector2Int(150, 120)));

            _view.Render(null);

            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void CloseHitArea_PressReleaseAndClick_UpdatesNativeControlAndRaisesRequest()
        {
            int closeCount = 0;
            _view.CloseRequested += () => closeCount++;
            _view.Render(CreateLegend(new Vector2Int(150, 120)));
            UIRaycastArea hitArea = FindComponent<UIRaycastArea>("CloseHitArea");
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            hitArea.OnPointerDown(eventData);
            Assert.AreEqual(Color.cyan, FindComponent<TextMeshProUGUI>("CloseTextField").color);
            hitArea.OnPointerUp(eventData);
            Assert.AreEqual(Color.white, FindComponent<TextMeshProUGUI>("CloseTextField").color);
            hitArea.OnPointerClick(eventData);

            Assert.AreEqual(1, closeCount);
        }

        [Test]
        public void Hide_VisibleLegend_DeactivatesLegend()
        {
            _view.Render(CreateLegend(new Vector2Int(150, 120)));

            _view.Hide();

            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsCloseAndRaisesDestroyedEvent()
        {
            GalacticInformationLegendView destroyed = null;
            int closeCount = 0;
            _view.Destroyed += view => destroyed = view;
            _view.CloseRequested += () => closeCount++;
            _view.Render(CreateLegend(new Vector2Int(150, 120)));
            UIRaycastArea hitArea = FindComponent<UIRaycastArea>("CloseHitArea");
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            hitArea.OnPointerClick(eventData);

            Assert.AreSame(_view, destroyed);
            Assert.AreEqual(0, closeCount);
        }

        /// <summary>
        /// Creates legend.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The created legend.</returns>
        private GalacticInformationLegendRenderData CreateLegend(Vector2Int position)
        {
            return new GalacticInformationLegendRenderData(
                new RectInt(position.x, position.y, 180, 135),
                new GalacticInformationTextRenderData(
                    "Mines",
                    Color.cyan,
                    new RectInt(10, 6, 140, 22)
                ),
                Color.black,
                new GalacticInformationTextRenderData(
                    "Marker colors",
                    Color.cyan,
                    new RectInt(10, 80, 160, 20)
                ),
                new[]
                {
                    new GalacticInformationLegendRowRenderData(
                        new RectInt(10, 32, 160, 22),
                        _legendTexture,
                        "6+"
                    ),
                    new GalacticInformationLegendRowRenderData(
                        new RectInt(10, 54, 160, 22),
                        _legendTexture,
                        "3–5"
                    ),
                },
                CreateFrame(),
                new RectInt(160, 8, 12, 12)
            );
        }

        /// <summary>
        /// Creates frame.
        /// </summary>
        /// <returns>The created frame.</returns>
        private GalacticInformationFrameRenderData CreateFrame()
        {
            return new GalacticInformationFrameRenderData(
                180,
                135,
                Enumerable.Repeat(_frameTexture, 8).ToArray()
            );
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
            return _view
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }
    }
}
