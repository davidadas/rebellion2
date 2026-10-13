using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class ContentRestartLoadingViewTests
    {
        private ContentRestartLoadingView _instance;

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
            ContentRestartLoadingView prefab = Resources.Load<ContentRestartLoadingView>(
                "UI/ContentRestartLoadingView"
            );

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
            Assert.IsNotNull(
                prefab.transform.Find("Background/ShuttleViewport").GetComponent<RawImage>()
            );
            Assert.IsNotNull(prefab.transform.Find("ShuttleRig/ModelRoot"));
            Assert.IsNotNull(prefab.transform.Find("ShuttleRig/Camera").GetComponent<Camera>());

            Transform status = prefab.transform.Find("Background/StatusTextField");
            Assert.IsNotNull(status);
            Assert.AreEqual("Loading ...", status.GetComponent<TextMeshProUGUI>().text);
        }

        [UnityTest]
        public IEnumerator Initialize_ApplicationShuttleExists_LoadsRenderableModel()
        {
            ContentRestartLoadingView prefab = Resources.Load<ContentRestartLoadingView>(
                "UI/ContentRestartLoadingView"
            );
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
        }
    }
}
