using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalCombatViewTests
    {
        private GameObject _scene;
        private AudioManager _audio;
        private bool _ownsAudio;
        private ActiveBattle _battle;
        private TacticalCombat _combat;
        private TacticalCombatView _view;
        private GameObject _shieldTemplate;

        /// <summary>
        /// Creates a real shot simulation and its scene-owned presentation without global scene searches.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _scene = new GameObject("Combat View Test");
            GameObject cameraObject = new GameObject("Camera", typeof(UnityEngine.Camera));
            cameraObject.transform.SetParent(_scene.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 5f, -20f);
            _ownsAudio = AudioManager.Instance == null;
            _audio = AudioManager.EnsureExists();
            _battle = new ActiveBattle { Kind = BattleKind.Space };
            CapitalShip shooter = new CapitalShip
            {
                OwnerInstanceID = "FIRST",
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                ModelSize = new ModelDimensions
                {
                    Width = 2f,
                    Height = 2f,
                    Depth = 2f,
                },
            };
            HardpointGroup group = new HardpointGroup { ID = "gun" };
            group
                .GetHardpoints()
                .Add(
                    new Hardpoint
                    {
                        Health = 100,
                        FiringArcDegrees = 30f,
                        Weapon = new WeaponData
                        {
                            WeaponType = HardpointWeaponType.LaserCannon,
                            Range = 20f,
                            FireIntervalSeconds = 1f,
                            Projectile = new ProjectileData
                            {
                                Damage = 10f,
                                Speed = 10f,
                                Length = 1f,
                                Width = 0.1f,
                                ColorHex = "#2EFF4D",
                            },
                        },
                    }
                );
            shooter.GetHardpointGroups().Add(group);
            CombatUnit source = _battle.AddCombatants(shooter)[0];
            CapitalShip target = (CapitalShip)shooter.CreateCopy();
            target.InstanceID = "target";
            target.OwnerInstanceID = "SECOND";
            target.GetHardpointGroups().Clear();
            CombatUnit targetCombatant = _battle.AddCombatants(target)[0];
            targetCombatant.Position.Z = 10f;
            Hardpoint point = TacticalCombat.GetGroups(source)[0].GetHardpoints()[0];
            _combat = new TacticalCombat(
                _battle,
                new Dictionary<Hardpoint, TacticalCombat.Mount>
                {
                    [point] = new TacticalCombat.Mount(
                        System.Numerics.Vector3.Zero,
                        System.Numerics.Vector3.UnitZ,
                        System.Numerics.Vector3.UnitY
                    ),
                }
            );
            _shieldTemplate = new GameObject("Shield Template");
            _shieldTemplate.transform.SetParent(_scene.transform, false);
            _shieldTemplate.SetActive(false);
            _view = new TacticalCombatView(
                _battle,
                _combat,
                _scene.transform,
                cameraObject.GetComponent<UnityEngine.Camera>(),
                _audio,
                new Dictionary<string, AudioClip>(),
                AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/TacticalLaserBolt.shader"),
                new Dictionary<CombatUnit, Transform>
                {
                    [source] = _scene.transform,
                    [targetCombatant] = _scene.transform,
                },
                _shieldTemplate
            );
        }

        /// <summary>
        /// Releases the effects before the scene and any application audio manager owned by the test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _view.Dispose();
            Object.DestroyImmediate(_scene);
            if (_ownsAudio && _audio)
                Object.DestroyImmediate(_audio.gameObject);
        }

        /// <summary>
        /// Verifies the fixed voice budget prevents effect count from creating unbounded audio sources.
        /// </summary>
        [Test]
        public void Constructor_WeaponPresentation_CreatesSevenBoundedSpatialVoices()
        {
            AudioSource[] sources = _scene.GetComponentsInChildren<AudioSource>();
            Assert.That(sources, Has.Length.EqualTo(7));
            foreach (AudioSource source in sources)
            {
                Assert.That(source.spatialBlend, Is.EqualTo(0.4f));
                Assert.That(source.playOnAwake, Is.False);
            }
        }

        /// <summary>
        /// Verifies shots use the retained weapon shader and authored color instead of a fallback material.
        /// </summary>
        [Test]
        public void Advance_EmittedShot_UsesAuthoredColorAndWeaponShader()
        {
            _combat.Advance(0.125f);
            _view.Advance(0.125f, false);
            LineRenderer[] lines = _scene.GetComponentsInChildren<LineRenderer>();
            Assert.That(lines, Is.Not.Empty);
            foreach (LineRenderer line in lines)
            {
                Assert.That(
                    line.sharedMaterial.shader.name,
                    Is.EqualTo("Rebellion/Tactical Laser Bolt")
                );
                Assert.That(
                    line.sharedMaterial.GetColor("_Color"),
                    Is.EqualTo((Color)new Color32(46, 255, 77, 255))
                );
            }
        }

        [Test]
        public void Advance_Hitscan_RendersAndExpiresBeamWithoutSavedProjectile()
        {
            CombatUnit shooter = _battle.GetParticipants()[0].GetCombatants()[0];
            ProjectileData projectile = shooter
                .Data.GetHardpointGroups()[0]
                .GetHardpoints()[0]
                .Weapon.Projectile;
            projectile.DeliveryMode = WeaponDeliveryMode.Hitscan;
            projectile.EffectDurationSeconds = 0.2f;

            _combat.Advance(1f / 64f);
            _view.Advance(1f / 64f, false);

            Assert.IsEmpty(_battle.Projectiles);
            Assert.IsNotEmpty(_scene.GetComponentsInChildren<LineRenderer>());
            _view.Advance(1f, false);
            _view.Advance(1f / 64f, false);
            Assert.IsEmpty(_scene.GetComponentsInChildren<LineRenderer>());
        }

        /// <summary>
        /// Verifies scene presentation does not independently move the simulation's projectile.
        /// </summary>
        [Test]
        public void Advance_PresentationOnly_DoesNotAdvanceProjectileState()
        {
            _combat.Advance(0.125f);
            float position = _battle.Projectiles[0].Position.Z;
            _view.Advance(1f, false);
            Assert.That(_battle.Projectiles[0].Position.Z, Is.EqualTo(position));
        }

        /// <summary>
        /// Verifies every owned voice observes the application's sound volume.
        /// </summary>
        [Test]
        public void Advance_CurrentVolume_UsesApplicationSettings()
        {
            _view.Advance(0.1f, false);
            foreach (AudioSource source in _scene.GetComponentsInChildren<AudioSource>())
                Assert.That(
                    source.volume,
                    Is.EqualTo(0.1f * _audio.MasterVolume * _audio.SfxVolume)
                );
        }

        /// <summary>
        /// Verifies destruction releases materials and unsubscribes event callbacks before subsequent simulation work.
        /// </summary>
        [Test]
        public void Dispose_ActivePresentation_ReleasesSceneOwnedEffects()
        {
            _combat.Advance(0.125f);
            _view.Advance(0.125f, false);
            _view.Dispose();
            Assert.That(_scene.GetComponentsInChildren<LineRenderer>(), Is.Empty);
            Assert.That(_scene.GetComponentsInChildren<AudioSource>(), Is.Empty);
            Assert.DoesNotThrow(() => _combat.Advance(2f));
        }
    }
}
