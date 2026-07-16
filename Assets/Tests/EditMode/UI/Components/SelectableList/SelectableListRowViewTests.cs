using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components.SelectableList
{
    [TestFixture]
    public class SelectableListRowViewTests
    {
        private RawImage _hitArea;
        private TestSelectableListRowView _row;
        private GameObject _rowObject;

        [SetUp]
        public void SetUp()
        {
            _rowObject = new GameObject(
                "Row",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(TestSelectableListRowView)
            );
            _hitArea = _rowObject.GetComponent<RawImage>();
            _row = _rowObject.GetComponent<TestSelectableListRowView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rowObject);
        }

        [Test]
        public void ConfigureSelectableRow_DisabledRow_EnablesRowAndHitArea()
        {
            _row.enabled = false;
            _hitArea.enabled = false;
            _hitArea.raycastTarget = false;
            _hitArea.canvasRenderer.cullTransparentMesh = true;

            _row.Configure(7, _hitArea);

            Assert.IsTrue(_row.enabled);
            Assert.AreEqual(7, _row.Index);
            Assert.IsTrue(_hitArea.enabled);
            Assert.IsTrue(_hitArea.raycastTarget);
            Assert.IsFalse(_hitArea.canvasRenderer.cullTransparentMesh);
        }

        private sealed class TestSelectableListRowView : SelectableListRowView
        {
            public void Configure(int index, RawImage hitArea)
            {
                ConfigureSelectableRow(index, hitArea);
            }
        }
    }
}
