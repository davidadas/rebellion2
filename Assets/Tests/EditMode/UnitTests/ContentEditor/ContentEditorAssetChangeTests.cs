using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorAssetChangeTests : ContentPackEditorTestBase
    {
        [Test]
        public void Undo_GroupedArtworkReplacement_RestoresImageAndRecipeTogether()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            string address;
            using (project.BeginEdit("First artwork"))
            {
                address = ContentPackEditorStorage.WriteGeneratedPng(
                    project,
                    field,
                    new byte[] { 1 },
                    true,
                    "first recipe"
                );
                project.SetField(field, address);
            }
            using (project.BeginEdit("Next artwork"))
            {
                ContentPackEditorStorage.WriteGeneratedPng(
                    project,
                    field,
                    new byte[] { 2 },
                    true,
                    "next recipe"
                );
                project.SetField(field, address);
            }

            Assert.IsTrue(project.Undo());
            ContentPackEditorField restored = GetField(project);
            Assert.AreEqual(address, restored.Value);
            Assert.AreEqual(
                "first recipe",
                ContentPackEditorStorage.ReadGeneratedLayers(project, restored)
            );
            CollectionAssert.AreEqual(
                new byte[] { 1 },
                File.ReadAllBytes(Path.Combine(project.PackRootPath, address[5..] + ".png"))
            );
            Assert.IsTrue(project.Redo());
            Assert.AreEqual(
                "next recipe",
                ContentPackEditorStorage.ReadGeneratedLayers(project, GetField(project))
            );
        }

        [Test]
        public void Undo_ArtworkChangedExternally_PreservesExternalFileAndHistory()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            string address = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                new byte[] { 1 }
            );
            string target = Path.Combine(project.PackRootPath, address[5..] + ".png");
            File.WriteAllBytes(target, new byte[] { 9 });

            Assert.Throws<IOException>(() => project.Undo());

            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(target));
            Assert.IsTrue(project.CanUndo);
        }

        /// <summary>
        /// Gets the first fixture starfighter's owned display field.
        /// </summary>
        /// <param name="project">The working project.</param>
        /// <returns>The current field wrapper.</returns>
        private static ContentPackEditorField GetField(ContentPackEditorProject project) =>
            project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Starfighter")
                .GetField("DisplayImagePath");
    }
}
