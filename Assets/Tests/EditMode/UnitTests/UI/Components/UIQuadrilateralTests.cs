using System;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class UIQuadrilateralTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ToMatrix_PerspectiveSurface_MapsEachCorner(int index)
        {
            UIQuadrilateral surface = CreateSurface();

            Vector2 result = surface.ToMatrix().MultiplyPoint(UIQuadrilateral.Unit[index]);

            Assert.That(Vector2.Distance(result, surface[index]), Is.LessThan(0.00001f));
        }

        [Test]
        public void ToMatrix_PerspectiveSurface_PreservesStraightLines()
        {
            Matrix4x4 projection = CreateSurface().ToMatrix();
            Vector2 first = projection.MultiplyPoint(new Vector3(0, 0.4f));
            Vector2 middle = projection.MultiplyPoint(new Vector3(0.5f, 0.4f));
            Vector2 last = projection.MultiplyPoint(new Vector3(1, 0.4f));

            Vector2 line = last - first;
            Vector2 offset = middle - first;
            Assert.That(Mathf.Abs(line.x * offset.y - line.y * offset.x), Is.LessThan(0.00001f));
        }

        [Test]
        public void ToMatrix_ProjectedPoint_InverseRestoresPosition()
        {
            Matrix4x4 projection = CreateSurface().ToMatrix();
            Vector3 point = new Vector3(0.31f, 0.62f);

            Vector3 restored = projection.inverse.MultiplyPoint(projection.MultiplyPoint(point));

            Assert.That(Vector3.Distance(restored, point), Is.LessThan(0.00001f));
        }

        [Test]
        public void Contains_PointInBoundingRectangleOutsideSurface_ReturnsFalse()
        {
            UIQuadrilateral surface = CreateSurface();
            Vector2 point = new Vector2(0.95f, 0.95f);

            Assert.IsTrue(surface.Bounds.Contains(point));
            Assert.IsFalse(surface.Contains(point));
        }

        [Test]
        public void Contains_InteriorPoint_ReturnsTrue()
        {
            Assert.IsTrue(CreateSurface().Contains(new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void Contains_BoundaryPoint_ReturnsTrue()
        {
            Assert.IsTrue(UIQuadrilateral.Unit.Contains(Vector2.up));
        }

        [Test]
        public void Contains_NonfinitePoint_ReturnsFalse()
        {
            Assert.IsFalse(UIQuadrilateral.Unit.Contains(new Vector2(float.NaN, 0.5f)));
            Assert.IsFalse(
                UIQuadrilateral.Unit.Contains(new Vector2(float.PositiveInfinity, 0.5f))
            );
        }

        [Test]
        public void IsValid_CrossedCorners_ReturnsFalse()
        {
            UIQuadrilateral surface = new UIQuadrilateral(
                Vector2.zero,
                Vector2.one,
                Vector2.up,
                Vector2.right
            );

            Assert.IsFalse(surface.IsValid);
        }

        [Test]
        public void IsValid_CollapsedCorners_ReturnsFalse()
        {
            Assert.IsFalse(UIQuadrilateral.Unit.WithCorner(0, Vector2.up).IsValid);
        }

        [Test]
        public void IsValid_NonfiniteCorner_ReturnsFalse()
        {
            Assert.IsFalse(UIQuadrilateral.Unit.WithCorner(0, new Vector2(float.NaN, 0)).IsValid);
        }

        [Test]
        public void ToMatrix_InvalidSurface_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => default(UIQuadrilateral).ToMatrix());
        }

        [Test]
        public void WithCorner_OneCornerChanged_PreservesOtherCorners()
        {
            UIQuadrilateral changed = UIQuadrilateral.Unit.WithCorner(1, new Vector2(0.2f, 0.9f));

            Assert.AreEqual(new Vector2(0.2f, 0.9f), changed[1]);
            Assert.AreEqual(UIQuadrilateral.Unit[0], changed[0]);
            Assert.AreEqual(UIQuadrilateral.Unit[2], changed[2]);
            Assert.AreEqual(UIQuadrilateral.Unit[3], changed[3]);
        }

        /// <summary>
        /// Creates a convex surface with a projective upper edge.
        /// </summary>
        /// <returns>The test surface.</returns>
        private static UIQuadrilateral CreateSurface()
        {
            return new UIQuadrilateral(
                new Vector2(0.15f, 0),
                Vector2.up,
                new Vector2(0.7f, 0.8f),
                Vector2.right
            );
        }
    }
}
