using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Schema;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Tests.ContentEditor.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.Integration
{
    [TestFixture]
    [Category("Integration")]
    [Category("RequiresMedia")]
    public sealed class ContentPackEditorViewTests : ContentEditorViewTestBase
    {
        [Test]
        public void DiscardChanges_GalaxyDragInProgress_RestoresSavedCoordinatesBeforeClosing()
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
            string saved = document.Text;
            PointerEventData pointer = new(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, marker.transform.position),
            };
            marker.OnBeginDrag(pointer);
            pointer.position += new Vector2(20, 10);
            marker.OnDrag(pointer);
            Assert.AreNotEqual(saved, document.Text);

            workspace.DiscardChanges();

            Assert.AreEqual(saved, document.Text);
            Assert.IsFalse(project.CanUndo);
            Assert.IsFalse(project.IsDirty);
            workspace.ClearProject();
            Assert.IsNull(workspace.Project);
        }

        [Test]
        public void ClearProject_ArtworkUndoHistory_ReleasesSnapshotsAndPreservesArtwork()
        {
            string image = AddArtworkHistory();
            ContentPackEditorStorage.Save(project);
            string history = Path.Combine(project.PackRootPath, ".rebellion2-editor", "History");
            byte[] expected = File.ReadAllBytes(image);
            Assert.IsNotEmpty(Directory.GetDirectories(history));

            workspace.ClearProject();

            Assert.IsNull(workspace.Project);
            Assert.IsEmpty(Directory.GetDirectories(history));
            CollectionAssert.AreEqual(expected, File.ReadAllBytes(image));
        }

        [Test]
        public void Dispose_ArtworkUndoHistory_ReleasesSnapshotsAndPreservesArtwork()
        {
            string image = AddArtworkHistory();
            ContentPackEditorStorage.Save(project);
            string history = Path.Combine(project.PackRootPath, ".rebellion2-editor", "History");
            byte[] expected = File.ReadAllBytes(image);

            workspace.Dispose();

            Assert.IsEmpty(Directory.GetDirectories(history));
            CollectionAssert.AreEqual(expected, File.ReadAllBytes(image));
        }

        [Test]
        public void RenderProject_SameProject_PreservesArtworkUndoAndSnapshots()
        {
            string image = AddArtworkHistory();
            string history = Path.Combine(project.PackRootPath, ".rebellion2-editor", "History");
            string[] before = Directory.GetDirectories(history);
            byte[] latest = File.ReadAllBytes(image);

            workspace.RenderProject(project, assets);

            CollectionAssert.AreEquivalent(before, Directory.GetDirectories(history));
            Assert.IsTrue(project.Undo());
            CollectionAssert.AreNotEqual(latest, File.ReadAllBytes(image));
            Assert.IsTrue(project.Redo());
            CollectionAssert.AreEqual(latest, File.ReadAllBytes(image));
        }

        [Test]
        public void RenderProject_DifferentProject_ReleasesPreviousArtworkSnapshots()
        {
            string image = AddArtworkHistory();
            ContentPackEditorStorage.Save(project);
            string history = Path.Combine(project.PackRootPath, ".rebellion2-editor", "History");
            ContentPackEditorProject next = CreateProject(Path.Combine(folder, "next"));

            workspace.RenderProject(next, assets);

            Assert.AreSame(next, workspace.Project);
            Assert.IsEmpty(Directory.GetDirectories(history));
            Assert.IsTrue(File.Exists(image));
        }

        [Test]
        public void RenderProject_RejectedProject_PreservesPreviousArtworkUndo()
        {
            string image = AddArtworkHistory();
            string history = Path.Combine(project.PackRootPath, ".rebellion2-editor", "History");
            using ContentPackEditorProject rejected = ContentPackEditorStorage.Open(
                TestContent.Pack.ContentRootPath,
                TestContent.Pack.Definition.ID
            );
            Assert.IsTrue(rejected.IsInstalled);
            byte[] latest = File.ReadAllBytes(image);

            Assert.Throws<InvalidOperationException>(() =>
                workspace.RenderProject(rejected, assets)
            );

            Assert.AreSame(project, workspace.Project);
            Assert.IsNotEmpty(Directory.GetDirectories(history));
            Assert.IsTrue(project.Undo());
            CollectionAssert.AreNotEqual(latest, File.ReadAllBytes(image));
        }

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
        public void RenderProject_NewFactionMissionPresentation_ExposesBothUnsetIconSlots()
        {
            ContentPackEditorRecord faction = project.AddFaction();
            workspace.RenderProject(project, assets);
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find(
                "mission-presentation/" + faction.Catalog.Faction + "/Espionage"
            );
            SelectFactionMission(navigation, page);

            ContentEditorFieldView[] fields =
                view.GetComponentsInChildren<ContentEditorFieldView>();
            CollectionAssert.AreEquivalent(
                new[] { "LargeImagePath", "SmallImagePath" },
                fields.Select(field => field.Field.MemberName)
            );
            Assert.IsTrue(
                fields.All(field =>
                    field
                        .GetComponentsInChildren<Button>()
                        .Any(button =>
                            button.GetComponentInChildren<TMP_Text>()?.text == "Choose image..."
                        )
                )
            );
            Assert.IsNull(page.Record.Element.Element(nameof(FactionTheme.MissionIcons)));
        }

        [Test]
        public void RenderProject_NewFactionIconEdits_SaveBothSizesOnOneRuntimeKey()
        {
            ContentPackEditorRecord faction = project.AddFaction();
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find(
                "mission-presentation/" + faction.Catalog.Faction + "/Espionage"
            );
            string source = Path.Combine(folder, "mission-art.png");
            Texture2D image = new(2, 2);
            try
            {
                File.WriteAllBytes(source, image.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
            ContentPackEditorField large = page.Fields.Single(field =>
                field.MemberName == "LargeImagePath"
            );
            ContentPackEditorField small = page.Fields.Single(field =>
                field.MemberName == "SmallImagePath"
            );
            project.SetField(large, ContentPackEditorStorage.ImportAsset(project, source, large));
            project.SetField(small, ContentPackEditorStorage.ImportAsset(project, source, small));
            ContentPackEditorStorage.Save(project);

            XDocument saved = XDocument.Load(page.Record.Catalog.Document.TargetPath);
            XElement icon = saved.Descendants(nameof(MissionIconTheme)).Single();
            Assert.AreEqual(MissionIconKeys.Espionage, (string)icon.Element("Key"));
            string expected =
                "Pack/Factions/" + faction.Catalog.Faction + "/Assets/mission-art.png";
            Assert.AreEqual(expected, (string)icon.Element("LargeImagePath"));
            Assert.AreEqual(expected, (string)icon.Element("SmallImagePath"));
            Assert.IsTrue(File.Exists(Path.Combine(project.PackRootPath, expected[5..])));
        }

        [Test]
        public void RenderProject_NewFactionMissionArticle_AddsAnEditableFactionScopedEntry()
        {
            ContentPackEditorRecord faction = project.AddFaction();
            workspace.RenderProject(project, assets);
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectFactionMission(
                navigation,
                navigation.Find("mission-presentation/" + faction.Catalog.Faction + "/Espionage")
            );

            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "AddMissionArticle")
                .onClick.Invoke();

            ContentPackEditorField description =
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(field => field.Field.MemberName == "Description")
                    .Field;
            Assert.AreEqual("Espionage", description.Record.ID);
            Assert.AreEqual(
                faction.ID,
                (string)description.Record.Element.Element("VisibleFactionInstanceID")
            );
        }

        [Test]
        public void RenderProject_EspionageReportText_CustomizesOneOutcomeForTheNewFaction()
        {
            ContentPackEditorDocument definitions = project.Documents.Single(document =>
                document.Address == "Pack/" + project.Definition.MessageDefinitionsPath
            );
            XDocument authored = XDocument.Parse(definitions.Text);
            foreach (string outcome in new[] { "Success", "Failed" })
                authored.Root.Add(
                    new XElement(
                        "MessageDefinition",
                        new XElement("DisplayName", "Shared Espionage " + outcome),
                        new XElement("ResultType", "MissionReport"),
                        new XElement("Outcome", outcome),
                        new XElement("MissionTypeID", "Espionage"),
                        new XElement("MessageType", "Mission"),
                        new XElement("Subject", "Report for {system}"),
                        new XElement("Body", "Shared " + outcome + " {details}"),
                        new XElement("BackgroundImage", new XAttribute("Key", "mission_report"))
                    )
                );
            project.SetDocumentText(definitions, authored.ToString());
            ContentPackEditorRecord faction = project.AddFaction();
            workspace.RenderProject(project, assets);
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectFactionMission(
                navigation,
                navigation.Find("mission-presentation/" + faction.Catalog.Faction + "/Espionage")
            );
            view.GetComponentsInChildren<Button>()
                .Single(button => button.GetComponentInChildren<TMP_Text>()?.text == "Show reports")
                .onClick.Invoke();
            TMP_Dropdown outcomes = view.GetComponentsInChildren<TMP_Dropdown>()
                .Single(dropdown => dropdown.name == "CollectionSelection");
            outcomes.value = outcomes.options.FindIndex(option => option.text == "Failed");
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "CustomizeEspionageReport")
                .onClick.Invoke();
            TMP_InputField input = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.Field.MemberName == "Body")
                .GetComponentInChildren<TMP_InputField>();
            Assert.AreEqual("Shared Failed {details}", input.text);
            input.SetTextWithoutNotify("Faction report {system}");
            input.onEndEdit.Invoke(input.text);
            ContentPackEditorStorage.Save(project);
            XDocument saved = XDocument.Load(definitions.TargetPath);
            XElement custom = saved
                .Root.Elements()
                .Single(entry => (string)entry.Element("FactionInstanceID") == faction.ID);
            Assert.AreEqual("Failed", (string)custom.Element("Outcome"));
            Assert.AreEqual("Faction report {system}", (string)custom.Element("Body"));
            Assert.AreEqual(
                "mission_report",
                (string)custom.Element("BackgroundImage").Attribute("Key")
            );

            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "ResetEspionageReport")
                .onClick.Invoke();
            Assert.IsFalse(
                XDocument
                    .Parse(definitions.Text)
                    .Root.Elements()
                    .Any(entry => (string)entry.Element("FactionInstanceID") == faction.ID)
            );
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
            CollectionAssert.Contains(fileItems, "Save");
            CollectionAssert.Contains(fileItems, "Validate");
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

        [Test]
        public void ConfirmModRemoval_InstalledContent_ShowsBoldSavesNote()
        {
            view.ConfirmModRemoval("Delete this copy?", "Delete", () => { });

            TMP_Text note = view.GetComponentInChildren<ActionDialogView>()
                .GetComponentsInChildren<TMP_Text>()
                .Single(text => text.text == "Saves will not be deleted.");

            Assert.That(note.fontStyle.HasFlag(FontStyles.Bold), Is.True);
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
                "Projects may require updates between releases.",
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
            Assert.AreEqual(GameSize.Large, requested.Value.GalaxySize);
            Assert.AreEqual(GameDifficulty.Medium, requested.Value.Difficulty);
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
            Assert.AreEqual("GameEvent", XElement.Parse(xml.text).Name.LocalName);
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
        public void RenderProject_MissionPages_SeparateFactionPresentationFromSharedRules()
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
            CollectionAssert.AreEquivalent(
                rules.Single(page => page.Caption == "AI decisions").Fields,
                rules.SelectMany(page => page.Fields).Where(field => field.Group == "AI")
            );
            Assert.IsTrue(
                rules
                    .Single(page => page.Caption == "Difficulty")
                    .Fields.Any(field => field.MemberName == "MissionSuccessChancePoints")
            );
            ContentEditorTreeNode diplomacy = pages.Single(page => page.MissionID == "Diplomacy");
            Assert.IsTrue(diplomacy.Fields.Any(field => field.SectionPath.Contains("Diplomacy")));
            Assert.IsTrue(
                diplomacy.Fields.All(field =>
                    field.Record.Kind == "GameConfig" && field.Group != "AI"
                )
            );
            foreach (string faction in new[] { "Alliance", "Empire" })
            {
                ContentEditorTreeNode presentation = navigation.Find(
                    "mission-presentation/" + faction + "/Diplomacy"
                );
                ContentPackEditorRecord article = presentation
                    .Fields.Single(field => field.MemberName == "Description")
                    .Record;
                Assert.AreEqual(faction, article.Catalog.Faction);
                SelectFactionMission(navigation, presentation);
                Assert.IsTrue(
                    view.GetComponentsInChildren<RawImage>()
                        .Any(image => image.texture == assets.GetTexture(article.ImageAddress))
                );
            }
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
        public void RenderProject_ShipNames_ChangesTheSelectedDesignPoolInline()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            view.GetComponentsInChildren<Button>()
                .Single(button => button.GetComponentInChildren<TMP_Text>()?.text == "Ship names")
                .onClick.Invoke();

            TMP_Dropdown designs = view.GetComponentsInChildren<TMP_Dropdown>()
                .Single(dropdown => dropdown.name == "ShipDesignDropdown");
            Assert.Greater(designs.options.Count, 1);
            int selected = (designs.value + 1) % designs.options.Count;
            string selectedName = designs.options[selected].text;
            designs.value = selected;
            ContentEditorFieldView pool = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.Field.MemberName == "ShipNamePoolID");
            Assert.AreEqual(selectedName, pool.Field.Record.Name);
            TMP_Dropdown choices = pool.GetComponentInChildren<TMP_Dropdown>();
            int target = choices.options.FindIndex(option =>
                option.text.StartsWith("Pool ", StringComparison.Ordinal)
                && option.text != choices.options[choices.value].text
            );
            Assert.GreaterOrEqual(target, 0);
            string expected = choices.options[target].text["Pool ".Length..];
            choices.value = target;

            Assert.AreEqual(expected, pool.Field.Value);
            Assert.IsTrue(designs.gameObject.activeInHierarchy);
        }

        [Test]
        public void SelectPage_NeutralTheme_OpensTheEditableSectorPreview()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find(
                ContentEditorContentSections.NeutralThemePageKey
            );

            SelectPage(page);

            ContentEditorSectorPreview preview =
                view.GetComponentInChildren<ContentEditorSectorPreview>();
            Assert.IsNotNull(preview);
            Assert.IsNotEmpty(preview.GetComponentsInChildren<ContentEditorArtworkEditTarget>());
            Assert.IsFalse(
                view.GetComponentsInChildren<Button>().Any(button => button.name == "Tab0")
            );
        }

        [Test]
        public void SelectPage_NeutralThemeWithHud_HidesPlayerStrategyView()
        {
            ContentPackEditorRecord neutral = ContentEditorPresentation.GetNeutralThemeRecord(
                project.GetCatalogs()
            );
            neutral.Element.Add(
                new XElement(
                    nameof(FactionTheme.TacticalHUDLayout),
                    new XElement(
                        nameof(TacticalHUDLayout.ImagePath),
                        "Pack/Factions/Alliance/Strategy/UI/Hud/background"
                    )
                )
            );
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find(
                ContentEditorContentSections.NeutralThemePageKey
            );

            SelectPage(page);

            TMP_Dropdown surfaces = view.GetComponentsInChildren<TMP_Dropdown>()
                .Single(dropdown => dropdown.name == "ArtworkViewSelector");
            CollectionAssert.DoesNotContain(
                surfaces.options.Select(option => option.text).ToArray(),
                "Strategy view"
            );
        }

        [Test]
        public void SelectPage_NeutralGalaxyMapIcons_ShowsEditableStarMarkers()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find(ContentEditorContentSections.NeutralThemePageKey));
            TMP_Dropdown surfaces = view.GetComponentsInChildren<TMP_Dropdown>()
                .Single(dropdown => dropdown.name == "ArtworkViewSelector");

            int index = surfaces.options.FindIndex(option => option.text == "Galaxy map icons");
            Assert.GreaterOrEqual(index, 0);
            surfaces.value = index;

            CollectionAssert.AreEquivalent(
                new[]
                {
                    nameof(PlanetIcons.Small),
                    nameof(PlanetIcons.Medium),
                    nameof(PlanetIcons.Large),
                    nameof(PlanetIcons.XL),
                },
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Where(field => field.Field.MediaType == ContentPackEditorMediaType.Image)
                    .Select(field => field.Field.MemberName)
            );
        }

        [Test]
        public void ShowField_NeutralThemeCoordinate_OpensAdvancedXml()
        {
            ContentPackEditorRecord neutral = ContentEditorPresentation.GetNeutralThemeRecord(
                project.GetCatalogs()
            );
            ContentPackEditorField coordinate = neutral
                .GetFields()
                .Single(field =>
                    field.MemberName == "X" && field.Group == nameof(FactionTheme.GalaxyBackground)
                );

            view.ShowField(coordinate, "Check this coordinate.");

            ContentEditorXmlView xml = view.GetComponentInChildren<ContentEditorXmlView>();
            Assert.IsNotNull(xml);
            StringAssert.Contains("<SourcePosition>", xml.Text);
        }

        [Test]
        public void RenderProject_FactionTabs_SeparateAuthoringTasks()
        {
            foreach (
                (string section, string tab) in new[]
                {
                    ("General", "Tab0"),
                    ("Colors", "Tab10"),
                    ("Rules", "Tab5"),
                    ("Ship names", "Tab6"),
                    ("Messages", "Tab7"),
                    ("Interfaces", "Tab1"),
                    ("Default backgrounds", "Tab9"),
                    ("Encyclopedia", "Tab8"),
                }
            )
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
                if (section == "Interfaces")
                {
                    fields = fields
                        .Where(field =>
                            ContentEditorContentSections.IsFactionInterfaceField(field)
                            && ContentEditorContentSections.FactionArtworkCategory(field)
                                == "Planet sector"
                        )
                        .ToArray();
                }
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
                if (section == "Colors")
                    CollectionAssert.AreEquivalent(
                        fields.Select(field => field.MemberName),
                        view.GetComponentsInChildren<ContentEditorColorPickerView>()
                            .SelectMany(picker => new[] { picker.Field, picker.LinkField })
                            .Where(field => field != null)
                            .Select(field => field.MemberName)
                    );
                else
                    Assert.AreEqual(
                        fields.Length,
                        view.GetComponentsInChildren<ContentEditorFieldView>()
                            .Count(field =>
                                section != "Ship names"
                                || field.Field.MemberName != "ShipNamePoolID"
                            )
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
                    CollectionAssert.Contains(headings, "Ship assignments");
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
                            Is.EqualTo((float)image.texture.width / image.texture.height)
                                .Within(0.02f)
                        );
                    }
                }
                Assert.IsFalse(project.IsDirty);
            }
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
            XmlSchemaSet schemas = new();
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

        [Test]
        public void RenderProject_GenerationTabs_ShowOnlyTheSelectedTask()
        {
            foreach (
                (string section, int index) in new[]
                {
                    ("Galaxy", 0),
                    ("Officers", 1),
                    ("Buildings", 2),
                    ("Units", 3),
                    ("Resources", 4),
                    ("Support", 5),
                    ("Balance", 6),
                }
            )
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
            XElement defaultResourceProfile = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind == "GameGenerationConfig")
                .SelectMany(record => record.Element.Descendants(nameof(PlanetResourceProfile)))
                .Single(element =>
                    element.Element(nameof(PlanetResourceProfile.Availability))?.Value
                    == GameResourceAvailability.Normal.ToString()
                );
            int defaultEnergy = int.Parse(
                defaultResourceProfile.Element(nameof(PlanetResourceProfile.EnergyMax)).Value
            );
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
                Assert.IsFalse(
                    planets[i].transform.Find("EnergyValueTextField").gameObject.activeSelf
                );
                Assert.AreEqual(
                    defaultEnergy,
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
        public void AddUnit_UnrelatedRecordSelected_CreatesAnUnassignedUnitAndSupportsUndo()
        {
            ContentPackEditorCatalog roster = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers");
            SelectRecord(
                project
                    .GetCatalogs()
                    .First(catalog => catalog.Name == "Buildings")
                    .Records.First(record =>
                        !ContentEditorUnitArtworkLayout.IsHeadquartersIcon(record)
                    )
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

        [Test]
        public void AddUnit_EmptyCatalog_CreatesFreshFieldsWithoutAnExistingUnit()
        {
            foreach (
                string category in new[]
                {
                    "Officers",
                    "Starfighters",
                    "Capital ships",
                    "Troops",
                    "Special forces",
                    "Buildings",
                }
            )
            {
                ContentPackEditorCatalog roster = project
                    .GetCatalogs()
                    .First(catalog => catalog.Name == category);
                string root = XDocument.Parse(roster.Document.Text).Root.Name.LocalName;
                project.SetDocumentText(roster.Document, "<" + root + " />");
                workspace.RenderProject(project, assets);
                roster = project
                    .GetCatalogs()
                    .Single(catalog => catalog.Document == roster.Document);

                ChooseUnitCategory(roster);

                ContentPackEditorRecord unit = roster.Records.Single();
                Assert.IsEmpty(unit.GetField("DisplayImagePath").Value);
                Assert.IsEmpty(unit.GetField("SmallDisplayImagePath").Value);
                Assert.IsEmpty(unit.GetField("EncyclopediaImagePath").Value);
                Assert.IsEmpty(unit.GetField("Description").Value);
                Assert.IsTrue(view.GetComponentsInChildren<ContentEditorFieldView>().Any());
                Assert.IsTrue(view.CommitFields());
            }
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

        [TestCase("Officers", "+ Add officer", "RecruitingFactionInstanceIDs", 0)]
        [TestCase("Starfighters", "+ Add starfighter", "ManufacturingFactionInstanceIDs", 1)]
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
        public void ShowNewEraDialog_OpeningTheWorkflow_LeavesTheWorkingPackUnchanged()
        {
            view.ShowNewEraDialog();
            Assert.IsTrue(
                view.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "New era")
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

        [TestCase("Mod / content pack", "New mod / content pack")]
        [TestCase("Era", "New era")]
        public void ClearProject_SelectingProjectType_OpensTheMatchingForm(
            string projectType,
            string heading
        )
        {
            view.ClearProject();

            Button create = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "NewPack");
            Assert.That(create.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("New project"));
            create.onClick.Invoke();
            ContextMenuCommandView[] commands =
                view.GetComponentsInChildren<ContextMenuCommandView>();
            string[] choices = commands
                .Select(item => item.GetComponentInChildren<TMP_Text>().text)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "Mod / content pack", "Era" }, choices);
            commands[Array.IndexOf(choices, projectType)]
                .OnPointerClick(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    }
                );

            ContentEditorPackDialogView dialog =
                view.GetComponentInChildren<ContentEditorPackDialogView>();
            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(
                dialog.GetComponentsInChildren<TMP_Text>().Any(label => label.text == heading),
                Is.True
            );
        }

        [Test]
        public void ProjectsPage_AuthoringPreview_UsesTheFullProjectWidth()
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/ContentPackEditor/ContentPackEditorRoot.prefab"
            );
            ScrollAreaLayout page = prefab
                .GetComponentsInChildren<ScrollAreaLayout>(true)
                .Single(scroll => scroll.name == "ProjectsPage");
            var projects = (RectTransform)page.ScrollArea.ContentRoot.Find("Projects");

            Assert.That(
                page.ScrollArea.ViewportWidth,
                Is.GreaterThanOrEqualTo(projects.rect.width)
            );
        }

        [Test]
        public void ClearProject_FileNewSubmenu_ListsModsBeforeEras()
        {
            view.ClearProject();
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "File")
                .onClick.Invoke();
            var commands = view.GetComponentsInChildren<ContextMenuCommandView>();
            ContextMenuCommandView create = commands.Single(command =>
                command.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "New")
            );
            create.OnPointerEnter(new PointerEventData(EventSystem.current));
            view.GetComponentInChildren<ContextMenuView>(true).RenderCurrent();

            string[] choices = view.GetComponentsInChildren<ContextMenuCommandView>()
                .Except(commands)
                .Select(command => command.GetComponentInChildren<TMP_Text>().text)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "Mod / content pack", "Era" }, choices);
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
        public void RenderProject_ApplyingUnchangedXml_KeepsTheEditorOpenWithoutCreatingAnOverride()
        {
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == "Officers").Records.First()
            );
            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Tab3")
                .onClick.Invoke();
            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "ApplyXml")
                .onClick.Invoke();

            Assert.IsTrue(
                view.GetComponentsInChildren<Button>()
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
        public void HandleKeyboardShortcut_EscapeWithNewEraDialog_ClosesOnlyTheDialog()
        {
            view.RenderStatus("Keep this status.", false);
            view.ShowNewEraDialog();
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
            string key =
                catalog.Faction == "Shared"
                    ? "missions/reference-only-mission"
                    : "mission-presentation/" + catalog.Faction + "/reference-only-mission";
            if (catalog.Faction == "Shared")
            {
                SelectPage(navigation.Find(key));
                Assert.IsFalse(
                    view.GetComponentsInChildren<Button>()
                        .Any(button => button.GetComponentInChildren<TMP_Text>()?.text == "Rules")
                );
            }
            else
                SelectFactionMission(navigation, navigation.Find(key));
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Any(field => field.Field.MemberName == "Description")
            );
        }

        /// <summary>
        /// Replaces one image twice so the active workspace owns usable artwork undo revisions.
        /// </summary>
        /// <returns>The current artwork file.</returns>
        private string AddArtworkHistory()
        {
            ContentPackEditorField field = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Starfighter")
                .GetField("DisplayImagePath");
            Texture2D texture = new Texture2D(1, 1);
            try
            {
                texture.SetPixel(0, 0, Color.red);
                texture.Apply();
                string address = ContentPackEditorStorage.WriteGeneratedPng(
                    project,
                    field,
                    texture.EncodeToPNG()
                );
                project.SetField(field, address);
                texture.SetPixel(0, 0, Color.blue);
                texture.Apply();
                ContentPackEditorStorage.WriteGeneratedPng(project, field, texture.EncodeToPNG());
                return Path.Combine(project.PackRootPath, address[5..] + ".png");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Shows the mission in its faction's mission presentation page.
        /// </summary>
        /// <param name="navigation">The fixture navigation tree.</param>
        /// <param name="mission">The mission node to open.</param>
        private void SelectFactionMission(
            ContentEditorNavigation navigation,
            ContentEditorTreeNode mission
        )
        {
            string factionKey = "factions/" + mission.Key.Split('/')[1];
            SelectPage(navigation.Find(factionKey));
            view.GetComponentsInChildren<Button>()
                .Single(button =>
                    button.GetComponentInChildren<TMP_Text>()?.text == "Mission presentation"
                )
                .onClick.Invoke();
            TMP_Dropdown selector = view.GetComponentsInChildren<TMP_Dropdown>()
                .Single(dropdown => dropdown.name == "MissionPresentationDropdown");
            int index = selector.options.FindIndex(item => item.text == mission.Caption);
            Assert.GreaterOrEqual(index, 0, mission.Key);
            selector.value = index;
        }
    }
}
