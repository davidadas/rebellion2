using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class SpriteSheetAnimationViewTests
    {
        private GameObject root;
        private SpriteSheetAnimationView view;
        private RawImage image;
        private Texture2D texture;
        private SpriteSheetAnimationViewData animation;

        /// <summary>
        /// Instantiates only the small animation control and its test texture.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            view = UIComponentTestHelper.InstantiatePrefabComponent<SpriteSheetAnimationView>(
                "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab"
            );
            root = view.gameObject;
            image = view.GetComponentInChildren<RawImage>(true);
            texture = new Texture2D(16, 16);
            animation = new SpriteSheetAnimationViewData(texture, 4, 2, 4f);
        }

        /// <summary>
        /// Releases the control and the texture owned by this test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void Awake_AuthoredIndicator_StartsWithPlaybackAndArtworkDisabled()
        {
            Assert.IsFalse(view.enabled);
            Assert.IsFalse(image.enabled);
        }

        [Test]
        public void Render_Visible_StartsAtTopLeftFrame()
        {
            view.Render(animation, true);
            Assert.IsTrue(view.enabled);
            Assert.IsTrue(image.enabled);
            Assert.AreSame(texture, image.texture);
            Assert.AreEqual(new Rect(0f, 0.5f, 0.5f, 0.5f), image.uvRect);
        }

        [TestCase(0.25f, 0.5f, 0.5f)]
        [TestCase(0.5f, 0f, 0f)]
        [TestCase(0.75f, 0.5f, 0f)]
        [TestCase(1f, 0f, 0.5f)]
        [TestCase(2.25f, 0.5f, 0.5f)]
        public void AdvanceAnimation_ElapsedTime_TraversesRowsAndLoops(
            float seconds,
            float x,
            float y
        )
        {
            view.Render(animation, true);
            view.AdvanceAnimation(seconds);
            Assert.AreEqual(new Rect(x, y, 0.5f, 0.5f), image.uvRect);
        }

        [Test]
        public void Render_EquivalentSnapshot_PreservesPlaybackPosition()
        {
            view.Render(animation, true);
            view.AdvanceAnimation(0.25f);
            view.Render(new SpriteSheetAnimationViewData(texture, 4, 2, 4f), true);
            Assert.AreEqual(new Rect(0.5f, 0.5f, 0.5f, 0.5f), image.uvRect);
        }

        [Test]
        public void Render_Hidden_DisablesImageAndPlayback()
        {
            view.Render(animation, true);
            view.AdvanceAnimation(0.25f);
            view.Render(animation, false);
            view.AdvanceAnimation(0.5f);
            Assert.IsFalse(view.enabled);
            Assert.IsFalse(image.enabled);
            Assert.AreEqual(new Rect(0.5f, 0.5f, 0.5f, 0.5f), image.uvRect);
        }

        [Test]
        public void Render_ShownAgain_RestartsAtFirstFrame()
        {
            view.Render(animation, true);
            view.AdvanceAnimation(0.5f);
            view.Render(animation, false);
            view.Render(animation, true);
            Assert.AreEqual(new Rect(0f, 0.5f, 0.5f, 0.5f), image.uvRect);
        }

        [Test]
        public void Render_ChangedLayout_RestartsWithNewGrid()
        {
            view.Render(animation, true);
            view.AdvanceAnimation(0.5f);
            view.Render(new SpriteSheetAnimationViewData(texture, 2, 2, 4f), true);
            Assert.AreEqual(new Rect(0f, 0f, 0.5f, 1f), image.uvRect);
        }

        [Test]
        public void Render_ChangedFactionTexture_RestartsWithNewArtwork()
        {
            Texture2D otherTexture = new Texture2D(16, 16);
            try
            {
                view.Render(animation, true);
                view.AdvanceAnimation(0.5f);
                view.Render(new SpriteSheetAnimationViewData(otherTexture, 4, 2, 4f), true);
                Assert.AreSame(otherTexture, image.texture);
                Assert.AreEqual(new Rect(0f, 0.5f, 0.5f, 0.5f), image.uvRect);
            }
            finally
            {
                Object.DestroyImmediate(otherTexture);
            }
        }

        [Test]
        public void Render_NoAnimation_HidesArtwork()
        {
            view.Render(animation, true);
            view.Render(null, true);
            Assert.IsFalse(view.enabled);
            Assert.IsFalse(image.enabled);
            Assert.IsNull(image.texture);
        }

        [Test]
        public void Render_AuthoredIndicator_ContainsNoModelRenderingRig()
        {
            Assert.IsEmpty(view.GetComponentsInChildren<Camera>(true));
            Assert.IsEmpty(view.GetComponentsInChildren<Light>(true));
            Assert.IsEmpty(view.GetComponentsInChildren<ContentModelBinding>(true));
            GameObject strategy = UIComponentTestHelper.InstantiatePrefab(
                "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab"
            );
            try
            {
                Assert.IsNull(strategy.transform.Find("UI/AutosaveModelPreview"));
            }
            finally
            {
                Object.DestroyImmediate(strategy);
            }
        }
    }
}
