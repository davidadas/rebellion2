using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatMeshPickerTests
    {
        private GameObject root;
        private Mesh mesh;
        private TacticalCombatMeshPicker picker;

        /// <summary>
        /// Creates one shared triangle mesh and an isolated picker for each scenario.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Picker test");
            mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-1, -1, 0),
                    new Vector3(1, -1, 0),
                    new Vector3(0, 1, 0),
                },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.RecalculateBounds();
            picker = new TacticalCombatMeshPicker();
        }

        /// <summary>
        /// Releases the test hierarchy and shared mesh.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Pick_OverlappingUnits_SelectsNearestVisibleFace()
        {
            AddUnit("far", 10);
            TacticalCombatUnit expected = AddUnit("near", 5);
            float distance = 100;

            TacticalCombatUnit actual = picker.Pick(
                new Ray(Vector3.zero, Vector3.forward),
                ref distance
            );

            Assert.AreSame(expected, actual);
            Assert.That(distance, Is.EqualTo(5).Within(0.001f));
        }

        [Test]
        public void Pick_OutsideTriangle_ReturnsNoUnit()
        {
            AddUnit("unit", 5);
            float distance = 100;

            TacticalCombatUnit actual = picker.Pick(
                new Ray(new Vector3(.9f, .9f, 0), Vector3.forward),
                ref distance
            );

            Assert.IsNull(actual);
        }

        [Test]
        public void Pick_HiddenDetail_IgnoresInactiveGeometry()
        {
            AddUnit("hidden", 3);
            root.transform.GetChild(0).gameObject.SetActive(false);
            TacticalCombatUnit expected = AddUnit("visible", 5);
            float distance = 100;

            TacticalCombatUnit actual = picker.Pick(
                new Ray(Vector3.zero, Vector3.forward),
                ref distance
            );

            Assert.AreSame(expected, actual);
        }

        [Test]
        public void Pick_WithdrawnUnit_IgnoresInactiveParticipant()
        {
            TacticalCombatUnit withdrawn = AddUnit("withdrawn", 3);
            withdrawn.Status = TacticalCombatUnitStatus.Withdrawn;
            TacticalCombatUnit expected = AddUnit("visible", 5);
            float distance = 100;

            TacticalCombatUnit actual = picker.Pick(
                new Ray(Vector3.zero, Vector3.forward),
                ref distance
            );

            Assert.AreSame(expected, actual);
        }

        [Test]
        public void Pick_ScaledHierarchy_PreservesWorldRayDistance()
        {
            AddUnit("scaled", 5);
            root.transform.localScale = Vector3.one * 2;
            float distance = 100;

            TacticalCombatUnit actual = picker.Pick(
                new Ray(Vector3.zero, Vector3.forward),
                ref distance
            );

            Assert.IsNotNull(actual);
            Assert.That(distance, Is.EqualTo(10).Within(0.001f));
        }

        /// <summary>
        /// Registers one live participant and a triangle at a known camera distance.
        /// </summary>
        /// <param name="id">The participant identifier.</param>
        /// <param name="depth">The triangle's local forward distance.</param>
        /// <returns>The associated participant.</returns>
        private TacticalCombatUnit AddUnit(string id, float depth)
        {
            TacticalCombatUnit unit = new(
                new CapitalShip
                {
                    InstanceID = id,
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                },
                CombatSide.Attacker,
                root.transform.childCount
            );
            GameObject visual = new GameObject(id, typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0, 0, depth);
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            picker.Register(visual, unit);
            return unit;
        }
    }
}
