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
    public abstract class ContentEditorViewTestBase
    {
        protected ContentPackEditorProject project;
        protected ContentAssets assets;
        protected ContentPackEditorView view;
        protected ContentPackEditorWorkspaceController workspace;
        protected string folder;

        /// <summary>
        /// Opens a fixture-owned pack copy in the generated workspace.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            string contentRoot = TestContent.Pack.ContentRootPath;
            ContentPackDefinition source = TestContent.Pack.Definition;
            folder = Path.Combine(
                Path.GetTempPath(),
                "content-editor-ui-" + Guid.NewGuid().ToString("N")
            );
            project = CreateProject(folder);
            assets = TestContent.Assets;
            view = UIComponentTestHelper.InstantiatePrefabComponent<ContentPackEditorView>(
                "Assets/Prefabs/UI/ContentPackEditor/ContentPackEditorRoot.prefab"
            );
            Canvas canvas = view.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)view.transform).sizeDelta = new Vector2(1600, 900);
            Canvas.ForceUpdateCanvases();
            workspace = new ContentPackEditorWorkspaceController(view);
            view.Initialize(new[] { source }, contentRoot);
            workspace.RenderProject(project, assets);
        }

        /// <summary>
        /// Releases the workspace and removes the fixture-owned copy.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            workspace?.Dispose();
            if (view != null)
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        /// <summary>
        /// Supplies the small editable inputs for ordinary view checks.
        /// </summary>
        /// <param name="destination">The fixture-owned working directory.</param>
        /// <returns>The independent working project.</returns>
        protected virtual ContentPackEditorProject CreateProject(string destination)
        {
            return ContentEditorTestData.CreateProject(destination);
        }

        /// <summary>
        /// Adds a unit directly through the visible category menu.
        /// </summary>
        /// <param name="roster">A catalog identifying the unit category.</param>
        protected void ChooseUnitCategory(ContentPackEditorCatalog roster)
        {
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "AddUnit")
                .onClick.Invoke();
            ContextMenuCommandView category = view.GetComponentsInChildren<ContextMenuCommandView>()
                .Single(command =>
                    command
                        .GetComponentsInChildren<TMP_Text>()
                        .Any(label => label.text == roster.ItemName)
                );
            PointerEventData click = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            };
            category.OnPointerClick(click);
        }

        /// <summary>
        /// Selects a named entry through its visible tree row.
        /// </summary>
        /// <param name="record">The entry to select.</param>
        protected void SelectRecord(ContentPackEditorRecord record)
        {
            TMP_InputField search = view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search");
            search.SetTextWithoutNotify(record.ID);
            search.onValueChanged.Invoke(record.ID);
            ContentEditorRecordRowView row =
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Single(item =>
                        item.Node?.Record?.Element == record.Element
                        && (record.Kind != "GameConfig" || item.Node.Key.Contains("#rules/"))
                    );
            row.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }
            );
        }

        /// <summary>
        /// Selects one logical page through its visible navigation row.
        /// </summary>
        /// <param name="node">The page to select.</param>
        protected void SelectPage(ContentEditorTreeNode node)
        {
            TMP_InputField search = view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "Search");
            search.SetTextWithoutNotify(node.Caption);
            search.onValueChanged.Invoke(node.Caption);
            ContentEditorRecordRowView[] matches =
                view.GetComponentsInChildren<ContentEditorRecordRowView>()
                    .Where(item => item.Node?.Key == node.Key)
                    .ToArray();
            Assert.AreEqual(1, matches.Length, node.Key);
            ContentEditorRecordRowView row = matches.Single();
            row.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }
            );
        }

        /// <summary>
        /// Identifies a projected property including its repeated-entry context.
        /// </summary>
        /// <param name="field">The projected property.</param>
        /// <returns>The comparable authored field identity.</returns>
        protected static string FieldIdentity(ContentPackEditorField field)
        {
            return field.Group + "|" + string.Join("/", field.SectionPath) + "|" + field.Name;
        }

        /// <summary>
        /// Finds the visible diplomacy control in the dedicated officer form.
        /// </summary>
        /// <returns>The shared text input.</returns>
        protected TMP_InputField SkillInput()
        {
            return view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.name == "DiplomacyField")
                .GetComponentInChildren<TMP_InputField>(true);
        }

        /// <summary>
        /// Opens shared detection rules through their visible navigation row.
        /// </summary>
        /// <returns>The current editable probability grid.</returns>
        protected ContentEditorNumericGrid SelectSharedMissionRules()
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
        protected TMP_InputField ProbabilityInput(int score, int column) =>
            view.GetComponentsInChildren<ContentEditorFieldView>()
                .Single(field => field.name == "Probability" + column + "Score" + score + "Field")
                .GetComponentInChildren<TMP_InputField>();

        /// <summary>
        /// Reads the authored value directly from the project's current rules document.
        /// </summary>
        /// <param name="score">The success-table threshold.</param>
        /// <returns>The serialized value, or empty when no entry exists.</returns>
        protected string ProbabilityValue(int score) =>
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

        [ExecuteAlways]
        protected sealed class EditModeEventSystem : EventSystem { }
    }
}
