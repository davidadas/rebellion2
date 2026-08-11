using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.TacticalBattle
{
    [TestFixture]
    public class TacticalUnitViewTests
    {
        private GameObject root;
        private TacticalUnitView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TacticalUnitViewTests");
            view = root.AddComponent<TacticalUnitView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void Synchronize_ActiveUnit_AppliesPositionAndFacing()
        {
            TacticalUnitState unit = CreateUnit();
            unit.Position = new System.Numerics.Vector3(4f, 5f, 6f);
            unit.Forward = System.Numerics.Vector3.UnitX;
            view.Initialize(unit);

            view.Synchronize();

            Assert.That(root.transform.localPosition, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            Assert.That(Vector3.Angle(root.transform.forward, Vector3.right), Is.LessThan(0.001f));
        }

        [Test]
        public void Synchronize_DestroyedUnit_HidesPresentation()
        {
            TacticalUnitState unit = CreateUnit();
            view.Initialize(unit);
            unit.ApplyDamage(unit.Hull + unit.Shields);

            view.Synchronize();

            Assert.That(root.activeSelf, Is.False);
        }

        [Test]
        public void ConfigureHighlight_CapitalShipBounds_CreatesHiddenTwelveEdgeBox()
        {
            view.ConfigureHighlight(new Bounds(Vector3.zero, new Vector3(2f, 4f, 6f)));

            MeshFilter highlight = root.GetComponentInChildren<MeshFilter>(true);

            Assert.That(highlight, Is.Not.Null);
            Assert.That(highlight.sharedMesh.vertexCount, Is.EqualTo(8));
            Assert.That(highlight.sharedMesh.GetIndexCount(0), Is.EqualTo(24));
            Assert.That(highlight.gameObject.activeSelf, Is.False);
        }

        /// <summary>
        /// Creates one active capital-ship tactical state for presentation tests.
        /// </summary>
        /// <returns>The initialized tactical state.</returns>
        private static TacticalUnitState CreateUnit()
        {
            return TacticalUnitState.FromCapitalShip(
                new CapitalShip
                {
                    CurrentHullStrength = 100,
                    MaxHullStrength = 100,
                    MaxShieldStrength = 50,
                },
                TacticalBattleSide.Attacker
            );
        }
    }
}
