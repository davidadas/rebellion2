using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    public sealed class ContentLibraryTests
    {
        private string root;
        private ContentLibrary library;

        /// <summary>
        /// Creates an isolated installation with a protected base pack.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "reb2-library-" + Guid.NewGuid().ToString("N"));
            Write(
                "Content/Packs/Base/pack.xml",
                "<ContentPack><ID>base</ID><DisplayName>Base pack</DisplayName><Version>1</Version></ContentPack>"
            );
            library = new ContentLibrary(Path.Combine(root, "Content"));
        }

        /// <summary>
        /// Removes only fixture-owned content.
        /// </summary>
        [TearDown]
        public void TearDown() => Directory.Delete(root, true);

        [Test]
        public void Discover_LocalAndBuiltInPacks_ReturnsBothWithoutLoadingGameData()
        {
            Write(
                "Mods/custom/pack.xml",
                "<ContentPack><ID>custom</ID><Version>2</Version><BasePackID>base</BasePackID></ContentPack>"
            );
            ContentLibraryItem[] items = library.Discover().ToArray();
            Assert.AreEqual(2, items.Length);
            Assert.IsTrue(items.Single(item => item.ID == "base").IsReadOnly);
            Assert.IsFalse(items.Single(item => item.ID == "custom").IsReadOnly);
            Assert.AreEqual("2", items.Single(item => item.ID == "custom").Version);
        }

        [Test]
        public void Discover_EmptyDisplayName_UsesContentIdentifier()
        {
            Write(
                "Mods/custom/pack.xml",
                "<ContentPack><ID>custom</ID><DisplayName></DisplayName><Version>1</Version></ContentPack>"
            );
            Assert.AreEqual("custom", library.Discover().Single(item => item.ID == "custom").Name);
        }

        [Test]
        public void Discover_OverlayForMissingPack_RemainsVisibleInGlobalLibrary()
        {
            Write(
                "Mods/overlay/mod.xml",
                "<ContentModDefinition><ID>overlay</ID><Version>1</Version><DisplayName>Overlay</DisplayName><BasePackID>missing</BasePackID><BasePackVersion>1</BasePackVersion></ContentModDefinition>"
            );
            ContentLibraryItem item = library.Discover().Single(entry => entry.ID == "overlay");
            Assert.IsFalse(item.IsPack);
            Assert.AreEqual("missing", item.BasePackID);
        }

        [Test]
        public void Discover_MalformedManifest_RetainsInspectableEntry()
        {
            Write("Mods/broken/pack.xml", "<ContentPack><ID>");
            ContentLibraryItem item = library.Discover().Single(entry => entry.Name == "broken");
            Assert.IsNotEmpty(item.Error);
            Assert.IsFalse(item.IsReadOnly);
        }

        [Test]
        public void Discover_InvalidManagedRecord_RetainsManifestAndReportsIssue()
        {
            Write(
                "Mods/custom/pack.xml",
                "<ContentPack><ID>custom</ID><Version>1</Version></ContentPack>"
            );
            Write("Mods/custom/.modio.json", "{broken");
            ContentLibraryItem item = library.Discover().Single(entry => entry.ID == "custom");
            Assert.IsNotEmpty(item.Error);
            Assert.IsNull(item.Installation);
        }

        [Test]
        public void Discover_UnrelatedFolder_DoesNotCreateContentEntry()
        {
            Write("Mods/notes/readme.txt", "notes");
            Assert.AreEqual(1, library.Discover().Count);
        }

        [Test]
        public void Remove_BuiltInPack_ProtectsInstalledContent()
        {
            Assert.Throws<IOException>(() =>
                library.Remove(Path.Combine(root, "Content/Packs/Base"))
            );
            Assert.IsTrue(File.Exists(Path.Combine(root, "Content/Packs/Base/pack.xml")));
        }

        [Test]
        public void Remove_DependentPack_ProtectsRequiredBase()
        {
            Write(
                "Mods/parent/pack.xml",
                "<ContentPack><ID>parent</ID><Version>1</Version></ContentPack>"
            );
            Write(
                "Mods/child/pack.xml",
                "<ContentPack><ID>child</ID><Version>1</Version><BasePackID>parent</BasePackID></ContentPack>"
            );
            Assert.Throws<IOException>(() => library.Remove(Path.Combine(root, "Mods/parent")));
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Mods/parent")));
        }

        [Test]
        public void Remove_LocalPack_DeletesOnlySelectedInstallation()
        {
            Write(
                "Mods/first/pack.xml",
                "<ContentPack><ID>first</ID><Version>1</Version></ContentPack>"
            );
            Write(
                "Mods/second/pack.xml",
                "<ContentPack><ID>second</ID><Version>1</Version></ContentPack>"
            );
            library.Remove(Path.Combine(root, "Mods/first"));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "Mods/first")));
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Mods/second")));
        }

        [Test]
        public void Remove_UnknownDirectory_RejectsUnrelatedFiles()
        {
            Write("unrelated/file.txt", "keep");
            Assert.Throws<IOException>(() => library.Remove(Path.Combine(root, "unrelated")));
            Assert.IsTrue(File.Exists(Path.Combine(root, "unrelated/file.txt")));
        }

        /// <summary>
        /// Writes one fixture-owned file and creates its parent directory.
        /// </summary>
        /// <param name="relative">The relative file path.</param>
        /// <param name="text">The file content.</param>
        private void Write(string relative, string text)
        {
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }
    }
}
