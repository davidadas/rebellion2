using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalacticInformationFrameViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private GalacticInformationFrameView _view;
        private GameObject _rootObject;
        private Texture2D _texture;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            Transform submenu = FindTransform("ResourcesSubmenu");
            _view = submenu.GetComponentInChildren<GalacticInformationFrameView>(true);
            _texture = new Texture2D(45, 45);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        /// <summary>
        /// Verifies render complete frame applies textures and section geometry.
        /// </summary>
        [Test]
        public void Render_CompleteFrame_AppliesTexturesAndSectionGeometry()
        {
            Vector2Int sectionSize = UILayout.GetTextureSourceSize(_texture);
            GalacticInformationFrameRenderData data = new GalacticInformationFrameRenderData(
                100,
                80,
                Enumerable.Repeat(_texture, 8).ToArray()
            );

            _view.Render(data);

            Assert.AreEqual(
                new RectInt(0, 0, 100, 80),
                UILayout.GetSourceRect(_view.transform as RectTransform)
            );
            Assert.AreEqual(
                new RectInt(0, 0, sectionSize.x, sectionSize.y),
                UILayout.GetSourceRect(FindImage("TopLeftImage").rectTransform)
            );
            Assert.AreEqual(
                new RectInt(100 - sectionSize.x, 0, sectionSize.x, sectionSize.y),
                UILayout.GetSourceRect(FindImage("TopRightImage").rectTransform)
            );
            Assert.AreEqual(
                new RectInt(sectionSize.x, 0, 100 - sectionSize.x * 2, sectionSize.y),
                UILayout.GetSourceRect(FindImage("TopImage").rectTransform)
            );
            Assert.AreEqual(
                new RectInt(0, sectionSize.y, sectionSize.x, 80 - sectionSize.y * 2),
                UILayout.GetSourceRect(FindImage("LeftImage").rectTransform)
            );
            Assert.AreSame(_texture, FindImage("BottomRightImage").texture);
            Assert.IsTrue(FindImage("BottomRightImage").gameObject.activeSelf);
            Assert.IsFalse(FindImage("BottomRightImage").raycastTarget);
        }

        /// <summary>
        /// Verifies render null data hides all frame sections.
        /// </summary>
        [Test]
        public void Render_NullData_HidesAllFrameSections()
        {
            _view.Render(
                new GalacticInformationFrameRenderData(
                    100,
                    80,
                    Enumerable.Repeat(_texture, 8).ToArray()
                )
            );

            _view.Render(null);

            foreach (RawImage image in _view.GetComponentsInChildren<RawImage>(true))
            {
                Assert.IsNull(image.texture);
                Assert.IsFalse(image.enabled);
                Assert.IsFalse(image.gameObject.activeSelf);
            }
        }

        /// <summary>
        /// Verifies render incomplete texture collection throws argument exception.
        /// </summary>
        [Test]
        public void Render_IncompleteTextureCollection_ThrowsArgumentException()
        {
            GalacticInformationFrameRenderData data = new GalacticInformationFrameRenderData(
                100,
                80,
                Enumerable.Repeat(_texture, 7).ToArray()
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
        }

        /// <summary>
        /// Verifies render missing section texture hides only missing section.
        /// </summary>
        [Test]
        public void Render_MissingSectionTexture_HidesOnlyMissingSection()
        {
            Texture2D[] textures = Enumerable.Repeat(_texture, 8).ToArray();
            textures[0] = null;

            _view.Render(new GalacticInformationFrameRenderData(100, 80, textures));

            Assert.IsFalse(FindImage("TopLeftImage").gameObject.activeSelf);
            Assert.IsTrue(FindImage("TopRightImage").gameObject.activeSelf);
        }

        /// <summary>
        /// Finds image.
        /// </summary>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching image.</returns>
        private RawImage FindImage(string objectName)
        {
            return _view
                .GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == objectName);
        }

        /// <summary>
        /// Finds transform.
        /// </summary>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching transform.</returns>
        private Transform FindTransform(string objectName)
        {
            return _rootObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName);
        }
    }
}
