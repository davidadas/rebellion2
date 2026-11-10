using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentPackEditorViewTests : ContentEditorViewTestBase
    {
        [Test]
        public void RenderValidationResult_NoWarnings_DoesNotOpenADialog()
        {
            view.RenderValidationResult(
                "Content pack validated successfully.",
                Array.Empty<string>()
            );

            Assert.IsFalse(
                view.GetComponentInChildren<ActionDialogView>(true).gameObject.activeSelf
            );
            Assert.That(
                view.GetComponentsInChildren<TMP_Text>(),
                Has.Some.Matches<TMP_Text>(text =>
                    text.text == "Content pack validated successfully."
                )
            );
        }

        [Test]
        public void RenderValidationResult_MultipleWarnings_PresentsEachWarningAndCloses()
        {
            view.RenderValidationResult(
                "Content pack exported.",
                new[] { "First warning.", "Second warning." }
            );
            ActionDialogView dialog = view.GetComponentInChildren<ActionDialogView>(true);
            Assert.IsTrue(dialog.gameObject.activeSelf);
            Assert.That(
                dialog.GetComponentsInChildren<TMP_Text>(),
                Has.Some.Matches<TMP_Text>(text => text.text == "First warning.")
            );

            Click(dialog, "Next");

            Assert.IsTrue(dialog.gameObject.activeSelf);
            Assert.That(
                dialog.GetComponentsInChildren<TMP_Text>(),
                Has.Some.Matches<TMP_Text>(text => text.text == "Second warning.")
            );
            Click(dialog, "Close");
            Assert.IsFalse(dialog.gameObject.activeSelf);
        }

        [Test]
        public void RenderValidationResult_WarningDialog_CanBeDismissed()
        {
            view.RenderValidationResult(
                "Content pack validated.",
                new[] { "A legal restriction." }
            );
            ActionDialogView dialog = view.GetComponentInChildren<ActionDialogView>(true);

            Assert.IsTrue(dialog.TryCancel());

            Assert.IsFalse(dialog.gameObject.activeSelf);
        }

        /// <summary>
        /// Clicks a visible semantic action in the warning dialog.
        /// </summary>
        /// <param name="dialog">The active warning dialog.</param>
        /// <param name="caption">The action's visible label.</param>
        private static void Click(ActionDialogView dialog, string caption) =>
            dialog
                .GetComponentsInChildren<Button>()
                .Single(button => button.GetComponentInChildren<TMP_Text>()?.text == caption)
                .onClick.Invoke();
    }
}
