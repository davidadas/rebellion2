using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class HoverTooltipTests
    {
        [TestCase(8)]
        [TestCase(15)]
        public void Advance_WrappedContentAtConfiguredFontSize_ContainsEveryLine(int fontSize)
        {
            GameObject parent = new("Parent", typeof(RectTransform));
            RectTransform surface = (RectTransform)parent.transform;
            UILayout.SetSourceRect(surface, 0, 0, 400, 300);
            HoverTooltip tooltip = new(surface, "Help", 180, true, fontSize);
            try
            {
                tooltip.Begin(
                    () =>
                        new(
                            "A longer field description that wraps onto several lines.",
                            new(150, 20, 1, 1)
                        ),
                    0
                );
                tooltip.Advance(HoverTooltip.HoverDelaySeconds);
                TextMeshProUGUI label = parent.GetComponentInChildren<TextMeshProUGUI>();
                Assert.IsTrue(tooltip.IsVisible);
                Assert.AreEqual(fontSize, label.fontSize);
                Assert.LessOrEqual(
                    label
                        .GetPreferredValues(
                            label.text,
                            label.rectTransform.rect.width,
                            float.PositiveInfinity
                        )
                        .y,
                    label.rectTransform.rect.height
                );
                Assert.LessOrEqual(
                    label.transform.parent.GetComponent<RectTransform>().rect.width,
                    180
                );
            }
            finally
            {
                tooltip.Destroy();
                Object.DestroyImmediate(parent);
            }
        }
    }
}
