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
