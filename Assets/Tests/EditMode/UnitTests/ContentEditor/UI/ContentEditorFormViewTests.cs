using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorFormViewTests
    {
        private string folder;
        private ContentPackEditorProject project;
        private ContentAssets assets;
        private GameObject canvas;
        private ContentPackEditorRecord selectedRecord;
        private ContentEditorTreeNode selectedPage;
        private string section;
        private ContentEditorFormView view;

        /// <summary>
        /// Creates independent document inputs without opening the editor window.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(
                Path.GetTempPath(),
                "editor-form-" + Guid.NewGuid().ToString("N")
            );
            project = ContentEditorTestData.CreateProject(folder);
            assets = TestContent.Assets;
            section = "General";
            selectedRecord = null;
            selectedPage = null;
            canvas = new GameObject("Form test", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(800, 900);
            view = UIComponentTestHelper.InstantiatePrefabComponent<ContentEditorFormView>(
                "Assets/Prefabs/UI/ContentPackEditor/ContentPackEditorRoot.prefab"
            );
            RectTransform rect = (RectTransform)view.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(800, 900);
            view.gameObject.SetActive(true);
            ContentBindings.Apply(view.gameObject, assets);
            view.FieldSubmitted += SubmitField;
            view.FieldValidationRequested += project.ValidateField;
            view.ClearFieldsRequested += project.ClearFields;
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Releases the isolated form and fixture-owned files.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (canvas != null)
                UnityEngine.Object.DestroyImmediate(canvas);
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [Test]
        public void Render_FactionMissionRestrictions_UsesNamedCheckboxesForTheAuthoredSelection()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find("factions/Alliance");
            SelectPage(page);
            RenderSection("Rules");
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
        public void Render_PlanetEncyclopedia_EditsEmbeddedDataBeforeTheStandaloneArticle()
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
            RenderSection("Encyclopedia");
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
        public void Render_MissionRules_UsesOnlyItsOwnSettingsAndCenteredSuccessTable()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find("missions/Diplomacy");
            SelectPage(page);
            Assert.IsFalse(
                view.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "Mission rules")
            );
            RenderSection("Rules");
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
            RenderForm();
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
        public void Render_EmptyProbabilityCell_SavesAndReloadsAnEnteredValue(string value)
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
        public void Render_EmptyProbabilityCell_RejectsInvalidNumbersWithoutAddingAnEntry(
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
        public void Render_OptionalProbabilityCell_ClearsRetypesAndRestoresThroughHistory()
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
            RenderForm();
            Assert.AreEqual("64", SelectSharedMissionRules().GetCell(0, column).Value);
        }

        [Test]
        public void Render_EmptyProbabilityColumn_RemainsAvailableForNewValues()
        {
            ContentEditorNumericGrid grid = SelectSharedMissionRules();
            int column = grid.Columns.ToList().IndexOf("Foil");
            foreach (
                int score in grid.Scores.Where(score =>
                    grid.GetCell(score, column).Value.Length > 0
                )
            )
                project.SetField(grid.GetCell(score, column), string.Empty);
            RenderForm();
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
        public void Render_FactionReferenceArticle_EditsItsOwnDocumentAndSupportsUndo()
        {
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(project.GetCatalogs());
            ContentEditorTreeNode page = navigation.Find("factions/Alliance");
            ContentPackEditorRecord article = page.RelatedRecords.Single(record =>
                record.Kind == "EncyclopediaEntry" && record.ID == "FLEET"
            );
            string before = article.Element.Element("Description").Value;
            SelectPage(page);
            RenderSection("Encyclopedia");
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
            RenderForm();
            Assert.AreEqual(
                before,
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(field => field.name == "DescriptionField")
                    .GetComponentInChildren<TMP_InputField>()
                    .text
            );
            Assert.IsTrue(project.Redo());
            RenderForm();
            Assert.AreEqual(
                "Edited fleet article",
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(field => field.name == "DescriptionField")
                    .GetComponentInChildren<TMP_InputField>()
                    .text
            );
        }

        [Test]
        public void Validate_StoredInvalidNumber_BlocksSavingWithoutTrappingNavigation()
        {
            ContentPackEditorCatalog ships = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Capital ships");
            XDocument document = XDocument.Parse(ships.Document.Text);
            document.Root.Elements().First().SetElementValue("Hyperdrive", "invalid");
            project.SetDocumentText(ships.Document, document.ToString());
            RenderForm();
            SelectRecord(
                project
                    .GetCatalogs()
                    .First(catalog => catalog.Name == "Capital ships")
                    .Records.First()
            );

            Assert.IsTrue(view.Commit());
            Assert.IsFalse(view.Validate());
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(field => field.Field.MemberName == "Hyperdrive")
                    .HasError
            );
        }

        [Test]
        public void Render_SpecialForcesAllowedMissions_UsesASelectableMissionSet()
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
        public void Render_ManufacturingResearchRequirements_AppearWithProductionInsteadOfSkills(
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
        public void Render_AttachingOptionalTraitMember_KeepsOtherDraftsBoundToTheirOwnFields()
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
            RenderForm();
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
        public void Render_TraitWithOmittedModifiers_ShowsNeutralDefaultsWithoutAuthoringThem()
        {
            ContentPackEditorRecord trait = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Trait");
            XDocument document = XDocument.Parse(trait.Catalog.Document.Text);
            XElement element = document.Descendants("Trait").First();
            element.Element("PersonnelTravelTimePercent")?.Remove();
            project.SetDocumentText(trait.Catalog.Document, document.ToString());
            RenderForm();
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
        public void Render_FactionReferences_UseNamesAndStoreTheSelectedIdentity()
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
        public void Render_InvalidDraft_RevertRestoresTheAcceptedValueWithoutChangingHistory()
        {
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == "Officers").Records.First()
            );
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
            Assert.IsTrue(view.Commit());
            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(project.Undo());
        }

        [Test]
        public void Render_StartingMessages_SelectingAnEntryDoesNotDuplicateOrEditOtherMessages()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            RenderSection("Messages");
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
        public void Render_AddingProbabilityThreshold_LeavesXmlUntouchedUntilAValueIsEntered()
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
        public void Render_OfficerTraits_ShowsNamedSelectionsAndRetainsTheAuthoredSet()
        {
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            string traitID = project.GetTraitOptions().Keys.First();
            project.SetFieldValues(officer.GetField("Traits"), new[] { traitID });
            RenderForm();
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
        public void Render_RecruitingFactions_UsesNamedCheckboxesAndStoresTheSelectedID()
        {
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == "Officers").Records.First()
            );
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
        public void Render_DirectoryPreview_OffersInheritedFilesWithoutEditingTheRoot()
        {
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == "Officers").Records.First()
            );
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
        public void Render_FieldGuidance_ShowsInfoIconsOnlyForDocumentedCaptions()
        {
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == "Officers").Records.First()
            );
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

        [Test]
        public void Render_ChangingAnEnum_UsesTheSharedSelectorAndUpdatesTheDocument()
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
        public void Render_BooleanAndTextControls_AlignToTheSameRowCenter()
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
        public void Render_TraitsAndSharedRules_GroupsControlsByTheirActualScope()
        {
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == "Officers").Records.First()
            );
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
        public void Render_NestedGeneration_ShowsSeparateSectionsAndCompleteFieldCaptions(int width)
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(width, 900);
            Canvas.ForceUpdateCanvases();
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "GameGenerationConfig");

            SelectRecord(theme);
            RenderSection("Galaxy");
            RenderSection("Galaxy");
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
        public void Render_InvalidAuthoredBoolean_AllowsRepairThroughTheTextInput()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == "Officers");
            System.Xml.Linq.XDocument xml = System.Xml.Linq.XDocument.Parse(catalog.Document.Text);
            xml.Root.Elements().First().SetElementValue("CanBetray", "not-a-bool");
            project.SetDocumentText(catalog.Document, xml.ToString());

            Assert.DoesNotThrow(() => RenderForm());
            SelectRecord(
                project.GetCatalogs().First(item => item.Name == "Officers").Records.First()
            );
            TMP_InputField input = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.name == "CanbetrayField")
                .GetComponentInChildren<TMP_InputField>();
            Assert.AreEqual("not-a-bool", input.text);
            input.SetTextWithoutNotify("true");
            input.onEndEdit.Invoke("true");

            Assert.IsTrue(view.Commit());
            StringAssert.Contains("<CanBetray>true</CanBetray>", catalog.Document.Text);
        }

        [Test]
        public void Render_InvalidArtworkAddress_LeavesTheReferenceEditable()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == "Officers");
            System.Xml.Linq.XDocument xml = System.Xml.Linq.XDocument.Parse(catalog.Document.Text);
            xml.Root.Elements()
                .First()
                .SetElementValue("EncyclopediaImagePath", "unscoped/image.png");
            project.SetDocumentText(catalog.Document, xml.ToString());

            Assert.DoesNotThrow(() => RenderForm());
            SelectRecord(
                project.GetCatalogs().First(item => item.Name == "Officers").Records.First()
            );
            RenderSection("Encyclopedia");
            TMP_InputField input = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.name == "EncyclopediaimagepathField")
                .GetComponentInChildren<TMP_InputField>();

            Assert.AreEqual("unscoped/image.png", input.text);
        }

        [Test]
        public void Render_InvalidEnum_OffersTheTypedSelectorAndKeepsTheDraftUnchanged()
        {
            ContentPackEditorRecord building = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Buildings")
                .Records.First();
            XDocument xml = XDocument.Parse(building.Catalog.Document.Text);
            xml.Root.Elements().First().SetElementValue("BuildingType", "InvalidKind");
            project.SetDocumentText(building.Catalog.Document, xml.ToString());
            RenderForm();
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

        [TestCase(420)]
        [TestCase(1000)]
        public void Render_RulesAndMissionPages_KeepsWrappedTextAndControlsInsideTheirRows(
            int width
        )
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(width, 900);
            Canvas.ForceUpdateCanvases();
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            navigation.SetExpanded(true);
            ContentEditorTreeNode[] pages = navigation
                .GetRows(string.Empty)
                .Where(node => node.Record != null || node.Fields != null)
                .Where(node => node.Record?.Kind != "GameEvent")
                .GroupBy(node =>
                    (
                        Kind: node.Record?.Kind,
                        Group: node.Fields?.FirstOrDefault()?.Group,
                        Mission: node.MissionID != null
                    )
                )
                .Select(group =>
                    group
                        .OrderByDescending(node =>
                            node.Fields?.Count ?? node.Record.GetFields().Count
                        )
                        .First()
                )
                .ToArray();
            foreach (ContentEditorTreeNode page in pages)
            {
                SelectPage(page);
                if (page.Record?.Kind == "GameGenerationConfig")
                    RenderSection(ContentEditorContentSections.GenerationSections[0]);
                if (
                    page.MissionID != null
                    && page.Fields.Any(field => field.Record.Kind == "GameConfig")
                )
                    RenderSection("Rules");
                Canvas.ForceUpdateCanvases();
                Assert.IsTrue(view.Commit(), page.Key);
                Assert.IsFalse(project.IsDirty, page.Key);
                foreach (
                    ContentEditorFieldView control in view.GetComponentsInChildren<ContentEditorFieldView>()
                )
                {
                    RectTransform rect = (RectTransform)control.transform;
                    foreach (
                        TMP_InputField input in control.GetComponentsInChildren<TMP_InputField>()
                    )
                    {
                        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                            rect,
                            input.transform
                        );
                        Assert.LessOrEqual(
                            bounds.max.x,
                            rect.rect.xMax + 1,
                            page.Caption + "/" + control.name
                        );
                        Assert.GreaterOrEqual(
                            bounds.min.x,
                            rect.rect.xMin - 1,
                            page.Caption + "/" + control.name
                        );
                        Assert.GreaterOrEqual(
                            bounds.min.y,
                            rect.rect.yMin - 1,
                            page.Caption + "/" + control.name
                        );
                    }
                }
                foreach (
                    TMP_Text label in view.GetComponentsInChildren<TMP_Text>()
                        .Where(text =>
                            text.name.StartsWith("GroupTemplate", StringComparison.Ordinal)
                            || text.name == "Label"
                                && text.transform.parent.GetComponent<ContentEditorFieldView>()
                                    != null
                        )
                )
                {
                    if (label.text is "Score" or "Threshold")
                    {
                        label.ForceMeshUpdate();
                        Assert.AreEqual(
                            1,
                            label.textInfo.lineCount,
                            page.Caption + ": " + label.text
                        );
                    }
                    Assert.LessOrEqual(
                        label
                            .GetPreferredValues(
                                label.text,
                                label.rectTransform.rect.width,
                                float.PositiveInfinity
                            )
                            .y,
                        label.rectTransform.rect.height + 1,
                        page.Caption + ": " + label.text
                    );
                }
            }
            Assert.IsFalse(project.IsDirty);
            Assert.AreEqual(
                1,
                Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Count()
            );
        }

        [Test]
        public void Render_StackedInput_OutlinesRemainInsideTheFieldAndScrollMask()
        {
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            SelectRecord(officer);
            RenderSection("Encyclopedia");
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
        public void Render_NarrowMapProperties_GivesNamesTheFullPropertyColumn()
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(340, 900);
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

        [TestCase("Officers", "Encyclopedia", 1000)]
        [TestCase("Officers", "Encyclopedia", 1800)]
        [TestCase("Officers", "Artwork", 1000)]
        [TestCase("Officers", "Artwork", 1800)]
        [TestCase("Capital ships", "Encyclopedia", 1000)]
        [TestCase("Capital ships", "Artwork", 1800)]
        public void Render_MediaTabAfterGeneral_KeepsTheImageAboveEveryField(
            string category,
            string tabName,
            int width
        )
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(width, 900);
            Canvas.ForceUpdateCanvases();
            ContentPackEditorRecord record = project
                .GetCatalogs()
                .First(catalog => catalog.Name == category)
                .Records.First();
            SelectRecord(record);
            RenderSection(tabName);
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

        /// <summary>
        /// Presents one record directly through the form's public boundary.
        /// </summary>
        /// <param name="record">The definition to display.</param>
        private void SelectRecord(ContentPackEditorRecord record)
        {
            selectedRecord = record;
            selectedPage = null;
            section = ContentPackEditorProjector
                .BuildSelection(record, null, null, "General")
                .Section;
            RenderForm();
        }

        /// <summary>
        /// Presents a projected page without constructing navigation controls.
        /// </summary>
        /// <param name="page">The logical page to display.</param>
        private void SelectPage(ContentEditorTreeNode page)
        {
            selectedPage = page;
            selectedRecord =
                page.Record
                ?? page.Catalog?.DocumentRecord
                ?? page.Fields?.FirstOrDefault()?.Record;
            section = "General";
            RenderForm();
        }

        /// <summary>
        /// Selects one inspector section without constructing its window tabs.
        /// </summary>
        /// <param name="value">The section owned by the form.</param>
        private void RenderSection(string value)
        {
            section = value;
            RenderForm();
        }

        /// <summary>
        /// Creates only the authored form subtree and refreshes its current document bindings.
        /// </summary>
        private void RenderForm()
        {
            if (selectedPage != null)
            {
                ContentEditorNavigation navigation = new();
                navigation.Render(project.GetCatalogs());
                selectedPage = navigation.Find(selectedPage.Key);
                selectedRecord =
                    selectedPage.Record
                    ?? selectedPage.Catalog?.DocumentRecord
                    ?? selectedPage.Fields?.FirstOrDefault()?.Record;
            }
            else
            {
                string key = selectedRecord?.Key;
                selectedRecord = project
                    .GetCatalogs()
                    .SelectMany(catalog =>
                        catalog.Records.Concat(catalog.Records.SelectMany(record => record.Planets))
                    )
                    .FirstOrDefault(record =>
                        key == null ? record.Kind == "Officer" : record.Key == key
                    );
            }
            view.Render(project, assets, selectedRecord, selectedPage, section, true);
        }

        /// <summary>
        /// Connects a submitted form value to its independent document owner.
        /// </summary>
        /// <param name="field">The submitted member.</param>
        /// <param name="values">The submitted scalar or collection values.</param>
        private void SubmitField(ContentPackEditorField field, IReadOnlyList<string> values)
        {
            if (field.IsList)
                project.SetFieldValues(field, values);
            else
                project.SetField(field, values.Single());
        }

        /// <summary>
        /// Opens shared detection rules through their visible navigation row.
        /// </summary>
        /// <returns>The current editable probability grid.</returns>
        private ContentEditorNumericGrid SelectSharedMissionRules()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            navigation.SetExpanded(true);
            ContentEditorTreeNode page = navigation
                .GetRows(string.Empty)
                .Single(node => node.Caption == "Shared mission rules");
            SelectPage(page);
            return new ContentEditorNumericGrid(page.Fields);
        }

        /// <summary>
        /// Finds a visible shared input by its probability-table coordinates.
        /// </summary>
        /// <param name="score">The row threshold.</param>
        /// <param name="column">The probability column.</param>
        /// <returns>The editable input.</returns>
        private TMP_InputField ProbabilityInput(int score, int column) =>
            view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.name == "Probability" + column + "Score" + score + "Field")
                .GetComponentInChildren<TMP_InputField>();

        /// <summary>
        /// Reads the authored value directly from the project's current rules document.
        /// </summary>
        /// <param name="score">The success-table threshold.</param>
        /// <returns>The serialized value, or empty when no entry exists.</returns>
        private string ProbabilityValue(int score) =>
            project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Rules")
                .Records.Single()
                .Element.Element("ProbabilityTables")
                .Element("Mission")
                .Element("Foil")
                .Elements("Entry")
                .SingleOrDefault(entry =>
                    entry.Element("Key").Value
                    == score.ToString(System.Globalization.CultureInfo.InvariantCulture)
                )
                ?.Element("Value")
                ?.Value
            ?? string.Empty;
    }
}
