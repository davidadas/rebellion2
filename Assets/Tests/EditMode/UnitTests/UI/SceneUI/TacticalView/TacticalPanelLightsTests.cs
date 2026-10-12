using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalPanelLightsTests
    {
        private GameObject _instance;

        /// <summary>Runs actual lamp lifecycle in an isolated play-mode scene.</summary>
        /// <returns>The play-mode transition.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _instance = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/TacticalView/TacticalViewRoot.prefab"
                )
            );
            _instance.GetComponent<TacticalViewController>().enabled = false;
        }

        /// <summary>Releases the isolated scene.</summary>
        /// <returns>The edit-mode transition.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_instance);
            yield return new ExitPlayMode();
        }

        /// <summary>Checks lamps dip occasionally rather than flashing the complete bank together.</summary>
        /// <returns>The frame following the sampled presentation interval.</returns>
        [UnityTest]
        public IEnumerator Render_TwentySeconds_DimsIndividualLampsOccasionally()
        {
            TacticalPanelLights bank = _instance.GetComponentInChildren<TacticalPanelLights>();
            bank.enabled = false;
            Image[] lamps = bank.GetComponentsInChildren<Image>();
            Color[] steady = lamps.Select(lamp => lamp.color).ToArray();
            int changedSamples = 0;
            for (int sample = 0; sample < 200; sample++)
            {
                bank.Render(sample * 0.1f);
                int changed = lamps.Where((lamp, i) => lamp.color != steady[i]).Count();
                Assert.That(changed, Is.LessThan(lamps.Length));
                if (changed > 0)
                    changedSamples++;
            }
            Assert.That(changedSamples, Is.GreaterThan(0).And.LessThan(100));
            yield return null;
        }

        /// <summary>Checks decorative timing never consumes the game's random stream.</summary>
        /// <returns>The frame following the timing update.</returns>
        [UnityTest]
        public IEnumerator Render_ChangingTime_DoesNotConsumeUnityRandom()
        {
            Random.State state = Random.state;
            _instance.GetComponentInChildren<TacticalPanelLights>().Render(120f);
            Assert.AreEqual(state, Random.state);
            yield return null;
        }
    }
}
