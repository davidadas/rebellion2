using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalCommandButtonTests
    {
        private GameObject _instance;

        /// <summary>
        /// Enters an isolated scene so actual Unity button lifecycle runs.
        /// </summary>
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
            yield return null;
        }

        /// <summary>
        /// Releases the generated scene and restores edit mode.
        /// </summary>
        /// <returns>The edit-mode transition.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_instance);
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks every command key depresses and restores through actual pointer handlers.
        /// </summary>
        /// <returns>The frame allowing transition feedback to settle.</returns>
        [UnityTest]
        public IEnumerator OnPointerDown_AllCommands_DepressesAndRestoresFace()
        {
            TacticalCommandButton[] buttons =
                _instance.GetComponentsInChildren<TacticalCommandButton>();
            Assert.AreEqual(9, buttons.Length);
            foreach (TacticalCommandButton button in buttons)
            {
                Assert.IsTrue(Application.isPlaying, "Pointer feedback requires play mode.");
                Assert.IsTrue(button.IsActive(), button.name);
                Assert.IsTrue(button.IsInteractable(), button.name);
                RectTransform face = (RectTransform)button.transform.Find("KeyFace");
                Vector2 released = face.anchoredPosition;
                PointerEventData pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                };
                button.OnPointerEnter(pointer);
                button.OnPointerDown(pointer);
                Assert.That(face.anchoredPosition.y, Is.EqualTo(released.y - 2f));
                button.OnPointerUp(pointer);
                Assert.AreEqual(released, face.anchoredPosition);
            }
            yield return null;
        }

        /// <summary>
        /// Checks placeholder clicks have no persistent callback or game-state side effect.
        /// </summary>
        /// <returns>The frame following the click.</returns>
        [UnityTest]
        public IEnumerator OnPointerClick_PlaceholderCommand_DoesNotCreateBattleState()
        {
            foreach (
                TacticalCommandButton button in _instance.GetComponentsInChildren<TacticalCommandButton>()
            )
            {
                Assert.AreEqual(0, button.onClick.GetPersistentEventCount());
                button.OnPointerClick(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    }
                );
            }
            Assert.IsNull(_instance.GetComponent<TacticalViewController>().ActiveBattle);
            yield return null;
        }

        /// <summary>
        /// Checks disabled controls do not display a physical key depression.
        /// </summary>
        /// <returns>The frame following the input.</returns>
        [UnityTest]
        public IEnumerator OnPointerDown_DisabledKey_DoesNotDepressFace()
        {
            TacticalCommandButton button =
                _instance.GetComponentInChildren<TacticalCommandButton>();
            RectTransform face = (RectTransform)button.transform.Find("KeyFace");
            Vector2 position = face.anchoredPosition;
            button.interactable = false;
            button.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }
            );
            Assert.AreEqual(position, face.anchoredPosition);
            yield return null;
        }
    }
}
