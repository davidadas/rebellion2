using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.MainMenu
{
    [TestFixture]
    public sealed class AnalyticsConsentViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab";

        private GameObject _instance;
        private GameObject _eventSystemObject;
        private EventSystem _eventSystem;
        private AnalyticsConsentView _view;
        private Button _allowButton;
        private CanvasGroup _canvasGroup;

        /// <summary>
        /// Enters play mode and instantiates the authored consent overlay with an event system.
        /// </summary>
        /// <returns>The play-mode setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"Missing test prefab at {_prefabPath}.");

            _eventSystemObject = new GameObject("EventSystemUnderTest", typeof(EventSystem));
            _eventSystem = _eventSystemObject.GetComponent<EventSystem>();
            GameObject overlayPrefab = prefab
                .transform.Find("UI/AnalyticsConsentCanvas")
                .gameObject;
            _instance = UnityEngine.Object.Instantiate(overlayPrefab);
            _view = _instance.GetComponent<AnalyticsConsentView>();
            _canvasGroup = _instance.GetComponent<CanvasGroup>();
            _allowButton = _instance
                .transform.Find("ConsentPage/AllowButton")
                .GetComponent<Button>();
        }

        /// <summary>
        /// Destroys the instantiated consent overlay and leaves play mode.
        /// </summary>
        /// <returns>The play-mode cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _view.Render(false);
            UnityEngine.Object.Destroy(_instance);
            UnityEngine.Object.Destroy(_eventSystemObject);
            yield return null;
            yield return new ExitPlayMode();
        }

        [Test]
        public void Render_HiddenPromptSubmit_DoesNotAllowAnalytics()
        {
            int allowedCount = 0;
            _view.Allowed += () => allowedCount++;

            _view.Render(true);
            _allowButton.OnSubmit(new BaseEventData(_eventSystem));

            Assert.IsFalse(_canvasGroup.interactable);
            Assert.AreEqual(0, allowedCount);
        }
    }
}
