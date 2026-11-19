using System;
using System.Linq;
using System.Xml.Linq;
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
        public void RenderValidationErrors_FourIssues_ShowsEveryIssueInThreeRowViewport()
        {
            ContentPackEditorField[] fields = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.Take(2)
                .SelectMany(officer =>
                    new[] { officer.GetField("CurrentRank"), officer.GetField("DisplayName") }
                )
                .ToArray();
            ContentEditorValidationIssue[] issues = fields
                .Select(
                    (field, index) =>
                    {
                        return new ContentEditorValidationIssue(
                            new ArgumentException($"{field.MemberName}: Invalid {index}."),
                            field,
                            $"Invalid {index}."
                        );
                    }
                )
                .ToArray();

            view.RenderValidationErrors(issues);

            ContentEditorValidationIssueListView list =
                view.GetComponentInChildren<ContentEditorValidationIssueListView>(true);
            ContentEditorValidationIssueView[] rows =
                list.GetComponentsInChildren<ContentEditorValidationIssueView>();
            Assert.IsTrue(list.gameObject.activeSelf);
            Assert.AreEqual(4, rows.Length);
            Assert.AreEqual(216, ((RectTransform)list.transform).rect.height);
            Assert.AreEqual(
                216,
                list.GetComponentInChildren<ScrollAreaLayout>().ScrollArea.ViewportHeight
            );
            Assert.AreEqual(274, ((RectTransform)list.transform.parent).rect.height);
            Assert.Greater(
                list.GetComponentInChildren<ScrollAreaLayout>().ScrollArea.ViewportWidth,
                500
            );
            Assert.That(
                rows.Select(row => row.GetComponentInChildren<TMP_Text>().text),
                Is.EquivalentTo(issues.Select(issue => issue.Message))
            );
        }

        [Test]
        public void RenderStatus_AfterValidationErrors_HidesValidationControls()
        {
            ContentPackEditorField rank = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First()
                .GetField("CurrentRank");
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

            view.RenderStatus("Content pack saved.", false);

            ContentEditorValidationIssueListView list =
                view.GetComponentInChildren<ContentEditorValidationIssueListView>(true);
            Button toggle = view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "ToggleValidationErrors");
            Assert.IsFalse(list.gameObject.activeSelf);
            Assert.IsFalse(toggle.gameObject.activeSelf);
            Assert.IsTrue(list.transform.parent.gameObject.activeSelf);
        }

        [Test]
        public void RenderValidationErrors_SelectedIssue_RevealsFieldAndKeepsIssueListOpen()
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
                ContentEditorValidationIssueListView list =
                    view.GetComponentInChildren<ContentEditorValidationIssueListView>(true);

                Button link = list.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "OpenValidationIssue");
                Assert.AreEqual("Icon", link.targetGraphic.name);
                Assert.AreNotEqual(link.colors.normalColor, link.colors.highlightedColor);
                link.OnPointerClick(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    }
                );

                ContentEditorFieldView field =
                    view.GetComponentsInChildren<ContentEditorFieldView>()
                        .Single(item => item.Field?.MemberName == "CurrentRank");
                Assert.IsTrue(list.transform.parent.gameObject.activeSelf);
                Assert.IsTrue(list.gameObject.activeSelf);
                Assert.AreEqual(130, ((RectTransform)list.transform.parent).rect.height);
                Assert.IsTrue(field.HasError);
                Assert.AreEqual(
                    "Choose a listed value.",
                    field
                        .GetComponentsInChildren<TextMeshProUGUI>(true)
                        .Single(text => text.name == "Error")
                        .text
                );

                Button toggle = list
                    .transform.parent.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "ToggleValidationErrors");
                Assert.AreEqual("Hide", toggle.GetComponentInChildren<TMP_Text>().text);
                toggle.onClick.Invoke();

                Assert.IsFalse(list.gameObject.activeSelf);
                Assert.AreEqual(58, ((RectTransform)list.transform.parent).rect.height);
                Assert.AreEqual("Show", toggle.GetComponentInChildren<TMP_Text>().text);
                toggle.onClick.Invoke();

                Assert.IsTrue(list.gameObject.activeSelf);
                Assert.AreEqual("Hide", toggle.GetComponentInChildren<TMP_Text>().text);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(eventSystem);
            }
        }

        [Test]
        public void RenderValidationErrors_InvalidArtworkIssue_KeepsReportVisibleDuringNavigation()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == "Officers");
            XDocument document = XDocument.Parse(catalog.Document.Text);
            document
                .Root.Elements()
                .First()
                .SetElementValue("EncyclopediaImagePath", "unscoped/image.png");
            project.SetDocumentText(catalog.Document, document.ToString());
            workspace.RenderProject(project, assets);
            ContentPackEditorField artwork = project
                .GetCatalogs()
                .First(item => item.Name == "Officers")
                .Records.First()
                .GetField("EncyclopediaImagePath");
            view.RenderValidationErrors(
                new[]
                {
                    new ContentEditorValidationIssue(
                        new ArgumentException("Artwork: Use a content-relative path."),
                        artwork,
                        "Use a content-relative path."
                    ),
                }
            );
            ContentEditorValidationIssueListView list =
                view.GetComponentInChildren<ContentEditorValidationIssueListView>(true);

            list.GetComponentsInChildren<Button>()
                .Single(button => button.name == "OpenValidationIssue")
                .onClick.Invoke();

            Assert.IsTrue(list.gameObject.activeSelf);
            Assert.IsTrue(
                view.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "ToggleValidationErrors")
                    .gameObject.activeSelf
            );
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(item => item.Field?.MemberName == "EncyclopediaImagePath");
            Assert.IsTrue(field.HasError);
        }

        [Test]
        public void ShowField_XmlDraftDiscard_NavigatesToRequestedField()
        {
            ContentPackEditorRecord[] officers = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.Take(2)
                .ToArray();
            SelectRecord(officers[0]);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab3")
                .onClick.Invoke();
            TMP_InputField xml = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "AdvancedXml");
            xml.text += "\n<!-- Pending edit -->";

            view.ShowField(officers[1].GetField("CurrentRank"), "Choose a listed value.");
            ActionDialogView confirmation = view.GetComponentInChildren<ActionDialogView>();
            confirmation
                .GetComponentsInChildren<Button>()
                .Single(button => button.GetComponentInChildren<TMP_Text>().text == "Discard")
                .onClick.Invoke();

            Assert.AreEqual(
                officers[1].Name,
                view.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "RecordName")
                    .text
            );
            ContentEditorFieldView rank = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.Field?.MemberName == "CurrentRank");
            Assert.IsTrue(rank.HasError);
            Assert.AreEqual(
                "Choose a listed value.",
                rank.GetComponentsInChildren<TMP_Text>(true)
                    .Single(text => text.name == "Error")
                    .text
            );
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
