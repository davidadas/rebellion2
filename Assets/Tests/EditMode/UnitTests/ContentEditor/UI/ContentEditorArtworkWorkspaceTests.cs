using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorArtworkWorkspaceTests : ContentEditorViewTestBase
    {
        private Texture2D small;
        private Texture2D large;

        [TearDown]
        public void ReleaseImages()
        {
            if (small != null)
                Object.DestroyImmediate(small);
            if (large != null)
                Object.DestroyImmediate(large);
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

        private ContentEditorArtworkWorkspace ShowArtwork(
            ArtworkEffectActions commands = null,
            bool changed = false,
            bool sizeSpecificLayers = false
        )
        {
            small = new Texture2D(32, 16);
            large = new Texture2D(64, 32);
            var layers = new[]
            {
                new ArtworkEffectLayerPreview(0, "Lower wake", small, new Rect(0, 0, 1, 1)),
                new ArtworkEffectLayerPreview(
                    1,
                    "Upper wake",
                    sizeSpecificLayers ? null : small,
                    new Rect(0, 0, 1, 1)
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
                new ArtworkEffectPreviewFrame("Small", small, null, layers),
                new ArtworkEffectPreviewFrame("Large", large, null, largeLayers),
            };
            commands ??= new ArtworkEffectActions();
            commands.AddLabel = "Add wake";
            commands.RebuildLabel = "Rebuild travel artwork";
            view.ShowGeneratedArtwork(
                "Travel artwork",
                "Test unit",
                frames,
                0,
                1,
                changed,
                commands
            );
            Canvas.ForceUpdateCanvases();
            return view.GetComponentInChildren<ContentEditorArtworkWorkspace>();
        }

        private static string[] GetLayerNames(ContentEditorArtworkWorkspace workspace) =>
            workspace
                .GetComponentsInChildren<Button>()
                .Where(button => button.name == "SelectLayer")
                .Select(button => button.GetComponentInChildren<TMP_Text>().text)
                .ToArray();

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
