using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class ContentEditorInputScrollRelayTests
    {
        private GameObject root;
        private ScrollAreaView scrollArea;
        private TMP_InputField input;
        private ContentEditorInputScrollRelay relay;

        /// <summary>
        /// Creates an input inside an independently scrollable form.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            root = UIComponentTestHelper.InstantiatePrefab(
                "Assets/Prefabs/UI/Common/MenuScrollArea.prefab"
            );
            scrollArea = root.GetComponent<ScrollAreaView>();
            input = UIComponentTestHelper.InstantiatePrefabComponent<TMP_InputField>(
                "Assets/Prefabs/UI/Common/TextInput.prefab"
            );
            input.transform.SetParent(scrollArea.ContentRoot, false);
            relay = input.gameObject.AddComponent<ContentEditorInputScrollRelay>();
            scrollArea.SetContentHeight(scrollArea.ViewportHeight + 200, 20, true);
        }

        /// <summary>
        /// Releases the isolated form and its input.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void OnScroll_InputInsideEditorScrollArea_MovesTheOuterForm()
        {
            relay.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(0, -1) });

            for (int frame = 0; frame < 30; frame++)
                scrollArea.AdvanceScroll(0.04f);

            Assert.Greater(scrollArea.ContentRoot.anchoredPosition.y, 0);
        }

        [Test]
        public void OnScroll_OverflowingMultilineInput_LeavesTheOuterFormStationary()
        {
            ((RectTransform)input.transform).sizeDelta = new Vector2(120, 40);
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.textComponent.textWrappingMode = TextWrappingModes.Normal;
            input.SetTextWithoutNotify(
                "A long multiline value that wraps through several lines inside the editor."
            );
            Canvas.ForceUpdateCanvases();
            input.textComponent.ForceMeshUpdate();
            Assert.Greater(input.textComponent.preferredHeight, input.textViewport.rect.height);

            relay.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(0, -1) });
            for (int frame = 0; frame < 30; frame++)
                scrollArea.AdvanceScroll(0.04f);

            Assert.AreEqual(0, scrollArea.ContentRoot.anchoredPosition.y);
        }
    }
}
