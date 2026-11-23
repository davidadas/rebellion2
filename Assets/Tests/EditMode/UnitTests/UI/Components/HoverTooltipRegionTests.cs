using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class HoverTooltipRegionTests
    {
        [Test]
        public void OnPointerEnter_OrdinaryRegion_UsesSharedDelayTextAndRenderer()
        {
            GameObject root = new("Tooltip owner", typeof(RectTransform));
            GameObject regionObject = new(
                "Region",
                typeof(RectTransform),
                typeof(HoverTooltipRegion)
            );
            regionObject.transform.SetParent(root.transform, false);
            HoverTooltip tooltip = new((RectTransform)root.transform, "Shared help");
            HoverTooltipRegion region = regionObject.GetComponent<HoverTooltipRegion>();
            try
            {
                region.Configure(tooltip, "Shield Recharge", new RectInt(10, 10, 22, 15));
                float entered = Time.unscaledTime;

                region.OnPointerEnter(null);
                tooltip.Advance(entered + HoverTooltip.HoverDelaySeconds - 0.01f);
                Assert.IsFalse(tooltip.IsVisible);
                tooltip.Advance(entered + HoverTooltip.HoverDelaySeconds);

                Assert.IsTrue(tooltip.IsVisible);
                Assert.AreEqual(
                    "Shield Recharge",
                    root.GetComponentInChildren<TextMeshProUGUI>().text
                );
            }
            finally
            {
                tooltip.Destroy();
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OnPointerExit_ActiveRegion_CancelsSharedReveal()
        {
            GameObject root = new("Tooltip owner", typeof(RectTransform));
            GameObject regionObject = new(
                "Region",
                typeof(RectTransform),
                typeof(HoverTooltipRegion)
            );
            regionObject.transform.SetParent(root.transform, false);
            HoverTooltip tooltip = new((RectTransform)root.transform, "Shared help");
            HoverTooltipRegion region = regionObject.GetComponent<HoverTooltipRegion>();
            try
            {
                region.Configure(tooltip, "Shield Recharge", new RectInt(10, 10, 22, 15));
                region.OnPointerEnter(null);
                tooltip.Advance(Time.unscaledTime + HoverTooltip.HoverDelaySeconds);
                Assert.IsTrue(tooltip.IsVisible);

                region.OnPointerExit(null);
                tooltip.Advance(Time.unscaledTime + 2 * HoverTooltip.HoverDelaySeconds);

                Assert.IsFalse(tooltip.IsVisible);
            }
            finally
            {
                tooltip.Destroy();
                Object.DestroyImmediate(root);
            }
        }
    }
}
