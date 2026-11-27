using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Shared
{
    [TestFixture]
    [Category("RequiresMedia")]
    public sealed class StrategyUnitCardViewTests
    {
        private GameObject window;
        private StrategyUnitCardView card;
        private Texture2D texture;

        /// <summary>
        /// Opens an authored unit-card template with every production image reference.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            window = UIComponentTestHelper.InstantiatePrefab(
                "Assets/Prefabs/UI/StrategyView/DefenseWindow.prefab"
            );
            card = window.GetComponentInChildren<StrategyUnitCardView>(true);
            for (Transform parent = card.transform.parent; parent != null; parent = parent.parent)
            {
                parent.gameObject.SetActive(true);
                if (parent == window.transform)
                    break;
            }
            texture = new Texture2D(275, 113);
        }

        /// <summary>
        /// Releases the fixture images and hierarchy.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(window);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void Render_CustomArtworkOrder_DrawsInjuryAboveCapture()
        {
            card.Render(
                Data(
                    new[]
                    {
                        GameplayArtworkLayer.Entity,
                        GameplayArtworkLayer.Travel,
                        GameplayArtworkLayer.Captured,
                        GameplayArtworkLayer.Damage,
                    }
                )
            );

            Assert.Greater(
                Image("DamagedOverlayImage").transform.GetSiblingIndex(),
                Image("CapturedOverlayImage").transform.GetSiblingIndex()
            );
        }

        [Test]
        public void Render_UnconfiguredUnitAfterCustomUnit_RestoresDefaultDrawingOrder()
        {
            card.Render(
                Data(
                    new[]
                    {
                        GameplayArtworkLayer.Entity,
                        GameplayArtworkLayer.Travel,
                        GameplayArtworkLayer.Captured,
                        GameplayArtworkLayer.Damage,
                    }
                )
            );
            card.Render(Data(null));

            Assert.Less(
                Image("DamagedOverlayImage").transform.GetSiblingIndex(),
                Image("CapturedOverlayImage").transform.GetSiblingIndex()
            );
        }

        [Test]
        public void TryAppendDragImages_CustomArtworkOrder_MatchesVisibleCard()
        {
            card.Render(
                Data(
                    new[]
                    {
                        GameplayArtworkLayer.Entity,
                        GameplayArtworkLayer.Travel,
                        GameplayArtworkLayer.Captured,
                        GameplayArtworkLayer.Damage,
                    }
                )
            );
            var images = new List<RawImage>();
            Assert.IsTrue(card.TryAppendDragImages(images));

            Assert.Less(
                images.FindIndex(image => image.name == "CapturedOverlayImage"),
                images.FindIndex(image => image.name == "DamagedOverlayImage")
            );
        }

        /// <summary>
        /// Creates a complete card containing simultaneous travel, injury and capture images.
        /// </summary>
        /// <param name="order">The optional authored layer order.</param>
        /// <returns>The immutable card presentation.</returns>
        private StrategyUnitCardRenderData Data(IReadOnlyList<GameplayArtworkLayer> order) =>
            new(
                "Officer",
                Color.white,
                true,
                false,
                texture,
                texture,
                texture,
                texture,
                texture,
                null,
                0,
                null,
                null,
                null,
                true,
                artworkLayerOrder: order
            );

        /// <summary>
        /// Gets one authored image within the card.
        /// </summary>
        /// <param name="name">The image's authored name.</param>
        /// <returns>The matching image.</returns>
        private RawImage Image(string name) =>
            card.GetComponentsInChildren<RawImage>(true).Single(image => image.name == name);
    }
}
