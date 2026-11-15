using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatImpactEffectsTests
    {
        private GameObject _root;
        private TacticalCombatImpactEffects _effects;
        private ContentAssets _assets;
        private TacticalCombatDefinition _definition;

        /// <summary>
        /// Enters an isolated scene and loads the pack-authored animation resources.
        /// </summary>
        /// <returns>The play-mode setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            ContentPack pack = ContentPackLoader.OpenActive();
            _assets = new ContentAssets(pack.FileResolver);
            _definition = pack.GameData.TacticalCombat;
            _root = new GameObject("Impact test");
            GameObject camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(_root.transform, false);
            _effects = new TacticalCombatImpactEffects(
                _root.transform,
                camera.GetComponent<Camera>(),
                Shader.Find("Rebellion/Tactical fighter"),
                _definition.Effects,
                _assets
            );
        }

        /// <summary>
        /// Releases effect-owned resources before disposing their addressed textures.
        /// </summary>
        /// <returns>The play-mode cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _effects.Dispose();
            Object.Destroy(_root);
            yield return null;
            _assets.Dispose();
            yield return new ExitPlayMode();
        }

        [TestCase(
            TacticalWeaponType.LaserCannon,
            TacticalCombatImpactKind.Shield,
            TacticalCombatEffectKind.ShieldHit
        )]
        [TestCase(
            TacticalWeaponType.IonCannon,
            TacticalCombatImpactKind.Shield,
            TacticalCombatEffectKind.IonShieldHit
        )]
        [TestCase(
            TacticalWeaponType.LaserCannon,
            TacticalCombatImpactKind.Hull,
            TacticalCombatEffectKind.HullHit
        )]
        [TestCase(
            TacticalWeaponType.IonCannon,
            TacticalCombatImpactKind.Hull,
            TacticalCombatEffectKind.IonHullHit
        )]
        [TestCase(
            TacticalWeaponType.Torpedo,
            TacticalCombatImpactKind.Hull,
            TacticalCombatEffectKind.TorpedoHullHit
        )]
        [TestCase(
            TacticalWeaponType.Turbolaser,
            TacticalCombatImpactKind.Destroyed,
            TacticalCombatEffectKind.Destruction
        )]
        public void ShowImpact_WeaponAndSurface_UsesAuthoredSequence(
            TacticalWeaponType weapon,
            TacticalCombatImpactKind impact,
            TacticalCombatEffectKind expected
        )
        {
            TacticalCombatShot shot = CreateShot(weapon);
            _effects.ShowImpact(shot.Target, shot.Weapon, impact);
            TacticalCombatEffectDefinition definition = _definition.Effects.Single(item =>
                item.Kind == expected
            );
            Assert.AreSame(
                _assets.GetTexture(definition.FramePaths[0]),
                _root.GetComponentInChildren<MeshRenderer>().sharedMaterial.mainTexture
            );
        }

        [Test]
        public void ShowImpact_DestructionAlreadyPlaying_PreservesHigherPrioritySequence()
        {
            TacticalCombatShot shot = CreateShot(TacticalWeaponType.LaserCannon);
            _effects.ShowDestruction(shot.Target);
            Texture initial = _root
                .GetComponentInChildren<MeshRenderer>()
                .sharedMaterial.mainTexture;
            _effects.ShowImpact(shot.Target, shot.Weapon, TacticalCombatImpactKind.Hull);
            Assert.AreSame(
                initial,
                _root.GetComponentInChildren<MeshRenderer>().sharedMaterial.mainTexture
            );
        }

        [Test]
        public void Advance_Destruction_ReleasesModelAfterTwelfthPresentationFrame()
        {
            TacticalCombatShot shot = CreateShot(TacticalWeaponType.LaserCannon);
            _effects.ShowDestruction(shot.Target);
            for (int frame = 0; frame < 11; frame++)
                _effects.Advance(1f / 60);
            Assert.IsTrue(_effects.KeepsModelVisible(shot.Target));
            _effects.Advance(1f / 60);
            Assert.IsFalse(_effects.KeepsModelVisible(shot.Target));
        }

        [Test]
        public void Advance_CompletedFrameSequence_DisablesImpactRenderer()
        {
            TacticalCombatShot shot = CreateShot(TacticalWeaponType.LaserCannon);
            _effects.ShowImpact(shot.Target, shot.Weapon, TacticalCombatImpactKind.Shield);
            int count = _definition
                .Effects.Single(item => item.Kind == TacticalCombatEffectKind.ShieldHit)
                .FramePaths.Count;
            for (int frame = 0; frame <= count; frame++)
                _effects.Advance(1f / 60);
            Assert.IsFalse(_root.GetComponentInChildren<MeshRenderer>().enabled);
        }

        [Test]
        public void Advance_TractorLock_WaitsForStrictIntervalAndWrapsAllFrames()
        {
            TacticalCombatUnit unit = CreateShot(TacticalWeaponType.LaserCannon).Target;
            _effects.SetLock(unit, TacticalCombatEffectKind.TractorRestraint, true);
            MeshRenderer renderer = GetLockRenderer();
            Assert.IsTrue(renderer.enabled);
            Assert.AreSame(
                GetLockFrame(TacticalCombatEffectKind.TractorRestraint, 0),
                renderer.sharedMaterial.mainTexture
            );

            _effects.Advance(.1f);
            _effects.Advance(.1f);
            Assert.AreSame(
                GetLockFrame(TacticalCombatEffectKind.TractorRestraint, 0),
                renderer.sharedMaterial.mainTexture
            );
            for (int frame = 1; frame < 8; frame++)
            {
                _effects.Advance(.101f);
                Assert.AreSame(
                    GetLockFrame(TacticalCombatEffectKind.TractorRestraint, frame),
                    renderer.sharedMaterial.mainTexture
                );
            }
            _effects.Advance(.101f);
            Assert.AreSame(
                GetLockFrame(TacticalCombatEffectKind.TractorRestraint, 0),
                renderer.sharedMaterial.mainTexture
            );
        }

        [Test]
        public void SetLock_RepeatedActivation_PreservesFrameAndElapsedInterval()
        {
            TacticalCombatUnit unit = CreateShot(TacticalWeaponType.LaserCannon).Target;
            _effects.SetLock(unit, TacticalCombatEffectKind.TractorRestraint, true);
            _effects.Advance(.101f);
            _effects.Advance(.06f);
            _effects.SetLock(unit, TacticalCombatEffectKind.TractorRestraint, true);
            _effects.Advance(.05f);

            Assert.AreSame(
                GetLockFrame(TacticalCombatEffectKind.TractorRestraint, 1),
                GetLockRenderer().sharedMaterial.mainTexture
            );
        }

        [Test]
        public void SetLock_GravityReplacesTractor_DoesNotRestoreClearedOwnership()
        {
            TacticalCombatUnit unit = CreateShot(TacticalWeaponType.LaserCannon).Target;
            _effects.SetLock(unit, TacticalCombatEffectKind.TractorRestraint, true);
            _effects.SetLock(unit, TacticalCombatEffectKind.GravityLock, true);
            Assert.AreSame(
                GetLockFrame(TacticalCombatEffectKind.GravityLock, 0),
                GetLockRenderer().sharedMaterial.mainTexture
            );

            _effects.SetLock(unit, TacticalCombatEffectKind.GravityLock, false);

            Assert.IsFalse(GetLockRenderer().enabled);
        }

        [Test]
        public void SetLock_OverlappingSelectors_PreservesDetachBeforeRepeatedGravity()
        {
            TacticalCombatUnit unit = CreateShot(TacticalWeaponType.LaserCannon).Target;
            _effects.SetLock(unit, TacticalCombatEffectKind.GravityLock, true);
            _effects.SetLock(unit, TacticalCombatEffectKind.TractorRestraint, true);
            Assert.AreSame(
                GetLockFrame(TacticalCombatEffectKind.TractorRestraint, 0),
                GetLockRenderer().sharedMaterial.mainTexture
            );

            _effects.SetLock(unit, TacticalCombatEffectKind.GravityLock, true);

            Assert.IsFalse(GetLockRenderer().enabled);
            _effects.Advance(.101f);
            Assert.IsFalse(GetLockRenderer().enabled);
            _effects.SetLock(unit, TacticalCombatEffectKind.GravityLock, true);
            Assert.IsTrue(GetLockRenderer().enabled);
        }

        [Test]
        public void RestartLocks_ChangedModelDetail_ReattachesFirstFrame()
        {
            TacticalCombatUnit unit = CreateShot(TacticalWeaponType.LaserCannon).Target;
            _effects.SetLock(unit, TacticalCombatEffectKind.TractorRestraint, true);
            _effects.Advance(.101f);
            _effects.Advance(.101f);
            Assert.AreSame(
                GetLockFrame(TacticalCombatEffectKind.TractorRestraint, 1),
                GetLockRenderer().sharedMaterial.mainTexture
            );

            _effects.RestartLocks(unit);

            Assert.IsTrue(GetLockRenderer().enabled);
            Assert.AreSame(
                GetLockFrame(TacticalCombatEffectKind.TractorRestraint, 0),
                GetLockRenderer().sharedMaterial.mainTexture
            );
        }

        [Test]
        public void ClearLocks_ImpactPlaying_PreservesImpactAnimation()
        {
            TacticalCombatShot shot = CreateShot(TacticalWeaponType.LaserCannon);
            _effects.SetLock(shot.Target, TacticalCombatEffectKind.TractorRestraint, true);
            _effects.ShowImpact(shot.Target, shot.Weapon, TacticalCombatImpactKind.Shield);

            _effects.ClearLocks(shot.Target);

            Assert.IsFalse(GetLockRenderer().enabled);
            Assert.IsTrue(
                _root
                    .GetComponentsInChildren<MeshRenderer>(true)
                    .Single(renderer => renderer.name == "Impact")
                    .enabled
            );
        }

        [Test]
        public void Advance_Destruction_ReleasesLockOnTwelfthPresentationFrame()
        {
            TacticalCombatUnit unit = CreateShot(TacticalWeaponType.LaserCannon).Target;
            _effects.SetLock(unit, TacticalCombatEffectKind.TractorRestraint, true);
            _effects.ShowDestruction(unit);
            for (int frame = 0; frame < 11; frame++)
                _effects.Advance(1f / 60);
            Assert.IsTrue(GetLockRenderer().enabled);

            _effects.Advance(1f / 60);

            Assert.IsFalse(GetLockRenderer().enabled);
        }

        [Test]
        public void Advance_LockAndImpact_PlacesThemOnOppositeSidesOfCapital()
        {
            TacticalCombatShot shot = CreateShot(TacticalWeaponType.LaserCannon);
            shot.Target.Position = new System.Numerics.Vector3(0, 0, 10);
            _effects.SetLock(shot.Target, TacticalCombatEffectKind.TractorRestraint, true);
            _effects.ShowImpact(shot.Target, shot.Weapon, TacticalCombatImpactKind.Shield);

            _effects.Advance(0);

            Assert.AreEqual(new Vector3(0, 0, 11), GetLockRenderer().transform.position);
            Assert.AreEqual(
                new Vector3(0, 0, 9),
                _root
                    .GetComponentsInChildren<MeshRenderer>(true)
                    .Single(renderer => renderer.name == "Impact")
                    .transform.position
            );
        }

        /// <summary>
        /// Finds the independently controlled loop billboard.
        /// </summary>
        /// <returns>The loop renderer.</returns>
        private MeshRenderer GetLockRenderer() =>
            _root
                .GetComponentsInChildren<MeshRenderer>(true)
                .Single(renderer => renderer.name == "Lock");

        /// <summary>
        /// Resolves one authored loop frame through the active content cache.
        /// </summary>
        /// <param name="kind">The selected loop animation.</param>
        /// <param name="frame">The zero-based frame index.</param>
        /// <returns>The cached frame texture.</returns>
        private Texture2D GetLockFrame(TacticalCombatEffectKind kind, int frame) =>
            _assets.GetTexture(
                _definition.Effects.Single(definition => definition.Kind == kind).FramePaths[frame]
            );

        /// <summary>
        /// Creates a capital discharge without mutating either campaign unit.
        /// </summary>
        /// <param name="weapon">The impacting weapon.</param>
        /// <returns>The captured discharge.</returns>
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
            );
            return new TacticalCombatShot(source, target, weapon, 50);
        }
    }
}
