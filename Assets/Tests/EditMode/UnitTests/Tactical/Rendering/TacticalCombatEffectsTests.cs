using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Tactical.Rendering;
using Rebellion.UI.SceneUI.TacticalCombat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Vector3N = System.Numerics.Vector3;

namespace Rebellion.Tests.Tactical.Rendering
{
    [TestFixture]
    public sealed class TacticalCombatEffectsTests
    {
        private GameObject _root;
        private TacticalCombatEffects _effects;
        private readonly Dictionary<TacticalCombatUnit, Vector3> _positions = new();

        /// <summary>
        /// Creates an isolated effects owner using the authored tactical shader.
        /// </summary>
        /// <returns>The play-mode setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _root = new GameObject("Effects test");
            _positions.Clear();
            _effects = new TacticalCombatEffects(
                _root.transform,
                Shader.Find("Rebellion/Tactical fighter"),
                100,
                GetPresentationPosition
            );
        }

        /// <summary>
        /// Releases effect meshes, materials, and their owner after each case.
        /// </summary>
        /// <returns>The deferred destruction sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _effects.Dispose();
            Object.Destroy(_root);
            yield return null;
            yield return new ExitPlayMode();
        }

        [Test]
        public void Advance_Torpedo_RemainsVisibleUntilTwoSecondLifetimeExpires()
        {
            TacticalCombatShot shot = CreateShot(TacticalWeaponType.Torpedo);
            _effects.ShowShot(shot, 0);
            _effects.Advance(1.5f);
            Assert.AreEqual(1, _root.GetComponentsInChildren<MeshRenderer>().Length);
            _effects.Advance(0.51f);
            Assert.IsEmpty(_root.GetComponentsInChildren<MeshRenderer>());
        }

        [Test]
        public void ShowShot_ExpiredEffect_ReusesItsRendererAndGeometry()
        {
            TacticalCombatShot shot = CreateShot(TacticalWeaponType.LaserCannon);
            _effects.ShowShot(shot, 0);
            MeshRenderer first = _root.GetComponentInChildren<MeshRenderer>();
            _effects.Advance(1.01f);
            _effects.ShowShot(shot, 0);
            Assert.AreSame(first, _root.GetComponentInChildren<MeshRenderer>());
            Assert.AreEqual(1, _root.transform.childCount);
        }

        [TestCase(TacticalWeaponType.LaserCannon, 0, 1, 0, 0)]
        [TestCase(TacticalWeaponType.LaserCannon, 1, 0, 1, 0)]
        [TestCase(TacticalWeaponType.IonCannon, 0, 0, 0, 1)]
        [TestCase(TacticalWeaponType.Torpedo, 1, 1, 1, 1)]
        public void ShowShot_WeaponAndFaction_UsesSpecifiedBeamColor(
            TacticalWeaponType weapon,
            int faction,
            float red,
            float green,
            float blue
        )
        {
            _effects.ShowShot(CreateShot(weapon), faction);
            Assert.AreEqual(
                new Color(red, green, blue),
                _root.GetComponentInChildren<MeshRenderer>().sharedMaterial.color
            );
        }

        [Test]
        public void Advance_Superlaser_GrowsFromFixedEmitterTowardTarget()
        {
            TacticalCombatShot shot = CreateShot(TacticalWeaponType.Turbolaser);
            Vector3 origin = new(5, 0, 0);
            _effects.ShowSuperlaser(origin, shot.Target);
            _effects.Advance(0.5f);
            Transform beam = _root.transform.GetChild(0);
            Assert.AreEqual(origin, beam.localPosition);
            Assert.AreEqual(
                Vector3.Distance(origin, new Vector3(10, 0, 0)) * .5f,
                beam.GetChild(0).localScale.z
            );
        }

        [Test]
        public void ShowShot_TemporaryPresentationPose_UsesPresentedSourceAndMovingTarget()
        {
            TacticalCombatShot shot = CreateShot(TacticalWeaponType.LaserCannon);
            Vector3 origin = new(0, 4, 0);
            _positions[shot.Source] = origin;
            _positions[shot.Target] = new Vector3(10, 6, 20);

            _effects.ShowShot(shot, 0);
            Transform beam = _root.transform.GetChild(0);
            Assert.AreEqual(origin, beam.localPosition);
            _effects.Advance(.5f);
            Assert.AreEqual(new Vector3(5, 5, 10), beam.localPosition);
            _positions[shot.Target] = new Vector3(20, 9, 0);
            _effects.Advance(.1f);

            Assert.AreEqual(
                Vector3.LerpUnclamped(origin, _positions[shot.Target], .6f),
                beam.localPosition
            );
            Assert.AreEqual(Vector3N.Zero, shot.Source.Position);
            Assert.AreEqual(new Vector3N(10, 0, 0), shot.Target.Position);
        }

        [Test]
        public void Advance_SuperlaserPresentationPose_TracksRenderedTargetFromFixedEmitter()
        {
            TacticalCombatShot shot = CreateShot(TacticalWeaponType.Turbolaser);
            Vector3 origin = new(-3, 0, 2);
            _positions[shot.Target] = new Vector3(10, 8, 12);
            _effects.ShowSuperlaser(origin, shot.Target);

            _effects.Advance(.5f);

            Transform beam = _root.transform.GetChild(0);
            Assert.AreEqual(origin, beam.localPosition);
            Assert.AreEqual(
                Vector3.Distance(origin, _positions[shot.Target]) * .5f,
                beam.GetChild(0).localScale.z
            );
            Assert.AreEqual(new Vector3N(10, 0, 0), shot.Target.Position);
        }

        [Test]
        public void Advance_MovingSuperlaserTarget_RetainsMeasuredGrowthExtentAndTracksDirection()
        {
            TacticalCombatShot shot = CreateShot(TacticalWeaponType.Turbolaser);
            Vector3 origin = new(5, 0, 0);
            _positions[shot.Target] = new Vector3(5, 0, 10);
            _effects.ShowSuperlaser(origin, shot.Target);
            _effects.Advance(.25f);
            Transform beam = _root.transform.GetChild(0);
            Assert.That(beam.GetChild(0).localScale.z, Is.EqualTo(2.5f).Within(.0001f));
            _positions[shot.Target] = new Vector3(25, 0, 0);

            _effects.Advance(.25f);

            Assert.That(beam.GetChild(0).localScale.z, Is.EqualTo(5).Within(.0001f));
            Assert.That(Vector3.Dot(beam.forward, Vector3.right), Is.EqualTo(1).Within(.0001f));
            Assert.AreEqual(origin, beam.localPosition);
            Assert.AreEqual(new Vector3N(10, 0, 0), shot.Target.Position);
        }

        [Test]
        public void Advance_ZeroTimeSuperlaserFrame_MeasuresExtentOnFirstGrowthFrame()
        {
            TacticalCombatShot shot = CreateShot(TacticalWeaponType.Turbolaser);
            _positions[shot.Target] = new Vector3(0, 0, 10);
            _effects.ShowSuperlaser(Vector3.zero, shot.Target);
            _effects.Advance(0);
            _positions[shot.Target] = new Vector3(0, 0, 20);

            _effects.Advance(.25f);

            Transform beam = _root.transform.GetChild(0);
            Assert.That(beam.GetChild(0).localScale.z, Is.EqualTo(5).Within(.0001f));
        }

        [UnityTest]
        public IEnumerator Dispose_ActiveEffect_ReleasesOwnedMeshMaterialAndObjects()
        {
            _effects.ShowShot(CreateShot(TacticalWeaponType.Turbolaser), 1);
            MeshRenderer renderer = _root.GetComponentInChildren<MeshRenderer>();
            Material material = renderer.sharedMaterial;
            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            _effects.Dispose();
            yield return null;
            Assert.IsTrue(material == null);
            Assert.IsTrue(mesh == null);
            Assert.AreEqual(0, _root.transform.childCount);
        }

        /// <summary>
        /// Creates a discharge between two separate campaign-backed ship captures.
        /// </summary>
        /// <param name="weapon">The effect's weapon category.</param>
        /// <returns>A captured shot with a fixed ten-unit span.</returns>
        private static TacticalCombatShot CreateShot(TacticalWeaponType weapon)
        {
            TacticalCombatUnit source = new(
                CombatUnit.Create(
                    new CapitalShip
                    {
                        InstanceID = "source",
                        MaxHullStrength = 100,
                        CurrentHullStrength = 100,
                    }
                ),
                CombatSide.Attacker,
                1
            );
            TacticalCombatUnit target = new(
                CombatUnit.Create(
                    new CapitalShip
                    {
                        InstanceID = "target",
                        MaxHullStrength = 100,
                        CurrentHullStrength = 100,
                    }
                ),
                CombatSide.Defender,
                1000001
            )
            {
                Position = new Vector3N(10, 0, 0),
            };
            return new TacticalCombatShot(source, target, weapon, 50);
        }

        /// <summary>
        /// Resolves explicit temporary poses while retaining ordinary fixture positions elsewhere.
        /// </summary>
        /// <param name="unit">The test participant.</param>
        /// <returns>The encounter-local rendering position.</returns>
        private Vector3 GetPresentationPosition(TacticalCombatUnit unit) =>
            _positions.TryGetValue(unit, out Vector3 position)
                ? position
                : new Vector3(unit.Position.X, unit.Position.Y, unit.Position.Z);
    }
}
