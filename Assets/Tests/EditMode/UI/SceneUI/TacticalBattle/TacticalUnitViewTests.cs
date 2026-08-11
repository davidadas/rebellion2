using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.TacticalBattle
{
    [TestFixture]
    public class TacticalUnitViewTests
    {
        private Sprite[] gravityWellFrames;
        private GameObject root;
        private Sprite[] tractorLockFrames;
        private TacticalUnitView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TacticalUnitViewTests");
            view = root.AddComponent<TacticalUnitView>();
            tractorLockFrames = CreateFrames(Color.blue);
            gravityWellFrames = CreateFrames(Color.red);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            DestroyFrames(tractorLockFrames);
            DestroyFrames(gravityWellFrames);
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

        [Test]
        public void ConfigurePersistentEffects_GravityWellUnit_ShowsGravityWellEffect()
        {
            view.Initialize(CreateUnit(true));

            view.ConfigurePersistentEffects(
                tractorLockFrames,
                gravityWellFrames,
                new Bounds(Vector3.zero, Vector3.one)
            );

            Assert.IsTrue(FindEffect("Gravity Well Effect").gameObject.activeSelf);
        }

        [Test]
        public void ShowTractorBeam_ScaledUnit_PreservesWorldEffectDiameter()
        {
            root.transform.localScale = Vector3.one * 12f;
            view.Initialize(CreateUnit());

            view.ConfigurePersistentEffects(
                tractorLockFrames,
                gravityWellFrames,
                new Bounds(Vector3.zero, Vector3.one)
            );
            view.ShowTractorBeam();

            Assert.AreEqual(5f, FindOneShotEffect().transform.lossyScale.x);
        }

        [Test]
        public void ShowTractorBeam_ConfiguredFrames_CreatesOneShotEffect()
        {
            view.Initialize(CreateUnit());
            view.ConfigurePersistentEffects(
                tractorLockFrames,
                gravityWellFrames,
                new Bounds(Vector3.zero, Vector3.one)
            );

            view.ShowTractorBeam();

            Assert.That(FindOneShotEffect(), Is.Not.Null);
        }

        [Test]
        public void ShowTractorBeam_MultipleAttacks_CreatesIndependentEffects()
        {
            view.Initialize(CreateUnit());
            view.ConfigurePersistentEffects(
                tractorLockFrames,
                gravityWellFrames,
                new Bounds(Vector3.zero, Vector3.one)
            );
            view.ShowTractorBeam();
            view.ShowTractorBeam();

            Assert.That(
                root.GetComponentsInChildren<TacticalOneShotEffectView>(true),
                Has.Length.EqualTo(2)
            );
        }

        /// <summary>
        /// Creates one active capital-ship tactical state for presentation tests.
        /// </summary>
        /// <returns>The initialized tactical state.</returns>
        private static TacticalUnitState CreateUnit(bool hasGravityWell = false)
        {
            return TacticalUnitState.FromCapitalShip(
                new CapitalShip
                {
                    CurrentHullStrength = 100,
                    MaxHullStrength = 100,
                    MaxShieldStrength = 50,
                    HasGravityWell = hasGravityWell,
                },
                TacticalBattleSide.Attacker
            );
        }

        /// <summary>
        /// Creates one test animation sequence with the required eight frames.
        /// </summary>
        /// <param name="color">The solid frame color.</param>
        /// <returns>The owned test sprites.</returns>
        private static Sprite[] CreateFrames(Color color)
        {
            Sprite[] frames = new Sprite[8];
            for (int index = 0; index < frames.Length; index++)
            {
                Texture2D texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, color);
                texture.Apply();
                frames[index] = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    1f
                );
            }

            return frames;
        }

        /// <summary>
        /// Releases one owned test animation sequence and its textures.
        /// </summary>
        /// <param name="frames">The sprites to release.</param>
        private static void DestroyFrames(Sprite[] frames)
        {
            if (frames == null)
                return;

            foreach (Sprite frame in frames)
            {
                if (frame == null)
                    continue;

                Texture texture = frame.texture;
                Object.DestroyImmediate(frame);
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Finds one configured persistent effect by hierarchy name.
        /// </summary>
        /// <param name="name">The effect hierarchy name.</param>
        /// <returns>The matching effect.</returns>
        private TacticalPersistentEffectView FindEffect(string name)
        {
            return System.Array.Find(
                root.GetComponentsInChildren<TacticalPersistentEffectView>(true),
                effect => effect.name == name
            );
        }

        /// <summary>
        /// Finds the first configured one-shot tactical effect.
        /// </summary>
        /// <returns>The matching effect.</returns>
        private TacticalOneShotEffectView FindOneShotEffect()
        {
            return root.GetComponentInChildren<TacticalOneShotEffectView>(true);
        }
    }
}
