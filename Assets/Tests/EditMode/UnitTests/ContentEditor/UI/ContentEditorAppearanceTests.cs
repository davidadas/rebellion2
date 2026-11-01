using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorAppearanceTests
    {
        private ContentPackEditorView view;
        private ContentEditorAppearance appearance;

        /// <summary>
        /// Creates the authored window without opening a project or rendering forms.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            view = UIComponentTestHelper.InstantiatePrefabComponent<ContentPackEditorView>(
                "Assets/Prefabs/UI/ContentPackEditor/ContentPackEditorRoot.prefab"
            );
            appearance = view.GetComponent<ContentEditorAppearance>();
            view.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)view.transform).sizeDelta = new Vector2(1600, 900);
            view.Initialize(
                new[] { TestContent.Pack.Definition },
                TestContent.Pack.ContentRootPath
            );
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Releases the isolated appearance controls.
        /// </summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(view.gameObject);

        [TestCase(ContentEditorColorMode.Dark, "Dark side")]
        [TestCase(ContentEditorColorMode.Light, "Light side")]
        [TestCase(ContentEditorColorMode.Empire, "Empire")]
        [TestCase(ContentEditorColorMode.Rebels, "Rebels")]
        [TestCase(ContentEditorColorMode.Dagobah, "Dagobah")]
        public void SetColorMode_AppearanceMenu_CentersAReadableCheckmarkInTheSelectedRow(
            ContentEditorColorMode mode,
            string expectedLabel
        )
        {
            view.SetColorMode(mode);
            string[] modes = { "Dark side", "Light side", "Empire", "Rebels", "Dagobah" };
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "View")
                .onClick.Invoke();
            view.GetComponentsInChildren<ContextMenuCommandView>()
                .Single(command =>
                    command.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "Theme")
                )
                .OnPointerEnter(new PointerEventData(EventSystem.current));
            view.GetComponentInChildren<ContextMenuView>(true).RenderCurrent();
            RawImage mark = view.GetComponentsInChildren<ContextMenuCommandView>()
                .Where(command =>
                    command
                        .GetComponentsInChildren<TMP_Text>()
                        .Any(label => modes.Contains(label.text))
                )
                .SelectMany(command => command.GetComponentsInChildren<RawImage>())
                .Single(image => image.texture != null);
            RectTransform row =
                mark.GetComponentInParent<ContextMenuCommandView>().transform as RectTransform;
            CollectionAssert.Contains(
                row.GetComponentsInChildren<TMP_Text>().Select(label => label.text).ToArray(),
                expectedLabel
            );
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                row,
                mark.rectTransform
            );
            Assert.That(bounds.size.x, Is.InRange(12f, 20f));
            Assert.That(bounds.size.y, Is.InRange(12f, 20f));
            Assert.That(bounds.center.y, Is.EqualTo(row.rect.center.y).Within(1f));
            RectTransform gutter = (RectTransform)mark.transform.parent;
            Bounds gutterBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                gutter,
                mark.rectTransform
            );
            Assert.That(gutterBounds.center.x, Is.EqualTo(gutter.rect.center.x).Within(0.5f));
            Assert.GreaterOrEqual(gutterBounds.min.x - gutter.rect.xMin, 6);
            Assert.GreaterOrEqual(gutter.rect.xMax - gutterBounds.max.x, 6);
            TMP_Dropdown selector = view.GetComponentsInChildren<TMP_Dropdown>(true).First();
            Image selectedGlyph = (Image)
                selector.template.GetComponentInChildren<Toggle>(true).graphic;
            Assert.AreSame(selectedGlyph.sprite.texture, mark.texture);
            RawImage checkboxGlyph = view.GetComponentsInChildren<UICheckboxView>(true)
                .First()
                .GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "Tick");
            Assert.AreSame(mark.texture, checkboxGlyph.texture);
            Assert.AreEqual(mark.uvRect, checkboxGlyph.uvRect);
            if (
                mode
                is ContentEditorColorMode.Dark
                    or ContentEditorColorMode.Empire
                    or ContentEditorColorMode.Dagobah
            )
            {
                Assert.AreEqual(Color.white, mark.color);
                Assert.AreEqual(Color.white, checkboxGlyph.color);
                Assert.AreEqual(Color.white, selectedGlyph.color);
            }
            RectTransform submenu = (RectTransform)row.parent;
            RectTransform parentPanel = (RectTransform)
                view.GetComponentsInChildren<ContextMenuCommandView>()
                    .Single(command =>
                        command
                            .GetComponentsInChildren<TMP_Text>()
                            .Any(label => label.text == "Theme")
                    )
                    .transform.parent;
            Assert.Greater(submenu.position.x, parentPanel.position.x);
        }

        [TestCase(ContentEditorColorMode.Dark)]
        [TestCase(ContentEditorColorMode.Light)]
        [TestCase(ContentEditorColorMode.Empire)]
        [TestCase(ContentEditorColorMode.Rebels)]
        [TestCase(ContentEditorColorMode.Dagobah)]
        public void SetMode_Scrollbars_KeepOpaqueContrastingHandlesWithoutContentTextures(
            ContentEditorColorMode mode
        )
        {
            appearance.SetMode(mode);
            Scrollbar[] bars = appearance.GetComponentsInChildren<Scrollbar>(true);
            Assert.IsNotEmpty(bars);
            foreach (Scrollbar bar in bars)
            {
                Graphic handle = bar.targetGraphic;
                Assert.AreEqual(1, handle.color.a);
                Assert.AreNotEqual(bar.GetComponent<Image>().color, handle.color);
                if (handle is RawImage image)
                {
                    Assert.IsNull(image.texture);
                    Assert.IsNull(image.GetComponent<ContentTextureBinding>());
                }
            }
        }
    }
}
