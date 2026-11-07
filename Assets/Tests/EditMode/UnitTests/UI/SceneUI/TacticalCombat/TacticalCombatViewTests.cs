using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatViewTests
    {
        private GameObject _prefab;

        /// <summary>
        /// Opens the authored combat view without constructing a battle or resolving private content.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _prefab = PrefabUtility.LoadPrefabContents(
                "Assets/Prefabs/UI/StrategyView/TacticalCombat.prefab"
            );
        }

        /// <summary>
        /// Releases the isolated prefab contents after each authored-layout assertion.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_prefab != null)
                PrefabUtility.UnloadPrefabContents(_prefab);
        }

        [Test]
        public void AuthoredPrefab_WideOrTallDisplay_PreservesCompleteCenteredReferenceSurface()
        {
            CanvasScaler scaler = _prefab.GetComponentInChildren<CanvasScaler>(true);
            RectTransform frame = _prefab.transform.Find("Canvas/Frame") as RectTransform;

            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode);
            Assert.AreEqual(new Vector2(640, 480), scaler.referenceResolution);
            Assert.IsNotNull(frame);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), frame.anchorMin);
            Assert.AreEqual(frame.anchorMin, frame.anchorMax);
            Assert.AreEqual(frame.anchorMin, frame.pivot);
            Assert.AreEqual(Vector2.zero, frame.anchoredPosition);
            Assert.AreEqual(scaler.referenceResolution, frame.sizeDelta);
            Assert.IsNull(frame.GetComponent<AspectRatioFitter>());
        }

        [Test]
        public void AuthoredPrefab_ViewportAndRightPanel_KeepTheirReferencePixelExtents()
        {
            RectTransform frame = _prefab.transform.Find("Canvas/Frame") as RectTransform;
            RectTransform viewport = frame.Find("Viewport") as RectTransform;
            RectTransform panel = frame.Find("Right panel") as RectTransform;

            Assert.AreEqual(new Vector2(439, 437), viewport.sizeDelta);
            Assert.AreEqual(new Vector2(17, -30), viewport.anchoredPosition);
            Assert.AreEqual(new Vector2(149, 236), panel.sizeDelta);
            Assert.AreEqual(new Vector2(482, -28), panel.anchoredPosition);
            Assert.LessOrEqual(panel.anchoredPosition.x + panel.sizeDelta.x, frame.sizeDelta.x);
            Assert.LessOrEqual(
                -viewport.anchoredPosition.y + viewport.sizeDelta.y,
                frame.sizeDelta.y
            );
        }

        [Test]
        public void AuthoredPrefab_InspectionDetails_OwnsFiveSystemRegionsAndClippedStatusRows()
        {
            Transform panel = _prefab.transform.Find("Canvas/Frame/Right panel");
            string[] rows = { "Name", "Assignment", "Orders", "Tactics", "Fighter counts" };
            foreach (string row in rows)
            {
                TMPro.TextMeshProUGUI label = panel.Find(row).GetComponent<TMPro.TextMeshProUGUI>();
                Assert.AreEqual(12, label.fontSize);
                Assert.AreEqual(TMPro.FontStyles.Normal, label.fontStyle);
                Assert.IsNotNull(label.GetComponent<RectMask2D>());
            }
            for (int index = 0; index < 5; index++)
            {
                RectTransform icon = panel.Find("System " + index) as RectTransform;
                Assert.AreEqual(new Vector2(10 + index * 27, -103), icon.anchoredPosition);
                Assert.AreEqual(new Vector2(22, 15), icon.sizeDelta);
                Assert.IsNotNull(icon.GetComponent<HoverTooltipRegion>());
            }
        }
    }
}
