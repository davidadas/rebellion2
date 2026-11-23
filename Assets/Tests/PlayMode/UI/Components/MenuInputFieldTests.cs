using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
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
            GameObject inputObject = new GameObject("Input", typeof(RectTransform), typeof(Image));
            inputObject.SetActive(false);
            inputObject.transform.SetParent(_canvas.transform, false);
            RectTransform viewport = (RectTransform)
                new GameObject(
                    "Text viewport",
                    typeof(RectTransform),
                    typeof(RectMask2D)
                ).transform;
            viewport.SetParent(inputObject.transform, false);
            viewport.sizeDelta = new Vector2(200, 30);
            _input = inputObject.AddComponent<MenuInputField>();
            _input.targetGraphic = inputObject.GetComponent<Image>();
            _input.textViewport = viewport;
            _input.textComponent = CreateLabel("Text", viewport);
            TextMeshProUGUI placeholder = CreateLabel("Placeholder", viewport);
            placeholder.text = "Type a message";
            _input.placeholder = placeholder;
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

        /// <summary>
        /// Creates a label that fills the input field's text viewport.
        /// </summary>
        /// <param name="name">The label object name.</param>
        /// <param name="parent">The input field's text viewport.</param>
        /// <returns>The configured label.</returns>
        private static TextMeshProUGUI CreateLabel(string name, RectTransform parent)
        {
            TextMeshProUGUI label = new GameObject(name).AddComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return label;
        }
    }
}
