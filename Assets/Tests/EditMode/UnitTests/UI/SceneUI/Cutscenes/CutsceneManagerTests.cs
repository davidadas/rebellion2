using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Video;

namespace Rebellion.Tests.UI.SceneUI.Cutscenes
{
    [TestFixture]
    public class CutsceneManagerTests
    {
        private const string _prefabPath = "Assets/Resources/UI/CutscenePlayer.prefab";
        private const string _clipPath =
            "Assets/Tests/EditMode/UnitTests/UI/SceneUI/Cutscenes/CutsceneTestClip.webm";

        private VideoClip _clip;
        private CutsceneManager _manager;
        private GameObject _managerObject;
        private GameObject _playerPrefab;
        private AudioManager _audioManager;
        private bool _previousAudioPause;
        private ContentAssets _ownedAssets;
        private string _ownedContentRoot;

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
            _ownedAssets?.Dispose();
            _ownedAssets = null;
            if (_ownedContentRoot != null && Directory.Exists(_ownedContentRoot))
                Directory.Delete(_ownedContentRoot, true);
            _ownedContentRoot = null;
        }

        [Test]
        public void Initialize_NullPrefab_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                _manager.Initialize(null)
            );

            Assert.AreEqual("prefab", exception.ParamName);
        }

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

        [Test]
        public void Play_ValidClip_NotifiesPresentationStart()
        {
            int startingCount = 0;
            _manager.PlaybackStarting += () => startingCount++;

            _manager.Play(_clip, null);

            Assert.AreEqual(1, startingCount);
        }

        [Test]
        public void Play_ValidClip_AppliesMasterScaledVideoVolume()
        {
            _audioManager.SetMasterVolume(0.5f);
            _audioManager.SetVideoVolume(0.25f);

            _manager.Play(_clip, null);
            CutscenePlayer player = GetField<CutscenePlayer>("activePlayer");

            Assert.AreEqual(0.125f, player.GetComponent<AudioSource>().volume);
        }

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

        [Test]
        public void Play_NullClipDuringActivePlayback_NotifiesPresentationFinish()
        {
            int finishedCount = 0;
            _manager.PlaybackFinished += () => finishedCount++;
            _manager.Play(_clip, null);

            _manager.Play((VideoClip)null, null);

            Assert.AreEqual(1, finishedCount);
        }

        [UnityTest]
        public IEnumerator PlayOwned_DisposedOwnership_RestoresApplicationState()
        {
            InitializeOwnedContent();
            Time.timeScale = 0.75f;
            IDisposable ownership = _manager.PlayOwned(
                "Application/Videos/blank",
                null,
                CutsceneSkipInput.EscapeOrPrimaryPointer
            );

            ownership.Dispose();

            Assert.AreEqual(0.75f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayOwned_StaleOwnershipDisposed_PreservesReplacementPlayback()
        {
            InitializeOwnedContent();
            IDisposable first = _manager.PlayOwned("Application/Videos/blank", null);
            using IDisposable replacement = _manager.PlayOwned("Application/Videos/blank", null);

            first.Dispose();

            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(AudioListener.pause);
            replacement.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayOwned_DisposedOwnership_DoesNotCompleteCancelledPlayback()
        {
            InitializeOwnedContent();
            int completions = 0;
            IDisposable ownership = _manager.PlayOwned(
                "Application/Videos/blank",
                () => completions++
            );

            ownership.Dispose();

            Assert.AreEqual(0, completions);
            yield return null;
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

        /// <summary>
        /// Creates an external synthetic black-and-silent video for scoped playback requests.
        /// </summary>
        private void InitializeOwnedContent()
        {
            _ownedContentRoot = Path.Combine(
                Path.GetTempPath(),
                "rebellion2-owned-video",
                Guid.NewGuid().ToString("N")
            );
            string videoDirectory = Path.Combine(_ownedContentRoot, "Application", "Videos");
            string packRoot = Path.Combine(_ownedContentRoot, "Pack");
            Directory.CreateDirectory(videoDirectory);
            Directory.CreateDirectory(packRoot);
            File.WriteAllBytes(
                Path.Combine(videoDirectory, "blank.mp4"),
                Convert.FromBase64String(
                    "AAAAIGZ0eXBpc29tAAACAGlzb21pc28yYXZjMW1wNDEAAAYWbW9vdgAAAGxtdmhkAAAAAAAAAAAAAAAAAAAD6AAAA+gAAQAAAQAAAAAAAAAAAA"
                        + "AAAAEAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAwAAAkN0cmFrAAAAXHRraGQA"
                        + "AAADAAAAAAAAAAAAAAABAAAAAAAAA+gAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAABAAAAAABAAAAAQAA"
                        + "AAAAAkZWR0cwAAABxlbHN0AAAAAAAAAAEAAAPoAAAAAAABAAAAAAG7bWRpYQAAACBtZGhkAAAAAAAAAAAAAAAAAABAAAAAQABVxAAAAAAALWhk"
                        + "bHIAAAAAAAAAAHZpZGUAAAAAAAAAAAAAAABWaWRlb0hhbmRsZXIAAAABZm1pbmYAAAAUdm1oZAAAAAEAAAAAAAAAAAAAACRkaW5mAAAAHGRyZW"
                        + "YAAAAAAAAAAQAAAAx1cmwgAAAAAQAAASZzdGJsAAAAwnN0c2QAAAAAAAAAAQAAALJhdmMxAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAABAAEABI"
                        + "AAAASAAAAAAAAAABFUxhdmM2Mi4yOC4xMDIgbGlieDI2NAAAAAAAAAAAAAAAGP//AAAAOGF2Y0MBZAAK/+EAG2dkAAqs2V7AWoEBAKAAAAMAIA"
                        + "AAAwBB4kSywAEABmjr48siwP34+AAAAAAQcGFzcAAAAAEAAAABAAAAFGJ0cnQAAAAAAAAWKAAAAAAAAAAYc3R0cwAAAAAAAAABAAAAAQAAQAAA"
                        + "AAAcc3RzYwAAAAAAAAABAAAAAQAAAAEAAAABAAAAFHN0c3oAAAAAAAACxQAAAAEAAAAUc3RjbwAAAAAAAAABAAAGWwAAAv10cmFrAAAAXHRraG"
                        + "QAAAADAAAAAAAAAAAAAAACAAAAAAAAA+gAAAAAAAAAAAAAAAEBAAAAAAEAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAA"
                        + "AAAAAAAkZWR0cwAAABxlbHN0AAAAAAAAAAEAAAPoAAAEAAABAAAAAAJ1bWRpYQAAACBtZGhkAAAAAAAAAAAAAAAAAACsRAAAsERVxAAAAAAALW"
                        + "hkbHIAAAAAAAAAAHNvdW4AAAAAAAAAAAAAAABTb3VuZEhhbmRsZXIAAAACIG1pbmYAAAAQc21oZAAAAAAAAAAAAAAAJGRpbmYAAAAcZHJlZgAA"
                        + "AAAAAAABAAAADHVybCAAAAABAAAB5HN0YmwAAAB+c3RzZAAAAAAAAAABAAAAbm1wNGEAAAAAAAAAAQAAAAAAAAAAAAEAEAAAAACsRAAAAAAANm"
                        + "VzZHMAAAAAA4CAgCUAAgAEgICAF0AVAAAAAAENiAAABgQFgICABRIIVuUABoCAgAECAAAAFGJ0cnQAAAAAAAENiAAABgQAAAAgc3R0cwAAAAAA"
                        + "AAACAAAALAAABAAAAAABAAAARAAAAChzdHNjAAAAAAAAAAIAAAABAAAAAQAAAAEAAAACAAAALAAAAAEAAADIc3RzegAAAAAAAAAAAAAALQAAAB"
                        + "UAAAAEAAAABAAAAAQAAAAEAAAABAAAAAQAAAAEAAAABAAAAAQAAAAEAAAABAAAAAQAAAAEAAAABAAAAAQAAAAEAAAABAAAAAQAAAAEAAAABAAA"
                        + "AAQAAAAEAAAABAAAAAQAAAAEAAAABAAAAAQAAAAEAAAABAAAAAQAAAAEAAAABAAAAAQAAAAEAAAABAAAAAQAAAAEAAAABAAAAAQAAAAEAAAABA"
                        + "AAAAQAAAAEAAAABAAAABhzdGNvAAAAAAAAAAIAAAZGAAAJIAAAABpzZ3BkAQAAAHJvbGwAAAACAAAAAf//AAAAHHNiZ3AAAAAAcm9sbAAAAAEA"
                        + "AAAtAAAAAQAAAGJ1ZHRhAAAAWm1ldGEAAAAAAAAAIWhkbHIAAAAAAAAAAG1kaXJhcHBsAAAAAAAAAAAAAAAALWlsc3QAAAAlqXRvbwAAAB1kYX"
                        + "RhAAAAAQAAAABMYXZmNjIuMTIuMTAyAAAACGZyZWUAAAOSbWRhdN4CAExhdmM2Mi4yOC4xMDIAAjBADgAAAq0GBf//qdxF6b3m2Ui3lizYINkj"
                        + "7u94MjY0IC0gY29yZSAxNjUgcjMyMjIgYjM1NjA1YSAtIEguMjY0L01QRUctNCBBVkMgY29kZWMgLSBDb3B5bGVmdCAyMDAzLTIwMjUgLSBodH"
                        + "RwOi8vd3d3LnZpZGVvbGFuLm9yZy94MjY0Lmh0bWwgLSBvcHRpb25zOiBjYWJhYz0xIHJlZj0zIGRlYmxvY2s9MTowOjAgYW5hbHlzZT0weDM6"
                        + "MHgxMTMgbWU9aGV4IHN1Ym1lPTcgcHN5PTEgcHN5X3JkPTEuMDA6MC4wMCBtaXhlZF9yZWY9MSBtZV9yYW5nZT0xNiBjaHJvbWFfbWU9MSB0cm"
                        + "VsbGlzPTEgOHg4ZGN0PTEgY3FtPTAgZGVhZHpvbmU9MjEsMTEgZmFzdF9wc2tpcD0xIGNocm9tYV9xcF9vZmZzZXQ9LTIgdGhyZWFkcz0xIGxv"
                        + "b2thaGVhZF90aHJlYWRzPTEgc2xpY2VkX3RocmVhZHM9MCBucj0wIGRlY2ltYXRlPTEgaW50ZXJsYWNlZD0wIGJsdXJheV9jb21wYXQ9MCBjb2"
                        + "5zdHJhaW5lZF9pbnRyYT0wIGJmcmFtZXM9MyBiX3B5cmFtaWQ9MiBiX2FkYXB0PTEgYl9iaWFzPTAgZGlyZWN0PTEgd2VpZ2h0Yj0xIG9wZW5f"
                        + "Z29wPTAgd2VpZ2h0cD0yIGtleWludD0yNTAga2V5aW50X21pbj0xIHNjZW5lY3V0PTQwIGludHJhX3JlZnJlc2g9MCByY19sb29rYWhlYWQ9ND"
                        + "AgcmM9Y3JmIG1idHJlZT0xIGNyZj0yMy4wIHFjb21wPTAuNjAgcXBtaW49MCBxcG1heD02OSBxcHN0ZXA9NCBpcF9yYXRpbz0xLjQwIGFxPTE6"
                        + "MS4wMACAAAAAEGWIhAAV//73ye/Apuvb34EBGCAHARggBwEYIAcBGCAHARggBwEYIAcBGCAHARggBwEYIAcBGCAHARggBwEYIAcBGCAHARggBw"
                        + "EYIAcBGCAHARggBwEYIAcBGCAHARggBwEYIAcBGCAHARggBwEYIAcBGCAHARggBwEYIAcBGCAHARggBwEYIAcBGCAHARggBwEYIAcBGCAHARgg"
                        + "BwEYIAcBGCAHARggBwEYIAcBGCAHARggBwEYIAcBGCAHARggBw=="
                )
            );
            _ownedAssets = new ContentAssets(new ContentFileResolver(_ownedContentRoot, packRoot));
            _manager.InitializeContent(_ownedAssets);
        }
    }
}
