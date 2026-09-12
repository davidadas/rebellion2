using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentFileResolverTests
    {
        private string root;
        private string contentRoot;
        private string packRoot;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(
                Path.GetTempPath(),
                "rebellion2-content-resolver",
                Guid.NewGuid().ToString("N")
            );
            contentRoot = Path.Combine(root, "Content");
            packRoot = Path.Combine(contentRoot, "Packs", "Base");
            Directory.CreateDirectory(packRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void ResolveFile_ModDoesNotContainAddress_FallsBackToBasePack()
        {
            string baseFile = WriteFile(packRoot, "Data/ships.xml", "base");
            string modContent = Path.Combine(root, "Mods", "Example", "Content");
            Directory.CreateDirectory(modContent);
            ContentFileResolver resolver = new ContentFileResolver(
                contentRoot,
                packRoot,
                new[] { modContent }
            );

            Assert.AreEqual(baseFile, resolver.ResolveFile("Pack/Data/ships.xml"));
        }

        [Test]
        public void ResolveFile_MultipleModsContainAddress_LastModWins()
        {
            WriteFile(packRoot, "Data/ships.xml", "base");
            string firstMod = Path.Combine(root, "Mods", "First", "Content");
            string secondMod = Path.Combine(root, "Mods", "Second", "Content");
            WriteFile(firstMod, "Pack/Data/ships.xml", "first");
            string winningFile = WriteFile(secondMod, "Pack/Data/ships.xml", "second");
            ContentFileResolver resolver = new ContentFileResolver(
                contentRoot,
                packRoot,
                new[] { firstMod, secondMod }
            );

            Assert.AreEqual(winningFile, resolver.ResolveFile("Pack/Data/ships.xml"));
        }

        [Test]
        public void ResolveFile_ModUsesDifferentExtension_ModStillWins()
        {
            WriteFile(packRoot, "UI/portrait.png", "base");
            string modContent = Path.Combine(root, "Mods", "Example", "Content");
            string modFile = WriteFile(modContent, "Pack/UI/portrait.jpg", "mod");
            ContentFileResolver resolver = new ContentFileResolver(
                contentRoot,
                packRoot,
                new[] { modContent }
            );

            Assert.AreEqual(
                modFile,
                resolver.ResolveFile("Pack/UI/portrait", ".png", ".jpg", ".jpeg")
            );
        }

        [Test]
        public void EnumerateFileAddresses_LayersFilesAndRemovesDuplicateAddresses()
        {
            WriteFile(packRoot, "UI/base.png", "base");
            WriteFile(packRoot, "UI/replaced.png", "base");
            string modContent = Path.Combine(root, "Mods", "Example", "Content");
            WriteFile(modContent, "Pack/UI/replaced.png", "mod");
            WriteFile(modContent, "Pack/UI/added.png", "mod");
            ContentFileResolver resolver = new ContentFileResolver(
                contentRoot,
                packRoot,
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
            WriteFile(packRoot, "Data/ships.xml", "base");
            string modRoot = Path.Combine(root, "Mods", "ShipRebalance");
            WriteFile(
                modRoot,
                "mod.xml",
                "<ContentModDefinition><ID>ship-rebalance</ID><Version>1.0.0</Version>"
                    + "<DisplayName>Ship Rebalance</DisplayName><BasePackID>base-pack</BasePackID>"
                    + "</ContentModDefinition>"
            );
            string modFile = WriteFile(modRoot, "Content/Pack/Data/ships.xml", "mod");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                contentRoot,
                packRoot,
                "base-pack"
            );

            Assert.AreEqual(1, resolver.Mods.Count);
            Assert.AreEqual("ship-rebalance", resolver.Mods[0].ID);
            Assert.AreEqual(modFile, resolver.ResolveFile("Pack/Data/ships.xml"));
        }

        private static string WriteFile(string basePath, string relativePath, string contents)
        {
            string path = Path.Combine(basePath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
            return path;
        }
    }
}
