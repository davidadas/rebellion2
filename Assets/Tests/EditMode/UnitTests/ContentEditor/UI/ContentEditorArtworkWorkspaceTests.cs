using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    [Category("RequiresMedia")]
    public sealed class ContentEditorArtworkWorkspaceTests : ContentEditorViewTestBase
    {
        private Texture2D small;
        private Texture2D large;

        /// <summary>
        /// Releases the fixture textures and editor view.
        /// </summary>
        [TearDown]
        public void ReleaseImages()
        {
            if (small != null)
                Object.DestroyImmediate(small);
            if (large != null)
                Object.DestroyImmediate(large);
        }

        [Test]
        public void CaptureReopen_ProjectRefresh_RetainsTheCardLayerAndViewport()
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(800, 500);
            Canvas.ForceUpdateCanvases();
            var commands = new ArtworkEffectActions();
            int reopened = -1;
            commands.Reopen = output =>
            {
                Texture2D oldSmall = small;
                Texture2D oldLarge = large;
                commands.ReadCurrent = null;
                ShowArtwork(commands, initialOutput: output);
                Object.DestroyImmediate(oldSmall);
                Object.DestroyImmediate(oldLarge);
                reopened = output;
            };
            var workspace = ShowArtwork(commands);
            GetButton(workspace, "ArtworkOutput1").onClick.Invoke();
            GetButton(workspace, "ActualPixels").onClick.Invoke();
            var canvas = workspace.GetComponentInChildren<ContentEditorArtworkCanvas>();
            Vector2 center = RectTransformUtility.WorldToScreenPoint(
                null,
                canvas.transform.position
            );
            canvas.OnScroll(
                new PointerEventData(EventSystem.current)
                {
                    position = center,
                    scrollDelta = new Vector2(0, 20),
                }
            );
            var drag = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Middle,
                pressPosition = center,
                position = center + new Vector2(10, 10),
            };
            canvas.OnBeginDrag(drag);
            canvas.OnDrag(drag);
            canvas.OnEndDrag(drag);
            var before = canvas.CaptureView();
            Assert.AreNotEqual(Vector2.zero, before.Pan);
            var restore = view.CaptureArtworkReopen();

            workspace.Dismiss();
            restore();

            Assert.AreEqual(1, reopened);
            Assert.IsTrue(workspace.gameObject.activeSelf);
            Assert.AreEqual(before, canvas.CaptureView());
        }

        [Test]
        public void CaptureReopen_DraftWithoutReloadOwner_ReturnsNoRestoreAction()
        {
            ShowArtwork();
            Assert.IsNull(view.CaptureArtworkReopen());
        }

        [TestCase(600)]
        [TestCase(800)]
        [TestCase(1600)]
        public void ShowGeneratedArtwork_CardVariantsButton_DoesNotOverlapSaveOrTitle(int width)
        {
            ((RectTransform)view.transform).sizeDelta = new Vector2(width, 1000);
            Canvas.ForceUpdateCanvases();
            int clicked = 0;
            var workspace = ShowArtwork(
                new ArtworkEffectActions { ApplyToAllCards = () => clicked++ }
            );
            Canvas.ForceUpdateCanvases();
            var button = GetButton(workspace, "ApplyArtworkToAllCards");
            button.onClick.Invoke();
            Assert.AreEqual(1, clicked);
            Assert.AreEqual("Apply to all cards", button.GetComponentInChildren<TMP_Text>().text);
            var other = GetButton(workspace, "SaveArtwork");
            var first = new Vector3[4];
            var second = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(first);
            ((RectTransform)other.transform).GetWorldCorners(second);
            Assert.IsFalse(
                new Rect(first[0], first[2] - first[0]).Overlaps(
                    new Rect(second[0], second[2] - second[0])
                )
            );
            workspace
                .GetComponentsInChildren<TMP_Text>()
                .Single(label => label.name == "ArtworkTitle")
                .rectTransform.GetWorldCorners(second);
            Assert.IsFalse(
                new Rect(first[0], first[2] - first[0]).Overlaps(
                    new Rect(second[0], second[2] - second[0])
                )
            );
            Assert.GreaterOrEqual(((RectTransform)button.transform).anchoredPosition.x, 0);
        }

        [Test]
        public void ShowGeneratedArtwork_CardVariantsButton_HasAFilledBackgroundBesideSave()
        {
            var workspace = ShowArtwork(new ArtworkEffectActions { ApplyToAllCards = () => { } });
            var apply = GetButton(workspace, "ApplyArtworkToAllCards");
            var save = GetButton(workspace, "SaveArtwork");
            Assert.AreNotEqual(
                apply.transform.parent.GetComponent<Image>().color,
                apply.targetGraphic.color
            );
            Assert.AreEqual(save.targetGraphic.color, apply.targetGraphic.color);
            Assert.AreEqual(
                ((RectTransform)save.transform).anchoredPosition.y,
                ((RectTransform)apply.transform).anchoredPosition.y
            );
            float gap =
                ((RectTransform)save.transform).anchoredPosition.x
                - (
                    ((RectTransform)apply.transform).anchoredPosition.x
                    + ((RectTransform)apply.transform).rect.width
                );
            Assert.AreEqual(8, gap);
        }

        [Test]
        public void ShowGeneratedArtwork_ReusedForNonShipDraft_HidesPreviousBatchAction()
        {
            var workspace = ShowArtwork(new ArtworkEffectActions { ApplyToAllCards = () => { } });
            Assert.IsTrue(GetButton(workspace, "ApplyArtworkToAllCards").gameObject.activeSelf);
            ShowArtwork();
            Assert.IsFalse(
                workspace
                    .GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "ApplyArtworkToAllCards")
                    .gameObject.activeSelf
            );
        }

        [Test]
        public void ShowGeneratedArtwork_WithMultipleSizes_DisplaysThePressedOutput()
        {
            var workspace = ShowArtwork();
            GetButton(workspace, "ArtworkOutput1").onClick.Invoke();

            var canvas = workspace.GetComponentInChildren<ContentEditorArtworkCanvas>();
            var backing = canvas.transform.Find("Artwork/Backing").GetComponent<RawImage>();
            Assert.That(backing.texture, Is.SameAs(large));
        }

        [Test]
        public void ShowGeneratedArtwork_OverlappingLayers_SelectsTheLayerNamedInTheList()
        {
            int selected = -1;
            var workspace = ShowArtwork(
                new ArtworkEffectActions { Select = index => selected = index }
            );
            foreach (Button button in workspace.GetComponentsInChildren<Button>())
                if (
                    button.name == "SelectLayer"
                    && button.GetComponentInChildren<TMP_Text>().text == "Lower wake"
                )
                    button.onClick.Invoke();

            Assert.That(selected, Is.EqualTo(0));
        }

        [Test]
        public void ShowGeneratedArtwork_DecreaseScale_ResizesTheSelectedWake()
        {
            (int, int)? requested = null;
            var workspace = ShowArtwork(
                new ArtworkEffectActions
                {
                    Resize = (index, percent) => requested = (index, percent),
                }
            );
            GetButton(workspace, "DecreaseLayerScale").onClick.Invoke();

            Assert.That(requested, Is.EqualTo((1, 90)));
        }

        [Test]
        public void TryCancel_UnsavedArtwork_PresentsSaveOrDiscard()
        {
            var workspace = ShowArtwork(changed: true);
            workspace.TryCancel();

            Assert.That(workspace.gameObject.activeSelf, Is.True);
            Assert.That(
                view.GetComponentInChildren<ActionDialogView>().gameObject.activeSelf,
                Is.True
            );
        }

        [Test]
        public void ShowGeneratedArtwork_WithWakeVariation_OffersTheSeparateAction()
        {
            int requests = 0;
            var workspace = ShowArtwork(
                new ArtworkEffectActions { VaryShipWakes = () => requests++ }
            );
            GetButton(workspace, "VaryShipWakes").onClick.Invoke();

            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void ShowGeneratedArtwork_ImportLayer_RoutesToTheDraftCommand()
        {
            int requests = 0;
            var workspace = ShowArtwork(new ArtworkEffectActions { Import = () => requests++ });

            GetButton(workspace, "ImportArtworkLayer").onClick.Invoke();

            Assert.That(requests, Is.EqualTo(1));
        }

        [TestCase("ArtworkCanvas")]
        [TestCase("LayerInspector")]
        public void TryImportDroppedImage_DraftSurface_ImportsLayer(string surface)
        {
            string imported = null;
            var workspace = ShowArtwork(
                new ArtworkEffectActions { ImportDroppedImage = path => imported = path }
            );
            var target = workspace.transform.Find("Workspace/" + surface).GetComponent<Image>();
            Assert.That(target.raycastTarget, Is.True);

            DropImageOn(target.gameObject, (_, _) => Assert.Fail("Imported into the unit form."));

            Assert.That(imported, Is.EqualTo("dropped.png"));
        }

        [Test]
        public void TryImportDroppedImage_ComparingCurrentArtwork_DoesNotImportLayer()
        {
            int imports = 0;
            var workspace = ShowArtwork(
                new ArtworkEffectActions { ImportDroppedImage = _ => imports++ }
            );
            GetButton(workspace, "CurrentPreview").onClick.Invoke();
            var canvas = workspace.GetComponentInChildren<ContentEditorArtworkCanvas>();

            DropImageOn(canvas.gameObject, (_, _) => Assert.Fail("Imported into the unit form."));

            Assert.That(imports, Is.Zero);
        }

        [Test]
        public void ShowGeneratedArtwork_WithoutWakeVariation_HidesTheSeparateAction()
        {
            var workspace = ShowArtwork();

            var variation = GetButton(workspace, "VaryShipWakes", includeInactive: true);
            Assert.That(variation.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ShowGeneratedArtwork_WithWakeOnlyOnLargeCard_OmitsItFromSmallCard()
        {
            var workspace = ShowArtwork(sizeSpecificLayers: true);

            Assert.That(GetLayerNames(workspace), Does.Not.Contain("Upper wake"));
        }

        [Test]
        public void SelectOutput_WithWakeOnlyOnLargeCard_ShowsItOnLargeCard()
        {
            var workspace = ShowArtwork(sizeSpecificLayers: true);
            GetButton(workspace, "ArtworkOutput1").onClick.Invoke();

            Assert.That(GetLayerNames(workspace), Does.Contain("Upper wake"));
        }

        [Test]
        public void SelectOutput_WithMoreWakesInTheSelectedSize_ExpandsTheLayerList()
        {
            var workspace = ShowArtwork(sizeSpecificLayers: true);
            var list = workspace.GetComponentInChildren<ScrollAreaLayout>();
            float smallHeight = ((RectTransform)list.transform).rect.height;
            GetButton(workspace, "ArtworkOutput1").onClick.Invoke();

            Assert.That(((RectTransform)list.transform).rect.height, Is.GreaterThan(smallHeight));
        }

        [Test]
        public void MoveOrder_WithWakeOnlyOnLargeCard_TargetsNextVisibleWake()
        {
            (int, int)? requested = null;
            var workspace = ShowArtwork(
                new ArtworkEffectActions
                {
                    Reorder = (index, target) => requested = (index, target),
                },
                sizeSpecificLayers: true
            );
            GetButton(workspace, "MoveLayerForward").onClick.Invoke();

            Assert.That(requested, Is.EqualTo((0, 2)));
        }

        [Test]
        public void MoveOrder_LinkedUnitArtwork_TargetsAdjacentEffect()
        {
            (int, int)? requested = null;
            var workspace = ShowArtwork(
                new ArtworkEffectActions
                {
                    Reorder = (index, target) => requested = (index, target),
                },
                linkedFirst: true
            );
            foreach (Button button in workspace.GetComponentsInChildren<Button>())
                if (
                    button.name == "SelectLayer"
                    && button.GetComponentInChildren<TMP_Text>().text == "Lower wake"
                )
                    button.onClick.Invoke();
            GetButton(workspace, "MoveLayerForward").onClick.Invoke();

            Assert.That(requested, Is.EqualTo((0, 1)));
        }

        [Test]
        public void ShowGeneratedArtwork_CroppedLayer_PreservesTheVisibleAspectRatio()
        {
            var workspace = ShowArtwork(crop: new Rect(0.25f, 0, 0.5f, 1));
            ContentEditorArtworkLayerRow row = workspace
                .GetComponentsInChildren<ContentEditorArtworkLayerRow>()
                .Single(item =>
                    item.GetComponentsInChildren<TMP_Text>()
                        .Any(label => label.text == "Lower wake")
                );
            AspectRatioFitter thumbnail = row.GetComponentInChildren<AspectRatioFitter>();

            Assert.AreEqual(1f, thumbnail.aspectRatio);
        }

        /// <summary>
        /// Opens the artwork workspace with the fixture layer stacks.
        /// </summary>
        /// <param name="commands">The actions assigned to the workspace.</param>
        /// <param name="changed">Whether the draft starts with unsaved changes.</param>
        /// <param name="sizeSpecificLayers">Whether each output shows a different layer stack.</param>
        /// <param name="linkedFirst">Whether the first fixture layer represents linked unit artwork.</param>
        /// <param name="crop">The optional visible source rectangle of the first layer.</param>
        /// <param name="initialOutput">The card selected when restoring an artwork workspace.</param>
        /// <returns>The rendered workspace.</returns>
        private ContentEditorArtworkWorkspace ShowArtwork(
            ArtworkEffectActions commands = null,
            bool changed = false,
            bool sizeSpecificLayers = false,
            bool linkedFirst = false,
            Rect? crop = null,
            int initialOutput = 0
        )
        {
            small = new Texture2D(32, 16);
            large = new Texture2D(64, 32);
            var layers = new[]
            {
                new ArtworkEffectLayerPreview(
                    0,
                    "Lower wake",
                    small,
                    new Rect(0, 0, 1, 1),
                    linked: linkedFirst,
                    uv: crop
                ),
                new ArtworkEffectLayerPreview(
                    1,
                    "Upper wake",
                    sizeSpecificLayers ? null : small,
                    new Rect(0, 0, 1, 1),
                    scalePercent: 100
                ),
                new ArtworkEffectLayerPreview(
                    2,
                    "Outer wake",
                    sizeSpecificLayers ? small : null,
                    new Rect(0, 0, 1, 1)
                ),
            };
            var largeLayers = sizeSpecificLayers
                ? new[]
                {
                    layers[0],
                    new ArtworkEffectLayerPreview(1, "Upper wake", large, new Rect(0, 0, 1, 1)),
                    layers[2],
                }
                : layers;
            var frames = new[]
            {
                new ArtworkEffectPreviewFrame("Small", small, layers),
                new ArtworkEffectPreviewFrame("Large", large, largeLayers),
            };
            commands ??= new ArtworkEffectActions();
            commands.ReadCurrent ??= () => frames;
            commands.AddLabel = "Add wake";
            commands.RebuildLabel = "Rebuild travel artwork";
            view.ShowGeneratedArtwork(
                "Travel artwork",
                "Test unit",
                frames,
                initialOutput,
                1,
                changed,
                commands
            );
            Canvas.ForceUpdateCanvases();
            return view.GetComponentInChildren<ContentEditorArtworkWorkspace>();
        }

        /// <summary>
        /// Reads the active layer rows in display order.
        /// </summary>
        /// <param name="workspace">The artwork workspace.</param>
        /// <returns>The visible layer names.</returns>
        private static string[] GetLayerNames(ContentEditorArtworkWorkspace workspace) =>
            workspace
                .GetComponentsInChildren<Button>()
                .Where(button => button.name == "SelectLayer")
                .Select(button => button.GetComponentInChildren<TMP_Text>().text)
                .ToArray();

        /// <summary>
        /// Finds a button in the artwork workspace.
        /// </summary>
        /// <param name="workspace">The artwork workspace.</param>
        /// <param name="name">The button's object name.</param>
        /// <param name="includeInactive">Whether to include inactive objects.</param>
        /// <returns>The matching button.</returns>
        private static Button GetButton(
            ContentEditorArtworkWorkspace workspace,
            string name,
            bool includeInactive = false
        )
        {
            foreach (Button button in workspace.GetComponentsInChildren<Button>(includeInactive))
                if (button.name == name)
                    return button;
            throw new AssertionException("Artwork button was missing.");
        }
    }
}
