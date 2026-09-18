using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Targeting
{
    [TestFixture]
    public class StrategyWindowTargetingSourceTests
    {
        private GameObject _windowObject;

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_windowObject != null)
                Object.DestroyImmediate(_windowObject);
        }

        /// <summary>
        /// Verifies constructor source items change preserves snapshot.
        /// </summary>
        [Test]
        public void Constructor_SourceItemsChange_PreservesSnapshot()
        {
            UIWindow window = CreateWindow();
            Officer officer = new Officer();
            List<ISceneNode> items = new List<ISceneNode> { officer };

            StrategyWindowTargetingSource source = new StrategyWindowTargetingSource(
                window,
                StrategyMenuAction.Move,
                12,
                34,
                items
            );
            items.Clear();

            Assert.AreEqual(1, source.Items.Count);
            Assert.AreSame(officer, source.Items[0]);
        }

        /// <summary>
        /// Verifies constructor null items normalizes to empty list.
        /// </summary>
        [Test]
        public void Constructor_NullItems_NormalizesToEmptyList()
        {
            StrategyWindowTargetingSource source = new StrategyWindowTargetingSource(
                null,
                StrategyMenuAction.Status,
                0,
                0,
                null
            );

            Assert.IsNotNull(source.Items);
            Assert.IsEmpty(source.Items);
        }

        [TestCase(StrategyMenuAction.CreateMission, "Select mission target")]
        [TestCase(StrategyMenuAction.Destination, "Select destination")]
        [TestCase(StrategyMenuAction.Move, "Select move destination")]
        [TestCase(StrategyMenuAction.MoveConfirm, "Select move destination")]
        [TestCase(
            StrategyMenuAction.WaypointMove,
            "Select waypoints; press Enter to move or Escape to undo"
        )]
        [TestCase(StrategyMenuAction.Status, "Select target")]
        public void GetPrompt_Action_ReturnsExpectedPrompt(
            StrategyMenuAction action,
            string expectedPrompt
        )
        {
            string prompt = StrategyWindowTargetingSource.GetPrompt(action);

            Assert.AreEqual(expectedPrompt, prompt);
        }

        /// <summary>
        /// Verifies try append waypoint valid identifiers preserves selection order.
        /// </summary>
        [Test]
        public void TryAppendWaypoint_ValidIdentifiers_PreservesSelectionOrder()
        {
            StrategyWindowTargetingSource source = new StrategyWindowTargetingSource(
                null,
                StrategyMenuAction.WaypointMove,
                0,
                0,
                null
            );

            bool firstAppended = source.TryAppendWaypoint("first");
            bool secondAppended = source.TryAppendWaypoint("second");

            Assert.IsTrue(firstAppended);
            Assert.IsTrue(secondAppended);
            CollectionAssert.AreEqual(new[] { "first", "second" }, source.WaypointPlanetIds);
        }

        /// <summary>
        /// Verifies try remove last waypoint multiple waypoints removes newest waypoint.
        /// </summary>
        [Test]
        public void TryRemoveLastWaypoint_MultipleWaypoints_RemovesNewestWaypoint()
        {
            StrategyWindowTargetingSource source = new StrategyWindowTargetingSource(
                null,
                StrategyMenuAction.WaypointMove,
                0,
                0,
                null
            );
            source.TryAppendWaypoint("first");
            source.TryAppendWaypoint("second");

            bool removed = source.TryRemoveLastWaypoint();

            Assert.IsTrue(removed);
            CollectionAssert.AreEqual(new[] { "first" }, source.WaypointPlanetIds);
        }

        /// <summary>
        /// Creates window.
        /// </summary>
        /// <returns>The created window.</returns>
        private UIWindow CreateWindow()
        {
            _windowObject = new GameObject(
                "Window",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(UIWindow)
            );
            return _windowObject.GetComponent<UIWindow>();
        }
    }
}
