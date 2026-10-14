using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Rebellion.Tests.UI.SceneUI.LoadingScreen
{
    [TestFixture]
    public sealed class LoadingScreenSceneTests
    {
        private const string _scenePath = "Assets/Scenes/LoadingScreen.unity";

        [Test]
        public void GeneratedScene_LoadingScreenRoot_UsesCurrentInteractivePrefab()
        {
            Scene scene = EditorSceneManager.OpenScene(_scenePath, OpenSceneMode.Additive);

            try
            {
                GameObject loadingRoot = scene
                    .GetRootGameObjects()
                    .Single(root => root.name == "LoadingScreen");
                GameObject loadingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/UI/LoadingScreen.prefab"
                );

                Assert.IsNotNull(loadingRoot.GetComponent<LoadingScreenView>());
                Assert.AreSame(
                    loadingPrefab,
                    PrefabUtility.GetCorrespondingObjectFromSource(loadingRoot)
                );
                Assert.IsNotNull(
                    loadingRoot
                        .transform.Find("Background/ContinueButton")
                        .GetComponent<LoadingContinueButtonFeedback>()
                );
                Assert.IsNotNull(
                    scene
                        .GetRootGameObjects()
                        .Single(root => root.name == "EventSystem")
                        .GetComponent<StandaloneInputModule>()
                );
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
