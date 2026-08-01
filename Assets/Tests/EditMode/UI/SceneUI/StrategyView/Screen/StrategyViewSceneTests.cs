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
                    prefabRoot.transform.Find("Viewport") as RectTransform;

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
                StrategyController controller = prefabRoot.GetComponent<StrategyController>();
                CanvasGroup contentGroup = prefabRoot.GetComponent<CanvasGroup>();
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
        public void AuthoredPrefab_FitsCenteredSixteenByNineViewport()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(_prefabPath);

            try
            {
                AspectRatioFitter fitter = prefabRoot.GetComponent<AspectRatioFitter>();
                Assert.IsNotNull(fitter);
                Assert.AreEqual(AspectRatioFitter.AspectMode.FitInParent, fitter.aspectMode);
                Assert.AreEqual(16f / 9f, fitter.aspectRatio, 0.0001f);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static CanvasScaler GetSceneCanvasScaler(Scene scene)
        {
            Transform gameRoot = scene
                .GetRootGameObjects()
                .Single(root => root.name == "GameRoot")
                .transform;
            CanvasScaler canvasScaler = gameRoot.Find("UI/Canvas").GetComponent<CanvasScaler>();

            return canvasScaler
                ?? throw new InvalidOperationException("Strategy View CanvasScaler is missing.");
        }
    }
}
