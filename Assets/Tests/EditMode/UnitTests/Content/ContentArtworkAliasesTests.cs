using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentArtworkAliasesTests
    {
        private string directory;
        private string pack;

        /// <summary>
        /// Creates isolated application, pack, and overlay roots.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(
                Path.GetTempPath(),
                "artwork-aliases-" + Guid.NewGuid().ToString("N")
            );
            pack = Path.Combine(directory, "Pack");
            Directory.CreateDirectory(pack);
        }

        /// <summary>
        /// Removes the isolated roots after each check.
        /// </summary>
        [TearDown]
        public void TearDown() => Directory.Delete(directory, true);

        [Test]
        public void ResolveFile_LegacyAddress_PrefersCurrentArtworkOverRetainedOldFile()
        {
            Write(pack, "old.png", "old");
            string current = Write(pack, "Units/current.png", "current");
            Write(
                pack,
                ContentArtworkAliases.FileName,
                "<ArtworkAliases><Image From='Pack/old.png' To='Pack/Units/current'/></ArtworkAliases>"
            );

            var resolver = new ContentFileResolver(directory, pack);

            Assert.AreEqual(current, resolver.ResolveFile("Pack/old.png", ".png"));
        }

        [Test]
        public void ResolveFile_UpperModOverridesLegacyAddress_PreservesModPrecedence()
        {
            Write(pack, "Units/current.png", "current");
            Write(
                pack,
                ContentArtworkAliases.FileName,
                "<ArtworkAliases><Image From='Pack/old.png' To='Pack/Units/current.png'/></ArtworkAliases>"
            );
            string mod = Path.Combine(directory, "Mod");
            string overridden = Write(Path.Combine(mod, "Pack"), "old.png", "mod");

            var resolver = new ContentFileResolver(directory, pack, new[] { mod });

            Assert.AreEqual(overridden, resolver.ResolveFile("Pack/old.png"));
        }

        [Test]
        public void ResolveFile_UpperModOverridesDestination_UsesCurrentModArtwork()
        {
            Write(pack, "Units/current.png", "current");
            Write(
                pack,
                ContentArtworkAliases.FileName,
                "<ArtworkAliases><Image From='Pack/old.png' To='Pack/Units/current'/></ArtworkAliases>"
            );
            string mod = Path.Combine(directory, "Mod");
            string overridden = Write(Path.Combine(mod, "Pack"), "Units/current.png", "mod");

            var resolver = new ContentFileResolver(directory, pack, new[] { mod });

            Assert.AreEqual(overridden, resolver.ResolveFile("Pack/old.png", ".png"));
        }

        [TestCase("<Image From='Pack/old.png' To='Pack/../outside.png'/>")]
        [TestCase("<Image From='Pack/old.png' To='Application/image.png'/>")]
        [TestCase("<Image From='Pack/old.png' To='Pack/data.xml'/>")]
        [TestCase(
            "<Image From='Pack/old.png' To='Pack/new.png'/><Image From='Pack/old.png' To='Pack/other.png'/>"
        )]
        [TestCase(
            "<Image From='Pack/one.png' To='Pack/two.png'/><Image From='Pack/two.png' To='Pack/one.png'/>"
        )]
        public void Load_InvalidMap_RejectsBeforeResolvingFiles(string entries)
        {
            Write(
                pack,
                ContentArtworkAliases.FileName,
                "<ArtworkAliases>" + entries + "</ArtworkAliases>"
            );

            Assert.Throws<InvalidDataException>(() => ContentArtworkAliases.Load(pack));
        }

        [Test]
        public void ResolveFile_CycleAcrossLayers_RejectsWithoutRecursingIndefinitely()
        {
            string inherited = Path.Combine(directory, "Inherited");
            Write(
                pack,
                ContentArtworkAliases.FileName,
                "<ArtworkAliases><Image From='Pack/one.png' To='Pack/two.png'/></ArtworkAliases>"
            );
            Write(
                inherited,
                ContentArtworkAliases.FileName,
                "<ArtworkAliases><Image From='Pack/two.png' To='Pack/one.png'/></ArtworkAliases>"
            );
            var resolver = new ContentFileResolver(
                directory,
                pack,
                fallbackPackRootPaths: new[] { inherited }
            );

            Assert.Throws<InvalidDataException>(() => resolver.ResolveFile("Pack/one.png"));
        }

        [Test]
        public void Serialize_CyclicMap_RejectsBeforePublishingIt()
        {
            var aliases = new Dictionary<string, string>
            {
                ["Pack/one.png"] = "Pack/two.png",
                ["Pack/two.png"] = "Pack/one.png",
            };

            Assert.Throws<InvalidDataException>(() => ContentArtworkAliases.Serialize(aliases));
        }

        /// <summary>
        /// Writes a fixture file beneath one content layer.
        /// </summary>
        /// <param name="root">The layer root.</param>
        /// <param name="relative">The layer-relative path.</param>
        /// <param name="contents">The file contents.</param>
        /// <returns>The created physical file.</returns>
        private static string Write(string root, string relative, string contents)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
            return path;
        }
    }
}
