using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Tactical.Rendering;
using Rebellion.UI.SceneUI.TacticalCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Rebellion.Tests.Tactical.Rendering
{
    [TestFixture]
    public sealed class TacticalCombatImpactEffectsTests
    {
        private GameObject _root;
        private TacticalCombatImpactEffects _effects;
        private ContentAssets _assets;
        private TacticalCombatDefinition _definition;
        private string _folder;
        private readonly Dictionary<CombatUnit, Vector3> _positions = new();
        private int? _closeUpPrimary;

        /// <summary>
        /// Creates test-owned animation frames and exposes them through the real content resolver.
        /// </summary>
        [OneTimeSetUp]
        public void CreateFrames()
        {
            _folder = Path.Combine(Path.GetTempPath(), "tactical-impact-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(_folder);
            _definition = new TacticalCombatDefinition();
            Texture2D source = new(2, 2);
            try
            {
                byte[] png = source.EncodeToPNG();
                foreach (
                    TacticalCombatEffectKind kind in Enum.GetValues(
                        typeof(TacticalCombatEffectKind)
                    )
                )
                {
                    TacticalCombatEffectDefinition effect = new()
                    {
                        Kind = kind,
                        Size = 1,
                        OriginX = 0.5f,
                        OriginY = 0.5f,
                    };
                    int frameCount = kind switch
                    {
                        TacticalCombatEffectKind.Destruction => 12,
                        TacticalCombatEffectKind.TractorRestraint
                        or TacticalCombatEffectKind.GravityLock => 8,
                        _ => 4,
                    };
                    for (int frame = 0; frame < frameCount; frame++)
                    {
                        string name = $"{kind}-{frame}";
                        File.WriteAllBytes(Path.Combine(_folder, name + ".png"), png);
                        effect.FramePaths.Add("Pack/" + name);
                    }
                    _definition.Effects.Add(effect);
                }
            }
            finally
            {
                Object.Destroy(source);
            }
        }

        /// <summary>
        /// Creates an isolated effects owner with the test animation definitions.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _assets = new ContentAssets(new ContentFileResolver(_folder, _folder));
            _root = new GameObject("Impact test");
            _positions.Clear();
            _closeUpPrimary = null;
            GameObject camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(_root.transform, false);
            _effects = new TacticalCombatImpactEffects(
                _root.transform,
                camera.GetComponent<Camera>(),
                Shader.Find("Rebellion/Tactical fighter"),
                _definition.Effects,
                _assets,
                GetPresentationPosition,
                () => _closeUpPrimary
            );
        }

        /// <summary>
        /// Releases effect-owned resources before disposing their addressed textures.
        /// </summary>
        /// <returns>The play-mode cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _effects?.Dispose();
            Object.Destroy(_root);
            yield return null;
            _assets?.Dispose();
        }

        /// <summary>
        /// Removes test-owned animation frames after the textures have been released.
        /// </summary>
        [OneTimeTearDown]
        public void RemoveFrames()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestCase(
            HardpointWeaponType.LaserCannon,
            TacticalCombatImpactKind.Shield,
            TacticalCombatEffectKind.ShieldHit
        )]
        [TestCase(
            HardpointWeaponType.IonCannon,
            TacticalCombatImpactKind.Shield,
            TacticalCombatEffectKind.IonShieldHit
        )]
        [TestCase(
            HardpointWeaponType.LaserCannon,
            TacticalCombatImpactKind.Hull,
            TacticalCombatEffectKind.HullHit
        )]
        [TestCase(
            HardpointWeaponType.IonCannon,
            TacticalCombatImpactKind.Hull,
            TacticalCombatEffectKind.IonHullHit
        )]
        [TestCase(
            HardpointWeaponType.TorpedoLauncher,
            TacticalCombatImpactKind.Hull,
            TacticalCombatEffectKind.TorpedoHullHit
        )]
        [TestCase(
            HardpointWeaponType.Turbolaser,
            TacticalCombatImpactKind.Destroyed,
            TacticalCombatEffectKind.Destruction
        )]
        public void ShowImpact_WeaponAndSurface_UsesAuthoredSequence(
            HardpointWeaponType weapon,
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
            TacticalCombatShot shot = CreateShot(HardpointWeaponType.LaserCannon);
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
            TacticalCombatShot shot = CreateShot(HardpointWeaponType.LaserCannon);
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
            TacticalCombatShot shot = CreateShot(HardpointWeaponType.LaserCannon);
            _effects.ShowImpact(shot.Target, shot.Weapon, TacticalCombatImpactKind.Shield);
            int count = _definition
                .Effects.Single(item => item.Kind == TacticalCombatEffectKind.ShieldHit)
                .FramePaths.Count;
            for (int frame = 0; frame <= count; frame++)
                _effects.Advance(1f / 60);
            Assert.IsFalse(_root.GetComponentInChildren<MeshRenderer>().enabled);
        }

        [Test]
        public void ShowDestruction_CloseUpPrimary_ClearsLocksAndRetainsHiddenSequence()
        {
            CombatUnit unit = CreateShot(HardpointWeaponType.LaserCannon).Target;
            _closeUpPrimary = unit.Index;
            _effects.SetLock(unit, TacticalCombatEffectKind.TractorRestraint, true);

            _effects.ShowDestruction(unit);

            Assert.IsFalse(GetImpactRenderer().enabled);
            Assert.IsFalse(GetLockRenderer().enabled);
            Assert.IsTrue(_effects.KeepsModelVisible(unit));
            _closeUpPrimary = null;
            for (int frame = 0; frame < 11; frame++)
                _effects.Advance(1f / 60);
            Assert.IsFalse(GetImpactRenderer().enabled);
            Assert.IsTrue(_effects.KeepsModelVisible(unit));
            _effects.Advance(1f / 60);
            Assert.IsFalse(_effects.KeepsModelVisible(unit));
        }

        [Test]
        public void ShowImpact_RejectedCloseUpRequest_PreservesActiveImpactAndLock()
        {
            TacticalCombatShot shot = CreateShot(HardpointWeaponType.LaserCannon);
            _effects.SetLock(shot.Target, TacticalCombatEffectKind.GravityLock, true);
            _effects.ShowDestruction(shot.Target);
            _closeUpPrimary = shot.Target.Index;

            _effects.ShowImpact(shot.Target, shot.Weapon, TacticalCombatImpactKind.Shield);

            Assert.IsTrue(GetImpactRenderer().enabled);
            Assert.IsTrue(GetLockRenderer().enabled);
        }

        [Test]
        public void Advance_NewCloseUpPrimary_ReleasesImpactPriorityAndRetainsRegisteredLoop()
        {
            TacticalCombatShot shot = CreateShot(HardpointWeaponType.LaserCannon);
            _effects.SetLock(shot.Target, TacticalCombatEffectKind.TractorRestraint, true);
            _effects.ShowDestruction(shot.Target);
            _effects.Advance(.101f);
            _effects.Advance(.101f);
            _closeUpPrimary = shot.Target.Index;

            _effects.Advance(.101f);

            Assert.IsFalse(GetImpactRenderer().enabled);
            Assert.IsFalse(_effects.KeepsModelVisible(shot.Target));
            Assert.IsTrue(GetLockRenderer().enabled);
            _closeUpPrimary = null;
            _effects.RestartLocks(shot.Target);
            _effects.ShowImpact(shot.Target, shot.Weapon, TacticalCombatImpactKind.Shield);
            _effects.Advance(.101f);
            Assert.IsTrue(GetImpactRenderer().enabled);
            Assert.AreSame(
                GetLockFrame(TacticalCombatEffectKind.TractorRestraint, 3),
                GetLockRenderer().sharedMaterial.mainTexture
            );
        }

        [Test]
        public void ShowImpact_DifferentCloseUpPrimary_PreservesImpactAndLock()
        {
            TacticalCombatShot shot = CreateShot(HardpointWeaponType.LaserCannon);
            _closeUpPrimary = shot.Source.Index;
            _effects.SetLock(shot.Target, TacticalCombatEffectKind.GravityLock, true);

            _effects.ShowImpact(shot.Target, shot.Weapon, TacticalCombatImpactKind.Shield);
            _effects.Advance(0);

            Assert.IsTrue(GetImpactRenderer().enabled);
            Assert.IsTrue(GetLockRenderer().enabled);
        }

        [Test]
        public void Advance_TractorLock_WaitsForStrictIntervalAndWrapsAllFrames()
        {
            CombatUnit unit = CreateShot(HardpointWeaponType.LaserCannon).Target;
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
            CombatUnit unit = CreateShot(HardpointWeaponType.LaserCannon).Target;
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
            CombatUnit unit = CreateShot(HardpointWeaponType.LaserCannon).Target;
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
            CombatUnit unit = CreateShot(HardpointWeaponType.LaserCannon).Target;
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
            CombatUnit unit = CreateShot(HardpointWeaponType.LaserCannon).Target;
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
            TacticalCombatShot shot = CreateShot(HardpointWeaponType.LaserCannon);
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
            CombatUnit unit = CreateShot(HardpointWeaponType.LaserCannon).Target;
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
            TacticalCombatShot shot = CreateShot(HardpointWeaponType.LaserCannon);
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

        [Test]
        public void Advance_TemporaryPresentationPose_KeepsImpactsAndLocksWithPresentedCapital()
        {
            TacticalCombatShot shot = CreateShot(HardpointWeaponType.LaserCannon);
            shot.Target.Position = new System.Numerics.Vector3(0, 0, 10);
            _positions[shot.Target] = new Vector3(0, 0, 20);
            _effects.SetLock(shot.Target, TacticalCombatEffectKind.TractorRestraint, true);
            _effects.ShowImpact(shot.Target, shot.Weapon, TacticalCombatImpactKind.Shield);

            _effects.Advance(0);

            Assert.AreEqual(new Vector3(0, 0, 21), GetLockRenderer().transform.position);
            Assert.AreEqual(
                new Vector3(0, 0, 19),
                _root
                    .GetComponentsInChildren<MeshRenderer>(true)
                    .Single(renderer => renderer.name == "Impact")
                    .transform.position
            );
            Assert.AreEqual(new System.Numerics.Vector3(0, 0, 10), shot.Target.Position);
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
        /// Gets the attached one-shot impact image for presentation assertions.
        /// </summary>
        /// <returns>The impact image's renderer.</returns>
        private MeshRenderer GetImpactRenderer() =>
            _root
                .GetComponentsInChildren<MeshRenderer>(true)
                .Single(renderer => renderer.name == "Impact");

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
        /// Resolves explicit temporary poses while retaining ordinary fixture positions elsewhere.
        /// </summary>
        /// <param name="unit">The test participant.</param>
        /// <returns>The encounter-local rendering position.</returns>
        private Vector3 GetPresentationPosition(CombatUnit unit) =>
            _positions.TryGetValue(unit, out Vector3 position)
                ? position
                : new Vector3(unit.Position.X, unit.Position.Y, unit.Position.Z);

        /// <summary>
        /// Creates a capital discharge without mutating either campaign unit.
        /// </summary>
        /// <param name="weapon">The impacting weapon.</param>
        /// <returns>The captured discharge.</returns>
        private static TacticalCombatShot CreateShot(HardpointWeaponType weapon)
        {
            CombatUnit source = TacticalCombatDeployment.CreateUnit(
                new CapitalShip
                {
                    InstanceID = "source",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                },
                CombatSide.Attacker,
                1
            );
            CombatUnit target = TacticalCombatDeployment.CreateUnit(
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
