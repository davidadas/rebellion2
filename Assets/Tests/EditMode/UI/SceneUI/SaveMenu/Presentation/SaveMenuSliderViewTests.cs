using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.SaveMenu.Presentation
{
    [TestFixture]
    public class SaveMenuSliderViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/SaveMenu/SaveMenuSlider.prefab";

        private GameObject _rootObject;
        private SaveMenuSliderView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponent<SaveMenuSliderView>();
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [TestCase(-1f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(2f, 1f)]
        public void Render_Value_ClampsSliderAndPositionsThumb(float value, float expected)
        {
            _view.Render(value);

            Slider slider = GetField<Slider>("slider");
            RawImage thumb = GetField<RawImage>("thumbImage");
            RectInt sliderBounds = GetSourceRect(slider.transform);
            RectInt thumbBounds = GetSourceRect(thumb.transform);
            Assert.AreEqual(expected, slider.value);
            Assert.AreEqual(
                Mathf.RoundToInt(expected * Mathf.Max(0, sliderBounds.width - thumbBounds.width)),
                thumbBounds.x
            );
            Assert.AreEqual(0, thumbBounds.y);
        }

        [Test]
        public void Slider_ValueChanged_RepositionsThumbAndRaisesNormalizedValue()
        {
            _view.Render(0f);
            float requestedValue = -1f;
            _view.ValueChanged += value => requestedValue = value;

            GetField<Slider>("slider").value = 0.75f;

            Assert.AreEqual(0.75f, requestedValue);
            Slider slider = GetField<Slider>("slider");
            RectInt sliderBounds = GetSourceRect(slider.transform);
            RectInt thumbBounds = GetSourceRect(GetField<RawImage>("thumbImage").transform);
            Assert.AreEqual(
                Mathf.RoundToInt(0.75f * (sliderBounds.width - thumbBounds.width)),
                thumbBounds.x
            );
        }

        [Test]
        public void OnDisable_BoundSlider_UnbindsValueChanges()
        {
            _view.Render(0f);
            int requestCount = 0;
            _view.ValueChanged += _ => requestCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDisable");
            GetField<Slider>("slider").value = 1f;

            Assert.AreEqual(0, requestCount);
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(SaveMenuSliderView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }

        private static RectInt GetSourceRect(Transform transform)
        {
            return UILayout.GetSourceRect(transform as RectTransform);
        }
    }
}
