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
        [TestCase(false)]
        [TestCase(true)]
        public void RenderConnection_PublishingDialogIsHidden_UpdatesConnectionLabel(bool connected)
        {
            view.Publishing.gameObject.SetActive(false);

            view.Publishing.RenderConnection(connected ? new[] { "Team" } : Array.Empty<string>());

            Assert.That(view.Publishing.IsOpen, Is.False);
            Assert.That(
                view.Publishing.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "ThunderstoreConnection")
                    .GetComponentInChildren<TMP_Text>(true)
                    .text,
                Is.EqualTo(connected ? "Thunderstore connected" : "Connect Thunderstore…")
            );
        }

        [Test]
        public void SetBusy_ConnectionDialogIsHidden_KeepsCancelLabelAvailable()
        {
            Assert.DoesNotThrow(() => view.Connection.SetBusy(true));
            Assert.That(
                view.Connection.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "CloseConnection")
                    .GetComponentInChildren<TMP_Text>(true)
                    .text,
                Is.EqualTo("Cancel")
            );
        }

        [TestCase(true, false, false)]
        [TestCase(false, false, true)]
        [TestCase(true, true, true)]
        public void ShowAfterVerification_ConnectionRequest_OpensOnlySetupOrManagement(
            bool verified,
            bool manageAccount,
            bool opensDialog
        )
        {
            view.Connection.ShowAfterVerification(
                verified ? new[] { "Team" } : Array.Empty<string>(),
                manageAccount
            );

            Assert.That(view.Connection.IsOpen, Is.EqualTo(opensDialog));
        }

        [Test]
        public void ShowProjects_ShortWindowAtHighZoom_KeepsRowsUsableInScrollablePage()
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(2000, 636);
            view.ApplySettings(new ContentEditorSettings { Zoom = 1.75f });
            view.ShowProjects();
            ScrollAreaLayout[] lists = view.Projects.GetComponentsInChildren<ScrollAreaLayout>(
                true
            );
            Assert.That(
                lists.Min(list => list.ScrollArea.ViewportHeight),
                Is.GreaterThanOrEqualTo(ContentEditorProjectRowView.Height)
            );
            ScrollAreaLayout page = view.GetComponentsInChildren<ScrollAreaLayout>(true)
                .Single(list => list.name == "ProjectsPage");
            Assert.That(
                page.ScrollArea.ContentRoot.rect.height,
                Is.GreaterThan(page.ScrollArea.ViewportHeight)
            );
        }

        [Test]
        public void HandleKeyboardShortcut_CropIsOpen_MovesSelectionWithArrowKeys()
        {
            string path = System.IO.Path.Combine(folder, "crop.png");
            Texture2D source = new(128, 64);
            try
            {
                System.IO.File.WriteAllBytes(path, source.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
            view.Publishing.Show(project, new ContentEditorPublishingSettings());
            view.Publishing.ShowIconCrop(path, _ => { });
            RectTransform selection = view
                .Publishing.ImageCrop.GetComponentsInChildren<RectTransform>()
                .Single(rect => rect.name == "Selection");
            float previous = selection.anchorMin.x;
            Assert.That(
                view.HandleKeyboardShortcut(
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.RightArrow }
                ),
                Is.True
            );
            Assert.That(selection.anchorMin.x, Is.GreaterThan(previous));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Show_ShortWindow_KeepsDialogActionsInsideWindow(bool crop)
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(2000, 636);
            view.Publishing.Show(project, new ContentEditorPublishingSettings());
            if (crop)
            {
                string path = System.IO.Path.Combine(folder, "crop.png");
                Texture2D image = new(128, 64);
                try
                {
                    System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(image);
                }
                view.Publishing.ShowIconCrop(path, _ => { });
            }
            view.Connection.Show();
            view.Connection.RenderConnection(Array.Empty<string>());
            Canvas.ForceUpdateCanvases();
            foreach (
                Button button in view.GetComponentsInChildren<Button>()
                    .Where(button =>
                        button.name == "ClosePublishing"
                        || button.name == "ContinuePublishing"
                        || button.name == "ConnectThunderstore"
                        || button.name == "CloseConnection"
                        || button.name == "CancelCrop"
                        || button.name == "ApplyCrop"
                    )
            )
            {
                var corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Assert.That(
                    corners.All(corner =>
                        ((RectTransform)view.transform).rect.Contains(
                            view.transform.InverseTransformPoint(corner)
                        )
                    ),
                    Is.True,
                    button.name
                );
            }
        }

        [Test]
        public void ReadPackageID_IncompleteConnectedDraft_KeepsProjectAvailableWithoutLink()
        {
            ContentEditorPublishingStorage.Save(
                project,
                new ContentEditorPublishingSettings { TeamName = "Team", PackageName = "My Pack" }
            );
            ContentEditorLocalProject local = ContentEditorLocalProject.Read(project.PackRootPath);
            Assert.That(local.Error, Is.Null);
            Assert.That(local.PackageID, Is.Null);
        }

        [Test]
        public void ShowProjects_LocalProjectRow_FillsItsColumnAndShowsVersion()
        {
            view.ShowProjects();
            view.Projects.RenderLocal(new[] { ContentEditorLocalProject.FromProject(project) });
            Canvas.ForceUpdateCanvases();
            ScrollAreaLayout scroll = view
                .Projects.GetComponentsInChildren<ScrollAreaLayout>()
                .Single(item => item.name == "LocalProjects");
            ContentEditorProjectRowView row =
                scroll.GetComponentInChildren<ContentEditorProjectRowView>();

            Assert.That(((RectTransform)row.transform).rect.width, Is.GreaterThan(480));
            Assert.That(
                ((RectTransform)row.transform).rect.width,
                Is.EqualTo(scroll.ScrollArea.ViewportWidth).Within(1)
            );
            Assert.That(
                row.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "summary")
                    .isTextOverflowing,
                Is.False
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ShowProjects_OpenWorkingProject_MarksItsRowForEditing(bool asMod)
        {
            using ContentPackEditorProject working = ContentPackEditorStorage.CreateInFolder(
                project.ContentRootPath,
                TestContent.Pack.Definition.ID,
                System.IO.Path.Combine(folder, "working-project"),
                "active-project-row",
                "Active project row",
                "1.0.0",
                asMod
            );
            view.ShowProjects();
            view.Projects.RenderLocal(new[] { ContentEditorLocalProject.FromProject(working) });
            view.Projects.SetCurrentProject(working);
            Canvas.ForceUpdateCanvases();

            ContentEditorProjectRowView row = view
                .Projects.GetComponentsInChildren<ContentEditorProjectRowView>()
                .Single(item =>
                    item.GetComponentsInChildren<TMP_Text>()
                        .Any(label => label.text == working.DisplayName)
                );
            TMP_Text[] captions = row.GetComponentsInChildren<TMP_Text>();

            Assert.That(
                captions.Single(label => label.name == "summary").text,
                Does.EndWith("Open now")
            );
            Assert.That(captions.Select(label => label.text), Does.Contain("Edit"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ZoomContent_ModPageIsSelected_PreservesPageAndOpenProjectDraft(bool library)
        {
            ContentEditorPage page = library ? ContentEditorPage.Library : ContentEditorPage.Browse;
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            view.ShowField(officer.GetField("EncyclopediaDescription"), null);
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(item => item.Field?.MemberName == "EncyclopediaDescription");
            TMP_InputField input = field.GetComponentInChildren<TMP_InputField>();
            input.SetTextWithoutNotify("Uncommitted project draft");
            view.SetWorkspaceNavigation(page);
            view.ModManager.Show();
            view.ModManager.RenderRows(Array.Empty<ContentEditorModEntry>(), library, 0, true);

            view.ZoomContent(-0.1f);

            Assert.That(view.ModManager.gameObject.activeSelf, Is.True);
            Assert.That(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text == page.ToString()),
                Is.True
            );
            view.ShowProjects();
            view.EditCurrentProject();
            Assert.That(view.ModManager.gameObject.activeSelf, Is.False);
            Assert.That(input.gameObject.activeInHierarchy, Is.True);
            Assert.That(input.text, Is.EqualTo("Uncommitted project draft"));
        }

        [Test]
        public void Render_ManyCategories_SelectsMultipleKeysWithoutClosingDropdown()
        {
            view.SetWorkspaceNavigation(ContentEditorPage.Browse);
            view.ModManager.Show();
            view.ModManager.RenderRows(Array.Empty<ContentEditorModEntry>(), false, 0, true);
            view.ModManager.GetComponentsInChildren<Button>()
                .Single(button => button.name == "filterButton")
                .onClick.Invoke();
            MultiSelectDropdown picker = view
                .ModManager.GetComponentsInChildren<MultiSelectDropdown>()
                .Single(item => item.name == "IncludeCategories");
            var choices = Enumerable
                .Range(0, 40)
                .ToDictionary(index => "key-" + index, index => "Category " + index.ToString("D2"));
            string[] selected = null;
            picker.Changed += () => selected = picker.SelectedValues.ToArray();
            picker.SetOptions(choices, "Any category");
            picker.GetComponent<Button>().onClick.Invoke();
            Toggle[] options = picker.GetComponentsInChildren<Toggle>();

            options[1].isOn = true;
            options[35].isOn = true;

            CollectionAssert.AreEquivalent(new[] { "key-1", "key-35" }, selected);
            Assert.That(picker.IsExpanded, Is.True);
        }

        [TestCase("ContinuePublishing")]
        [TestCase("ReleaseStep")]
        [TestCase("ReviewStep")]
        public void Show_PublishingDraftHasMissingRequirements_PreventsForwardNavigation(
            string buttonName
        )
        {
            view.Publishing.Show(project, new ContentEditorPublishingSettings());
            Button advance = view
                .Publishing.GetComponentsInChildren<Button>()
                .Single(button => button.name == buttonName);

            advance.onClick.Invoke();

            Assert.That(
                view.Publishing.GetComponentsInChildren<TMP_InputField>()
                    .Any(input => input.name == "PublishingPackageName"),
                Is.True
            );
            Assert.That(
                view.Publishing.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "PublishingStatus")
                    .text,
                Does.Contain("Package name")
            );
        }

        [Test]
        public void Show_UnfinishedPublishingDraft_ReopensSavedFieldsWithoutRequiringReleaseValidity()
        {
            var settings = new ContentEditorPublishingSettings
            {
                PackageName = "",
                Description = "An unfinished listing",
                Readme = "Notes in progress",
                Dependencies = "Example-Required-1.2.3",
                TeamName = "ExampleTeam",
            };
            ContentEditorPublishingStorage.Save(project, settings);
            view.Publishing.Show(project, ContentEditorPublishingStorage.Load(project));
            view
                .Publishing.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "PublishingDescription")
                .text = "Edited unfinished listing";
            ContentEditorPublishingStorage.Save(project, view.Publishing.ReadSettings());
            view.Publishing.Hide();

            view.Publishing.Show(project, ContentEditorPublishingStorage.Load(project));

            ContentEditorPublishingSettings reopened = view.Publishing.ReadSettings();
            Assert.That(reopened.PackageName, Is.Empty);
            Assert.That(reopened.Description, Is.EqualTo("Edited unfinished listing"));
            Assert.That(reopened.Readme, Is.EqualTo("Notes in progress"));
            CollectionAssert.AreEqual(
                new[] { "Example-Required-1.2.3" },
                reopened.GetDependencies()
            );
            Assert.That(reopened.TeamName, Is.EqualTo("ExampleTeam"));
        }

        [Test]
        public void Render_ChangingSearch_ReusesTheCurrentNavigationTree()
        {
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            TMP_InputField search = view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(field => field.name == "Search");
            search.SetTextWithoutNotify(officer.ID[..^1]);
            search.onValueChanged.Invoke(search.text);
            ContentEditorTreeNode entry = view.GetComponentsInChildren<ContentEditorRecordRowView>()
                .Single(row => row.Node?.Record?.Element == officer.Element)
                .Node;

            search.SetTextWithoutNotify(officer.ID);
            search.onValueChanged.Invoke(search.text);

            Assert.AreSame(
                entry,
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(row => row.Node?.Record?.Element == officer.Element)
                    .Node
            );
        }

        [Test]
        public void Render_CutscenesTabOnPackSettings_ShowsBothStartupVideoSlots()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("category/Pack settings"));
            Button tab = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "CutscenesTab");
            tab.onClick.Invoke();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    nameof(ContentPackDefinition.StartupIntroVideoPath),
                    nameof(ContentPackDefinition.OpeningCrawlVideoPath),
                },
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Select(row => row.Field.MemberName)
            );
        }

        [TestCase("Alliance")]
        [TestCase("Empire")]
        public void Render_CutscenesTabOnFaction_ShowsItsFourCampaignVideoSlots(string faction)
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find("factions/" + faction);
            ContentPackEditorRecord theme = page.RelatedRecords.Single(record =>
                record.Kind == nameof(FactionTheme)
            );
            SelectPage(page);
            Button tab = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "CutscenesTab");
            tab.onClick.Invoke();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    nameof(FactionTheme.IntroCutscenePath),
                    nameof(FactionTheme.VictoryCutscenePath),
                    nameof(FactionTheme.DefeatCutscenePath),
                    nameof(FactionTheme.HeadquartersDestroyedCutscenePath),
                },
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Where(row =>
                        row.Field.Record.Key == theme.Key
                        && row.Field.MediaType == ContentPackEditorMediaType.Video
                    )
                    .Select(row => row.Field.MemberName)
            );
        }

        [Test]
        public void Render_SelectingStartupPreviewResolution_RequestsTheBuiltInVideoAtThatSize()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("category/Pack settings"));
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "CutscenesTab")
                .onClick.Invoke();
            ContentEditorFieldView slot = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(row =>
                    row.Field.MemberName == nameof(ContentPackDefinition.StartupIntroVideoPath)
                );
            (string Address, Vector2Int? Size) requested = default;
            view.PreviewRequested += (_, address, size) => requested = (address, size);

            TMP_Dropdown preview = slot.GetComponentInChildren<TMP_Dropdown>();
            string chosen = preview.options[1].text;
            preview.value = 1;

            Assert.AreEqual(ContentPackDefinition.DefaultStartupIntroVideoPath, requested.Address);
            Assert.IsTrue(requested.Size.HasValue);
            Assert.AreEqual(chosen, $"{requested.Size.Value.x} × {requested.Size.Value.y}");
        }

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
