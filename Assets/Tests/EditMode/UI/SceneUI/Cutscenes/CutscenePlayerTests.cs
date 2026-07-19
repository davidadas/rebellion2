using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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
        private VideoPlayer _videoPlayer;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _player = _rootObject.GetComponent<CutscenePlayer>();
            _screen = GetField<RawImage>("screen");
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
            Assert.IsFalse(_audioSource.playOnAwake);
        }

        [Test]
        public void Play_ValidClip_ConfiguresVideoAndAudioOutput()
        {
            _player.Play(_clip, null);

            Assert.AreSame(_clip, _videoPlayer.clip);
            Assert.AreEqual(VideoAudioOutputMode.AudioSource, _videoPlayer.audioOutputMode);
            Assert.AreSame(_audioSource, _videoPlayer.GetTargetAudioSource(0));
        }

        [Test]
        public void EndCutscene_RepeatedTermination_InvokesCompletionOnce()
        {
            int completedCount = 0;
            _player.Play(_clip, () => completedCount++);

            Invoke("EndCutscene");
            Invoke("EndCutscene");

            Assert.AreEqual(1, completedCount);
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(CutscenePlayer)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_player);
        }

        private void Invoke(string methodName)
        {
            MethodInfo method = typeof(CutscenePlayer).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            try
            {
                method.Invoke(_player, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }
    }
}
