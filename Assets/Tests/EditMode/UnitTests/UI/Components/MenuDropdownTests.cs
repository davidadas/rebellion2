using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    [Category("RequiresMedia")]
    public sealed class MenuDropdownTests
    {
        private GameObject canvas;
        private MenuDropdown dropdown;
        private Texture2D texture;
        private Sprite sprite;

        /// <summary>
        /// Creates the actual shared dropdown and independently owned thumbnail pixels.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("Dropdown test", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            dropdown = UIComponentTestHelper.InstantiatePrefabComponent<MenuDropdown>(
                "Assets/Prefabs/UI/Common/MenuDropdown.prefab"
            );
            dropdown.transform.SetParent(canvas.transform, false);
            texture = new Texture2D(8, 4);
            sprite = Sprite.Create(texture, new Rect(0, 0, 8, 4), Vector2.one * 0.5f);
        }

        /// <summary>
        /// Releases native popup objects before their owner and test image are destroyed.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (canvas != null)
                Object.DestroyImmediate(canvas);
            if (sprite != null)
                Object.DestroyImmediate(sprite);
            if (texture != null)
                Object.DestroyImmediate(texture);
        }

        [Test]
        public void SetOptions_ImageChoices_DisplaysTheSelectedThumbnailAndReservesItsSpace()
        {
            dropdown.SetOptions(
                new List<TMP_Dropdown.OptionData> { new("Planet", sprite, Color.white) }
            );

            Assert.AreSame(sprite, dropdown.captionImage.sprite);
            Assert.IsTrue(dropdown.captionImage.enabled);
            Assert.IsTrue(dropdown.captionImage.preserveAspect);
            Assert.IsTrue(dropdown.itemImage.preserveAspect);
            Assert.GreaterOrEqual(
                dropdown.captionText.rectTransform.offsetMin.x,
                dropdown.captionImage.rectTransform.anchoredPosition.x
                    + dropdown.captionImage.rectTransform.rect.width
            );
            Assert.GreaterOrEqual(
                dropdown.itemText.rectTransform.offsetMin.x,
                dropdown.itemImage.rectTransform.anchoredPosition.x
                    + dropdown.itemImage.rectTransform.rect.width
            );
        }

        [Test]
        public void SetOptions_TextChoicesAfterImages_RestoresTheTextRowSizeAndPadding()
        {
            RectTransform item = (RectTransform)dropdown.itemText.transform.parent;
            float originalHeight = item.rect.height;
            Vector2 originalPadding = dropdown.captionText.rectTransform.offsetMin;
            dropdown.SetOptions(
                new List<TMP_Dropdown.OptionData> { new("Planet", sprite, Color.white) }
            );
            Assert.Greater(item.rect.height, originalHeight);

            dropdown.SetOptions(
                new List<TMP_Dropdown.OptionData> { new("Very slow"), new("Normal") }
            );

            Assert.AreEqual(originalHeight, item.rect.height);
            Assert.AreEqual(originalPadding, dropdown.captionText.rectTransform.offsetMin);
            Assert.IsFalse(dropdown.captionImage.gameObject.activeSelf);
            Assert.IsFalse(dropdown.itemImage.gameObject.activeSelf);
        }

        [Test]
        public void SetOptions_MixedChoices_HidesTheThumbnailForTheTextOnlySelection()
        {
            dropdown.SetOptions(
                new List<TMP_Dropdown.OptionData>
                {
                    new("Planet", sprite, Color.white),
                    new("No image"),
                }
            );

            dropdown.value = 1;

            Assert.AreEqual("No image", dropdown.captionText.text);
            Assert.IsFalse(dropdown.captionImage.enabled);
        }
    }
}
