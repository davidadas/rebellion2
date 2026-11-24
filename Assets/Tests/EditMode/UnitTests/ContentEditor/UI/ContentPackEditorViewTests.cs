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
    [Category("RequiresMedia")]
    public sealed class ContentPackEditorViewTests : ContentEditorViewTestBase
    {
        [TestCase(EventModifiers.Control)]
        [TestCase(EventModifiers.Command)]
        public void HandleKeyboardShortcut_ValidatePackShortcut_RaisesValidateRequested(
            EventModifiers modifier
        )
        {
            int requests = 0;
            view.ValidateRequested += () => requests++;
            Event key = new Event
            {
                type = EventType.KeyDown,
                keyCode = KeyCode.V,
                modifiers = modifier | EventModifiers.Shift,
            };

            Assert.IsTrue(view.HandleKeyboardShortcut(key));
            Assert.AreEqual(1, requests);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryImportDroppedImage_ArtworkSlot_RoutesToTheDisplayedField(bool empty)
        {
            var (field, slot) = ShowArtworkSlot(empty);
            (string Record, string Member, string Path)? requested = null;

            DropImageOn(
                slot,
                (target, path) => requested = (target.Record.ID, target.MemberName, path)
            );

            Assert.That(requested, Is.EqualTo((field.Record.ID, field.MemberName, "dropped.png")));
        }

        [Test]
        public void TryImportDroppedImage_WithModalOpen_DoesNotRunImport()
        {
            var (_, slot) = ShowArtworkSlot(empty: true);
            view.RenderValidationResult("Pack validation", new[] { "Validation warning." });

            Assert.IsFalse(DropImageOn(slot, (_, _) => Assert.Fail("Imported through a modal.")));
        }

        [Test]
        public void TryImportDroppedImage_WhenFieldCommitIsRejected_DoesNotRunImport()
        {
            var officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            var field = officer.GetField("EncyclopediaImagePath");
            view.ShowField(field, null);
            var description = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(item => item.Field?.MemberName == "EncyclopediaDescription");
            description.Submitted += (_, _) => throw new ArgumentException("Rejected draft.");
            description
                .GetComponentInChildren<TMP_InputField>()
                .SetTextWithoutNotify("Pending edit.");
            var image = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(item => item.Field?.MemberName == field.MemberName);
            int imports = 0;

            DropImageOn(image.gameObject, (_, _) => imports++);

            Assert.That(description.HasRejectedDraft, Is.True);
            Assert.That(imports, Is.Zero);
        }

        [Test]
        public void ShowArtworkCuts_WithThreeOutputs_DisplaysEveryFullFrameBeforeApply()
        {
            var cuts = CreateArtworkCuts();
            int applications = 0;

            view.ShowArtworkCuts(cuts, new[] { true, true, true }, _ => applications++);
            Canvas.ForceUpdateCanvases();

            ImagePreviewView preview = view.GetComponentInChildren<ImagePreviewView>();
            RawImage[] artwork = preview
                .GetComponentsInChildren<RawImage>()
                .Where(image => image.name == "CutArtwork")
                .ToArray();
            CollectionAssert.AreEqual(
                cuts.Select(cut => cut.Item2),
                artwork.Select(image => image.texture)
            );
            Assert.IsTrue(artwork.All(image => image.uvRect == new Rect(0, 0, 1, 1)));
            Assert.AreEqual(0, applications);
            preview
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "UseArtwork")
                .onClick.Invoke();
            Assert.AreEqual(1, applications);
        }

        [Test]
        public void ShowArtworkCuts_UncheckedEncyclopedia_ExcludesItFromTheSave()
        {
            var cuts = CreateArtworkCuts();
            string[] savedMembers = null;
            view.ShowArtworkCuts(
                cuts,
                new[] { true, true, true },
                indices =>
                    savedMembers = indices.Select(index => cuts[index].Field.MemberName).ToArray()
            );
            ImagePreviewView preview = view.GetComponentInChildren<ImagePreviewView>();
            Toggle[] selections = preview.GetComponentsInChildren<Toggle>();

            Assert.That(selections.Length, Is.EqualTo(3));
            Assert.That(selections.All(toggle => toggle.isOn), Is.True);
            selections[0].isOn = false;
            preview
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "UseArtwork")
                .onClick.Invoke();

            CollectionAssert.AreEqual(
                new[] { "DisplayImagePath", "SmallDisplayImagePath" },
                savedMembers
            );
        }

        [Test]
        public void ShowArtworkCuts_PreviousChoice_PersistsAfterRefreshingTheImages()
        {
            bool[] selected = { true, true, true };
            view.ShowArtworkCuts(CreateArtworkCuts(), selected, _ => { });
            ImagePreviewView preview = view.GetComponentInChildren<ImagePreviewView>();
            preview.GetComponentsInChildren<Toggle>()[1].isOn = false;

            view.ShowArtworkCuts(CreateArtworkCuts(), selected, _ => { });

            CollectionAssert.AreEqual(
                new[] { true, false, true },
                preview.GetComponentsInChildren<Toggle>().Select(toggle => toggle.isOn)
            );
        }

        [Test]
        public void ShowArtworkCuts_NoCheckedCuts_DisablesSave()
        {
            view.ShowArtworkCuts(
                CreateArtworkCuts(),
                new[] { true, true, true },
                _ => Assert.Fail()
            );
            ImagePreviewView preview = view.GetComponentInChildren<ImagePreviewView>();
            foreach (Toggle choice in preview.GetComponentsInChildren<Toggle>())
                choice.isOn = false;

            Button save = preview
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "UseArtwork");

            Assert.IsFalse(save.interactable);
            save.onClick.Invoke();
        }

        [Test]
        public void ShowArtworkCuts_ShipEncyclopediaDrag_MovesArtworkOverAStationaryBackground()
        {
            var ship = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Starfighter");
            SelectRecord(ship);
            var background = new Texture2D(8, 4);
            try
            {
                view.ShowArtworkCuts(
                    new[] { (ship.GetField("EncyclopediaImagePath"), new Texture2D(8, 4)) },
                    new[] { true },
                    _ => { },
                    (_, _) => { },
                    encyclopediaBackground: background
                );
                Canvas.ForceUpdateCanvases();
                ImagePreviewView preview = view.GetComponentInChildren<ImagePreviewView>();
                RawImage[] layers = preview.GetComponentsInChildren<RawImage>();
                var backdrop = layers.Single(layer => layer.name == "CutBackground");
                var artwork = layers.Single(layer => layer.name == "CutArtwork");
                Vector2 backdropOrigin = backdrop.rectTransform.anchoredPosition;
                Vector2 artworkOrigin = artwork.rectTransform.anchoredPosition;
                Vector2 start = RectTransformUtility.WorldToScreenPoint(
                    null,
                    artwork.rectTransform.TransformPoint(artwork.rectTransform.rect.center)
                );
                var drag = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    pressPosition = start,
                    position = start + new Vector2(20, 0),
                };

                preview.OnBeginDrag(drag);
                preview.OnDrag(drag);

                Assert.That(backdrop.texture, Is.SameAs(background));
                Assert.That(backdrop.rectTransform.anchoredPosition, Is.EqualTo(backdropOrigin));
                Assert.That(
                    artwork.rectTransform.anchoredPosition.x,
                    Is.GreaterThan(artworkOrigin.x)
                );
            }
            finally
            {
                view.GetComponentInChildren<ImagePreviewView>()?.TryCancel();
                UnityEngine.Object.DestroyImmediate(background);
            }
        }

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
        /// Creates the three common previews for an officer's artwork.
        /// </summary>
        /// <returns>The output fields and their temporary textures.</returns>
        private (ContentPackEditorField Field, Texture2D Texture)[] CreateArtworkCuts()
        {
            var officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            SelectRecord(officer);
            return new[]
            {
                (officer.GetField("EncyclopediaImagePath"), new Texture2D(60, 30)),
                (officer.GetField("DisplayImagePath"), new Texture2D(30, 30)),
                (officer.GetField("SmallDisplayImagePath"), new Texture2D(40, 16)),
            };
        }

        /// <summary>
        /// Opens the officer's message image slot with the requested assignment.
        /// </summary>
        /// <param name="empty">Whether to clear the slot before opening the artwork page.</param>
        /// <returns>The image field and its drop surface.</returns>
        private (ContentPackEditorField Field, GameObject Slot) ShowArtworkSlot(bool empty)
        {
            var officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            var field = officer.GetField("MessageImagePath");
            if (empty)
                project.SetField(field, string.Empty);
            else
                Assert.IsFalse(string.IsNullOrWhiteSpace(field.Value));
            SelectRecord(officer);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.GetComponentInChildren<TMP_Text>()?.text == "Artwork")
                .onClick.Invoke();
            var slot = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith(
                        "-Output-MessageImagePath-Backing",
                        StringComparison.Ordinal
                    )
                )
                .gameObject;
            return (field, slot);
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
