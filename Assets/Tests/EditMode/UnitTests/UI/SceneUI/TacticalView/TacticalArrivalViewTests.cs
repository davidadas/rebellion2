using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalArrivalViewTests
    {
        private GameObject _scene;
        private AudioClip _clip;
        private AudioManager _audio;
        private bool _ownsAudio;
        private TacticalArrivalView _view;
        private Dictionary<CombatUnit, Transform> _presentations;

        /// <summary>
        /// Creates an isolated presentation root and silent arrival sound.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _scene = new GameObject("Arrival Test Scene");
            _clip = AudioClip.Create("Arrival Test", 441, 1, 44100, false);
            _ownsAudio = AudioManager.Instance == null;
            _audio = AudioManager.EnsureExists();
            _presentations = new Dictionary<CombatUnit, Transform>();
        }

        /// <summary>
        /// Releases the effect before its models, audio clip, and owning scene.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _view?.Dispose();
            _view = null;
            Object.DestroyImmediate(_scene);
            Object.DestroyImmediate(_clip);
            if (_ownsAudio && _audio != null)
                Object.DestroyImmediate(_audio.gameObject);
        }

        /// <summary>
        /// Checks the visual starts behind its authored facing without changing saved coordinates.
        /// </summary>
        /// <param name="forwardZ">The authored forward direction.</param>
        [TestCase(1f)]
        [TestCase(-1f)]
        public void Constructor_DeployedUnit_OffsetsOnlyPresentation(float forwardZ)
        {
            CombatUnit unit = AddUnit(forwardZ);
            Transform root = _presentations[unit];

            _view = CreateView();

            Assert.That(
                Vector3.Distance(new Vector3(6f, 2f, 20f - forwardZ * 135f), root.localPosition),
                Is.LessThan(0.0001f)
            );
            Assert.AreEqual(20f, unit.Position.Z);
            Assert.AreEqual(forwardZ, unit.Forward.Z);
            Assert.IsTrue(_view.IsArriving(unit));
        }

        /// <summary>
        /// Checks the fly-in follows its quartic easing while saved positions remain unchanged.
        /// </summary>
        [Test]
        public void Advance_HalfwayThroughArrival_EasesTowardDeploymentPose()
        {
            CombatUnit unit = AddUnit();
            _view = CreateView();

            _view.Advance(0.72f + 0.43f, false);

            Assert.That(_presentations[unit].localPosition.z, Is.EqualTo(11.5625f).Within(0.0001f));
            Assert.AreEqual(20f, unit.Position.Z);
            Assert.IsTrue(_view.IsArriving(unit));
        }

        /// <summary>
        /// Checks pausing does not consume any of the arrival clock.
        /// </summary>
        [Test]
        public void Advance_Paused_DoesNotAdvanceFlyIn()
        {
            CombatUnit unit = AddUnit();
            _view = CreateView();
            Vector3 before = _presentations[unit].localPosition;

            _view.Advance(1f, true);
            Assert.AreEqual(before, _presentations[unit].localPosition);
            _view.Advance(0.72f, false);

            Assert.AreEqual(before, _presentations[unit].localPosition);
        }

        /// <summary>
        /// Checks a completed fly-in releases pose ownership and emits its endpoint burst.
        /// </summary>
        [Test]
        public void Advance_ArrivalFinishes_ReleasesPoseAndCreatesBurst()
        {
            CombatUnit unit = AddUnit();
            _view = CreateView();

            _view.Advance(1.58f, false);

            Assert.IsFalse(_view.IsArriving(unit));
            Assert.AreEqual(new Vector3(6f, 2f, 20f), _presentations[unit].localPosition);
            Assert.IsFalse(_presentations[unit].GetComponent<TrailRenderer>().emitting);
            Assert.That(_scene.GetComponentsInChildren<ParticleSystem>(), Has.Length.EqualTo(1));
        }

        /// <summary>
        /// Checks successive units receive the configured small arrival stagger.
        /// </summary>
        [Test]
        public void Advance_StaggeredUnits_FirstFinishesBeforeSecond()
        {
            CombatUnit first = AddUnit();
            CombatUnit second = AddUnit();
            _view = CreateView();

            _view.Advance(1.59f, false);

            Assert.IsFalse(_view.IsArriving(first));
            Assert.IsTrue(_view.IsArriving(second));
        }

        /// <summary>
        /// Checks the arrival sound follows the existing application volume settings.
        /// </summary>
        [Test]
        public void Advance_CurrentAudioSettings_UsesApplicationVolumes()
        {
            AddUnit();
            _view = CreateView();

            _view.Advance(0.1f, false);

            AudioSource source = _scene.GetComponentInChildren<AudioSource>();
            Assert.AreEqual(_audio.MasterVolume * _audio.SfxVolume, source.volume);
            Assert.AreEqual(0f, source.spatialBlend);
        }

        /// <summary>
        /// Checks leaving during a fly-in restores the pose and releases all owned resources.
        /// </summary>
        [Test]
        public void Dispose_UnfinishedArrival_RestoresPoseAndReleasesResources()
        {
            CombatUnit unit = AddUnit();
            _view = CreateView();
            Material material = _presentations[unit].GetComponent<TrailRenderer>().sharedMaterial;
            Texture texture = material.mainTexture;

            _view.Dispose();

            Assert.AreEqual(new Vector3(6f, 2f, 20f), _presentations[unit].localPosition);
            Assert.IsNull(_presentations[unit].GetComponent<TrailRenderer>());
            Assert.IsTrue(material == null);
            Assert.IsTrue(texture == null);
            Assert.IsNull(_scene.transform.Find("Hyperspace Arrival"));
            Assert.IsFalse(_view.IsArriving(unit));
        }

        /// <summary>
        /// Checks invalid presentation content leaves no partially constructed effect hierarchy.
        /// </summary>
        [Test]
        public void Constructor_InvalidEffectColor_ReleasesPartialResources()
        {
            CombatUnit unit = AddUnit();
            FactionThemeLibrary themes = CreateThemes("invalid");

            Assert.Throws<InvalidOperationException>(() => CreateView(themes));

            Assert.AreEqual(new Vector3(6f, 2f, 20f), _presentations[unit].localPosition);
            Assert.IsNull(_presentations[unit].GetComponent<TrailRenderer>());
            Assert.IsNull(_scene.transform.Find("Hyperspace Arrival"));
        }

        /// <summary>
        /// Checks units that already withdrew are not given a new arrival effect.
        /// </summary>
        [Test]
        public void Constructor_RetreatedUnit_DoesNotAnimateOrCreateTrail()
        {
            CombatUnit unit = AddUnit();
            unit.HasRetreated = true;

            _view = CreateView();

            Assert.IsFalse(_view.IsArriving(unit));
            Assert.AreEqual(new Vector3(6f, 2f, 20f), _presentations[unit].localPosition);
            Assert.IsNull(_presentations[unit].GetComponent<TrailRenderer>());
        }

        /// <summary>
        /// Creates a battle copy and its independently positioned visual root.
        /// </summary>
        /// <param name="forwardZ">The deployed heading.</param>
        /// <returns>The battle copy registered with the fixture.</returns>
        private CombatUnit AddUnit(float forwardZ = 1f)
        {
            CombatUnit unit = CombatUnit.Create(new CapitalShip { OwnerInstanceID = "FACTION" });
            unit.Position = new BattleVector3
            {
                X = 6f,
                Y = 2f,
                Z = 20f,
            };
            unit.Forward = new BattleVector3 { Z = forwardZ };
            unit.GetUnit().ModelSize = new ModelDimensions
            {
                Width = 4f,
                Height = 2f,
                Depth = 10f,
            };
            GameObject root = new GameObject("Unit");
            root.transform.SetParent(_scene.transform, false);
            root.transform.localPosition = new Vector3(6f, 2f, 20f);
            root.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, forwardZ));
            _presentations.Add(unit, root.transform);
            return unit;
        }

        /// <summary>
        /// Creates the presentation owner using only public dependencies.
        /// </summary>
        /// <param name="themes">Optional explicitly configured test themes.</param>
        /// <returns>The newly constructed arrival owner.</returns>
        private TacticalArrivalView CreateView(FactionThemeLibrary themes = null) =>
            new TacticalArrivalView(
                _scene.transform,
                _presentations,
                themes ?? CreateThemes(),
                _clip,
                _audio
            );

        /// <summary>
        /// Creates valid content colors without depending on a particular installed content pack.
        /// </summary>
        /// <param name="trailColor">The configured trail color.</param>
        /// <returns>The theme lookup used by arrival presentation.</returns>
        private static FactionThemeLibrary CreateThemes(string trailColor = "#33B3FFD1") =>
            new FactionThemeLibrary(
                new FactionThemes
                {
                    new FactionTheme { FactionInstanceID = "DEFAULT" },
                    new FactionTheme
                    {
                        FactionInstanceID = "FACTION",
                        BattleArrival = new BattleArrivalTheme
                        {
                            SoundPath = "Pack/Shared/Tactical/Audio/hyperspace-arrival.wav",
                            TrailColorHex = trailColor,
                            BurstColorHex = "#33B8FFFF",
                        },
                    },
                }
            );
    }
}
