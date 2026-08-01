using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Rebellion.Tests.UI.SceneUI.Cutscenes
{
    [TestFixture]
    public class CutscenePlayerTests
    {
        private const string _clipPath =
            "Assets/Tests/EditMode/UI/SceneUI/Cutscenes/CutsceneTestClip.webm";
        private const string _prefabPath = "Assets/Prefabs/UI/Cutscenes/CutscenePlayer.prefab";

        private AudioSource _audioSource;
        private VideoClip _clip;
        private CutscenePlayer _player;
        private GameObject _rootObject;
        private RawImage _screen;
        private Color _authoredScreenColor;
        private VideoPlayer _videoPlayer;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _player = _rootObject.GetComponent<CutscenePlayer>();
            _screen = GetField<RawImage>("screen");
            _authoredScreenColor = _screen.color;
            _videoPlayer = GetField<VideoPlayer>("videoPlayer");
            _audioSource = GetField<AudioSource>("audioSource");
            _clip = AssetDatabase.LoadAssetAtPath<VideoClip>(_clipPath);
            UIComponentTestHelper.InvokeLifecycle(_player, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (_rootObject != null)
                UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Awake_AuthoredPrefab_ConfiguresPlaybackComponents()
        {
            Assert.IsNotNull(_screen);
            Assert.IsNotNull(_videoPlayer);
            Assert.IsNotNull(_audioSource);
            Assert.IsFalse(_videoPlayer.playOnAwake);
            Assert.IsFalse(_videoPlayer.isLooping);
            Assert.IsFalse(_videoPlayer.sendFrameReadyEvents);
            Assert.IsFalse(_audioSource.playOnAwake);
            Assert.AreEqual(Color.black, _screen.color);
            Assert.IsTrue(_screen.raycastTarget);
        }

        [Test]
        public void Play_ValidClip_ConfiguresVideoAndAudioOutput()
        {
            _player.Play(_clip, null);

            Assert.AreSame(_clip, _videoPlayer.clip);
            Assert.AreEqual(VideoAudioOutputMode.AudioSource, _videoPlayer.audioOutputMode);
            Assert.AreSame(_audioSource, _videoPlayer.GetTargetAudioSource(0));
            Assert.IsTrue(_videoPlayer.sendFrameReadyEvents);
            Assert.AreEqual(Color.black, _screen.color);
        }

        [Test]
        public void Play_ValidUrl_ConfiguresUrlVideoSource()
        {
            const string videoUrl = "file:///tmp/cutscene.mp4";

#if UNITY_EDITOR_LINUX
            LogAssert.Expect(
                LogType.Error,
                new Regex("Can't create LinuxVideoMedia, format .* not supported")
            );
#endif
            _player.Play(videoUrl, null);

            Assert.AreEqual(VideoSource.Url, _videoPlayer.source);
            Assert.AreEqual(videoUrl, _videoPlayer.url);
            Assert.AreEqual(VideoAudioOutputMode.AudioSource, _videoPlayer.audioOutputMode);
            Assert.AreSame(_audioSource, _videoPlayer.GetTargetAudioSource(0));
            Assert.IsTrue(_videoPlayer.sendFrameReadyEvents);
            Assert.AreEqual(Color.black, _screen.color);
        }

        [Test]
        public void HandleFirstFrameReady_NewFrame_RevealsVideoScreen()
        {
            _player.Play(_clip, null);

            Invoke("HandleFirstFrameReady", _videoPlayer, 0L);

            Assert.AreEqual(_authoredScreenColor, _screen.color);
            Assert.IsFalse(_videoPlayer.sendFrameReadyEvents);
        }

        [Test]
        public void EndCutscene_RepeatedTermination_InvokesCompletionOnce()
        {
            int completedCount = 0;
            _player.Play(_clip, () => completedCount++);

            Invoke("EndCutscene");
            Invoke("EndCutscene");

            Assert.AreEqual(1, completedCount);
            Assert.AreEqual(Color.black, _screen.color);
            Assert.IsFalse(_videoPlayer.sendFrameReadyEvents);
        }

        [Test]
        public void OnDestroy_ActivePlayback_BlanksScreenAndReleasesFrameEvents()
        {
            int completedCount = 0;
            _player.Play(_clip, () => completedCount++);
            Invoke("HandleFirstFrameReady", _videoPlayer, 0L);

            Invoke("OnDestroy");

            Assert.AreEqual(0, completedCount);
            Assert.AreEqual(Color.black, _screen.color);
            Assert.IsFalse(_videoPlayer.sendFrameReadyEvents);
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(CutscenePlayer)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_player);
        }

        private void Invoke(string methodName, params object[] parameters)
        {
            MethodInfo method = typeof(CutscenePlayer).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            try
            {
                method.Invoke(_player, parameters);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }
    }
}
