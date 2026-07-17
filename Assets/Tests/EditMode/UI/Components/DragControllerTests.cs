using System;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class DragControllerTests
    {
        private Texture2D _texture;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(1, 1);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        [Test]
        public void Constructor_NegativeStartDistance_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DragController(-1));
        }

        [Test]
        public void DragRequest_NullSource_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DragRequest(null));
        }

        [Test]
        public void DragRequest_Source_StoresSource()
        {
            object source = new object();

            DragRequest request = new DragRequest(source);

            Assert.AreSame(source, request.Source);
        }

        [Test]
        public void DragPreview_Constructor_StoresPresentationValues()
        {
            DragPreview preview = new DragPreview(_texture, 10, 20, 3, 4);

            Assert.AreSame(_texture, preview.Texture);
            Assert.AreEqual(10, preview.Width);
            Assert.AreEqual(20, preview.Height);
            Assert.AreEqual(3, preview.OffsetX);
            Assert.AreEqual(4, preview.OffsetY);
        }

        [Test]
        public void StartCandidate_NullRequest_ThrowsArgumentNullException()
        {
            DragController controller = new DragController(5);

            Assert.Throws<ArgumentNullException>(() => controller.StartCandidate(null, 0, 0));
        }

        [Test]
        public void HasCandidateDragStarted_DistanceBelowThreshold_ReturnsFalse()
        {
            DragController controller = new DragController(5);
            DragRequest request = new DragRequest(new object());
            controller.StartCandidate(request, 10, 20);

            bool started = controller.HasCandidateDragStarted(13, 23);

            Assert.IsFalse(started);
            Assert.IsTrue(controller.HasCandidate);
            Assert.AreSame(request, controller.CandidateRequest);
        }

        [Test]
        public void HasCandidateDragStarted_DistanceAtThreshold_ReturnsTrue()
        {
            DragController controller = new DragController(5);
            controller.StartCandidate(new DragRequest(new object()), 10, 20);

            bool started = controller.HasCandidateDragStarted(13, 24);

            Assert.IsTrue(started);
        }

        [Test]
        public void HasCandidateDragStarted_MissingCandidate_ReturnsFalse()
        {
            DragController controller = new DragController(0);

            bool started = controller.HasCandidateDragStarted(0, 0);

            Assert.IsFalse(started);
        }

        [Test]
        public void BeginDrag_MissingCandidate_ThrowsInvalidOperationException()
        {
            DragController controller = new DragController(0);
            DragPreview preview = new DragPreview(_texture, 10, 20, 3, 4);

            Assert.Throws<InvalidOperationException>(() => controller.BeginDrag(preview, 0, 0));
        }

        [Test]
        public void BeginDrag_NullPreview_ThrowsArgumentNullException()
        {
            DragController controller = new DragController(0);
            controller.StartCandidate(new DragRequest(new object()), 0, 0);

            Assert.Throws<ArgumentNullException>(() => controller.BeginDrag(null, 0, 0));
        }

        [Test]
        public void BeginMovePreviewAndEnd_ActiveDrag_TracksCompleteFlow()
        {
            object source = new object();
            DragController controller = new DragController(5);
            DragRequest request = new DragRequest(source);
            DragPreview preview = new DragPreview(_texture, 10, 20, 3, 4);
            controller.StartCandidate(request, 1, 2);

            controller.BeginDrag(preview, 10, 20);
            bool moved = controller.Move(30, 40);
            bool hasPreview = controller.TryGetPreview(
                out Texture texture,
                out int x,
                out int y,
                out int width,
                out int height
            );
            bool ended = controller.End(50, 60, out DragRequest completedRequest);

            Assert.IsTrue(moved);
            Assert.IsTrue(hasPreview);
            Assert.AreSame(_texture, texture);
            Assert.AreEqual(27, x);
            Assert.AreEqual(36, y);
            Assert.AreEqual(10, width);
            Assert.AreEqual(20, height);
            Assert.IsTrue(ended);
            Assert.AreSame(request, completedRequest);
            Assert.IsFalse(controller.HasCandidate);
            Assert.IsFalse(controller.IsDragging);
            Assert.IsNull(controller.ActiveRequest);
        }

        [Test]
        public void MoveAndEnd_MissingActiveDrag_ReturnFalse()
        {
            DragController controller = new DragController(0);

            bool moved = controller.Move(1, 2);
            bool ended = controller.End(1, 2, out DragRequest request);

            Assert.IsFalse(moved);
            Assert.IsFalse(ended);
            Assert.IsNull(request);
        }

        [Test]
        public void TryGetPreview_MissingActiveDrag_ReturnsClearedOutputs()
        {
            DragController controller = new DragController(0);

            bool hasPreview = controller.TryGetPreview(
                out Texture texture,
                out int x,
                out int y,
                out int width,
                out int height
            );

            Assert.IsFalse(hasPreview);
            Assert.IsNull(texture);
            Assert.AreEqual(0, x);
            Assert.AreEqual(0, y);
            Assert.AreEqual(0, width);
            Assert.AreEqual(0, height);
        }

        [Test]
        public void TryGetPreview_NullPreviewTexture_ReturnsFalseWithGeometry()
        {
            DragController controller = new DragController(0);
            controller.StartCandidate(new DragRequest(new object()), 0, 0);
            controller.BeginDrag(new DragPreview(null, 10, 20, 3, 4), 30, 40);

            bool hasPreview = controller.TryGetPreview(
                out Texture texture,
                out int x,
                out int y,
                out int width,
                out int height
            );

            Assert.IsFalse(hasPreview);
            Assert.IsNull(texture);
            Assert.AreEqual(27, x);
            Assert.AreEqual(36, y);
            Assert.AreEqual(10, width);
            Assert.AreEqual(20, height);
        }

        [Test]
        public void ClearSource_MatchingCandidate_ClearsCandidateOnly()
        {
            object source = new object();
            DragController controller = new DragController(0);
            controller.StartCandidate(new DragRequest(source), 0, 0);

            controller.ClearSource(source);

            Assert.IsFalse(controller.HasCandidate);
            Assert.IsFalse(controller.IsDragging);
        }

        [Test]
        public void ClearSource_MatchingActiveDrag_ClearsActiveDragOnly()
        {
            object source = new object();
            DragController controller = new DragController(0);
            controller.StartCandidate(new DragRequest(source), 0, 0);
            controller.BeginDrag(new DragPreview(_texture, 1, 1, 0, 0), 0, 0);

            controller.ClearSource(source);

            Assert.IsFalse(controller.HasCandidate);
            Assert.IsFalse(controller.IsDragging);
        }

        [Test]
        public void ClearSource_NonmatchingOrNullSource_PreservesState()
        {
            object source = new object();
            DragController controller = new DragController(0);
            controller.StartCandidate(new DragRequest(source), 0, 0);

            controller.ClearSource(null);
            controller.ClearSource(new object());

            Assert.IsTrue(controller.HasCandidate);
        }

        [Test]
        public void Clear_CandidateAndActiveState_ClearsBoth()
        {
            DragController controller = new DragController(0);
            controller.StartCandidate(new DragRequest(new object()), 0, 0);
            controller.BeginDrag(new DragPreview(_texture, 1, 1, 0, 0), 0, 0);
            controller.StartCandidate(new DragRequest(new object()), 0, 0);

            controller.Clear();

            Assert.IsFalse(controller.HasCandidate);
            Assert.IsFalse(controller.IsDragging);
        }
    }
}
