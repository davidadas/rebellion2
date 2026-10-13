using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.LoadingScreen
{
    [TestFixture]
    public sealed class LoadingScreenViewTests
    {
        private LoadingScreenView _instance;

        /// <summary>
        /// Removes the instantiated loading view after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_instance != null)
                Object.DestroyImmediate(_instance.gameObject);
        }

        [Test]
        public void GeneratedPrefab_LoadedFromResources_ConfiguresLoadingPresentation()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");

            Assert.IsNotNull(prefab);
            Canvas canvas = prefab.GetComponent<Canvas>();
            Assert.IsNotNull(canvas);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.AreEqual(short.MaxValue, canvas.sortingOrder);
            Assert.IsNotNull(
                prefab
                    .transform.Find("Background/HyperspaceField")
                    .GetComponent<HyperspaceStarfieldGraphic>()
            );
            Assert.IsNotNull(prefab.transform.Find("Background").GetComponent<CanvasGroup>());
            Transform fadeOverlay = prefab.transform.Find("FadeOverlay");
            Assert.IsNotNull(fadeOverlay.GetComponent<Image>());
            Assert.IsNotNull(fadeOverlay.GetComponent<CanvasGroup>());
            Transform shuttleViewport = prefab.transform.Find("Background/ShuttleViewport");
            Assert.IsNotNull(shuttleViewport.GetComponent<RawImage>());
            Transform commandShade = prefab.transform.Find("Background/BottomCommandShade");
            Assert.Less(commandShade.GetSiblingIndex(), shuttleViewport.GetSiblingIndex());
            Assert.IsNotNull(
                prefab
                    .transform.Find("Background/LoadingHud")
                    .GetComponent<HyperspaceLoadingHudGraphic>()
            );
            Transform continueButton = prefab.transform.Find("Background/ContinueButton");
            Assert.IsNotNull(continueButton);
            Assert.IsNotNull(continueButton.GetComponent<Button>());
            Assert.IsFalse(continueButton.gameObject.activeSelf);
            Assert.IsNotNull(prefab.transform.Find("ShuttleRig/ModelRoot"));
            Assert.IsNotNull(
                prefab.transform.Find("ShuttleRig/CameraOrbit/Camera").GetComponent<Camera>()
            );
            Assert.IsNull(prefab.transform.Find("Background/TransitLabel"));
            Assert.IsNull(prefab.transform.Find("Background/DetailTextField"));
            Assert.IsNotNull(prefab.transform.Find("Background/FailureDetailTextField"));

            Transform status = prefab.transform.Find("Background/StatusTextField");
            Assert.IsNotNull(status);
            Assert.AreEqual("Loading ...", status.GetComponent<TextMeshProUGUI>().text);
        }

        [Test]
        public void PrepareEntrance_TransitionLayers_StartOverOutgoingScene()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            CanvasGroup presentation = _instance
                .transform.Find("Background")
                .GetComponent<CanvasGroup>();
            CanvasGroup fade = _instance.transform.Find("FadeOverlay").GetComponent<CanvasGroup>();

            _instance.PrepareEntrance();

            Assert.IsTrue(_instance.gameObject.activeSelf);
            Assert.AreEqual(0f, presentation.alpha);
            Assert.AreEqual(0f, fade.alpha);

            _instance.SetBlackOpacity(1f);
            _instance.SetPresentationVisible(true);

            Assert.AreEqual(1f, presentation.alpha);
            Assert.AreEqual(1f, fade.alpha);
        }

        [Test]
        public void ShowLoadFailure_ThenPrepareEntrance_RestoresLoadingStatus()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            TextMeshProUGUI status = _instance
                .transform.Find("Background/StatusTextField")
                .GetComponent<TextMeshProUGUI>();

            _instance.ShowLoadFailure("Required content is unavailable.");

            Assert.AreEqual("LOAD FAILED\nRETURNING TO MENU...", status.text);
            Assert.AreEqual(
                "Required content is unavailable.",
                _instance
                    .transform.Find("Background/FailureDetailTextField")
                    .GetComponent<TextMeshProUGUI>()
                    .text
            );

            _instance.PrepareEntrance();

            Assert.AreEqual("Loading ...", status.text);
            Assert.IsFalse(
                _instance.transform.Find("Background/FailureDetailTextField").gameObject.activeSelf
            );
        }

        [Test]
        public void ShowReady_LoadCompleted_ShowsContinuePrompt()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            Button continueButton = _instance
                .transform.Find("Background/ContinueButton")
                .GetComponent<Button>();
            TextMeshProUGUI status = _instance
                .transform.Find("Background/StatusTextField")
                .GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI continueLabel =
                continueButton.GetComponentInChildren<TextMeshProUGUI>();
            CanvasGroup fade = _instance.transform.Find("FadeOverlay").GetComponent<CanvasGroup>();

            _instance.ShowReady();

            Assert.AreEqual("GAME READY", status.text);
            Assert.AreEqual("PRESS ENTER TO CONTINUE", continueLabel.text);
            Assert.IsTrue(continueButton.gameObject.activeSelf);
            Assert.IsTrue(continueButton.interactable);
            Assert.IsFalse(fade.blocksRaycasts);
        }

        [Test]
        public void ShowReady_ContinueButtonClicked_RaisesContinueRequest()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            Button continueButton = _instance
                .transform.Find("Background/ContinueButton")
                .GetComponent<Button>();
            bool requested = false;
            _instance.ContinueRequested += () => requested = true;

            _instance.ShowReady();
            continueButton.onClick.Invoke();

            Assert.IsTrue(requested);
            Assert.IsFalse(continueButton.interactable);
        }

        [UnityTest]
        public IEnumerator Initialize_ApplicationShuttleExists_LoadsRenderableModel()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            _instance.Initialize(Path.Combine(Application.dataPath, "Content"));

            Transform modelRoot = _instance.transform.Find("ShuttleRig/ModelRoot");
            float timeout = Time.realtimeSinceStartup + 10f;
            while (
                modelRoot.GetComponentsInChildren<Renderer>().Length == 0
                && Time.realtimeSinceStartup < timeout
            )
            {
                yield return null;
            }

            Assert.IsNotEmpty(modelRoot.GetComponentsInChildren<Renderer>());
            int exhaustPlumeCount = 0;
            foreach (Transform child in modelRoot.GetComponentsInChildren<Transform>())
            {
                if (child.name.StartsWith("EnginePlume", System.StringComparison.Ordinal))
                    exhaustPlumeCount++;
            }
            Assert.AreEqual(4, exhaustPlumeCount);
            Assert.IsEmpty(modelRoot.GetComponentsInChildren<ParticleSystem>());
            Assert.IsEmpty(modelRoot.GetComponentsInChildren<Light>());
        }
    }
}
