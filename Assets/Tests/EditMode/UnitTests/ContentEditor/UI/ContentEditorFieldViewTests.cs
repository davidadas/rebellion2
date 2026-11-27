using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    [Category("RequiresMedia")]
    public sealed class ContentEditorFieldViewTests : ContentPackEditorTestBase
    {
        private ContentPackEditorProject project;
        private ContentPackEditorRecord officer;
        private ContentEditorFieldView control;
        private GameObject canvas;

        /// <summary>
        /// Creates one field control with independent officer data.
        /// </summary>
        [SetUp]
        public void SetUpField()
        {
            WriteFixtureFile(
                "Data/officers.xml",
                "<Officers><Officer><TypeID>OFFICER</TypeID>"
                    + "<DisplayName>Officer</DisplayName>"
                    + "<EncyclopediaDescription>Description.</EncyclopediaDescription>"
                    + "<IsRecruitable>false</IsRecruitable>"
                    + "<RecruitingFactionInstanceIDs /></Officer></Officers>"
            );
            UpdateBaseFactionDefinition(faction => faction.OfficersPath = "Data/officers.xml");
            project = CreateWorkingProject();
            officer = project.GetCatalogs().Single(c => c.Name == "Officers").Records.Single();
            control = UIComponentTestHelper.InstantiatePrefabComponent<ContentEditorFieldView>(
                "Assets/Prefabs/UI/ContentPackEditor/ContentPackEditorRoot.prefab"
            );
            canvas = new GameObject("Field test", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            control.transform.SetParent(canvas.transform, false);
            control.gameObject.SetActive(true);
        }

        /// <summary>
        /// Releases the isolated field and canvas.
        /// </summary>
        [TearDown]
        public void TearDownField()
        {
            if (canvas != null)
                Object.DestroyImmediate(canvas);
        }

        [Test]
        public void Render_StarfighterRole_ShowsFourRolesAndUnclassified()
        {
            WriteFixtureFile(
                "Data/starfighters.xml",
                "<Starfighters><Starfighter><TypeID>FIGHTER</TypeID>"
                    + "<Roles><StarfighterRole>Bomber</StarfighterRole></Roles>"
                    + "</Starfighter></Starfighters>"
            );
            UpdateBaseFactionDefinition(faction =>
                faction.StarfightersPath = "Data/starfighters.xml"
            );
            using ContentPackEditorProject fighters = ContentPackEditorStorage.Create(
                contentRoot,
                "base-pack",
                "fighters",
                "Fighters",
                "1.0.0"
            );
            ContentPackEditorRecord record = fighters
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Starfighters")
                .Records.Single();
            ContentPackEditorField field = record.GetField("Role");
            string before = field.Document.Text;
            control.Submitted += (selected, values) => fighters.SetField(selected, values.Single());

            control.Render(
                ContentPackEditorProjector.BuildField(fighters, field),
                null,
                null,
                null
            );
            control.SetLayout(new Rect(0, 0, 1000, 0), 220);

            TMP_Dropdown dropdown = control.GetComponentInChildren<TMP_Dropdown>();
            Assert.IsNotNull(dropdown);
            CollectionAssert.AreEqual(
                new[] { "Unclassified", "Interceptor", "Multirole", "Bomber", "Heavy assault" },
                dropdown.options.Select(option => option.text)
            );
            Assert.AreEqual("Bomber", dropdown.options[dropdown.value].text);
            Assert.IsEmpty(control.GetComponentsInChildren<UICheckboxView>());
            Assert.AreEqual(before, field.Document.Text);

            dropdown.value = 1;

            Assert.AreEqual("Interceptor", record.Element.Element("Role")?.Value);
            Assert.IsNull(record.Element.Element("Roles"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Bomber")]
        public void Render_ReassignedStarfighterRole_CanReturnToUnclassified(string initialRole)
        {
            WriteFixtureFile(
                "Data/starfighters.xml",
                "<Starfighters><Starfighter><TypeID>FIGHTER</TypeID>"
                    + (initialRole == null ? "" : $"<Role>{initialRole}</Role>")
                    + "</Starfighter></Starfighters>"
            );
            UpdateBaseFactionDefinition(faction =>
                faction.StarfightersPath = "Data/starfighters.xml"
            );
            using ContentPackEditorProject fighters = ContentPackEditorStorage.Create(
                contentRoot,
                "base-pack",
                "fighters",
                "Fighters",
                "1.0.0"
            );
            ContentPackEditorRecord record = fighters
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Starfighters")
                .Records.Single();
            control.Submitted += (selected, values) => fighters.SetField(selected, values.Single());
            control.Render(
                ContentPackEditorProjector.BuildField(fighters, record.GetField("Role")),
                null,
                null,
                null
            );
            TMP_Dropdown dropdown = control.GetComponentInChildren<TMP_Dropdown>();
            dropdown.value = dropdown.options.FindIndex(option => option.text == "Heavy assault");
            Assert.AreEqual("HeavyAssault", record.GetField("Role").Value);

            control.Render(
                ContentPackEditorProjector.BuildField(fighters, record.GetField("Role")),
                null,
                null,
                null
            );
            int unclassified = dropdown.options.FindIndex(option => option.text == "Unclassified");
            Assert.GreaterOrEqual(unclassified, 0);
            dropdown.value = unclassified;

            Assert.AreEqual(string.Empty, record.GetField("Role").Value);
            Assert.IsNull(record.Element.Element("Role"));
            Assert.IsNull(record.Element.Element("Roles"));
            ContentPackEditorStorage.Save(fighters);
            using ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                fighters.PackRootPath
            );
            record = reopened
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Starfighters")
                .Records.Single();
            Assert.AreEqual(string.Empty, record.GetField("Role").Value);
            control.Render(
                ContentPackEditorProjector.BuildField(reopened, record.GetField("Role")),
                null,
                null,
                null
            );
            Assert.AreEqual("Unclassified", dropdown.options[dropdown.value].text);
        }

        [Test]
        public void Render_EmptyAudioList_OffersAddingAClipWithoutAnEditablePath()
        {
            ContentPackEditorField field = ContentEditorUnitAudio
                .GetFields(officer)
                .Single(item => item.MemberName == "Order");
            ContentPackEditorField requested = null;
            string before = field.Document.Text;
            control.Render(
                ContentPackEditorProjector.BuildField(project, field),
                null,
                value => requested = value,
                null
            );
            control.SetLayout(new Rect(0, 0, 1000, 0), 220);

            Assert.IsFalse(
                control.GetComponentInChildren<TMP_InputField>(true).gameObject.activeSelf
            );
            TMP_Dropdown clips = control.GetComponentInChildren<TMP_Dropdown>();
            Assert.AreEqual("No clips", clips.options.Single().text);
            Assert.IsFalse(clips.interactable);
            Assert.IsFalse(
                control
                    .GetComponentsInChildren<Button>()
                    .Single(button => button.name == "Preview")
                    .interactable
            );
            Assert.IsFalse(
                control
                    .GetComponentsInChildren<Button>()
                    .Single(button => button.name == "SelectItems")
                    .interactable
            );
            control
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Import")
                .onClick.Invoke();
            Assert.AreSame(field, requested);
            Assert.AreEqual(before, field.Document.Text);
        }

        [Test]
        public void SelectDropdownValue_AudioList_SelectsPlaybackWithoutEditingTheList()
        {
            WriteFixtureFile("Audio/one.wav", "fixture");
            WriteFixtureFile("Audio/two.wav", "fixture");
            ContentPackEditorField field = ContentEditorUnitAudio
                .GetFields(officer)
                .Single(item => item.MemberName == "Order");
            project.SetFieldValues(field, new[] { "Pack/Audio/one.wav", "Pack/Audio/two.wav" });
            string before = field.Document.Text;
            string played = null;
            control.Render(
                ContentPackEditorProjector.BuildField(project, field),
                null,
                null,
                (_, address, _) => played = address
            );
            control.SetLayout(new Rect(0, 0, 1000, 0), 220);

            control.GetComponentInChildren<TMP_Dropdown>().value = 1;
            control
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "Preview")
                .onClick.Invoke();

            Assert.AreEqual("Pack/Audio/two.wav", played);
            Assert.AreEqual(before, field.Document.Text);
            CollectionAssert.AreEqual(
                new[] { "one.wav", "two.wav" },
                control.GetComponentInChildren<TMP_Dropdown>().options.Select(option => option.text)
            );
        }

        [Test]
        public void Render_AudioList_RejectsEditsFromTheHiddenTextInput()
        {
            ContentPackEditorField field = ContentEditorUnitAudio
                .GetFields(officer)
                .Single(item => item.MemberName == "Order");
            control.Submitted += project.SetFieldValues;
            control.Render(ContentPackEditorProjector.BuildField(project, field), null, null, null);

            control.GetComponentInChildren<TMP_InputField>(true).onEndEdit.Invoke("arbitrary text");

            Assert.IsEmpty(field.Values);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void RequestSelection_AudioList_RemovesOnlyTheSelectedClipAndSupportsUndo()
        {
            WriteFixtureFile("Audio/one.wav", "fixture");
            WriteFixtureFile("Audio/two.wav", "fixture");
            ContentPackEditorField field = ContentEditorUnitAudio
                .GetFields(officer)
                .Single(item => item.MemberName == "Order");
            project.SetFieldValues(field, new[] { "Pack/Audio/one.wav", "Pack/Audio/two.wav" });
            control.AudioClipRemovalRequested += project.RemoveAudioClip;
            control.Render(ContentPackEditorProjector.BuildField(project, field), null, null, null);
            control.SetLayout(new Rect(0, 0, 1000, 0), 220);
            control.GetComponentInChildren<TMP_Dropdown>().value = 1;

            control
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "SelectItems")
                .onClick.Invoke();

            CollectionAssert.AreEqual(new[] { "Pack/Audio/one.wav" }, field.Values);
            Assert.IsTrue(project.Undo());
            CollectionAssert.AreEqual(
                new[] { "Pack/Audio/one.wav", "Pack/Audio/two.wav" },
                ContentEditorUnitAudio
                    .GetFields(
                        project
                            .GetCatalogs()
                            .Single(catalog => catalog.Name == "Officers")
                            .Records.Single()
                    )
                    .Single(item => item.MemberName == "Order")
                    .Values
            );
        }

        [TestCase("Officers", "RecruitingFactionInstanceIDs")]
        [TestCase("Starfighters", "ManufacturingFactionInstanceIDs")]
        [TestCase("Capital ships", "ManufacturingFactionInstanceIDs")]
        [TestCase("Troops", "ManufacturingFactionInstanceIDs")]
        [TestCase("Special forces", "ManufacturingFactionInstanceIDs")]
        [TestCase("Buildings", "ManufacturingFactionInstanceIDs")]
        public void Render_NewUnit_LetsTheAuthorChooseEitherOrBothFactions(
            string category,
            string member
        )
        {
            project = ContentEditorTestData.CreateProject(
                Path.Combine(contentRoot, "WorkingProject")
            );
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == category);
            ContentPackEditorRecord unit = project.AddUnit(catalog);
            control.Submitted += (field, values) => project.SetFieldValues(field, values);
            control.Render(
                ContentPackEditorProjector.BuildField(project, unit.GetField(member)),
                null,
                null,
                null
            );
            ContentEditorFieldView field = control;
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
            foreach (UICheckboxView choice in choices.Where(choice => choice != selected))
            {
                UIComponentTestHelper.InvokeLifecycle(choice, "Awake");
                choice.GetComponent<Toggle>().isOn = true;
            }
            CollectionAssert.AreEquivalent(
                project.GetFactionOptions().Keys,
                unit.GetField(member).Values
            );
            control.Commit();
            Assert.IsFalse(control.HasError);
        }

        [TestCase(340)]
        [TestCase(700)]
        public void SetLayout_LongDescription_KeepsAStableEditorWithItsOwnScrollbar(int width)
        {
            ContentPackEditorField description = officer.GetField("EncyclopediaDescription");
            project.SetField(
                description,
                string.Join(
                    " ",
                    Enumerable.Repeat(
                        "A long description that must remain readable at any window size.",
                        45
                    )
                )
            );
            control.Render(
                ContentPackEditorProjector.BuildField(project, description),
                null,
                null,
                null
            );

            int height = control.SetLayout(new Rect(0, 0, width, control.Height), 0);
            Canvas.ForceUpdateCanvases();

            TMP_InputField input = control.GetComponentInChildren<TMP_InputField>();
            Assert.AreEqual(152, height);
            Assert.IsTrue(input.verticalScrollbar.gameObject.activeSelf);
            Assert.Greater(
                input
                    .textComponent.GetPreferredValues(
                        input.text,
                        input.textViewport.rect.width,
                        float.PositiveInfinity
                    )
                    .y,
                input.textViewport.rect.height
            );
        }

        [Test]
        public void Render_ReusedDescriptionInput_StartsAtTheFirstLine()
        {
            ContentPackEditorField description = officer.GetField("EncyclopediaDescription");
            ContentPackEditorFieldRenderData data = ContentPackEditorProjector.BuildField(
                project,
                description
            );
            control.Render(data, null, null, null);
            TMP_InputField input = control.GetComponentInChildren<TMP_InputField>();
            input.stringPosition = input.text.Length;
            input.textComponent.rectTransform.anchoredPosition = new Vector2(0, 56);
            input.verticalScrollbar.SetValueWithoutNotify(1);

            control.Render(data, null, null, null);
            control.SetLayout(new Rect(0, 0, 520, control.Height), 0);
            Canvas.ForceUpdateCanvases();

            Assert.AreEqual(0, input.stringPosition);
            Assert.AreEqual(0f, input.textComponent.rectTransform.anchoredPosition.y, 0.01f);
        }

        [Test]
        public void SetLayout_LongFieldCaption_WrapsAndExpandsTheRow()
        {
            control.Render(
                new ContentPackEditorFieldRenderData(
                    officer.GetField("RecruitingFactionInstanceIDs"),
                    new Dictionary<string, string> { ["first"] = "First", ["second"] = "Second" },
                    null
                ),
                null,
                null,
                null
            );

            int height = control.SetLayout(new Rect(0, 0, 340, control.Height), 70);
            Canvas.ForceUpdateCanvases();

            TMP_Text label = control
                .GetComponentsInChildren<TMP_Text>()
                .Single(text => text.name == "Label" && text.transform.parent == control.transform);
            label.ForceMeshUpdate();
            Assert.Greater(height, control.Height);
            Assert.Greater(label.textInfo.lineCount, 1);
            Assert.IsFalse(label.isTextTruncated);
            Assert.LessOrEqual(label.preferredHeight, label.rectTransform.rect.height);
            Assert.AreEqual(height, ((RectTransform)control.transform).rect.height, 0.1f);
        }

        [Test]
        public void Render_InheritedSetting_UsesTheStandardInputColorWithoutAFieldResetControl()
        {
            ContentPackEditorField description = officer.GetField("EncyclopediaDescription");
            control.Render(
                ContentPackEditorProjector.BuildField(project, description),
                null,
                null,
                null
            );
            control.SetLayout(new Rect(0, 0, 700, control.Height), 220);
            TMP_InputField input = control.GetComponentInChildren<TMP_InputField>();

            Assert.IsFalse(
                control
                    .GetComponentsInChildren<Button>(true)
                    .Any(button => button.name == "Inheritance")
            );
            Assert.IsFalse(
                control
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Any(text => text.text.Contains("Reset"))
            );
            Assert.AreEqual(
                ContentEditorAppearance.ColorFor(
                    ContentEditorColorMode.Dark,
                    ContentEditorColorRole.Input
                ),
                input.GetComponent<Image>().color
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void SetLayout_EditingAnInheritedField_UsesOverrideColorWithoutChangingItsSize()
        {
            ContentPackEditorField description = officer.GetField("EncyclopediaDescription");
            control.Submitted += (field, values) => project.SetField(field, values.Single());
            control.Render(
                ContentPackEditorProjector.BuildField(project, description),
                () => control.SetLayout(new Rect(0, 0, 700, control.Height), 220),
                null,
                null
            );
            control.SetLayout(new Rect(0, 0, 700, control.Height), 220);

            control
                .GetComponentInChildren<TMP_InputField>()
                .onEndEdit.Invoke("Changed description");

            Assert.AreEqual(152, control.SetLayout(new Rect(0, 0, 700, control.Height), 220));
            Assert.AreEqual(
                ContentEditorAppearance.ColorFor(
                    ContentEditorColorMode.Dark,
                    ContentEditorColorRole.OverrideInput
                ),
                control.GetComponentInChildren<TMP_InputField>().GetComponent<Image>().color
            );
        }

        [Test]
        public void ApplyBoolean_InheritedCheckbox_UsesOverrideIndicatorAfterEdit()
        {
            ContentPackEditorField recruitable = officer.GetField("IsRecruitable");
            control.Submitted += (field, values) => project.SetField(field, values.Single());
            control.Render(
                ContentPackEditorProjector.BuildField(project, recruitable),
                null,
                null,
                null
            );
            TextMeshProUGUI label = control
                .GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "Label" && text.transform.parent == control.transform);
            UICheckboxView checkbox = control.GetComponentInChildren<UICheckboxView>();
            UIComponentTestHelper.InvokeLifecycle(checkbox, "Awake");

            checkbox.GetComponent<Toggle>().isOn = true;

            Assert.AreEqual(
                ContentEditorAppearance.ColorFor(
                    ContentEditorColorMode.Dark,
                    ContentEditorColorRole.Accent
                ),
                label.color
            );
            Assert.AreEqual("true", recruitable.Value);
        }

        [Test]
        public void SetCellLayout_InheritedField_KeepsTheCompactRowHeight()
        {
            ContentPackEditorField description = officer.GetField("DisplayName");
            control.Render(
                ContentPackEditorProjector.BuildField(project, description),
                null,
                null,
                null
            );
            int height = control.SetLayout(new Rect(0, 0, 180, 44), 0);
            control.SetCellLayout();
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(44, height);
        }
    }
}
