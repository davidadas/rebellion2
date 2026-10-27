using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
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
            Canvas canvas = prefab.GetComponentInChildren<Canvas>();
            Assert.IsNotNull(canvas);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.AreEqual(short.MaxValue, canvas.sortingOrder);
            Assert.IsNotNull(
                prefab
                    .transform.Find("Canvas/Background/HyperspaceField")
                    .GetComponent<HyperspaceStarfieldGraphic>()
            );
            Assert.IsNotNull(
                prefab.transform.Find("Canvas/Background").GetComponent<CanvasGroup>()
            );
            Transform fadeOverlay = prefab.transform.Find("Canvas/FadeOverlay");
            Assert.IsNotNull(fadeOverlay.GetComponent<Image>());
            Assert.IsNotNull(fadeOverlay.GetComponent<CanvasGroup>());
            Transform shuttleViewport = prefab.transform.Find("Canvas/Background/ShuttleViewport");
            RawImage shuttleViewportImage = shuttleViewport.GetComponent<RawImage>();
            Assert.IsNotNull(shuttleViewportImage);
            Assert.IsFalse(shuttleViewportImage.raycastTarget);
            Transform commandShade = prefab.transform.Find("Canvas/Background/BottomCommandShade");
            Assert.Less(commandShade.GetSiblingIndex(), shuttleViewport.GetSiblingIndex());
            Assert.IsNull(prefab.transform.Find("Canvas/Background/LoadingHud"));
            Transform continueButton = prefab.transform.Find("Canvas/Background/ContinueButton");
            Assert.IsNotNull(continueButton);
            Assert.IsNotNull(continueButton.GetComponent<Button>());
            Assert.IsFalse(continueButton.gameObject.activeSelf);
            Assert.IsNotNull(prefab.transform.Find("ShuttleRig/ModelRoot"));
            AudioSource flightAudio = prefab.GetComponent<AudioSource>();
            Assert.IsNotNull(flightAudio);
            Assert.IsTrue(flightAudio.loop);
            Assert.IsFalse(flightAudio.playOnAwake);
            Assert.AreEqual(0f, flightAudio.spatialBlend);
            Assert.IsNotNull(
                prefab.transform.Find("ShuttleRig/CameraOrbit/Camera").GetComponent<Camera>()
            );
            Assert.IsNull(prefab.transform.Find("Canvas/Background/TransitLabel"));
            Assert.IsNull(prefab.transform.Find("Canvas/Background/DetailTextField"));
            Assert.IsNotNull(prefab.transform.Find("Canvas/Background/FailureDetailTextField"));

            Transform status = prefab.transform.Find("Canvas/Background/StatusTextField");
            Assert.IsNotNull(status);
            Assert.AreEqual("LOADING, PLEASE WAIT...", status.GetComponent<TextMeshProUGUI>().text);
        }

        [Test]
        public void GeneratedPrefab_ShuttleCamera_UsesCloseFlightFraming()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            Camera camera = prefab
                .transform.Find("ShuttleRig/CameraOrbit/Camera")
                .GetComponent<Camera>();

            Assert.AreEqual(new Vector3(0f, 0.08f, -6.4f), camera.transform.localPosition);
            Assert.AreEqual(32f, camera.fieldOfView);
        }

        [Test]
        public void GeneratedPrefab_MainMenuPlanetsPresent_ExcludesThemFromShuttleCamera()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab"
            );
            Camera camera = prefab
                .transform.Find("ShuttleRig/CameraOrbit/Camera")
                .GetComponent<Camera>();

            foreach (
                Renderer renderer in menu
                    .transform.Find("PlanetRig")
                    .GetComponentsInChildren<Renderer>(true)
            )
                Assert.That(
                    camera.cullingMask & (1 << renderer.gameObject.layer),
                    Is.Zero,
                    renderer.name
                );
        }

        [Test]
        public void GeneratedPrefab_MainMenuLightingPresent_ExcludesLightsFromOtherPresentation()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab"
            );
            Camera shuttleCamera = prefab
                .transform.Find("ShuttleRig/CameraOrbit/Camera")
                .GetComponent<Camera>();
            Camera planetCamera = menu.transform.Find("PlanetRig/Camera").GetComponent<Camera>();

            foreach (Light light in menu.GetComponentsInChildren<Light>(true))
                if (light.type == LightType.Directional)
                    Assert.That(light.cullingMask & shuttleCamera.cullingMask, Is.Zero, light.name);
            foreach (Light light in prefab.GetComponentsInChildren<Light>(true))
                Assert.That(light.cullingMask & planetCamera.cullingMask, Is.Zero, light.name);
        }

        [Test]
        public void PrepareEntrance_TransitionLayers_StartOverOutgoingScene()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            CanvasGroup presentation = _instance
                .transform.Find("Canvas/Background")
                .GetComponent<CanvasGroup>();
            CanvasGroup fade = _instance
                .transform.Find("Canvas/FadeOverlay")
                .GetComponent<CanvasGroup>();

            _instance.PrepareEntrance();

            Assert.IsTrue(_instance.gameObject.activeSelf);
            Assert.AreEqual(0f, presentation.alpha);
            Assert.AreEqual(0f, fade.alpha);

            _instance.SetBlackOpacity(1f);
            Assert.IsTrue(fade.blocksRaycasts);
            _instance.SetPresentationVisible(true);

            Assert.AreEqual(1f, presentation.alpha);
            Assert.AreEqual(1f, fade.alpha);

            _instance.SetBlackOpacity(0f);

            Assert.IsFalse(fade.blocksRaycasts);
        }

        [Test]
        public void ShowLoadFailure_ThenPrepareEntrance_RestoresLoadingStatus()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            TextMeshProUGUI status = _instance
                .transform.Find("Canvas/Background/StatusTextField")
                .GetComponent<TextMeshProUGUI>();

            _instance.ShowLoadFailure("Required content is unavailable.");

            Assert.AreEqual("LOAD FAILED\nRETURNING TO MENU...", status.text);
            Assert.AreEqual(
                "Required content is unavailable.",
                _instance
                    .transform.Find("Canvas/Background/FailureDetailTextField")
                    .GetComponent<TextMeshProUGUI>()
                    .text
            );

            _instance.PrepareEntrance();

            Assert.AreEqual("LOADING, PLEASE WAIT...", status.text);
            Assert.IsFalse(
                _instance
                    .transform.Find("Canvas/Background/FailureDetailTextField")
                    .gameObject.activeSelf
            );
        }

        [Test]
        public void ShowReady_LoadCompleted_ShowsContinuePrompt()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            Button continueButton = _instance
                .transform.Find("Canvas/Background/ContinueButton")
                .GetComponent<Button>();
            Assert.IsTrue(continueButton.targetGraphic.raycastTarget);
            TextMeshProUGUI status = _instance
                .transform.Find("Canvas/Background/StatusTextField")
                .GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI continueLabel =
                continueButton.GetComponentInChildren<TextMeshProUGUI>();
            CanvasGroup fade = _instance
                .transform.Find("Canvas/FadeOverlay")
                .GetComponent<CanvasGroup>();

            _instance.ShowReady();

            Assert.AreEqual(string.Empty, status.text);
            Assert.AreEqual("CONTINUE", continueLabel.text);
            Assert.AreEqual(
                "PRESS ENTER",
                continueButton.transform.Find("KeyboardHint").GetComponent<TextMeshProUGUI>().text
            );
            Assert.IsNotNull(continueButton.transform.Find("OuterGlow"));
            Assert.IsNotNull(continueButton.transform.Find("Frame"));
            Assert.IsNotNull(continueButton.transform.Find("Panel"));
            Assert.IsNotNull(continueButton.transform.Find("LeftAccent"));
            Assert.IsNotNull(continueButton.transform.Find("RightAccent"));
            Assert.AreEqual(Selectable.Transition.None, continueButton.transition);
            Assert.IsNotNull(continueButton.GetComponent<LoadingContinueButtonFeedback>());
            Assert.IsTrue(continueButton.gameObject.activeSelf);
            Assert.IsTrue(continueButton.interactable);
            Assert.IsFalse(fade.blocksRaycasts);
        }

        [Test]
        public void ShowTerminalFailure_UnrecoverableFailure_ShowsQuitAction()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            TextMeshProUGUI status = _instance
                .transform.Find("Canvas/Background/StatusTextField")
                .GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI detail = _instance
                .transform.Find("Canvas/Background/FailureDetailTextField")
                .GetComponent<TextMeshProUGUI>();
            Button continueButton = _instance
                .transform.Find("Canvas/Background/ContinueButton")
                .GetComponent<Button>();

            _instance.ShowTerminalFailure("Replacement services could not be created.");

            Assert.AreEqual("APPLICATION RECOVERY FAILED", status.text);
            Assert.AreEqual("Replacement services could not be created.", detail.text);
            Assert.AreEqual(
                "QUIT",
                continueButton.transform.Find("Label").GetComponent<TextMeshProUGUI>().text
            );
            Assert.IsTrue(continueButton.gameObject.activeSelf);
            Assert.IsTrue(continueButton.interactable);
        }

        [Test]
        public void ShowReady_ContinueButtonClicked_RaisesContinueRequest()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            Button continueButton = _instance
                .transform.Find("Canvas/Background/ContinueButton")
                .GetComponent<Button>();
            bool requested = false;
            _instance.ContinueRequested += () => requested = true;

            _instance.ShowReady();
            continueButton.onClick.Invoke();

            Assert.IsTrue(requested);
            Assert.IsFalse(continueButton.interactable);
        }

        [UnityTest]
        public IEnumerator ShowReady_ContinueButtonClicked_KeepsLoadingStatusHidden()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            Button continueButton = _instance
                .transform.Find("Canvas/Background/ContinueButton")
                .GetComponent<Button>();
            TextMeshProUGUI status = _instance
                .transform.Find("Canvas/Background/StatusTextField")
                .GetComponent<TextMeshProUGUI>();

            _instance.ShowReady();
            continueButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(string.Empty, status.text);
        }

        [UnityTest]
        public IEnumerator ContinueButton_PointerEntered_BrightensWithoutResizingControl()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            _instance.ShowReady();
            Transform continueTransform = _instance.transform.Find(
                "Canvas/Background/ContinueButton"
            );
            LoadingContinueButtonFeedback feedback =
                continueTransform.GetComponent<LoadingContinueButtonFeedback>();
            Image frame = continueTransform.Find("Frame").GetComponent<Image>();
            float initialScale = continueTransform.localScale.x;
            Color initialFrameColor = frame.color;

            feedback.OnPointerEnter(null);
            yield return null;

            Assert.AreEqual(initialScale, continueTransform.localScale.x);
            Assert.Greater(frame.color.grayscale, initialFrameColor.grayscale);
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
            Transform engineRoot = null;
            int exhaustPlumeCount = 0;
            foreach (Transform child in modelRoot.GetComponentsInChildren<Transform>())
            {
                if (child.name == "ion-engine_39")
                    engineRoot = child;
                if (child.name.StartsWith("EnginePlume", System.StringComparison.Ordinal))
                {
                    exhaustPlumeCount++;
                    Assert.AreEqual(engineRoot, child.parent);
                    Assert.AreEqual(
                        "Custom/LoadingShuttleExhaust",
                        child.GetComponent<MeshRenderer>().sharedMaterial.shader.name
                    );
                }
            }
            Assert.IsNotNull(engineRoot);
            Assert.AreEqual(4, exhaustPlumeCount);
            Assert.IsEmpty(modelRoot.GetComponentsInChildren<ParticleSystem>());
            Assert.IsEmpty(modelRoot.GetComponentsInChildren<Light>());
        }

        [UnityTest]
        public IEnumerator Initialize_MainMenuPlanetCameraPresent_ExcludesShuttleAndExhaust()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            _instance.Initialize(Path.Combine(Application.dataPath, "Content"));
            Transform modelRoot = _instance.transform.Find("ShuttleRig/ModelRoot");
            float deadline = Time.realtimeSinceStartup + 10f;
            while (
                modelRoot.GetComponentsInChildren<Renderer>(true).Length == 0
                && Time.realtimeSinceStartup < deadline
            )
                yield return null;
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab"
            );
            Camera planetCamera = menu.transform.Find("PlanetRig/Camera").GetComponent<Camera>();
            Camera shuttleCamera = _instance
                .transform.Find("ShuttleRig/CameraOrbit/Camera")
                .GetComponent<Camera>();

            foreach (Renderer renderer in renderers)
            {
                int layerMask = 1 << renderer.gameObject.layer;
                Assert.That(shuttleCamera.cullingMask & layerMask, Is.Not.Zero, renderer.name);
                Assert.That(planetCamera.cullingMask & layerMask, Is.Zero, renderer.name);
            }
        }

        [UnityTest]
        public IEnumerator Initialize_HiddenPresentation_NormalizesCompleteModel()
        {
            yield return VerifyModelFraming(true, 1f);
        }

        [UnityTest]
        public IEnumerator Initialize_ScaledCanvas_PreservesCameraDistanceAndModelSize()
        {
            yield return VerifyModelFraming(false, 2f);
        }

        [UnityTest]
        public IEnumerator Initialize_ApplicationFlightAudioExists_LoadsLoopingReactor()
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            _instance.Initialize(Path.Combine(Application.dataPath, "Content"));

            AudioSource flightAudio = _instance.GetComponent<AudioSource>();
            Transform modelRoot = _instance.transform.Find("ShuttleRig/ModelRoot");
            float timeout = Time.realtimeSinceStartup + 10f;
            while (
                (
                    flightAudio.clip == null
                    || modelRoot.GetComponentsInChildren<Renderer>().Length == 0
                )
                && Time.realtimeSinceStartup < timeout
            )
            {
                yield return null;
            }

            Assert.IsNotNull(flightAudio.clip);
            Assert.AreEqual("shuttle-reactor", flightAudio.clip.name);
            Assert.IsTrue(flightAudio.loop);
        }

        /// <summary>
        /// Verifies that loading and resizing the UI preserve the shuttle's physical framing.
        /// </summary>
        /// <param name="hidden">Whether the model loads while its presentation is hidden.</param>
        /// <param name="canvasScale">The initial UI scale to apply before loading.</param>
        /// <returns>The asynchronous framing verification sequence.</returns>
        private IEnumerator VerifyModelFraming(bool hidden, float canvasScale)
        {
            LoadingScreenView prefab = Resources.Load<LoadingScreenView>("UI/LoadingScreen");
            _instance = Object.Instantiate(prefab);
            CanvasScaler scaler = _instance.GetComponentInChildren<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = canvasScale;
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (hidden)
                _instance.SetVisible(false);
            _instance.Initialize(Path.Combine(Application.dataPath, "Content"));

            Transform modelRoot = _instance.transform.Find("ShuttleRig/ModelRoot");
            float timeout = Time.realtimeSinceStartup + 10f;
            while (
                modelRoot.GetComponentsInChildren<Renderer>(true).Length == 0
                && Time.realtimeSinceStartup < timeout
            )
                yield return null;
            Assert.IsNotEmpty(modelRoot.GetComponentsInChildren<Renderer>(true));
            _instance.SetVisible(true);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Bounds bounds = default;
            bool found = false;
            foreach (Renderer renderer in modelRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.StartsWith("EnginePlume", System.StringComparison.Ordinal))
                    continue;
                if (found)
                    bounds.Encapsulate(renderer.bounds);
                else
                    bounds = renderer.bounds;
                found = true;
            }
            Camera camera = _instance
                .transform.Find("ShuttleRig/CameraOrbit/Camera")
                .GetComponent<Camera>();
            float cameraDepth = camera
                .transform.InverseTransformDirection(bounds.center - camera.transform.position)
                .z;
            float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            TestContext.WriteLine(
                $"Hidden={hidden}; canvas={canvasScale}; rigScale={modelRoot.lossyScale}; bounds={bounds.size}; cameraDepth={cameraDepth}; modelScale={modelRoot.GetChild(0).localScale}"
            );
            Assert.That(largestDimension, Is.EqualTo(2.05f).Within(0.03f));
            Assert.That(cameraDepth, Is.EqualTo(6.4f).Within(0.03f));
            Transform rig = modelRoot.parent;
            Transform fillLight = rig.Find("FillLight");
            float fillDistance = Vector3.Distance(fillLight.position, rig.position);
            Assert.That(fillDistance, Is.EqualTo(Mathf.Sqrt(13.5f)).Within(0.01f));

            Vector3 cameraPosition = camera.transform.position;
            Vector3 lightPosition = fillLight.position;
            Vector3 modelScale = modelRoot.lossyScale;
            scaler.scaleFactor = 3f;
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(fillLight.position, Is.EqualTo(lightPosition));
            Assert.That(modelRoot.lossyScale, Is.EqualTo(modelScale));
        }
    }
}
