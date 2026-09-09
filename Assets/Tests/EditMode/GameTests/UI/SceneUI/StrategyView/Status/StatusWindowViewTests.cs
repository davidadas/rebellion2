using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Status
{
    [TestFixture]
    public class StatusWindowViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StatusWindow.prefab";

        private Texture2D _backgroundTexture;
        private Texture2D _firstTexture;
        private Texture2D _secondTexture;
        private StatusWindowView _view;
        private GameObject _viewObject;

        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<StatusWindowView>();
            _backgroundTexture = new Texture2D(420, 260);
            _firstTexture = new Texture2D(120, 60);
            _secondTexture = new Texture2D(40, 100);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_secondTexture);
            UnityEngine.Object.DestroyImmediate(_firstTexture);
            UnityEngine.Object.DestroyImmediate(_backgroundTexture);
            UnityEngine.Object.DestroyImmediate(_viewObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_CompletePresentation_AppliesFrameImagesLabelRowsAndControls()
        {
            StatusWindowRenderData data = CreateRenderData(
                false,
                false,
                "Capital Ship Status",
                new[] { _firstTexture, _secondTexture },
                "Medium Transport",
                new[]
                {
                    new StatusWindowRowRenderData("Class:", "Medium Transport"),
                    new StatusWindowRowRenderData("Status:", "Active"),
                }
            );

            _view.Render(data);

            RectInt windowRect = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(31, windowRect.x);
            Assert.AreEqual(47, windowRect.y);
            RawImage backgroundImage = FindComponent<RawImage>("BackgroundImage");
            Assert.AreSame(_backgroundTexture, backgroundImage.texture);
            Assert.AreEqual(
                UILayout.GetTextureSourceSize(_backgroundTexture),
                UILayout.GetSourceRect(backgroundImage.rectTransform).size
            );
            Assert.AreEqual("Capital Ship Status", FindText("HeaderTextField").text);
            Assert.AreEqual("Medium Transport", FindText("LabelTextField0").text);
            RawImage[] images = FindStatusImages();
            Assert.AreEqual(2, images.Length);
            Assert.AreSame(_firstTexture, images[0].texture);
            Assert.AreSame(_secondTexture, images[1].texture);
            Assert.AreEqual("Class:", FindText("LeftRowTextField0").text);
            Assert.AreEqual("Medium Transport", FindText("RightRowTextField0").text);
            Assert.AreEqual("Status:", FindText("LeftRowTextField1").text);
            Assert.AreEqual("Active", FindText("RightRowTextField1").text);
            Assert.IsTrue(FindComponent<Button>("InfoButtonImage").interactable);
            Assert.IsTrue(_viewObject.activeSelf);
        }

        [Test]
        public void Render_LongDetailRow_KeepsColumnsSeparateAndFitsTextWithoutWrapping()
        {
            _view.Render(
                CreateRenderData(
                    false,
                    false,
                    "Capital Ship Status",
                    new[] { _firstTexture },
                    "Ship",
                    new[]
                    {
                        new StatusWindowRowRenderData(
                            "Maximum Shield Strength:",
                            "Extremely Long Fleet Name"
                        ),
                    }
                )
            );

            TextMeshProUGUI left = FindText("LeftRowTextField0");
            TextMeshProUGUI right = FindText("RightRowTextField0");
            RectInt leftRect = UILayout.GetSourceRect(left.rectTransform);
            RectInt rightRect = UILayout.GetSourceRect(right.rectTransform);

            Assert.LessOrEqual(leftRect.xMax, rightRect.x);
            Assert.IsTrue(left.enableAutoSizing);
            Assert.IsTrue(right.enableAutoSizing);
            Assert.AreEqual(TextWrappingModes.NoWrap, left.textWrappingMode);
            Assert.AreEqual(TextWrappingModes.NoWrap, right.textWrappingMode);
            Assert.AreEqual(TextOverflowModes.Ellipsis, left.overflowMode);
            Assert.AreEqual(TextOverflowModes.Ellipsis, right.overflowMode);
        }

        [Test]
        public void Render_LongLeftLabelWithShortValue_AllocatesAdditionalLabelWidth()
        {
            _view.Render(
                CreateRenderData(
                    false,
                    false,
                    "Manufacturing Status",
                    new[] { _firstTexture },
                    "Construction Yards",
                    new[] { new StatusWindowRowRenderData("Estimated Day of Completion:", "140") }
                )
            );

            RectInt templateRect = UILayout.GetSourceRect(
                FindText("LeftRowTextTemplate").rectTransform
            );
            RectInt labelRect = UILayout.GetSourceRect(FindText("LeftRowTextField0").rectTransform);
            RectInt valueRect = UILayout.GetSourceRect(
                FindText("RightRowTextField0").rectTransform
            );

            Assert.Greater(labelRect.width, templateRect.width);
            Assert.LessOrEqual(labelRect.xMax, valueRect.x);
        }

        [Test]
        public void Render_CenteredImage_CentersFittedImageInAuthoredArea()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    false,
                    "Status",
                    new[] { _firstTexture },
                    string.Empty,
                    Array.Empty<StatusWindowRowRenderData>()
                )
            );

            RectInt imageArea = UILayout.GetSourceRect(
                FindTransform("StatusImageAreaTemplate") as RectTransform
            );
            RectInt imageRect = UILayout.GetSourceRect(FindStatusImages().Single().rectTransform);

            Assert.AreEqual(imageArea.x + (imageArea.width - imageRect.width) / 2, imageRect.x);
            Assert.AreEqual(imageArea.y + (imageArea.height - imageRect.height) / 2, imageRect.y);
        }

        [Test]
        public void Render_LongLabel_WrapsWithinAuthoredStatusColumn()
        {
            _view.Render(
                CreateRenderData(
                    false,
                    false,
                    "Special Forces Status",
                    new[] { _firstTexture },
                    "Longprobe Y-wing Recon Team",
                    Array.Empty<StatusWindowRowRenderData>()
                )
            );

            TextMeshProUGUI label = FindText("LabelTextField0");
            RectInt labelRect = UILayout.GetSourceRect(label.rectTransform);

            Assert.AreEqual(TextWrappingModes.Normal, label.textWrappingMode);
            Assert.AreEqual(126, labelRect.width);
            Assert.Greater(labelRect.height, 17);
        }

        [Test]
        public void Render_NullImagesAndEmptyText_HidesOptionalPresentation()
        {
            _view.Render(
                CreateRenderData(
                    false,
                    false,
                    "Status",
                    new[] { _firstTexture },
                    "Label",
                    new[] { new StatusWindowRowRenderData("Left", "Right") }
                )
            );

            _view.Render(
                CreateRenderData(
                    false,
                    true,
                    string.Empty,
                    new Texture2D[] { null },
                    string.Empty,
                    Array.Empty<StatusWindowRowRenderData>()
                )
            );

            Assert.IsFalse(FindTransform("HeaderTextField").gameObject.activeSelf);
            Assert.IsFalse(FindTransform("LabelTextField0").gameObject.activeSelf);
            Assert.IsFalse(FindTransform("LeftRowTextField0").gameObject.activeSelf);
            Assert.IsFalse(FindTransform("RightRowTextField0").gameObject.activeSelf);
            Assert.IsFalse(FindStatusImages().Single().gameObject.activeSelf);
            Assert.IsFalse(FindComponent<Button>("InfoButtonImage").interactable);
        }

        [Test]
        public void Render_ShorterPresentation_ReusesCachesAndHidesUnusedEntries()
        {
            _view.Render(
                CreateRenderData(
                    false,
                    false,
                    "Status",
                    new[] { _firstTexture, _secondTexture },
                    "Label",
                    new[]
                    {
                        new StatusWindowRowRenderData("One", "First"),
                        new StatusWindowRowRenderData("Two", "Second"),
                    }
                )
            );
            RawImage firstImage = FindStatusImages()[0];
            RawImage secondImage = FindStatusImages()[1];
            TextMeshProUGUI secondLeft = FindText("LeftRowTextField1");

            _view.Render(
                CreateRenderData(
                    false,
                    false,
                    "Updated",
                    new[] { _secondTexture },
                    "Updated Label",
                    new[] { new StatusWindowRowRenderData("Only", "Row") }
                )
            );

            Assert.AreSame(firstImage, FindStatusImages()[0]);
            Assert.AreSame(_secondTexture, firstImage.texture);
            Assert.IsFalse(secondImage.gameObject.activeSelf);
            Assert.AreEqual("Only", FindText("LeftRowTextField0").text);
            Assert.IsFalse(secondLeft.gameObject.activeSelf);
        }

        [Test]
        public void RequestMethods_SubscribedHandlers_EmitSemanticRequests()
        {
            StatusWindowView closeView = null;
            StatusWindowView infoView = null;
            _view.CloseRequested += view => closeView = view;
            _view.InfoRequested += view => infoView = view;

            _view.RequestClose();
            _view.RequestInfo();

            Assert.AreSame(_view, closeView);
            Assert.AreSame(_view, infoView);
        }

        [Test]
        public void AuthoredButtons_PressThenClick_EmitControlBeforeSemanticRequests()
        {
            int controlCount = 0;
            int closeCount = 0;
            int infoCount = 0;
            _view.ControlPressed += _ => controlCount++;
            _view.CloseRequested += _ => closeCount++;
            _view.InfoRequested += _ => infoCount++;
            _view.Render(
                CreateRenderData(
                    false,
                    false,
                    "Status",
                    Array.Empty<Texture2D>(),
                    string.Empty,
                    Array.Empty<StatusWindowRowRenderData>()
                )
            );
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            FindComponent<RawImagePressVisual>("CloseButtonImage").OnPointerDown(eventData);
            Assert.AreEqual(1, controlCount);
            Assert.AreEqual(0, closeCount);
            FindComponent<Button>("CloseButtonImage").onClick.Invoke();
            FindComponent<RawImagePressVisual>("CloseButtonImage").OnPointerUp(eventData);
            FindComponent<RawImagePressVisual>("InfoButtonImage").OnPointerDown(eventData);
            Assert.AreEqual(2, controlCount);
            Assert.AreEqual(0, infoCount);
            FindComponent<Button>("InfoButtonImage").onClick.Invoke();

            Assert.AreEqual(2, controlCount);
            Assert.AreEqual(1, closeCount);
            Assert.AreEqual(1, infoCount);
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsButtonsAndRaisesDestroyedEvent()
        {
            StatusWindowView destroyedView = null;
            int controlCount = 0;
            int closeCount = 0;
            int infoCount = 0;
            _view.Destroyed += view => destroyedView = view;
            _view.ControlPressed += _ => controlCount++;
            _view.CloseRequested += _ => closeCount++;
            _view.InfoRequested += _ => infoCount++;
            _view.Render(
                CreateRenderData(
                    false,
                    false,
                    "Status",
                    Array.Empty<Texture2D>(),
                    string.Empty,
                    Array.Empty<StatusWindowRowRenderData>()
                )
            );

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            FindComponent<RawImagePressVisual>("CloseButtonImage")
                .OnPointerDown(
                    new PointerEventData(null) { button = PointerEventData.InputButton.Left }
                );
            FindComponent<Button>("CloseButtonImage").onClick.Invoke();
            FindComponent<Button>("InfoButtonImage").onClick.Invoke();

            Assert.AreSame(_view, destroyedView);
            Assert.AreEqual(0, controlCount);
            Assert.AreEqual(0, closeCount);
            Assert.AreEqual(0, infoCount);
        }

        private StatusWindowRenderData CreateRenderData(
            bool centerImage,
            bool infoDisabled,
            string header,
            Texture2D[] imageTextures,
            string label,
            StatusWindowRowRenderData[] rows
        )
        {
            return new StatusWindowRenderData(
                31,
                47,
                _backgroundTexture,
                centerImage,
                infoDisabled,
                header,
                imageTextures,
                label,
                rows
            );
        }

        private RawImage[] FindStatusImages()
        {
            return _viewObject
                .GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name.StartsWith("StatusImage", StringComparison.Ordinal))
                .Where(image => image.name != "StatusImageTemplate")
                .OrderBy(image => image.name)
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

        private Transform FindTransform(string objectName)
        {
            return _viewObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName);
        }
    }
}
