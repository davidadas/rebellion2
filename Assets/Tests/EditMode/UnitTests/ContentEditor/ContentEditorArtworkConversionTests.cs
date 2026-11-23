using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorArtworkConversionTests : ContentPackEditorTestBase
    {
        [Test]
        public void ImportAsset_ExternalImage_UsesUnitFolderAndSlotName()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            string source = Path.Combine(
                Directory.GetParent(contentRoot).FullName,
                "arbitrary-name.png"
            );
            byte[] image = CreateImage(Color.red);
            File.WriteAllBytes(source, image);

            string address = ContentPackEditorStorage.ImportAsset(project, source, field);

            Assert.AreEqual("Pack/Factions/Alliance/Units/Starfighters/SFAL01/display", address);
            Assert.IsTrue(File.Exists(Local(project, address + ".png")));
            CollectionAssert.AreEqual(image, File.ReadAllBytes(source));
        }

        [Test]
        public void ImportAsset_InvalidImage_LeavesCurrentArtworkUnchanged()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            string before = field.Value;
            string source = Path.Combine(Directory.GetParent(contentRoot).FullName, "invalid.png");
            File.WriteAllText(source, "not an image");

            Assert.Throws<InvalidDataException>(() =>
                ContentPackEditorStorage.ImportAsset(project, source, field)
            );

            Assert.AreEqual(before, field.Value);
            Assert.IsFalse(
                File.Exists(
                    Local(project, "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display.png")
                )
            );
        }

        [Test]
        public void ImportAsset_ImportAsNew_PreservesExistingUnitFile()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            byte[] previous = CreateImage(Color.red);
            string address = ContentPackEditorStorage.WriteGeneratedPng(project, field, previous);
            project.SetField(field, address);
            string source = Path.Combine(Directory.GetParent(contentRoot).FullName, "other.png");
            File.WriteAllBytes(source, CreateImage(Color.blue));

            string imported = ContentPackEditorStorage.ImportAsset(project, source, field);

            Assert.AreEqual(address + "-2", imported);
            CollectionAssert.AreEqual(
                previous,
                File.ReadAllBytes(Local(project, address + ".png"))
            );
        }

        [Test]
        public void WriteGeneratedPng_InheritedArtwork_WritesOnlyToWorkingPack()
        {
            WriteFixtureFile(
                "Factions/Fixture/Data/starfighters.xml",
                "<Starfighters><Starfighter><TypeID>FIGHTER</TypeID><DisplayImagePath>Pack/Factions/Fixture/Units/Starfighters/FIGHTER/display</DisplayImagePath></Starfighter></Starfighters>"
            );
            UpdateBaseFactionDefinition(faction =>
                faction.StarfightersPath = "Factions/Fixture/Data/starfighters.xml"
            );
            WriteFixtureFile(
                "Factions/Fixture/Units/Starfighters/FIGHTER/display.png",
                "base image"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorField field = GetField(project, "FIGHTER");

            string address = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                new byte[] { 1 }
            );

            Assert.AreEqual("Pack/Factions/Fixture/Units/Starfighters/FIGHTER/display", address);
            Assert.AreEqual(
                "base image",
                File.ReadAllText(
                    Path.Combine(contentRoot, "Packs", "BasePack", address[5..] + ".png")
                )
            );
            CollectionAssert.AreEqual(
                new byte[] { 1 },
                File.ReadAllBytes(Local(project, address + ".png"))
            );
        }

        [Test]
        public void WriteGeneratedPng_HeadquartersIcon_PreservesItsInheritedUiFolder()
        {
            WriteFixtureFile(
                "Data/buildings.xml",
                "<Buildings><Building><TypeID>HQ</TypeID><BuildingType>Headquarters</BuildingType><DisplayImagePath>Pack/Factions/Alliance/Strategy/UI/Shared/galaxy-hq-icon</DisplayImagePath></Building></Buildings>"
            );
            UpdateBasePackDefinition(pack => pack.BuildingsPath = "Data/buildings.xml");
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record => record.ID == "HQ")
                .GetField("DisplayImagePath");

            string address = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                new byte[] { 1 }
            );

            Assert.AreEqual("Pack/Factions/Alliance/Strategy/UI/Shared/galaxy-hq-icon", address);
            Assert.IsTrue(File.Exists(Local(project, address + ".png")));
        }

        [Test]
        public void Convert_ImportedImage_PreservesLegacyAddressAfterLaterReplacement()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string previous = "Pack/Assets/arbitrary-name.png";
            WriteLegacy(project, field, previous, new byte[] { 1, 2, 3 });

            string report = ContentEditorArtworkConversion.Convert(project);
            string current = field.Value;
            ContentPackEditorStorage.WriteGeneratedPng(project, field, new byte[] { 4, 5 });
            ContentFileResolver resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );

            Assert.AreEqual("Pack/Factions/Alliance/Units/Starfighters/SFAL01/display", current);
            Assert.AreEqual(resolver.ResolveFile(current, ".png"), resolver.ResolveFile(previous));
            CollectionAssert.AreEqual(
                new byte[] { 4, 5 },
                File.ReadAllBytes(resolver.ResolveFile(previous))
            );
            CollectionAssert.AreEqual(
                new byte[] { 1, 2, 3 },
                File.ReadAllBytes(Local(project, previous))
            );
            StringAssert.Contains("Backup:", report);
            Assert.IsTrue(
                Directory
                    .EnumerateFiles(
                        Path.Combine(
                            project.PackRootPath,
                            ContentEditorAssetChange.EditorDirectory,
                            "Backups"
                        ),
                        "*.xml",
                        SearchOption.AllDirectories
                    )
                    .Any()
            );
        }

        [Test]
        public void Convert_ExtensionlessOldReference_RedirectsBothAddressForms()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            WriteLegacy(project, field, "Pack/Assets/portrait.png", new byte[] { 1 });
            project.SetField(field, "Pack/Assets/portrait");

            ContentEditorArtworkConversion.Convert(project);

            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            Assert.AreEqual(
                resolver.ResolveFile(field.Value, ".png"),
                resolver.ResolveFile("Pack/Assets/portrait", ".png")
            );
            Assert.AreEqual(
                resolver.ResolveFile(field.Value, ".png"),
                resolver.ResolveFile("Pack/Assets/portrait.png")
            );
        }

        [Test]
        public void Convert_PreviousGeneratedRevisions_RedirectsOlderSavedVersions()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            string folder = "Pack/Assets/Generated/starfighter/sfal01/";
            string old = folder + "display-" + new string('a', 24) + ".png";
            string current = folder + "display-" + new string('b', 24) + ".png";
            WriteLegacy(project, field, old, new byte[] { 1 });
            WriteLegacy(project, field, current, new byte[] { 2 });

            ContentEditorArtworkConversion.Convert(project);

            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            CollectionAssert.AreEqual(
                new byte[] { 2 },
                File.ReadAllBytes(resolver.ResolveFile(old))
            );
        }

        [Test]
        public void Convert_EditableGeneratedImage_PreservesItsLayerRecipe()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            byte[] bytes = { 1, 2, 3 };
            const string recipe = "{\"Version\":1}";
            string address =
                "Pack/Assets/Generated/starfighter/sfal01/display-"
                + ContentPackEditorStorage.GetArtworkRevision(bytes, recipe)
                + ".png";
            WriteLegacy(project, field, address, bytes);
            File.WriteAllText(Local(project, address) + ".layers.json", recipe);

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(recipe, ContentPackEditorStorage.ReadGeneratedLayers(project, field));
        }

        [Test]
        public void Convert_SharedImportedFile_PreservesSharingAndReportsIt()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField first = GetField(project);
            ContentPackEditorField second = GetField(project, "SFAL02");
            const string previous = "Pack/Assets/shared.png";
            WriteLegacy(project, first, previous, new byte[] { 1 });
            project.SetField(second, previous);

            string report = ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(previous, first.Value);
            Assert.AreEqual(previous, second.Value);
            StringAssert.Contains("Kept shared artwork", report);
            Assert.AreEqual(previous, ContentArtworkAliases.Load(project.PackRootPath)[previous]);
        }

        [Test]
        public void Convert_ConflictingDestination_ChangesNeitherFilesNorReferences()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField first = GetField(project);
            ContentPackEditorField second = GetField(project, "SFAL02");
            string target = Local(
                project,
                "Pack/Factions/Alliance/Units/Starfighters/SFAL02/display.png"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, new byte[] { 9 });
            WriteLegacy(project, first, "Pack/Assets/first.png", new byte[] { 1 });
            WriteLegacy(project, second, "Pack/Assets/second.png", new byte[] { 2 });

            Assert.Throws<IOException>(() => ContentEditorArtworkConversion.Convert(project));

            Assert.AreEqual("Pack/Assets/first.png", first.Value);
            Assert.IsFalse(
                File.Exists(
                    Local(project, "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display.png")
                )
            );
            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(target));
        }

        [Test]
        public void Convert_MissingSource_ChangesNeitherFilesNorReferences()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            WriteLegacy(project, field, "Pack/Assets/missing.png", new byte[] { 1 });
            File.Delete(Local(project, "Pack/Assets/missing.png"));

            Assert.Throws<FileNotFoundException>(() =>
                ContentEditorArtworkConversion.Convert(project)
            );

            Assert.AreEqual("Pack/Assets/missing.png", field.Value);
            Assert.IsFalse(
                File.Exists(Path.Combine(project.PackRootPath, ContentArtworkAliases.FileName))
            );
        }

        [Test]
        public void Convert_ConvertedPack_IsIdempotent()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            WriteLegacy(project, field, "Pack/Assets/image.png", new byte[] { 1 });
            ContentEditorArtworkConversion.Convert(project);
            string aliases = File.ReadAllText(
                Path.Combine(project.PackRootPath, ContentArtworkAliases.FileName)
            );

            Assert.IsEmpty(ContentEditorArtworkConversion.Convert(project));

            Assert.AreEqual(
                aliases,
                File.ReadAllText(Path.Combine(project.PackRootPath, ContentArtworkAliases.FileName))
            );
        }

        [Test]
        public void Convert_DifferentImageFormatOccupiesSlot_RejectsBeforeReplacingArtwork()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            string target = Local(
                project,
                "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display.png"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, new byte[] { 9 });
            WriteLegacy(project, field, "Pack/Assets/image.jpg", new byte[] { 1 });

            Assert.Throws<IOException>(() => ContentEditorArtworkConversion.Convert(project));

            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(target));
        }

        [Test]
        public void PreserveAddress_PreviouslySharedImage_DoesNotReassignOlderSavesAfterSplitting()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField first = GetField(project);
            ContentPackEditorField second = GetField(project, "SFAL02");
            const string shared = "Pack/Assets/shared.png";
            WriteLegacy(project, first, shared, new byte[] { 1 });
            project.SetField(second, shared);
            string firstAddress = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                first,
                new byte[] { 2 }
            );
            project.SetField(first, firstAddress);

            string secondAddress = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                second,
                new byte[] { 3 }
            );
            project.SetField(second, secondAddress);
            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );

            CollectionAssert.AreEqual(
                new byte[] { 1 },
                File.ReadAllBytes(resolver.ResolveFile(shared))
            );
            Assert.AreEqual(shared, ContentArtworkAliases.Load(project.PackRootPath)[shared]);
        }

        [Test]
        public async Task WriteSnapshotAsync_ArtworkUndoHistory_ExcludesPrivateRevisionsAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            string address = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                CreateImage(Color.red)
            );
            project.SetField(field, address);
            string staging = Path.Combine(Directory.GetParent(contentRoot).FullName, "snapshot");

            await ContentPackEditorExporter.WriteSnapshotAsync(
                project,
                staging,
                CancellationToken.None
            );

            Assert.IsFalse(
                Directory.Exists(Path.Combine(staging, ContentEditorAssetChange.EditorDirectory))
            );
            Assert.IsTrue(File.Exists(Path.Combine(staging, address[5..] + ".png")));
        }

        [Test]
        public void Save_LegacyArtwork_OrganizesAndReopensAtStableAddress()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            WriteLegacy(project, field, "Pack/Assets/image.png", new byte[] { 1 });

            ContentPackEditorStorage.Save(project);
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                project.ContentRootPath,
                project.PackRootPath
            );

            Assert.AreEqual(
                "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display",
                GetField(reopened).Value
            );
            Assert.IsFalse(reopened.IsDirty);
        }

        /// <summary>
        /// Gets one fixture starfighter's display image field.
        /// </summary>
        /// <param name="project">The current working project.</param>
        /// <param name="id">The unit type identifier.</param>
        /// <returns>The owned image field.</returns>
        private static ContentPackEditorField GetField(
            ContentPackEditorProject project,
            string id = "SFAL01"
        ) =>
            project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record => record.Kind == "Starfighter" && record.ID == id)
                .GetField("DisplayImagePath");

        /// <summary>
        /// Maps a pack address to its writable physical file.
        /// </summary>
        /// <param name="project">The pack owning the file.</param>
        /// <param name="address">The pack-scoped address.</param>
        /// <returns>The absolute filename.</returns>
        private static string Local(ContentPackEditorProject project, string address) =>
            Path.GetFullPath(address[5..], project.PackRootPath);

        /// <summary>
        /// Creates a file in the older import layout and assigns it to a unit.
        /// </summary>
        /// <param name="project">The working pack.</param>
        /// <param name="field">The destination artwork slot.</param>
        /// <param name="address">The previous image address.</param>
        /// <param name="bytes">The original image contents.</param>
        private static void WriteLegacy(
            ContentPackEditorProject project,
            ContentPackEditorField field,
            string address,
            byte[] bytes
        )
        {
            string file = Local(project, address);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, bytes);
            project.SetField(field, address);
        }

        /// <summary>
        /// Encodes a real image for import validation.
        /// </summary>
        /// <param name="color">The pixel color.</param>
        /// <returns>The complete PNG data.</returns>
        private static byte[] CreateImage(Color color)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
