using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalFlightAudioTests
    {
        private GameObject _scene;
        private AudioClip _clip;
        private AudioManager _audio;
        private bool _ownsAudio;
        private TacticalFlightAudio _view;

        /// <summary>
        /// Creates isolated scene resources using the application's existing audio manager.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _scene = new GameObject("Flight Audio Test");
            _clip = AudioClip.Create("Flight Cue", 441, 1, 44100, false);
            _ownsAudio = AudioManager.Instance == null;
            _audio = AudioManager.EnsureExists();
            _view = new TacticalFlightAudio(_scene.transform, _clip, _audio);
        }

        /// <summary>
        /// Releases scene-owned sources before the shared application audio resources.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _view.Dispose();
            Object.DestroyImmediate(_scene);
            Object.DestroyImmediate(_clip);
            if (_ownsAudio && _audio != null)
                Object.DestroyImmediate(_audio.gameObject);
        }

        /// <summary>
        /// Checks launch cues use a bounded set of nonpositional sources without playback on creation.
        /// </summary>
        [Test]
        public void Constructor_LaunchSound_CreatesFiveNonpositionalSources()
        {
            AudioSource[] sources = _scene.GetComponentsInChildren<AudioSource>();
            Assert.That(sources, Has.Length.EqualTo(5));
            foreach (AudioSource source in sources)
            {
                Assert.That(source.clip, Is.SameAs(_clip));
                Assert.That(source.spatialBlend, Is.Zero);
                Assert.IsFalse(source.playOnAwake);
                Assert.IsFalse(source.isPlaying);
            }
        }

        /// <summary>
        /// Checks the flight effects follow application master and effect volumes.
        /// </summary>
        [Test]
        public void Advance_CurrentSettings_UsesApplicationVolumes()
        {
            _view.Advance(0.1f);
            foreach (AudioSource source in _scene.GetComponentsInChildren<AudioSource>())
                Assert.That(source.volume, Is.EqualTo(_audio.MasterVolume * _audio.SfxVolume));
        }

        /// <summary>
        /// Checks disposing a scene never destroys its application-owned sound clip.
        /// </summary>
        [Test]
        public void Dispose_ResidentClip_ReleasesSourcesAndKeepsClip()
        {
            _view.Dispose();
            Assert.That(_scene.GetComponentsInChildren<AudioSource>(), Is.Empty);
            Assert.IsTrue(_clip != null);
        }

        /// <summary>
        /// Checks invalid elapsed time cannot corrupt the sound queue's real-time clock.
        /// </summary>
        [Test]
        public void Advance_NegativeElapsed_ThrowsArgumentOutOfRangeException() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => _view.Advance(-1f));
    }
}
