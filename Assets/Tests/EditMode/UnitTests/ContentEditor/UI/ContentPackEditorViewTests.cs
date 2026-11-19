using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

        [Test]
        public void RenderValidationErrors_MultipleIssues_ShowsEveryNavigableFailure()
        {
            ContentPackEditorRecord[] officers = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.Take(2)
                .ToArray();
            ContentEditorValidationIssue[] issues = officers
                .Select(
                    (officer, index) =>
                    {
                        ContentPackEditorField field = officer.GetField("CurrentRank");
                        return new ContentEditorValidationIssue(
                            new ArgumentException($"{officer.ID} / Current rank: Invalid {index}."),
                            field,
                            $"Invalid {index}."
                        );
                    }
                )
                .ToArray();

            view.RenderValidationErrors(issues);

            ContentEditorValidationDialogView dialog =
                view.GetComponentInChildren<ContentEditorValidationDialogView>(true);
            ContentEditorValidationIssueView[] rows =
                dialog.GetComponentsInChildren<ContentEditorValidationIssueView>();
            Assert.IsTrue(dialog.IsOpen);
            Assert.AreEqual(2, rows.Length);
            Assert.That(
                rows.Select(row => row.GetComponentInChildren<TMP_Text>().text),
                Is.EquivalentTo(issues.Select(issue => issue.Message))
            );
        }

        [Test]
        public void RenderValidationErrors_SelectedIssue_RevealsItsFieldAndInlineError()
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EditModeEventSystem>();
            try
            {
                ContentPackEditorRecord officer = project
                    .GetCatalogs()
                    .First(catalog => catalog.Name == "Officers")
                    .Records.Last();
                ContentPackEditorField rank = officer.GetField("CurrentRank");
                view.RenderValidationErrors(
                    new[]
                    {
                        new ContentEditorValidationIssue(
                            new ArgumentException("Current rank: Choose a listed value."),
                            rank,
                            "Choose a listed value."
                        ),
                    }
                );
                ContentEditorValidationDialogView dialog =
                    view.GetComponentInChildren<ContentEditorValidationDialogView>(true);

                dialog
                    .GetComponentsInChildren<Button>()
                    .Single(button => button.name == "ValidationIssueLink")
                    .onClick.Invoke();

                ContentEditorFieldView field =
                    view.GetComponentsInChildren<ContentEditorFieldView>()
                        .Single(item => item.Field?.MemberName == "CurrentRank");
                Assert.IsFalse(dialog.IsOpen);
                Assert.IsTrue(field.HasError);
                Assert.AreEqual(
                    "Choose a listed value.",
                    field
                        .GetComponentsInChildren<TextMeshProUGUI>(true)
                        .Single(text => text.name == "Error")
                        .text
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(eventSystem);
            }
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
