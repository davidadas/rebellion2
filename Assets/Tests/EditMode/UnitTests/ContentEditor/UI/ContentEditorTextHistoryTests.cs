using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorTextHistoryTests
    {
        private TMP_InputField input;
        private ContentEditorTextHistory history;

        private static EventModifiers ShortcutModifier =>
            Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer
                ? EventModifiers.Command
                : EventModifiers.Control;

        /// <summary>
        /// Binds text history to the shared input prefab with an initial authored value.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            input = UIComponentTestHelper.InstantiatePrefabComponent<TMP_InputField>(
                "Assets/Prefabs/UI/Common/TextInput.prefab"
            );
            history = input.gameObject.AddComponent<ContentEditorTextHistory>();
            input.SetTextWithoutNotify("Original");
            history.ClearHistory();
            input.onSelect.Invoke(input.text);
        }

        /// <summary>
        /// Releases the fixture input and its listeners.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(input.gameObject);
        }

        [Test]
        public void Undo_UncommittedPaste_RestoresTheTextAndSupportsRedo()
        {
            input.text = "Replacement text";
            Assert.IsTrue(history.Undo());
            Assert.AreEqual("Original", input.text);
            Assert.IsTrue(history.Redo());
            Assert.AreEqual("Replacement text", input.text);
        }

        [Test]
        public void Undo_SeveralDraftReplacements_RetainsEveryUndoAndRedoStep()
        {
            foreach (
                string text in new[]
                {
                    "First replacement",
                    "Second replacement",
                    "Third replacement",
                }
            )
                input.text = text;
            foreach (string text in new[] { "Second replacement", "First replacement", "Original" })
            {
                Assert.IsTrue(history.Undo());
                Assert.AreEqual(text, input.text);
            }
            foreach (
                string text in new[]
                {
                    "First replacement",
                    "Second replacement",
                    "Third replacement",
                }
            )
            {
                Assert.IsTrue(history.Redo());
                Assert.AreEqual(text, input.text);
            }
        }

        [Test]
        public void Undo_ReplacementAfterAnUndo_DiscardsTheAbandonedRedo()
        {
            input.text = "First replacement";
            history.Undo();
            input.text = "Different replacement";
            Assert.IsFalse(history.CanRedo);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual("Original", input.text);
        }

        [Test]
        public void Undo_RefocusingAnUncommittedDraft_RetainsItsEditingHistory()
        {
            input.text = "Uncommitted draft";
            input.onDeselect.Invoke(input.text);
            input.onSelect.Invoke(input.text);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual("Original", input.text);
            Assert.IsTrue(history.Redo());
            Assert.AreEqual("Uncommitted draft", input.text);
        }

        [Test]
        public void ClearHistory_ReusedField_CannotRestoreAnotherRecordsText()
        {
            input.text = "Unsaved draft";
            input.SetTextWithoutNotify("Another record");
            history.ClearHistory();
            Assert.IsFalse(history.Undo());
            Assert.IsFalse(history.Redo());
            Assert.AreEqual("Another record", input.text);
        }

        [Test]
        public void Undo_RestoringText_NotifiesTheOrdinaryInputConsumers()
        {
            input.text = "Draft";
            string observed = null;
            input.onValueChanged.AddListener(value => observed = value);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual("Original", observed);
        }

        [Test]
        public void Undo_NativePaste_ReversesTheWholeInsertionAndSupportsRedo()
        {
            string previousClipboard = GUIUtility.systemCopyBuffer;
            try
            {
                GUIUtility.systemCopyBuffer =
                    "<Officer><DisplayName>First pasted draft</DisplayName></Officer>";
                input.ProcessEvent(
                    new Event
                    {
                        type = EventType.KeyDown,
                        keyCode = KeyCode.A,
                        modifiers = ShortcutModifier,
                    }
                );
                input.ProcessEvent(
                    new Event
                    {
                        type = EventType.KeyDown,
                        keyCode = KeyCode.V,
                        modifiers = ShortcutModifier,
                    }
                );
                Assert.AreEqual(GUIUtility.systemCopyBuffer, input.text);
                Assert.IsTrue(history.Undo());
                Assert.AreEqual("Original", input.text);
                Assert.IsTrue(history.Redo());
                Assert.AreEqual(GUIUtility.systemCopyBuffer, input.text);
            }
            finally
            {
                GUIUtility.systemCopyBuffer = previousClipboard;
            }
        }

        [Test]
        public void Undo_TwoNativePastesInOneFrame_RetainsIndependentInsertionBoundaries()
        {
            string previousClipboard = GUIUtility.systemCopyBuffer;
            try
            {
                input.selectionStringAnchorPosition = input.selectionStringFocusPosition = input
                    .text
                    .Length;
                foreach (string text in new[] { " first paste", " second paste" })
                {
                    GUIUtility.systemCopyBuffer = text;
                    input.ProcessEvent(
                        new Event
                        {
                            type = EventType.KeyDown,
                            keyCode = KeyCode.V,
                            modifiers = ShortcutModifier,
                        }
                    );
                }
                Assert.AreEqual("Original first paste second paste", input.text);
                Assert.IsTrue(history.Undo());
                Assert.AreEqual("Original first paste", input.text);
                Assert.IsTrue(history.Undo());
                Assert.AreEqual("Original", input.text);
            }
            finally
            {
                GUIUtility.systemCopyBuffer = previousClipboard;
            }
        }
    }
}
