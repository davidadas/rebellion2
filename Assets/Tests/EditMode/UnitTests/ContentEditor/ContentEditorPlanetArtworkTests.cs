using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorPlanetArtworkTests : ContentPackEditorTestBase
    {
        private const string _shared = "Pack/Shared/Planets/shared";

        private string BaseRoot => Path.Combine(contentRoot, "Packs/BasePack");

        /// <summary>
        /// Creates two planets sharing one inherited icon and one encyclopedia image.
        /// </summary>
        [SetUp]
        public void SetUpPlanets()
        {
            WriteFixtureFile(
                "Data/planets.xml",
                "<PlanetSectors><PlanetSector><TypeID>SECTOR</TypeID><Planets>"
                    + string.Join(
                        "",
                        new[] { "ONE", "TWO" }.Select(id =>
                            $"<Planet><TypeID>{id}</TypeID><PlanetIconPath>{_shared}</PlanetIconPath>"
                            + "<EncyclopediaImagePath>Pack/Shared/Planets/article</EncyclopediaImagePath></Planet>"
                        )
                    )
                    + "</Planets></PlanetSector></PlanetSectors>"
            );
            UpdateBasePackDefinition(pack => pack.PlanetSectorsPath = "Data/planets.xml");
            WriteImage(BaseRoot, "Shared/Planets/shared.png", Color.red);
            WriteImage(BaseRoot, "Shared/Planets/article.png", Color.blue);
        }

        [Test]
        public void GetChoices_InheritedAndLocalReferences_OffersBothWithoutCopyingTheBase()
        {
            using ContentPackEditorProject project = CreateWorkingProject();
            WriteImage(project.PackRootPath, "Shared/Planets/new.png", Color.green);
            project.SetField(
                GetPlanet(project, "TWO").GetField("PlanetIconPath"),
                "Pack/Shared/Planets/new.png"
            );
            string before = string.Join("\n", project.Documents.Select(document => document.Text));

            var choices = ContentEditorPlanetArtwork.GetChoices(
                project,
                GetPlanet(project, "ONE").GetField("PlanetIconPath")
            );

            Assert.That(choices.Keys, Does.Contain(_shared));
            Assert.That(choices.Keys, Does.Contain("Pack/Shared/Planets/new.png"));
            StringAssert.Contains("inherited", choices[_shared]);
            StringAssert.Contains("this pack", choices["Pack/Shared/Planets/new.png"]);
            Assert.AreEqual(
                1,
                Directory.GetFiles(Path.Combine(project.PackRootPath, "Shared/Planets")).Length
            );
            Assert.AreEqual(
                before,
                string.Join("\n", project.Documents.Select(document => document.Text))
            );
        }

        [TestCase("PlanetIconPath", "Pack/Shared/Planets/shared", "Pack/Shared/Planets/article")]
        [TestCase(
            "EncyclopediaImagePath",
            "Pack/Shared/Planets/article",
            "Pack/Shared/Planets/shared"
        )]
        public void GetChoices_DifferentPlanetImageRoles_OnlyOffersImagesUsedByTheSelectedRole(
            string member,
            string expected,
            string excluded
        )
        {
            using ContentPackEditorProject project = CreateWorkingProject();

            var choices = ContentEditorPlanetArtwork.GetChoices(
                project,
                GetPlanet(project, "ONE").GetField(member)
            );

            Assert.That(choices.Keys, Does.Contain(expected));
            Assert.IsFalse(
                choices.Keys.Any(address => address.StartsWith(excluded, StringComparison.Ordinal))
            );
        }

        [TestCase("PlanetIconPath", "Pack/Shared/Planets/shared", "Pack/Shared/Planets/article")]
        [TestCase(
            "EncyclopediaImagePath",
            "Pack/Shared/Planets/article",
            "Pack/Shared/Planets/shared"
        )]
        public void GetChoices_CurrentAssignmentsCleared_KeepsTheInheritedPoolSeparatedByRole(
            string member,
            string expected,
            string excluded
        )
        {
            using ContentPackEditorProject project = CreateWorkingProject();
            foreach (string id in new[] { "ONE", "TWO" })
                project.SetField(GetPlanet(project, id).GetField(member), string.Empty);

            var choices = ContentEditorPlanetArtwork.GetChoices(
                project,
                GetPlanet(project, "ONE").GetField(member)
            );

            Assert.That(choices.Keys, Does.Contain(expected));
            Assert.IsFalse(
                choices.Keys.Any(address => address.StartsWith(excluded, StringComparison.Ordinal))
            );
        }

        [Test]
        public void GetChoices_LegacyReferences_OffersTheExistingFileOnceAndKeepsTheSelectedAddress()
        {
            using ContentPackEditorProject project = CreateWorkingProject();
            WriteImage(project.PackRootPath, "Assets/legacy.png", Color.green);
            ContentPackEditorField field = GetPlanet(project, "ONE").GetField("PlanetIconPath");
            project.SetField(field, "Pack/Assets/legacy");
            project.SetField(
                GetPlanet(project, "TWO").GetField("PlanetIconPath"),
                "Pack/Assets/legacy.png"
            );

            var choices = ContentEditorPlanetArtwork.GetChoices(project, field);

            Assert.AreEqual(
                1,
                choices.Keys.Count(key =>
                    key.StartsWith("Pack/Assets/legacy", StringComparison.Ordinal)
                )
            );
            Assert.That(choices.Keys, Does.Contain("Pack/Assets/legacy"));
            Assert.AreEqual("Pack/Assets/legacy", field.Value);
        }

        [Test]
        public void GetChoices_LocalOverride_OffersOneChoiceForTheResolvedImage()
        {
            using ContentPackEditorProject project = CreateWorkingProject();
            WriteImage(project.PackRootPath, "Shared/Planets/shared.png", Color.green);

            var choices = ContentEditorPlanetArtwork.GetChoices(
                project,
                GetPlanet(project, "ONE").GetField("PlanetIconPath")
            );

            Assert.AreEqual(
                1,
                choices.Keys.Count(key => key.StartsWith(_shared, StringComparison.Ordinal))
            );
            StringAssert.Contains("this pack", choices[_shared]);
        }

        [TestCase(".png")]
        [TestCase(".jpg")]
        public void ImportAsset_InheritedFilenameCollision_PreservesTheSharedImage(string extension)
        {
            if (extension != ".png")
            {
                File.Delete(Path.Combine(BaseRoot, "Shared/Planets/shared.png"));
                WriteImage(BaseRoot, "Shared/Planets/shared" + extension, Color.red);
            }
            using ContentPackEditorProject project = CreateWorkingProject();
            string source = WriteImage(contentRoot, "Import/shared.png", Color.green);
            ContentPackEditorField field = GetPlanet(project, "ONE").GetField("PlanetIconPath");
            string original = Resolve(project, _shared);
            byte[] before = File.ReadAllBytes(original);

            string imported = ContentPackEditorStorage.ImportAsset(project, source, field);
            project.SetField(field, imported);

            Assert.AreEqual("Pack/Shared/Planets/shared-2", imported);
            Assert.AreEqual(_shared, GetPlanet(project, "TWO").GetField("PlanetIconPath").Value);
            Assert.AreEqual(original, Resolve(project, _shared));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(original));
            CollectionAssert.AreEqual(
                File.ReadAllBytes(source),
                File.ReadAllBytes(Resolve(project, imported))
            );
        }

        [Test]
        public void ImportAsset_ReusedByTwoPlanets_CopiesOneFileAndPersistsBothReferences()
        {
            using ContentPackEditorProject project = CreateWorkingProject();
            string source = WriteImage(contentRoot, "Import/custom.png", Color.green);
            ContentPackEditorField first = GetPlanet(project, "ONE").GetField("PlanetIconPath");
            ContentPackEditorField second = GetPlanet(project, "TWO").GetField("PlanetIconPath");

            string address = ContentPackEditorStorage.ImportAsset(project, source, first);
            project.SetField(first, address);
            project.SetField(second, ContentPackEditorStorage.ImportAsset(project, source, second));
            ContentPackEditorStorage.Save(project);

            Assert.AreEqual(first.Value, second.Value);
            Assert.AreEqual(
                1,
                Directory.GetFiles(Path.Combine(project.PackRootPath, "Shared/Planets")).Length
            );
            using ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );
            Assert.AreEqual(address, GetPlanet(reopened, "ONE").GetField("PlanetIconPath").Value);
            Assert.AreEqual(address, GetPlanet(reopened, "TWO").GetField("PlanetIconPath").Value);
        }

        [Test]
        public void ImportAsset_MatchingInheritedImage_ReusesItWithoutCreatingAFile()
        {
            using ContentPackEditorProject project = CreateWorkingProject();
            string source = Path.Combine(BaseRoot, "Shared/Planets/shared.png");

            string address = ContentPackEditorStorage.ImportAsset(
                project,
                source,
                GetPlanet(project, "ONE").GetField("PlanetIconPath")
            );

            Assert.AreEqual(_shared, address);
            Assert.IsFalse(
                Directory
                    .EnumerateFiles(project.PackRootPath, "*.png", SearchOption.AllDirectories)
                    .Any()
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void Replace_InheritedImage_ChangesAllReferencesWithinTheModAndSupportsUndoRedo()
        {
            using ContentPackEditorProject project = CreateWorkingProject();
            string original = Resolve(project, _shared);
            byte[] before = File.ReadAllBytes(original);
            string source = WriteImage(contentRoot, "Import/replacement.png", Color.green);

            ContentEditorPlanetArtwork.Replace(
                project,
                GetPlanet(project, "ONE").GetField("PlanetIconPath"),
                source
            );

            foreach (string id in new[] { "ONE", "TWO" })
                CollectionAssert.AreEqual(
                    File.ReadAllBytes(source),
                    File.ReadAllBytes(
                        Resolve(project, GetPlanet(project, id).GetField("PlanetIconPath").Value)
                    )
                );
            CollectionAssert.AreEqual(before, File.ReadAllBytes(original));
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(original, Resolve(project, _shared));
            Assert.IsTrue(project.Redo());
            ContentPackEditorStorage.Save(project);
            using ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );
            CollectionAssert.AreEqual(
                File.ReadAllBytes(source),
                File.ReadAllBytes(Resolve(reopened, _shared))
            );
            Assert.AreEqual(_shared, GetPlanet(reopened, "ONE").GetField("PlanetIconPath").Value);
        }

        [Test]
        public void Replace_AliasedImage_OverridesItsResolvedFileWithoutRewritingTheAddressMap()
        {
            WriteImage(BaseRoot, "Shared/Planets/actual.png", Color.blue);
            WriteFixtureFile(
                ContentArtworkAliases.FileName,
                $"<ArtworkAliases><Image From=\"{_shared}\" To=\"Pack/Shared/Planets/actual\" /></ArtworkAliases>"
            );
            using ContentPackEditorProject project = CreateWorkingProject();
            string original = Resolve(project, _shared);
            byte[] before = File.ReadAllBytes(original);
            string source = WriteImage(contentRoot, "Import/replacement.png", Color.green);

            ContentEditorPlanetArtwork.Replace(
                project,
                GetPlanet(project, "ONE").GetField("PlanetIconPath"),
                source
            );

            CollectionAssert.AreEqual(
                File.ReadAllBytes(source),
                File.ReadAllBytes(Resolve(project, _shared))
            );
            CollectionAssert.AreEqual(before, File.ReadAllBytes(original));
            Assert.IsFalse(
                File.Exists(Path.Combine(project.PackRootPath, ContentArtworkAliases.FileName))
            );
        }

        [Test]
        public void Replace_DifferentFormat_LeavesTheSharedImageUntouched()
        {
            using ContentPackEditorProject project = CreateWorkingProject();
            string source = WriteImage(contentRoot, "Import/replacement.jpg", Color.green);
            string original = Resolve(project, _shared);

            Assert.Throws<ArgumentException>(() =>
                ContentEditorPlanetArtwork.Replace(
                    project,
                    GetPlanet(project, "ONE").GetField("PlanetIconPath"),
                    source
                )
            );

            Assert.AreEqual(original, Resolve(project, _shared));
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImportAsset_DanglingImageAlias_ReservesItsAddressWithoutOverwritingFiles(
            bool hasOrphan
        )
        {
            string original = Path.Combine(BaseRoot, "Shared/Planets/shared.png");
            byte[] before = File.ReadAllBytes(original);
            if (!hasOrphan)
                File.Delete(original);
            WriteFixtureFile(
                ContentArtworkAliases.FileName,
                $"<ArtworkAliases><Image From=\"{_shared}\" To=\"Pack/Shared/Planets/missing\" /></ArtworkAliases>"
            );
            using ContentPackEditorProject project = CreateWorkingProject();
            string source = WriteImage(contentRoot, "Import/shared.png", Color.green);

            string address = ContentPackEditorStorage.ImportAsset(
                project,
                source,
                GetPlanet(project, "ONE").GetField("PlanetIconPath")
            );

            Assert.AreEqual("Pack/Shared/Planets/shared-2", address);
            Assert.IsNotNull(Resolve(project, address));
            Assert.IsNull(Resolve(project, _shared));
            if (hasOrphan)
                CollectionAssert.AreEqual(before, File.ReadAllBytes(original));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImportAsset_DanglingAliasDestination_KeepsSharedReferencesUnresolved(
            bool hasOrphan
        )
        {
            string original = Path.Combine(BaseRoot, "Shared/Planets/shared.png");
            byte[] before = File.ReadAllBytes(original);
            if (!hasOrphan)
                File.Delete(original);
            WriteFixtureFile(
                ContentArtworkAliases.FileName,
                $"<ArtworkAliases><Image From=\"{_shared}\" To=\"Pack/Shared/Planets/missing\" /></ArtworkAliases>"
            );
            using ContentPackEditorProject project = CreateWorkingProject();
            string source = WriteImage(contentRoot, "Import/missing.png", Color.green);

            string address = ContentPackEditorStorage.ImportAsset(
                project,
                source,
                GetPlanet(project, "ONE").GetField("PlanetIconPath")
            );

            Assert.AreEqual("Pack/Shared/Planets/missing-2", address);
            Assert.IsNotNull(Resolve(project, address));
            Assert.IsNull(Resolve(project, _shared));
            Assert.IsFalse(
                File.Exists(Path.Combine(project.PackRootPath, "Shared/Planets/missing.png"))
            );
            if (hasOrphan)
                CollectionAssert.AreEqual(before, File.ReadAllBytes(original));
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void ImportAsset_LiteralAliasWithExtension_PreservesOtherPlanets(
            bool isSource,
            bool hasImage
        )
        {
            string literal = _shared + ".png";
            string from = isSource ? literal : _shared;
            string to = isSource ? "Pack/Shared/Planets/actual" : literal;
            File.Delete(Path.Combine(BaseRoot, "Shared/Planets/shared.png"));
            if (hasImage)
                WriteImage(
                    BaseRoot,
                    isSource ? "Shared/Planets/actual.png" : "Shared/Planets/shared.png",
                    Color.red
                );
            string planets = Path.Combine(BaseRoot, "Data/planets.xml");
            WriteFixtureFile("Data/planets.xml", File.ReadAllText(planets).Replace(_shared, from));
            WriteFixtureFile(
                ContentArtworkAliases.FileName,
                $"<ArtworkAliases><Image From=\"{from}\" To=\"{to}\" /></ArtworkAliases>"
            );
            using ContentPackEditorProject project = CreateWorkingProject();
            string original = Resolve(project, from);
            byte[] before = original == null ? null : File.ReadAllBytes(original);
            string source = WriteImage(contentRoot, "Import/shared.png.jpg", Color.green);
            ContentPackEditorField field = GetPlanet(project, "ONE").GetField("PlanetIconPath");

            string imported = ContentPackEditorStorage.ImportAsset(project, source, field);
            project.SetField(field, imported);

            Assert.AreEqual("Pack/Shared/Planets/shared.png-2", imported);
            Assert.AreEqual(from, GetPlanet(project, "TWO").GetField("PlanetIconPath").Value);
            Assert.AreEqual(original, Resolve(project, from));
            if (hasImage)
                CollectionAssert.AreEqual(before, File.ReadAllBytes(original));
            CollectionAssert.AreEqual(
                File.ReadAllBytes(source),
                File.ReadAllBytes(Resolve(project, imported))
            );
        }

        /// <summary>
        /// Finds one nested planet through the project's public catalog view.
        /// </summary>
        /// <param name="project">The authoring project.</param>
        /// <param name="id">The authored planet identity.</param>
        /// <returns>The selected planet record.</returns>
        private static ContentPackEditorRecord GetPlanet(
            ContentPackEditorProject project,
            string id
        ) =>
            project
                .GetCatalogs()
                .Where(catalog => catalog.IsMap)
                .SelectMany(catalog => catalog.Records)
                .SelectMany(record => record.Planets)
                .Single(planet => planet.ID == id);

        /// <summary>
        /// Resolves artwork using a fresh resolver over the current working files.
        /// </summary>
        /// <param name="project">The authoring project.</param>
        /// <param name="address">The shared image address.</param>
        /// <returns>The image selected by the inheritance chain.</returns>
        private static string Resolve(ContentPackEditorProject project, string address) =>
            ContentPackLoader
                .CreateFolderResolver(
                    project.ContentRootPath,
                    project.PackRootPath,
                    project.Definition
                )
                .ResolveFile(address, ".png", ".jpg", ".jpeg");

        /// <summary>
        /// Writes a valid small image in the requested format.
        /// </summary>
        /// <param name="root">The destination root.</param>
        /// <param name="relative">The image path beneath the root.</param>
        /// <param name="color">The image color.</param>
        /// <returns>The complete file path.</returns>
        private static string WriteImage(string root, string relative, Color color)
        {
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Texture2D image = new(2, 2);
            try
            {
                image.SetPixels(Enumerable.Repeat(color, 4).ToArray());
                image.Apply();
                File.WriteAllBytes(
                    path,
                    Path.GetExtension(path) == ".jpg" ? image.EncodeToJPG() : image.EncodeToPNG()
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
            return path;
        }
    }
}
