using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    [Category("RequiresMedia")]
    public sealed class ContentEditorGameplayArtworkEditorTests : ContentEditorViewTestBase
    {
        private Texture2D texture;
        private ContentPackEditorRecord officer;

        /// <summary>
        /// Creates borrowed fixture artwork and selects an officer definition.
        /// </summary>
        [SetUp]
        public void SetUpArtwork()
        {
            texture = new Texture2D(24, 12);
            officer = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Officer");
        }

        /// <summary>
        /// Releases the fixture texture after the modal releases its borrowed images.
        /// </summary>
        [TearDown]
        public void TearDownArtwork()
        {
            view.GetComponentInChildren<ContentEditorArtworkWorkspace>(true).Dismiss();
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void Show_OfficerCombinedArtwork_OffersOrderingWithoutImageGeneration()
        {
            OpenEditor();

            Assert.IsNotNull(Button("MoveLayerForward"));
            Assert.IsFalse(Button("AddArtworkLayer", true).gameObject.activeSelf);
            Assert.IsFalse(Button("ImportArtworkLayer", true).gameObject.activeSelf);
            Assert.IsNull(officer.Element.Element("ArtworkLayerOrder"));
        }

        [Test]
        public void Save_InjuryAboveCapture_StoresOrderWithoutChangingImageReferences()
        {
            string[] artwork = officer
                .GetFields()
                .Where(field => field.IsMedia)
                .Select(field => field.Value)
                .ToArray();
            OpenEditor();
            SelectLayer("Captured");
            Button("MoveLayerBackward").onClick.Invoke();
            Button("SaveArtwork").onClick.Invoke();

            CollectionAssert.AreEqual(
                new[] { "Entity", "Travel", "Captured", "Damage" },
                officer.GetField("ArtworkLayerOrder").Values
            );
            CollectionAssert.AreEqual(
                artwork,
                officer.GetFields().Where(field => field.IsMedia).Select(field => field.Value)
            );
        }

        [Test]
        public void Save_ReopenedPack_RetainsOfficerLayerOrder()
        {
            OpenEditor(() => ContentPackEditorStorage.Save(project));
            SelectLayer("Captured");
            Button("MoveLayerBackward").onClick.Invoke();
            Button("SaveArtwork").onClick.Invoke();
            using var reopened = ContentPackEditorStorage.OpenFolder(
                TestContent.Pack.ContentRootPath,
                folder
            );
            ContentPackEditorRecord restored = reopened
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record => record.Kind == "Officer" && record.ID == officer.ID);

            CollectionAssert.AreEqual(
                new[] { "Entity", "Travel", "Captured", "Damage" },
                restored.GetField("ArtworkLayerOrder").Values
            );
        }

        [Test]
        public void Undo_ReorderedLayers_RestoresSavedPreview()
        {
            OpenEditor();
            SelectLayer("Captured");
            Button("MoveLayerBackward").onClick.Invoke();
            Button("UndoArtwork").onClick.Invoke();

            Assert.IsFalse(Button("SaveArtwork").interactable);
            CollectionAssert.AreEqual(
                new[] { "Captured", "Injured", "Travel", "Portrait" },
                LayerNames()
            );
        }

        [Test]
        public void Redo_UndoneReorder_RestoresEditedPreview()
        {
            OpenEditor();
            SelectLayer("Captured");
            Button("MoveLayerBackward").onClick.Invoke();
            Button("UndoArtwork").onClick.Invoke();
            Button("RedoArtwork").onClick.Invoke();

            CollectionAssert.AreEqual(
                new[] { "Injured", "Captured", "Travel", "Portrait" },
                LayerNames()
            );
        }

        [Test]
        public void Show_CurrentPackComparison_ShowsSavedLayerList()
        {
            OpenEditor();
            SelectLayer("Captured");
            Button("MoveLayerBackward").onClick.Invoke();
            Button("CurrentPreview").onClick.Invoke();

            CollectionAssert.AreEqual(
                new[] { "Captured", "Injured", "Travel", "Portrait" },
                LayerNames()
            );
        }

        [Test]
        public void Save_PreviouslyComparedDraft_RefreshesCurrentPackPreview()
        {
            OpenEditor();
            SelectLayer("Captured");
            Button("MoveLayerBackward").onClick.Invoke();
            Button("CurrentPreview").onClick.Invoke();
            Button("SaveArtwork").onClick.Invoke();
            Button("CurrentPreview").onClick.Invoke();

            CollectionAssert.AreEqual(
                new[] { "Injured", "Captured", "Travel", "Portrait" },
                LayerNames()
            );
        }

        [Test]
        public void Dismiss_UnsavedLayerChanges_LeavesUnitUnchanged()
        {
            OpenEditor();
            SelectLayer("Captured");
            Button("MoveLayerBackward").onClick.Invoke();
            view.GetComponentInChildren<ContentEditorArtworkWorkspace>().Dismiss();

            Assert.IsNull(officer.Element.Element("ArtworkLayerOrder"));
        }

        [Test]
        public void Save_PersistenceFails_KeepsDraftUnsaved()
        {
            OpenEditor(() => throw new InvalidOperationException("Fixture save failed."));
            SelectLayer("Captured");
            Button("MoveLayerBackward").onClick.Invoke();
            Assert.Throws<InvalidOperationException>(() => Button("SaveArtwork").onClick.Invoke());

            Assert.IsTrue(Button("SaveArtwork").interactable);
        }

        /// <summary>
        /// Opens the real workspace with a small isolated officer composition.
        /// </summary>
        /// <param name="persist">The fixture's pack-saving boundary.</param>
        private void OpenEditor(Action persist = null)
        {
            var layers = new[]
            {
                new ArtworkEffectLayerPreview(
                    0,
                    "Portrait",
                    texture,
                    new Rect(0, 0, 1, 1),
                    linked: true
                ),
                new ArtworkEffectLayerPreview(
                    1,
                    "Travel",
                    texture,
                    new Rect(0, 0, 1, 1),
                    linked: true
                ),
                new ArtworkEffectLayerPreview(
                    2,
                    "Injured",
                    texture,
                    new Rect(0, 0, 1, 1),
                    linked: true
                ),
                new ArtworkEffectLayerPreview(
                    3,
                    "Captured",
                    texture,
                    new Rect(0, 0, 1, 1),
                    linked: true
                ),
            };
            new ContentEditorGameplayArtworkEditor(
                view,
                project,
                officer,
                new ArtworkEffectPreviewFrame("Combined", texture, layers),
                persist ?? (() => { }),
                action => action()
            ).Show();
        }

        /// <summary>
        /// Gets one button in the active artwork workspace.
        /// </summary>
        /// <param name="name">The authored control name.</param>
        /// <param name="inactive">Whether a hidden control is included.</param>
        /// <returns>The matching workspace button.</returns>
        private Button Button(string name, bool inactive = false) =>
            view.GetComponentInChildren<ContentEditorArtworkWorkspace>(true)
                .GetComponentsInChildren<Button>(inactive)
                .Single(button => button.name == name);

        /// <summary>
        /// Selects a linked image through its actual layer-list button.
        /// </summary>
        /// <param name="name">The displayed layer name.</param>
        private void SelectLayer(string name) =>
            view.GetComponentInChildren<ContentEditorArtworkWorkspace>()
                .GetComponentsInChildren<Button>()
                .Single(button =>
                    button.name == "SelectLayer"
                    && button.GetComponentInChildren<TMP_Text>().text == name
                )
                .onClick.Invoke();

        /// <summary>
        /// Reads the visible layer list in front-to-back order.
        /// </summary>
        /// <returns>The labels rendered by the workspace.</returns>
        private string[] LayerNames() =>
            view.GetComponentInChildren<ContentEditorArtworkWorkspace>()
                .GetComponentsInChildren<Button>()
                .Where(button => button.name == "SelectLayer")
                .Select(button => button.GetComponentInChildren<TMP_Text>().text)
                .ToArray();
    }
}
