using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class SelectableListRowViewTests
{
    [Test]
    public void ConfigureSelectableRowEnablesRenderedRow()
    {
        GameObject rowObject = new GameObject(
            "Row",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(TestSelectableListRowView)
        );
        try
        {
            RawImage hitArea = rowObject.GetComponent<RawImage>();
            TestSelectableListRowView row = rowObject.GetComponent<TestSelectableListRowView>();
            row.enabled = false;
            hitArea.enabled = false;
            hitArea.raycastTarget = false;
            hitArea.canvasRenderer.cullTransparentMesh = true;

            row.Configure(7, hitArea);

            Assert.IsTrue(row.enabled);
            Assert.AreEqual(7, row.Index);
            Assert.IsTrue(hitArea.enabled);
            Assert.IsTrue(hitArea.raycastTarget);
            Assert.IsFalse(hitArea.canvasRenderer.cullTransparentMesh);
        }
        finally
        {
            Object.DestroyImmediate(rowObject);
        }
    }

    private sealed class TestSelectableListRowView : SelectableListRowView
    {
        public void Configure(int index, RawImage hitArea)
        {
            ConfigureSelectableRow(index, hitArea);
        }
    }
}
