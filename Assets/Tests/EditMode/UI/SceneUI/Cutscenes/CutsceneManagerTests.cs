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
        private const string _prefabPath = "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab";
        private const string _clipPath = "Assets/Resources/Videos/intro.mp4";

        private VideoClip _clip;
        private CutsceneManager _manager;
        private GameObject _secondaryObject;

        [SetUp]
        public void SetUp()
        {
            _manager = UIComponentTestHelper.InstantiatePrefabComponent<CutsceneManager>(
                _prefabPath
            );
            UIComponentTestHelper.InvokeLifecycle(_manager, "Awake");
            _clip = AssetDatabase.LoadAssetAtPath<VideoClip>(_clipPath);
        }

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
                UnityEngine.Object.DestroyImmediate(_manager.gameObject);
            }

            if (_secondaryObject != null)
                UnityEngine.Object.DestroyImmediate(_secondaryObject);

            Time.timeScale = 1f;
        }

        [Test]
        public void Awake_AuthoredPrefab_AssignsSingleton()
        {
            Assert.AreSame(_manager, CutsceneManager.Instance);
        }

        [Test]
        public void Awake_MissingCutscenePrefab_ThrowsMissingReferenceException()
        {
            SetField("cutscenePrefab", null);

            MissingReferenceException exception = Assert.Throws<MissingReferenceException>(() =>
                UIComponentTestHelper.InvokeLifecycle(_manager, "Awake")
            );

            Assert.AreEqual($"{_manager.name}/CutscenePrefab is missing.", exception.Message);
        }

        [Test]
        public void Awake_SecondManager_ThrowsInvalidOperationException()
        {
            _secondaryObject = new GameObject("SecondaryCutsceneManager");
            CutsceneManager secondary = _secondaryObject.AddComponent<CutsceneManager>();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                UIComponentTestHelper.InvokeLifecycle(secondary, "Awake")
            );

            Assert.AreEqual("Only one CutsceneManager may be active.", exception.Message);
        }

        [Test]
        public void Play_NullClip_InvokesCompletionWithoutChangingTimeScale()
        {
            int completedCount = 0;
            Time.timeScale = 0.75f;

            _manager.Play(null, () => completedCount++);

            Assert.AreEqual(1, completedCount);
            Assert.AreEqual(0.75f, Time.timeScale);
        }

        [Test]
        public void Play_ValidClip_PausesTimeAndCreatesPlayer()
        {
            _manager.Play(_clip, null);
            CutscenePlayer player = GetField<CutscenePlayer>("activePlayer");

            Assert.IsNotNull(player);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreSame(_clip, player.GetComponent<VideoPlayer>().clip);
        }

        [Test]
        public void OnDestroy_ActivePlayback_RestoresPreviousTimeScale()
        {
            Time.timeScale = 0.75f;
            _manager.Play(_clip, null);

            UIComponentTestHelper.InvokeLifecycle(_manager, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(_manager.gameObject);
            _manager = null;

            Assert.AreEqual(0.75f, Time.timeScale);
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
        public void OnDestroy_OwnedSingleton_ClearsInstance()
        {
            UIComponentTestHelper.InvokeLifecycle(_manager, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(_manager.gameObject);
            _manager = null;

            Assert.IsNull(CutsceneManager.Instance);
        }

        [Test]
        public void OnDestroy_Nonowner_PreservesSingleton()
        {
            _secondaryObject = new GameObject("SecondaryCutsceneManager");
            CutsceneManager secondary = _secondaryObject.AddComponent<CutsceneManager>();

            UIComponentTestHelper.InvokeLifecycle(secondary, "OnDestroy");

            Assert.AreSame(_manager, CutsceneManager.Instance);
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(CutsceneManager)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_manager);
        }

        private void SetField(string fieldName, object value)
        {
            typeof(CutsceneManager)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_manager, value);
        }
    }
}
