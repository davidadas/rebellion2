using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesDetailPanelViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/MessagesWindow.prefab";

        private Texture2D _cardTexture;
        private Texture2D _iconTexture;
        private Texture2D _overlayTexture;
        private MessagesDetailPanelView _view;
        private GameObject _windowObject;

        [SetUp]
        public void SetUp()
        {
            _windowObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _windowObject.GetComponentInChildren<MessagesDetailPanelView>(true);
            _cardTexture = new Texture2D(80, 40);
            _overlayTexture = new Texture2D(40, 80);
            _iconTexture = new Texture2D(16, 16);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_iconTexture);
            UnityEngine.Object.DestroyImmediate(_overlayTexture);
            UnityEngine.Object.DestroyImmediate(_cardTexture);
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_Detail_AppliesScaledArtworkHeaderNavigationAndLines()
        {
            RawImage card = FindComponent<RawImage>("DetailCardImage");
            RawImage overlay = FindComponent<RawImage>("DetailOverlayImage");
            RectInt expectedCardRect = MessagesDetailPanelView.GetScaledImageRect(
                _cardTexture,
                UILayout.GetSourceRect(card.rectTransform)
            );
            RectInt expectedOverlayRect = MessagesDetailPanelView.GetScaledImageRect(
                _overlayTexture,
                UILayout.GetSourceRect(overlay.rectTransform)
            );
            MessagesDetailPanelRenderData data = CreateDetail(
                "First line\nSecond line",
                true,
                false
            );

            _view.Render(data);

            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.AreSame(_cardTexture, card.texture);
            Assert.AreEqual(expectedCardRect, UILayout.GetSourceRect(card.rectTransform));
            Assert.AreSame(_overlayTexture, overlay.texture);
            Assert.AreEqual(expectedOverlayRect, UILayout.GetSourceRect(overlay.rectTransform));
            Assert.AreSame(_iconTexture, FindComponent<RawImage>("DetailIconImage").texture);
            Assert.AreEqual("Mission report", FindText("DetailHeaderTextField").text);
            Assert.IsFalse(FindComponent<Button>("DetailPreviousButtonImage").interactable);
            Assert.IsFalse(FindComponent<RawImage>("DetailPreviousButtonImage").raycastTarget);
            Assert.IsTrue(FindComponent<Button>("DetailNextButtonImage").interactable);
            Assert.IsTrue(FindComponent<RawImage>("DetailNextButtonImage").raycastTarget);
            CollectionAssert.AreEqual(
                new[] { "First line", "Second line" },
                FindDetailLines().Select(line => line.text).ToArray()
            );
        }

        [Test]
        public void Render_MissingArtwork_HidesCardAndOverlay()
        {
            _view.Render(CreateDetail("Message", false, false));
            MessagesDetailPanelRenderData data = new MessagesDetailPanelRenderData(
                "message-8",
                "No artwork",
                "Message",
                null,
                null,
                _iconTexture,
                false,
                false
            );

            _view.Render(data);

            Assert.IsFalse(FindObject("DetailCardImage").activeSelf);
            Assert.IsFalse(FindObject("DetailOverlayImage").activeSelf);
        }

        [Test]
        public void Render_ShorterText_HidesUnusedCachedLineFields()
        {
            _view.Render(CreateDetail("First\nSecond\nThird", false, false));
            TextMeshProUGUI thirdLine = FindDetailLines().Single(line => line.text == "Third");

            _view.Render(CreateDetail("Replacement", false, true));

            Assert.IsFalse(thirdLine.gameObject.activeSelf);
            Assert.AreEqual("Replacement", FindDetailLines().Single().text);
        }

        [Test]
        public void GetScaledImageRect_ValidTexture_PreservesAuthoredWidthAndAspectRatio()
        {
            RectInt template = new RectInt(3, 4, 160, 120);

            RectInt result = MessagesDetailPanelView.GetScaledImageRect(_cardTexture, template);

            Assert.AreEqual(new RectInt(3, 4, 160, 80), result);
        }

        [Test]
        public void GetScaledImageRect_MissingTexture_ReturnsAuthoredRect()
        {
            RectInt template = new RectInt(3, 4, 160, 120);

            RectInt result = MessagesDetailPanelView.GetScaledImageRect(null, template);

            Assert.AreEqual(template, result);
        }

        [Test]
        public void NavigationButtons_Click_RaisePreviousAndNextRequests()
        {
            int previousCount = 0;
            int nextCount = 0;
            _view.PreviousRequested += () => previousCount++;
            _view.NextRequested += () => nextCount++;

            FindComponent<Button>("DetailPreviousButtonImage").onClick.Invoke();
            FindComponent<Button>("DetailNextButtonImage").onClick.Invoke();

            Assert.AreEqual(1, previousCount);
            Assert.AreEqual(1, nextCount);
        }

        [Test]
        public void Hide_VisiblePanel_DeactivatesPanel()
        {
            _view.Render(CreateDetail("Message", false, false));

            _view.Hide();

            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsNavigationControls()
        {
            int previousCount = 0;
            int nextCount = 0;
            _view.PreviousRequested += () => previousCount++;
            _view.NextRequested += () => nextCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            FindComponent<Button>("DetailPreviousButtonImage").onClick.Invoke();
            FindComponent<Button>("DetailNextButtonImage").onClick.Invoke();

            Assert.AreEqual(0, previousCount);
            Assert.AreEqual(0, nextCount);
        }

        private MessagesDetailPanelRenderData CreateDetail(
            string text,
            bool previousDisabled,
            bool nextDisabled
        )
        {
            return new MessagesDetailPanelRenderData(
                "message-7",
                "Mission report",
                text,
                _cardTexture,
                _overlayTexture,
                _iconTexture,
                previousDisabled,
                nextDisabled
            );
        }

        private TextMeshProUGUI[] FindDetailLines()
        {
            return _windowObject
                .GetComponentsInChildren<TextMeshProUGUI>(true)
                .Where(text =>
                    text.name.StartsWith("DetailLineTextField", StringComparison.Ordinal)
                    && text.name != "DetailLineTextTemplate"
                    && text.gameObject.activeSelf
                )
                .OrderBy(text => UILayout.GetSourceRect(text.rectTransform).y)
                .ToArray();
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _windowObject
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        private GameObject FindObject(string objectName)
        {
            return _windowObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        private TextMeshProUGUI FindText(string objectName)
        {
            return FindComponent<TextMeshProUGUI>(objectName);
        }
    }
}
