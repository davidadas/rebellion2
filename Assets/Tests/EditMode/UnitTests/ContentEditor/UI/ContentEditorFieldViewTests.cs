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
                    + "<EncyclopediaDescription>Description.</EncyclopediaDescription>"
                    + "<RecruitingFactionInstanceIDs /></Officer></Officers>"
            );
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
        public void SetLayout_LongDescription_GrowsToShowAllWrappedText(int width)
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
            Assert.Greater(height, 152);
            Assert.LessOrEqual(
                input
                    .textComponent.GetPreferredValues(
                        input.text,
                        input.textViewport.rect.width,
                        float.PositiveInfinity
                    )
                    .y,
                input.textViewport.rect.height + 1
            );
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
                .Single(text => text.name == "Label");
            label.ForceMeshUpdate();
            Assert.Greater(height, control.Height);
            Assert.Greater(label.textInfo.lineCount, 1);
            Assert.IsFalse(label.isTextTruncated);
            Assert.LessOrEqual(label.preferredHeight, label.rectTransform.rect.height);
            Assert.AreEqual(height, ((RectTransform)control.transform).rect.height, 0.1f);
        }
    }
}
