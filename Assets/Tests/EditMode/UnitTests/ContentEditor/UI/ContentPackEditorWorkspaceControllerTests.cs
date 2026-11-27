using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    [Category("RequiresMedia")]
    public sealed class ContentPackEditorWorkspaceControllerTests : ContentEditorViewTestBase
    {
        [Test]
        public void SelectRecord_DuplicateAuthoredTypeID_PreservesFormAndShowsValidationError()
        {
            project = ContentEditorTestData.CreateLoadableProject(
                Path.Combine(folder, "InheritedContent")
            );
            ContentPackEditorRecord ship = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "CapitalShip");
            XElement overlay = new XElement(
                "CapitalShips",
                new XAttribute("Inherit", true),
                new XElement(
                    "CapitalShip",
                    new XElement("TypeID", ship.ID),
                    new XElement("DisplayName", "Edited ship")
                ),
                new XElement(
                    "CapitalShip",
                    new XElement("TypeID", ship.ID),
                    new XElement("ResearchOrder", 10)
                )
            );
            Directory.CreateDirectory(Path.GetDirectoryName(ship.Catalog.Document.TargetPath));
            File.WriteAllText(ship.Catalog.Document.TargetPath, overlay.ToString());
            project = ContentPackEditorStorage.OpenFolder(
                project.ContentRootPath,
                project.PackRootPath
            );
            workspace.RenderProject(project, assets);
            ContentPackEditorRecord previous = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "CapitalShip" && record.ID != ship.ID);
            SelectRecord(previous);
            ContentPackEditorRecord duplicate = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record => record.ID == ship.ID);

            Assert.DoesNotThrow(() => SelectRecord(duplicate));

            Assert.IsTrue(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Any(field => field.Field?.Record.ID == previous.ID)
            );
            Assert.IsFalse(
                view.GetComponentsInChildren<ContentEditorFieldView>()
                    .Any(field => field.Field?.Record.ID == duplicate.ID)
            );
            Assert.IsTrue(
                view.GetComponentsInChildren<TMP_Text>()
                    .Any(text =>
                        text.text.Contains("Duplicate TypeID")
                        && text.text.Contains(ship.ID)
                        && text.text.Contains(ship.Catalog.Document.Address)
                    )
            );
        }

        [Test]
        public void RenderFieldError_SnapshotInvalidChoice_RevealsAndFocusesItsDropdown()
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EditModeEventSystem>();
            try
            {
                ContentPackEditorRecord officer = project
                    .GetCatalogs()
                    .First(catalog => catalog.Name == "Officers")
                    .Records.Last();
                ContentPackEditorRecord snapshot = new ContentPackEditorRecord(
                    officer.Catalog,
                    new XElement(officer.Element)
                );
                ContentPackEditorField rank = snapshot.GetField("CurrentRank");
                view.RenderFieldError(
                    "Officers/OFAL005/Current rank: Choose a listed value.",
                    rank,
                    "Choose a listed value."
                );
                Button link = view.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "OpenInvalidField");
                RawImage linkIcon = link.GetComponentInChildren<RawImage>(true);

                Assert.IsTrue(link.gameObject.activeSelf);
                Assert.AreEqual(new Vector2(16, 16), linkIcon.rectTransform.sizeDelta);
                Assert.AreEqual(
                    "Application/Strategy/UI/Windows/ui_strategyview_open_location_button",
                    linkIcon.GetComponent<ContentTextureBinding>().Address
                );
                link.onClick.Invoke();

                ContentEditorFieldView field =
                    view.GetComponentsInChildren<ContentEditorFieldView>()
                        .Single(item => item.Field?.MemberName == "CurrentRank");
                TMP_Dropdown dropdown = field.GetComponentInChildren<TMP_Dropdown>();
                Assert.AreSame(dropdown.gameObject, EventSystem.current.currentSelectedGameObject);
                Assert.IsTrue(field.HasError);
                Assert.AreEqual(
                    "Choose a listed value.",
                    field
                        .GetComponentsInChildren<TextMeshProUGUI>(true)
                        .Single(text => text.name == "Error")
                        .text
                );
                Assert.IsFalse(
                    field
                        .GetComponentsInChildren<Button>(true)
                        .Single(button => button.name == "Revert")
                        .gameObject.activeSelf
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(eventSystem);
            }
        }

        [Test]
        public void RenderStatus_AfterFieldError_HidesTheValidationLink()
        {
            ContentPackEditorField field = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First()
                .GetField("CurrentRank");
            view.RenderFieldError(
                "Officers/OFAL001/Current rank: Choose a listed value.",
                field,
                "Choose a listed value."
            );
            Button link = view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "OpenInvalidField");

            view.RenderStatus("Content pack saved.", false);

            Assert.IsFalse(link.gameObject.activeSelf);
        }

        [Test]
        public void ApplyArtworkPlacement_GalaxyBackgroundMove_ChangesBothCoordinatesAsOneEdit()
        {
            ContentPackEditorRecord theme = GetAllianceTheme();
            ContentPackEditorField[] fields = theme.GetFields().ToArray();
            ContentPackEditorField xField = GetGalaxyPositionField(fields, "X");
            ContentPackEditorField yField = GetGalaxyPositionField(fields, "Y");
            int originalX = int.Parse(xField.Value);
            int originalY = int.Parse(yField.Value);

            workspace.ApplyArtworkPlacement(
                new[]
                {
                    new ContentEditorArtworkPlacementChange(xField, originalX + 11),
                    new ContentEditorArtworkPlacementChange(yField, originalY + 7),
                }
            );

            Assert.AreEqual((originalX + 11).ToString(), xField.Value);
            Assert.AreEqual((originalY + 7).ToString(), yField.Value);
            Assert.AreEqual("Move interface artwork", project.UndoDescription);
            Assert.IsTrue(project.Undo());
            ContentPackEditorField[] restored = GetAllianceTheme().GetFields().ToArray();
            Assert.AreEqual(originalX.ToString(), GetGalaxyPositionField(restored, "X").Value);
            Assert.AreEqual(originalY.ToString(), GetGalaxyPositionField(restored, "Y").Value);
        }

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

            Assert.AreEqual("Reset settings", resetSection.GetComponentInChildren<TMP_Text>().text);
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
        public void ResetSection_HighlightOverrideWithoutInheritedColorScheme_RestoresBaseAndSupportsUndo()
        {
            project = ContentEditorTestData.CreateLoadableProject(
                Path.Combine(folder, "InheritedContent")
            );
            ContentPackEditorRecord theme = GetAllianceTheme();
            Assert.IsNull(theme.Element.Element(nameof(FactionTheme.ColorScheme)));
            Color defaultHighlight = ContentEditorPresentation
                .GetFactionTheme(theme)
                .GetHighlightTextColor();
            ContentPackEditorField highlight = ContentEditorContentSections
                .FactionArtworkFields(theme)
                .Single(field =>
                    field.MemberName == nameof(FactionColorScheme.HighlightTextColorHex)
                );
            project.SetField(highlight, "ABCDEF");
            workspace.RenderProject(project, assets);
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("factions/Alliance"));
            view.RenderSection("Colors");
            Button reset = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "ResetSection");
            Assert.AreEqual("Reset colors", reset.GetComponentInChildren<TMP_Text>().text);

            reset.onClick.Invoke();

            Assert.AreEqual(
                defaultHighlight,
                ContentEditorPresentation
                    .GetFactionTheme(GetAllianceTheme())
                    .GetHighlightTextColor()
            );
            Assert.IsFalse(reset.gameObject.activeSelf);
            Assert.IsTrue(project.Undo());
            ContentPackEditorField restored = GetAllianceTheme()
                .GetFields()
                .Single(field =>
                    field.MemberName == nameof(FactionColorScheme.HighlightTextColorHex)
                );
            Assert.AreEqual("ABCDEF", restored.Value);
        }

        [Test]
        public void SetField_LegacyPrimaryColorChanges_PreservesWhichColorsFollowIt()
        {
            ContentPackEditorRecord record = GetAllianceTheme();
            record.Element.Add(
                new XElement(
                    nameof(FactionTheme.ColorScheme),
                    new XElement(nameof(FactionColorScheme.PrimaryColorHex), "112233"),
                    new XElement(nameof(FactionColorScheme.HighlightTextColorHex), "112233"),
                    new XElement(nameof(FactionColorScheme.SelectionColorHex), "223344")
                )
            );
            ContentPackEditorField primary = ContentEditorContentSections
                .FactionColorFields(record)
                .Single(field => field.MemberName == nameof(FactionColorScheme.PrimaryColorHex));

            project.SetField(primary, "223344");
            project.SetField(primary, "334455");

            FactionTheme edited = ContentEditorPresentation.GetFactionTheme(record);
            Assert.AreEqual(
                new Color32(0x33, 0x44, 0x55, 0xff),
                (Color32)edited.GetHighlightTextColor()
            );
            Assert.AreEqual(
                new Color32(0x22, 0x33, 0x44, 0xff),
                (Color32)edited.GetSelectionColor()
            );
        }

        [Test]
        public void SetField_TurningOffPrimaryLink_KeepsTheDisplayedColor()
        {
            ContentPackEditorRecord record = GetAllianceTheme();
            record.Element.Add(
                new XElement(
                    nameof(FactionTheme.ColorScheme),
                    new XElement(nameof(FactionColorScheme.PrimaryColorHex), "112233"),
                    new XElement(nameof(FactionColorScheme.HighlightTextColorHex), "0000FF"),
                    new XElement(nameof(FactionColorScheme.HighlightTextFollowsPrimary), true)
                )
            );
            var fields = ContentEditorContentSections
                .FactionColorFields(record)
                .ToDictionary(field => field.MemberName);

            project.SetField(
                fields[nameof(FactionColorScheme.HighlightTextFollowsPrimary)],
                "false"
            );
            project.SetField(fields[nameof(FactionColorScheme.PrimaryColorHex)], "334455");

            FactionTheme edited = ContentEditorPresentation.GetFactionTheme(record);
            Assert.AreEqual(
                new Color32(0x11, 0x22, 0x33, 0xff),
                (Color32)edited.GetHighlightTextColor()
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
            Assert.AreEqual(
                "Reset buildings",
                resetSection.GetComponentInChildren<TMP_Text>().text
            );
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

        [Test]
        public void HandleKeyboardShortcut_FocusedPlanet_NudgesOnlyThatPlanetAndCanUndo()
        {
            ContentPackEditorRecord sector = project
                .GetCatalogs()
                .First(catalog => catalog.IsMap)
                .Records.First(record => record.Planets.Count >= 2);
            ContentPackEditorRecord first = sector.Planets[0];
            ContentPackEditorRecord second = sector.Planets[1];
            Vector2Int firstPosition = new(first.X, first.Y);
            Vector2Int secondPosition = new(second.X, second.Y);
            string before = sector.Catalog.Document.Text;
            SelectRecord(sector);
            ContentEditorPlanetDrag clicked =
                view.GetComponentsInChildren<ContentEditorPlanetDrag>()[0];
            EventSystem previous = EventSystem.current;
            GameObject eventRoot = new("Map selection events", typeof(EditModeEventSystem));
            EventSystem.current = eventRoot.GetComponent<EventSystem>();
            try
            {
                PointerEventData pointer = new(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                };
                ExecuteEvents.Execute(
                    clicked.gameObject,
                    pointer,
                    ExecuteEvents.pointerDownHandler
                );
                Assert.AreEqual(
                    "PlanetSelection",
                    EventSystem.current.currentSelectedGameObject?.name
                );
                Assert.IsTrue(
                    view.HandleKeyboardShortcut(
                        new Event { type = EventType.KeyDown, keyCode = KeyCode.D }
                    )
                );
                Assert.AreEqual(firstPosition + Vector2Int.right, new Vector2Int(first.X, first.Y));
                Assert.AreEqual(secondPosition, new Vector2Int(second.X, second.Y));
                Assert.IsTrue(project.Undo());
                Assert.AreEqual(before, sector.Catalog.Document.Text);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(eventRoot);
                if (previous != null)
                    EventSystem.current = previous;
            }
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

        /// <summary>
        /// Finds the Alliance theme in the fixture-owned project.
        /// </summary>
        /// <returns>The editable Alliance theme record.</returns>
        private ContentPackEditorRecord GetAllianceTheme()
        {
            return project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record =>
                    record.Kind == "FactionTheme"
                    && record.Element.Element("FactionInstanceID")?.Value == "FNALL1"
                );
        }

        /// <summary>
        /// Finds one coordinate beneath the Galaxy background's source position.
        /// </summary>
        /// <param name="fields">The theme fields to search.</param>
        /// <param name="member">The X or Y coordinate member.</param>
        /// <returns>The matching authored coordinate field.</returns>
        private static ContentPackEditorField GetGalaxyPositionField(
            ContentPackEditorField[] fields,
            string member
        )
        {
            return fields.Single(field =>
                field.MemberName == member
                && field.XmlElement.Parent?.Name.LocalName == "SourcePosition"
                && field
                    .XmlElement.Ancestors()
                    .Any(element => element.Name.LocalName == "GalaxyBackground")
            );
        }
    }
}
