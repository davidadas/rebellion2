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
            _effects.ShowImpact(shot, impact);
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
            _effects.ShowImpact(shot, TacticalCombatImpactKind.Hull);
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
            _effects.ShowImpact(shot, TacticalCombatImpactKind.Shield);
            int count = _definition
                .Effects.Single(item => item.Kind == TacticalCombatEffectKind.ShieldHit)
                .FramePaths.Count;
            for (int frame = 0; frame <= count; frame++)
                _effects.Advance(1f / 60);
            Assert.IsFalse(_root.GetComponentInChildren<MeshRenderer>().enabled);
        }

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
