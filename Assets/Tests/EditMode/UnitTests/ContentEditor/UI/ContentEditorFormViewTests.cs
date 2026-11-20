using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public void Render_AIDecisionIntervals_ExposesOnlyPositiveDecisionValues()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            navigation.SetExpanded(true);
            ContentEditorTreeNode page = navigation
                .GetRows(string.Empty)
                .Single(node => node.Caption == "AI decisions");

            SelectPage(page);

            Assert.AreEqual(3, page.Fields.Count);
            CollectionAssert.AreEquivalent(
                new[] { "Easy", "Medium", "Hard" },
                page.Fields.Select(field => field.XmlElement.Parent.Element("Key").Value)
            );
            Assert.IsTrue(page.Fields.All(field => field.MemberName == "Value" && field.IsNumber));
            Assert.AreEqual(
                3,
                view.GetComponentsInChildren<TMP_InputField>().Count(input => input.interactable)
            );
            ContentPackEditorField field = page.Fields.First();
            Assert.Throws<ArgumentException>(() => project.SetField(field, "0"));
            project.SetField(field, "13");
            Assert.AreEqual("13", field.Value);
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

        [TestCase("RefinementMultiplier")]
        [TestCase("ResourceProcessingPointsPerFacility")]
        public void Render_FactionEconomyRules_ShowsHelpInsideEachFieldRow(string member)
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            RenderSection("Rules");
            Canvas.ForceUpdateCanvases();

            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(control => control.Field.MemberName == member);
            HelpTooltipView help = field.GetComponentInChildren<HelpTooltipView>();
            RectInt bounds = UILayout.GetSourceRect((RectTransform)help.transform);
            RectTransform row = (RectTransform)field.transform;

            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(row.rect.height));
            Assert.That(field.Field.HelpText, Is.Not.Empty);
        }

        [Test]
        public void FocusField_FieldBelowViewport_AlignsRowWithViewportTop()
        {
            ScrollAreaLayout fields = view.GetComponentsInChildren<ScrollAreaLayout>(true).Single();
            ((RectTransform)fields.transform).SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                120
            );
            SelectRecord(
                project.GetCatalogs().First(catalog => catalog.Name == "Officers").Records.First()
            );
            Canvas.ForceUpdateCanvases();
            ContentEditorFieldView target = view.GetComponentsInChildren<ContentEditorFieldView>()
                .OrderBy(field => ((RectTransform)field.transform).anchoredPosition.y)
                .First();
            RectTransform targetRect = (RectTransform)target.transform;
            float expectedOffset = -targetRect.anchoredPosition.y;
            Assert.Greater(expectedOffset, 0);

            Assert.IsTrue(view.FocusField(target.Field));

            Assert.AreEqual(
                expectedOffset,
                fields.ScrollArea.ContentRoot.anchoredPosition.y,
                0.01f
            );
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
        public void Commit_CurrentValueErrorOnChoice_AllowsNavigationWithoutHidingTheError()
        {
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            SelectRecord(officer);
            ContentPackEditorField rank = officer.GetField("CurrentRank");

            Assert.IsTrue(view.FocusField(rank, "Choose a listed value."));
            Assert.IsTrue(view.Commit());

            ContentEditorFieldView row = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.Field.MemberName == "CurrentRank");
            Assert.IsTrue(row.HasError);
            Assert.IsFalse(row.HasRejectedDraft);
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
                project
                    .GetCatalogs()
                    .First(catalog => catalog.Name == catalogName)
                    .Records.First(record =>
                        !ContentEditorUnitArtworkLayout.IsHeadquartersIcon(record)
                    )
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
            foreach (
                ContentEditorFieldView field in fields.Where(field =>
                    !string.IsNullOrWhiteSpace(field.Field.Value)
                )
            )
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
            view.transform.localScale = Vector3.one * 0.65f;
            Canvas.ForceUpdateCanvases();
            Image minimumZoomGlyph = view.GetComponentsInChildren<HelpTooltipView>()
                .First(info => info.gameObject.activeSelf)
                .GetComponentsInChildren<Image>()
                .Single(image => image.sprite != null);
            Vector3[] corners = new Vector3[4];
            minimumZoomGlyph.rectTransform.GetWorldCorners(corners);
            Assert.GreaterOrEqual(Vector3.Distance(corners[0], corners[3]), 15f);
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
        public void Render_StartingSetupRange_ShowsDescriptionIconBesideGroupedCaption()
        {
            ContentPackEditorRecord setup = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "GameGenerationConfig");

            SelectRecord(setup);
            RenderSection("Resources");
            Canvas.ForceUpdateCanvases();

            TMP_Text caption = view.GetComponentsInChildren<TMP_Text>()
                .Single(label => label.text == "Core energy");
            HelpTooltipView info = caption.GetComponentInChildren<HelpTooltipView>();
            Assert.IsNotNull(info);
            Assert.IsTrue(info.gameObject.activeSelf);
            TMP_LineInfo line = caption.textInfo.lineInfo[0];
            float lineCenter = -(line.ascender + line.descender) * 0.5f;
            Assert.AreEqual(
                lineCenter,
                UILayout.GetSourceRect((RectTransform)info.transform).center.y,
                1f
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void Render_InvalidAuthoredBoolean_AllowsRepairThroughTheTextInput()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == "Officers");
            XDocument xml = XDocument.Parse(catalog.Document.Text);
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
        public void Render_InvalidArtworkAddress_KeepsTheCompactChooserAvailable()
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == "Officers");
            XDocument xml = XDocument.Parse(catalog.Document.Text);
            xml.Root.Elements()
                .First()
                .SetElementValue("EncyclopediaImagePath", "unscoped/image.png");
            project.SetDocumentText(catalog.Document, xml.ToString());

            Assert.DoesNotThrow(() => RenderForm());
            SelectRecord(
                project.GetCatalogs().First(item => item.Name == "Officers").Records.First()
            );
            RenderSection("Encyclopedia");
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.name == "EncyclopediaimagepathField");
            TMP_InputField input = field.GetComponentInChildren<TMP_InputField>(true);
            TMP_Text selected = field
                .GetComponentsInChildren<TMP_Text>()
                .Single(text => text.name == "SelectedMedia");

            Assert.IsFalse(input.gameObject.activeSelf);
            Assert.AreEqual("image.png", selected.text);
            Assert.IsTrue(
                field.GetComponentsInChildren<Button>().Any(button => button.name == "Import")
            );
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

        [TestCase("Officer")]
        [TestCase("SpecialForce")]
        [TestCase("Regiment")]
        [TestCase("CapitalShip")]
        [TestCase("Starfighter")]
        [TestCase("Building")]
        public void Render_UnitArtwork_ShowsEverySupportedImageField(string kind)
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == kind);
            SelectRecord(unit);
            RenderSection("Artwork");

            HashSet<string> members = new(StringComparer.Ordinal);
            TMP_Dropdown size = view.GetComponentsInChildren<TMP_Dropdown>()
                .SingleOrDefault(dropdown => dropdown.name == "ArtworkStateSizeDropdown");
            int count = size?.options.Count ?? 1;
            for (int index = 0; index < count; index++)
            {
                if (size != null)
                    size.value = index;
                ContentEditorFieldView[] fields =
                    view.GetComponentsInChildren<ContentEditorFieldView>();
                Assert.IsTrue(fields.All(field => field.Field.Record.Element == unit.Element));
                members.UnionWith(fields.Select(field => field.Field.MemberName));
            }
            CollectionAssert.IsSubsetOf(ContentEditorUnitArtworkLayout.GetMembers(unit), members);
        }

        [Test]
        public void Render_UnitArtworkFile_UsesSingleImageActionWithoutRedundantControls()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "Starfighter" && record.Catalog.Faction == "Alliance"
                );
            SelectRecord(unit);
            RenderSection("Artwork");
            ContentEditorFieldView image = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.Field.MemberName == "DisplayImagePath");
            TMP_InputField input = image.GetComponentInChildren<TMP_InputField>(true);
            TMP_Text selected = image
                .GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.name == "SelectedMedia");

            Assert.IsFalse(input.gameObject.activeSelf);
            Assert.IsFalse(selected.gameObject.activeSelf);
            Button action = image.GetComponentsInChildren<Button>().Single();
            Assert.AreEqual("Import", action.name);
            Assert.AreEqual("Change image...", action.GetComponentInChildren<TMP_Text>().text);
        }

        [TestCase("Officer")]
        [TestCase("SpecialForce")]
        [TestCase("Regiment")]
        [TestCase("CapitalShip")]
        [TestCase("Starfighter")]
        [TestCase("Building")]
        public void Render_UnitArtworkCards_KeepEveryDirectChildInsideTheContentInset(string kind)
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == kind
                    && (
                        kind != "Building"
                        || !ContentEditorUnitArtworkLayout.IsHeadquartersIcon(record)
                    )
                );
            SelectRecord(unit);
            RenderSection("Artwork");
            Canvas.ForceUpdateCanvases();

            ContentEditorCardView[] cards = view.GetComponentsInChildren<ContentEditorCardView>();

            Assert.IsNotEmpty(cards);
            foreach (ContentEditorCardView card in cards)
            {
                Assert.Greater(card.transform.childCount, 0, card.name);
                float right = card.RectTransform.rect.width - ContentEditorCardView.ContentInset;
                float bottom = card.RectTransform.rect.height - ContentEditorCardView.ContentInset;
                foreach (
                    RectTransform child in card.GetComponentsInChildren<RectTransform>()
                        .Where(rect => rect.parent == card.transform && rect.gameObject.activeSelf)
                )
                {
                    RectInt bounds = UILayout.GetSourceRect(child);
                    Assert.GreaterOrEqual(
                        bounds.xMin,
                        ContentEditorCardView.ContentInset,
                        card.name + "/" + child.name
                    );
                    Assert.LessOrEqual(bounds.xMax, right + 1, card.name + "/" + child.name);
                    Assert.GreaterOrEqual(
                        bounds.yMin,
                        ContentEditorCardView.ContentInset,
                        card.name + "/" + child.name
                    );
                    Assert.LessOrEqual(bounds.yMax, bottom + 1, card.name + "/" + child.name);
                }
            }
        }

        [Test]
        public void Render_AfterUnitArtwork_HidesEveryArtworkCardFromOtherSections()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Officer");
            SelectRecord(unit);
            RenderSection("Artwork");
            Assert.IsNotEmpty(view.GetComponentsInChildren<ContentEditorCardView>());

            RenderSection("General");

            Assert.IsEmpty(view.GetComponentsInChildren<ContentEditorCardView>());
        }

        [Test]
        public void Render_UnitArtworkImages_ShowsOneSemanticCardPerField()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "Starfighter" && record.Catalog.Faction == "Alliance"
                );
            SelectRecord(unit);
            RenderSection("Artwork");

            ContentEditorCardView results = view.GetComponentsInChildren<ContentEditorCardView>()
                .Single(card => card.name.EndsWith("-Images", StringComparison.Ordinal));
            ContentEditorFieldView[] fields =
                results.GetComponentsInChildren<ContentEditorFieldView>();
            RawImage[] previews = results
                .GetComponentsInChildren<RawImage>()
                .Where(image => image.name.Contains("Output-", StringComparison.Ordinal))
                .ToArray();

            Assert.IsNotEmpty(fields);
            Assert.IsNotEmpty(previews);
            Assert.IsFalse(
                view.GetComponentsInChildren<ContentEditorCardView>()
                    .Any(card => card.name.Contains("OutputFiles", StringComparison.Ordinal))
            );
            foreach (
                RawImage backing in previews.Where(image =>
                    image.name.EndsWith("-Backing", StringComparison.Ordinal)
                )
            )
            {
                ContentEditorFieldView field = fields.Single(candidate =>
                    backing.name.EndsWith(
                        "Output-" + candidate.Field.MemberName + "-Backing",
                        StringComparison.Ordinal
                    )
                );
                RectInt imageBounds = UILayout.GetSourceRect(backing.rectTransform);
                RectInt fieldBounds = UILayout.GetSourceRect((RectTransform)field.transform);
                Assert.AreEqual(imageBounds.x, fieldBounds.x, field.name);
                Assert.AreEqual(imageBounds.width, fieldBounds.width, field.name);
                Assert.GreaterOrEqual(fieldBounds.y, imageBounds.yMax, field.name);
                Button[] actions = field.GetComponentsInChildren<Button>();
                Assert.AreEqual(1, actions.Length, field.name);
                StringAssert.Contains(
                    "image...",
                    actions[0].GetComponentInChildren<TMP_Text>().text
                );
            }
            Assert.IsTrue(
                results.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "Images")
            );
            Assert.IsFalse(
                results
                    .GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text is "display" or "encyclopedia")
            );
        }

        [Test]
        public void Render_UnitArtworkWithLargeImage_OffersGenerationForOrdinaryOutputs()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Officer" && record.Catalog.Faction == "Alliance");
            ContentPackEditorField requestedSource = null;
            IReadOnlyList<ContentPackEditorField> requestedTargets = null;
            view.ArtworkCutsRequested += (source, targets) =>
            {
                requestedSource = source;
                requestedTargets = targets;
            };
            SelectRecord(unit);
            RenderSection("Artwork");

            Button action = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "GenerateArtworkCuts");
            action.onClick.Invoke();

            Assert.AreEqual(
                "Generate cuts from message image",
                action.GetComponentInChildren<TMP_Text>().text
            );
            Assert.AreEqual("MessageImagePath", requestedSource.MemberName);
            CollectionAssert.AreEquivalent(
                new[] { "DisplayImagePath", "SmallDisplayImagePath", "EncyclopediaImagePath" },
                requestedTargets.Select(field => field.MemberName).ToArray()
            );
        }

        [Test]
        public void Render_ShipArtworkWithoutMessage_UsesTransparentDisplayAsGenerationSource()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "CapitalShip" && record.Catalog.Faction == "Alliance"
                );
            ContentPackEditorField requestedSource = null;
            IReadOnlyList<ContentPackEditorField> requestedTargets = null;
            view.ArtworkCutsRequested += (source, targets) =>
            {
                requestedSource = source;
                requestedTargets = targets;
            };
            SelectRecord(unit);
            RenderSection("Artwork");

            Button action = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "GenerateArtworkCuts");
            action.onClick.Invoke();

            Assert.AreEqual(
                "Generate cuts from display image",
                action.GetComponentInChildren<TMP_Text>().text
            );
            Assert.AreEqual("DisplayImagePath", requestedSource.MemberName);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "SmallDisplayImagePath",
                    "BattleResultImagePath",
                    "MessageImagePath",
                    "EncyclopediaImagePath",
                },
                requestedTargets.Select(field => field.MemberName).ToArray()
            );
        }

        [TestCase("SpecialForce")]
        [TestCase("Building")]
        [TestCase("CapitalShip")]
        [TestCase("Starfighter")]
        public void Render_NonOfficerArtworkWithMessage_UsesDisplayAsGenerationSource(string kind)
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == kind
                    && (
                        kind != "Building"
                        || !ContentEditorUnitArtworkLayout.IsHeadquartersIcon(record)
                    )
                );
            ContentPackEditorField display = unit.GetField("DisplayImagePath");
            ContentPackEditorField message = unit.GetField("MessageImagePath");
            if (string.IsNullOrWhiteSpace(message.Value))
                project.SetField(message, display.Value);
            ContentPackEditorField requestedSource = null;
            view.ArtworkCutsRequested += (source, _) => requestedSource = source;
            SelectRecord(unit);
            RenderSection("Artwork");

            Button action = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "GenerateArtworkCuts");
            action.onClick.Invoke();

            Assert.AreEqual(
                "Generate cuts from display image",
                action.GetComponentInChildren<TMP_Text>().text
            );
            Assert.AreEqual("DisplayImagePath", requestedSource.MemberName);
            Assert.AreSame(display.XmlElement, requestedSource.XmlElement);
        }

        [Test]
        public void Render_UnitArtworkImages_FramesCurrentOutputsWithEffectiveBackground()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "Starfighter" && record.Catalog.Faction == "Alliance"
                );
            string backgroundAddress = unit.GetField("EncyclopediaImagePath").Value;
            SetFactionArtworkBackground("StarfighterBackgroundImagePath", backgroundAddress);
            SelectRecord(unit);
            RenderSection("Artwork");

            RawImage backing = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith(
                        "Output-SmallDisplayImagePath-Backing",
                        StringComparison.Ordinal
                    )
                );
            RawImage background = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith(
                        "Output-SmallDisplayImagePath-Background",
                        StringComparison.Ordinal
                    )
                );

            Assert.AreSame(assets.GetTexture(backgroundAddress), background.texture);
            Assert.AreEqual(
                UILayout.GetSourceRect(backing.rectTransform),
                UILayout.GetSourceRect(background.rectTransform)
            );
        }

        [Test]
        public void Render_OfficerArtwork_AppliesRuntimeBackgroundsToApplicableOutputs()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Officer" && record.Catalog.Faction == "Alliance");
            SetFactionArtworkBackground(
                "OfficerBackgroundImagePath",
                unit.GetField("EncyclopediaImagePath").Value
            );
            string compactBackgroundAddress = unit.GetField("SmallDisplayImagePath").Value;
            SetFactionPersonnelBackground(compactBackgroundAddress);
            SelectRecord(unit);
            RenderSection("Artwork");

            string[] backgrounds = view.GetComponentsInChildren<RawImage>()
                .Where(image => image.name.EndsWith("-Background", StringComparison.Ordinal))
                .Select(image => image.name)
                .ToArray();

            CollectionAssert.Contains(
                backgrounds,
                "ArticleImageTemplate-Output-MessageImagePath-Background"
            );
            CollectionAssert.Contains(
                backgrounds,
                "ArticleImageTemplate-Output-EncyclopediaImagePath-Background"
            );
            CollectionAssert.DoesNotContain(
                backgrounds,
                "ArticleImageTemplate-Output-DisplayImagePath-Background"
            );
            CollectionAssert.Contains(
                backgrounds,
                "ArticleImageTemplate-Output-SmallDisplayImagePath-Background"
            );
            RawImage compactBackground = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name == "ArticleImageTemplate-Output-SmallDisplayImagePath-Background"
                );
            Assert.AreSame(assets.GetTexture(compactBackgroundAddress), compactBackground.texture);
            CollectionAssert.Contains(backgrounds, "ArticleImageTemplate-State-Normal-Background");
            RawImage stateBackground = view.GetComponentsInChildren<RawImage>()
                .Single(image => image.name == "ArticleImageTemplate-State-Normal-Background");
            Assert.AreSame(assets.GetTexture(compactBackgroundAddress), stateBackground.texture);
        }

        [Test]
        public void Render_UnitArtworkBackgroundToggles_ChangeOnlyTheirOwnPreviewSection()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "Starfighter" && record.Catalog.Faction == "Alliance"
                );
            SetFactionArtworkBackground(
                "StarfighterBackgroundImagePath",
                unit.GetField("EncyclopediaImagePath").Value
            );
            SelectRecord(unit);
            RenderSection("Artwork");
            string[] documentText = project.Documents.Select(document => document.Text).ToArray();
            UICheckboxView outputToggle = view.GetComponentsInChildren<UICheckboxView>()
                .Single(toggle => toggle.name == "ImagesBackgroundToggle");
            UICheckboxView stateToggle = view.GetComponentsInChildren<UICheckboxView>()
                .Single(toggle => toggle.name == "GameplayStatesBackgroundToggle");
            Assert.IsTrue(outputToggle.IsChecked);
            Assert.IsTrue(stateToggle.IsChecked);

            outputToggle.GetComponent<Toggle>().isOn = false;
            Canvas.ForceUpdateCanvases();

            Assert.IsFalse(
                view.GetComponentsInChildren<RawImage>()
                    .Any(image =>
                        image.name.Contains("Output-", StringComparison.Ordinal)
                        && image.name.EndsWith("-Background", StringComparison.Ordinal)
                    )
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<RawImage>()
                    .Any(image =>
                        image.name.Contains("State-", StringComparison.Ordinal)
                        && image.name.EndsWith("-Background", StringComparison.Ordinal)
                    )
            );
            stateToggle.GetComponent<Toggle>().isOn = false;
            Canvas.ForceUpdateCanvases();

            Assert.IsFalse(
                view.GetComponentsInChildren<RawImage>()
                    .Any(image =>
                        image.name.Contains("State-", StringComparison.Ordinal)
                        && image.name.EndsWith("-Background", StringComparison.Ordinal)
                    )
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<RawImage>()
                    .Any(image =>
                        image.name.EndsWith(
                            "Output-SmallDisplayImagePath-Entity",
                            StringComparison.Ordinal
                        )
                    )
            );
            CollectionAssert.AreEqual(
                documentText,
                project.Documents.Select(document => document.Text).ToArray()
            );
            Assert.IsNull(unit.Element.Element("UseArtworkBackground"));
        }

        [Test]
        public void Render_ShipStateSize_ShowsOnlyTheSelectedTravelOutput()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "CapitalShip" && record.Catalog.Faction == "Alliance"
                );
            SelectRecord(unit);
            RenderSection("Artwork");
            TMP_Dropdown size = view.GetComponentsInChildren<TMP_Dropdown>()
                .Single(dropdown => dropdown.name == "ArtworkStateSizeDropdown");
            size.value = size.options.FindIndex(option => option.text == "Battle result");

            string[] fields = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Select(field => field.Field.MemberName)
                .ToArray();
            CollectionAssert.Contains(fields, "BattleResultInTransitImagePath");
            CollectionAssert.DoesNotContain(fields, "InTransitSmallImagePath");
            CollectionAssert.DoesNotContain(fields, "InTransitImagePath");
        }

        [Test]
        public void Render_UnitArtworkMissingOutput_ShowsAnEmptyImageSlot()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "CapitalShip" && record.Catalog.Faction == "Alliance"
                );
            Assert.IsTrue(string.IsNullOrWhiteSpace(unit.GetField("MessageImagePath").Value));
            SelectRecord(unit);
            RenderSection("Artwork");

            Assert.IsTrue(
                view.GetComponentsInChildren<RawImage>()
                    .Any(image =>
                        image.name.Contains(
                            "Output-MessageImagePath-Backing",
                            StringComparison.Ordinal
                        )
                    )
            );
            ContentEditorFieldView message = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.Field.MemberName == "MessageImagePath");
            Assert.AreEqual(40, UILayout.GetSourceRect((RectTransform)message.transform).height);
            Assert.AreEqual(
                "Choose image...",
                message
                    .GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.transform.parent.GetComponent<Button>() != null)
                    .text
            );
        }

        [Test]
        public void Render_UnitArtworkMissingOutputs_PlacesActionsBelowTheEmptySlots()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Regiment" && record.Catalog.Faction == "Alliance");
            SelectRecord(unit);
            RenderSection("Artwork");

            RectInt message = UILayout.GetSourceRect(
                (RectTransform)
                    view.GetComponentsInChildren<ContentEditorFieldView>()
                        .Single(field => field.Field.MemberName == "MessageImagePath")
                        .transform
            );
            RectInt small = UILayout.GetSourceRect(
                (RectTransform)
                    view.GetComponentsInChildren<ContentEditorFieldView>()
                        .Single(field => field.Field.MemberName == "SmallDisplayImagePath")
                        .transform
            );

            RawImage[] slots = view.GetComponentsInChildren<RawImage>();
            RectInt messageSlot = UILayout.GetSourceRect(
                slots
                    .Single(image =>
                        image.name.EndsWith(
                            "Output-MessageImagePath-Backing",
                            StringComparison.Ordinal
                        )
                    )
                    .rectTransform
            );
            RectInt smallSlot = UILayout.GetSourceRect(
                slots
                    .Single(image =>
                        image.name.EndsWith(
                            "Output-SmallDisplayImagePath-Backing",
                            StringComparison.Ordinal
                        )
                    )
                    .rectTransform
            );
            Assert.GreaterOrEqual(message.y, messageSlot.yMax);
            Assert.GreaterOrEqual(small.y, smallSlot.yMax);
        }

        [Test]
        public void Render_UnitArtworkBackground_AlignsPreviewAndStatusToLeftEdge()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "Starfighter" && record.Catalog.Faction == "Alliance"
                );
            SetFactionArtworkBackground(
                "StarfighterBackgroundImagePath",
                unit.GetField("EncyclopediaImagePath").Value
            );
            SelectRecord(unit);
            RenderSection("Artwork");

            ContentEditorCardView card = view.GetComponentsInChildren<ContentEditorCardView>()
                .Single(item => item.name.EndsWith("-Background", StringComparison.Ordinal));
            RawImage preview = card.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith(
                        "EffectiveBackground-BackgroundImagePath",
                        StringComparison.Ordinal
                    )
                );
            TMP_Text status = card.GetComponentsInChildren<TMP_Text>()
                .Single(label =>
                    label.text.StartsWith("Using faction default", StringComparison.Ordinal)
                );
            RectInt previewBounds = UILayout.GetSourceRect(preview.rectTransform);
            RectInt statusBounds = UILayout.GetSourceRect(status.rectTransform);
            ContentEditorFieldView field = card.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(item =>
                    item.Field.MemberName == ContentEditorUnitArtworkLayout.BackgroundMember
                );
            RectInt fieldBounds = UILayout.GetSourceRect((RectTransform)field.transform);
            Button action = field.GetComponentsInChildren<Button>().Single();

            Assert.AreEqual(ContentEditorCardView.ContentInset, previewBounds.x);
            Assert.AreEqual(previewBounds.x, statusBounds.x);
            Assert.AreEqual(previewBounds.x, fieldBounds.x);
            Assert.AreEqual(previewBounds.width, fieldBounds.width);
            Assert.GreaterOrEqual(fieldBounds.y, statusBounds.yMax);
            Assert.AreEqual(
                "Override background...",
                action.GetComponentInChildren<TMP_Text>().text
            );
            Assert.IsFalse(
                field
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Single(text => text.name == "SelectedMedia")
                    .gameObject.activeSelf
            );
        }

        [Test]
        public void Render_UnitArtworkImages_AlignsCaptionWithPreviewLeftEdge()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Officer");
            SelectRecord(unit);
            RenderSection("Artwork");

            ContentEditorCardView card = view.GetComponentsInChildren<ContentEditorCardView>()
                .Single(item => item.name.EndsWith("-Images", StringComparison.Ordinal));
            RawImage preview = card.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith("Output-DisplayImagePath-Backing", StringComparison.Ordinal)
                );
            TMP_Text caption = card.GetComponentsInChildren<TMP_Text>()
                .Single(label => label.text == "Display (360 × 360)");

            Assert.AreEqual(
                UILayout.GetSourceRect(preview.rectTransform).x,
                UILayout.GetSourceRect(caption.rectTransform).x
            );
            Assert.Less(
                UILayout.GetSourceRect(caption.rectTransform).y,
                UILayout.GetSourceRect(preview.rectTransform).y
            );
        }

        [Test]
        public void Render_OfficerArtworkImages_PacksDisplayAndSmallDisplayWithoutColumnGap()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Officer");
            SelectRecord(unit);
            RenderSection("Artwork");

            RectInt display = UILayout.GetSourceRect(
                view.GetComponentsInChildren<RawImage>()
                    .Single(image =>
                        image.name.EndsWith(
                            "Output-DisplayImagePath-Backing",
                            StringComparison.Ordinal
                        )
                    )
                    .rectTransform
            );
            RectInt small = UILayout.GetSourceRect(
                view.GetComponentsInChildren<RawImage>()
                    .Single(image =>
                        image.name.EndsWith(
                            "Output-SmallDisplayImagePath-Backing",
                            StringComparison.Ordinal
                        )
                    )
                    .rectTransform
            );

            Assert.AreEqual(display.y, small.y);
            Assert.AreEqual(16, small.x - display.xMax);
        }

        [Test]
        public void Render_OfficerArtworkImages_AlignsActionsWithinEachGalleryRow()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Officer");
            SelectRecord(unit);
            RenderSection("Artwork");

            int displayActionY = UILayout
                .GetSourceRect(
                    (RectTransform)
                        view.GetComponentsInChildren<ContentEditorFieldView>()
                            .Single(field => field.Field.MemberName == "DisplayImagePath")
                            .transform
                )
                .y;
            int smallActionY = UILayout
                .GetSourceRect(
                    (RectTransform)
                        view.GetComponentsInChildren<ContentEditorFieldView>()
                            .Single(field => field.Field.MemberName == "SmallDisplayImagePath")
                            .transform
                )
                .y;

            Assert.AreEqual(displayActionY, smallActionY);
        }

        [Test]
        public void Render_OfficerAudio_UsesCompactRowsForVoiceFileLists()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Officer" && record.Catalog.Faction == "Alliance");
            SelectRecord(unit);
            RenderSection("Audio");

            ContentEditorFieldView[] audio = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Where(field =>
                    field.Field.MediaType == ContentPackEditorMediaType.Audio && field.Field.IsList
                )
                .ToArray();

            Assert.IsNotEmpty(audio);
            Assert.IsTrue(
                audio.All(field =>
                    UILayout.GetSourceRect((RectTransform)field.transform).height <= 64
                )
            );
            Assert.IsTrue(
                audio.All(field =>
                    !field
                        .GetComponentInChildren<TMP_InputField>()
                        .text.Contains("Pack/", StringComparison.Ordinal)
                )
            );
            ContentPackEditorField removable = audio[0].Field;
            Assert.IsNotEmpty(removable.Values);
            audio[0].GetComponentInChildren<TMP_InputField>().onEndEdit.Invoke(string.Empty);
            Assert.IsEmpty(removable.Values);
        }

        [Test]
        public void Render_UnitArtworkImages_UsesCurrentOutputTexture()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "Starfighter" && record.Catalog.Faction == "Alliance"
                );
            string backgroundAddress = unit.GetField("EncyclopediaImagePath").Value;
            string smallAddress = unit.GetField("SmallDisplayImagePath").Value;
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme"
                    && record.Element.Element("FactionInstanceID")?.Value == "FNALL1"
                );
            ContentPackEditorField defaultBackground = ContentEditorContentSections
                .FactionArtworkFields(theme)
                .Single(field => field.MemberName == "StarfighterBackgroundImagePath");
            project.SetField(defaultBackground, backgroundAddress);
            SelectRecord(unit);
            RenderSection("Artwork");

            Texture2D smallTexture = assets.GetTexture(smallAddress);
            RawImage small = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith(
                        "Output-SmallDisplayImagePath-Entity",
                        StringComparison.Ordinal
                    )
                );
            RawImage background = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith(
                        "Output-SmallDisplayImagePath-Background",
                        StringComparison.Ordinal
                    )
                );
            Assert.AreSame(smallTexture, small.texture);
            Assert.AreEqual(new Rect(0, 0, 1, 1), small.uvRect);
            Assert.IsNotNull(background.texture);
            Assert.IsTrue(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text == "Small display (297 × 113)")
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text.StartsWith("Message (", StringComparison.Ordinal))
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text.StartsWith("Display (", StringComparison.Ordinal))
            );
        }

        [Test]
        public void Render_HeadquartersGeneral_ShowsIconAndIdentityFields()
        {
            ContentPackEditorRecord headquarters = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(ContentEditorUnitArtworkLayout.IsHeadquartersIcon);
            SelectRecord(headquarters);
            RenderSection("General");

            CollectionAssert.AreEquivalent(
                new[] { "TypeID", "DisplayName" },
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Select(field => field.Field.MemberName)
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<RawImage>()
                    .Any(image =>
                        image.texture != null
                        && image.name.EndsWith("Headquarters-Entity", StringComparison.Ordinal)
                    )
            );
        }

        [Test]
        public void Render_HeadquartersArtwork_ShowsOnlyCompleteIconUses()
        {
            ContentPackEditorRecord headquarters = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(ContentEditorUnitArtworkLayout.IsHeadquartersIcon);
            ContentPackEditorRecord facility = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "Building"
                    && !ContentEditorUnitArtworkLayout.IsHeadquartersIcon(record)
                );
            project.SetField(
                headquarters.GetField("DisplayImagePath"),
                facility.GetField("DisplayImagePath").Value
            );
            SelectRecord(headquarters);
            RenderSection("Artwork");

            ContentEditorFieldView[] fields =
                view.GetComponentsInChildren<ContentEditorFieldView>();
            CollectionAssert.AreEquivalent(
                new[] { "DisplayImagePath", "SmallDisplayImagePath", "InTransitSmallImagePath" },
                fields.Select(field => field.Field.MemberName).ToArray()
            );
            Assert.IsFalse(
                view.GetComponentsInChildren<Button>()
                    .Any(button => button.name == "GenerateArtworkCuts")
            );
            RawImage entity = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith("Output-DisplayImagePath-Entity", StringComparison.Ordinal)
                );
            RawImage backing = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith("Output-DisplayImagePath-Backing", StringComparison.Ordinal)
                );
            RectInt entityBounds = UILayout.GetSourceRect(entity.rectTransform);
            RectInt backingBounds = UILayout.GetSourceRect(backing.rectTransform);

            Assert.AreEqual(new Rect(0, 0, 1, 1), entity.uvRect);
            Assert.AreEqual(backingBounds.width, entityBounds.width);
            Assert.Less(entityBounds.height, backingBounds.height);
            Assert.AreEqual(
                backingBounds.y + (backingBounds.height - entityBounds.height) / 2,
                entityBounds.y
            );
        }

        [Test]
        public void Render_UnitArtworkWithStates_ShowsCombinedTravelAndDamageLayers()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record =>
                    record.Kind == "Starfighter" && record.Catalog.Faction == "Alliance"
                );
            SelectRecord(unit);
            RenderSection("Artwork");

            RawImage entity = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith("State-Travel-+-damaged-Entity", StringComparison.Ordinal)
                );
            RawImage travel = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith(
                        "State-Travel-+-damaged-Overlay-0",
                        StringComparison.Ordinal
                    )
                );
            RawImage damage = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith(
                        "State-Travel-+-damaged-Overlay-1",
                        StringComparison.Ordinal
                    )
                );

            Assert.IsNotNull(entity.texture);
            Assert.IsNotNull(travel.texture);
            Assert.IsNotNull(damage.texture);
            Assert.Less(entity.transform.GetSiblingIndex(), travel.transform.GetSiblingIndex());
            Assert.Less(travel.transform.GetSiblingIndex(), damage.transform.GetSiblingIndex());
            Assert.IsTrue(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label =>
                        label.text.StartsWith("Travel + damaged", StringComparison.Ordinal)
                    )
            );
        }

        [TestCase(false, "In transit")]
        [TestCase(true, "In transit (Defense)")]
        public void Render_RegimentTransit_ShowsSeparateCardsOnlyForDifferentArtwork(
            bool addOverlay,
            string expectedCaption
        )
        {
            ContentPackEditorRecord troop = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Regiment" && record.Catalog.Faction == "Alliance");
            if (addOverlay)
                project.SetField(
                    troop.GetField("InTransitSmallImagePath"),
                    troop.GetField("DisplayImagePath").Value
                );
            SelectRecord(troop);
            RenderSection("Artwork");

            string[] captions = view.GetComponentsInChildren<TMP_Text>()
                .Select(label => label.text)
                .ToArray();
            CollectionAssert.Contains(captions, expectedCaption);
            Assert.AreEqual(addOverlay, captions.Contains("In transit (Fleet)"));
        }

        [Test]
        public void Render_OfficerArtworkStates_PlacesActionsBelowMatchingPreviews()
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Officer" && record.Catalog.Faction == "Alliance");
            SelectRecord(unit);
            RenderSection("Artwork");

            AssertStateActionAligned("Injured", "InjuredImagePath");
            AssertStateActionAligned("Captured", "CapturedOverlayImagePath");
            RectInt[] stateBounds = new[] { "Normal", "In-transit", "Injured", "Captured" }
                .Select(state =>
                    UILayout.GetSourceRect(
                        view.GetComponentsInChildren<RawImage>()
                            .Single(image =>
                                image.name.EndsWith(
                                    "State-" + state + "-Backing",
                                    StringComparison.Ordinal
                                )
                            )
                            .rectTransform
                    )
                )
                .ToArray();
            Assert.IsTrue(stateBounds.Take(3).All(bounds => bounds.y == stateBounds[0].y));
            Assert.Greater(stateBounds[3].y, stateBounds[0].y);
            Assert.IsFalse(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text == "Additional state images")
            );
        }

        [Test]
        public void Render_FactionArtwork_ListedFieldsHaveMatchingPreviewTargets()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            RenderSection("Interfaces");
            List<string> gaps = new();

            for (int index = 0; index < view.ArtworkViewOptions.Count; index++)
            {
                string category = view.ArtworkViewOptions[index];
                SelectArtworkSurface(category);
                ContentPackEditorField[] represented =
                    view.GetComponentsInChildren<ContentEditorArtworkEditTarget>()
                        .SelectMany(target => target.Fields)
                        .ToArray();
                string[] missing = view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Select(field => field.Field)
                    .Where(ContentEditorContentSections.IsFactionInterfaceField)
                    .Where(field =>
                        !represented.Any(candidate =>
                            candidate.XmlElement == field.XmlElement
                            && candidate.XmlAttributeName == field.XmlAttributeName
                        )
                    )
                    .Select(field => field.Name)
                    .ToArray();
                if (missing.Length > 0)
                {
                    string targets = string.Join(
                        "; ",
                        view.GetComponentsInChildren<ContentEditorArtworkEditTarget>()
                            .Select(target =>
                                target.name
                                + "="
                                + string.Join("|", target.Fields.Select(field => field.Name))
                            )
                    );
                    gaps.Add(
                        category
                            + ": "
                            + string.Join(", ", missing)
                            + Environment.NewLine
                            + "Targets: "
                            + targets
                    );
                }
            }

            Assert.IsEmpty(gaps, string.Join(Environment.NewLine, gaps));
        }

        [Test]
        public void Render_FleetArtworkEveryTarget_DoubleClickOpensItsExactField()
        {
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme" && record.Catalog.Faction == "Alliance"
                );
            theme.Element.Add(
                XElement.Parse(
                    "<ColorScheme><ActiveWindowTitleColorHex>B2342D</ActiveWindowTitleColorHex></ColorScheme>"
                ),
                XElement.Parse(
                    "<WindowTitleTheme>"
                        + "<ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleet-detail-background</ActiveImagePath>"
                        + "</WindowTitleTheme>"
                ),
                XElement.Parse(
                    "<StrategyWindows><Fleet>"
                        + "<DetailBackgroundImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleet-detail-background</DetailBackgroundImagePath>"
                        + "<BannerImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleet-construction-large</BannerImagePath>"
                        + "<Tabs>"
                        + "<CapitalShips><ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-capitalship-selected-icon</ActiveImagePath><InactiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-capitalship-icon</InactiveImagePath></CapitalShips>"
                        + "<Starfighters><ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-starfighter-selected-icon</ActiveImagePath><InactiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-starfighter-icon</InactiveImagePath></Starfighters>"
                        + "<Regiments><ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-regiment-selected-icon</ActiveImagePath><InactiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-regiment-icon</InactiveImagePath></Regiments>"
                        + "<Officers><ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-officer-selected-icon</ActiveImagePath><InactiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-officer-icon</InactiveImagePath></Officers>"
                        + "</Tabs></Fleet></StrategyWindows>"
                )
            );
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            RenderSection("Interfaces");
            SelectArtworkSurface("Fleet window");
            int targetCount = view.GetComponentsInChildren<ContentEditorArtworkEditTarget>().Length;
            Action<string> openSection = RenderSection;
            view.SectionRequested += openSection;

            Assert.AreEqual(
                18,
                targetCount,
                "The Fleet preview must expose every faction-owned or faction-tinted asset."
            );
            try
            {
                for (int index = 0; index < targetCount; index++)
                {
                    RenderSection("Interfaces");
                    SelectArtworkSurface("Fleet window");
                    ContentEditorArtworkEditTarget[] targets =
                        view.GetComponentsInChildren<ContentEditorArtworkEditTarget>();
                    Assert.AreEqual(
                        targetCount,
                        targets.Length,
                        "Fleet target count changed while auditing."
                    );
                    ContentEditorArtworkEditTarget target = targets[index];
                    RawImage image = target.GetComponent<RawImage>();
                    ContentPackEditorField released = target.Field;
                    ContentEditorArtworkWindowPreview preview =
                        target.GetComponentInParent<ContentEditorArtworkWindowPreview>();
                    ContentPackEditorField opened = null;
                    Action<ContentPackEditorField> capture = field => opened = field;

                    Assert.IsNotNull(released, target.name);
                    Assert.IsNotNull(preview, target.name);
                    Assert.IsTrue(
                        image.raycastTarget,
                        target.name + " must receive real pointer input."
                    );
                    if (
                        released.MediaType == ContentPackEditorMediaType.Image
                        && !string.IsNullOrWhiteSpace(released.Value)
                    )
                    {
                        Texture2D expectedTexture = assets.GetTexture(
                            released.GetMediaAddress(released.Value)
                        );
                        Assert.IsTrue(
                            image.texture == expectedTexture
                                || image.texture?.name == expectedTexture.name + "-tinted",
                            target.name
                                + " renders "
                                + image.texture?.name
                                + " but opens "
                                + released.Name
                                + "."
                        );
                    }
                    Assert.IsTrue(
                        target.GetComponents<Selectable>().All(selectable => !selectable.enabled),
                        target.name + " must not execute copied gameplay actions."
                    );
                    target.SetHovered(true);
                    ContentPackEditorField expected = target.Field;
                    GameObject frame = target
                        .transform.Find("ContentEditorArtworkHoverFrame")
                        ?.gameObject;
                    Assert.IsNotNull(frame, target.name);
                    Assert.IsTrue(frame.activeSelf, target.name);
                    Assert.IsTrue(
                        frame.GetComponentsInChildren<Image>().All(border => !border.raycastTarget),
                        target.name
                    );
                    preview.ArtworkOpened += capture;
                    try
                    {
                        target.OnPointerClick(
                            new PointerEventData(EventSystem.current)
                            {
                                button = PointerEventData.InputButton.Left,
                                clickCount = 2,
                            }
                        );
                    }
                    finally
                    {
                        preview.ArtworkOpened -= capture;
                    }

                    Assert.AreSame(expected, opened, target.name);
                    Canvas.ForceUpdateCanvases();
                    ScrollAreaLayout fields = view.GetComponentsInChildren<ScrollAreaLayout>(true)
                        .Single();
                    if (expected.Group == nameof(FactionTheme.ColorScheme))
                    {
                        ContentEditorColorPickerView picker =
                            view.GetComponentsInChildren<ContentEditorColorPickerView>()
                                .Single(item =>
                                    item.Field.XmlElement == expected.XmlElement
                                    && item.Field.XmlAttributeName == expected.XmlAttributeName
                                );
                        Assert.AreEqual(
                            -((RectTransform)picker.transform).anchoredPosition.y,
                            fields.ScrollArea.ContentRoot.anchoredPosition.y,
                            0.01f,
                            target.name + " must align its color card with the viewport top."
                        );
                    }
                    else
                    {
                        ContentEditorFieldView fieldView =
                            view.GetComponentsInChildren<ContentEditorFieldView>()
                                .Single(item =>
                                    item.Field.XmlElement == expected.XmlElement
                                    && item.Field.XmlAttributeName == expected.XmlAttributeName
                                );
                        float fieldTop = ((RectTransform)fieldView.transform).anchoredPosition.y;
                        float captionTop = view.GetComponentsInChildren<TMP_Text>()
                            .Where(label =>
                                label.text == expected.Caption
                                && label.transform.parent == fields.ScrollArea.ContentRoot
                                && label.rectTransform.anchoredPosition.y >= fieldTop
                            )
                            .Min(label => label.rectTransform.anchoredPosition.y);
                        Assert.AreEqual(
                            -captionTop,
                            fields.ScrollArea.ContentRoot.anchoredPosition.y,
                            0.01f,
                            target.name + " must align its image caption with the viewport top."
                        );
                    }
                }
            }
            finally
            {
                view.SectionRequested -= openSection;
            }
        }

        [Test]
        public void Render_FleetSharedSelectionMasks_OpensFactionSelectionColor()
        {
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme" && record.Catalog.Faction == "Alliance"
                );
            XElement unitTileIcons = theme
                .Element.Element(nameof(FactionTheme.PlanetOverlayTheme))
                .Element(nameof(PlanetOverlayTheme.UnitTileIcons));
            unitTileIcons.Element(nameof(UnitTileIcons.FleetListSelectionImagePath)).Value =
                "Pack/Shared/Strategy/UI/ThemeMasks/selection-fleet-list";
            unitTileIcons.Element(nameof(UnitTileIcons.FleetDetailSelectionImagePath)).Value =
                "Pack/Shared/Strategy/UI/ThemeMasks/selection-fleet-detail";
            XElement colors = theme.Element.Element(nameof(FactionTheme.ColorScheme));
            if (colors == null)
            {
                colors = new XElement(nameof(FactionTheme.ColorScheme));
                theme.Element.Add(colors);
            }
            if (colors.Element(nameof(FactionColorScheme.SelectionColorHex)) == null)
                colors.Add(new XElement(nameof(FactionColorScheme.SelectionColorHex), "B2342D"));
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            RenderSection("Interfaces");
            SelectArtworkSurface("Fleet window");
            FleetWindowView fleet = view.GetComponentInChildren<FleetWindowView>();
            RawImage listSelection = fleet
                .GetComponentsInChildren<FleetListRowView>()
                .Single(row => row.NameTextField.text == "First fleet")
                .GetComponentsInChildren<RawImage>()
                .Single(image => image.name == "SelectionImage");
            RawImage detailSelection = fleet
                .GetComponentsInChildren<StrategyUnitCardView>()
                .First(card => card.gameObject.activeSelf && card.name != "ItemTemplate")
                .GetComponentsInChildren<RawImage>()
                .Single(image => image.name == "SelectionImage");

            foreach (RawImage image in new[] { listSelection, detailSelection })
            {
                ContentEditorArtworkEditTarget target =
                    image.GetComponent<ContentEditorArtworkEditTarget>();
                Assert.IsNotNull(target, image.name);
                Assert.AreEqual(
                    nameof(FactionColorScheme.SelectionColorHex),
                    target.Field.MemberName,
                    image.name
                );
                Assert.AreEqual(nameof(FactionTheme.ColorScheme), target.Field.Group, image.name);
            }
        }

        [Test]
        public void Render_FactionArtworkDisabledState_ShowsDefaultAndDisabledViewsSideBySide()
        {
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme" && record.Catalog.Faction == "Alliance"
                );
            theme.Element.Add(
                XElement.Parse(
                    "<StrategyWindows><Defense><PersonnelTab>"
                        + "<ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Defense/defense-window-tab-personnel-active-tab</ActiveImagePath>"
                        + "<InactiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Defense/defense-tab</InactiveImagePath>"
                        + "<DisabledImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Defense/defense-window-tab-personnel-disabled-tab</DisabledImagePath>"
                        + "</PersonnelTab></Defense></StrategyWindows>"
                )
            );
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            RenderSection("Interfaces");
            SelectArtworkSurface("Defense window");
            DefenseWindowView[] windows = view.GetComponentsInChildren<DefenseWindowView>();
            RawImage personnelTab = windows
                .Select(window =>
                    window
                        .GetComponentsInChildren<RawImage>(true)
                        .Single(image => image.name == "PersonnelTabButtonImage")
                )
                .Single(image =>
                    image.GetComponent<ContentEditorArtworkEditTarget>()?.Field.MemberName
                    == "DisabledImagePath"
                );
            ContentEditorArtworkEditTarget target =
                personnelTab.GetComponent<ContentEditorArtworkEditTarget>();
            Texture disabledTexture = personnelTab.texture;

            Assert.AreEqual(2, windows.Length);
            Assert.IsFalse(
                view.GetComponentsInChildren<Button>(true)
                    .Any(button => button.name.StartsWith("ArtworkState", StringComparison.Ordinal))
            );
            Assert.IsNotNull(target);
            Assert.AreEqual("DisabledImagePath", target.Field.MemberName);
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Any(field =>
                        field.Field.XmlElement == target.Field.XmlElement
                        && field.gameObject.activeSelf
                    )
            );
            target.SetHovered(true);
            Assert.AreSame(disabledTexture, personnelTab.texture);
        }

        [TestCase("Alliance")]
        [TestCase("Empire")]
        public void Render_InstalledFactionDisabledPreviews_OnlyShowFormsWithVisibleDisabledArtwork(
            string faction
        )
        {
            project = ContentPackEditorStorage.Open(
                TestContent.Pack.ContentRootPath,
                TestContent.Pack.Definition.ID
            );
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/" + faction));
            RenderSection("Interfaces");
            ContentPackEditorRecord theme = selectedPage.RelatedRecords.Single(record =>
                record.Kind == "FactionTheme"
            );
            string[] configuredCategories = theme
                .GetFields()
                .Where(field => field.MemberName == "DisabledImagePath")
                .Select(ContentEditorContentSections.FactionArtworkCategory)
                .Distinct()
                .OrderBy(category => category, StringComparer.Ordinal)
                .ToArray();
            List<string> visibleDisabledCategories = new();

            foreach (string category in configuredCategories)
            {
                SelectArtworkSurface(category);
                ContentEditorArtworkWindowPreview disabled =
                    view.GetComponentsInChildren<ContentEditorArtworkWindowPreview>(true)
                        .Single(preview => preview.name == "ArtworkDisabledWindowPreview");
                if (!disabled.gameObject.activeInHierarchy)
                    continue;
                visibleDisabledCategories.Add(category);
                Assert.IsTrue(
                    disabled.RenderedFields.Any(field => field.MemberName == "DisabledImagePath"),
                    category
                );
                Assert.IsTrue(
                    disabled
                        .GetComponentsInChildren<ContentEditorArtworkEditTarget>()
                        .SelectMany(target => target.Fields)
                        .Any(field => field.MemberName == "DisabledImagePath"),
                    category
                );
            }

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "Combat",
                    "Defense window",
                    "Facilities and construction",
                    "Messages",
                    "Missions",
                },
                configuredCategories
            );
            CollectionAssert.AreEquivalent(
                new[] { "Combat", "Defense window", "Facilities and construction", "Messages" },
                visibleDisabledCategories
            );
        }

        [Test]
        public void Render_FactionArtworkDisabledStateMatchingDefault_DoesNotDuplicateTheWindow()
        {
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme" && record.Catalog.Faction == "Alliance"
                );
            theme.Element.Add(
                XElement.Parse(
                    "<StrategyWindows><Defense><PersonnelTab>"
                        + "<ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Defense/defense-window-tab-personnel-active-tab</ActiveImagePath>"
                        + "<InactiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Defense/defense-tab</InactiveImagePath>"
                        + "<DisabledImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Defense/defense-window-tab-personnel-active-tab</DisabledImagePath>"
                        + "</PersonnelTab></Defense></StrategyWindows>"
                )
            );
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            RenderSection("Interfaces");
            SelectArtworkSurface("Defense window");

            ContentEditorArtworkWindowPreview disabled =
                view.GetComponentsInChildren<ContentEditorArtworkWindowPreview>(true)
                    .Single(preview => preview.name == "ArtworkDisabledWindowPreview");

            Assert.IsFalse(disabled.gameObject.activeInHierarchy);
            Assert.AreEqual(1, view.GetComponentsInChildren<DefenseWindowView>().Length);
            Assert.IsFalse(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text is "Default" or "Disabled")
            );
        }

        [Test]
        public void Render_FactionArtworkUnrepresentedDisabledState_DoesNotDuplicateTheWindow()
        {
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme" && record.Catalog.Faction == "Alliance"
                );
            theme.Element.Add(
                XElement.Parse(
                    "<PlanetWindowTheme><MissionsPane><MissionTabs><SecondaryParticipants>"
                        + "<NormalImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Missions/planet-mission-normal</NormalImagePath>"
                        + "<SelectedImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Missions/planet-mission-highlighted</SelectedImagePath>"
                        + "<DisabledImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Missions/planet-mission-normal</DisabledImagePath>"
                        + "</SecondaryParticipants></MissionTabs></MissionsPane></PlanetWindowTheme>"
                )
            );
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            RenderSection("Interfaces");
            SelectArtworkSurface("Missions");

            ContentEditorArtworkWindowPreview disabled =
                view.GetComponentsInChildren<ContentEditorArtworkWindowPreview>(true)
                    .Single(preview => preview.name == "ArtworkDisabledWindowPreview");
            Assert.IsFalse(disabled.gameObject.activeInHierarchy);
            Assert.AreEqual(2, view.GetComponentsInChildren<MissionsWindowView>(true).Length);
            Assert.IsFalse(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text is "Default" or "Disabled")
            );
        }

        [Test]
        public void Render_FactionTintedMasks_DoubleClickOpensTheDedicatedColorCard()
        {
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme" && record.Catalog.Faction == "Alliance"
                );
            theme.Element.Add(
                XElement.Parse(
                    "<ColorScheme>"
                        + "<SelectionColorHex>FF0000</SelectionColorHex>"
                        + "<ActiveWindowTitleColorHex>FF0000</ActiveWindowTitleColorHex>"
                        + "</ColorScheme>"
                ),
                XElement.Parse(
                    "<WindowTitleTheme>"
                        + "<ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleet-detail-background</ActiveImagePath>"
                        + "</WindowTitleTheme>"
                ),
                XElement.Parse(
                    "<StrategyWindows><Defense>"
                        + "<PersonnelTab>"
                        + "<ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Defense/defense-window-tab-personnel-active-tab</ActiveImagePath>"
                        + "<InactiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Defense/defense-tab</InactiveImagePath>"
                        + "</PersonnelTab>"
                        + "<SelectionImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleet-selection</SelectionImagePath>"
                        + "<PersonnelBackgroundImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleet-detail-background</PersonnelBackgroundImagePath>"
                        + "</Defense></StrategyWindows>"
                )
            );
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            RenderSection("Interfaces");
            SelectArtworkSurface("Defense window");

            ContentEditorArtworkEditTarget[] targets =
                view.GetComponentsInChildren<ContentEditorArtworkEditTarget>()
                    .Where(target => target.Field.Group == nameof(FactionTheme.ColorScheme))
                    .ToArray();
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    nameof(FactionColorScheme.SelectionColorHex),
                    nameof(FactionColorScheme.ActiveWindowTitleColorHex),
                },
                targets.Select(target => target.Field.MemberName)
            );

            string requested = null;
            Action<string> openSection = value =>
            {
                requested = value;
                RenderSection(value);
            };
            view.SectionRequested += openSection;
            try
            {
                ContentEditorArtworkEditTarget title = targets.Single(target =>
                    target.Field.MemberName == nameof(FactionColorScheme.ActiveWindowTitleColorHex)
                );
                title.OnPointerClick(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                        clickCount = 2,
                    }
                );
            }
            finally
            {
                view.SectionRequested -= openSection;
            }

            Assert.AreEqual("Colors", requested);
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorColorPickerView>()
                    .Any(picker =>
                        picker.Field.MemberName
                        == nameof(FactionColorScheme.ActiveWindowTitleColorHex)
                    )
            );
        }

        [Test]
        public void Render_FactionColors_ProvidesColorWheelAndBrightnessCommitRelay()
        {
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme" && record.Catalog.Faction == "Alliance"
                );
            theme.Element.Add(
                XElement.Parse(
                    "<ColorScheme>"
                        + "<SelectionColorHex>FF0000</SelectionColorHex>"
                        + "<ActiveWindowTitleColorHex>00FF00</ActiveWindowTitleColorHex>"
                        + "</ColorScheme>"
                )
            );
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));

            RenderSection("Colors");

            ContentEditorColorPickerView[] pickers =
                view.GetComponentsInChildren<ContentEditorColorPickerView>();
            Assert.IsNotEmpty(pickers);
            foreach (ContentEditorColorPickerView picker in pickers)
            {
                ContentEditorColorWheel wheel =
                    picker.GetComponentInChildren<ContentEditorColorWheel>(true);
                Assert.IsNotNull(wheel, picker.Field.MemberName);
                Assert.IsNotNull(wheel.GetComponent<RawImage>().texture, picker.Field.MemberName);
                Assert.IsNotNull(wheel.transform.Find("Selector"), picker.Field.MemberName);
                Slider[] sliders = picker.GetComponentsInChildren<Slider>(true);
                Assert.AreEqual(1, sliders.Length, picker.Field.MemberName);
                Assert.IsNotNull(
                    sliders[0].GetComponent<ContentEditorSliderCommit>(),
                    picker.Field.MemberName
                );
            }
        }

        [Test]
        public void Render_AfterFactionColors_HidesEveryColorPickerFromOtherSections()
        {
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme" && record.Catalog.Faction == "Alliance"
                );
            theme.Element.Add(
                XElement.Parse(
                    "<ColorScheme>"
                        + "<SelectionColorHex>FF0000</SelectionColorHex>"
                        + "<ActiveWindowTitleColorHex>00FF00</ActiveWindowTitleColorHex>"
                        + "</ColorScheme>"
                )
            );
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            RenderSection("Colors");
            Assert.IsNotEmpty(view.GetComponentsInChildren<ContentEditorColorPickerView>());

            RenderSection("General");

            Assert.IsEmpty(view.GetComponentsInChildren<ContentEditorColorPickerView>());
        }

        [Test]
        public void Render_FactionArtworkEveryAvailableSurface_RendersWithoutPresentationErrors()
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            RenderSection("Interfaces");
            string[] options = view.ArtworkViewOptions.ToArray();

            foreach (string option in options)
            {
                Assert.DoesNotThrow(
                    () => SelectArtworkSurface(option),
                    "Failed surface: " + option
                );
                Assert.IsFalse(
                    view.GetComponentsInChildren<TMP_Text>()
                        .Any(label =>
                            label.text
                            == "The gameplay-window preview contains an image path that could not be loaded."
                        ),
                    option
                );
            }
        }

        [Test]
        public void Render_FactionArtwork_UsesSurfaceDropdownAndExactWindowPreviews()
        {
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme" && record.Catalog.Faction == "Alliance"
                );
            theme.Element.Add(
                XElement.Parse(
                    "<StrategyWindows>"
                        + "<Defense><PersonnelTab>"
                        + "<ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Defense/defense-icon</ActiveImagePath>"
                        + "<InactiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Defense/planetsector-planet-defense-pressed</InactiveImagePath>"
                        + "</PersonnelTab></Defense>"
                        + "<Facility><ControlTab>"
                        + "<ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Facility/facility-tab</ActiveImagePath>"
                        + "</ControlTab></Facility>"
                        + "<Fleet>"
                        + "<DetailBackgroundImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleet-detail-background</DetailBackgroundImagePath>"
                        + "<BannerImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleet-construction-large</BannerImagePath>"
                        + "<Tabs>"
                        + "<CapitalShips><ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-capitalship-selected-icon</ActiveImagePath><InactiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-capitalship-icon</InactiveImagePath></CapitalShips>"
                        + "<Starfighters><ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-starfighter-selected-icon</ActiveImagePath><InactiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-starfighter-icon</InactiveImagePath></Starfighters>"
                        + "<Regiments><ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-regiment-selected-icon</ActiveImagePath><InactiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-regiment-icon</InactiveImagePath></Regiments>"
                        + "<Officers><ActiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-officer-selected-icon</ActiveImagePath><InactiveImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/fleetspane-officer-icon</InactiveImagePath></Officers>"
                        + "</Tabs></Fleet>"
                        + "<Encyclopedia><AllDatabasesButton>"
                        + "<UpImagePath>Pack/Shared/Strategy/UI/Windows/Encyclopedia/encyclopedia-window-all-systems-button-up</UpImagePath>"
                        + "<DownImagePath>Pack/Shared/Strategy/UI/Windows/Encyclopedia/encyclopedia-window-all-systems-button-pressed</DownImagePath>"
                        + "</AllDatabasesButton><FacilityButton>"
                        + "<UpImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Encyclopedia/encyclopedia-window-facility-button-up</UpImagePath>"
                        + "<DownImagePath>Pack/Factions/Alliance/Strategy/UI/Windows/Encyclopedia/encyclopedia-window-facility-button-pressed</DownImagePath>"
                        + "</FacilityButton></Encyclopedia>"
                        + "</StrategyWindows>"
                ),
                XElement.Parse(
                    "<TacticalHUDLayout>"
                        + "<ImagePath>Pack/Factions/Alliance/Strategy/UI/Hud/background</ImagePath>"
                        + "<SpeedIndicatorSourceLayout><X>167</X><Y>20</Y><Width>16</Width><Height>10</Height></SpeedIndicatorSourceLayout>"
                        + "<SpeedContextSourceLayout><X>106</X><Y>19</Y><Width>80</Width><Height>11</Height></SpeedContextSourceLayout>"
                        + "<SpeedIndicators><MediumImagePath>Pack/Factions/Alliance/Strategy/UI/Hud/speed-medium</MediumImagePath></SpeedIndicators>"
                        + "<Buttons><StrategyHudButtonTheme>"
                        + "<Action>Options</Action>"
                        + "<UpImagePath>Pack/Factions/Alliance/Strategy/UI/Hud/main-button-options-up</UpImagePath>"
                        + "<PressedImagePath>Pack/Factions/Alliance/Strategy/UI/Hud/main-button-options-pressed</PressedImagePath>"
                        + "<PressedImageLayout><X>3</X><Y>355</Y><Width>27</Width><Height>41</Height></PressedImageLayout>"
                        + "<HitArea><X>3</X><Y>355</Y><Width>35</Width><Height>37</Height></HitArea>"
                        + "</StrategyHudButtonTheme></Buttons></TacticalHUDLayout>"
                )
            );
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));

            RenderSection("Interfaces");

            RectTransform artworkSurface = view.GetComponentsInChildren<RectTransform>()
                .Single(rect => rect.name == "ArtworkViewSelector");
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "Planet sector",
                    "Strategy view",
                    "Defense window",
                    "Fleet window",
                    "Facilities and construction",
                },
                view.ArtworkViewOptions.ToArray()
            );
            Assert.AreEqual("Planet sector", view.ArtworkViewSelection);
            RectInt selectorRect = UILayout.GetSourceRect((RectTransform)artworkSurface.transform);
            float selectorViewportWidth = artworkSurface
                .GetComponentInParent<ScrollAreaView>()
                .ViewportWidth;
            Assert.AreEqual(
                selectorViewportWidth * 0.5f,
                selectorRect.center.x,
                1f,
                "The selected surface should be centered instead of reading like a field label."
            );
            Assert.IsFalse(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text.StartsWith("Planet sector ("))
            );
            Assert.AreEqual(2, artworkSurface.GetComponentsInChildren<Button>().Length);
            ContentEditorSectorPreview sectorPreview =
                view.GetComponentInChildren<ContentEditorSectorPreview>();
            Assert.IsNotNull(sectorPreview);
            RectInt previewRect = UILayout.GetSourceRect((RectTransform)sectorPreview.transform);
            Assert.AreEqual(520, previewRect.width);
            Assert.Greater(previewRect.height, 700);
            FactionTheme factionTheme = ContentEditorPresentation
                .GetFactionThemes(project)
                .Single(theme => theme.Name == "Alliance")
                .Theme;
            Texture2D fleetOverlay = assets.GetSmoothTexture(
                factionTheme.PlanetOverlayTheme.PlanetOverlayIcons.Fleets.NormalImagePath
            );
            Texture2D fleetHoverOverlay = assets.GetTexture(
                factionTheme.PlanetOverlayTheme.PlanetOverlayIcons.Fleets.HoverImagePath
            );
            Assert.AreSame(
                assets.GetTexture(factionTheme.GalaxyBackground.ImagePath),
                sectorPreview.GetComponent<RawImage>().texture
            );
            Assert.IsFalse(
                sectorPreview
                    .GetComponentsInChildren<ContentEditorArtworkEditTarget>()
                    .Any(target =>
                        target.Field?.Value == factionTheme.GalaxyBackground.DestroyedPlanetIconPath
                    ),
                "Shared planet artwork must not masquerade as a faction-owned edit target."
            );
            Assert.AreSame(
                fleetOverlay,
                sectorPreview
                    .GetComponentsInChildren<RawImage>()
                    .First(image => image.name == "FleetImage")
                    .texture
            );
            RawImage fleetImage = sectorPreview
                .GetComponentsInChildren<RawImage>()
                .First(image => image.name == "FleetImage");
            ContentEditorArtworkEditTarget fleetTarget =
                fleetImage.GetComponent<ContentEditorArtworkEditTarget>();
            Assert.IsNotNull(fleetTarget);
            Assert.IsTrue(fleetImage.raycastTarget);
            Assert.AreEqual("NormalImagePath", fleetTarget.Field.MemberName);
            Assert.IsTrue(
                fleetTarget.Field.XmlElement.Ancestors().Any(element => element.Name == "Fleets")
            );
            GameObject fleetFrame = fleetTarget
                .transform.Find("ContentEditorArtworkHoverFrame")
                .gameObject;
            Assert.IsFalse(fleetFrame.activeSelf);
            PointerEventData fleetPointer = new(null) { pointerEnter = fleetImage.gameObject };
            fleetTarget.OnPointerEnter(fleetPointer);
            Assert.IsTrue(fleetFrame.activeSelf);
            Assert.AreSame(fleetHoverOverlay, fleetImage.texture);
            Assert.AreEqual("HoverImagePath", fleetTarget.Field.MemberName);
            RectInt fleetContentBounds = assets.GetTextureContentBounds(fleetHoverOverlay);
            RectTransform fleetFrameRect = (RectTransform)fleetFrame.transform;
            Assert.AreEqual(
                (float)fleetContentBounds.xMin / fleetHoverOverlay.width,
                fleetFrameRect.anchorMin.x,
                0.001f
            );
            Assert.AreEqual(
                (float)fleetContentBounds.yMin / fleetHoverOverlay.height,
                fleetFrameRect.anchorMin.y,
                0.001f
            );
            Assert.AreEqual(
                (float)fleetContentBounds.xMax / fleetHoverOverlay.width,
                fleetFrameRect.anchorMax.x,
                0.001f
            );
            Assert.AreEqual(
                (float)fleetContentBounds.yMax / fleetHoverOverlay.height,
                fleetFrameRect.anchorMax.y,
                0.001f
            );
            Assert.Less(
                (fleetFrameRect.anchorMax.x - fleetFrameRect.anchorMin.x)
                    * (fleetFrameRect.anchorMax.y - fleetFrameRect.anchorMin.y),
                1f
            );
            Assert.IsNull(fleetFrame.GetComponent<Graphic>());
            Assert.AreEqual(4, fleetFrame.GetComponentsInChildren<Image>().Length);
            Assert.IsTrue(
                fleetFrame.GetComponentsInChildren<Image>().All(image => !image.raycastTarget)
            );
            fleetTarget.OnPointerExit(new PointerEventData(null));
            Assert.IsFalse(fleetFrame.activeSelf);
            fleetTarget.OnPointerClick(
                new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Left,
                    clickCount = 2,
                }
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<RawImage>()
                    .Any(image =>
                        image.name.StartsWith("ArticleImageTemplate", StringComparison.Ordinal)
                        && image.texture != null
                    )
            );
            Assert.IsFalse(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text.StartsWith("Current: ", StringComparison.Ordinal))
            );

            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Any(field =>
                        field.Field.Value.EndsWith(
                            "planet-mission-highlighted",
                            StringComparison.Ordinal
                        )
                    )
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Any(field =>
                        field.Field.Value.EndsWith(
                            "planet-garrison-highlighted",
                            StringComparison.Ordinal
                        )
                    )
            );

            string[] visibleMembers = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Select(field => field.Field.MemberName)
                .ToArray();
            CollectionAssert.DoesNotContain(visibleMembers, "FleetListIconImagePath");
            CollectionAssert.DoesNotContain(visibleMembers, "OfficerBackgroundImagePath");
            CollectionAssert.DoesNotContain(visibleMembers, "CapitalShipBackgroundImagePath");
            CollectionAssert.DoesNotContain(visibleMembers, "AnimationImageRoot");

            SelectArtworkSurface("Fleet window");
            ContentEditorFieldView fleetIcon =
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Single(field => field.Field.MemberName == "FleetListIconImagePath");
            Assert.AreEqual("Fleet list icon image (297 × 113)", fleetIcon.Field.Caption);
            Assert.IsNull(view.GetComponentInChildren<ContentEditorSectorPreview>());
            FleetWindowView fleetWindow = view.GetComponentInChildren<FleetWindowView>();
            Assert.IsNotNull(fleetWindow);
            GameObject contentEditorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/ContentPackEditor/ContentPackEditorRoot.prefab"
            );
            FleetWindowView[] nestedFleetWindows = contentEditorPrefab
                .GetComponentsInChildren<FleetWindowView>(true)
                .ToArray();
            Assert.AreEqual(2, nestedFleetWindows.Length);
            foreach (FleetWindowView nestedFleetWindow in nestedFleetWindows)
            {
                GameObject fleetPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(
                    nestedFleetWindow.gameObject
                );
                Assert.IsNotNull(fleetPrefab);
                Assert.AreEqual(
                    "Assets/Prefabs/UI/StrategyView/FleetWindow.prefab",
                    AssetDatabase.GetAssetPath(fleetPrefab)
                );
            }
            Assert.That(fleetWindow.transform.localScale.x, Is.InRange(1f, 3f));
            var fleetSurface =
                fleetWindow.GetComponentInParent<ContentEditorArtworkWindowPreview>();
            RectInt fleetSurfaceRect = UILayout.GetSourceRect(
                (RectTransform)fleetSurface.transform
            );
            Assert.LessOrEqual(
                fleetSurfaceRect.yMax,
                fleetSurface.GetComponentInParent<ScrollAreaView>().ViewportHeight + 1f
            );
            Assert.AreSame(
                assets.GetTexture(factionTheme.StrategyWindows.Fleet.DetailBackgroundImagePath),
                fleetWindow
                    .GetComponentsInChildren<RawImage>()
                    .Single(image => image.name == "DetailBackgroundImage")
                    .texture
            );
            Assert.IsTrue(
                fleetWindow
                    .GetComponentsInChildren<FleetListRowView>()
                    .Any(row => row.gameObject.activeSelf && row.name != "FleetRowTemplate")
            );
            string[] expectedFleetRows = new[] { "First fleet" }
                .Concat(
                    project
                        .GetCatalogs()
                        .Where(catalog => catalog.Faction == "Alliance")
                        .SelectMany(catalog => catalog.Records)
                        .Where(record => record.Kind == "CapitalShip")
                        .OrderBy(record => record.Name, StringComparer.Ordinal)
                        .Take(2)
                        .Select(record => record.Name)
                )
                .Concat(new[] { "Fleet in transit", "Damaged fleet" })
                .ToArray();
            CollectionAssert.AreEqual(
                expectedFleetRows,
                fleetWindow
                    .GetComponentsInChildren<FleetListRowView>()
                    .Where(row => row.gameObject.activeSelf && row.name != "FleetRowTemplate")
                    .OrderBy(row => row.Index)
                    .Select(row => row.NameTextField.text)
                    .ToArray(),
                "Expanded ships must remain directly below their parent fleet instead of appearing beneath unrelated fleet rows."
            );
            Assert.IsTrue(
                fleetWindow
                    .GetComponentsInChildren<StrategyUnitCardView>()
                    .Any(card => card.gameObject.activeSelf && card.name != "ItemTemplate")
            );
            ContentEditorArtworkWindowPreview fleetPreview =
                fleetWindow.GetComponentInParent<ContentEditorArtworkWindowPreview>();
            foreach (
                (string rowName, string imageName, string memberName) in new[]
                {
                    ("Fleet in transit", "EnrouteOverlayImage", "FleetListEnrouteIconImagePath"),
                    ("Damaged fleet", "DamagedOverlayImage", "FleetListDamagedIconImagePath"),
                }
            )
            {
                FleetListRowView row = fleetWindow
                    .GetComponentsInChildren<FleetListRowView>()
                    .Single(candidate => candidate.NameTextField.text == rowName);
                RawImage stateImage = row.GetComponentsInChildren<RawImage>()
                    .Single(image => image.name == imageName);
                ContentEditorArtworkEditTarget stateTarget =
                    stateImage.GetComponent<ContentEditorArtworkEditTarget>();
                Assert.IsNotNull(stateTarget, rowName);
                Assert.AreEqual(memberName, stateTarget.Field.MemberName, rowName);
                ContentPackEditorField opened = null;
                fleetPreview.ArtworkOpened += field => opened = field;
                stateTarget.OnPointerClick(
                    new PointerEventData(null)
                    {
                        button = PointerEventData.InputButton.Left,
                        clickCount = 2,
                    }
                );
                Assert.AreSame(stateTarget.Field, opened, rowName);
            }
            ContentPackEditorRecord previewShip = project
                .GetCatalogs()
                .Where(catalog => catalog.Faction == "Alliance")
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind == "CapitalShip")
                .OrderBy(record => record.Name, StringComparer.Ordinal)
                .First();
            Texture2D previewShipTexture = assets.GetTexture(
                previewShip.Element.Element("SmallDisplayImagePath").Value
            );
            Assert.AreSame(
                previewShipTexture,
                fleetWindow
                    .GetComponentsInChildren<StrategyUnitCardView>()
                    .First(card => card.gameObject.activeSelf && card.name != "ItemTemplate")
                    .GetComponentsInChildren<RawImage>()
                    .Single(image => image.name == "EntityImage")
                    .texture
            );
            RawImage detailBackgroundImage = fleetWindow
                .GetComponentsInChildren<RawImage>()
                .Single(image => image.name == "DetailBackgroundImage");
            ContentEditorArtworkEditTarget editTarget =
                detailBackgroundImage.GetComponent<ContentEditorArtworkEditTarget>();
            string targetNames = string.Join(
                ",",
                fleetWindow
                    .GetComponentsInChildren<ContentEditorArtworkEditTarget>()
                    .Select(target => target.name)
            );
            Assert.IsNotNull(
                editTarget,
                $"enabled={detailBackgroundImage.enabled}; active={detailBackgroundImage.gameObject.activeInHierarchy}; binding={detailBackgroundImage.GetComponent<ContentEditorThemeTextureBinding>()?.MemberPath}; targets={targetNames}"
            );
            Assert.AreEqual("DetailBackgroundImagePath", editTarget.Field.MemberName);
            AssertArtworkTarget(
                fleetWindow
                    .GetComponentsInChildren<RawImage>()
                    .Single(image => image.name == "BannerImage"),
                "BannerImagePath",
                "Fleet"
            );
            FleetListRowView firstFleetRow = fleetWindow
                .GetComponentsInChildren<FleetListRowView>()
                .Single(row => row.NameTextField.text == "First fleet");
            AssertArtworkTarget(
                firstFleetRow
                    .GetComponentsInChildren<RawImage>()
                    .Single(image => image.name == "IconImage"),
                "FleetListIconImagePath",
                "UnitTileIcons"
            );
            AssertArtworkTarget(
                firstFleetRow
                    .GetComponentsInChildren<RawImage>()
                    .Single(image => image.name == "SelectionImage"),
                "FleetListSelectionImagePath",
                "UnitTileIcons"
            );
            AssertArtworkTarget(
                firstFleetRow
                    .GetComponentsInChildren<RawImage>()
                    .Single(image => image.name == "StarfighterBadgeImage"),
                "FleetStarfightersBadgeImagePath",
                "UnitTileIcons"
            );
            AssertArtworkTarget(
                firstFleetRow
                    .GetComponentsInChildren<RawImage>()
                    .Single(image => image.name == "TroopBadgeImage"),
                "FleetTroopsBadgeImagePath",
                "UnitTileIcons"
            );
            AssertArtworkTarget(
                firstFleetRow
                    .GetComponentsInChildren<RawImage>()
                    .Single(image => image.name == "PersonnelBadgeImage"),
                "FleetPersonnelBadgeImagePath",
                "UnitTileIcons"
            );
            StrategyUnitCardView[] activeDetailCards = fleetWindow
                .GetComponentsInChildren<StrategyUnitCardView>()
                .Where(card => card.name != "ItemTemplate")
                .ToArray();
            Assert.AreEqual(2, activeDetailCards.Length);
            AssertArtworkTarget(
                activeDetailCards[0]
                    .GetComponentsInChildren<RawImage>()
                    .Single(image => image.name == "SelectionImage"),
                "FleetDetailSelectionImagePath",
                "UnitTileIcons"
            );
            AssertArtworkTarget(
                activeDetailCards[1]
                    .GetComponentsInChildren<RawImage>()
                    .Single(image => image.name == "BackgroundImage"),
                "FleetConstructionSmallImagePath",
                "UnitTileIcons"
            );
            foreach (
                (string imageName, string owner, string memberName) in new[]
                {
                    ("CapitalShipsTabButtonImage", "CapitalShips", "ActiveImagePath"),
                    ("StarfightersTabButtonImage", "Starfighters", "InactiveImagePath"),
                    ("RegimentsTabButtonImage", "Regiments", "InactiveImagePath"),
                    ("PersonnelTabButtonImage", "Officers", "InactiveImagePath"),
                }
            )
            {
                AssertArtworkTarget(
                    fleetWindow
                        .GetComponentsInChildren<RawImage>()
                        .Single(image => image.name == imageName),
                    memberName,
                    owner
                );
            }
            GameObject detailHoverFrame = editTarget
                .transform.Find("ContentEditorArtworkHoverFrame")
                .gameObject;
            Assert.IsFalse(detailHoverFrame.activeSelf);
            editTarget.SetHovered(true);
            Assert.IsTrue(detailHoverFrame.activeSelf);
            editTarget.SetHovered(false);
            Assert.IsFalse(detailHoverFrame.activeSelf);
            RawImage entityImage = fleetWindow
                .GetComponentsInChildren<StrategyUnitCardView>()
                .First(card => card.gameObject.activeSelf && card.name != "ItemTemplate")
                .GetComponentsInChildren<RawImage>()
                .Single(image => image.name == "EntityImage");
            ContentEditorArtworkEditTarget entityEditTarget =
                entityImage.GetComponent<ContentEditorArtworkEditTarget>();
            Assert.IsTrue(
                entityEditTarget == null || entityEditTarget.Field == null,
                "Representative unit artwork belongs to the unit editor, not this faction interface surface."
            );
            ContentPackEditorField requestedAsset = null;
            view.AssetEditRequested += field => requestedAsset = field;
            RawImage starfighterTab = fleetWindow
                .GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "StarfightersTabButtonImage");
            Assert.IsTrue(
                starfighterTab.gameObject.activeInHierarchy,
                "The rendered Starfighters tab must remain visible and directly editable."
            );
            ContentEditorArtworkEditTarget starfighterTabTarget =
                starfighterTab.GetComponent<ContentEditorArtworkEditTarget>();
            Assert.IsNotNull(starfighterTabTarget);
            Assert.AreEqual("InactiveImagePath", starfighterTabTarget.Field.MemberName);
            Texture releasedTabTexture = starfighterTab.texture;
            starfighterTabTarget.SetHovered(true);
            Assert.AreEqual("InactiveImagePath", starfighterTabTarget.Field.MemberName);
            Assert.AreSame(releasedTabTexture, starfighterTab.texture);
            RawImagePressVisual tabVisual = starfighterTab.GetComponent<RawImagePressVisual>();
            PointerEventData press = new(null) { button = PointerEventData.InputButton.Left };
            tabVisual.OnPointerDown(press);
            Assert.AreNotSame(releasedTabTexture, starfighterTab.texture);
            tabVisual.OnPointerUp(press);
            Assert.AreSame(releasedTabTexture, starfighterTab.texture);
            starfighterTabTarget.OnPointerClick(
                new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Left,
                    clickCount = 2,
                }
            );
            Assert.IsNull(requestedAsset);
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Any(field =>
                        field.Field.XmlElement == starfighterTabTarget.Field.XmlElement
                        && field.gameObject.activeSelf
                    )
            );
            starfighterTabTarget.SetHovered(false);
            requestedAsset = null;
            entityEditTarget?.OnPointerClick(
                new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Left,
                    clickCount = 2,
                }
            );
            Assert.IsNull(requestedAsset);
            Assert.IsFalse(
                fleetWindow
                    .GetComponentsInChildren<ContentEditorArtworkEditTarget>()
                    .Any(target => target.transform.Find("ContentEditorAssetEditButton") != null)
            );
            SelectArtworkSurface("Encyclopedia");
            EncyclopediaWindowView encyclopediaWindow =
                view.GetComponentInChildren<EncyclopediaWindowView>();
            Assert.IsNotNull(encyclopediaWindow);
            RawImage allDatabasesTab = encyclopediaWindow
                .GetComponentsInChildren<RawImage>()
                .Single(image => image.name == "AllDatabasesTabButtonImage");
            Assert.IsNotNull(allDatabasesTab.texture);
            Assert.IsNull(allDatabasesTab.GetComponent<ContentEditorArtworkEditTarget>());
            RawImage facilitiesTab = encyclopediaWindow
                .GetComponentsInChildren<RawImage>()
                .Single(image => image.name == "FacilitiesTabButtonImage");
            Assert.IsNotNull(facilitiesTab.texture);
            Assert.IsNotNull(facilitiesTab.GetComponent<ContentEditorArtworkEditTarget>());
            Assert.IsFalse(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label =>
                        label.text
                        == "The gameplay-window preview contains an image path that could not be loaded."
                    )
            );

            SelectArtworkSurface("Facilities and construction");
            FacilityWindowView facilityWindow = view.GetComponentInChildren<FacilityWindowView>();
            ConstructionWindowView constructionWindow =
                view.GetComponentInChildren<ConstructionWindowView>();
            Assert.IsNotNull(facilityWindow);
            Assert.IsNull(constructionWindow);
            Assert.Greater(facilityWindow.transform.localScale.x, 1f);

            SelectArtworkSurface("Strategy view");
            StrategyHudView strategyHud = view.GetComponentInChildren<StrategyHudView>();
            Assert.IsNotNull(strategyHud);
            RectTransform strategySurface = (RectTransform)strategyHud.transform.parent;
            RectInt strategySourceRect = UILayout.GetSourceRect(strategySurface);
            Assert.AreEqual(853, strategySourceRect.width);
            Assert.AreEqual(480, strategySourceRect.height);
            ContentEditorArtworkWindowPreview strategyPreview =
                view.GetComponentInChildren<ContentEditorArtworkWindowPreview>();
            Assert.IsNotNull(strategyPreview.GetComponent<RectMask2D>());
            string[] opaqueMissingImages = strategyPreview
                .GetComponentsInChildren<RawImage>()
                .Where(image => image.enabled && image.texture == null && image.color.a > 0.01f)
                .Select(image => image.name)
                .ToArray();
            Assert.IsEmpty(
                opaqueMissingImages,
                "The complete Strategy view cannot contain opaque graphics without artwork: "
                    + string.Join(", ", opaqueMissingImages)
            );
            Assert.LessOrEqual(
                UILayout.GetSourceRect((RectTransform)strategyPreview.transform).height,
                strategyPreview.GetComponentInParent<ScrollAreaView>().ViewportHeight - 180f + 1f
            );
            RawImage optionsButton = strategyHud
                .GetComponentsInChildren<RawImage>()
                .Single(image => image.name == "OptionsButtonImage");
            ContentEditorArtworkEditTarget optionsTarget =
                optionsButton.GetComponent<ContentEditorArtworkEditTarget>();
            Assert.IsNotNull(optionsTarget);
            Assert.IsTrue(optionsTarget.CanMove);
            Assert.IsTrue(optionsButton.raycastTarget);
            Assert.AreEqual("UpImagePath", optionsTarget.Field.MemberName);
            Texture optionsUp = optionsButton.texture;
            optionsTarget.SetHovered(true);
            Assert.AreEqual("PressedImagePath", optionsTarget.Field.MemberName);
            Assert.AreNotSame(optionsUp, optionsButton.texture);
            requestedAsset = null;
            optionsTarget.OnPointerClick(
                new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Left,
                    clickCount = 2,
                }
            );
            Assert.IsNull(requestedAsset);
            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Any(field =>
                        field.Field.XmlElement == optionsTarget.Field.XmlElement
                        && field.gameObject.activeSelf
                    )
            );
            optionsTarget.SetHovered(false);
            RawImage galaxyBackground = strategyPreview
                .GetComponentInChildren<GalaxyMapView>()
                .GetComponentsInChildren<RawImage>()
                .Single(image => image.name == "BackgroundImage");
            GalaxyMapView galaxyMap = strategyPreview.GetComponentInChildren<GalaxyMapView>();
            ContentPackEditorRecord[] expectedSectors = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind == "PlanetSector")
                .ToArray();
            Assert.AreEqual(
                expectedSectors.Length,
                galaxyMap.GetComponentsInChildren<PlanetSectorClusterView>().Length
            );
            Assert.AreEqual(
                expectedSectors.Sum(sector => sector.Planets.Count),
                galaxyMap
                    .GetComponentsInChildren<RawImage>()
                    .Count(image =>
                        image.name.StartsWith("Star", StringComparison.Ordinal)
                        && image.name.EndsWith("Image", StringComparison.Ordinal)
                        && image.name != "StarImageTemplate"
                    )
            );
            Texture2D mixedMarker = assets.GetTexture(
                factionTheme.GalaxyBackground.PlanetIcons.Mixed
            );
            Assert.IsFalse(
                galaxyMap
                    .GetComponentsInChildren<RawImage>()
                    .Any(image =>
                        image.name.StartsWith("Star", StringComparison.Ordinal)
                        && image.texture == mixedMarker
                    ),
                "The removed mixed marker must not be synthesized into the full-galaxy preview."
            );
            Texture2D destroyedPlanetTexture = assets.GetTexture(
                factionTheme.GalaxyBackground.DestroyedPlanetIconPath
            );
            Assert.IsFalse(
                galaxyMap
                    .GetComponentsInChildren<RawImage>()
                    .Any(image =>
                        image.name.StartsWith("Star", StringComparison.Ordinal)
                        && image.texture == destroyedPlanetTexture
                    ),
                "Destroyed-planet artwork belongs to the sector window and must not obscure the galaxy map."
            );
            Assert.AreSame(
                assets.GetTexture(factionTheme.GalaxyBackground.ImagePath),
                galaxyBackground.texture
            );
            Assert.IsNull(
                galaxyBackground.GetComponent<ContentEditorArtworkEditTarget>(),
                "A shared galaxy backdrop must not masquerade as faction-owned artwork."
            );
            RectInt galaxyRect = UILayout.GetSourceRect(galaxyBackground.rectTransform);
            Assert.GreaterOrEqual(galaxyRect.xMin, strategySourceRect.xMin);
            Assert.GreaterOrEqual(galaxyRect.yMin, strategySourceRect.yMin);
            Assert.LessOrEqual(galaxyRect.xMax, strategySourceRect.xMax);
            Assert.LessOrEqual(galaxyRect.yMax, strategySourceRect.yMax);
            RawImage speedIndicator = strategyPreview
                .GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "SpeedIndicatorImage");
            Assert.IsTrue(speedIndicator.gameObject.activeInHierarchy);
            Assert.IsTrue(speedIndicator.enabled);
            Assert.IsNotNull(speedIndicator.texture);
            ContentEditorArtworkEditTarget speedTarget =
                speedIndicator.GetComponent<ContentEditorArtworkEditTarget>();
            Assert.IsNotNull(speedTarget);
            Assert.IsTrue(speedTarget.CanMove);
            RectTransform speedInteraction = strategyPreview
                .GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "GameSpeedButton");
            Assert.IsTrue(speedInteraction.gameObject.activeInHierarchy);
            Canvas.ForceUpdateCanvases();
            Vector2 speedGrabPoint = RectTransformUtility.WorldToScreenPoint(
                null,
                speedInteraction.TransformPoint(speedInteraction.rect.center)
            );
            Assert.IsFalse(
                RectTransformUtility.RectangleContainsScreenPoint(
                    speedIndicator.rectTransform,
                    speedGrabPoint,
                    null
                ),
                "The representative grab point must exercise the larger speed-control region."
            );
            Assert.IsTrue(speedTarget.ContainsScreenPoint(speedGrabPoint, null));
            Assert.IsTrue(speedTarget.BeginMove());
            IReadOnlyList<ContentEditorArtworkPlacementChange> speedChanges =
                speedTarget.CompleteMove(new Vector2Int(5, 3));
            Assert.AreEqual(4, speedChanges.Count);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    nameof(TacticalHUDLayout.SpeedIndicatorSourceLayout),
                    nameof(TacticalHUDLayout.SpeedContextSourceLayout),
                },
                speedChanges
                    .Select(change => change.Field.XmlElement.Parent.Name.LocalName)
                    .Distinct()
                    .ToArray()
            );

            RenderSection("Default backgrounds");
            ContentEditorFieldView[] backgroundFields =
                view.GetComponentsInChildren<ContentEditorFieldView>();
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "OfficerBackgroundImagePath",
                    "CapitalShipBackgroundImagePath",
                    "StarfighterBackgroundImagePath",
                },
                backgroundFields.Select(field => field.Field.MemberName).ToArray()
            );
            Assert.AreEqual(3, backgroundFields.Length);
            Assert.IsTrue(
                backgroundFields.All(field =>
                    field
                        .GetComponentsInChildren<Button>()
                        .Count(button => button.gameObject.activeSelf) == 1
                )
            );
            CollectionAssert.AreEquivalent(
                new[] { "Officers", "Capital ships", "Starfighters" },
                view.GetComponentsInChildren<TMP_Text>()
                    .Where(label => label.text is "Officers" or "Capital ships" or "Starfighters")
                    .Select(label => label.text)
                    .ToArray()
            );
            int[] actionTops = backgroundFields
                .Select(field => UILayout.GetSourceRect((RectTransform)field.transform).y)
                .ToArray();
            Assert.IsTrue(actionTops.All(top => top == actionTops[0]));
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void Render_FactionArtworkTransparentOverlay_ShowsVisiblePixelsInsteadOfTransparentCanvas()
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(1200, 900);
            Canvas.ForceUpdateCanvases();
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));

            RenderSection("Interfaces");

            const string address =
                "Pack/Factions/Alliance/Strategy/UI/Windows/Fleet/planet-fleet-normal";
            Texture2D texture = assets.GetTexture(address);
            RectInt contentBounds = assets.GetTextureContentBounds(texture);
            RawImage preview = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.StartsWith("ArticleImageTemplate", StringComparison.Ordinal)
                    && image.texture == texture
                );
            Rect expectedUv = new Rect(
                (float)contentBounds.x / texture.width,
                (float)contentBounds.y / texture.height,
                (float)contentBounds.width / texture.width,
                (float)contentBounds.height / texture.height
            );
            RectInt previewBounds = UILayout.GetSourceRect(preview.rectTransform);

            Assert.Less(contentBounds.width * contentBounds.height, texture.width * texture.height);
            Assert.AreEqual(expectedUv, preview.uvRect);
            Assert.AreEqual(
                (float)contentBounds.width / contentBounds.height,
                (float)previewBounds.width / previewBounds.height,
                0.01f
            );
            Assert.IsFalse(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.text.StartsWith("Current: ", StringComparison.Ordinal))
            );
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase("Officers", "Encyclopedia", 1000)]
        [TestCase("Officers", "Encyclopedia", 1800)]
        [TestCase("Capital ships", "Encyclopedia", 1000)]
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
            ContentPackEditorField art = ContentEditorContentSections
                .EncyclopediaFields(record, project.GetCatalogs())
                .First(field => field.MediaType == ContentPackEditorMediaType.Image);
            Assert.AreSame(assets.GetTexture(art.Value), image.texture);
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase("Skills")]
        [TestCase("Probability")]
        [TestCase("Ranges")]
        public void Render_InheritedCompactRows_KeepsSuccessiveFieldControlsSeparated(string area)
        {
            project = ContentEditorTestData.CreateLoadableProject(
                Path.Combine(folder, "InheritedContent")
            );
            if (area == "Skills")
                SelectRecord(
                    project
                        .GetCatalogs()
                        .SelectMany(catalog => catalog.Records)
                        .First(record => record.Kind == "Officer")
                );
            else if (area == "Probability")
                SelectSharedMissionRules();
            else
            {
                SelectRecord(
                    project
                        .GetCatalogs()
                        .SelectMany(catalog => catalog.Records)
                        .First(record => record.Kind == "GameGenerationConfig")
                );
                RenderSection("Support");
            }
            Canvas.ForceUpdateCanvases();
            ContentEditorFieldView[] fields =
                view.GetComponentsInChildren<ContentEditorFieldView>();
            Assert.IsTrue(fields.Any(field => field.Field.CanInherit));
            for (int first = 0; first < fields.Length; first++)
            for (int second = first + 1; second < fields.Length; second++)
                Assert.IsFalse(
                    FieldRect(fields[first]).Overlaps(FieldRect(fields[second])),
                    fields[first].name + " overlaps " + fields[second].name
                );
        }

        /// <summary>
        /// Sets one faction-level unit artwork background through the projected theme fields.
        /// </summary>
        /// <param name="memberName">The artwork-default member to update.</param>
        /// <param name="address">The content address to assign.</param>
        private void SetFactionArtworkBackground(string memberName, string address)
        {
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme"
                    && record.Element.Element("FactionInstanceID")?.Value == "FNALL1"
                );
            ContentPackEditorField background = ContentEditorContentSections
                .FactionArtworkFields(theme)
                .Single(field => field.MemberName == memberName);
            project.SetField(background, address);
        }

        /// <summary>
        /// Selects one faction interface preview through its authored dropdown.
        /// </summary>
        /// <param name="caption">The exact interface surface caption.</param>
        private void SelectArtworkSurface(string caption)
        {
            CollectionAssert.Contains(view.ArtworkViewOptions, caption);
            for (
                int attempt = 0;
                attempt < view.ArtworkViewOptions.Count && view.ArtworkViewSelection != caption;
                attempt++
            )
                view.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "NextArtworkView")
                    .onClick.Invoke();
            Assert.AreEqual(caption, view.ArtworkViewSelection);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Configures the gray personnel card used by the runtime Defense and Fleet presentations.
        /// </summary>
        /// <param name="address">The content address to use for both personnel-card contexts.</param>
        private void SetFactionPersonnelBackground(string address)
        {
            ContentPackEditorRecord theme = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme"
                    && record.Element.Element("FactionInstanceID")?.Value == "FNALL1"
                );
            theme.Element.Element("StrategyWindows")?.Remove();
            theme.Element.Add(
                new XElement(
                    "StrategyWindows",
                    new XElement("Defense", new XElement("PersonnelBackgroundImagePath", address)),
                    new XElement("Fleet", new XElement("PersonnelBackgroundImagePath", address))
                )
            );
        }

        /// <summary>
        /// Verifies that a literal preview image navigates to its semantically owned theme field.
        /// </summary>
        /// <param name="image">The rendered preview image.</param>
        /// <param name="memberName">The exact serialized member expected for the image.</param>
        /// <param name="ownerName">The required serialized owner in the field path.</param>
        private static void AssertArtworkTarget(RawImage image, string memberName, string ownerName)
        {
            ContentEditorArtworkEditTarget target =
                image.GetComponent<ContentEditorArtworkEditTarget>();
            Assert.IsNotNull(target, image.name);
            Assert.IsNotNull(target.Field, image.name);
            Assert.AreEqual(memberName, target.Field.MemberName, image.name);
            Assert.IsTrue(
                target
                    .Field.XmlElement.Ancestors()
                    .Any(element => element.Name.LocalName == ownerName),
                image.name + " must belong to " + ownerName + "."
            );
        }

        /// <summary>
        /// Gets the occupied field rectangle in the shared canvas coordinates.
        /// </summary>
        /// <param name="field">The rendered field.</param>
        /// <returns>The occupied rectangle.</returns>
        private static Rect FieldRect(ContentEditorFieldView field)
        {
            Vector3[] corners = new Vector3[4];
            ((RectTransform)field.transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(
                corners[0].x + .01f,
                corners[0].y + .01f,
                corners[2].x - .01f,
                corners[2].y - .01f
            );
        }

        /// <summary>
        /// Verifies that one state action belongs to the preview it changes.
        /// </summary>
        /// <param name="state">The state-preview name.</param>
        /// <param name="memberName">The serialized image member.</param>
        private void AssertStateActionAligned(string state, string memberName)
        {
            RawImage preview = view.GetComponentsInChildren<RawImage>()
                .Single(image =>
                    image.name.EndsWith("State-" + state + "-Backing", StringComparison.Ordinal)
                );
            ContentEditorFieldView field = view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(item => item.Field.MemberName == memberName);
            RectInt previewBounds = UILayout.GetSourceRect(preview.rectTransform);
            RectInt fieldBounds = UILayout.GetSourceRect((RectTransform)field.transform);

            Assert.AreEqual(previewBounds.x, fieldBounds.x, memberName);
            Assert.AreEqual(previewBounds.width, fieldBounds.width, memberName);
            Assert.GreaterOrEqual(fieldBounds.y, previewBounds.yMax, memberName);
            Assert.AreEqual(1, field.GetComponentsInChildren<Button>().Length, memberName);
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
                    entry.Element("Key").Value == score.ToString(CultureInfo.InvariantCulture)
                )
                ?.Element("Value")
                ?.Value
            ?? string.Empty;
    }
}
