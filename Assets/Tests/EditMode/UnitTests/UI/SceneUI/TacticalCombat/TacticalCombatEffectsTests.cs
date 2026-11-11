using System.Collections;
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
    public sealed class TacticalCombatEffectsTests
    {
        private GameObject _root;
        private TacticalCombatEffects _effects;

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
            _effects = new TacticalCombatEffects(
                _root.transform,
                Shader.Find("Rebellion/Tactical fighter"),
                100
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
                new CapitalShip
                {
                    InstanceID = "source",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                },
                CombatSide.Attacker,
                1
            );
            TacticalCombatUnit target = new(
                new CapitalShip
                {
                    InstanceID = "target",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                },
                CombatSide.Defender,
                1000001
            )
            {
                Position = new Vector3N(10, 0, 0),
            };
            return new TacticalCombatShot(source, target, weapon, 50);
        }
    }
}
