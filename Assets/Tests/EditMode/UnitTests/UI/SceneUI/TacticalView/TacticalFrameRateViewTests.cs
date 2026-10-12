using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalFrameRateViewTests
    {
        private GameObject _instance;
        private TacticalFrameRateView _counter;
        private float _previousTimeScale;

        /// <summary>Creates the generated counter using its normal Unity lifecycle.</summary>
        /// <returns>The play-mode transition.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _previousTimeScale = Time.timeScale;
            _instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/TacticalView/TacticalViewRoot.prefab"
                )
            );
            _instance.GetComponent<TacticalViewController>().enabled = false;
            _counter = _instance.GetComponentInChildren<TacticalFrameRateView>();
        }

        /// <summary>Restores presentation time and releases the isolated scene.</summary>
        /// <returns>The edit-mode transition.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _previousTimeScale;
            Object.DestroyImmediate(_instance);
            yield return new ExitPlayMode();
        }

        /// <summary>Checks the counter remains anchored to the top-right without intercepting input.</summary>
        /// <returns>The completed layout check.</returns>
        [UnityTest]
        public IEnumerator Transform_GeneratedCounter_AnchorsTopRight()
        {
            TMP_Text label = _counter.GetComponent<TMP_Text>();
            Assert.AreEqual(Vector2.one, label.rectTransform.anchorMin);
            Assert.AreEqual(Vector2.one, label.rectTransform.anchorMax);
            Assert.AreEqual(Vector2.one, label.rectTransform.pivot);
            Assert.AreEqual(new Vector2(-12f, -12f), label.rectTransform.anchoredPosition);
            Assert.IsFalse(label.raycastTarget);
            yield return null;
        }

        /// <summary>Checks the displayed frame rate continues updating when scaled time is paused.</summary>
        /// <returns>The unscaled sampling interval.</returns>
        [UnityTest]
        public IEnumerator Enabled_PausedTime_UpdatesFrameRate()
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.6f);

            string value = _counter.GetComponent<TMP_Text>().text;
            Assert.That(value, Does.Match("^[0-9]+ FPS$"));
        }
    }
}
