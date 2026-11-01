using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Schema;
using NUnit.Framework;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentPackEditorViewTests : ContentEditorViewTestBase
    {
        [TestCase(false)]
        [TestCase(true)]
        public void RenderProject_AddFaction_CreatesAndOpensCombinedFactionPage(bool fromMenu)
        {
            int before = project.GetFactionOptions().Count;
            if (fromMenu)
            {
                view.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "AddUnit")
                    .onClick.Invoke();
                view.GetComponentsInChildren<ContextMenuCommandView>()
                    .Single(command =>
                        command
                            .GetComponentsInChildren<TMP_Text>()
                            .Any(label => label.text == "Faction")
                    )
                    .OnPointerClick(
                        new PointerEventData(EventSystem.current)
                        {
                            button = PointerEventData.InputButton.Left,
                        }
                    );
            }
            else
            {
                view.GetComponentsInChildren<TMP_InputField>(true)
                    .Single(input => input.name == "Search")
                    .text = "Add faction";
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(row => row.Node.Caption == "+ Add faction")
                    .OnPointerDown(
                        new PointerEventData(EventSystem.current)
                        {
                            button = PointerEventData.InputButton.Left,
                        }
                    );
            }
            Assert.AreEqual(before, project.GetFactionOptions().Count);
            ActionDialogView warning = view.GetComponentInChildren<ActionDialogView>();
            Assert.IsNotNull(warning);
            Assert.IsTrue(
                warning
                    .GetComponentsInChildren<TMP_Text>()
                    .Any(text => text.text.Contains("not guaranteed to work"))
            );
            warning
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Primary")
                .onClick.Invoke();
            Assert.AreEqual(before + 1, project.GetFactionOptions().Count);
            ContentEditorRecordRowView row =
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(item =>
                        item.Node.Record?.Element.Element("InstanceID")?.Value == "NEW_FACTION"
                    );
            Assert.AreEqual(3, row.Node.RelatedRecords.Count);
            Assert.AreEqual("New faction", row.Node.Caption);
            Assert.AreEqual(
                row.Node.Caption,
                view.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "RecordName")
                    .text
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Any(field => field.Field.MemberName == "DisplayName")
            );
            Assert.IsTrue(project.Undo());
            workspace.RenderProject(project, assets);
            Assert.AreEqual(before, project.GetFactionOptions().Count);
            Assert.IsFalse(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(item => item.Node.Record?.ID == "NEW_FACTION")
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AddFaction_ThirdFactionWarning_CancelingLeavesTheProjectUnchanged(bool escape)
        {
            int before = project.GetFactionOptions().Count;
            view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search")
                .text = "Add faction";
            view.GetComponentsInChildren<ContentEditorRecordRowView>()
                .Single(row => row.Node.Caption == "+ Add faction")
                .OnPointerDown(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    }
                );
            ActionDialogView warning = view.GetComponentInChildren<ActionDialogView>();
            Assert.IsNotNull(warning);
            Assert.AreEqual(before, project.GetFactionOptions().Count);
            Assert.IsTrue(
                warning.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "Warning")
            );
            if (escape)
                Assert.IsTrue(warning.TryCancel());
            else
                warning
                    .GetComponentsInChildren<Button>()
                    .Single(button => button.name == "Cancel")
                    .onClick.Invoke();
            Assert.IsFalse(warning.gameObject.activeSelf);
            Assert.AreEqual(before, project.GetFactionOptions().Count);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void AddFaction_OneExistingFaction_CreatesWithoutAWarning()
        {
            ContentPackEditorRecord removed = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "Faction" && record.Element.Element("InstanceID") != null
                );
            project.RemoveRecord(removed);
            workspace.RenderProject(project, assets);
            int before = project.GetFactionOptions().Count;
            view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search")
                .text = "Add faction";
            view.GetComponentsInChildren<ContentEditorRecordRowView>()
                .Single(row => row.Node.Caption == "+ Add faction")
                .OnPointerDown(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    }
                );
            Assert.AreEqual(before + 1, project.GetFactionOptions().Count);
            Assert.IsFalse(
                view.GetComponentInChildren<ActionDialogView>(true).gameObject.activeSelf
            );
        }

        [Test]
        public void RemoveRecord_FactionWithAnInvalidDraft_DeletesWithoutCommittingTheDraft()
        {
            ContentPackEditorRecord faction = project.AddFaction();
            string id = faction.ID;
            workspace.RenderProject(project, assets);
            SelectRecord(faction);
            TMP_InputField input = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field =>
                    field.Field.MemberName == "InstanceID"
                    && field.Field.Record.Element == faction.Element
                )
                .GetComponentInChildren<TMP_InputField>();
            input.onSelect.Invoke(input.text);
            input.text = string.Empty;
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Delete")
                .onClick.Invoke();
            ActionDialogView confirmation = view.GetComponentInChildren<ActionDialogView>();
            Assert.IsNotNull(confirmation);
            confirmation
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Primary")
                .onClick.Invoke();
            Assert.IsFalse(project.GetFactionOptions().ContainsKey(id));
            Assert.IsEmpty(
                view.GetComponentsInChildren<TMP_InputField>(true)
                    .Single(field => field.name == "Search")
                    .text
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemoveRecord_Faction_RefreshesTheTreeAfterConfirmation(
            bool missingGameplayEntry
        )
        {
            int before = project.GetFactionOptions().Count;
            ContentPackEditorRecord faction = project.AddFaction();
            string id = faction.ID;
            if (missingGameplayEntry)
                faction.Catalog.Remove(faction);
            workspace.RenderProject(project, assets);
            ContentPackEditorRecord selected = missingGameplayEntry
                ? project
                    .GetCatalogs()
                    .Single(catalog =>
                        catalog.DocumentRecord.Element.Element("ID")?.Value == id
                        && catalog.DocumentRecord.Element.Element("FactionDataPath") != null
                    )
                    .DocumentRecord
                : faction;
            SelectRecord(selected);
            Assert.IsFalse(
                view.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "Duplicate")
                    .interactable
            );
            Button delete = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Delete");
            Assert.IsTrue(delete.interactable);
            delete.onClick.Invoke();
            ActionDialogView confirmation = view.GetComponentInChildren<ActionDialogView>();
            Assert.IsTrue(
                confirmation
                    .GetComponentsInChildren<TMP_Text>()
                    .Any(text => text.text.Contains("faction-specific data"))
            );
            confirmation
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Primary")
                .onClick.Invoke();
            Assert.AreEqual(before, project.GetFactionOptions().Count);
            Assert.IsFalse(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row =>
                        row.Node.Record?.ID == id || row.Node.CreationFactionInstanceID == id
                    )
            );
            Assert.IsTrue(project.Undo());
            workspace.RenderProject(project, assets);
            Assert.IsTrue(
                project
                    .GetCatalogs()
                    .Any(catalog => catalog.DocumentRecord.Element.Element("ID")?.Value == id)
            );
        }

        [Test]
        public void RenderProject_MenuCommands_ReplaceTheRedundantToolbar()
        {
            Assert.IsFalse(
                view.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Toolbar")
            );
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "File")
                .onClick.Invoke();
            string[] fileItems = view.GetComponentsInChildren<ContextMenuCommandView>()
                .SelectMany(item => item.GetComponentsInChildren<TMP_Text>())
                .Select(label => label.text)
                .ToArray();
            CollectionAssert.Contains(fileItems, "Save pack");
            CollectionAssert.Contains(fileItems, "Validate pack");
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Edit")
                .onClick.Invoke();
            string[] editItems = view.GetComponentsInChildren<ContextMenuCommandView>()
                .SelectMany(item => item.GetComponentsInChildren<TMP_Text>())
                .Select(label => label.text)
                .ToArray();
            foreach (string item in new[] { "Undo", "Redo" })
                CollectionAssert.Contains(editItems, item);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "View")
                .onClick.Invoke();
            string[] viewItems = view.GetComponentsInChildren<ContextMenuCommandView>()
                .SelectMany(item => item.GetComponentsInChildren<TMP_Text>())
                .Select(label => label.text)
                .ToArray();
            foreach (string item in new[] { "Zoom", "Theme" })
                CollectionAssert.Contains(viewItems, item);
            view.GetComponentsInChildren<ContextMenuCommandView>()
                .Single(command =>
                    command.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "Zoom")
                )
                .OnPointerEnter(new PointerEventData(EventSystem.current));
            view.GetComponentInChildren<ContextMenuView>(true).RenderCurrent();
            string[] zoomItems = view.GetComponentsInChildren<ContextMenuCommandView>()
                .SelectMany(item => item.GetComponentsInChildren<TMP_Text>())
                .Select(label => label.text)
                .ToArray();
            CollectionAssert.Contains(zoomItems, "Zoom in");
            CollectionAssert.Contains(zoomItems, "Zoom out");
            CollectionAssert.DoesNotContain(viewItems, "Expand all");
            Assert.IsNotNull(
                view.GetComponentsInChildren<Button>().Single(button => button.name == "ExpandAll")
            );
            Assert.IsNotNull(
                view.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "CollapseAll")
            );
            Assert.IsFalse(
                view.GetComponentsInChildren<Button>(true).Any(button => button.name == "Help")
            );
        }

        [TestCase(ContentEditorColorMode.Dark, "Dark side")]
        [TestCase(ContentEditorColorMode.Light, "Light side")]
        [TestCase(ContentEditorColorMode.Empire, "Empire")]
        [TestCase(ContentEditorColorMode.Rebels, "Rebels")]
        [TestCase(ContentEditorColorMode.Dagobah, "Dagobah")]
        public void RenderProject_AppearanceMenu_CentersAReadableCheckmarkInTheSelectedRow(
            ContentEditorColorMode mode,
            string expectedLabel
        )
        {
            view.SetColorMode(mode);
            string[] modes = { "Dark side", "Light side", "Empire", "Rebels", "Dagobah" };
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "View")
                .onClick.Invoke();
            view.GetComponentsInChildren<ContextMenuCommandView>()
                .Single(command =>
                    command.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "Theme")
                )
                .OnPointerEnter(new PointerEventData(EventSystem.current));
            view.GetComponentInChildren<ContextMenuView>(true).RenderCurrent();
            RawImage mark = view.GetComponentsInChildren<ContextMenuCommandView>()
                .Where(command =>
                    command
                        .GetComponentsInChildren<TMP_Text>()
                        .Any(label => modes.Contains(label.text))
                )
                .SelectMany(command => command.GetComponentsInChildren<RawImage>())
                .Single(image => image.texture != null);
            RectTransform row =
                mark.GetComponentInParent<ContextMenuCommandView>().transform as RectTransform;
            CollectionAssert.Contains(
                row.GetComponentsInChildren<TMP_Text>().Select(label => label.text).ToArray(),
                expectedLabel
            );
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                row,
                mark.rectTransform
            );
            Assert.That(bounds.size.x, Is.InRange(12f, 20f));
            Assert.That(bounds.size.y, Is.InRange(12f, 20f));
            Assert.That(bounds.center.y, Is.EqualTo(row.rect.center.y).Within(1f));
            RectTransform gutter = (RectTransform)mark.transform.parent;
            Bounds gutterBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                gutter,
                mark.rectTransform
            );
            Assert.That(gutterBounds.center.x, Is.EqualTo(gutter.rect.center.x).Within(0.5f));
            Assert.GreaterOrEqual(gutterBounds.min.x - gutter.rect.xMin, 6);
            Assert.GreaterOrEqual(gutter.rect.xMax - gutterBounds.max.x, 6);
            TMP_Dropdown selector = view.GetComponentsInChildren<TMP_Dropdown>(true).First();
            Image selectedGlyph = (Image)
                selector.template.GetComponentInChildren<Toggle>(true).graphic;
            Assert.AreSame(selectedGlyph.sprite.texture, mark.texture);
            RawImage checkboxGlyph = view.GetComponentsInChildren<UICheckboxView>(true)
                .First()
                .GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "Tick");
            Assert.AreSame(mark.texture, checkboxGlyph.texture);
            Assert.AreEqual(mark.uvRect, checkboxGlyph.uvRect);
            if (
                mode
                is ContentEditorColorMode.Dark
                    or ContentEditorColorMode.Empire
                    or ContentEditorColorMode.Dagobah
            )
            {
                Assert.AreEqual(Color.white, mark.color);
                Assert.AreEqual(Color.white, checkboxGlyph.color);
                Assert.AreEqual(Color.white, selectedGlyph.color);
            }
            RectTransform submenu = (RectTransform)row.parent;
            RectTransform parentPanel = (RectTransform)
                view.GetComponentsInChildren<ContextMenuCommandView>()
                    .Single(command =>
                        command
                            .GetComponentsInChildren<TMP_Text>()
                            .Any(label => label.text == "Theme")
                    )
                    .transform.parent;
            Assert.Greater(submenu.position.x, parentPanel.position.x);
        }

        [Test]
        public void ShowAlphaNotice_ConfirmationRequired_ConsumesEscapeAndAcknowledgesOnlyOnConfirm()
        {
            bool acknowledged = false;
            view.ShowAlphaNotice(() => acknowledged = true);
            ActionDialogView prompt = view.GetComponentInChildren<ActionDialogView>();
            Assert.IsTrue(prompt.TryCancel());
            Assert.IsTrue(prompt.gameObject.activeSelf);
            Assert.IsFalse(acknowledged);
            Button confirm = prompt.GetComponentsInChildren<Button>().Single();
            Assert.AreEqual("Confirm", confirm.GetComponentInChildren<TMP_Text>().text);
            TMP_Text heading = prompt
                .GetComponentsInChildren<TMP_Text>()
                .Single(text => text.text == "WARNING");
            Assert.IsTrue(heading.fontStyle.HasFlag(FontStyles.Bold));
            Assert.AreNotEqual(heading.color, heading.transform.parent.GetComponent<Image>().color);
            StringAssert.Contains(
                "not guaranteed",
                prompt
                    .GetComponentsInChildren<TMP_Text>()
                    .Single(text => text.text.Contains("alpha"))
                    .text
            );
            confirm.onClick.Invoke();
            Assert.IsTrue(acknowledged);
            Assert.IsFalse(prompt.gameObject.activeSelf);
            prompt.Show("Discard changes?", "Discard", () => { });
            Assert.IsFalse(heading.transform.parent.gameObject.activeSelf);
        }

        [Test]
        public void RenderProject_StackedInput_OutlinesRemainInsideTheFieldAndScrollMask()
        {
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            SelectRecord(officer);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "EncyclopediaTab")
                .onClick.Invoke();
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(control => control.name == "EncyclopediadescriptionField");
            field.SetLayout(new Rect(0, 0, 340, field.Height), 0);
            Canvas.ForceUpdateCanvases();
            TMP_InputField input = field.GetComponentInChildren<TMP_InputField>();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                field.transform,
                input.transform
            );
            float border = Mathf.Abs(input.GetComponent<Outline>().effectDistance.x);
            Assert.GreaterOrEqual(
                bounds.min.x - border,
                ((RectTransform)field.transform).rect.xMin
            );
        }

        [Test]
        public void RenderProject_FactionMissionRestrictions_UsesNamedCheckboxesForTheAuthoredSelection()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find("factions/Alliance");
            SelectPage(page);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab5")
                .onClick.Invoke();
            ContentEditorFieldView control = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field =>
                    field
                        .GetComponentsInChildren<TMP_Text>(true)
                        .Any(label => label.name == "Label" && label.text == "Disabled missions")
                );
            Assert.IsNull(control.GetComponentInChildren<TMP_InputField>());
            UICheckboxView checkbox = control
                .GetComponentsInChildren<UICheckboxView>()
                .Single(choice =>
                    choice.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "Diplomacy")
                );
            ContentPackEditorField field = page.Fields.Single(item =>
                item.MemberName == "DisallowedMissionTypeIDs"
            );
            Assert.AreEqual(field.Values.Contains("Diplomacy"), checkbox.IsChecked);
            Assert.AreEqual(
                field.Choices.Count,
                control.GetComponentsInChildren<UICheckboxView>().Length
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_FitMap_ResetsPanAndZoomWithoutChangingPlanetCoordinates()
        {
            SelectRecord(project.GetCatalogs().First(catalog => catalog.IsMap).Records.First());
            ContentEditorMapViewport viewport =
                view.GetComponentInChildren<ContentEditorMapViewport>();
            Button fit = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "FitMap");
            Assert.IsFalse(fit.interactable);
            RectTransform rect = (RectTransform)viewport.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center)
            );
            viewport.OnScroll(
                new PointerEventData(EventSystem.current)
                {
                    position = point,
                    scrollDelta = Vector2.up,
                }
            );
            Assert.IsFalse(viewport.IsFitted);
            Assert.IsTrue(fit.interactable);
            fit.onClick.Invoke();
            Assert.IsTrue(viewport.IsFitted);
            Assert.IsFalse(fit.interactable);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_FitMap_KeepsTheWholeSectorWindowInsideTheViewport()
        {
            SelectRecord(project.GetCatalogs().First(catalog => catalog.IsMap).Records.First());
            Canvas.ForceUpdateCanvases();
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "FitMap")
                .onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            RectTransform viewport = (RectTransform)
                view.GetComponentInChildren<ContentEditorMapViewport>().transform;
            RectTransform window = (RectTransform)
                view.GetComponentInChildren<PlanetSectorWindowView>().transform;
            Vector3[] corners = new Vector3[4];
            window.GetWorldCorners(corners);
            Vector3 firstCorner = viewport.InverseTransformPoint(corners[0]);
            Vector3 lastCorner = viewport.InverseTransformPoint(corners[2]);
            Assert.IsTrue(
                Mathf.Abs(lastCorner.x - firstCorner.x - viewport.rect.width) < 0.001f
                    || Mathf.Abs(lastCorner.y - firstCorner.y - viewport.rect.height) < 0.001f,
                "A fitted sector must fill at least one viewport dimension."
            );
            foreach (Vector3 corner in corners)
            {
                Vector3 point = viewport.InverseTransformPoint(corner);
                Assert.GreaterOrEqual(point.x, viewport.rect.xMin - 0.001f);
                Assert.GreaterOrEqual(point.y, viewport.rect.yMin - 0.001f);
                Assert.LessOrEqual(point.x, viewport.rect.xMax + 0.001f);
                Assert.LessOrEqual(point.y, viewport.rect.yMax + 0.001f);
            }
            RectTransform canvas = (RectTransform)view.transform;
            viewport.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 point = canvas.InverseTransformPoint(corner);
                Assert.GreaterOrEqual(point.y, canvas.rect.yMin - 0.001f);
                Assert.LessOrEqual(point.y, canvas.rect.yMax + 0.001f);
            }
        }

        [Test]
        public void RenderProject_AutoArrangeButton_RequestsGeometryWithoutEditingTheProject()
        {
            SelectRecord(project.GetCatalogs().First(catalog => catalog.IsMap).Records.First());
            string before = string.Join("\n", project.Documents.Select(document => document.Text));
            ContentEditorMapLayout requested = null;
            view.MapLayoutRequested += layout => requested = layout;
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "ArrangeMap")
                .onClick.Invoke();
            Assert.IsNotNull(requested);
            Assert.Greater(requested.Items.Count, 0);
            Assert.AreEqual(
                before,
                string.Join("\n", project.Documents.Select(document => document.Text))
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "FitMap")
                    .GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text == "Fit to view")
            );
        }

        [Test]
        public void RenderProject_FactionIconPreview_UsesEachFactionsArtworkWithoutEditingThePack()
        {
            SelectRecord(project.GetCatalogs().First(catalog => catalog.IsMap).Records.First());
            TMP_Dropdown chooser = view.GetComponentsInChildren<TMP_Dropdown>()
                .Single(dropdown => dropdown.name == "PreviewFaction");
            Assert.GreaterOrEqual(chooser.options.Count, 3);
            foreach (int index in new[] { 1, 2 })
            {
                chooser.value = index;
                FactionTheme theme = ContentEditorPresentation
                    .GetFactionThemes(project)[index - 1]
                    .Theme;
                Texture2D expected = assets.GetTexture(
                    theme.PlanetOverlayTheme.PlanetOverlayIcons.Fleets.NormalImagePath
                );
                Assert.IsTrue(
                    view.GetComponentsInChildren<PlanetSectorPlanetView>()
                        .First()
                        .GetComponentsInChildren<RawImage>()
                        .Any(image => image.texture == expected)
                );
            }
            chooser.value = 0;
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_TypingASearchMatchingTheSelectedRecord_RetainsInputFocus()
        {
            TMP_InputField search = view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search");
            GameObject events = new GameObject("Search focus test", typeof(EditModeEventSystem));
            try
            {
                EventSystem.current = events.GetComponent<EditModeEventSystem>();
                EventSystem.current.SetSelectedGameObject(search.gameObject);
                search.text = "a";
                Assert.AreSame(search.gameObject, EventSystem.current.currentSelectedGameObject);
                Assert.IsFalse(project.IsDirty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(events);
            }
        }

        [Test]
        public void RenderProject_CommittingAField_KeepsFocusOnTheNextControl()
        {
            GameObject events = new GameObject(
                "Field commit focus test",
                typeof(EditModeEventSystem)
            );
            try
            {
                EventSystem.current = events.GetComponent<EditModeEventSystem>();
                Button next = view.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "Test");
                EventSystem.current.SetSelectedGameObject(next.gameObject);
                TMP_InputField input = SkillInput();
                input.SetTextWithoutNotify(input.text == "101" ? "102" : "101");
                input.onEndEdit.Invoke(input.text);
                Assert.IsTrue(project.IsDirty);
                Assert.AreSame(next.gameObject, EventSystem.current.currentSelectedGameObject);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(events);
            }
        }

        [TestCase(ContentEditorColorMode.Dark)]
        [TestCase(ContentEditorColorMode.Light)]
        [TestCase(ContentEditorColorMode.Empire)]
        [TestCase(ContentEditorColorMode.Rebels)]
        [TestCase(ContentEditorColorMode.Dagobah)]
        public void SetColorMode_EditingATextDraft_RecolorsTheFormWithoutChangingItsValueOrHistory(
            ContentEditorColorMode mode
        )
        {
            TMP_InputField input = SkillInput();
            input.onSelect.Invoke(input.text);
            input.text = "Uncommitted draft";
            TextInputHistory history = input.GetComponent<TextInputHistory>();
            Image background = view.GetComponent<Image>();
            view.SetColorMode(mode);
            Assert.AreEqual(
                ContentEditorAppearance.ColorFor(mode, ContentEditorColorRole.Background),
                background.color
            );
            Assert.AreEqual("Uncommitted draft", input.text);
            Assert.IsTrue(history.CanUndo);
            Assert.IsFalse(project.IsDirty);
            Assert.Greater(input.textComponent.color.a, 0.9f);
            Assert.AreEqual(
                ContentEditorAppearance.ColorFor(mode, ContentEditorColorRole.Text),
                input.textComponent.color
            );
            Assert.AreEqual(
                ContentEditorAppearance.ColorFor(mode, ContentEditorColorRole.TextSelection),
                input.selectionColor
            );
            Button button = view.GetComponentsInChildren<Button>()
                .Single(control => control.name == "Duplicate");
            Assert.AreEqual(
                ContentEditorAppearance.ColorFor(mode, ContentEditorColorRole.Button),
                button.targetGraphic.color
            );
            Assert.AreEqual(
                ContentEditorAppearance.ColorFor(mode, ContentEditorColorRole.ButtonText),
                button.GetComponentInChildren<TMP_Text>().color
            );
            view.SetColorMode(ContentEditorColorMode.Dark);
            Assert.AreEqual("Uncommitted draft", input.text);
            Assert.IsTrue(history.CanUndo);
            Assert.Greater(input.textComponent.color.grayscale, 0.6f);
        }

        [TestCase(ContentEditorColorMode.Dark)]
        [TestCase(ContentEditorColorMode.Light)]
        [TestCase(ContentEditorColorMode.Empire)]
        [TestCase(ContentEditorColorMode.Rebels)]
        [TestCase(ContentEditorColorMode.Dagobah)]
        public void RenderProject_NewControlsAfterChangingAppearance_UseTheSelectedPalette(
            ContentEditorColorMode mode
        )
        {
            view.SetColorMode(mode);
            SelectRecord(
                project
                    .GetCatalogs()
                    .First(catalog => catalog.Name == "Starfighters")
                    .Records.First()
            );
            ContentEditorFieldView[] fields =
                view.GetComponentsInChildren<ContentEditorFieldView>();
            Assert.IsNotEmpty(fields);
            foreach (
                TMP_InputField input in fields.SelectMany(field =>
                    field.GetComponentsInChildren<TMP_InputField>()
                )
            )
            {
                Assert.AreEqual(
                    ContentEditorAppearance.ColorFor(mode, ContentEditorColorRole.Input),
                    input.GetComponent<Image>().color
                );
                Assert.AreEqual(
                    ContentEditorAppearance.ColorFor(mode, ContentEditorColorRole.Text),
                    input.textComponent.color
                );
            }
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_RunMenu_UsesScenarioFactionsAndLargeMediumDefaults()
        {
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Test")
                .onClick.Invoke();
            string[] captions = view.GetComponentsInChildren<ContextMenuCommandView>()
                .SelectMany(item => item.GetComponentsInChildren<TMP_Text>())
                .Select(label => label.text)
                .ToArray();
            foreach (string faction in project.GetFactionOptions().Values)
                CollectionAssert.Contains(captions, "Play as " + faction);
            CollectionAssert.Contains(captions, "Galaxy size: Large");
            CollectionAssert.Contains(captions, "Difficulty: Medium");
            ContentEditorPlayOptions? requested = null;
            view.PlayRequested += options => requested = options;
            ContextMenuCommandView command = view.GetComponentsInChildren<ContextMenuCommandView>()
                .First(item =>
                    item.GetComponentsInChildren<TMP_Text>()
                        .Any(label => label.text.StartsWith("Play as ", StringComparison.Ordinal))
                );
            command.OnPointerClick(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }
            );
            Assert.IsTrue(requested.HasValue);
            Assert.AreEqual(Rebellion.Game.GameSize.Large, requested.Value.GalaxySize);
            Assert.AreEqual(Rebellion.Game.GameDifficulty.Medium, requested.Value.Difficulty);
            Assert.AreEqual(project.Definition.DefaultScenarioID, requested.Value.ScenarioID);
            Assert.IsTrue(project.GetFactionOptions().ContainsKey(requested.Value.FactionID));
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_SelectedEvent_ShowsOnlyItsOwnXmlWithoutFlattenedActionFields()
        {
            ContentPackEditorRecord record = project
                .GetCatalogs()
                .First(catalog => catalog.CanAddEvent)
                .Records.First();
            SelectRecord(record);
            TMP_InputField xml = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "AdvancedXml");
            Assert.AreEqual(record.Element.ToString(), xml.text);
            Assert.AreEqual("GameEvent", System.Xml.Linq.XElement.Parse(xml.text).Name.LocalName);
            Assert.IsEmpty(view.GetComponentsInChildren<ContentEditorFieldView>());
            Assert.IsTrue(view.CommitFields());
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_ApplyingUnchangedEventXml_PreservesInheritanceAndSelection()
        {
            ContentPackEditorRecord original = project
                .GetCatalogs()
                .First(catalog => catalog.CanAddEvent)
                .Records.First();
            SelectRecord(original);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "ApplyXml")
                .onClick.Invoke();
            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(project.CanUndo);
            TMP_InputField xml = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "AdvancedXml");
            Assert.AreEqual(original.Element.ToString(), xml.text);
            Assert.IsEmpty(view.GetComponentsInChildren<ContentEditorFieldView>());
        }

        [Test]
        public void RenderProject_PlanetEncyclopedia_EditsEmbeddedDataBeforeTheStandaloneArticle()
        {
            ContentPackEditorRecord article = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "EncyclopediaEntry"
                    && record.Element.Element("Category")?.Value == "System"
                );
            ContentPackEditorRecord planet = project
                .GetCatalogs()
                .Where(catalog => catalog.IsMap)
                .SelectMany(catalog => catalog.Records)
                .SelectMany(sector => sector.Planets)
                .Single(record => record.ID == article.ID);
            SelectRecord(planet);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "EncyclopediaTab")
                .onClick.Invoke();
            TMP_InputField description = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.name == "EncyclopediadescriptionField")
                .GetComponentInChildren<TMP_InputField>();
            string articleBefore = article.Catalog.Document.Text;
            description.SetTextWithoutNotify("New planet article");
            description.onEndEdit.Invoke(description.text);
            Assert.AreEqual("New planet article", planet.GetField("EncyclopediaDescription").Value);
            Assert.AreEqual(articleBefore, article.Catalog.Document.Text);
            Assert.IsTrue(project.Undo());
        }

        [Test]
        public void RenderProject_MissionPages_CombineFactionArticlesAndExcludeAiWeights()
        {
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(project.GetCatalogs());
            navigation.SetExpanded(true);
            ContentEditorTreeNode[] pages = navigation
                .GetRows(string.Empty)
                .Where(node => node.Fields != null)
                .ToArray();
            ContentEditorTreeNode[] rules = pages
                .Where(page => page.Key.Contains("#rules/"))
                .ToArray();
            Assert.IsNotEmpty(rules);
            Assert.IsFalse(rules.SelectMany(page => page.Fields).Any(field => field.Group == "AI"));
            Assert.IsTrue(
                rules
                    .Single(page => page.Caption == "Difficulty")
                    .Fields.Any(field => field.MemberName == "MissionSuccessChancePoints")
            );
            ContentEditorTreeNode research = pages.Single(page => page.MissionID == "Diplomacy");
            Assert.IsTrue(research.Fields.Any(field => field.SectionPath.Contains("Diplomacy")));
            Assert.IsFalse(research.Fields.Any(field => field.Group == "AI"));
            Assert.AreEqual(2, research.RelatedRecords.Count);
            Assert.IsEmpty(research.Children);
            SelectPage(research);
            foreach (ContentPackEditorRecord article in research.RelatedRecords)
                Assert.IsTrue(
                    view.GetComponentsInChildren<RawImage>()
                        .Any(image => image.texture == assets.GetTexture(article.ImageAddress))
                );
            Assert.AreEqual(
                1,
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Count(row => row.GetComponent<RawImage>().color.a > 0)
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_UndoingAMessageEdit_RetainsTheSelectedMessageWithoutAnIdField()
        {
            ContentPackEditorRecord message = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind == "MessageDefinition")
                .Skip(1)
                .First();
            SelectRecord(message);
            string name = message.Name;
            TMP_InputField subject = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.name == "SubjectField")
                .GetComponentInChildren<TMP_InputField>();
            string before = subject.text;
            subject.SetTextWithoutNotify("Changed subject");
            subject.onEndEdit.Invoke(subject.text);
            Assert.IsTrue(project.Undo());
            workspace.RenderProject(project, assets);
            Assert.AreEqual(
                name,
                view.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "RecordName")
                    .text
            );
            Assert.AreEqual(
                before,
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(field => field.name == "SubjectField")
                    .GetComponentInChildren<TMP_InputField>()
                    .text
            );
        }

        [Test]
        public void CommitFields_TypedValue_TransfersUndoToDocumentHistory()
        {
            TMP_InputField input = SkillInput();
            string original = input.text;
            TextInputHistory history = input.GetComponent<TextInputHistory>();
            input.onSelect.Invoke(input.text);
            input.text = original == "123" ? "124" : "123";
            Assert.IsTrue(history.CanUndo);
            Assert.IsTrue(view.CommitFields());
            Assert.IsFalse(history.CanUndo);
            Assert.IsTrue(project.CanUndo);
            Assert.IsTrue(project.Undo());
            workspace.RenderProject(project, assets);
            Assert.AreEqual(original, SkillInput().text);
        }

        [Test]
        public void RenderProject_SeveralCommittedEdits_SupportsRepeatedUndoAndRedo()
        {
            string initial = SkillInput().text;
            foreach (string value in new[] { "101", "102", "103" })
            {
                TMP_InputField input = SkillInput();
                input.SetTextWithoutNotify(value);
                input.onEndEdit.Invoke(value);
            }
            foreach (string expected in new[] { "102", "101", initial })
            {
                Assert.IsTrue(project.Undo());
                workspace.RenderProject(project, assets);
                Assert.AreEqual(expected, SkillInput().text);
            }
            foreach (string expected in new[] { "101", "102", "103" })
            {
                Assert.IsTrue(project.Redo());
                workspace.RenderProject(project, assets);
                Assert.AreEqual(expected, SkillInput().text);
            }
        }

        [Test]
        public void ZoomContent_PendingDraft_PreservesItsValueAndLeavesTheMenuUnscaled()
        {
            TMP_InputField input = SkillInput();
            input.onSelect.Invoke(input.text);
            input.text = "Pending draft";
            Transform menu = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "File")
                .transform.parent;
            view.ZoomContent(0.2f);
            Assert.AreEqual("Pending draft", input.text);
            Assert.AreEqual(Vector3.one, menu.localScale);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_AdvancedUnit_ShowsOnlyTheSelectedOfficer()
        {
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab3")
                .onClick.Invoke();
            TMP_InputField input = view.GetComponentsInChildren<TMP_InputField>()
                .Single(field => field.name == "AdvancedXml");
            Assert.AreEqual("Officer", System.Xml.Linq.XElement.Parse(input.text).Name.LocalName);
        }

        [Test]
        public void RenderProject_MissionRules_UsesOnlyItsOwnSettingsAndCenteredSuccessTable()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find("missions/Diplomacy");
            SelectPage(page);
            Assert.IsFalse(
                view.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "Mission rules")
            );
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab5")
                .onClick.Invoke();
            ContentEditorNumericGrid grid = new(
                page.Fields.Where(field => field.Record.Kind == "GameConfig").ToArray(),
                page.MissionID
            );
            TMP_Text tablesHeading = view.GetComponentsInChildren<TMP_Text>()
                .Single(label => label.text == "Probability tables");
            Assert.AreEqual("Probability tables", tablesHeading.text);
            Assert.IsFalse(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text == "Shared settings")
            );
            CollectionAssert.AreEqual(new[] { "Success" }, grid.Columns);
            Assert.IsFalse(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text.Contains(" / Game modifier / "))
            );
            int column = grid.Columns.ToList().IndexOf("Success");
            Assert.GreaterOrEqual(column, 0);
            Assert.IsNull(grid.GetCell(-20, column));
            foreach (int score in grid.Scores)
                for (int table = 0; table < grid.Columns.Count; table++)
                {
                    ContentEditorFieldView cell =
                        view.GetComponentsInChildren<ContentEditorFieldView>()
                            .Single(item =>
                                item.name == "Probability" + table + "Score" + score + "Field"
                            );
                    TMP_InputField value = cell.GetComponentInChildren<TMP_InputField>();
                    Assert.IsTrue(value.interactable);
                    Assert.IsFalse(value.readOnly);
                    Assert.AreEqual(grid.GetCell(score, table).Value, value.text);
                    RectTransform rect = (RectTransform)cell.transform;
                    Bounds control = RectTransformUtility.CalculateRelativeRectTransformBounds(
                        rect,
                        value.transform
                    );
                    Assert.That(control.center.x, Is.EqualTo(rect.rect.center.x).Within(0.01f));
                    Assert.That(control.center.y, Is.EqualTo(rect.rect.center.y).Within(0.01f));
                    Assert.AreEqual(TextAlignmentOptions.Center, value.textComponent.alignment);
                }
            Assert.IsFalse(project.IsDirty);
            ContentPackEditorField target = grid.GetCell(0, column);
            string before = target.Value;
            TMP_InputField input = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.name == "Probability" + column + "Score0Field")
                .GetComponentInChildren<TMP_InputField>();
            input.SetTextWithoutNotify("43");
            input.onEndEdit.Invoke(input.text);
            Assert.AreEqual("43", target.Value);
            Assert.IsTrue(project.Undo());
            workspace.RenderProject(project, assets);
            page = navigation.Find("missions/Diplomacy");
            ContentPackEditorRecord rules = project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Rules")
                .Records.Single();
            Assert.AreEqual(
                before,
                rules
                    .Element.Element("ProbabilityTables")
                    .Element("Mission")
                    .Element("Diplomacy")
                    .Elements("Entry")
                    .Single(entry => entry.Element("Key").Value == "0")
                    .Element("Value")
                    .Value
            );
            Assert.IsFalse(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label =>
                        label.text == "GameConfig" || label.text.Contains("Inherited from")
                    )
            );
        }

        [TestCase("0")]
        [TestCase("37")]
        public void RenderProject_EmptyProbabilityCell_SavesAndReloadsAnEnteredValue(string value)
        {
            ContentEditorNumericGrid grid = SelectSharedMissionRules();
            int column = grid.Columns.ToList().IndexOf("Foil");
            Assert.AreEqual(string.Empty, grid.GetCell(0, column).Value);
            TMP_InputField input = ProbabilityInput(0, column);
            input.SetTextWithoutNotify(value);
            input.onEndEdit.Invoke(input.text);
            Assert.AreEqual(value, ProbabilityValue(0));
            ContentPackEditorStorage.Save(project);
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                project.ContentRootPath,
                folder
            );
            Assert.AreEqual(
                value,
                reopened
                    .GetCatalogs()
                    .Single(catalog => catalog.Name == "Rules")
                    .Records.Single()
                    .Element.Element("ProbabilityTables")
                    .Element("Mission")
                    .Element("Foil")
                    .Elements("Entry")
                    .Single(entry => entry.Element("Key").Value == "0")
                    .Element("Value")
                    .Value
            );
        }

        [TestCase("text")]
        [TestCase("3.5")]
        [TestCase("2147483648")]
        public void RenderProject_EmptyProbabilityCell_RejectsInvalidNumbersWithoutAddingAnEntry(
            string value
        )
        {
            ContentEditorNumericGrid grid = SelectSharedMissionRules();
            int column = grid.Columns.ToList().IndexOf("Foil");
            TMP_InputField input = ProbabilityInput(0, column);
            input.SetTextWithoutNotify(value);
            input.onEndEdit.Invoke(input.text);
            Assert.IsTrue(input.GetComponentInParent<ContentEditorFieldView>().HasError);
            Assert.AreEqual(string.Empty, ProbabilityValue(0));
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_OptionalProbabilityCell_ClearsRetypesAndRestoresThroughHistory()
        {
            ContentEditorNumericGrid grid = SelectSharedMissionRules();
            int column = grid.Columns.ToList().IndexOf("Foil");
            string before = project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Rules")
                .Document.Text;
            TMP_InputField input = ProbabilityInput(0, column);
            foreach (string value in new[] { "37", "", "64" })
            {
                input.SetTextWithoutNotify(value);
                input.onEndEdit.Invoke(input.text);
                Assert.IsFalse(input.GetComponentInParent<ContentEditorFieldView>().HasError);
                Assert.AreEqual(value, ProbabilityValue(0));
            }
            Assert.AreEqual(
                1,
                project
                    .GetCatalogs()
                    .Single(catalog => catalog.Name == "Rules")
                    .Records.Single()
                    .Element.Element("ProbabilityTables")
                    .Element("Mission")
                    .Element("Foil")
                    .Elements("Entry")
                    .Count(entry => entry.Element("Key").Value == "0")
            );
            for (int step = 0; step < 3; step++)
                Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                before,
                project.GetCatalogs().Single(catalog => catalog.Name == "Rules").Document.Text
            );
            for (int step = 0; step < 3; step++)
                Assert.IsTrue(project.Redo());
            workspace.RenderProject(project, assets);
            Assert.AreEqual("64", SelectSharedMissionRules().GetCell(0, column).Value);
        }

        [Test]
        public void RenderProject_EmptyProbabilityColumn_RemainsAvailableForNewValues()
        {
            ContentEditorNumericGrid grid = SelectSharedMissionRules();
            int column = grid.Columns.ToList().IndexOf("Foil");
            foreach (
                int score in grid.Scores.Where(score =>
                    grid.GetCell(score, column).Value.Length > 0
                )
            )
                project.SetField(grid.GetCell(score, column), string.Empty);
            workspace.RenderProject(project, assets);
            grid = SelectSharedMissionRules();
            column = grid.Columns.ToList().IndexOf("Foil");
            Assert.GreaterOrEqual(column, 0);
            Assert.IsTrue(grid.Scores.All(score => grid.GetCell(score, column).Value.Length == 0));
            TMP_InputField input = ProbabilityInput(0, column);
            input.SetTextWithoutNotify("45");
            input.onEndEdit.Invoke(input.text);
            Assert.AreEqual("45", ProbabilityValue(0));
        }

        [Test]
        public void ZoomContent_ClearedProbability_RetainsItsEditableRowAndCorrectBinding()
        {
            ContentEditorNumericGrid grid = SelectSharedMissionRules();
            int column = grid.Columns.ToList().IndexOf("Foil");
            int score = grid.Scores.First(value => grid.GetCell(value, column).Value.Length > 0);
            TMP_InputField input = ProbabilityInput(score, column);
            input.SetTextWithoutNotify(string.Empty);
            input.onEndEdit.Invoke(input.text);
            view.ZoomContent(0.1f);
            Canvas.ForceUpdateCanvases();
            input = ProbabilityInput(score, column);
            Assert.AreEqual(string.Empty, input.text);
            input.SetTextWithoutNotify("58");
            input.onEndEdit.Invoke(input.text);
            Assert.AreEqual("58", ProbabilityValue(score));
        }

        [TestCase(EventModifiers.Control)]
        [TestCase(EventModifiers.Command)]
        public void HandleKeyboardShortcut_SeveralXmlDraftEdits_UndoesAndRedoesEveryStep(
            EventModifiers modifier
        )
        {
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab3")
                .onClick.Invoke();
            TMP_InputField xml = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "AdvancedXml");
            string baseline = xml.text;
            xml.onSelect.Invoke(xml.text);
            foreach (
                string suffix in new[]
                {
                    "\n<!-- First -->",
                    "\n<!-- Second -->",
                    "\n<!-- Third -->",
                }
            )
                xml.text = baseline + suffix;
            view.UndoRequested += () => view.UndoText();
            view.RedoRequested += () => view.RedoText();
            foreach (string suffix in new[] { "\n<!-- Second -->", "\n<!-- First -->", "" })
            {
                Assert.IsTrue(
                    view.HandleKeyboardShortcut(
                        new Event
                        {
                            type = EventType.KeyDown,
                            keyCode = KeyCode.Z,
                            modifiers = modifier,
                        }
                    )
                );
                Assert.AreEqual(baseline + suffix, xml.text);
            }
            foreach (
                string suffix in new[]
                {
                    "\n<!-- First -->",
                    "\n<!-- Second -->",
                    "\n<!-- Third -->",
                }
            )
            {
                Assert.IsTrue(
                    view.HandleKeyboardShortcut(
                        new Event
                        {
                            type = EventType.KeyDown,
                            keyCode = KeyCode.Z,
                            modifiers = modifier | EventModifiers.Shift,
                        }
                    )
                );
                Assert.AreEqual(baseline + suffix, xml.text);
            }
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase(0.2f)]
        [TestCase(0.35f)]
        public void RenderProject_GalaxyPreview_RendersEveryAuthoredPlanetThroughGameplayStars(
            float split
        )
        {
            TMP_InputField search = view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search");
            search.text = "Galaxy";
            view.GetComponentsInChildren<ContentEditorRecordRowView>()
                .Single(row => row.Node.Key == "galaxy")
                .OnPointerDown(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    }
                );
            ContentEditorMapMarker[] clusters =
                view.GetComponentsInChildren<ContentEditorMapMarker>();
            view.GetComponentInChildren<ContentEditorWorkbenchLayout>().SetSplit(split);
            Canvas.ForceUpdateCanvases();
            Assert.IsNotEmpty(clusters);
            foreach (ContentEditorMapMarker cluster in clusters)
            {
                PlanetSectorClusterView gameplay = cluster.GetComponent<PlanetSectorClusterView>();
                Assert.IsNotNull(gameplay);
                Assert.AreEqual(
                    cluster.Record.Planets.Count,
                    gameplay
                        .GetComponentsInChildren<RawImage>()
                        .Count(image =>
                            image.name.StartsWith("Star", StringComparison.Ordinal)
                            && image.name != "StarImageTemplate"
                        )
                );
                RectTransform map = (RectTransform)
                    cluster.GetComponentInParent<ContentEditorGalaxyPreview>().transform.parent;
                Texture background = map.GetComponent<RawImage>().texture;
                float scale = map.rect.width / UILayout.GetTextureSourceWidth(background);
                RawImage[] stars = gameplay
                    .GetComponentsInChildren<RawImage>()
                    .Where(image =>
                        image.name.StartsWith("Star", StringComparison.Ordinal)
                        && image.name != "StarImageTemplate"
                    )
                    .ToArray();
                cluster.OnPointerEnter(new PointerEventData(EventSystem.current));
                for (int index = 0; index < stars.Length; index++)
                {
                    RawImage star = stars[index];
                    ContentPackEditorRecord planet = cluster.Record.Planets[index];
                    Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                        map,
                        star.rectTransform
                    );
                    Vector2Int source = UILayout.GetTextureSourceSize(star.texture);
                    Assert.That(
                        bounds.size.x,
                        Is.EqualTo(source.x * scale).Within(0.1f),
                        planet.Name
                            + " star width; rect="
                            + star.rectTransform.rect
                            + "; starScale="
                            + star.transform.localScale
                            + "; clusterScale="
                            + cluster.transform.localScale
                            + "; map="
                            + map.rect
                            + "; texture="
                            + star.texture.width
                            + "x"
                            + star.texture.height
                    );
                    Assert.That(
                        bounds.center.x,
                        Is.EqualTo(map.rect.xMin + (planet.X + source.x / 2f) * scale).Within(0.1f),
                        planet.Name + " star position"
                    );
                    Assert.That(
                        bounds.center.y,
                        Is.EqualTo(map.rect.yMax - (planet.Y + source.y / 2f) * scale).Within(0.1f),
                        planet.Name + " star vertical position"
                    );
                }
                cluster.OnPointerExit(new PointerEventData(EventSystem.current));
            }
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_CatalogCreateRow_AddsABlankRecordToThatCatalog()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == "Officers");
            TMP_InputField search = view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search");
            search.text = "Officers";
            int count = catalog.Records.Count;
            ContentEditorRecordRowView add =
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(row =>
                        row.Node.CreationCatalog == catalog
                        && row.Node.Key.EndsWith("#add", StringComparison.Ordinal)
                    );
            add.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }
            );
            Assert.AreEqual(count + 1, catalog.Records.Count);
            Assert.AreEqual("New officer", catalog.Records.Last().Name);
            Assert.IsTrue(project.Undo());
        }

        [Test]
        public void RenderProject_FactionReferenceArticle_EditsItsOwnDocumentAndSupportsUndo()
        {
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find("factions/Alliance");
            ContentPackEditorRecord article = page.RelatedRecords.Single(record =>
                record.Kind == "EncyclopediaEntry" && record.ID == "FLEET"
            );
            string before = article.Element.Element("Description").Value;
            SelectPage(page);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab8")
                .onClick.Invoke();
            Assert.IsTrue(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text == "Fleet encyclopedia")
            );
            ContentEditorFieldView control = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.name == "DescriptionField");
            TMP_InputField input = control.GetComponentInChildren<TMP_InputField>();
            input.SetTextWithoutNotify("Edited fleet article");
            input.onEndEdit.Invoke(input.text);
            Assert.AreEqual("Edited fleet article", article.Element.Element("Description").Value);
            Assert.IsTrue(project.Undo());
            workspace.RenderProject(project, assets);
            Assert.AreEqual(
                before,
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(field => field.name == "DescriptionField")
                    .GetComponentInChildren<TMP_InputField>()
                    .text
            );
            Assert.IsTrue(project.Redo());
            workspace.RenderProject(project, assets);
            Assert.AreEqual(
                "Edited fleet article",
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(field => field.name == "DescriptionField")
                    .GetComponentInChildren<TMP_InputField>()
                    .text
            );
        }

        [TestCase("General", "Tab0")]
        [TestCase("Rules", "Tab5")]
        [TestCase("Ship names", "Tab6")]
        [TestCase("Messages", "Tab7")]
        [TestCase("Artwork", "Tab1")]
        [TestCase("Encyclopedia", "Tab8")]
        public void RenderProject_FactionTabs_SeparateAuthoringTasks(string section, string tab)
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find("factions/Alliance");
            SelectPage(page);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == tab)
                .onClick.Invoke();
            ContentPackEditorField[] fields = page
                .Fields.Where(field =>
                    ContentEditorContentSections.FactionSection(field) == section
                )
                .Where(field =>
                    field.Record.Kind != "EncyclopediaEntry"
                    || field.MemberName
                        is not ("TypeID" or "Category" or "VisibleFactionInstanceID")
                )
                .ToArray();
            Assert.IsNotEmpty(fields);
            if (section is "Ship names" or "Messages")
            {
                XElement selected = view.GetComponentsInChildren<ContentEditorFieldView>()
                    .First()
                    .Field.XmlElement.AncestorsAndSelf()
                    .First(element => element.Name.LocalName is "NamePool" or "Message");
                fields = fields
                    .Where(field => field.XmlElement.AncestorsAndSelf().Contains(selected))
                    .ToArray();
            }
            Assert.AreEqual(
                fields.Length,
                view.GetComponentsInChildren<ContentEditorFieldView>().Length
            );
            string[] headings = view.GetComponentsInChildren<TMP_Text>()
                .Select(text => text.text)
                .ToArray();
            Assert.IsFalse(
                headings.Any(text =>
                    text.StartsWith("Array of message", StringComparison.OrdinalIgnoreCase)
                )
            );
            if (section == "Ship names")
            {
                Assert.IsTrue(
                    headings.Any(text => text.StartsWith("Pool ") && text.Contains(" — "))
                );
                Assert.IsFalse(headings.Any(text => text.StartsWith("Name pool 1")));
            }
            if (section == "Encyclopedia")
            {
                RawImage[] images = view.GetComponentsInChildren<RawImage>()
                    .Where(image =>
                        image.name.StartsWith("ArticleImageTemplate", StringComparison.Ordinal)
                    )
                    .ToArray();
                Assert.IsNotEmpty(images);
                foreach (RawImage image in images)
                {
                    Assert.IsNull(image.GetComponent<AspectRatioFitter>());
                    Assert.That(
                        image.rectTransform.rect.width / image.rectTransform.rect.height,
                        Is.EqualTo((float)image.texture.width / image.texture.height).Within(0.02f)
                    );
                }
            }
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase(ContentEditorColorMode.Dark)]
        [TestCase(ContentEditorColorMode.Light)]
        [TestCase(ContentEditorColorMode.Empire)]
        [TestCase(ContentEditorColorMode.Rebels)]
        [TestCase(ContentEditorColorMode.Dagobah)]
        public void SetColorMode_Scrollbars_KeepOpaqueContrastingHandlesWithoutContentTextures(
            ContentEditorColorMode mode
        )
        {
            view.SetColorMode(mode);
            Scrollbar[] bars = view.GetComponentsInChildren<Scrollbar>(true);
            Assert.IsNotEmpty(bars);
            foreach (Scrollbar bar in bars)
            {
                Graphic handle = bar.targetGraphic;
                Assert.AreEqual(1, handle.color.a);
                Assert.AreNotEqual(bar.GetComponent<Image>().color, handle.color);
                if (handle is RawImage image)
                {
                    Assert.IsNull(image.texture);
                    Assert.IsNull(image.GetComponent<ContentTextureBinding>());
                }
            }
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_AddStartingUnits_OffersNamedSelectionAndSavesQuantities()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("category/Starting setup"));
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "GenerationTab3")
                .onClick.Invoke();
            var collection = view.GetComponentsInChildren<CollectionEditorView>().Single();
            collection
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Add")
                .onClick.Invoke();
            var setup = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record => record.Kind == "GameGenerationConfig");
            XElement placement = setup
                .Element.Element("UnitDeployment")
                .Element("FixedGarrisons")
                .Elements()
                .Last();
            Assert.AreEqual(string.Empty, placement.Element("FactionID").Value);
            foreach (string member in new[] { "FactionID", "PlanetTypeID" })
            {
                ContentEditorFieldView control =
                    view.GetComponentsInChildren<ContentEditorFieldView>()
                        .Single(control => control.Field.MemberName == member);
                TMP_Dropdown choice = control.GetComponentInChildren<TMP_Dropdown>();
                Assert.IsNotNull(choice);
                Assert.IsNull(control.GetComponentInChildren<TMP_InputField>());
                choice.value = 1;
                Assert.IsNotEmpty(placement.Element(member).Value);
            }
            view.GetComponentsInChildren<Button>()
                .Single(button =>
                    button.name.StartsWith("SetupUnitsTemplate", StringComparison.Ordinal)
                )
                .onClick.Invoke();
            SelectionDialogView dialog = view.GetComponentInChildren<SelectionDialogView>();
            Assert.IsNotNull(dialog);
            SelectionOptionView row = dialog.GetComponentsInChildren<SelectionOptionView>().First();
            string caption = row.GetComponentInChildren<TMP_Text>().text;
            TMP_InputField quantity = row.GetComponentInChildren<TMP_InputField>();
            quantity.text = "4";
            dialog
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Apply")
                .onClick.Invoke();
            Assert.IsFalse(dialog.IsOpen);
            XElement unit = placement.Element("Units").Elements().Single();
            Assert.AreEqual(4, (int)unit.Element("Count"));
            Assert.IsTrue(caption.Contains(unit.Element("TypeID").Value));
        }

        [Test]
        public void RenderProject_UnitSelection_CancelLeavesStartingSetupUnchanged()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("category/Starting setup"));
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "GenerationTab3")
                .onClick.Invoke();
            view.GetComponentsInChildren<Button>()
                .Single(button =>
                    button.name.StartsWith("SetupUnitsTemplate", StringComparison.Ordinal)
                )
                .onClick.Invoke();
            var setup = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record => record.Kind == "GameGenerationConfig");
            string before = setup.Catalog.Document.Text;
            SelectionDialogView dialog = view.GetComponentInChildren<SelectionDialogView>();
            TMP_InputField quantity = dialog
                .GetComponentsInChildren<SelectionOptionView>()
                .First()
                .GetComponentInChildren<TMP_InputField>();
            quantity.text = "20";
            quantity.onEndEdit.Invoke(quantity.text);
            Assert.IsTrue(view.TryCancel());
            Assert.IsFalse(dialog.IsOpen);
            Assert.AreEqual(before, setup.Catalog.Document.Text);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_UnitSelection_SearchRetainsQuantitiesAndRejectsInvalidInput()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("category/Starting setup"));
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "GenerationTab3")
                .onClick.Invoke();
            view.GetComponentsInChildren<Button>()
                .Single(button =>
                    button.name.StartsWith("SetupUnitsTemplate", StringComparison.Ordinal)
                )
                .onClick.Invoke();
            SelectionDialogView dialog = view.GetComponentInChildren<SelectionDialogView>();
            SelectionOptionView row = dialog.GetComponentsInChildren<SelectionOptionView>().First();
            Assert.Greater(((RectTransform)row.transform).rect.width, 650);
            Assert.Greater(row.GetComponentInChildren<TMP_Text>().rectTransform.rect.width, 500);
            ScrollAreaView scroll = dialog.GetComponentInChildren<ScrollAreaView>();
            Assert.AreEqual(0, scroll.ContentRoot.anchoredPosition.y, 0.1f);
            TMP_InputField quantity = row.GetComponentInChildren<TMP_InputField>();
            quantity.text = "-1";
            Button apply = dialog
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Apply");
            apply.onClick.Invoke();
            Assert.IsTrue(dialog.IsOpen);
            Assert.IsTrue(row.HasError);
            quantity.text = "7";
            quantity.onEndEdit.Invoke(quantity.text);
            string caption = row.GetComponentInChildren<TMP_Text>().text;
            TMP_InputField search = dialog
                .GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "SelectionSearch");
            search.text = "no-such-selection";
            Assert.IsEmpty(dialog.GetComponentsInChildren<SelectionOptionView>());
            search.text = string.Empty;
            Assert.AreEqual(
                "7",
                dialog
                    .GetComponentsInChildren<SelectionOptionView>()
                    .Single(option => option.GetComponentInChildren<TMP_Text>().text == caption)
                    .GetComponentInChildren<TMP_InputField>()
                    .text
            );
            view.TryCancel();
        }

        [Test]
        public void RenderProject_HeadquartersBuildings_UsesQuantityPickerWithoutChangingExistingOrder()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("category/Starting setup"));
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "GenerationTab2")
                .onClick.Invoke();
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.Field.MemberName == "FacilityTypeIDs");
            string before = field.Field.Document.Text;
            Assert.IsNull(field.GetComponentInChildren<TMP_InputField>());
            field
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "SelectItems")
                .onClick.Invoke();
            SelectionDialogView dialog = view.GetComponentInChildren<SelectionDialogView>();
            Assert.IsTrue(
                dialog.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "Quantity")
            );
            dialog
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Apply")
                .onClick.Invoke();
            Assert.AreEqual(before, field.Field.Document.Text);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_OfficerDestination_ReplacesOtherDestinationAndKeepsSchemaOrder()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("category/Starting setup"));
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "GenerationTab1")
                .onClick.Invoke();
            ContentEditorFieldView specific = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.Field.MemberName == "DestinationInstanceID");
            specific.GetComponentInChildren<TMP_Dropdown>().value = 1;
            UIComponentTestHelper.InvokeLifecycle(view, "LateUpdate");
            var setup = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record => record.Kind == "GameGenerationConfig");
            XElement officer = setup
                .Element.Element("Officers")
                .Element("StartingOfficers")
                .Elements()
                .First();
            Assert.IsNull(officer.Element("DestinationTypeID"));
            Assert.IsNotNull(officer.Element("DestinationInstanceID"));
            ContentEditorFieldView sizes = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.Field.MemberName == "GalaxySizes");
            UICheckboxView checkbox = sizes.GetComponentsInChildren<UICheckboxView>().First();
            UIComponentTestHelper.InvokeLifecycle(checkbox, "Awake");
            checkbox.GetComponent<Toggle>().isOn = true;
            CollectionAssert.AreEqual(
                new[] { "OfficerInstanceID", "GalaxySizes", "DestinationInstanceID" },
                officer.Elements().Select(element => element.Name.LocalName)
            );
            checkbox.GetComponent<Toggle>().isOn = false;
            Assert.IsNull(officer.Element("GalaxySizes"));
            System.Xml.Schema.XmlSchemaSet schemas = new();
            schemas.Add(
                null,
                Path.Combine(
                    project.ContentRootPath,
                    "Application",
                    "Schemas",
                    "generation-config.xsd"
                )
            );
            Assert.DoesNotThrow(() =>
                setup.Element.Document.Validate(
                    schemas,
                    (sender, error) =>
                    {
                        throw error.Exception;
                    }
                )
            );
        }

        [TestCase("Galaxy", 0)]
        [TestCase("Officers", 1)]
        [TestCase("Buildings", 2)]
        [TestCase("Units", 3)]
        [TestCase("Resources", 4)]
        [TestCase("Support", 5)]
        [TestCase("Balance", 6)]
        public void RenderProject_GenerationTabs_ShowOnlyTheSelectedTask(string section, int index)
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("category/Starting setup"));
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "GenerationTab" + index)
                .onClick.Invoke();
            ContentEditorFieldView[] fields =
                view.GetComponentsInChildren<ContentEditorFieldView>();
            Assert.IsNotEmpty(fields);
            Assert.IsTrue(
                fields.All(field => field.Field.Document != null),
                "Generation controls must edit authored pack data."
            );
            Assert.IsTrue(
                fields.All(field =>
                    ContentEditorContentSections.GenerationSection(field.Field) == section
                )
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_AdvancedXml_ProvidesAConnectedScrollbar()
        {
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab3")
                .onClick.Invoke();
            TMP_InputField xml = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "AdvancedXml");
            Assert.IsNotNull(xml.verticalScrollbar);
            Assert.IsTrue(xml.verticalScrollbar.gameObject.activeInHierarchy);
            Assert.Less(xml.textViewport.rect.width, ((RectTransform)xml.transform).rect.width);
        }

        [Test]
        public void ValidateFields_StoredInvalidNumber_BlocksSavingWithoutTrappingNavigation()
        {
            ContentPackEditorCatalog ships = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Capital ships");
            XDocument document = XDocument.Parse(ships.Document.Text);
            document.Root.Elements().First().SetElementValue("Hyperdrive", "invalid");
            project.SetDocumentText(ships.Document, document.ToString());
            workspace.RenderProject(project, assets);
            SelectRecord(
                project
                    .GetCatalogs()
                    .First(catalog => catalog.Name == "Capital ships")
                    .Records.First()
            );

            Assert.IsTrue(view.CommitFields());
            Assert.IsFalse(view.ValidateFields());
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(field => field.Field.MemberName == "Hyperdrive")
                    .HasError
            );
        }

        [Test]
        public void RenderProject_SpecialForcesAllowedMissions_UsesASelectableMissionSet()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "SpecialForce"
                    && record.Element.Element("AllowedMissionTypeIDs") != null
                );
            SelectRecord(unit);
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(control => control.Field.MemberName == "AllowedMissionTypeIDs");

            Assert.Greater(field.GetComponentsInChildren<UICheckboxView>().Length, 1);
            Assert.IsEmpty(field.GetComponentsInChildren<TMP_InputField>());
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase("Buildings")]
        [TestCase("Capital ships")]
        [TestCase("Starfighters")]
        public void RenderProject_ManufacturingResearchRequirements_AppearWithProductionInsteadOfSkills(
            string catalogName
        )
        {
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == catalogName).Records.First()
            );

            string[] headings = view.GetComponentsInChildren<TMP_Text>()
                .Select(text => text.text)
                .ToArray();
            Assert.Contains("Production", headings);
            Assert.IsFalse(headings.Contains("Skills"));
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Any(field => field.Field.MemberName == "ResearchDifficulty")
            );
        }

        [Test]
        public void ShowNewPackDialog_LongError_KeepsTheCompleteMessageAboveItsActions()
        {
            view.ShowNewPackDialog();
            ContentEditorPackDialogView dialog =
                view.GetComponentInChildren<ContentEditorPackDialogView>();
            string message = string.Join(
                " ",
                Enumerable.Repeat("Choose another destination for this content pack.", 10)
            );

            dialog.ShowError(message);

            TMP_Text error = dialog
                .GetComponentsInChildren<TMP_Text>()
                .Single(text => text.name == "Error");
            RectInt bounds = UILayout.GetSourceRect(error.rectTransform);
            RectInt confirm = UILayout.GetSourceRect(
                (RectTransform)
                    dialog
                        .GetComponentsInChildren<Button>()
                        .Single(button => button.name == "Confirm")
                        .transform
            );
            Assert.GreaterOrEqual(
                bounds.height,
                error.GetPreferredValues(message, bounds.width, 0).y
            );
            Assert.Greater(confirm.y, bounds.yMax);
        }

        [Test]
        public void RenderProject_AttachingOptionalTraitMember_KeepsOtherDraftsBoundToTheirOwnFields()
        {
            ContentPackEditorRecord trait = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Trait");
            XDocument document = XDocument.Parse(trait.Catalog.Document.Text);
            XElement element = document.Descendants("Trait").First();
            foreach (
                string member in new[]
                {
                    "LeadershipBonusPercent",
                    "PersonnelTravelTimePercent",
                    "SharesPersonnelTravelTimeModifier",
                }
            )
                element.Element(member)?.Remove();
            project.SetDocumentText(trait.Catalog.Document, document.ToString());
            workspace.RenderProject(project, assets);
            trait = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Trait");
            SelectRecord(trait);
            ContentEditorFieldView leadership =
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(field => field.Field.MemberName == "LeadershipBonusPercent");
            TMP_InputField draft = leadership.GetComponentInChildren<TMP_InputField>();
            draft.text = "invalid";
            draft.onEndEdit.Invoke(draft.text);
            ContentEditorFieldView sharing = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.Field.MemberName == "SharesPersonnelTravelTimeModifier");
            UIComponentTestHelper.InvokeLifecycle(
                sharing.GetComponentInChildren<UICheckboxView>(),
                "Awake"
            );
            sharing.GetComponentInChildren<Toggle>().isOn = true;
            Assert.AreEqual(
                "true",
                trait.Element.Element("SharesPersonnelTravelTimeModifier")?.Value
            );
            view.GetComponentInChildren<ContentEditorFormView>()
                .Render(project, assets, trait, null, "General", false);

            Assert.AreEqual("invalid", draft.text);
            Assert.IsTrue(leadership.HasError);
            foreach (
                ContentEditorFieldView field in view.GetComponentsInChildren<ContentEditorFieldView>()
            )
                Assert.AreEqual(
                    field.Field.Name.Replace(" / ", " ").Replace(" ", string.Empty) + "Field",
                    field.name
                );
        }

        [Test]
        public void RenderProject_TraitWithOmittedModifiers_ShowsNeutralDefaultsWithoutAuthoringThem()
        {
            ContentPackEditorRecord trait = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Trait");
            XDocument document = XDocument.Parse(trait.Catalog.Document.Text);
            XElement element = document.Descendants("Trait").First();
            element.Element("PersonnelTravelTimePercent")?.Remove();
            project.SetDocumentText(trait.Catalog.Document, document.ToString());
            workspace.RenderProject(project, assets);
            trait = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Trait");
            string before = trait.Catalog.Document.Text;

            SelectRecord(trait);

            Assert.AreEqual(
                "100",
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(control => control.Field.MemberName == "PersonnelTravelTimePercent")
                    .Field.Value
            );
            Assert.AreEqual(before, trait.Catalog.Document.Text);
            Assert.IsNull(trait.Element.Element("PersonnelTravelTimePercent"));
        }

        [Test]
        public void RenderProject_FactionReferences_UseNamesAndStoreTheSelectedIdentity()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(control => control.Field.MemberName == "HQInstanceID");
            var options = project.GetReferenceOptions(field.Field).ToArray();
            TMP_Dropdown selector = field.GetComponentInChildren<TMP_Dropdown>();
            CollectionAssert.AreEqual(
                options.Select(option => option.Value),
                selector.options.Select(option => option.text)
            );
            int index = Array.FindIndex(
                options,
                option => option.Key != field.Field.Value && option.Key.Length > 0
            );

            selector.value = index;

            Assert.AreEqual(options[index].Key, field.Field.Value);
            Assert.IsTrue(project.IsDirty);
        }

        [Test]
        public void RenderProject_InvalidDraft_RevertRestoresTheAcceptedValueWithoutChangingHistory()
        {
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(control => control.Field.MemberName == "TypeID");
            string accepted = field.Field.Value;
            TMP_InputField input = field.GetComponentInChildren<TMP_InputField>();
            input.text = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records[1]
                .ID;
            input.onEndEdit.Invoke(input.text);
            Assert.IsTrue(field.HasError);

            field
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Revert")
                .onClick.Invoke();

            Assert.AreEqual(accepted, input.text);
            Assert.IsFalse(field.HasError);
            Assert.IsTrue(view.CommitFields());
            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(project.Undo());
        }

        [Test]
        public void RenderProject_StartingMessages_SelectingAnEntryDoesNotDuplicateOrEditOtherMessages()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab7")
                .onClick.Invoke();
            TMP_Dropdown selector = view.GetComponentsInChildren<TMP_Dropdown>()
                .Single(control => control.name == "CollectionSelection");
            Assert.Greater(selector.options.Count, 1);
            Assert.AreEqual(
                1,
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Count(control => control.Field.MemberName == "Title")
            );

            selector.value = 1;

            Assert.AreEqual(
                selector.options[1].text,
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(control => control.Field.MemberName == "Title")
                    .Field.Value
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_DeletingStartingMessage_UndoRestoresTheExactCollection()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find("factions/Alliance");
            ContentPackEditorRecord faction = page.RelatedRecords.Single(record =>
                record.Kind == "Faction" && record.Element.Element("InstanceID") != null
            );
            string before = faction.Element.Element("Messages").ToString();
            SelectPage(page);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab7")
                .onClick.Invoke();
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "RemoveStartingMessage")
                .onClick.Invoke();
            view.GetComponentsInChildren<ActionDialogView>()
                .Single()
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Primary")
                .onClick.Invoke();
            Assert.AreNotEqual(before, faction.Element.Element("Messages").ToString());

            Assert.IsTrue(project.Undo());

            Assert.AreEqual(
                before,
                project
                    .GetCatalogs()
                    .SelectMany(catalog => catalog.Records)
                    .Single(record =>
                        record.Kind == "Faction"
                        && record.Element.Element("InstanceID")?.Value == faction.ID
                    )
                    .Element.Element("Messages")
                    .ToString()
            );
        }

        [Test]
        public void RenderProject_AddingShipNamePool_CreatesAnEmptyPoolAndFocusesItsIdentity()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find("factions/Alliance");
            ContentPackEditorRecord faction = page.RelatedRecords.Single(record =>
                record.Kind == "Faction" && record.Element.Element("InstanceID") != null
            );
            int before = faction.Element.Element("ShipNamePools").Elements().Count();
            SelectPage(page);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab6")
                .onClick.Invoke();

            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "AddShipNamePool")
                .onClick.Invoke();

            Assert.AreEqual(
                before + 1,
                faction.Element.Element("ShipNamePools").Elements().Count()
            );
            XElement added = faction.Element.Element("ShipNamePools").Elements().Last();
            Assert.IsEmpty(added.Element("Names").Elements());
            Assert.AreSame(
                added,
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(control => control.Field.MemberName == "NamePoolID")
                    .Field.XmlElement.Parent
            );
            Assert.IsTrue(project.Undo());
        }

        [Test]
        public void RenderProject_AddingProbabilityThreshold_LeavesXmlUntouchedUntilAValueIsEntered()
        {
            SelectSharedMissionRules();
            TMP_InputField score = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "NewScore");
            score.text = "1234";
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "AddScore")
                .onClick.Invoke();
            Assert.IsFalse(project.IsDirty);
            TMP_InputField cell = ProbabilityInput(1234, 0);

            cell.text = "42";
            cell.onEndEdit.Invoke(cell.text);

            Assert.IsTrue(project.IsDirty);
            Assert.AreEqual("42", cell.GetComponentInParent<ContentEditorFieldView>().Field.Value);
            Assert.IsTrue(project.Undo());
        }

        [Test]
        public void RenderProject_AdvancedXml_EnablesDraftActionsOnlyWhileEdited()
        {
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab3")
                .onClick.Invoke();
            TMP_InputField input = view.GetComponentsInChildren<TMP_InputField>()
                .Single(control => control.name == "AdvancedXml");
            Button apply = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "ApplyXml");
            Button discard = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "DiscardXml");
            string before = input.text;
            Assert.IsFalse(apply.interactable);
            Assert.IsFalse(discard.interactable);

            input.text += "\n";

            Assert.IsTrue(apply.interactable);
            Assert.IsTrue(discard.interactable);
            discard.onClick.Invoke();
            view.GetComponentInChildren<ActionDialogView>()
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Primary")
                .onClick.Invoke();
            Assert.AreEqual(before, input.text);
            Assert.IsFalse(apply.interactable);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_AddStartingMessage_OpensABlankMessageAndSupportsUndo()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find("factions/Alliance");
            ContentPackEditorRecord faction = page.RelatedRecords.Single(record =>
                record.Kind == "Faction" && record.Element.Element("InstanceID") != null
            );
            int before = faction.Element.Descendants("Message").Count();
            SelectPage(page);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab7")
                .onClick.Invoke();
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "AddFactionMessage")
                .onClick.Invoke();
            view.GetComponentsInChildren<ContextMenuCommandView>()
                .Single(item =>
                    item.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "Advice")
                )
                .OnPointerClick(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    }
                );
            Assert.AreEqual(before + 1, faction.Element.Descendants("Message").Count());
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Any(field =>
                        field.Field.MemberName == "Title" && field.Field.Value == "New message"
                    )
            );
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                before,
                project
                    .GetCatalogs()
                    .SelectMany(catalog => catalog.Records)
                    .Single(record =>
                        record.Kind == "Faction"
                        && record.Element.Element("InstanceID")?.Value == faction.ID
                    )
                    .Element.Descendants("Message")
                    .Count()
            );
        }

        [Test]
        public void RenderProject_NarrowMapProperties_GivesNamesTheFullPropertyColumn()
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(1000, 900);
            Canvas.ForceUpdateCanvases();
            SelectRecord(project.GetCatalogs().First(catalog => catalog.IsMap).Records.First());
            Canvas.ForceUpdateCanvases();
            ContentEditorFieldView name = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.Field.MemberName == "DisplayName");
            TMP_InputField input = name.GetComponentInChildren<TMP_InputField>();

            Assert.GreaterOrEqual(
                ((RectTransform)input.transform).rect.width,
                ((RectTransform)name.transform).rect.width - 16
            );
        }

        [TestCase(900, 1f)]
        [TestCase(1600, 1f)]
        [TestCase(1600, 1.5f)]
        public void RenderProject_MapHintWrapping_KeepsToolbarAndPreviewInSeparateRows(
            int width,
            float zoom
        )
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(width, 900);
            view.ZoomContent(zoom - 1);
            Canvas.ForceUpdateCanvases();
            SelectRecord(project.GetCatalogs().First(catalog => catalog.IsMap).Records.First());
            Canvas.ForceUpdateCanvases();
            TMP_Text hint = view.GetComponentsInChildren<TMP_Text>()
                .Single(label => label.name == "Hint");
            RectTransform hintRect = hint.rectTransform;
            RectTransform toolbar = (RectTransform)hint.transform.parent.Find("MapToolbar");
            RectTransform viewport = (RectTransform)hint.transform.parent.Find("MapViewport");
            RectTransform tabs = (RectTransform)
                view.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "Tab0")
                    .transform.parent;
            Assert.Less(
                hintRect.TransformPoint(hintRect.rect.max).y,
                toolbar.TransformPoint(toolbar.rect.min).y
            );
            Assert.GreaterOrEqual(
                hintRect.rect.height + 1,
                hint.GetPreferredValues(hint.text, hintRect.rect.width, float.PositiveInfinity).y
            );
            Assert.Less(
                viewport.TransformPoint(viewport.rect.max).y,
                hintRect.TransformPoint(hintRect.rect.min).y
            );
            PlanetSectorWindowView sector = view.GetComponentInChildren<PlanetSectorWindowView>();
            RectTransform sectorRect = (RectTransform)sector.transform;
            Assert.Less(
                sectorRect.TransformPoint(sectorRect.rect.max).y,
                tabs.TransformPoint(tabs.rect.min).y
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_SectorPreview_UsesGameplayPlanetsAndNeutralStatistics()
        {
            ContentPackEditorRecord sector = project
                .GetCatalogs()
                .First(catalog => catalog.IsMap)
                .Records.First();
            string before = sector.Catalog.Document.Text;
            SelectRecord(sector);
            RawImage backdrop = view.GetComponentsInChildren<RawImage>()
                .Single(image => image.name == "Map");
            Assert.IsNull(backdrop.texture);
            Assert.AreEqual(Color.black, backdrop.color);
            PlanetSectorWindowView window = view.GetComponentInChildren<PlanetSectorWindowView>();
            Assert.IsNotNull(window);
            RectTransform mapBack = (RectTransform)
                view.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "Galaxy")
                    .transform;
            RectTransform heading = view.GetComponentsInChildren<TMP_Text>()
                .Single(label => label.name == "RecordInfo")
                .rectTransform;
            Assert.Less(
                mapBack.TransformPoint(mapBack.rect.max).y,
                heading.TransformPoint(heading.rect.min).y
            );
            PlanetSectorPlanetView[] planets =
                window.GetComponentsInChildren<PlanetSectorPlanetView>();
            Assert.AreEqual(sector.Planets.Count, planets.Length);
            ContentPackEditorRecord[] records = sector.Planets.ToArray();
            int maximumEnergy = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind == "GameGenerationConfig")
                .SelectMany(record => record.Element.Descendants("EnergyMax"))
                .Max(element => int.Parse(element.Value));
            for (int i = 0; i < planets.Length; i++)
            {
                Assert.IsTrue(
                    planets[i]
                        .GetComponentsInChildren<TMP_Text>()
                        .Any(label => label.text == records[i].Name)
                );
                Image support = planets[i]
                    .transform.Find("SupportBar/BackgroundImage")
                    .GetComponent<Image>();
                Assert.AreEqual(
                    ContentEditorPresentation.GetNeutralTheme(project).GetPrimaryColor(),
                    support.color
                );
                Assert.AreEqual(
                    maximumEnergy,
                    planets[i]
                        .transform.Find("EnergyBar")
                        .GetComponentsInChildren<Image>()
                        .Count(image => image.name.StartsWith("Segment", StringComparison.Ordinal))
                );
                RectInt imageRect = planets[i].GetRenderedPlanetImageSourceRect();
                Vector2Int offset = window.UnprojectPlanetPosition(
                    new Vector2(imageRect.x, imageRect.y)
                );
                Assert.That(offset.x, Is.EqualTo(records[i].X - sector.X).Within(1));
                Assert.That(offset.y, Is.EqualTo(records[i].Y - sector.Y).Within(1));
            }
            Assert.AreEqual(before, sector.Catalog.Document.Text);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_ReturningToGalaxy_RestoresTheOverviewBackground()
        {
            SelectRecord(project.GetCatalogs().First(catalog => catalog.IsMap).Records.First());
            RawImage backdrop = view.GetComponentsInChildren<RawImage>()
                .Single(image => image.name == "Map");
            Assert.IsNull(backdrop.texture);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Galaxy")
                .onClick.Invoke();
            Assert.IsNotNull(backdrop.texture);
            view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search")
                .text = string.Empty;
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row => row.Node?.Record?.Kind == "PlanetSector")
            );
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AddPlanet_SectorCreateActions_AddsToTheSelectedSectorAndSupportsUndo(bool menu)
        {
            ContentPackEditorRecord sector = project
                .GetCatalogs()
                .First(catalog => catalog.IsMap)
                .Records.First();
            int before = sector.Planets.Count;
            SelectRecord(sector);
            if (menu)
            {
                view.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "AddUnit")
                    .onClick.Invoke();
                view.GetComponentsInChildren<ContextMenuCommandView>()
                    .Single(command =>
                        command
                            .GetComponentsInChildren<TMP_Text>()
                            .Any(label => label.text == "Planet")
                    )
                    .OnPointerClick(
                        new PointerEventData(EventSystem.current)
                        {
                            button = PointerEventData.InputButton.Left,
                        }
                    );
            }
            else
            {
                TMP_InputField search = view.GetComponentsInChildren<TMP_InputField>(true)
                    .Single(input => input.name == "Search");
                search.SetTextWithoutNotify(string.Empty);
                search.onValueChanged.Invoke(string.Empty);
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(row => row.Node.CreationParent?.Element == sector.Element)
                    .OnPointerDown(
                        new PointerEventData(EventSystem.current)
                        {
                            button = PointerEventData.InputButton.Left,
                        }
                    );
            }
            ContentPackEditorRecord updated = project
                .GetCatalogs()
                .First(catalog => catalog.IsMap)
                .Records.First();
            Assert.AreEqual(before + 1, updated.Planets.Count);
            ContentPackEditorRecord created = updated.Planets.Last();
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row => row.Node.Record?.Element == created.Element)
            );
            PlanetSectorWindowView window = view.GetComponentInChildren<PlanetSectorWindowView>();
            PlanetSectorPlanetView createdView = window
                .GetComponentsInChildren<PlanetSectorPlanetView>()
                .Last();
            RectInt image = createdView.GetRenderedPlanetImageSourceRect();
            RectInt bounds = UILayout.GetSourceRect((RectTransform)window.transform);
            Assert.Greater(image.x, 0);
            Assert.Greater(image.y, 0);
            Assert.Less(image.xMax, bounds.width);
            Assert.Less(image.yMax, bounds.height);
            Assert.AreEqual(
                "New planet",
                view.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "RecordName")
                    .text
            );
            Assert.AreEqual(
                before + 1,
                view.GetComponentsInChildren<PlanetSectorPlanetView>().Length
            );
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                before,
                project.GetCatalogs().First(catalog => catalog.IsMap).Records.First().Planets.Count
            );
        }

        [Test]
        public void RenderProject_Galaxy_SelectsOverviewAndListsSectorsDirectly()
        {
            TMP_InputField search = view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search");
            search.text = "Galaxy";
            ContentEditorRecordRowView galaxy =
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(row => row.Node.Key == "galaxy");
            Assert.IsTrue(
                galaxy
                    .Node.Children.Where(node => node.Record != null)
                    .All(node => node.Depth == 1 && node.Record.Kind == "PlanetSector")
            );
            galaxy.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }
            );
            Assert.IsNull(view.GetComponentInChildren<PlanetSectorWindowView>());
            Assert.AreEqual(
                "Galaxy",
                view.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "RecordName")
                    .text
            );
            search.text = string.Empty;
            workspace.RenderProject(project, assets);
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row => row.Node?.Record?.Kind == "PlanetSector")
            );
        }

        [TestCase(EventModifiers.Control)]
        [TestCase(EventModifiers.Command)]
        public void HandleKeyboardShortcut_SaveWithCreationRowFocused_DoesNotCreatePlanets(
            EventModifiers modifier
        )
        {
            ContentPackEditorRecord sector = project
                .GetCatalogs()
                .First(catalog => catalog.IsMap)
                .Records.First();
            ContentPackEditorRecord last = sector.Planets.Last();
            int before = sector.Planets.Count;
            SelectRecord(last);
            view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search")
                .text = string.Empty;
            ContentEditorRecordRowView row =
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(item => item.Node?.Record?.Element == last.Element);
            row.OnMove(new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down });
            Assert.AreEqual(
                before,
                sector.Planets.Count,
                "Navigating onto Add planet must not execute it."
            );
            int saves = 0;
            view.SaveRequested += () => saves++;
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(
                    view.HandleKeyboardShortcut(
                        new Event
                        {
                            type = EventType.KeyDown,
                            keyCode = KeyCode.S,
                            modifiers = modifier,
                        }
                    )
                );
            Assert.AreEqual(5, saves);
            Assert.AreEqual(before, sector.Planets.Count);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_GalaxyAndSectorRowClicks_ExpandAndCollapseTheirChildren()
        {
            view.ClearProject();
            workspace.RenderProject(project, assets);
            ContentPackEditorRecord sector = project
                .GetCatalogs()
                .First(catalog => catalog.IsMap)
                .Records.First();
            PointerEventData pointer = new(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            };
            ContentEditorRecordRowView galaxy =
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(row => row.Node?.Key == "galaxy");
            galaxy.OnPointerDown(pointer);
            Assert.AreEqual(
                "Galaxy",
                view.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "RecordName")
                    .text
            );
            ContentEditorRecordRowView sectorRow =
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(row => row.Node?.Record?.Element == sector.Element);
            sectorRow.OnPointerDown(pointer);
            Assert.IsNotNull(view.GetComponentInChildren<PlanetSectorWindowView>());
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row => row.Node?.CreationParent?.Element == sector.Element)
            );
            view.GetComponentsInChildren<ContentEditorRecordRowView>()
                .Single(row => row.Node?.Record?.Element == sector.Element)
                .OnPointerDown(pointer);
            Assert.IsFalse(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row => row.Node?.CreationParent?.Element == sector.Element)
            );
            view.GetComponentsInChildren<ContentEditorRecordRowView>()
                .Single(row => row.Node?.Key == "galaxy")
                .OnPointerDown(pointer);
            Assert.IsFalse(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row => row.Node?.Record?.Kind == "PlanetSector")
            );
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AddSector_MenuOrGalaxyRow_CreatesAndOpensAnEmptySector(bool menu)
        {
            ContentPackEditorCatalog galaxy = project.GetCatalogs().First(catalog => catalog.IsMap);
            int before = galaxy.Records.Count;
            if (menu)
            {
                view.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "AddUnit")
                    .onClick.Invoke();
                view.GetComponentsInChildren<ContextMenuCommandView>()
                    .Single(command =>
                        command
                            .GetComponentsInChildren<TMP_Text>()
                            .Any(label => label.text == "Planet sector")
                    )
                    .OnPointerClick(
                        new PointerEventData(EventSystem.current)
                        {
                            button = PointerEventData.InputButton.Left,
                        }
                    );
            }
            else
            {
                view.GetComponentsInChildren<TMP_InputField>(true)
                    .Single(input => input.name == "Search")
                    .text = "Galaxy";
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(row => row.Node?.Key == "galaxy/add-sector")
                    .OnPointerDown(
                        new PointerEventData(EventSystem.current)
                        {
                            button = PointerEventData.InputButton.Left,
                        }
                    );
            }
            ContentPackEditorRecord created = galaxy.Records.Last();
            Assert.AreEqual(before + 1, galaxy.Records.Count);
            Assert.AreEqual("New sector", created.Name);
            Assert.IsEmpty(created.Planets);
            Assert.IsNotNull(view.GetComponentInChildren<PlanetSectorWindowView>());
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row => row.Node?.CreationParent?.Element == created.Element)
            );
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                before,
                project.GetCatalogs().First(catalog => catalog.IsMap).Records.Count
            );
        }

        [Test]
        public void RenderProject_CreationRowKeyboardSubmit_AddsExactlyOnePlanet()
        {
            ContentPackEditorRecord sector = project
                .GetCatalogs()
                .First(catalog => catalog.IsMap)
                .Records.First();
            int before = sector.Planets.Count;
            SelectRecord(sector.Planets.Last());
            view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search")
                .text = string.Empty;
            ContentEditorRecordRowView row =
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(row => row.Node?.Record?.Element == sector.Planets.Last().Element);
            row.OnMove(new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down });
            Assert.AreEqual(before, sector.Planets.Count);
            view.GetComponentsInChildren<ContentEditorRecordRowView>()
                .Single(row => row.Node?.CreationParent?.Element == sector.Element)
                .OnSubmit(new BaseEventData(EventSystem.current));
            Assert.AreEqual(before + 1, sector.Planets.Count);
        }

        [Test]
        public void RenderProject_GalaxyMarker_SelectsWithoutOpeningAndRequiresADoubleClickToOpen()
        {
            TMP_InputField search = view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search");
            search.text = "Galaxy";
            view.GetComponentsInChildren<ContentEditorRecordRowView>()
                .Single(row => row.Node.Key == "galaxy")
                .OnPointerDown(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    }
                );
            ContentEditorMapMarker marker = view.GetComponentsInChildren<ContentEditorMapMarker>()
                .First();
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            };
            marker.OnBeginDrag(pointer);
            Assert.IsTrue(marker.gameObject.activeInHierarchy);
            Assert.IsNull(view.GetComponentInChildren<PlanetSectorWindowView>());
            marker.OnEndDrag(pointer);
            Canvas.ForceUpdateCanvases();
            Vector3 position = marker.transform.position;
            Vector3 scale = marker.transform.lossyScale;
            pointer.clickCount = 1;
            marker.OnPointerClick(pointer);
            Canvas.ForceUpdateCanvases();
            Assert.Less(Vector3.Distance(position, marker.transform.position), 0.1f);
            Assert.Less(Vector3.Distance(scale, marker.transform.lossyScale), 0.01f);
            Assert.IsNull(view.GetComponentInChildren<PlanetSectorWindowView>());
            Assert.IsTrue(marker.gameObject.activeInHierarchy);
            string sectorKey = marker.Record.Key;
            workspace.RenderProject(project, assets);
            Assert.IsNull(view.GetComponentInChildren<PlanetSectorWindowView>());
            marker = view.GetComponentsInChildren<ContentEditorMapMarker>()
                .Single(item => item.Record.Key == sectorKey);
            pointer.dragging = true;
            pointer.clickCount = 2;
            marker.OnPointerClick(pointer);
            Assert.IsNull(view.GetComponentInChildren<PlanetSectorWindowView>());
            pointer.dragging = false;
            marker.OnPointerClick(pointer);
            Assert.IsNotNull(view.GetComponentInChildren<PlanetSectorWindowView>());
        }

        [TestCase("Officers", "Encyclopedia", 1000)]
        [TestCase("Officers", "Encyclopedia", 1800)]
        [TestCase("Officers", "Artwork", 1000)]
        [TestCase("Officers", "Artwork", 1800)]
        [TestCase("Capital ships", "Encyclopedia", 1000)]
        [TestCase("Capital ships", "Artwork", 1800)]
        public void RenderProject_MediaTabAfterGeneral_KeepsTheImageAboveEveryField(
            string category,
            string tabName,
            int width
        )
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(width, 900);
            Canvas.ForceUpdateCanvases();
            workspace.RenderProject(project, assets);
            ContentPackEditorRecord record = project
                .GetCatalogs()
                .First(catalog => catalog.Name == category)
                .Records.First();
            SelectRecord(record);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.GetComponentInChildren<TMP_Text>().text == tabName)
                .onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            RawImage image = view.GetComponentsInChildren<RawImage>()
                .Single(control => control.name == "Portrait");
            RectTransform content = (RectTransform)image.transform.parent.parent.parent;
            Bounds imageBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                content,
                image.transform
            );
            foreach (
                ContentEditorFieldView field in view.GetComponentsInChildren<ContentEditorFieldView>()
            )
            {
                Bounds fieldBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    content,
                    field.transform
                );
                Assert.LessOrEqual(fieldBounds.max.y, imageBounds.min.y - 8, field.name);
            }
            if (tabName == "Encyclopedia")
            {
                ContentPackEditorField art = ContentEditorContentSections
                    .EncyclopediaFields(record, project.GetCatalogs())
                    .First(field => field.MediaType == ContentPackEditorMediaType.Image);
                Assert.AreSame(assets.GetTexture(art.Value), image.texture);
            }
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_OfficerTraits_ShowsNamedSelectionsAndRetainsTheAuthoredSet()
        {
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            string traitID = project.GetTraitOptions().Keys.First();
            project.SetFieldValues(officer.GetField("Traits"), new[] { traitID });
            workspace.RenderProject(project, assets);
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == "Officers").Records.First()
            );
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(control => control.name == "TraitsField");
            Assert.IsFalse(
                field.GetComponentInChildren<TMP_InputField>(true).gameObject.activeSelf
            );
            UICheckboxView[] choices = field.GetComponentsInChildren<UICheckboxView>();
            Assert.AreEqual(project.GetTraitOptions().Count, choices.Length);
            Assert.AreEqual(1, choices.Count(choice => choice.IsChecked));
            CollectionAssert.AreEquivalent(
                project.GetTraitOptions().Values,
                choices.Select(choice => choice.GetComponentInChildren<TMP_Text>().text)
            );
        }

        [Test]
        public void RenderProject_LeavingXmlDraft_OffersDiscardAndPreservesDraftOnCancel()
        {
            ContentPackEditorRecord other = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.Skip(1)
                .First();
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Tab3")
                .onClick.Invoke();
            TMP_InputField xml = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "AdvancedXml");
            string draft = xml.text + "\n<!-- Pending edit -->";
            xml.text = draft;
            SelectRecord(other);
            ActionDialogView confirmation = view.GetComponentInChildren<ActionDialogView>();
            Assert.IsNotNull(confirmation);
            Assert.IsTrue(confirmation.TryCancel());
            Assert.AreEqual(draft, xml.text);
            SelectRecord(other);
            confirmation = view.GetComponentInChildren<ActionDialogView>();
            confirmation
                .GetComponentsInChildren<Button>()
                .Single(button => button.GetComponentInChildren<TMP_Text>().text == "Discard")
                .onClick.Invoke();
            Assert.AreEqual(
                other.Name,
                view.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "RecordName")
                    .text
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void AddUnit_OfficerTraitChoice_CreatesABlankTraitWithoutSelectingATemplate()
        {
            int count = project.GetCatalogs().Single(catalog => catalog.CanAddTrait).Records.Count;
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "AddUnit")
                .onClick.Invoke();
            view.GetComponentsInChildren<ContextMenuCommandView>()
                .Single(command =>
                    command
                        .GetComponentsInChildren<TMP_Text>()
                        .Any(label => label.text == "Officer trait")
                )
                .OnPointerClick(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    }
                );
            ContentPackEditorCatalog traits = project
                .GetCatalogs()
                .Single(catalog => catalog.CanAddTrait);
            Assert.AreEqual(count + 1, traits.Records.Count);
            Assert.AreEqual(
                "100",
                traits.Records.Last().GetField("PersonnelTravelTimePercent").Value
            );
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                count,
                project.GetCatalogs().Single(catalog => catalog.CanAddTrait).Records.Count
            );
        }

        [Test]
        public void RenderProject_RecruitingFactions_UsesNamedCheckboxesAndStoresTheSelectedID()
        {
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(control =>
                    control
                        .GetComponentsInChildren<TMP_Text>(true)
                        .Any(label =>
                            label.name == "Label"
                            && label.text.StartsWith("Recruitable by", StringComparison.Ordinal)
                        )
                );
            var faction = project.GetFactionOptions().First();
            UICheckboxView choice = field
                .GetComponentsInChildren<UICheckboxView>()
                .Single(control =>
                    control.GetComponentInChildren<TMP_Text>().text == faction.Value
                );
            UIComponentTestHelper.InvokeLifecycle(choice, "Awake");
            choice.GetComponent<Toggle>().isOn = true;
            Assert.IsFalse(field.GetComponentsInChildren<TMP_InputField>().Any());
            Assert.IsTrue(
                project
                    .GetCatalogs()
                    .First(catalog => catalog.Name == "Officers")
                    .Records.First()
                    .GetField("RecruitingFactionInstanceIDs")
                    .Values.Contains(faction.Key)
            );
        }

        [Test]
        public void RenderProject_DirectoryPreview_OffersInheritedFilesWithoutEditingTheRoot()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Records.Any(record => record.Kind == "FactionTheme"));
            ContentPackEditorField field = catalog
                .Records.First()
                .GetFields()
                .Single(item => item.Name.Contains("advisor / Animation image root"));
            string original = field.Value;
            ContentEditorFieldView control = view.GetComponentsInChildren<ContentEditorFieldView>()
                .First();
            control.Render(
                ContentPackEditorProjector.BuildField(project, field),
                () => { },
                _ => { },
                (_, _) => { }
            );
            MenuDropdown selector = control.GetComponentInChildren<MenuDropdown>();
            Assert.IsNotNull(selector);
            Assert.Greater(selector.options.Count, 1);
            selector.value = 1;
            Assert.AreEqual(original, field.Value);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void AddUnit_UnrelatedRecordSelected_CreatesAnUnassignedUnitAndSupportsUndo()
        {
            ContentPackEditorCatalog roster = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers");
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == "Buildings").Records.First()
            );
            int count = roster.Records.Count;
            ChooseUnitCategory(roster);
            Assert.AreEqual(count + 1, roster.Records.Count);
            Assert.AreEqual("New officer", roster.Records.Last().Name);
            Assert.AreEqual("false", roster.Records.Last().GetField("IsMain").Value);
            Assert.IsEmpty(roster.Records.Last().GetField("RecruitingFactionInstanceIDs").Values);
            StringAssert.Contains(
                "Unassigned",
                view.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "RecordInfo")
                    .text
            );
            StringAssert.Contains(
                "Unsaved changes",
                view.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "RecordInfo")
                    .text
            );
            Assert.IsEmpty(
                view.GetComponentsInChildren<TMP_InputField>(true)
                    .Single(input => input.name == "Search")
                    .text
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row => row.Node?.Record?.Element == roster.Records.Last().Element)
            );
            Assert.IsTrue(view.CommitFields());
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                count,
                project
                    .GetCatalogs()
                    .Single(catalog => catalog.Document == roster.Document)
                    .Records.Count
            );
        }

        [TestCase("Officers")]
        [TestCase("Starfighters")]
        [TestCase("Capital ships")]
        [TestCase("Troops")]
        [TestCase("Special forces")]
        [TestCase("Buildings")]
        public void AddUnit_EmptyCatalog_CreatesFreshFieldsWithoutAnExistingUnit(string category)
        {
            ContentPackEditorCatalog roster = project
                .GetCatalogs()
                .First(catalog => catalog.Name == category);
            string root = System.Xml.Linq.XDocument.Parse(roster.Document.Text).Root.Name.LocalName;
            project.SetDocumentText(roster.Document, "<" + root + " />");
            workspace.RenderProject(project, assets);
            roster = project.GetCatalogs().Single(catalog => catalog.Document == roster.Document);

            ChooseUnitCategory(roster);

            ContentPackEditorRecord unit = roster.Records.Single();
            Assert.IsEmpty(unit.GetField("DisplayImagePath").Value);
            Assert.IsEmpty(unit.GetField("SmallDisplayImagePath").Value);
            Assert.IsEmpty(unit.GetField("EncyclopediaImagePath").Value);
            Assert.IsEmpty(unit.GetField("Description").Value);
            Assert.IsTrue(view.GetComponentsInChildren<ContentEditorFieldView>().Any());
            Assert.IsTrue(view.CommitFields());
        }

        [Test]
        public void AddUnit_BeforeChoosingACategory_LeavesTheProjectUnchanged()
        {
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "AddUnit")
                .onClick.Invoke();

            Assert.IsFalse(project.IsDirty);
            Assert.IsTrue(view.GetComponentsInChildren<ContextMenuCommandView>().Any());
        }

        [Test]
        public void AddUnit_Menu_UsesSingularNamesForEveryCreationAction()
        {
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "AddUnit")
                .onClick.Invoke();
            ContextMenuCommandView[] commands =
                view.GetComponentsInChildren<ContextMenuCommandView>();
            string[] expected =
            {
                "Faction",
                "Planet sector",
                "Officer",
                "Special forces",
                "Regiment",
                "Capital ship",
                "Starfighter",
                "Officer trait",
                "Building",
                "Game event",
            };
            Assert.AreEqual(expected.Length, commands.Length);
            foreach (string caption in expected)
                Assert.AreEqual(
                    1,
                    commands.Count(command =>
                        command
                            .GetComponentsInChildren<TMP_Text>()
                            .Any(label => label.text == caption)
                    ),
                    caption
                );
            CollectionAssert.AreEqual(
                expected,
                commands.Select(command =>
                    command
                        .GetComponentsInChildren<TMP_Text>()
                        .First(label => expected.Contains(label.text))
                        .text
                )
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_FieldGuidance_ShowsInfoIconsOnlyForDocumentedCaptions()
        {
            ContentEditorFieldView[] fields =
                view.GetComponentsInChildren<ContentEditorFieldView>();
            int visible = 0;
            foreach (ContentEditorFieldView field in fields)
            {
                HelpTooltipView info = field
                    .GetComponentsInChildren<HelpTooltipView>(true)
                    .Single(help =>
                        help.transform.parent == field.transform && help.name == "Info"
                    );
                TMP_Text label = field
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Single(text =>
                        text.name == "Label" && text.transform.parent == field.transform
                    );
                bool expected =
                    label.gameObject.activeSelf && !string.IsNullOrWhiteSpace(field.Field.HelpText);
                Assert.AreEqual(expected, info.gameObject.activeSelf, field.Field.MemberName);
                Assert.IsNull(info.GetComponent<TMP_Text>());
                Assert.IsNotNull(
                    info.GetComponentsInChildren<Image>().Single(image => image.sprite != null)
                );
                Assert.IsNull(field.GetComponent<HelpTooltipView>());
                if (!expected)
                    continue;
                visible++;
                RectTransform root = (RectTransform)field.transform;
                Bounds icon = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    root,
                    (RectTransform)info.transform
                );
                TMP_InputField value = field.GetComponentInChildren<TMP_InputField>();
                if (value != null)
                {
                    Bounds input = RectTransformUtility.CalculateRelativeRectTransformBounds(
                        root,
                        (RectTransform)value.transform
                    );
                    Assert.LessOrEqual(icon.max.x, input.min.x);
                }
            }
            Assert.Greater(visible, 0);
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase("Officers", "+ Add officer", "RecruitingFactionInstanceIDs", 0)]
        [TestCase("Officers", "+ Add officer", "RecruitingFactionInstanceIDs", 1)]
        [TestCase("Capital ships", "+ Add capital ship", "ManufacturingFactionInstanceIDs", 0)]
        [TestCase("Capital ships", "+ Add capital ship", "ManufacturingFactionInstanceIDs", 1)]
        [TestCase("Starfighters", "+ Add starfighter", "ManufacturingFactionInstanceIDs", 0)]
        [TestCase("Starfighters", "+ Add starfighter", "ManufacturingFactionInstanceIDs", 1)]
        [TestCase("Troops", "+ Add regiment", "ManufacturingFactionInstanceIDs", 0)]
        [TestCase("Troops", "+ Add regiment", "ManufacturingFactionInstanceIDs", 1)]
        [TestCase("Special forces", "+ Add special forces", "ManufacturingFactionInstanceIDs", 0)]
        [TestCase("Special forces", "+ Add special forces", "ManufacturingFactionInstanceIDs", 1)]
        [TestCase("Buildings", "+ Add building", "ManufacturingFactionInstanceIDs", 0)]
        [TestCase("Buildings", "+ Add building", "ManufacturingFactionInstanceIDs", 1)]
        public void AddUnit_FactionGroupAction_AssignsThatFactionAndSupportsUndo(
            string category,
            string caption,
            string member,
            int factionIndex
        )
        {
            string factionInstanceID = project
                .GetFactionOptions()
                .Keys.OrderBy(id => id, StringComparer.Ordinal)
                .ElementAt(factionIndex);
            view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search")
                .text = category;
            ContentEditorRecordRowView add =
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(row =>
                        row.Node?.CreationCatalog?.Name == category
                        && row.Node.CreationFactionInstanceID == factionInstanceID
                    );
            ContentPackEditorCatalog catalog = add.Node.CreationCatalog;
            int before = catalog.Records.Count;
            ContentEditorTreeNode group = view.GetComponentsInChildren<ContentEditorRecordRowView>()
                .Single(row => row.Node.Children.Contains(add.Node))
                .Node;
            Assert.AreEqual(caption, add.Node.Caption);
            Assert.AreSame(add.Node, group.Children.Last());
            Assert.AreEqual(group.Depth + 1, add.Node.Depth);
            Assert.AreEqual(project.GetFactionOptions()[factionInstanceID], group.Caption);
            add.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }
            );

            Assert.AreEqual(before + 1, catalog.Records.Count);
            ContentPackEditorRecord created = catalog.Records.Last();
            CollectionAssert.AreEqual(new[] { factionInstanceID }, created.GetField(member).Values);
            Assert.IsTrue(string.IsNullOrEmpty(created.Element.Element("OwnerInstanceID")?.Value));
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row => row.Node?.Record?.Element == created.Element)
            );
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                before,
                project
                    .GetCatalogs()
                    .Single(item => item.Document == catalog.Document)
                    .Records.Count
            );
            Assert.IsTrue(project.Redo());
            CollectionAssert.AreEqual(
                new[] { factionInstanceID },
                project
                    .GetCatalogs()
                    .Single(item => item.Document == catalog.Document)
                    .Records.Last()
                    .GetField(member)
                    .Values
            );
        }

        [TestCase("Officers", "RecruitingFactionInstanceIDs")]
        [TestCase("Starfighters", "ManufacturingFactionInstanceIDs")]
        [TestCase("Capital ships", "ManufacturingFactionInstanceIDs")]
        [TestCase("Troops", "ManufacturingFactionInstanceIDs")]
        [TestCase("Special forces", "ManufacturingFactionInstanceIDs")]
        [TestCase("Buildings", "ManufacturingFactionInstanceIDs")]
        public void AddUnit_NewDefinition_LetsTheAuthorChooseEitherOrBothFactions(
            string category,
            string member
        )
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == category);
            ChooseUnitCategory(catalog);
            ContentPackEditorRecord unit = catalog.Records.Last();
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(control => control.Field.MemberName == member);
            UICheckboxView[] choices = field.GetComponentsInChildren<UICheckboxView>();
            Assert.AreEqual(project.GetFactionOptions().Count, choices.Length);
            Assert.IsTrue(choices.All(choice => !choice.GetComponent<Toggle>().isOn));
            Assert.IsEmpty(unit.GetField(member).Values);
            var other = project.GetFactionOptions().Last();
            UICheckboxView selected = choices.Single(choice =>
                choice.GetComponentInChildren<TMP_Text>().text == other.Value
            );
            UIComponentTestHelper.InvokeLifecycle(selected, "Awake");
            selected.GetComponent<Toggle>().isOn = true;
            CollectionAssert.AreEqual(new[] { other.Key }, unit.GetField(member).Values);
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row => row.Node.Record == unit)
            );
            foreach (UICheckboxView choice in choices.Where(choice => choice != selected))
            {
                UIComponentTestHelper.InvokeLifecycle(choice, "Awake");
                choice.GetComponent<Toggle>().isOn = true;
            }
            CollectionAssert.AreEquivalent(
                project.GetFactionOptions().Keys,
                unit.GetField(member).Values
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row => row.Node.Record == unit)
            );
            Assert.IsTrue(view.CommitFields());
        }

        [Test]
        public void ConfirmDiscard_InvalidField_AllowsDiscardWithoutCommittingIt()
        {
            ContentPackEditorCatalog roster = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers");
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(control => control.name == "TypeIDField");
            TMP_InputField input = field.GetComponentInChildren<TMP_InputField>();
            input.text = roster.Records[1].ID;
            input.onEndEdit.Invoke(input.text);
            Assert.IsFalse(view.CommitFields());
            Assert.IsTrue(
                field
                    .GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text == "ID already in use.")
            );
            bool discarded = false;
            view.ConfirmDiscard(() => discarded = true);
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Primary")
                .onClick.Invoke();
            Assert.IsTrue(discarded);
        }

        [Test]
        public void RenderProject_AudioPreview_HasAnEnabledListenerAndTwoDimensionalSource()
        {
            Assert.AreEqual(1, view.GetComponentsInChildren<AudioListener>().Length);
            Assert.IsTrue(view.GetComponent<AudioListener>().enabled);
            Assert.AreEqual(0, view.GetComponent<AudioSource>().spatialBlend);
        }

        [Test]
        public void RenderProject_ChangingAnEnum_UsesTheSharedSelectorAndUpdatesTheDocument()
        {
            var candidate = project
                .GetCatalogs()
                .Where(catalog => catalog.IsMap)
                .SelectMany(catalog => catalog.Records)
                .Select(record => new
                {
                    Record = record,
                    Field = record
                        .GetFields()
                        .FirstOrDefault(field =>
                            !field.IsList
                            && !field.IsMedia
                            && field.Choices.Count > 1
                            && field.Choices.Contains(field.Value)
                        ),
                })
                .First(item => item.Field != null);
            SelectRecord(candidate.Record);
            ContentEditorFieldView fieldView =
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .First(field =>
                        field
                            .GetComponentsInChildren<TMP_Text>()
                            .Any(label =>
                                label.name == "Label" && label.text == candidate.Field.Caption
                            )
                    );
            TMP_Dropdown selector = fieldView.GetComponentInChildren<MenuDropdown>();
            Assert.IsNotNull(selector);
            string next = candidate.Field.Choices.First(choice => choice != candidate.Field.Value);
            selector.value = Array.IndexOf(candidate.Field.Choices.ToArray(), next);
            Assert.AreEqual(next, candidate.Field.Value);
            Assert.IsTrue(project.IsDirty);
        }

        [Test]
        public void ShowNewPackDialog_SelectingABasePack_UsesTheSelectedSourceWhenCreating()
        {
            view.Initialize(
                new[]
                {
                    new ContentPackDefinition { ID = "first", DisplayName = "First" },
                    new ContentPackDefinition { ID = "second", DisplayName = "Second" },
                },
                project.ContentRootPath
            );
            view.ShowNewPackDialog();
            view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "DisplayName")
                .text = "New pack";
            view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "PackID")
                .text = "new-pack";
            MenuDropdown selector = view.GetComponentsInChildren<MenuDropdown>()
                .Single(item => item.name == "PackChoice");
            selector.value = 1;
            string chosen = null;
            view.CreateRequested += (source, _, _, _, _) => chosen = source;
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Confirm")
                .onClick.Invoke();
            Assert.AreEqual("second", chosen);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_NewlyOpenedPack_ShowsInheritedOfficersImmediately()
        {
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            Assert.IsTrue(view.GetComponentsInChildren<ContentEditorFieldView>().Any());
            Assert.IsTrue(
                view.GetComponentsInChildren<TMP_Text>().Any(label => label.text == officer.Name)
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Any(row => row.Node.Record == officer)
            );
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ToggleBranch_ScrolledTree_PreservesScrollAndInspector(bool disclosure)
        {
            ContentEditorRecordRowView branch =
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(row => row.Node?.Caption == "Special forces");
            ScrollAreaView scroll = view.GetComponentsInChildren<ScrollAreaLayout>()
                .Single(layout => layout.name == "ContentTree")
                .ScrollArea;
            string selectedTitle = view.GetComponentsInChildren<TMP_Text>()
                .Single(label => label.name == "RecordName")
                .text;
            scroll.RevealContentRect(branch.Index * 36, scroll.ViewportHeight);
            float offset = scroll.ContentRoot.anchoredPosition.y;
            Assert.Greater(offset, 0);

            for (int toggle = 0; toggle < 2; toggle++)
            {
                if (disclosure)
                    branch.GetComponentInChildren<Button>().onClick.Invoke();
                else
                    branch.OnPointerDown(
                        new PointerEventData(EventSystem.current)
                        {
                            button = PointerEventData.InputButton.Left,
                        }
                    );
                Canvas.ForceUpdateCanvases();

                Assert.That(scroll.ContentRoot.anchoredPosition.y, Is.EqualTo(offset).Within(0.1f));
                Assert.AreEqual(
                    selectedTitle,
                    view.GetComponentsInChildren<TMP_Text>()
                        .Single(label => label.name == "RecordName")
                        .text
                );
            }
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void ShowNewPackDialog_WindowsLocation_DisplaysTheLiteralPath()
        {
            view.ShowNewPackDialog();
            TMP_InputField location = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "Location");
            location.text = @"C:\new\rebellion2\Mods";
            Canvas.ForceUpdateCanvases();
            location.textComponent.ForceMeshUpdate();
            Assert.AreEqual(
                location.text,
                location.textComponent.GetParsedText().TrimEnd('\u200B')
            );
        }

        [Test]
        public void ShowNewPackDialog_EnteringAName_GeneratesThePackFolderUnderMods()
        {
            view.ShowNewPackDialog();
            TMP_InputField name = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "DisplayName");
            name.text = "My New Pack!";
            TMP_InputField id = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "PackID");
            TMP_InputField location = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "Location");
            Assert.AreEqual("my-new-pack", id.text);
            Assert.AreEqual(
                Path.Combine(Directory.GetParent(project.ContentRootPath).FullName, "Mods"),
                location.text
            );
            string destination = null;
            view.CreateRequested += (_, _, _, _, folderPath) => destination = folderPath;
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Confirm")
                .onClick.Invoke();
            Assert.AreEqual(location.text, destination);
            StringAssert.Contains(
                Path.Combine(location.text, "my-new-pack"),
                view.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "PackFolder")
                    .text
            );
            id.text = "custom-id";
            name.text = "Different display name";
            Assert.AreEqual("custom-id", id.text);
        }

        [Test]
        public void ShowNewPackDialog_RequiredFieldsEmpty_DisablesCreation()
        {
            view.ShowNewPackDialog();
            Button confirm = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Confirm");
            Assert.IsFalse(confirm.interactable);
        }

        [Test]
        public void RenderProject_EditingASkillAndUndoing_RestoresTheVisibleValue()
        {
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            SelectRecord(officer);
            TMP_InputField input = SkillInput();
            string previous = input.text;
            string replacement = previous == "123" ? "124" : "123";
            input.SetTextWithoutNotify(replacement);
            input.onEndEdit.Invoke(replacement);

            Assert.IsTrue(project.IsDirty);
            Assert.IsTrue(project.Undo());
            workspace.RenderProject(project, assets);

            Assert.AreEqual(previous, SkillInput().text);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void ShowNewPackDialog_OpeningTheWorkflow_LeavesTheWorkingPackUnchanged()
        {
            view.ShowNewPackDialog();
            Assert.IsTrue(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text == "New content pack")
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void ClearProject_ReturnsToStartScreen_OffersOnlyExplicitNewAndOpen()
        {
            view.ClearProject();
            Assert.IsTrue(
                view.GetComponentsInChildren<Button>().Any(button => button.name == "NewPack")
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<Button>().Any(button => button.name == "OpenPack")
            );
            Assert.IsFalse(view.GetComponentsInChildren<ContentEditorFieldView>().Any());
            Assert.IsTrue(
                view.GetComponentsInChildren<Button>().Any(button => button.name == "View")
            );
        }

        [Test]
        public void RenderProject_DifferentImageShapes_KeepTheirAuthoredAspectRatios()
        {
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == "Officers").Records.First()
            );
            Canvas.ForceUpdateCanvases();
            int imageCount = 0;
            foreach (
                ContentEditorRecordRowView row in view.GetComponentsInChildren<ContentEditorRecordRowView>()
            )
            {
                RawImage image = row.GetComponentsInChildren<RawImage>()
                    .FirstOrDefault(item => item.name == "Thumbnail");
                if (image?.texture == null)
                    continue;
                imageCount++;
                float expected = (float)image.texture.width / image.texture.height;
                Assert.That(
                    image.GetComponent<AspectRatioFitter>().aspectRatio,
                    Is.EqualTo(expected).Within(0.001),
                    row.Node.Caption
                );
                Assert.AreEqual(
                    AspectRatioFitter.AspectMode.FitInParent,
                    image.GetComponent<AspectRatioFitter>().aspectMode
                );
            }
            Assert.Greater(imageCount, 0);
        }

        [Test]
        public void RenderProject_ClickingDisclosure_ExpandsBranchWithoutReplacingSelection()
        {
            view.ClearProject();
            workspace.RenderProject(project, assets);
            ContentEditorRecordRowView units =
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(row => row.Node?.Caption == "Units");
            Button disclosure = units.GetComponentsInChildren<Button>(true).Single();
            int before = view.GetComponentsInChildren<ContentEditorRecordRowView>().Length;
            disclosure.onClick.Invoke();
            int after = view.GetComponentsInChildren<ContentEditorRecordRowView>().Length;
            Assert.AreNotEqual(before, after);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_BooleanAndTextControls_AlignToTheSameRowCenter()
        {
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == "Officers").Records.First()
            );
            ContentEditorFieldView boolean = view.GetComponentsInChildren<ContentEditorFieldView>()
                .First(field => field.GetComponentInChildren<UICheckboxView>() != null);
            RectTransform label = boolean
                .GetComponentsInChildren<TMP_Text>()
                .Single(item => item.name == "Label")
                .rectTransform;
            RectTransform frame = boolean
                .GetComponentsInChildren<RawImage>()
                .Single(item => item.name == "Frame")
                .rectTransform;
            Assert.That(
                frame.TransformPoint(frame.rect.center).y,
                Is.EqualTo(label.TransformPoint(label.rect.center).y).Within(0.5)
            );
        }

        [Test]
        public void RenderProject_TraitsAndSharedRules_GroupsControlsByTheirActualScope()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            string[] roots = navigation
                .GetRows(string.Empty)
                .Where(node => node.Depth == 0)
                .Select(node => node.Caption)
                .ToArray();
            Assert.Less(
                Array.IndexOf(roots, "Officer traits"),
                Array.IndexOf(roots, "Starting setup")
            );
            Assert.AreEqual("Officer traits", roots[Array.IndexOf(roots, "Units") + 1]);
            Assert.AreEqual("Starting setup", roots[roots.Length - 2]);
            navigation.SetExpanded(true);
            ContentEditorTreeNode[] pages = navigation.GetRows(string.Empty).ToArray();
            ContentEditorTreeNode difficulty = pages.Single(node => node.Caption == "Difficulty");
            Assert.IsTrue(
                difficulty.Fields.Any(field => field.MemberName == "MissionSuccessChancePoints")
            );
            Assert.IsFalse(
                pages
                    .Where(node => node.MissionID != null)
                    .SelectMany(node => node.Fields)
                    .Any(field => field.Group == "DifficultyModifiers")
            );
            Assert.IsNotNull(
                navigation
                    .GetRows("Mission success chance points")
                    .SingleOrDefault(node => node.Key == difficulty.Key)
            );
        }

        [TestCase(900)]
        [TestCase(1600)]
        public void RenderProject_NestedGeneration_ShowsSeparateSectionsAndCompleteFieldCaptions(
            int width
        )
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(width, 900);
            Canvas.ForceUpdateCanvases();
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "GameGenerationConfig");

            SelectRecord(theme);
            Canvas.ForceUpdateCanvases();

            string[] headings = view.GetComponentsInChildren<TMP_Text>()
                .Where(label => label.name.StartsWith("GroupTemplate", StringComparison.Ordinal))
                .Select(label => label.text)
                .ToArray();
            Assert.IsNotEmpty(view.GetComponentsInChildren<CollectionEditorView>());
            Assert.IsFalse(headings.Any(heading => heading.Contains(" / ")));
            foreach (
                ContentEditorFieldView control in view.GetComponentsInChildren<ContentEditorFieldView>()
            )
            {
                TMP_Text label = control
                    .GetComponentsInChildren<TMP_Text>()
                    .SingleOrDefault(text =>
                        text.name == "Label" && text.transform.parent == control.transform
                    );
                if (label == null)
                {
                    Assert.IsTrue(control.Field.IsNumber);
                    Assert.IsNotNull(control.GetComponentInChildren<TMP_InputField>());
                    continue;
                }
                Assert.IsFalse(label.text.Contains(" / "), label.text);
                Assert.LessOrEqual(
                    label
                        .GetPreferredValues(
                            label.text,
                            label.rectTransform.rect.width,
                            float.PositiveInfinity
                        )
                        .y,
                    label.rectTransform.rect.height + 0.1f,
                    label.text
                );
            }
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RenderProject_InvalidAuthoredBoolean_AllowsRepairThroughTheTextInput()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == "Officers");
            System.Xml.Linq.XDocument xml = System.Xml.Linq.XDocument.Parse(catalog.Document.Text);
            xml.Root.Elements().First().SetElementValue("CanBetray", "not-a-bool");
            project.SetDocumentText(catalog.Document, xml.ToString());

            Assert.DoesNotThrow(() => workspace.RenderProject(project, assets));
            SelectRecord(
                project.GetCatalogs().First(item => item.Name == "Officers").Records.First()
            );
            TMP_InputField input = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.name == "CanbetrayField")
                .GetComponentInChildren<TMP_InputField>();
            Assert.AreEqual("not-a-bool", input.text);
            input.SetTextWithoutNotify("true");
            input.onEndEdit.Invoke("true");

            Assert.IsTrue(view.CommitFields());
            StringAssert.Contains("<CanBetray>true</CanBetray>", catalog.Document.Text);
        }

        [Test]
        public void RenderProject_InvalidArtworkAddress_LeavesTheReferenceEditable()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == "Officers");
            System.Xml.Linq.XDocument xml = System.Xml.Linq.XDocument.Parse(catalog.Document.Text);
            xml.Root.Elements()
                .First()
                .SetElementValue("EncyclopediaImagePath", "unscoped/image.png");
            project.SetDocumentText(catalog.Document, xml.ToString());

            Assert.DoesNotThrow(() => workspace.RenderProject(project, assets));
            SelectRecord(
                project.GetCatalogs().First(item => item.Name == "Officers").Records.First()
            );
            view.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Single(button => button.name == "EncyclopediaTab")
                .onClick.Invoke();
            TMP_InputField input = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.name == "EncyclopediaimagepathField")
                .GetComponentInChildren<TMP_InputField>();

            Assert.AreEqual("unscoped/image.png", input.text);
        }

        [Test]
        public void RenderProject_ApplyingUnchangedXml_KeepsTheEditorOpenWithoutCreatingAnOverride()
        {
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == "Officers").Records.First()
            );
            view.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Single(button => button.name == "Tab3")
                .onClick.Invoke();
            view.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Single(button => button.name == "ApplyXml")
                .onClick.Invoke();

            Assert.IsTrue(
                view.GetComponentsInChildren<UnityEngine.UI.Button>()
                    .Any(button => button.name is "ApplyXml" or "DiscardXml")
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void ClearProject_FocusedFieldEndEdit_CannotWriteToTheClosedProject()
        {
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .First(item => item.Field.MemberName == "DisplayName");
            TMP_InputField input = field.GetComponentInChildren<TMP_InputField>();
            string before = field.Field.Document.Text;
            ContentPackEditorDocument document = field.Field.Document;
            view.ClearProject();
            input.onEndEdit.Invoke("Late edit");
            Assert.IsNull(field.Field);
            Assert.AreEqual(before, document.Text);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void ClearProject_GalaxyDragInProgress_CompletesOneUndoableEdit()
        {
            TMP_InputField search = view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search");
            search.text = "Galaxy";
            view.GetComponentsInChildren<ContentEditorRecordRowView>()
                .Single(row => row.Node.Key == "galaxy")
                .OnPointerDown(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    }
                );
            Canvas.ForceUpdateCanvases();
            ContentEditorMapMarker marker = view.GetComponentsInChildren<ContentEditorMapMarker>()
                .First();
            ContentPackEditorDocument document = marker.Record.Catalog.Document;
            string before = document.Text;
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, marker.transform.position),
            };
            marker.OnBeginDrag(pointer);
            pointer.position += new Vector2(20, 10);
            marker.OnDrag(pointer);
            Assert.AreNotEqual(before, document.Text);
            view.ClearProject();
            Assert.IsTrue(project.CanUndo);
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(before, document.Text);
            Assert.IsFalse(project.CanUndo);
        }

        [Test]
        public void HandleKeyboardShortcut_EscapeWithNewPackDialog_ClosesOnlyTheDialog()
        {
            view.RenderStatus("Keep this status.", false);
            view.ShowNewPackDialog();
            Event key = new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape };
            Assert.IsTrue(view.HandleKeyboardShortcut(key));
            Assert.AreEqual(EventType.Used, key.type);
            Assert.IsFalse(view.GetComponentInChildren<ContentEditorPackDialogView>(true).IsOpen);
            Assert.IsTrue(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text == "Keep this status.")
            );
        }

        [Test]
        public void RenderProject_InvalidEnum_OffersTheTypedSelectorAndKeepsTheDraftUnchanged()
        {
            ContentPackEditorRecord building = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Buildings")
                .Records.First();
            XDocument xml = XDocument.Parse(building.Catalog.Document.Text);
            xml.Root.Elements().First().SetElementValue("BuildingType", "InvalidKind");
            project.SetDocumentText(building.Catalog.Document, xml.ToString());
            workspace.RenderProject(project, assets);
            building = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Buildings")
                .Records.First();
            SelectRecord(building);
            ContentEditorFieldView row = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.Field.MemberName == "BuildingType");
            TMP_Dropdown selector = row.GetComponentInChildren<TMP_Dropdown>();
            Assert.IsNotNull(selector);
            Assert.Greater(selector.options.Count, 1);
            row.ValidateCurrent();
            Assert.IsTrue(row.HasError);
            Assert.AreEqual("InvalidKind", building.GetField("BuildingType").Value);
        }

        [Test]
        public void RenderProject_DeleteSelectedOfficer_RebindsTheNextRecordAndSupportsUndo()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == "Officers");
            string removed = catalog.Records.First().ID;
            SelectRecord(catalog.Records.First());
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Delete")
                .onClick.Invoke();
            ActionDialogView confirmation = view.GetComponentInChildren<ActionDialogView>();
            confirmation
                .GetComponentsInChildren<Button>()
                .Single(button => button.GetComponentInChildren<TMP_Text>()?.text == "Delete")
                .onClick.Invoke();
            Assert.IsFalse(catalog.Records.Any(record => record.ID == removed));
            Assert.IsTrue(view.CommitFields());
            Assert.IsTrue(view.GetComponentsInChildren<ContentEditorFieldView>().Any());
            Assert.IsTrue(project.Undo());
            workspace.RenderProject(project, assets);
            Assert.IsTrue(
                project
                    .GetCatalogs()
                    .First(item => item.Document == catalog.Document)
                    .Records.Any(record => record.ID == removed)
            );
        }

        [Test]
        public void RenderProject_GalaxyOverview_ShowsOnlyTheMapEditingTab()
        {
            view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search")
                .text = "Galaxy";
            view.GetComponentsInChildren<ContentEditorRecordRowView>()
                .Single(row => row.Node.Key == "galaxy")
                .OnPointerDown(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    }
                );
            CollectionAssert.AreEqual(
                new[] { "General" },
                view.GetComponentsInChildren<Button>()
                    .Where(button => button.name.StartsWith("Tab", StringComparison.Ordinal))
                    .Select(button => button.GetComponentInChildren<TMP_Text>().text)
            );
        }

        [Test]
        public void RenderProject_ReferenceOnlyMission_OmitsTheEmptyRulesTab()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == "Encyclopedia entries");
            XDocument xml = XDocument.Parse(catalog.Document.Text);
            xml.Root.Add(
                new XElement(
                    "EncyclopediaEntry",
                    new XElement("TypeID", "reference-only-mission"),
                    new XElement("DisplayName", "Reference only mission"),
                    new XElement("Category", "Mission"),
                    new XElement("Description", "Reference text.")
                )
            );
            project.SetDocumentText(catalog.Document, xml.ToString());
            workspace.RenderProject(project, assets);
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("missions/reference-only-mission"));
            Assert.IsFalse(
                view.GetComponentsInChildren<Button>()
                    .Any(button => button.GetComponentInChildren<TMP_Text>()?.text == "Rules")
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Any(field => field.Field.MemberName == "Description")
            );
        }
    }
}
