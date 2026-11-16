using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    public sealed class ContentLibraryViewTests
    {
        private ContentPackEditorView editor;

        /// <summary>
        /// Instantiates only the authored view, without booting an application or game session.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            editor = UIComponentTestHelper.InstantiatePrefabComponent<ContentPackEditorView>(
                "Assets/Prefabs/UI/ContentPackEditor/ContentPackEditorRoot.prefab"
            );
            editor.Initialize(new ContentPackDefinition[0], "/content");
        }

        /// <summary>
        /// Releases the view and its pooled controls.
        /// </summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(editor.gameObject);

        [Test]
        public void Initialize_NoEditingProject_ShowsLibraryImmediately()
        {
            Assert.IsTrue(editor.Library.gameObject.activeSelf);
            Assert.IsTrue(
                editor
                    .Library.GetComponentsInChildren<Button>()
                    .Any(button => button.name == "Browse")
            );
        }

        [Test]
        public void Render_EmptyCollection_ShowsEmptyMessage()
        {
            editor.Library.Render(
                new ContentLibraryPresentation { EmptyMessage = "No installed content." }
            );
            TMP_Text empty = editor
                .Library.GetComponentsInChildren<TMP_Text>()
                .Single(text => text.name == "EmptyLibrary");
            Assert.AreEqual("No installed content.", empty.text);
            Assert.IsFalse(
                editor
                    .Library.GetComponentsInChildren<Button>()
                    .Any(button => button.name == "RemoveLibraryItem")
            );
        }

        [Test]
        public void Render_ProtectedContent_DoesNotOfferRemoval()
        {
            ContentLibraryRow row = new ContentLibraryRow
            {
                Key = "base",
                Name = "Base",
                Status = "Built-in",
                Version = "1",
            };
            editor.Library.Render(
                new ContentLibraryPresentation { Rows = new[] { row }, Selected = row }
            );
            Button remove = editor
                .Library.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "RemoveLibraryItem");
            Assert.IsFalse(remove.gameObject.activeSelf);
        }

        [Test]
        public void Render_PopulatedCollection_ShowsSelectableRow()
        {
            ContentLibraryRow row = new ContentLibraryRow
            {
                Key = "base",
                Name = "Base pack",
                Version = "1",
            };
            editor.Library.Render(
                new ContentLibraryPresentation { Rows = new[] { row }, Selected = row }
            );
            ContentLibraryRowView visible = editor
                .Library.GetComponentsInChildren<ContentLibraryRowView>()
                .Single();
            Assert.AreEqual(row.Key, visible.Key);
            Assert.That(
                visible.GetComponentsInChildren<TMP_Text>(),
                Has.Some.Matches<TMP_Text>(text => text.text == row.Name)
            );
        }

        [Test]
        public void Render_RemovableContent_ForwardsExactSelectedIdentity()
        {
            ContentLibraryRow row = new ContentLibraryRow
            {
                Key = "/mods/selected",
                Name = "Selected",
                CanRemove = true,
                Version = "1",
            };
            string requested = null;
            editor.Library.RemoveRequested += key => requested = key;
            editor.Library.Render(
                new ContentLibraryPresentation { Rows = new[] { row }, Selected = row }
            );
            editor
                .Library.GetComponentsInChildren<Button>()
                .Single(button => button.name == "RemoveLibraryItem")
                .onClick.Invoke();
            Assert.AreEqual(row.Key, requested);
        }
    }
}
