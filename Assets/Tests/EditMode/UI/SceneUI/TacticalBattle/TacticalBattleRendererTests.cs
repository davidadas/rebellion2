using System.Reflection;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.TacticalBattle
{
    [TestFixture]
    public sealed class TacticalBattleRendererTests
    {
        private GameObject root;
        private TacticalBattleRenderer renderer;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TacticalBattleRendererTests");
            renderer = root.AddComponent<TacticalBattleRenderer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void SetNavigationRoute_OrderedRoute_HighlightsOnlyLaterWaypoints()
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);
            InvokeCreateNavigationGrid(grid);
            TacticalNavPoint first = grid.GetPoints(0)[0];
            TacticalNavPoint second = grid.GetPoints(0)[1];

            renderer.SetNavigationRoute(new[] { first, second });

            TacticalNavigationMarker[] markers =
                root.GetComponentsInChildren<TacticalNavigationMarker>(true);
            Assert.That(
                FindMarker(markers, first).GetComponent<MeshRenderer>().sharedMaterial.color,
                Is.Not.EqualTo(Color.magenta)
            );
            Assert.That(
                FindMarker(markers, second).GetComponent<MeshRenderer>().sharedMaterial.color,
                Is.EqualTo(Color.magenta)
            );
        }

        [Test]
        public void SetNavigationRoute_ReplacedRoute_RestoresPriorWaypointLayerColor()
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);
            InvokeCreateNavigationGrid(grid);
            TacticalNavPoint first = grid.GetPoints(0)[0];
            TacticalNavPoint second = grid.GetPoints(0)[1];
            renderer.SetNavigationRoute(new[] { first, second });

            renderer.SetNavigationRoute(new[] { first });

            TacticalNavigationMarker marker = FindMarker(
                root.GetComponentsInChildren<TacticalNavigationMarker>(true),
                second
            );
            Assert.That(
                marker.GetComponent<MeshRenderer>().sharedMaterial,
                Is.SameAs(marker.NormalMaterial)
            );
        }

        [Test]
        public void HasActiveCombatEffects_TransientEffectExists_ReturnsTrue()
        {
            GameObject effect = new GameObject("Effect");
            effect.transform.SetParent(root.transform, false);
            effect.AddComponent<TacticalCombatEffectView>();

            bool hasActiveEffects = renderer.HasActiveCombatEffects;

            Assert.IsTrue(hasActiveEffects);
        }

        /// <summary>
        /// Builds the runtime-only marker hierarchy through the renderer's initialization helper.
        /// </summary>
        /// <param name="grid">The navigation grid to render.</param>
        private void InvokeCreateNavigationGrid(TacticalNavigationGrid grid)
        {
            typeof(TacticalBattleRenderer)
                .GetMethod("CreateNavigationGrid", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(renderer, new object[] { grid });
        }

        /// <summary>
        /// Finds the marker associated with one navigation-point instance.
        /// </summary>
        /// <param name="markers">The rendered markers to search.</param>
        /// <param name="point">The represented navigation point.</param>
        /// <returns>The matching marker.</returns>
        private static TacticalNavigationMarker FindMarker(
            TacticalNavigationMarker[] markers,
            TacticalNavPoint point
        )
        {
            return System.Array.Find(markers, marker => ReferenceEquals(marker.Point, point));
        }
    }
}
