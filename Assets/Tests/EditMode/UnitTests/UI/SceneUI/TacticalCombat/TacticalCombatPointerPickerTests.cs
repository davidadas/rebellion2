using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Gameplay;
using UnityEngine;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatPointerPickerTests
    {
        [Test]
        public void TryPick_FirstDiagonalHit_StopsBeforeOtherNeighboringRays()
        {
            List<Vector2> visited = new();
            TacticalCombatPick pick = (
                Vector2 point,
                out TacticalCombatUnit unit,
                out Vector3N waypoint
            ) =>
            {
                visited.Add(point);
                unit = null;
                waypoint = Vector3N.UnitX;
                return visited.Count >= 2;
            };

            bool found = TacticalCombatPointerPicker.TryPick(
                new Vector2(0.5f, 0.5f),
                new Vector2Int(100, 50),
                false,
                pick,
                out TacticalCombatUnit selected,
                out Vector3N position
            );

            Assert.IsTrue(found);
            Assert.IsNull(selected);
            Assert.AreEqual(Vector3N.UnitX, position);
            CollectionAssert.AreEqual(
                new[] { new Vector2(0.5f, 0.5f), new Vector2(0.49f, 0.52f) },
                visited
            );
        }

        [TestCase(false, false, 5)]
        [TestCase(true, true, 13)]
        public void TryPick_OnlyOuterEdgeHasHit_UsesSecondaryPointerNeighborhood(
            bool secondary,
            bool expected,
            int attempts
        )
        {
            List<Vector2> visited = new();
            TacticalCombatPick pick = (
                Vector2 point,
                out TacticalCombatUnit unit,
                out Vector3N waypoint
            ) =>
            {
                visited.Add(point);
                unit = null;
                waypoint = Vector3N.UnitZ;
                return point == new Vector2(0.48f, 0.5f);
            };

            bool found = TacticalCombatPointerPicker.TryPick(
                new Vector2(0.5f, 0.5f),
                new Vector2Int(100, 100),
                secondary,
                pick,
                out _,
                out Vector3N position
            );

            Assert.AreEqual(expected, found);
            Assert.AreEqual(attempts, visited.Count);
            if (expected)
                Assert.AreEqual(Vector3N.UnitZ, position);
        }
    }
}
