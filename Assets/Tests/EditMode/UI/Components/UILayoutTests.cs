using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class UILayoutTests
    {
        private GameObject _imageObject;
        private RawImage _image;
        private GameObject _textObject;
        private TextMeshProUGUI _text;
        private Texture2D _texture;

        [SetUp]
        public void SetUp()
        {
            _imageObject = new GameObject("Image", typeof(RectTransform), typeof(RawImage));
            _image = _imageObject.GetComponent<RawImage>();
            _textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            _text = _textObject.GetComponent<TextMeshProUGUI>();
            _texture = new Texture2D(450, 225);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_textObject);
            UnityEngine.Object.DestroyImmediate(_imageObject);
        }

        [Test]
        public void SetImage_TextureAndPosition_UsesTextureSourceDimensions()
        {
            UILayout.SetImage(_image, _texture, 12, 34);

            Assert.AreSame(_texture, _image.texture);
            Assert.AreEqual(
                new RectInt(12, 34, 100, 50),
                UILayout.GetSourceRect(_image.rectTransform)
            );
            Assert.IsFalse(_image.raycastTarget);
        }

        [Test]
        public void SetImage_ExplicitBounds_AppliesProvidedRectangle()
        {
            UILayout.SetImage(_image, _texture, 1, 2, 3, 4);

            Assert.AreEqual(new RectInt(1, 2, 3, 4), UILayout.GetSourceRect(_image.rectTransform));
        }

        [Test]
        public void SetImageTexture_NullTexture_HidesImage()
        {
            UILayout.SetImageTexture(_image, null);

            Assert.IsNull(_image.texture);
            Assert.IsFalse(_image.enabled);
            Assert.IsFalse(_imageObject.activeSelf);
            Assert.IsFalse(_image.raycastTarget);
        }

        [Test]
        public void SetCenteredImage_TextureLargerThanSlot_FitsAndCentersImage()
        {
            UILayout.SetCenteredImage(_image, _texture, new RectInt(10, 20, 60, 60));

            Assert.AreEqual(
                new RectInt(10, 35, 60, 30),
                UILayout.GetSourceRect(_image.rectTransform)
            );
        }

        [Test]
        public void SetHorizontallyCenteredImage_TextureLargerThanSlot_PreservesTopAndCentersImage()
        {
            UILayout.SetHorizontallyCenteredImage(_image, _texture, new RectInt(10, 20, 60, 60));

            Assert.AreEqual(
                new RectInt(10, 20, 60, 30),
                UILayout.GetSourceRect(_image.rectTransform)
            );
        }

        [Test]
        public void SetInteractiveImageTexture_Texture_ShowsInteractiveImage()
        {
            UILayout.SetInteractiveImageTexture(_image, _texture);

            Assert.AreSame(_texture, _image.texture);
            Assert.IsTrue(_image.enabled);
            Assert.IsTrue(_imageObject.activeSelf);
            Assert.IsTrue(_image.raycastTarget);
        }

        [Test]
        public void SetRightAlignedImageSize_Texture_PreservesAuthoredRightEdge()
        {
            UILayout.SetSourceRect(_image.rectTransform, 10, 20, 140, 60);

            UILayout.SetRightAlignedImageSize(_image, _texture);

            Assert.AreEqual(
                new RectInt(50, 20, 100, 50),
                UILayout.GetSourceRect(_image.rectTransform)
            );
        }

        [Test]
        public void SetRightAlignedImageSize_NullInputs_PreservesAuthoredRectangle()
        {
            UILayout.SetSourceRect(_image.rectTransform, 10, 20, 30, 40);

            UILayout.SetRightAlignedImageSize(null, _texture);
            UILayout.SetRightAlignedImageSize(_image, null);

            Assert.AreEqual(
                new RectInt(10, 20, 30, 40),
                UILayout.GetSourceRect(_image.rectTransform)
            );
        }

        [Test]
        public void SetTextContent_ValueAndColor_AppliesPresentationWithoutRaycast()
        {
            Color32 color = new Color32(10, 20, 30, 40);

            UILayout.SetTextContent(_text, "Value", color);

            Assert.AreEqual("Value", _text.text);
            Assert.AreEqual(color, (Color32)_text.color);
            Assert.IsFalse(_text.raycastTarget);
            Assert.IsTrue(_textObject.activeSelf);
        }

        [Test]
        public void SetTextContent_NullValue_NormalizesToEmptyString()
        {
            UILayout.SetTextContent(_text, null);

            Assert.AreEqual(string.Empty, _text.text);
        }

        [Test]
        public void SetTemplateText_ExplicitRectangle_CopiesTypographyAndAppliesBounds()
        {
            GameObject templateObject = CreateTextObject("Template", out TextMeshProUGUI template);
            template.fontSize = 17;
            template.textWrappingMode = TextWrappingModes.Normal;
            template.overflowMode = TextOverflowModes.Ellipsis;
            template.maskable = false;
            template.alignment = TextAlignmentOptions.BottomRight;

            UILayout.SetTemplateText(_text, template, "Value", Color.red, new RectInt(1, 2, 3, 4));

            Assert.AreEqual("Value", _text.text);
            Assert.AreEqual(17, _text.fontSize);
            Assert.AreEqual(TextWrappingModes.Normal, _text.textWrappingMode);
            Assert.AreEqual(TextOverflowModes.Ellipsis, _text.overflowMode);
            Assert.IsFalse(_text.maskable);
            Assert.AreEqual(TextAlignmentOptions.BottomRight, _text.alignment);
            Assert.AreEqual(new RectInt(1, 2, 3, 4), UILayout.GetSourceRect(_text.rectTransform));
            UnityEngine.Object.DestroyImmediate(templateObject);
        }

        [Test]
        public void SetTemplateText_AuthoredRectangle_CopiesTypographyAndBounds()
        {
            GameObject templateObject = CreateTextObject("Template", out TextMeshProUGUI template);
            template.fontSize = 19;
            template.alignment = TextAlignmentOptions.Center;
            UILayout.SetSourceRect(template.rectTransform, 5, 6, 70, 8);

            UILayout.SetTemplateText(_text, template, "Value", Color.green);

            Assert.AreEqual(19, _text.fontSize);
            Assert.AreEqual(TextAlignmentOptions.Center, _text.alignment);
            Assert.AreEqual(new RectInt(5, 6, 70, 8), UILayout.GetSourceRect(_text.rectTransform));
            UnityEngine.Object.DestroyImmediate(templateObject);
        }

        [Test]
        public void WrapText_NullTemplate_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => UILayout.WrapText(null, "Value", 10));
        }

        [Test]
        public void WrapText_NonpositiveMaximumWidth_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UILayout.WrapText(_text, "Value", 0));
        }

        [Test]
        public void WrapText_EmptyText_ReturnsEmptyCollection()
        {
            List<string> lines = UILayout.WrapText(_text, string.Empty, 10);

            Assert.IsEmpty(lines);
        }

        [Test]
        public void WrapText_TabularAndMultilineText_PreservesSourceRows()
        {
            List<string> lines = UILayout.WrapText(_text, "A\tB\r\nC\tD", 10);

            CollectionAssert.AreEqual(new[] { "A\tB", "C\tD" }, lines);
        }

        [TestCase(TextAnchor.UpperLeft, TextAlignmentOptions.TopLeft, 40)]
        [TestCase(TextAnchor.UpperCenter, TextAlignmentOptions.Top, 20)]
        [TestCase(TextAnchor.MiddleCenter, TextAlignmentOptions.Center, 20)]
        [TestCase(TextAnchor.UpperRight, TextAlignmentOptions.TopRight, 40)]
        [TestCase(TextAnchor.MiddleRight, TextAlignmentOptions.Right, 40)]
        [TestCase(TextAnchor.LowerLeft, TextAlignmentOptions.BottomLeft, 40)]
        public void SetText_Anchor_AppliesAlignmentAndHorizontalReference(
            TextAnchor anchor,
            TextAlignmentOptions expectedAlignment,
            int expectedX
        )
        {
            UILayout.SetText(_text, "Value", 40, 5, 40, 10, Color.white, 12, anchor);

            Assert.AreEqual(expectedAlignment, _text.alignment);
            Assert.AreEqual(expectedX, UILayout.GetSourceRect(_text.rectTransform).x);
            Assert.AreEqual(12, _text.fontSize);
            Assert.AreEqual(TextWrappingModes.NoWrap, _text.textWrappingMode);
            Assert.AreEqual(TextOverflowModes.Overflow, _text.overflowMode);
        }

        [Test]
        public void CopySourceRect_SourceTransform_CopiesCompleteAuthoredGeometry()
        {
            GameObject sourceObject = new GameObject("Source", typeof(RectTransform));
            RectTransform source = sourceObject.GetComponent<RectTransform>();
            source.anchorMin = new Vector2(0.2f, 0.3f);
            source.anchorMax = new Vector2(0.8f, 0.9f);
            source.pivot = new Vector2(0.4f, 0.6f);
            source.anchoredPosition = new Vector2(7f, 8f);
            source.sizeDelta = new Vector2(9f, 10f);

            UILayout.CopySourceRect(_image.rectTransform, source);

            Assert.AreEqual(source.anchorMin, _image.rectTransform.anchorMin);
            Assert.AreEqual(source.anchorMax, _image.rectTransform.anchorMax);
            Assert.AreEqual(source.pivot, _image.rectTransform.pivot);
            Assert.AreEqual(source.anchoredPosition, _image.rectTransform.anchoredPosition);
            Assert.AreEqual(source.sizeDelta, _image.rectTransform.sizeDelta);
            Assert.AreEqual(Vector3.one, _image.rectTransform.localScale);
            UnityEngine.Object.DestroyImmediate(sourceObject);
        }

        [Test]
        public void StretchLayoutMethods_Transforms_ApplyExpectedAnchorsAndOffsets()
        {
            UILayout.SetStretch(_image.rectTransform);

            Assert.AreEqual(Vector2.zero, _image.rectTransform.anchorMin);
            Assert.AreEqual(Vector2.one, _image.rectTransform.anchorMax);
            Assert.AreEqual(Vector2.zero, _image.rectTransform.offsetMin);
            Assert.AreEqual(Vector2.zero, _image.rectTransform.offsetMax);

            UILayout.SetTopStretchRect(_image.rectTransform, 1, 2, 3, 4);

            Assert.AreEqual(Vector2.up, _image.rectTransform.anchorMin);
            Assert.AreEqual(Vector2.one, _image.rectTransform.anchorMax);
            Assert.AreEqual(new Vector2(1, -6), _image.rectTransform.offsetMin);
            Assert.AreEqual(new Vector2(-3, -2), _image.rectTransform.offsetMax);

            UILayout.SetBottomStretchRect(_image.rectTransform, 5, 6, 7, 8);

            Assert.AreEqual(Vector2.zero, _image.rectTransform.anchorMin);
            Assert.AreEqual(Vector2.right, _image.rectTransform.anchorMax);
            Assert.AreEqual(new Vector2(5, 6), _image.rectTransform.offsetMin);
            Assert.AreEqual(new Vector2(-7, 14), _image.rectTransform.offsetMax);
        }

        [Test]
        public void SideAndCornerLayoutMethods_Transforms_ApplyExpectedGeometry()
        {
            UILayout.SetLeftStretchRect(_image.rectTransform, 1, 2, 3, 4);

            Assert.AreEqual(Vector2.zero, _image.rectTransform.anchorMin);
            Assert.AreEqual(Vector2.up, _image.rectTransform.anchorMax);
            Assert.AreEqual(new Vector2(1, 3), _image.rectTransform.offsetMin);
            Assert.AreEqual(new Vector2(5, -2), _image.rectTransform.offsetMax);

            UILayout.SetRightStretchRect(_image.rectTransform, 5, 6, 7, 8);

            Assert.AreEqual(Vector2.right, _image.rectTransform.anchorMin);
            Assert.AreEqual(Vector2.one, _image.rectTransform.anchorMax);
            Assert.AreEqual(new Vector2(-13, 7), _image.rectTransform.offsetMin);
            Assert.AreEqual(new Vector2(-5, -6), _image.rectTransform.offsetMax);

            UILayout.SetTopRightRect(_image.rectTransform, 9, 10, 11, 12);

            Assert.AreEqual(Vector2.one, _image.rectTransform.anchorMin);
            Assert.AreEqual(Vector2.one, _image.rectTransform.anchorMax);
            Assert.AreEqual(Vector2.one, _image.rectTransform.pivot);
            Assert.AreEqual(new Vector2(-9, -10), _image.rectTransform.anchoredPosition);
            Assert.AreEqual(new Vector2(11, 12), _image.rectTransform.sizeDelta);
        }

        [Test]
        public void GetSourceSize_AuthoredTransform_ReturnsPositiveDimensions()
        {
            _image.rectTransform.sizeDelta = new Vector2(640, 480);

            Vector2Int size = UILayout.GetSourceSize(_image.rectTransform);

            Assert.AreEqual(new Vector2Int(640, 480), size);
        }

        [Test]
        public void GetSourceSize_MissingTransform_ReturnsZero()
        {
            Vector2Int size = UILayout.GetSourceSize(null);

            Assert.AreEqual(Vector2Int.zero, size);
        }

        [Test]
        public void TryGetSourcePosition_SurfaceCenter_ReturnsSourceCenter()
        {
            _image.rectTransform.sizeDelta = new Vector2(640, 480);
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(
                null,
                _image.rectTransform.TransformPoint(Vector3.zero)
            );

            bool resolved = UILayout.TryGetSourcePosition(
                _image.rectTransform,
                screenPosition,
                null,
                out Vector2Int sourcePosition
            );

            Assert.IsTrue(resolved);
            Assert.AreEqual(new Vector2Int(320, 240), sourcePosition);
        }

        [Test]
        public void TryGetSourcePosition_MissingSurface_ReturnsFalse()
        {
            bool resolved = UILayout.TryGetSourcePosition(
                null,
                Vector2.zero,
                null,
                out Vector2Int sourcePosition
            );

            Assert.IsFalse(resolved);
            Assert.AreEqual(Vector2Int.zero, sourcePosition);
        }

        [Test]
        public void TryGetSourcePosition_ZeroSizeSurface_ReturnsFalse()
        {
            _image.rectTransform.sizeDelta = Vector2.zero;

            bool resolved = UILayout.TryGetSourcePosition(
                _image.rectTransform,
                Vector2.zero,
                null,
                out Vector2Int sourcePosition
            );

            Assert.IsFalse(resolved);
            Assert.AreEqual(Vector2Int.zero, sourcePosition);
        }

        [Test]
        public void GetFittedImageSize_InvalidInputs_ReturnsZero()
        {
            Assert.AreEqual(
                Vector2Int.zero,
                UILayout.GetFittedImageSize(null, new RectInt(0, 0, 1, 1))
            );
            Assert.AreEqual(
                Vector2Int.zero,
                UILayout.GetFittedImageSize(_texture, new RectInt(0, 0, 0, 1))
            );
            Assert.AreEqual(
                Vector2Int.zero,
                UILayout.GetFittedImageSize(_texture, new RectInt(0, 0, 1, 0))
            );
        }

        [Test]
        public void TextureSourceSize_HdTexture_ReturnsSourceDimensions()
        {
            Vector2Int size = UILayout.GetTextureSourceSize(_texture);

            Assert.AreEqual(new Vector2Int(100, 50), size);
            Assert.AreEqual(100, UILayout.GetTextureSourceWidth(_texture));
            Assert.AreEqual(50, UILayout.GetTextureSourceHeight(_texture));
            Assert.AreEqual(0, UILayout.GetTextureSourceWidth(null));
            Assert.AreEqual(0, UILayout.GetTextureSourceHeight(null));
            Assert.AreEqual(0, UILayout.ToSourceUnits(0));
            Assert.AreEqual(1, UILayout.ToSourceUnits(1));
        }

        [Test]
        public void CreateDragPreview_ValidGeometry_ReturnsPointerRelativePreview()
        {
            DragPreview preview = UILayout.CreateDragPreview(
                _texture,
                new RectInt(10, 20, 30, 40),
                17,
                29
            );

            Assert.IsNotNull(preview);
            Assert.AreSame(_texture, preview.Texture);
            Assert.AreEqual(30, preview.Width);
            Assert.AreEqual(40, preview.Height);
            Assert.AreEqual(7, preview.OffsetX);
            Assert.AreEqual(9, preview.OffsetY);
        }

        [Test]
        public void CreateDragPreview_MissingTextureOrGeometry_ReturnsNull()
        {
            Assert.IsNull(UILayout.CreateDragPreview(null, new RectInt(0, 0, 1, 1), 0, 0));
            Assert.IsNull(UILayout.CreateDragPreview(_texture, new RectInt(0, 0, 0, 1), 0, 0));
            Assert.IsNull(UILayout.CreateDragPreview(_texture, new RectInt(0, 0, 1, 0), 0, 0));
        }

        private static GameObject CreateTextObject(string objectName, out TextMeshProUGUI text)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );
            text = textObject.GetComponent<TextMeshProUGUI>();
            return textObject;
        }
    }
}
