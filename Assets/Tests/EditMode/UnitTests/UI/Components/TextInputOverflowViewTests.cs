using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class TextInputOverflowViewTests
    {
        private TMP_InputField input;
        private TextInputOverflowView view;

        /// <summary>
        /// Creates a narrow shared input to exercise overflow and rebinding.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            input = UIComponentTestHelper.InstantiatePrefabComponent<TMP_InputField>(
                "Assets/Prefabs/UI/Common/TextInput.prefab"
            );
            ((RectTransform)input.transform).sizeDelta = new Vector2(130, 34);
            input.textComponent.fontSize = 16;
            input.textComponent.enableAutoSizing = false;
            input.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
            input.lineType = TMP_InputField.LineType.SingleLine;
            view = input.gameObject.AddComponent<TextInputOverflowView>();
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Releases the fixture-owned input.
        /// </summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(input.gameObject);

        [TestCase(TextAlignmentOptions.MidlineLeft)]
        [TestCase(TextAlignmentOptions.MidlineRight)]
        [TestCase(TextAlignmentOptions.Midline)]
        public void ShowBeginning_OverflowingValue_RevealsItsPrefix(TextAlignmentOptions alignment)
        {
            input.textComponent.alignment = alignment;
            input.SetTextWithoutNotify("1234567890123456789012345678901234567890");
            input.textComponent.rectTransform.anchoredPosition = new Vector2(-200, 0);

            view.ShowBeginning();

            Vector3 start = input.textComponent.rectTransform.TransformPoint(
                new Vector3(input.textComponent.textInfo.characterInfo[0].origin, 0)
            );
            Assert.AreEqual(
                input.textViewport.rect.xMin,
                input.textViewport.InverseTransformPoint(start).x,
                0.1f
            );
            Assert.AreEqual(0, input.caretPosition);
            Assert.AreEqual(alignment, input.textComponent.alignment);
        }

        [Test]
        public void ShowBeginning_MultilineInputWithScrollbar_PreservesTheAuthorsScrollPosition()
        {
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            GameObject root = new(
                "Scrollbar",
                typeof(RectTransform),
                typeof(UnityEngine.UI.Scrollbar)
            );
            root.transform.SetParent(input.transform, false);
            input.verticalScrollbar = root.GetComponent<UnityEngine.UI.Scrollbar>();
            input.textComponent.rectTransform.anchoredPosition = new Vector2(0, 130);

            view.ShowBeginning();

            Assert.AreEqual(130, input.textComponent.rectTransform.anchoredPosition.y);
        }

        [Test]
        public void ShowBeginning_RebindingToShortValue_RetainsItsAuthoredAlignment()
        {
            input.textComponent.alignment = TextAlignmentOptions.MidlineRight;
            input.SetTextWithoutNotify(new string('9', 60));
            view.ShowBeginning();
            input.SetTextWithoutNotify("42");

            view.ShowBeginning();

            Assert.AreEqual(Vector2.zero, input.textComponent.rectTransform.anchoredPosition);
            Assert.AreEqual(TextAlignmentOptions.MidlineRight, input.textComponent.alignment);
            Assert.AreEqual("42", input.text);
        }
    }
}
