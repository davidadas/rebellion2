using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesChatComposerViewTests
    {
        private GameObject _window;
        private MessagesChatComposerView _view;
        private TMP_InputField _input;
        private Button _send;

        /// <summary>
        /// Opens the authored composer through its public rendering contract.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _window = UIComponentTestHelper.InstantiatePrefab(
                "Assets/Prefabs/UI/StrategyView/MessagesWindow.prefab"
            );
            _view = _window.GetComponentInChildren<MessagesChatComposerView>(true);
            _input = _view.GetComponentInChildren<TMP_InputField>(true);
            _send = _view
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "ChatSendButton");
            _view.Render(new MessagesChatComposerRenderData(string.Empty, true, false, null));
        }

        /// <summary>
        /// Releases the instantiated window and controls.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_window);
        }

        [Test]
        public void SendRequested_EmptyDraft_DoesNotSubmit()
        {
            int submissions = 0;
            _view.SendRequested += () => submissions++;

            _send.onClick.Invoke();

            Assert.IsFalse(_send.interactable);
            Assert.AreEqual(0, submissions);
        }

        [Test]
        public void SendRequested_WhitespaceDraft_PreservesAndSubmitsRawText()
        {
            int submissions = 0;
            string changedDraft = null;
            _view.SendRequested += () => submissions++;
            _view.DraftChanged += rawText => changedDraft = rawText;
            _input.text = " \n ";

            _send.onClick.Invoke();

            Assert.IsTrue(_send.interactable);
            Assert.AreEqual(" \n ", changedDraft);
            Assert.AreEqual(1, submissions);
        }

        [Test]
        public void Render_RefreshWithSameDraft_PreservesTextSelection()
        {
            _view.Render(
                new MessagesChatComposerRenderData("A multiline\ndraft", true, false, null)
            );
            _input.selectionStringAnchorPosition = 2;
            _input.selectionStringFocusPosition = 7;

            _view.Render(
                new MessagesChatComposerRenderData("A multiline\ndraft", false, false, null)
            );

            Assert.AreEqual(2, _input.selectionStringAnchorPosition);
            Assert.AreEqual(7, _input.selectionStringFocusPosition);
        }

        [Test]
        public void Render_RawMarkup_DisplaysLiteralDraft()
        {
            _view.Render(new MessagesChatComposerRenderData("<b>Hello</b>", true, false, null));

            Assert.AreEqual("<b>Hello</b>", _input.text);
            Assert.IsFalse(_input.richText);
            Assert.IsFalse(_input.textComponent.richText);
        }

        [Test]
        public void ProcessEvent_Return_InsertsNewlineWithoutSubmitting()
        {
            int submissions = 0;
            _view.SendRequested += () => submissions++;
            _input.text = "Hello";
            _input.stringPosition = _input.text.Length;

            _input.ProcessEvent(
                new Event
                {
                    type = EventType.KeyDown,
                    keyCode = KeyCode.Return,
                    character = '\n',
                }
            );

            Assert.AreEqual("Hello\n", _input.text);
            Assert.AreEqual(0, submissions);
        }

        [Test]
        public void Render_PendingSubmission_DisablesRepeatedSend()
        {
            _view.Render(new MessagesChatComposerRenderData("Hello", true, true, null));

            Assert.IsFalse(_send.interactable);
            Assert.IsTrue(_input.readOnly);
        }
    }
}
