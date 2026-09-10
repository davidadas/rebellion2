using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class NormalizedSliderViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab";

        private GameObject _rootObject;
        private NormalizedSliderView _view;

        /// <summary>
        /// Creates the generated normalized slider for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<NormalizedSliderView>(true);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Destroys the generated slider after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        /// <summary>
        /// Verifies rendering clamps values and places the thumb consistently.
        /// </summary>
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

        /// <summary>
        /// Verifies slider input repositions the thumb and emits a normalized value.
        /// </summary>
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

        /// <summary>
        /// Verifies disabling the view removes its slider listener.
        /// </summary>
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

        /// <summary>
        /// Reads a private authored reference from the slider under test.
        /// </summary>
        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(NormalizedSliderView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }

        /// <summary>
        /// Reads a transform in the source-pixel layout coordinate system.
        /// </summary>
        private static RectInt GetSourceRect(Transform transform)
        {
            return UILayout.GetSourceRect(transform as RectTransform);
        }
    }
}
