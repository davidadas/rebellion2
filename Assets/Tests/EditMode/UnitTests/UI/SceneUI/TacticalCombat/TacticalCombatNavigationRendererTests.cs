using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatNavigationRendererTests
    {
        private GameObject _root;
        private TacticalCombatNavigationRenderer _subject;

        /// <summary>
        /// Creates an isolated scene before exercising runtime-owned marker resources.
        /// </summary>
        /// <returns>The play-mode setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _root = new GameObject("Navigation test");
            _root.transform.position = Vector3.one * 100;
            _subject = new TacticalCombatNavigationRenderer(
                _root.transform,
                Shader.Find("Rebellion/Tactical fighter"),
                new TacticalCombatNavigation(100)
            );
        }

        /// <summary>
        /// Releases markers before leaving play mode.
        /// </summary>
        /// <returns>The cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _subject?.Dispose();
            Object.Destroy(_root);
            yield return null;
            _subject = null;
            _root = null;
            yield return new ExitPlayMode();
        }

        [Test]
        public void Constructor_NewEncounter_KeepsAllNavigationRingsHidden()
        {
            Assert.AreEqual(105, _root.GetComponentsInChildren<MeshRenderer>(true).Length);
            Assert.AreEqual(0, _root.GetComponentsInChildren<MeshRenderer>().Length);
        }

        [Test]
        public void ToggleRing_InnerThenOuter_ControlsIndependentVisibility()
        {
            _subject.ToggleRing(0);
            Assert.AreEqual(27, _root.GetComponentsInChildren<MeshRenderer>().Length);
            _subject.ToggleRing(3);
            Assert.AreEqual(53, _root.GetComponentsInChildren<MeshRenderer>().Length);
            _subject.ToggleRing(0);
            Assert.AreEqual(26, _root.GetComponentsInChildren<MeshRenderer>().Length);
        }

        [Test]
        public void TryPick_VisibleCenterMarker_ReturnsNavigationPosition()
        {
            _subject.ToggleRing(0);
            Ray ray = new(_root.transform.position - Vector3.forward * 5, Vector3.forward);
            Assert.IsTrue(_subject.TryPick(ray, 10, out Vector3N position));
            Assert.AreEqual(Vector3N.Zero, position);
        }

        [Test]
        public void TryPick_HiddenCenterMarker_DoesNotIssueMovementPoint()
        {
            Ray ray = new(_root.transform.position - Vector3.forward * 5, Vector3.forward);
            Assert.IsFalse(_subject.TryPick(ray, 10, out _));
        }

        [Test]
        public void UpdateSelection_RequestedRoute_HighlightsSelectedNavigationPoint()
        {
            TacticalCombatUnit unit = new(
                new CapitalShip
                {
                    InstanceID = "ship",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                },
                CombatSide.Attacker,
                1
            );
            TacticalCombatGroup group = new(2, new[] { unit }, Vector3N.UnitZ);
            group.SetWaypoint(Vector3N.Zero);
            _subject.UpdateSelection(new[] { group });
            MeshRenderer center = _root
                .GetComponentsInChildren<MeshRenderer>(true)
                .Single(renderer => renderer.transform.localPosition == Vector3.zero);
            Assert.AreEqual(Color.magenta, center.sharedMaterial.color);
            _subject.UpdateSelection(System.Array.Empty<TacticalCombatGroup>());
            Assert.AreEqual(Color.red, center.sharedMaterial.color);
        }
    }
}
