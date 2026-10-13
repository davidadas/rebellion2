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
            Assert.IsNotNull(prefab.transform.Find("ShuttleRig/ModelRoot"));
            Assert.IsNotNull(
                prefab.transform.Find("ShuttleRig/CameraOrbit/Camera").GetComponent<Camera>()
            );
            Assert.IsNull(prefab.transform.Find("Background/TransitLabel"));
            Assert.IsNull(prefab.transform.Find("Background/DetailTextField"));

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

            _instance.ShowLoadFailure();

            Assert.AreEqual("LOAD FAILED\nRETURNING TO MENU...", status.text);

            _instance.PrepareEntrance();

            Assert.AreEqual("Loading ...", status.text);
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
