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
        public void ResolveFile_DerivedPackDoesNotContainAddress_FallsBackToInheritedPack()
        {
            string inheritedPackRoot = Path.Combine(_contentRoot, "Packs", "Inherited");
            string inheritedFile = WriteFile(inheritedPackRoot, "Data/ships.xml", "inherited");
            ContentFileResolver resolver = new ContentFileResolver(
                _contentRoot,
                _packRoot,
                fallbackPackRootPaths: new[] { inheritedPackRoot }
            );

            Assert.AreEqual(inheritedFile, resolver.ResolveFile("Pack/Data/ships.xml"));
        }

        [Test]
        public void ResolveFile_DerivedAndInheritedPacksContainAddress_DerivedPackWins()
        {
            string inheritedPackRoot = Path.Combine(_contentRoot, "Packs", "Inherited");
            WriteFile(inheritedPackRoot, "Data/ships.xml", "inherited");
            string derivedFile = WriteFile(_packRoot, "Data/ships.xml", "derived");
            ContentFileResolver resolver = new ContentFileResolver(
                _contentRoot,
                _packRoot,
                fallbackPackRootPaths: new[] { inheritedPackRoot }
            );

            Assert.AreEqual(derivedFile, resolver.ResolveFile("Pack/Data/ships.xml"));
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
        public void EnumerateFileAddresses_DerivedAndInheritedPacks_ReturnsLayeredUnion()
        {
            string inheritedPackRoot = Path.Combine(_contentRoot, "Packs", "Inherited");
            WriteFile(inheritedPackRoot, "UI/inherited.png", "inherited");
            WriteFile(inheritedPackRoot, "UI/replaced.png", "inherited");
            WriteFile(_packRoot, "UI/replaced.png", "derived");
            WriteFile(_packRoot, "UI/derived.png", "derived");
            ContentFileResolver resolver = new ContentFileResolver(
                _contentRoot,
                _packRoot,
                fallbackPackRootPaths: new[] { inheritedPackRoot }
            );

            string[] addresses = resolver.EnumerateFileAddresses("Pack/UI").ToArray();

            CollectionAssert.AreEqual(
                new[] { "Pack/UI/derived.png", "Pack/UI/inherited.png", "Pack/UI/replaced.png" },
                addresses
            );
        }

        [Test]
        public void EnumerateFileAddresses_PackRoot_ReturnsAllPackFiles()
        {
            WriteFile(_packRoot, "pack.xml", "pack");
            WriteFile(_packRoot, "Data/ships.xml", "ships");
            ContentFileResolver resolver = new ContentFileResolver(_contentRoot, _packRoot);

            string[] addresses = resolver.EnumerateFileAddresses("Pack/").ToArray();

            CollectionAssert.AreEqual(new[] { "Pack/Data/ships.xml", "Pack/pack.xml" }, addresses);
        }

        [Test]
        public void Discover_DuplicateID_BlocksBothCopiesAndReportsTheirFolders()
        {
            WriteMod("first-copy", "duplicate", "");
            WriteMod("second-copy", "duplicate", "");
            WriteMod("unrelated", "unrelated", "");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.That(resolver.Mods.Single().ID, Is.EqualTo("unrelated"));
            Assert.That(resolver.AvailableMods.Single().ID, Is.EqualTo("unrelated"));
            Assert.That(
                resolver.ModIssues.Single().Message,
                Does.Contain("first-copy (1.0.0)").And.Contain("second-copy (1.0.0)")
            );
        }

        [Test]
        public void ReadValidatedMod_ClientOnlyOmitted_RemainsShared()
        {
            WriteMod("shared", "shared", "");

            Assert.That(
                ContentFileResolver
                    .ReadValidatedMod(Path.Combine(_root, "Mods", "shared"))
                    .ClientOnly,
                Is.False
            );
        }

        [Test]
        public void ReadValidatedMod_ClientOnlyRoundTrip_PreservesDeclaration()
        {
            WriteMod("local", "local", "<ClientOnly>true</ClientOnly>");
            ContentModDefinition definition = ContentFileResolver.ReadValidatedMod(
                Path.Combine(_root, "Mods", "local")
            );

            ContentModDefinition restored = SerializationHelper.Deserialize<ContentModDefinition>(
                SerializationHelper.Serialize(definition)
            );

            Assert.That(restored.ClientOnly, Is.True);
        }

        [Test]
        public void ReadValidatedMod_ClientOnlyExtendsPack_RejectsManifest()
        {
            WriteMod(
                "local",
                "local",
                "<ClientOnly>true</ClientOnly><ExtendsPack>true</ExtendsPack>"
            );

            Assert.Throws<InvalidDataException>(() =>
                ContentFileResolver.ReadValidatedMod(Path.Combine(_root, "Mods", "local"))
            );
        }

        [Test]
        public void ForGameplay_ClientOnlyLayer_ExcludesLocalFiles()
        {
            WriteMod("shared", "shared", "");
            WriteMod("local", "local", "<ClientOnly>true</ClientOnly>");
            string shared = WriteFile(
                Path.Combine(_root, "Mods", "shared", "Content"),
                "Pack/image.png",
                "shared"
            );
            string local = WriteFile(
                Path.Combine(_root, "Mods", "local", "Content"),
                "Pack/image.png",
                "local"
            );
            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.That(resolver.ResolveFile("Pack/image.png"), Is.EqualTo(local));
            Assert.That(resolver.ForGameplay().ResolveFile("Pack/image.png"), Is.EqualTo(shared));
        }

        [Test]
        public void Discover_ClientOrderRules_PreserveSharedPriority()
        {
            WriteMod("first", "first", "");
            WriteMod("second", "second", "");
            WriteMod(
                "local",
                "local",
                "<ClientOnly>true</ClientOnly><LoadAfter><ModID>second</ModID></LoadAfter><LoadBefore><ModID>first</ModID></LoadBefore>"
            );
            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0",
                preferredModOrder: new[] { "first", "second", "local" }
            );

            Assert.That(
                resolver.GameplayMods.Select(mod => mod.ID),
                Is.EqualTo(new[] { "first", "second" })
            );
        }

        [Test]
        public void Discover_SharedRequiresClientOnly_BlocksSharedMod()
        {
            WriteMod("shared", "shared", "<Requires><Mod><ID>local</ID></Mod></Requires>");
            WriteMod("local", "local", "<ClientOnly>true</ClientOnly>");
            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.That(resolver.GameplayMods, Is.Empty);
            Assert.That(
                resolver.ModIssues.Any(issue => issue.ID == "shared" && issue.BlocksSelection),
                Is.True
            );
        }

        [Test]
        public void Discover_ClientOrderingCrossesSharedDependency_PreservesSharedMods()
        {
            WriteMod("first", "first", "<LoadBefore><ModID>second</ModID></LoadBefore>");
            WriteMod("second", "second", "");
            WriteMod(
                "local",
                "local",
                "<ClientOnly>true</ClientOnly><LoadAfter><ModID>second</ModID></LoadAfter><LoadBefore><ModID>first</ModID></LoadBefore>"
            );

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.That(
                resolver.Mods.Select(mod => mod.ID),
                Is.EqualTo(new[] { "first", "second", "local" })
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Discover_ClientConflictsWithShared_PreservesSharedMod(bool applied)
        {
            WriteMod("shared", "shared", "");
            WriteMod(
                "local",
                "local",
                "<ClientOnly>true</ClientOnly><IncompatibleWith><ModID>shared</ModID></IncompatibleWith>"
            );

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0",
                appliedModIDs: applied ? new[] { "shared", "local" } : null,
                appliedModVersions: applied ? new[] { "1.0.0", "1.0.0" } : null
            );

            Assert.That(resolver.Mods.Select(mod => mod.ID), Is.EqualTo(new[] { "shared" }));
            Assert.That(resolver.ModIssues.Single().ID, Is.EqualTo("local"));
        }

        [Test]
        public void Discover_ClientOnlyRequiresShared_LoadsBoth()
        {
            WriteMod("shared", "shared", "");
            WriteMod(
                "local",
                "local",
                "<ClientOnly>true</ClientOnly><Requires><Mod><ID>shared</ID></Mod></Requires>"
            );
            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.That(
                resolver.Mods.Select(mod => mod.ID),
                Is.EqualTo(new[] { "shared", "local" })
            );
        }

        [Test]
        public void GetMoveRestriction_ClientCrossesSharedLayer_RejectsMove()
        {
            WriteMod("shared", "shared", "");
            WriteMod("local", "local", "<ClientOnly>true</ClientOnly>");
            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.That(ContentModLoadOrder.GetMoveRestriction(resolver.Mods, 1, 0), Is.Not.Empty);
        }

        [Test]
        public void Discover_NewModHasEarlierID_AppendsWithoutChangingAppliedOrder()
        {
            WriteMod("01-alpha", "alpha", "");
            WriteMod("02-bravo", "bravo", "");
            WriteMod("03-charlie", "charlie", "");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0",
                preferredModOrder: new[] { "charlie", "bravo" }
            );

            Assert.That(
                resolver.Mods.Select(mod => mod.ID),
                Is.EqualTo(new[] { "charlie", "bravo", "alpha" })
            );
        }

        [Test]
        public void Discover_FirstUseOfPack_BreaksTiesByID()
        {
            WriteMod("01-charlie", "charlie", "");
            WriteMod("02-alpha", "alpha", "");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0",
                preferredModOrder: Array.Empty<string>()
            );

            Assert.That(
                resolver.Mods.Select(mod => mod.ID),
                Is.EqualTo(new[] { "alpha", "charlie" })
            );
        }

        [Test]
        public void Discover_NewPrerequisite_LoadsBeforeDependentWithoutMovingUnrelatedModAhead()
        {
            WriteMod("appended", "new-dependency", "<LoadBefore><ModID>first</ModID></LoadBefore>");
            WriteMod("first", "first", "");
            WriteMod("second", "second", "");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0",
                preferredModOrder: new[] { "first", "second" }
            );

            Assert.That(
                resolver.Mods.Select(mod => mod.ID),
                Is.EqualTo(new[] { "new-dependency", "first", "second" })
            );
        }

        [Test]
        public void Discover_NewOrderingRulesMoveAppliedModsTogether_PreservesTheirRelativeOrder()
        {
            WriteMod("first", "first", "");
            WriteMod("second", "second", "");
            WriteMod("third", "third", "");
            WriteMod(
                "alpha",
                "alpha",
                "<LoadAfter><ModID>third</ModID></LoadAfter><LoadBefore><ModID>first</ModID></LoadBefore>"
            );
            WriteMod(
                "bravo",
                "bravo",
                "<LoadAfter><ModID>second</ModID></LoadAfter><LoadBefore><ModID>first</ModID></LoadBefore>"
            );

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0",
                preferredModOrder: new[] { "first", "second", "third" }
            );

            Assert.That(
                resolver
                    .Mods.Select(mod => mod.ID)
                    .Where(id => id is "first" or "second" or "third"),
                Is.EqualTo(new[] { "second", "third", "first" })
            );
        }

        [Test]
        public void GetMoveRestriction_MovedPastANonAdjacentDependent_ReportsTheDependency()
        {
            WriteMod("first", "first", "");
            WriteMod("second", "second", "");
            WriteMod("third", "third", "<Requires><Mod><ID>first</ID></Mod></Requires>");
            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.That(
                ContentModLoadOrder.GetMoveRestriction(resolver.Mods, 0, 2),
                Is.EqualTo("third requires first.")
            );
        }

        [Test]
        public void Discover_AppliedSelection_KeepsNewCompatibleModsPending()
        {
            WriteMod("applied", "applied", "");
            WriteMod("new", "new", "");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0",
                preferredModOrder: new[] { "applied" },
                appliedModIDs: new[] { "applied" }
            );

            Assert.That(resolver.Mods.Select(mod => mod.ID), Is.EqualTo(new[] { "applied" }));
            Assert.That(resolver.AvailableMods.Select(mod => mod.ID), Does.Contain("new"));
        }

        [Test]
        public void Discover_AppliedVersionWithAnInstalledUpdate_UsesTheRetainedRelease()
        {
            WriteMod("version-one", "example", "");
            string mods = Path.Combine(_root, "Mods");
            string retained = Path.Combine(mods, ".versions");
            Directory.CreateDirectory(retained);
            Directory.Move(
                Path.Combine(mods, "version-one"),
                Path.Combine(retained, "version-one")
            );
            WriteMod("version-two", "example", "");
            string manifest = Path.Combine(mods, "version-two", "mod.xml");
            File.WriteAllText(
                manifest,
                File.ReadAllText(manifest)
                    .Replace("<Version>1.0.0</Version>", "<Version>2.0.0</Version>")
            );

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0",
                appliedModIDs: new[] { "example" },
                appliedModVersions: new[] { "1.0.0" }
            );

            Assert.That(resolver.Mods.Single().Version, Is.EqualTo("1.0.0"));
            Assert.That(resolver.AvailableMods.Single().Version, Is.EqualTo("2.0.0"));
        }

        [Test]
        public void DescribeModOverrides_OrderedSparseSettings_ReportsWinningValue()
        {
            const string address = "Pack/Data/ships.xml";
            WriteFile(_packRoot, "Data/ships.xml", "<Ships><Cost>5</Cost></Ships>");
            WriteMod("alpha", "alpha", "");
            WriteFile(
                Path.Combine(_root, "Mods", "alpha", "Content"),
                address,
                "<Ships Inherit='true'><Cost>20</Cost></Ships>"
            );
            WriteMod("bravo", "bravo", "");
            WriteFile(
                Path.Combine(_root, "Mods", "bravo", "Content"),
                address,
                "<Ships Inherit='true'><Cost>25</Cost></Ships>"
            );

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0",
                preferredModOrder: new[] { "bravo", "alpha" }
            );

            Assert.That(
                resolver.DescribeModOverrides()["bravo"].Single(),
                Does.Contain("alpha").And.Contain("Cost to 20")
            );
        }

        [Test]
        public void Discover_MalformedArtworkAliasMap_KeepsOtherModsAvailable()
        {
            WriteMod("invalid", "invalid", "");
            WriteMod("valid", "valid", "");
            WriteFile(
                Path.Combine(_root, "Mods", "invalid", "Content"),
                "Pack/" + ContentArtworkAliases.FileName,
                "<InvalidArtworkAliases/>"
            );

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.That(
                resolver.AvailableMods.Select(mod => mod.ID),
                Is.EqualTo(new[] { "valid" })
            );
            Assert.That(resolver.ModIssues.Single().Message, Does.Contain("invalid root"));
        }

        [Test]
        public void DescribeModOverrides_ArtworkAliases_ReportsOnlyChangedTargets()
        {
            WriteMod("alpha", "alpha", "");
            WriteMod("bravo", "bravo", "");
            string address = "Pack/" + ContentArtworkAliases.FileName;
            WriteFile(
                Path.Combine(_root, "Mods", "alpha", "Content"),
                address,
                "<ArtworkAliases><Image From='Pack/shared' To='Pack/first'/>"
                    + "<Image From='Pack/alpha' To='Pack/second'/></ArtworkAliases>"
            );
            WriteFile(
                Path.Combine(_root, "Mods", "bravo", "Content"),
                address,
                "<ArtworkAliases><Image From='Pack/shared' To='Pack/third'/>"
                    + "<Image From='Pack/bravo' To='Pack/fourth'/></ArtworkAliases>"
            );

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.That(
                resolver.DescribeModOverrides()["alpha"],
                Is.EqualTo(new[] { "bravo overrides Pack/shared." })
            );
        }

        [Test]
        public void Discover_RequiredModWouldLoadLater_LoadsRequirementFirst()
        {
            WriteMod(
                "01-patch",
                "patch",
                "<Requires><Mod><ID>base-mod</ID><Version>1.0.0</Version></Mod></Requires>"
            );
            WriteMod("02-base", "base-mod", "");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            CollectionAssert.AreEqual(
                new[] { "base-mod", "patch" },
                resolver.Mods.Select(mod => mod.ID)
            );
        }

        [Test]
        public void Discover_MissingRequirement_KeepsModAvailableWithReason()
        {
            WriteMod("patch", "patch", "<Requires><Mod><ID>base-mod</ID></Mod></Requires>");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.That(resolver.AvailableMods.Single().ID, Is.EqualTo("patch"));
            Assert.That(resolver.Mods, Is.Empty);
            Assert.That(resolver.ModIssues.Single().Message, Does.Contain("base-mod"));
        }

        [Test]
        public void Discover_ModOrderCycle_LeavesAnUnrelatedModActive()
        {
            WriteMod("first", "first", "<LoadAfter><ModID>second</ModID></LoadAfter>");
            WriteMod("second", "second", "<LoadAfter><ModID>first</ModID></LoadAfter>");
            WriteMod("unrelated", "unrelated", "");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.That(resolver.Mods.Single().ID, Is.EqualTo("unrelated"));
            Assert.That(
                resolver.ModIssues.Select(issue => issue.ID),
                Is.EquivalentTo(new[] { "first", "second" })
            );
        }

        [Test]
        public void Discover_IncompatibleMods_BecomeLoadableWhenOneIsDisabled()
        {
            WriteMod(
                "first",
                "first",
                "<IncompatibleWith><ModID>second</ModID></IncompatibleWith>"
            );
            WriteMod("second", "second", "");

            ContentFileResolver both = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );
            ContentFileResolver one = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0",
                new[] { "second" }
            );

            Assert.That(both.Mods, Is.Empty);
            Assert.That(one.Mods.Single().ID, Is.EqualTo("first"));
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
                    + "<BasePackVersion>1.0.0</BasePackVersion>"
                    + "</ContentModDefinition>"
            );
            string modFile = WriteFile(modRoot, "Content/Pack/Data/ships.xml", "mod");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
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
                    + "<BasePackVersion>1.0.0</BasePackVersion>"
                    + "</ContentModDefinition>"
            );
            WriteFile(modRoot, "Content/Pack/Data/ships.xml", "mod");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0",
                new[] { "ship-rebalance" }
            );

            Assert.IsEmpty(resolver.Mods);
            Assert.AreEqual("ship-rebalance", resolver.AvailableMods.Single().ID);
            Assert.AreEqual("base", File.ReadAllText(resolver.ResolveFile("Pack/Data/ships.xml")));
        }

        [Test]
        public void Discover_ModForDifferentPackVersion_IgnoresDefinition()
        {
            string modRoot = Path.Combine(_root, "Mods", "ShipRebalance");
            WriteFile(
                modRoot,
                "mod.xml",
                "<ContentModDefinition><ID>ship-rebalance</ID><Version>1.0.0</Version>"
                    + "<DisplayName>Ship Rebalance</DisplayName><BasePackID>base-pack</BasePackID>"
                    + "<BasePackVersion>2.0.0</BasePackVersion>"
                    + "</ContentModDefinition>"
            );

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.IsEmpty(resolver.AvailableMods);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Discover_ContentPackFolder_DoesNotReportModIssues(bool malformed)
        {
            string packRoot = Path.Combine(_root, "Mods", "UserPack");
            WriteFile(
                packRoot,
                "pack.xml",
                malformed
                    ? "<ContentPack>"
                    : "<ContentPack><ID>user-pack</ID><Version>1.0.0</Version>"
                        + "<BasePackID>base-pack</BasePackID></ContentPack>"
            );

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            Assert.IsEmpty(resolver.Mods);
            Assert.IsEmpty(resolver.AvailableMods);
            Assert.IsEmpty(resolver.ModIssues);
        }

        [Test]
        public void Discover_InvalidDefinitions_SkipsThemAndLoadsValidMod()
        {
            Directory.CreateDirectory(Path.Combine(_root, "Mods", "MissingDefinition"));
            string incompleteModRoot = Path.Combine(_root, "Mods", "Incomplete");
            WriteFile(
                incompleteModRoot,
                "mod.xml",
                "<ContentModDefinition><ID>incomplete</ID><Version>1.0.0</Version>"
                    + "<DisplayName>Incomplete</DisplayName><BasePackID>base-pack</BasePackID>"
                    + "</ContentModDefinition>"
            );
            string malformedModRoot = Path.Combine(_root, "Mods", "Malformed");
            WriteFile(malformedModRoot, "mod.xml", "<ContentModDefinition><ID>malformed</ID>");
            WriteFile(malformedModRoot, "pack.xml", "<ContentPack/>");
            string validModRoot = Path.Combine(_root, "Mods", "Valid");
            WriteFile(validModRoot, "pack.xml", "<ContentPack/>");
            WriteFile(
                validModRoot,
                "mod.xml",
                "<ContentModDefinition><ID>valid</ID><Version>1.0.0</Version>"
                    + "<DisplayName>Valid</DisplayName><BasePackID>base-pack</BasePackID>"
                    + "<BasePackVersion>1.0.0</BasePackVersion>"
                    + "</ContentModDefinition>"
            );
            string validFile = WriteFile(validModRoot, "Content/Pack/Data/ships.xml", "valid");

            ContentFileResolver resolver = ContentFileResolver.Discover(
                _contentRoot,
                _packRoot,
                "base-pack",
                "1.0.0"
            );

            CollectionAssert.AreEqual(new[] { "valid" }, resolver.Mods.Select(mod => mod.ID));
            CollectionAssert.AreEqual(
                new[] { "valid" },
                resolver.AvailableMods.Select(mod => mod.ID)
            );
            Assert.AreEqual(3, resolver.ModIssues.Count);
            ContentModIssue incomplete = resolver.ModIssues.Single(issue =>
                issue.ID == "incomplete"
            );
            Assert.AreEqual("Incomplete", incomplete.DisplayName);
            StringAssert.Contains("BasePackVersion", incomplete.Message);
            ContentModIssue malformed = resolver.ModIssues.Single(issue => issue.ID == "Malformed");
            Assert.AreEqual("Malformed", malformed.DisplayName);
            Assert.IsNotEmpty(malformed.Message);
            ContentModIssue missing = resolver.ModIssues.Single(issue =>
                issue.ID == "MissingDefinition"
            );
            StringAssert.Contains("mod.xml", missing.Message);
            Assert.AreEqual(validFile, resolver.ResolveFile("Pack/Data/ships.xml"));
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
                "base-pack",
                "1.0.0"
            );

            Assert.IsEmpty(resolver.Mods);
        }

        /// <summary>
        /// Installs a compatible mod with the supplied relationship metadata.
        /// </summary>
        /// <param name="folder">The installation folder.</param>
        /// <param name="id">The mod identity.</param>
        /// <param name="relationships">The authored dependency and ordering XML.</param>
        private void WriteMod(string folder, string id, string relationships)
        {
            WriteFile(
                Path.Combine(_root, "Mods", folder),
                "mod.xml",
                $"<ContentModDefinition><ID>{id}</ID><DisplayName>{id}</DisplayName><Version>1.0.0</Version>"
                    + $"<BasePackID>base-pack</BasePackID><BasePackVersion>1.0.0</BasePackVersion>{relationships}</ContentModDefinition>"
            );
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
