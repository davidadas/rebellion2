using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.SaveMenu.Presentation
{
    [TestFixture]
    public class SaveMenuTacticalOptionRowViewTests
    {
        private const string _prefabPath =
            "Assets/Prefabs/UI/SaveMenu/SaveMenuTacticalOptionRow.prefab";

        private GameObject _rootObject;
        private SaveMenuTacticalOptionRowView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponent<SaveMenuTacticalOptionRowView>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [TestCase(true, "ON")]
        [TestCase(false, "OFF")]
        public void Render_OptionState_AppliesButtonAndTextPresentation(
            bool enabled,
            string expectedText
        )
        {
            Texture2D expectedTexture = GetField<Texture2D>(
                enabled ? "enabledTexture" : "disabledTexture"
            );
            Color expectedColor = GetField<Color>(
                enabled ? "enabledTextColor" : "disabledTextColor"
            );

            _view.Render(enabled);

            RawImage buttonImage = GetPressVisualImage();
            Assert.AreSame(expectedTexture, buttonImage.texture);
            Assert.AreEqual(expectedColor, GetField<TextMeshProUGUI>("labelTextField").color);
            Assert.AreEqual(expectedText, GetField<TextMeshProUGUI>("stateTextField").text);
            Assert.AreEqual(expectedColor, GetField<TextMeshProUGUI>("stateTextField").color);
        }

        [Test]
        public void Button_Click_RaisesConfiguredOption()
        {
            _view.Render(true);
            UserTacticalOption? requestedOption = null;
            _view.ToggleRequested += option => requestedOption = option;

            GetField<Button>("button").onClick.Invoke();

            Assert.AreEqual(_view.Option, requestedOption);
        }

        [Test]
        public void VerifyReferences_AuthoredPrefab_DoesNotThrow()
        {
            Assert.DoesNotThrow(_view.VerifyReferences);
        }

        [Test]
        public void OnEnable_AuthoredRow_BindsButtonWithoutRender()
        {
            UserTacticalOption? requestedOption = null;
            _view.ToggleRequested += option => requestedOption = option;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnEnable");
            GetField<Button>("button").onClick.Invoke();

            Assert.AreEqual(_view.Option, requestedOption);
        }

        [Test]
        public void OnDisable_UnboundRow_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => UIComponentTestHelper.InvokeLifecycle(_view, "OnDisable"));
        }

        [Test]
        public void OnDisable_BoundRow_UnbindsButton()
        {
            _view.Render(true);
            int requestCount = 0;
            _view.ToggleRequested += _ => requestCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDisable");
            GetField<Button>("button").onClick.Invoke();

            Assert.AreEqual(0, requestCount);
        }

        private RawImage GetPressVisualImage()
        {
            RawImagePressVisual visual = GetField<RawImagePressVisual>("buttonPressVisual");
            return (RawImage)
                typeof(RawImagePressVisual)
                    .GetField("image", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(visual);
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(SaveMenuTacticalOptionRowView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }
    }
}
