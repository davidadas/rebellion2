using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class MenuInputFieldTests
    {
        private GameObject _canvas;
        private GameObject _events;
        private MenuInputField _input;

        /// <summary>
        /// Instantiates the shared chat input on a canvas with normal input focus processing.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _events = new GameObject("InputEvents", typeof(EventSystem));
            _canvas = new GameObject("InputCanvas", typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Common/MenuChatPanel.prefab"
            );
            MenuInputField template = prefab.GetComponentInChildren<MenuInputField>(true);
            _input = Object.Instantiate(template, _canvas.transform);
            _input.gameObject.SetActive(true);
            _input.caretBlinkRate = 0;
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Releases the focused input and its event system.
        /// </summary>
        /// <returns>The play-mode cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_canvas);
            Object.Destroy(_events);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActivateInputField_EmptyValue_HidesPlaceholder()
        {
            EventSystem.current.SetSelectedGameObject(_input.gameObject);
            _input.ActivateInputField();
            yield return null;

            Assert.IsTrue(_input.isFocused);
            Assert.IsFalse(_input.placeholder.enabled);
            Assert.IsEmpty(_input.text);
        }

        [UnityTest]
        public IEnumerator DeactivateInputField_EmptyValue_RestoresPlaceholder()
        {
            EventSystem.current.SetSelectedGameObject(_input.gameObject);
            _input.ActivateInputField();
            yield return null;

            EventSystem.current.SetSelectedGameObject(null);
            _input.DeactivateInputField();
            yield return null;

            Assert.IsTrue(_input.placeholder.enabled);
            Assert.IsEmpty(_input.text);
        }

        [UnityTest]
        public IEnumerator SetTextWithoutNotify_ClearedWhileFocused_KeepsPlaceholderHidden()
        {
            _input.text = "Draft";
            EventSystem.current.SetSelectedGameObject(_input.gameObject);
            _input.ActivateInputField();
            yield return null;

            _input.SetTextWithoutNotify(string.Empty);
            yield return null;

            Assert.IsTrue(_input.isFocused);
            Assert.IsFalse(_input.placeholder.enabled);
        }

        [UnityTest]
        public IEnumerator Rebuild_FocusedText_AlignsCaretWithTextMetrics()
        {
            _input.text = "Message";
            EventSystem.current.SetSelectedGameObject(_input.gameObject);
            _input.ActivateInputField();
            yield return null;
            _input.MoveTextEnd(false);
            _input.ForceLabelUpdate();
            _input.textComponent.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            _input.Rebuild(CanvasUpdate.LatePreRender);

            CanvasRenderer caret = _input
                .GetComponentInChildren<TMP_SelectionCaret>()
                .GetComponent<CanvasRenderer>();
            Mesh mesh = caret.GetMesh();
            Assert.AreEqual(4, mesh.vertexCount);
            Vector3[] vertices = mesh
                .vertices.Select(vertex =>
                    _input.textComponent.transform.InverseTransformPoint(
                        caret.transform.TransformPoint(vertex)
                    )
                )
                .ToArray();
            TMP_CharacterInfo character = _input.textComponent.textInfo.characterInfo[
                _input.text.Length - 1
            ];
            Assert.AreEqual(character.descender, vertices.Min(vertex => vertex.y), 0.1f);
            Assert.AreEqual(character.ascender, vertices.Max(vertex => vertex.y), 0.1f);
        }
    }
}
