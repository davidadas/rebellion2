using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class OverflowTextTooltipTests
    {
        private GameObject _parent;
        private GameObject _labelObject;
        private GameObject _hoverSurface;
        private TextMeshProUGUI _label;
        private OverflowTextTooltip _tooltip;
        private OverflowTextTooltipTrigger _trigger;

        /// <summary>
        /// Creates a bounded text label for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("Parent", typeof(RectTransform));
            UILayout.SetSourceRect(_parent.transform as RectTransform, 0, 0, 300, 100);
            _labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );
            _labelObject.transform.SetParent(_parent.transform, false);
            _label = _labelObject.GetComponent<TextMeshProUGUI>();
            _label.fontSize = 12;
            UILayout.SetSourceRect(_label.rectTransform, 10, 10, 40, 18);
            _hoverSurface = new GameObject("HoverSurface", typeof(RectTransform));
            _hoverSurface.transform.SetParent(_parent.transform, false);
            _tooltip = _labelObject.AddComponent<OverflowTextTooltip>();
            _tooltip.Configure(_label);
            _trigger = _hoverSurface.AddComponent<OverflowTextTooltipTrigger>();
            _trigger.Configure(_tooltip);
            UIComponentTestHelper.InvokeLifecycle(_tooltip, "Awake");
        }

        /// <summary>
        /// Destroys the generated hierarchy after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent);
        }

        [Test]
        public void Configure_BoundedText_ConfiguresEllipsisWithoutStealingPointerRaycasts()
        {
            Assert.AreEqual(TextWrappingModes.NoWrap, _label.textWrappingMode);
            Assert.AreEqual(TextOverflowModes.Ellipsis, _label.overflowMode);
            Assert.IsFalse(_label.raycastTarget);
        }

        [Test]
        public void Configure_TruncateMode_PreservesHardCutoff()
        {
            _tooltip.Configure(_label, TextOverflowModes.Truncate);

            Assert.AreEqual(TextWrappingModes.NoWrap, _label.textWrappingMode);
            Assert.AreEqual(TextOverflowModes.Truncate, _label.overflowMode);
            Assert.IsFalse(_label.raycastTarget);
        }

        [Test]
        public void AdvanceHover_OverflowingText_ShowsCompleteText()
        {
            _label.text = "A content pack name that cannot fit";

            _trigger.OnPointerEnter(new PointerEventData(null));
            _tooltip.AdvanceHover(float.PositiveInfinity);

            Transform tooltipRoot = _parent.transform.Find("LabelTooltip");
            Assert.IsNotNull(tooltipRoot);
            Assert.IsTrue(tooltipRoot.gameObject.activeSelf);
            Assert.AreEqual(Color.black, tooltipRoot.GetComponent<Image>().color);
            Assert.AreEqual(
                new Color32(255, 255, 225, 255),
                (Color32)tooltipRoot.Find("Background").GetComponent<Image>().color
            );
            Assert.AreEqual(14, UILayout.GetSourceRect(tooltipRoot as RectTransform).height);
            Assert.AreEqual(
                _label.text,
                tooltipRoot.Find("Text").GetComponent<TextMeshProUGUI>().text
            );
        }

        [Test]
        public void AdvanceHover_FittingText_DoesNotCreateTooltip()
        {
            _label.text = "A";

            _trigger.OnPointerEnter(new PointerEventData(null));
            _tooltip.AdvanceHover(Time.unscaledTime + HoverTooltip.HoverDelaySeconds);

            Assert.IsNull(_parent.transform.Find("LabelTooltip"));
        }

        [Test]
        public void EndHover_VisibleTooltip_HidesTooltip()
        {
            _label.text = "A content pack name that cannot fit";
            _trigger.OnPointerEnter(new PointerEventData(null));
            _tooltip.AdvanceHover(float.PositiveInfinity);

            _trigger.OnPointerExit(new PointerEventData(null));

            Assert.IsFalse(_parent.transform.Find("LabelTooltip").gameObject.activeSelf);
        }

        [Test]
        public void AdvanceHover_BeforeSharedDelay_DoesNotShowTooltip()
        {
            _label.text = "A content pack name that cannot fit";
            float hoverStartTime = Time.unscaledTime;

            _trigger.OnPointerEnter(new PointerEventData(null));
            _tooltip.AdvanceHover(hoverStartTime + HoverTooltip.HoverDelaySeconds - 0.01f);

            Assert.IsNull(_parent.transform.Find("LabelTooltip"));
        }

        [Test]
        public void BeginHover_AdjacentTriggers_ReuseOneTooltipPresentation()
        {
            _label.text = "A content pack name that cannot fit";
            GameObject adjacentSurface = new GameObject("AdjacentSurface", typeof(RectTransform));
            adjacentSurface.transform.SetParent(_parent.transform, false);
            OverflowTextTooltipTrigger adjacentTrigger =
                adjacentSurface.AddComponent<OverflowTextTooltipTrigger>();
            adjacentTrigger.Configure(_tooltip);

            _trigger.OnPointerEnter(new PointerEventData(null));
            _tooltip.AdvanceHover(float.PositiveInfinity);
            _trigger.OnPointerExit(new PointerEventData(null));
            adjacentTrigger.OnPointerEnter(new PointerEventData(null));
            _tooltip.AdvanceHover(float.PositiveInfinity);

            Assert.AreEqual(
                1,
                _parent
                    .GetComponentsInChildren<RectTransform>(true)
                    .Count(rect => rect.name == "LabelTooltip")
            );
        }

        [Test]
        public void AdvanceHover_ValueWiderThanParent_WrapsWithoutTruncation()
        {
            _label.text = string.Join(" ", Enumerable.Repeat("Galactic", 30));

            _trigger.OnPointerEnter(new PointerEventData(null));
            _tooltip.AdvanceHover(float.PositiveInfinity);

            TextMeshProUGUI tooltipText = _parent
                .transform.Find("LabelTooltip/Text")
                .GetComponent<TextMeshProUGUI>();
            tooltipText.ForceMeshUpdate(true, true);
            Assert.AreEqual(TextWrappingModes.Normal, tooltipText.textWrappingMode);
            Assert.AreEqual(TextOverflowModes.Overflow, tooltipText.overflowMode);
            Assert.Greater(UILayout.GetSourceRect(tooltipText.rectTransform).height, 12);
            Assert.IsFalse(tooltipText.isTextOverflowing);
        }

        [Test]
        public void AdvanceHover_FittingValueChangesToOverflowing_RevealsUpdatedValueAfterDelay()
        {
            _label.text = "A";
            float hoverStartTime = Time.unscaledTime;
            _trigger.OnPointerEnter(new PointerEventData(null));
            _tooltip.AdvanceHover(hoverStartTime + HoverTooltip.HoverDelaySeconds);

            _label.text = "A content pack name that cannot fit";
            float valueChangedTime = hoverStartTime + 1f;
            _tooltip.AdvanceHover(valueChangedTime);

            Assert.IsNull(_parent.transform.Find("LabelTooltip"));

            _tooltip.AdvanceHover(valueChangedTime + HoverTooltip.HoverDelaySeconds);

            Assert.AreEqual(
                _label.text,
                _parent.transform.Find("LabelTooltip/Text").GetComponent<TextMeshProUGUI>().text
            );
        }

        [Test]
        public void AdvanceHover_OverflowingValueChangesToFitting_HidesVisibleTooltip()
        {
            _label.text = "A content pack name that cannot fit";
            _trigger.OnPointerEnter(new PointerEventData(null));
            _tooltip.AdvanceHover(float.PositiveInfinity);
            Transform tooltipRoot = _parent.transform.Find("LabelTooltip");

            _label.text = "A";
            _tooltip.AdvanceHover(float.PositiveInfinity);

            Assert.IsFalse(tooltipRoot.gameObject.activeSelf);
        }

        [Test]
        public void AdvanceHover_OverflowingValueChangesToOverflowing_RevealsUpdatedValueAfterDelay()
        {
            _label.text = "An initial content pack name that cannot fit";
            float hoverStartTime = Time.unscaledTime;
            _trigger.OnPointerEnter(new PointerEventData(null));
            _tooltip.AdvanceHover(hoverStartTime + HoverTooltip.HoverDelaySeconds);
            Transform tooltipRoot = _parent.transform.Find("LabelTooltip");

            _label.text = "A replacement content pack name that also cannot fit";
            float valueChangedTime = hoverStartTime + 1f;
            _tooltip.AdvanceHover(valueChangedTime);

            Assert.IsFalse(tooltipRoot.gameObject.activeSelf);

            _tooltip.AdvanceHover(valueChangedTime + HoverTooltip.HoverDelaySeconds);

            Assert.IsTrue(tooltipRoot.gameObject.activeSelf);
            Assert.AreEqual(
                _label.text,
                tooltipRoot.Find("Text").GetComponent<TextMeshProUGUI>().text
            );
        }
    }
}
