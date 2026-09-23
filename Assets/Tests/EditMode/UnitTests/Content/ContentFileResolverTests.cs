using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentFileResolverTests
    {
        private string _root;
        private string _contentRoot;
        private string _packRoot;

        /// <summary>
        /// Creates isolated base-content and pack directories for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "rebellion2-content-resolver",
                Guid.NewGuid().ToString("N")
            );
            _contentRoot = Path.Combine(_root, "Content");
            _packRoot = Path.Combine(_contentRoot, "Packs", "Base");
            Directory.CreateDirectory(_packRoot);
        }

        /// <summary>
        /// Removes the isolated content directory after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [Test]
        public void ResolveFile_ModDoesNotContainAddress_FallsBackToBasePack()
        {
            string baseFile = WriteFile(_packRoot, "Data/ships.xml", "base");
            string modContent = Path.Combine(_root, "Mods", "Example", "Content");
            Directory.CreateDirectory(modContent);
            ContentFileResolver resolver = new ContentFileResolver(
                _contentRoot,
                _packRoot,
                new[] { modContent }
            );

            Assert.AreEqual(baseFile, resolver.ResolveFile("Pack/Data/ships.xml"));
        }

        [Test]
        public void ResolveFile_MultipleModsContainAddress_LastModWins()
        {
            WriteFile(_packRoot, "Data/ships.xml", "base");
            string firstMod = Path.Combine(_root, "Mods", "First", "Content");
            string secondMod = Path.Combine(_root, "Mods", "Second", "Content");
            WriteFile(firstMod, "Pack/Data/ships.xml", "first");
            string winningFile = WriteFile(secondMod, "Pack/Data/ships.xml", "second");
            ContentFileResolver resolver = new ContentFileResolver(
                _contentRoot,
                _packRoot,
                new[] { firstMod, secondMod }
            );

            Assert.AreEqual(winningFile, resolver.ResolveFile("Pack/Data/ships.xml"));
        }

        [Test]
        public void ResolveFile_ModUsesDifferentExtension_ModStillWins()
        {
            WriteFile(_packRoot, "UI/portrait.png", "base");
            string modContent = Path.Combine(_root, "Mods", "Example", "Content");
            string modFile = WriteFile(modContent, "Pack/UI/portrait.jpg", "mod");
            ContentFileResolver resolver = new ContentFileResolver(
                _contentRoot,
                _packRoot,
                new[] { modContent }
            );

            Assert.AreEqual(
                modFile,
                resolver.ResolveFile("Pack/UI/portrait", ".png", ".jpg", ".jpeg")
            );
        }

        [Test]
        public void EnumerateFileAddresses_OverlappingFiles_LayersAndRemovesDuplicates()
        {
            WriteFile(_packRoot, "UI/base.png", "base");
            WriteFile(_packRoot, "UI/replaced.png", "base");
            string modContent = Path.Combine(_root, "Mods", "Example", "Content");
            WriteFile(modContent, "Pack/UI/replaced.png", "mod");
            WriteFile(modContent, "Pack/UI/added.png", "mod");
            ContentFileResolver resolver = new ContentFileResolver(
                _contentRoot,
                _packRoot,
                new[] { modContent }
            );

            string[] addresses = resolver.EnumerateFileAddresses("Pack/UI").ToArray();

            CollectionAssert.AreEqual(
                new[] { "Pack/UI/added.png", "Pack/UI/base.png", "Pack/UI/replaced.png" },
                addresses
            );
        }

        [Test]
        public void Discover_CompatibleMod_LoadsDefinitionAndContent()
        {
            WriteFile(_packRoot, "Data/ships.xml", "base");
            string modRoot = Path.Combine(_root, "Mods", "ShipRebalance");
            WriteFile(
                modRoot,
                "mod.xml",
                "<ContentModDefinition><ID>ship-rebalance</ID><Version>1.0.0</Version>"
                    + "<DisplayName>Ship Rebalance</DisplayName><BasePackID>base-pack</BasePackID>"
                    + "</ContentModDefinition>"
            );
            string modFile = WriteFile(modRoot, "Content/Pack/Data/ships.xml", "mod");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack"
            );

            Assert.AreEqual(1, resolver.Mods.Count);
            Assert.AreEqual("ship-rebalance", resolver.Mods[0].ID);
            Assert.AreEqual(modFile, resolver.ResolveFile("Pack/Data/ships.xml"));
        }

        [Test]
        public void Discover_DisabledCompatibleMod_ListsButDoesNotLoadMod()
        {
            WriteFile(_packRoot, "Data/ships.xml", "base");
            string modRoot = Path.Combine(_root, "Mods", "ShipRebalance");
            WriteFile(
                modRoot,
                "mod.xml",
                "<ContentModDefinition><ID>ship-rebalance</ID><Version>1.0.0</Version>"
                    + "<DisplayName>Ship Rebalance</DisplayName><BasePackID>base-pack</BasePackID>"
                    + "</ContentModDefinition>"
            );
            WriteFile(modRoot, "Content/Pack/Data/ships.xml", "mod");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                new[] { "ship-rebalance" }
            );

            Assert.IsEmpty(resolver.Mods);
            Assert.AreEqual("ship-rebalance", resolver.AvailableMods.Single().ID);
            Assert.AreEqual("base", File.ReadAllText(resolver.ResolveFile("Pack/Data/ships.xml")));
        }

        [Test]
        public void ResolveFile_AddressCrossesScope_ThrowsArgumentException()
        {
            ContentFileResolver resolver = new ContentFileResolver(_contentRoot, _packRoot);

            Assert.Throws<ArgumentException>(() =>
                resolver.ResolveFile("Application/../Pack/Data/ships.xml")
            );
        }

        [Test]
        public void ResolveFile_AddressLeavesPackRoot_ThrowsArgumentException()
        {
            ContentFileResolver resolver = new ContentFileResolver(_contentRoot, _packRoot);

            Assert.Throws<ArgumentException>(() => resolver.ResolveFile("Pack/../../outside.xml"));
        }

        [Test]
        public void ResolveFile_BackslashAddress_UsesPlatformPathHandling()
        {
            string baseFile = WriteFile(_packRoot, "Data/ships.xml", "base");
            ContentFileResolver resolver = new ContentFileResolver(_contentRoot, _packRoot);

            Assert.AreEqual(baseFile, resolver.ResolveFile(@"Pack\Data\ships.xml"));
        }

        [Test]
        public void Discover_UnrelatedIncompleteMod_IgnoresDefinition()
        {
            string modRoot = Path.Combine(_root, "Mods", "Unrelated");
            WriteFile(
                modRoot,
                "mod.xml",
                "<ContentModDefinition><BasePackID>other-pack</BasePackID>"
                    + "</ContentModDefinition>"
            );

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack"
            );

            Assert.IsEmpty(resolver.Mods);
        }

        /// <summary>
        /// Writes one test file beneath an isolated content root.
        /// </summary>
        /// <param name="basePath">The absolute base directory.</param>
        /// <param name="relativePath">The relative file path.</param>
        /// <param name="contents">The file contents.</param>
        /// <returns>The absolute written file path.</returns>
        private static string WriteFile(string basePath, string relativePath, string contents)
        {
            string normalizedRelativePath = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            string path = Path.Combine(basePath, normalizedRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
            return path;
        }
    }
}
