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
    public sealed class ContentPackEditorViewTests : ContentEditorViewTestBase
    {
        /// <summary>
        /// Opens installed content for comprehensive page and layout checks.
        /// </summary>
        /// <param name="destination">The fixture-owned working directory.</param>
        /// <returns>The full inherited project.</returns>
        protected override ContentPackEditorProject CreateProject(string destination)
        {
            return ContentPackEditorStorage.CreateInFolder(
                TestContent.Pack.ContentRootPath,
                TestContent.Pack.Definition.ID,
                destination,
                "editor-integration-test",
                "Editor test",
                "1.0.0"
            );
        }

        [Test]
        public void RenderProject_RulesAndMissionPages_KeepAllNonAiRulesAccessible()
        {
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(project.GetCatalogs());
            navigation.SetExpanded(true);
            ContentPackEditorField[] original = project
                .GetCatalogs()
                .Where(catalog => catalog.Name == "Rules")
                .SelectMany(catalog => catalog.Records)
                .SelectMany(record => record.GetFields())
                .Where(field => field.Group != "AI")
                .ToArray();
            ContentPackEditorField[] projected = navigation
                .GetRows(string.Empty)
                .Where(node => node.Fields != null)
                .SelectMany(node => node.Fields)
                .Where(field => field.Record.Kind == "GameConfig")
                .ToArray();
            CollectionAssert.AreEqual(
                original.Select(FieldIdentity).OrderBy(value => value, StringComparer.Ordinal),
                projected
                    .Select(FieldIdentity)
                    .Distinct()
                    .OrderBy(value => value, StringComparer.Ordinal)
            );
        }

        [Test]
        public void RenderProject_SelectingEveryCatalogPage_OpensFormsWithoutCreatingOverrides()
        {
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(project.GetCatalogs());
            navigation.SetExpanded(true);
            foreach (
                ContentEditorTreeNode node in navigation
                    .GetRows(string.Empty)
                    .Where(node => node.Record != null)
                    .GroupBy(node => node.Fields != null ? node.Key : node.Catalog.Document.Address)
                    .Select(group => group.First())
            )
            {
                SelectPage(node);
                Assert.IsTrue(view.CommitFields(), node.Key);
                Assert.IsFalse(project.IsDirty, node.Key);
            }

            Assert.IsFalse(project.IsDirty);
            Assert.AreEqual(
                1,
                Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Count()
            );
        }

        [TestCase(900, 0.4f)]
        [TestCase(1600, 0f)]
        public void RenderProject_RulesAndMissionPages_KeepsWrappedTextAndControlsInsideTheirRows(
            int width,
            float zoom
        )
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(width, 900);
            view.ZoomContent(zoom);
            Canvas.ForceUpdateCanvases();
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            navigation.SetExpanded(true);
            ContentEditorTreeNode[] pages = navigation
                .GetRows(string.Empty)
                .Where(node =>
                    node.Fields != null && (node.Key.Contains("#rules/") || node.MissionID != null)
                )
                .Concat(
                    navigation
                        .GetRows(string.Empty)
                        .Where(node =>
                            node.Record?.Kind
                                is "Officer"
                                    or "SpecialForce"
                                    or "Regiment"
                                    or "CapitalShip"
                                    or "Starfighter"
                                    or "Building"
                                    or "GameGenerationConfig"
                                    or "OfficerTrait"
                                    or "Faction"
                        )
                        .GroupBy(node => node.Record.Kind)
                        .Select(group => group.First())
                )
                .ToArray();
            foreach (ContentEditorTreeNode page in pages)
            {
                SelectPage(page);
                if (
                    page.MissionID != null
                    && page.Fields.Any(field => field.Record.Kind == "GameConfig")
                )
                    view.GetComponentsInChildren<Button>()
                        .Single(button => button.name == "Tab5")
                        .onClick.Invoke();
                Canvas.ForceUpdateCanvases();
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
        }
    }
}
