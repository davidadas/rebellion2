using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace Rebellion.Tests.UI.SceneUI.Cutscenes
{
    [TestFixture]
    public class CutsceneManagerTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/Cutscenes/CutscenePlayer.prefab";
        private const string _clipPath =
            "Assets/Tests/EditMode/UnitTests/UI/SceneUI/Cutscenes/CutsceneTestClip.webm";

        private VideoClip _clip;
        private CutsceneManager _manager;
        private GameObject _managerObject;
        private GameObject _playerPrefab;
        private AudioManager _audioManager;
        private bool _previousAudioPause;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _previousAudioPause = AudioListener.pause;
            AudioListener.pause = false;
            _playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            _managerObject = new GameObject("CutsceneManager");
            _manager = _managerObject.AddComponent<CutsceneManager>();
            _manager.Initialize(_playerPrefab);
            _audioManager = _managerObject.AddComponent<AudioManager>();
            _manager.InitializeAudio(_audioManager);
            _clip = AssetDatabase.LoadAssetAtPath<VideoClip>(_clipPath);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_manager != null)
            {
                CutscenePlayer player = GetField<CutscenePlayer>("activePlayer");
                if (player != null)
                {
                    UnityEngine.Object.DestroyImmediate(player.gameObject);
                    SetField("activePlayer", null);
                }

                UIComponentTestHelper.InvokeLifecycle(_manager, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(_managerObject);
            }

            Time.timeScale = 1f;
            AudioListener.pause = _previousAudioPause;
        }

        /// <summary>
        /// Verifies initialize null prefab throws argument null exception.
        /// </summary>
        [Test]
        public void Initialize_NullPrefab_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                _manager.Initialize(null)
            );

            Assert.AreEqual("prefab", exception.ParamName);
        }

        /// <summary>
        /// Verifies play null clip invokes completion without changing application state.
        /// </summary>
        [Test]
        public void Play_NullClip_InvokesCompletionWithoutChangingApplicationState()
        {
            int completedCount = 0;
            Time.timeScale = 0.75f;

            _manager.Play((VideoClip)null, () => completedCount++);

            Assert.AreEqual(1, completedCount);
            Assert.AreEqual(0.75f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
        }

        /// <summary>
        /// Verifies play valid clip pauses application and creates player.
        /// </summary>
        [Test]
        public void Play_ValidClip_PausesApplicationAndCreatesPlayer()
        {
            _manager.Play(_clip, null);
            CutscenePlayer player = GetField<CutscenePlayer>("activePlayer");

            Assert.IsNotNull(player);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(AudioListener.pause);
            Assert.AreSame(_clip, player.GetComponent<VideoPlayer>().clip);
        }

        /// <summary>
        /// Verifies play valid clip applies master scaled video volume.
        /// </summary>
        [Test]
        public void Play_ValidClip_AppliesMasterScaledVideoVolume()
        {
            _audioManager.SetMasterVolume(0.5f);
            _audioManager.SetVideoVolume(0.25f);

            _manager.Play(_clip, null);
            CutscenePlayer player = GetField<CutscenePlayer>("activePlayer");

            Assert.AreEqual(0.125f, player.GetComponent<AudioSource>().volume);
        }

        /// <summary>
        /// Verifies play replacement clip preserves initial time scale for restoration.
        /// </summary>
        [Test]
        public void Play_ReplacementClip_PreservesInitialTimeScaleForRestoration()
        {
            Time.timeScale = 0.75f;
            _manager.Play(_clip, null);
            CutscenePlayer firstPlayer = GetField<CutscenePlayer>("activePlayer");

            _manager.Play(_clip, null);
            CutscenePlayer secondPlayer = GetField<CutscenePlayer>("activePlayer");

            Assert.AreNotSame(firstPlayer, secondPlayer);
            Assert.AreEqual(0f, Time.timeScale);

            UIComponentTestHelper.InvokeLifecycle(_manager, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(_manager.gameObject);
            _manager = null;

            Assert.AreEqual(0.75f, Time.timeScale);
        }

        /// <summary>
        /// Verifies play replacement clip preserves initial audio pause for restoration.
        /// </summary>
        [Test]
        public void Play_ReplacementClip_PreservesInitialAudioPauseForRestoration()
        {
            AudioListener.pause = true;
            _manager.Play(_clip, null);
            _manager.Play(_clip, null);

            UIComponentTestHelper.InvokeLifecycle(_manager, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(_manager.gameObject);
            _manager = null;

            Assert.IsTrue(AudioListener.pause);
        }

        /// <summary>
        /// Verifies on destroy active playback restores previous application state.
        /// </summary>
        [Test]
        public void OnDestroy_ActivePlayback_RestoresPreviousApplicationState()
        {
            Time.timeScale = 0.75f;
            _manager.Play(_clip, null);

            UIComponentTestHelper.InvokeLifecycle(_manager, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(_manager.gameObject);
            _manager = null;

            Assert.AreEqual(0.75f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
        }

        /// <summary>
        /// Gets field.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The requested field.</returns>
        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(CutsceneManager)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_manager);
        }

        /// <summary>
        /// Sets field.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <param name="value">The value.</param>
        private void SetField(string fieldName, object value)
        {
            typeof(CutsceneManager)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_manager, value);
        }
    }
}
