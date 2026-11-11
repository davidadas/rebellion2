using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Screen
{
    [TestFixture]
    public class StrategyViewSceneTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";
        private const string _galacticInformationDisplayPrefabPath =
            "Assets/Prefabs/UI/StrategyView/GalacticInformationDisplay.prefab";
        private const string _scenePath = "Assets/Scenes/StrategyView.unity";

        [Test]
        public void AuthoredScene_CanvasScaler_FitsCompleteStrategySurface()
        {
            Scene scene = EditorSceneManager.OpenScene(_scenePath, OpenSceneMode.Additive);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(_prefabPath);

            try
            {
                CanvasScaler canvasScaler = GetSceneCanvasScaler(scene);
                RectTransform strategySurface =
                    prefabRoot.transform.Find("UI/Canvas/StrategyView/Viewport") as RectTransform;

                Assert.IsNotNull(strategySurface);
                Assert.AreEqual(
                    strategySurface.sizeDelta.x,
                    canvasScaler.referenceResolution.x,
                    0.0001f
                );
                Assert.AreEqual(
                    strategySurface.sizeDelta.y,
                    canvasScaler.referenceResolution.y,
                    0.0001f
                );
                Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, canvasScaler.screenMatchMode);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void AuthoredPrefab_StrategyController_ReferencesRootContentGroup()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(_prefabPath);

            try
            {
                StrategyController controller =
                    prefabRoot.GetComponentInChildren<StrategyController>(true);
                CanvasGroup contentGroup = controller.GetComponent<CanvasGroup>();
                CanvasGroup assignedGroup = (CanvasGroup)
                    typeof(StrategyController)
                        .GetField("contentGroup", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(controller);

                Assert.IsNotNull(contentGroup);
                Assert.AreSame(contentGroup, assignedGroup);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [Test]
        public void AuthoredPrefab_TacticalBattleButtons_TakeNoKeyboardNavigation()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(_prefabPath);

            try
            {
                TacticalBattleView view = prefabRoot.GetComponentInChildren<TacticalBattleView>(
                    true
                );
                Button[] buttons = view.GetComponentsInChildren<Button>(true);
                string[] navigable = buttons
                    .Where(button => button.navigation.mode != Navigation.Mode.None)
                    .Select(button => button.name)
                    .ToArray();

                // The battle reads Space, Enter, WASD and the arrows itself; a button that also
                // took them through uGUI navigation would be pressed by the same key.
                Assert.GreaterOrEqual(buttons.Length, 9);
                Assert.IsEmpty(navigable);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [Test]
        public void AuthoredPrefab_TacticalBattleRig_CarriesItsMaterialsAndItsOwnAmbientVolume()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(_prefabPath);

            try
            {
                TacticalBattleScene scene = prefabRoot.GetComponentInChildren<TacticalBattleScene>(
                    true
                );
                Material lit = scene.LitMaterial;
                Material particle = scene.ParticleMaterial;
                UnityEngine.Rendering.Volume ambient = prefabRoot
                    .GetComponentsInChildren<UnityEngine.Rendering.Volume>(true)
                    .Single(volume => volume.name == "TacticalBattleAmbientVolume");
                int cameraVolumeLayers = scene
                    .SceneCamera.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>()
                    .volumeLayerMask.value;

                // Materials referenced as assets ship with a player build, shader variants and
                // all; the pipeline's default material does not exist outside the editor.
                Assert.IsNotNull(lit);
                Assert.IsNotNull(particle);
                Assert.IsNotEmpty(AssetDatabase.GetAssetPath(lit));
                Assert.IsNotEmpty(AssetDatabase.GetAssetPath(particle));
                Assert.IsTrue(lit.IsKeywordEnabled("_DOUBLESIDED_ON"));
                Assert.IsTrue(particle.IsKeywordEnabled("_ALPHATEST_ON"));
                Assert.AreEqual("TacticalBattleAmbientVolumeProfile", ambient.sharedProfile.name);
                Assert.AreNotEqual(0, cameraVolumeLayers & (1 << ambient.gameObject.layer));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [Test]
        public void AuthoredPrefab_StrategyController_ReferencesHiddenTacticalBattleLayer()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(_prefabPath);

            try
            {
                StrategyController controller =
                    prefabRoot.GetComponentInChildren<StrategyController>(true);
                TacticalBattleView assignedView = (TacticalBattleView)
                    typeof(StrategyController)
                        .GetField("tacticalBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(controller);
                TacticalBattleView authoredView =
                    prefabRoot.GetComponentInChildren<TacticalBattleView>(true);

                Assert.IsNotNull(authoredView);
                Assert.AreSame(authoredView, assignedView);
                Assert.IsFalse(authoredView.gameObject.activeSelf);
                Assert.IsNotNull(authoredView.GetComponentInChildren<TacticalUnitView>(true));
                Assert.Greater(authoredView.GetArenaSourceRect().height, 0);

                TacticalBattleScene scene = prefabRoot.GetComponentInChildren<TacticalBattleScene>(
                    true
                );
                Assert.IsNotNull(scene);
                Assert.AreSame(scene, authoredView.Scene);
                Assert.IsFalse(scene.gameObject.activeSelf);
                Assert.IsFalse(scene.transform.IsChildOf(authoredView.transform));
                Assert.IsFalse(scene.SceneCamera.orthographic);
                Assert.AreEqual(
                    1 << PresentationLayers.TacticalBattle,
                    scene.SceneCamera.cullingMask
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [Test]
        public void LoadStarting_AuthoredPrefab_ConcealsCompleteStrategyPresentation()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(_prefabPath);

            try
            {
                StrategyController controller =
                    prefabRoot.GetComponentInChildren<StrategyController>(true);
                CanvasGroup contentGroup = controller.GetComponent<CanvasGroup>();
                MethodInfo handler = typeof(StrategyController).GetMethod(
                    "HandleLoadStarting",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );

                Assert.IsNotNull(handler);
                handler.Invoke(controller, null);

                Assert.AreEqual(0f, contentGroup.alpha);
                Assert.IsFalse(contentGroup.interactable);
                Assert.IsFalse(contentGroup.blocksRaycasts);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [Test]
        public void AuthoredPrefab_Default_FitsCenteredSixteenByNineViewport()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(_prefabPath);

            try
            {
                AspectRatioFitter fitter = prefabRoot.GetComponentInChildren<AspectRatioFitter>(
                    true
                );
                Assert.IsNotNull(fitter);
                Assert.AreEqual(AspectRatioFitter.AspectMode.FitInParent, fitter.aspectMode);
                Assert.AreEqual(16f / 9f, fitter.aspectRatio, 0.0001f);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [Test]
        public void AuthoredPrefab_GalacticInformationDisplay_IsStoredOutsideSceneHierarchy()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(_prefabPath);

            try
            {
                Assert.IsNotNull(
                    AssetDatabase.LoadAssetAtPath<GalacticInformationDisplayView>(
                        _galacticInformationDisplayPrefabPath
                    )
                );
                Assert.IsNull(
                    prefabRoot.GetComponentInChildren<GalacticInformationDisplayView>(true)
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// Gets scene canvas scaler.
        /// </summary>
        /// <param name="scene">The scene.</param>
        /// <returns>The requested scene canvas scaler.</returns>
        private static CanvasScaler GetSceneCanvasScaler(Scene scene)
        {
            Transform gameRoot = scene
                .GetRootGameObjects()
                .Single(root => root.name == "StrategyViewRoot")
                .transform;
            CanvasScaler canvasScaler = gameRoot.Find("UI/Canvas").GetComponent<CanvasScaler>();

            return canvasScaler
                ?? throw new InvalidOperationException("Strategy View CanvasScaler is missing.");
        }
    }
}
