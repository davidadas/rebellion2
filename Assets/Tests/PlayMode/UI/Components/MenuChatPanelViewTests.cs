using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class MenuChatPanelViewTests
    {
        private GameObject _canvas;
        private GameObject _events;
        private MenuChatPanelView _panel;

        /// <summary>
        /// Instantiates the shared browser chat panel in an active canvas.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _events = new GameObject("ChatEvents", typeof(EventSystem));
            _canvas = new GameObject("ChatCanvas", typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Common/MenuChatPanel.prefab"
            );
            _panel = Object
                .Instantiate(template, _canvas.transform)
                .GetComponent<MenuChatPanelView>();
            _panel.SetLayout("GENERAL CHAT", 904, 174);
            _panel.SetTranscript(string.Empty);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Releases the chat panel and its input events between cases.
        /// </summary>
        /// <returns>The frame needed to destroy the test objects.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_canvas);
            Object.Destroy(_events);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetTranscript_AtBottom_FollowsIncomingMessagesAcrossFrames()
        {
            _panel.SetTranscript(CreateTranscript(30));
            yield return null;

            _panel.SetTranscript(CreateTranscript(31));
            _panel.SetTranscript(CreateTranscript(32));
            yield return SettleScroll();

            AssertAtBottom();
        }

        [UnityTest]
        public IEnumerator SetTranscript_ReadingHistory_PreservesVisibleOffset()
        {
            _panel.SetTranscript(CreateTranscript(30));
            _panel.ScrollArea.VerticalNormalizedPosition = 0.6f;
            float offset = _panel.ScrollArea.ContentRoot.anchoredPosition.y;

            _panel.SetTranscript(CreateTranscript(32));
            yield return SettleScroll();

            Assert.AreEqual(offset, _panel.ScrollArea.ContentRoot.anchoredPosition.y, 0.1f);
            Assert.IsFalse(_panel.ScrollArea.IsAtBottom);
        }

        [UnityTest]
        public IEnumerator SetTranscript_QueuedScrollUp_AllowsReadingOlderMessages()
        {
            _panel.SetTranscript(CreateTranscript(30));
            float offset = _panel.ScrollArea.ContentRoot.anchoredPosition.y;
            _panel.ScrollArea.RelayScroll(new PointerEventData(null) { scrollDelta = Vector2.up });

            _panel.SetTranscript(CreateTranscript(31));
            yield return SettleScroll();

            Assert.AreEqual(offset - 12f, _panel.ScrollArea.ContentRoot.anchoredPosition.y, 0.1f);
            Assert.IsFalse(_panel.ScrollArea.IsAtBottom);
        }

        [UnityTest]
        public IEnumerator ScrollToBottom_ReadingHistory_RevealsNewestAndFollowsNewMessages()
        {
            _panel.SetTranscript(CreateTranscript(30));
            _panel.ScrollArea.VerticalNormalizedPosition = 0.7f;
            _panel.ScrollArea.RelayScroll(new PointerEventData(null) { scrollDelta = Vector2.up });
            _panel.SetTranscript(CreateTranscript(31));

            _panel.ScrollToBottom();
            yield return SettleScroll();
            AssertAtBottom();

            _panel.SetTranscript(CreateTranscript(32));
            yield return SettleScroll();
            AssertAtBottom();
        }

        [UnityTest]
        public IEnumerator SetTranscript_FirstOverflow_FollowsNewestMessage()
        {
            _panel.SetTranscript("First message\n");
            yield return null;

            _panel.SetTranscript(CreateTranscript(20));
            yield return SettleScroll();

            AssertAtBottom();
        }

        [UnityTest]
        public IEnumerator SetTranscript_WrappedMessage_FitsEntireNewestLine()
        {
            _panel.SetLayout("GENERAL CHAT", 310, 174);
            _panel.SetTranscript(CreateTranscript(20));

            _panel.SetTranscript(
                CreateTranscript(20)
                    + string.Join(" ", Enumerable.Repeat("Wrapped message", 40))
                    + "\n"
            );
            yield return SettleScroll();

            AssertAtBottom();
            Assert.GreaterOrEqual(
                _panel.Transcript.rectTransform.rect.height + 0.1f,
                _panel.Transcript.preferredHeight
            );
        }

        /// <summary>
        /// Advances canvas layout and any queued smooth scrolling.
        /// </summary>
        /// <returns>The layout frames preceding the final scroll state.</returns>
        private IEnumerator SettleScroll()
        {
            for (int frame = 0; frame < 3; frame++)
                yield return null;
            for (int frame = 0; frame < 30; frame++)
                _panel.ScrollArea.AdvanceScroll(0.04f);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Checks that overflowing content remains pinned to its final line.
        /// </summary>
        private void AssertAtBottom()
        {
            float bottom =
                _panel.ScrollArea.ContentRoot.rect.height - _panel.ScrollArea.ViewportHeight;
            Assert.Greater(bottom, 0f, "The fixture must overflow the viewport.");
            Assert.AreEqual(bottom, _panel.ScrollArea.ContentRoot.anchoredPosition.y, 0.1f);
            Assert.IsTrue(_panel.ScrollArea.IsAtBottom);
        }

        /// <summary>
        /// Builds a deterministic multiline conversation for overflow checks.
        /// </summary>
        /// <param name="count">The number of messages to include.</param>
        /// <returns>The complete plain-text conversation.</returns>
        private static string CreateTranscript(int count) =>
            string.Join(
                "\n",
                Enumerable.Range(1, count).Select(index => "Player: Message " + index)
            ) + "\n";
    }
}
