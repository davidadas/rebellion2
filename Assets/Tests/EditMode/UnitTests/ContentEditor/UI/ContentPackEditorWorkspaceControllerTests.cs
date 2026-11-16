using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentPackEditorWorkspaceControllerTests : ContentEditorViewTestBase
    {
        [Test]
        public void ResetSection_VisibleButton_ResetsOnlyTheCurrentSectionAndSupportsUndo()
        {
            project = ContentEditorTestData.CreateLoadableProject(
                Path.Combine(folder, "InheritedContent")
            );
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            string inheritedName = officer.GetField("DisplayName").Value;
            project.SetField(officer.GetField("DisplayName"), "Overridden name");
            project.SetField(officer.GetField("EncyclopediaDescription"), "Overridden description");
            workspace.RenderProject(project, assets);
            SelectRecord(officer);
            Button resetSection = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "ResetSection");

            resetSection.onClick.Invoke();

            ContentPackEditorRecord reset = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            Assert.AreEqual(inheritedName, reset.GetField("DisplayName").Value);
            Assert.IsTrue(reset.GetField("DisplayName").IsInherited);
            Assert.AreEqual(
                "Overridden description",
                reset.GetField("EncyclopediaDescription").Value
            );
            Assert.AreEqual("Reset section to defaults", project.UndoDescription);
            Assert.IsFalse(resetSection.gameObject.activeSelf);
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(
                "Overridden name",
                project
                    .GetCatalogs()
                    .First(catalog => catalog.Name == "Officers")
                    .Records.First()
                    .GetField("DisplayName")
                    .Value
            );
            Assert.AreEqual(
                "Overridden description",
                project
                    .GetCatalogs()
                    .First(catalog => catalog.Name == "Officers")
                    .Records.First()
                    .GetField("EncyclopediaDescription")
                    .Value
            );
        }

        [Test]
        public void SelectGenerationTask_TaskWithOverride_ShowsSectionResetImmediately()
        {
            project = ContentEditorTestData.CreateLoadableProject(
                Path.Combine(folder, "InheritedContent")
            );
            ContentPackEditorRecord setup = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record => record.Kind == "GameGenerationConfig");
            ContentPackEditorField multiplier = setup
                .GetFields()
                .Single(field => field.MemberName == "CoreMineMultiplier");
            project.SetField(multiplier, "999");
            workspace.RenderProject(project, assets);
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("category/Starting setup"));
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "GenerationTab2")
                .onClick.Invoke();
            Button resetSection = view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "ResetSection");
            Assert.IsFalse(resetSection.gameObject.activeSelf);

            TMP_Dropdown task = view.GetComponentsInChildren<TMP_Dropdown>()
                .Single(dropdown => dropdown.name == "SetupTask");
            task.value = 3;

            Assert.IsTrue(resetSection.gameObject.activeSelf);
            Assert.IsTrue(resetSection.interactable);
        }

        [Test]
        public void ResetSection_RemovedLookupThreshold_ShowsResetAndRestoresInheritedTables()
        {
            project = ContentEditorTestData.CreateLoadableProject(
                Path.Combine(folder, "InheritedContent")
            );
            workspace.RenderProject(project, assets);
            ContentEditorNumericGrid grid = SelectSharedMissionRules();
            ContentPackEditorDocument document = grid.ResetFields.First().Document;
            string inherited = document.Text;
            Button removeFirstRow = view.GetComponentsInChildren<Button>()
                .First(button =>
                    button.name.StartsWith("RemoveScoreTemplate", StringComparison.Ordinal)
                );

            removeFirstRow.onClick.Invoke();

            Button resetSection = view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "ResetSection");
            Assert.AreNotEqual(inherited, document.Text);
            Assert.IsTrue(resetSection.gameObject.activeSelf);
            resetSection.onClick.Invoke();
            Assert.AreEqual(inherited, document.Text);
            Assert.IsFalse(resetSection.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator ArrangeMapAsync_OverlappingPlanets_ClearsRenderedGeometryAndUndoesAsOneEdit()
        {
            ContentPackEditorCatalog catalog = project.GetCatalogs().First(item => item.IsMap);
            ContentPackEditorRecord sector = catalog.Records.First();
            foreach (ContentPackEditorRecord planet in sector.Planets)
                project.Move(planet, sector.X, sector.Y);
            workspace.RenderProject(project, assets);
            SelectRecord(sector);
            TMP_Dropdown faction = view.GetComponentsInChildren<TMP_Dropdown>()
                .Single(dropdown => dropdown.name == "PreviewFaction");
            faction.value = 1;
            string before = catalog.Document.Text;
            ContentEditorMapView map = view.GetComponentInChildren<ContentEditorMapView>();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
            ContentEditorMapLayout captured = map.CreateLayout();
            Task<int> arranging = workspace.ArrangeMapAsync(captured, timeout.Token);
            while (!arranging.IsCompleted)
                yield return null;
            int moved = arranging.GetAwaiter().GetResult();
            Assert.Greater(moved, 0);
            Assert.AreEqual(1, faction.value);
            PlanetSectorWindowView window = view.GetComponentInChildren<PlanetSectorWindowView>();
            Rect bounds = window.GetPlanetAreaSourceRect();
            PlanetSectorPlanetView[] planets =
                window.GetComponentsInChildren<PlanetSectorPlanetView>();
            for (int i = 0; i < planets.Length; i++)
                foreach (Rect first in planets[i].GetLayoutSourceRects())
                {
                    Assert.GreaterOrEqual(first.xMin, bounds.xMin - 0.001f);
                    Assert.GreaterOrEqual(first.yMin, bounds.yMin - 0.001f);
                    Assert.LessOrEqual(first.xMax, bounds.xMax + 0.001f);
                    Assert.LessOrEqual(first.yMax, bounds.yMax + 0.001f);
                    for (int j = i + 1; j < planets.Length; j++)
                        foreach (Rect second in planets[j].GetLayoutSourceRects())
                            Assert.IsFalse(
                                first.Overlaps(second),
                                $"Planets {i} and {j} overlap: {first}, {second}."
                            );
                }
            Task<int> repeating = workspace.ArrangeMapAsync(map.CreateLayout(), timeout.Token);
            while (!repeating.IsCompleted)
                yield return null;
            Assert.AreEqual(0, repeating.GetAwaiter().GetResult());
            string arranged = catalog.Document.Text;
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(before, catalog.Document.Text);
            Assert.IsTrue(project.Redo());
            Assert.AreEqual(arranged, catalog.Document.Text);
        }

        [UnityTest]
        public IEnumerator ArrangeMapAsync_GalaxyLayout_PreservesEachSectorsPlanetOffsets()
        {
            ContentPackEditorCatalog catalog = project.GetCatalogs().First(item => item.IsMap);
            ContentPackEditorRecord sector = catalog.Records.First();
            project.Move(sector, -200, -200);
            var offsets = catalog.Records.ToDictionary(
                record => record.Key,
                record =>
                    record.Planets.ToDictionary(
                        planet => planet.Key,
                        planet => new Vector2Int(planet.X - record.X, planet.Y - record.Y)
                    )
            );
            workspace.RenderProject(project, assets);
            SelectRecord(sector);
            Button galaxy = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Galaxy");
            galaxy.onClick.Invoke();
            ContentEditorMapView map = view.GetComponentInChildren<ContentEditorMapView>();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
            Task<int> arranging = workspace.ArrangeMapAsync(map.CreateLayout(), timeout.Token);
            while (!arranging.IsCompleted)
                yield return null;
            Assert.Greater(arranging.GetAwaiter().GetResult(), 0);
            foreach (ContentPackEditorRecord record in catalog.Records)
            foreach (ContentPackEditorRecord planet in record.Planets)
                Assert.AreEqual(
                    offsets[record.Key][planet.Key],
                    new Vector2Int(planet.X - record.X, planet.Y - record.Y)
                );
        }

        [UnityTest]
        public IEnumerator ArrangeMapAsync_CancelledRequest_DoesNotEditCoordinates()
        {
            SelectRecord(project.GetCatalogs().First(catalog => catalog.IsMap).Records.First());
            ContentEditorMapLayout layout = view.GetComponentInChildren<ContentEditorMapView>()
                .CreateLayout();
            string before = string.Join("\n", project.Documents.Select(document => document.Text));
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            Task<int> arranging = workspace.ArrangeMapAsync(layout, cancellation.Token);
            while (!arranging.IsCompleted)
                yield return null;
            Assert.Throws<TaskCanceledException>(() => arranging.GetAwaiter().GetResult());
            Assert.AreEqual(
                before,
                string.Join("\n", project.Documents.Select(document => document.Text))
            );
        }
    }
}
