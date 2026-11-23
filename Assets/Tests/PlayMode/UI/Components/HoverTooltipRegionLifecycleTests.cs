using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class HoverTooltipRegionLifecycleTests
    {
        [UnityTest]
        public IEnumerator OnPointerEnter_RegionDisabled_CancelsSharedReveal()
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

                regionObject.SetActive(false);
                tooltip.Advance(Time.unscaledTime + 2 * HoverTooltip.HoverDelaySeconds);

                Assert.IsFalse(tooltip.IsVisible);
            }
            finally
            {
                tooltip.Destroy();
                Object.Destroy(root);
            }
            yield return null;
        }
    }
}
