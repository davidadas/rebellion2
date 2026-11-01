using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorMapViewTests
    {
        private string folder;
        private ContentPackEditorProject project;
        private ContentAssets assets;
        private ContentEditorMapView view;
        private GameObject canvas;

        /// <summary>
        /// Creates the map subtree without opening forms or navigation controls.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "editor-map-" + Guid.NewGuid().ToString("N"));
            project = ContentEditorTestData.CreateProject(folder);
            assets = TestContent.Assets;
            canvas = new GameObject("Map test", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1600, 900);
            view = UIComponentTestHelper.InstantiatePrefabComponent<ContentEditorMapView>(
                "Assets/Prefabs/UI/ContentPackEditor/ContentPackEditorRoot.prefab"
            );
            RectTransform rect = (RectTransform)view.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(800, 700);
            ContentBindings.Apply(view.gameObject, assets);
            view.Bind(project, assets);
        }

        /// <summary>
        /// Releases the map and fixture-owned definitions.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(canvas);
            Directory.Delete(folder, true);
        }

        [Test]
        public void Render_FitMap_ResetsPanAndZoomWithoutChangingPlanetCoordinates()
        {
            SelectRecord(project.GetCatalogs().First(catalog => catalog.IsMap).Records.First());
            ContentEditorMapViewport viewport =
                view.GetComponentInChildren<ContentEditorMapViewport>();
            Button fit = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "FitMap");
            Assert.IsFalse(fit.interactable);
            RectTransform rect = (RectTransform)viewport.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center)
            );
            viewport.OnScroll(
                new PointerEventData(EventSystem.current)
                {
                    position = point,
                    scrollDelta = Vector2.up,
                }
            );
            Assert.IsFalse(viewport.IsFitted);
            Assert.IsTrue(fit.interactable);
            fit.onClick.Invoke();
            Assert.IsTrue(viewport.IsFitted);
            Assert.IsFalse(fit.interactable);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void Render_FitMap_KeepsTheWholeSectorWindowInsideTheViewport()
        {
            SelectRecord(project.GetCatalogs().First(catalog => catalog.IsMap).Records.First());
            Canvas.ForceUpdateCanvases();
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "FitMap")
                .onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            RectTransform viewport = (RectTransform)
                view.GetComponentInChildren<ContentEditorMapViewport>().transform;
            RectTransform window = (RectTransform)
                view.GetComponentInChildren<PlanetSectorWindowView>().transform;
            Vector3[] corners = new Vector3[4];
            window.GetWorldCorners(corners);
            Vector3 firstCorner = viewport.InverseTransformPoint(corners[0]);
            Vector3 lastCorner = viewport.InverseTransformPoint(corners[2]);
            Assert.IsTrue(
                Mathf.Abs(lastCorner.x - firstCorner.x - viewport.rect.width) < 0.001f
                    || Mathf.Abs(lastCorner.y - firstCorner.y - viewport.rect.height) < 0.001f,
                "A fitted sector must fill at least one viewport dimension."
            );
            foreach (Vector3 corner in corners)
            {
                Vector3 point = viewport.InverseTransformPoint(corner);
                Assert.GreaterOrEqual(point.x, viewport.rect.xMin - 0.001f);
                Assert.GreaterOrEqual(point.y, viewport.rect.yMin - 0.001f);
                Assert.LessOrEqual(point.x, viewport.rect.xMax + 0.001f);
                Assert.LessOrEqual(point.y, viewport.rect.yMax + 0.001f);
            }
            RectTransform canvas = (RectTransform)view.transform;
            viewport.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 point = canvas.InverseTransformPoint(corner);
                Assert.GreaterOrEqual(point.y, canvas.rect.yMin - 0.001f);
                Assert.LessOrEqual(point.y, canvas.rect.yMax + 0.001f);
            }
        }

        [Test]
        public void Render_FactionIconPreview_UsesEachFactionsArtworkWithoutEditingThePack()
        {
            SelectRecord(project.GetCatalogs().First(catalog => catalog.IsMap).Records.First());
            TMP_Dropdown chooser = view.GetComponentsInChildren<TMP_Dropdown>()
                .Single(dropdown => dropdown.name == "PreviewFaction");
            Assert.GreaterOrEqual(chooser.options.Count, 3);
            foreach (int index in new[] { 1, 2 })
            {
                chooser.value = index;
                FactionTheme theme = ContentEditorPresentation
                    .GetFactionThemes(project)[index - 1]
                    .Theme;
                Texture2D expected = assets.GetTexture(
                    theme.PlanetOverlayTheme.PlanetOverlayIcons.Fleets.NormalImagePath
                );
                Assert.IsTrue(
                    view.GetComponentsInChildren<PlanetSectorPlanetView>()
                        .First()
                        .GetComponentsInChildren<RawImage>()
                        .Any(image => image.texture == expected)
                );
            }
            chooser.value = 0;
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase(0.2f)]
        [TestCase(0.35f)]
        public void Render_GalaxyPreview_RendersEveryAuthoredPlanetThroughGameplayStars(float split)
        {
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            SelectPage(navigation.Find("galaxy"));
            ((RectTransform)view.transform).sizeDelta = new Vector2(1600 * (1 - split), 700);
            Canvas.ForceUpdateCanvases();
            ContentEditorMapMarker[] clusters =
                view.GetComponentsInChildren<ContentEditorMapMarker>();
            Assert.IsNotEmpty(clusters);
            foreach (ContentEditorMapMarker cluster in clusters)
            {
                PlanetSectorClusterView gameplay = cluster.GetComponent<PlanetSectorClusterView>();
                Assert.IsNotNull(gameplay);
                Assert.AreEqual(
                    cluster.Record.Planets.Count,
                    gameplay
                        .GetComponentsInChildren<RawImage>()
                        .Count(image =>
                            image.name.StartsWith("Star", StringComparison.Ordinal)
                            && image.name != "StarImageTemplate"
                        )
                );
                RectTransform map = (RectTransform)
                    cluster.GetComponentInParent<ContentEditorGalaxyPreview>().transform.parent;
                Texture background = map.GetComponent<RawImage>().texture;
                float scale = map.rect.width / UILayout.GetTextureSourceWidth(background);
                RawImage[] stars = gameplay
                    .GetComponentsInChildren<RawImage>()
                    .Where(image =>
                        image.name.StartsWith("Star", StringComparison.Ordinal)
                        && image.name != "StarImageTemplate"
                    )
                    .ToArray();
                cluster.OnPointerEnter(new PointerEventData(EventSystem.current));
                for (int index = 0; index < stars.Length; index++)
                {
                    RawImage star = stars[index];
                    ContentPackEditorRecord planet = cluster.Record.Planets[index];
                    Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                        map,
                        star.rectTransform
                    );
                    Vector2Int source = UILayout.GetTextureSourceSize(star.texture);
                    Assert.That(
                        bounds.size.x,
                        Is.EqualTo(source.x * scale).Within(0.1f),
                        planet.Name
                            + " star width; rect="
                            + star.rectTransform.rect
                            + "; starScale="
                            + star.transform.localScale
                            + "; clusterScale="
                            + cluster.transform.localScale
                            + "; map="
                            + map.rect
                            + "; texture="
                            + star.texture.width
                            + "x"
                            + star.texture.height
                    );
                    Assert.That(
                        bounds.center.x,
                        Is.EqualTo(map.rect.xMin + (planet.X + source.x / 2f) * scale).Within(0.1f),
                        planet.Name + " star position"
                    );
                    Assert.That(
                        bounds.center.y,
                        Is.EqualTo(map.rect.yMax - (planet.Y + source.y / 2f) * scale).Within(0.1f),
                        planet.Name + " star vertical position"
                    );
                }
                cluster.OnPointerExit(new PointerEventData(EventSystem.current));
            }
            Assert.IsFalse(project.IsDirty);
        }

        /// <summary>
        /// Presents the selected sector without constructing its property inspector.
        /// </summary>
        /// <param name="record">The selected map definition.</param>
        private void SelectRecord(ContentPackEditorRecord record)
        {
            view.gameObject.SetActive(true);
            view.Render(
                record,
                record.Kind == "PlanetSector"
                    ? record
                    : ContentPackEditorProjector.GetParentSector(record)
            );
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Presents the galaxy overview requested by its navigation projection.
        /// </summary>
        /// <param name="page">The galaxy page.</param>
        private void SelectPage(ContentEditorTreeNode page)
        {
            view.gameObject.SetActive(true);
            view.Render(page.Record ?? page.Catalog.DocumentRecord, null);
            Canvas.ForceUpdateCanvases();
        }
    }
}
