using System;
using System.Collections.Generic;
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
        public void Convert_ExistingBattleResultImage_PreservesBothImages()
        {
            const string folder = "Pack/Factions/Fixture/Units/CapitalShips/CSAL016/";
            WriteFixtureFile(
                "Factions/Fixture/Data/ships.xml",
                "<CapitalShips><CapitalShip><TypeID>CSAL016</TypeID>"
                    + $"<BattleResultImagePath>{folder}battle-result</BattleResultImagePath>"
                    + "</CapitalShip></CapitalShips>"
            );
            UpdateBaseFactionDefinition(faction =>
                faction.CapitalShipsPath = "Factions/Fixture/Data/ships.xml"
            );
            using ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorField field = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Single(record => record.Kind == "CapitalShip" && record.ID == "CSAL016")
                .GetField("BattleResultImagePath");
            byte[] existing = CreateImage(Color.red);
            byte[] imported = CreateImage(Color.blue);
            string occupied = Local(project, folder + "battle-result.png");
            Directory.CreateDirectory(Path.GetDirectoryName(occupied));
            File.WriteAllBytes(occupied, existing);
            const string previous = "Pack/Assets/custom-battle-result.png";
            WriteLegacy(project, field, previous, imported);

            Assert.DoesNotThrow(() => ContentEditorArtworkConversion.Convert(project));

            Assert.AreEqual(folder + "battle-result-2", field.Value);
            CollectionAssert.AreEqual(existing, File.ReadAllBytes(occupied));
            CollectionAssert.AreEqual(imported, File.ReadAllBytes(Local(project, previous)));
            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            CollectionAssert.AreEqual(
                imported,
                File.ReadAllBytes(resolver.ResolveFile(previous, ".png"))
            );
            Assert.AreEqual(occupied, resolver.ResolveFile(folder + "battle-result", ".png"));
        }

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
        public void Convert_MatchingNumberedDestination_ReusesTheExistingImage()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            string occupied = Local(project, preferred + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(occupied));
            File.WriteAllBytes(occupied, new byte[] { 9 });
            File.WriteAllBytes(Local(project, preferred + "-2.png"), new byte[] { 1 });
            WriteLegacy(project, field, "Pack/Assets/image.png", new byte[] { 1 });

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(preferred + "-2", field.Value);
            Assert.IsFalse(File.Exists(Local(project, preferred + "-3.png")));
            Assert.IsEmpty(ContentEditorArtworkConversion.Convert(project));
            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(occupied));
        }

        [TestCase(".layers.json")]
        [TestCase(".layers.hash")]
        public void Convert_OccupiedLayerSidecar_PreservesUnrelatedMetadata(string sidecar)
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            string occupied = Local(project, preferred + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(occupied));
            File.WriteAllBytes(occupied, new byte[] { 1 });
            File.WriteAllText(occupied + sidecar, "Unrelated layer metadata");
            WriteLegacy(project, field, "Pack/Assets/image.png", new byte[] { 1 });

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(preferred + "-2", field.Value);
            Assert.AreEqual("Unrelated layer metadata", File.ReadAllText(occupied + sidecar));
            Assert.IsNull(ContentPackEditorStorage.ReadGeneratedLayers(project, field));
        }

        [Test]
        public void Convert_InheritedNumberedImage_DoesNotShadowTheExistingImage()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            string inherited = Path.GetFullPath(
                preferred[5..] + "-2.png",
                Path.Combine(contentRoot, "Packs", "BasePack")
            );
            Directory.CreateDirectory(Path.GetDirectoryName(inherited));
            File.WriteAllBytes(inherited, new byte[] { 9 });
            string occupied = Local(project, preferred + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(occupied));
            File.WriteAllBytes(occupied, new byte[] { 8 });
            WriteLegacy(project, field, "Pack/Assets/image.png", new byte[] { 1 });

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(preferred + "-3", field.Value);
            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            Assert.AreEqual(inherited, resolver.ResolveFile(preferred + "-2", ".png"));
            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(inherited));
        }

        [TestCase("")]
        [TestCase(".png")]
        public void Convert_InheritedNumberedRedirect_DoesNotShadowItsExistingTarget(
            string extension
        )
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            const string reserved = "Pack/Shared/other.png";
            string inheritedRoot = Path.Combine(contentRoot, "Packs", "BasePack");
            string inherited = Path.GetFullPath(reserved[5..], inheritedRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(inherited));
            File.WriteAllBytes(inherited, new byte[] { 9 });
            File.WriteAllText(
                Path.Combine(inheritedRoot, ContentArtworkAliases.FileName),
                ContentArtworkAliases.Serialize(
                    new Dictionary<string, string> { [preferred + "-2" + extension] = reserved }
                )
            );
            string occupied = Local(project, preferred + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(occupied));
            File.WriteAllBytes(occupied, new byte[] { 8 });
            WriteLegacy(project, field, "Pack/Assets/image.png", new byte[] { 1 });

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(preferred + "-3", field.Value);
            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            Assert.AreEqual(inherited, resolver.ResolveFile(preferred + "-2" + extension, ".png"));
        }

        [TestCase("")]
        [TestCase(".png")]
        [TestCase(".jpg")]
        [TestCase(".jpeg")]
        public void Convert_InheritedPreferredRedirect_DoesNotShadowItsExistingTarget(
            string extension
        )
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            const string reserved = "Pack/Shared/other.png";
            string inheritedRoot = Path.Combine(contentRoot, "Packs", "BasePack");
            string inherited = Path.GetFullPath(reserved[5..], inheritedRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(inherited));
            File.WriteAllBytes(inherited, new byte[] { 9 });
            string aliasPath = Path.Combine(inheritedRoot, ContentArtworkAliases.FileName);
            string aliasText = ContentArtworkAliases.Serialize(
                new Dictionary<string, string> { [preferred + extension] = reserved }
            );
            File.WriteAllText(aliasPath, aliasText);
            const string previous = "Pack/Assets/image.png";
            WriteLegacy(project, field, previous, new byte[] { 1 });

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(preferred + "-2", field.Value);
            Assert.IsFalse(File.Exists(Local(project, preferred + ".png")));
            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            Assert.AreEqual(inherited, resolver.ResolveFile(preferred + extension, ".png"));
            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(inherited));
            Assert.AreEqual(aliasText, File.ReadAllText(aliasPath));
            Assert.AreEqual(
                Local(project, field.Value + ".png"),
                resolver.ResolveFile(previous, ".png")
            );
        }

        [TestCase("")]
        [TestCase(".png")]
        public void Convert_InheritedPreferredRedirectWithoutTarget_DoesNotClaimItsAddress(
            string extension
        )
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            string inheritedRoot = Path.Combine(contentRoot, "Packs", "BasePack");
            File.WriteAllText(
                Path.Combine(inheritedRoot, ContentArtworkAliases.FileName),
                ContentArtworkAliases.Serialize(
                    new Dictionary<string, string>
                    {
                        [preferred + extension] = "Pack/Shared/missing.png",
                    }
                )
            );
            WriteLegacy(project, field, "Pack/Assets/image.png", new byte[] { 1 });

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(preferred + "-2", field.Value);
            Assert.IsFalse(File.Exists(Local(project, preferred + ".png")));
            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            Assert.IsNull(resolver.ResolveFile(preferred + extension, ".png"));
        }

        [TestCase("")]
        [TestCase(".png")]
        public void Convert_InheritedPreferredIdentityMapping_KeepsNormalImageOverride(
            string extension
        )
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            string inheritedRoot = Path.Combine(contentRoot, "Packs", "BasePack");
            string inherited = Path.Combine(inheritedRoot, preferred[5..] + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(inherited));
            File.WriteAllBytes(inherited, new byte[] { 9 });
            File.WriteAllText(
                Path.Combine(inheritedRoot, ContentArtworkAliases.FileName),
                ContentArtworkAliases.Serialize(
                    new Dictionary<string, string>
                    {
                        [preferred + extension] = preferred + extension,
                    }
                )
            );
            WriteLegacy(project, field, "Pack/Assets/image.png", new byte[] { 1 });

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(preferred, field.Value);
            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            Assert.AreEqual(
                Local(project, preferred + ".png"),
                resolver.ResolveFile(preferred + extension, ".png")
            );
            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(inherited));
        }

        [Test]
        public void Convert_PreferredImageExistsInBase_WritesOnlyToTheWorkingPack()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            string inherited = Path.Combine(
                contentRoot,
                "Packs",
                "BasePack",
                preferred[5..] + ".png"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(inherited));
            File.WriteAllBytes(inherited, new byte[] { 9 });
            WriteLegacy(project, field, "Pack/Assets/image.png", new byte[] { 1 });

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(preferred, field.Value);
            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(inherited));
            CollectionAssert.AreEqual(
                new byte[] { 1 },
                File.ReadAllBytes(Local(project, preferred + ".png"))
            );
        }

        [Test]
        public void Convert_ConflictingLayerRecipe_PreservesBothEditableImages()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            byte[] bytes = { 1, 2, 3 };
            const string recipe = "{\"Version\":1}";
            const string existingRecipe = "{\"Version\":2}";
            string previous =
                "Pack/Assets/Generated/starfighter/sfal01/display-"
                + ContentPackEditorStorage.GetArtworkRevision(bytes, recipe)
                + ".png";
            WriteLegacy(project, field, previous, bytes);
            File.WriteAllText(Local(project, previous) + ".layers.json", recipe);
            string occupied = Local(project, preferred + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(occupied));
            File.WriteAllBytes(occupied, bytes);
            File.WriteAllText(occupied + ".layers.json", existingRecipe);
            string existingHash = ContentPackEditorStorage.GetArtworkRevision(
                bytes,
                existingRecipe
            );
            File.WriteAllText(occupied + ".layers.hash", existingHash);

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(preferred + "-2", field.Value);
            Assert.AreEqual(existingRecipe, File.ReadAllText(occupied + ".layers.json"));
            Assert.AreEqual(existingHash, File.ReadAllText(occupied + ".layers.hash"));
            Assert.AreEqual(recipe, ContentPackEditorStorage.ReadGeneratedLayers(project, field));
        }

        [TestCase("")]
        [TestCase(".png")]
        public void Convert_RedirectOwnsDestination_PreservesItsExistingTarget(string extension)
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            const string reserved = "Pack/Assets/reserved.png";
            WriteLegacy(project, field, reserved, new byte[] { 9 });
            WriteLegacy(project, field, "Pack/Assets/current.png", new byte[] { 1 });
            string occupied = Local(project, preferred + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(occupied));
            File.WriteAllBytes(occupied, new byte[] { 1 });
            File.WriteAllText(
                Path.Combine(project.PackRootPath, ContentArtworkAliases.FileName),
                ContentArtworkAliases.Serialize(
                    new Dictionary<string, string> { [preferred + extension] = reserved }
                )
            );

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(preferred + "-2", field.Value);
            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            CollectionAssert.AreEqual(
                new byte[] { 9 },
                File.ReadAllBytes(resolver.ResolveFile(preferred + extension, ".png"))
            );
            CollectionAssert.AreEqual(
                new byte[] { 1 },
                File.ReadAllBytes(resolver.ResolveFile(field.Value, ".png"))
            );
        }

        [Test]
        public void Save_ConflictingLegacyArtwork_ReopensAtTheAllocatedAddress()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            string occupied = Local(project, preferred + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(occupied));
            File.WriteAllBytes(occupied, new byte[] { 9 });
            const string previous = "Pack/Assets/image.png";
            WriteLegacy(project, field, previous, new byte[] { 1 });

            ContentPackEditorStorage.Save(project);
            using ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                project.ContentRootPath,
                project.PackRootPath
            );

            Assert.AreEqual(preferred + "-2", GetField(reopened).Value);
            Assert.IsFalse(reopened.IsDirty);
            var resolver = ContentPackLoader.CreateFolderResolver(
                reopened.ContentRootPath,
                reopened.PackRootPath,
                reopened.Definition
            );
            CollectionAssert.AreEqual(
                new byte[] { 1 },
                File.ReadAllBytes(resolver.ResolveFile(previous, ".png"))
            );
            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(occupied));
        }

        [Test]
        public void WriteGeneratedPng_ConvertedCollision_KeepsTheAllocatedAddress()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            string occupied = Local(project, preferred + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(occupied));
            File.WriteAllBytes(occupied, new byte[] { 9 });
            WriteLegacy(project, field, "Pack/Assets/image.png", new byte[] { 1 });
            ContentEditorArtworkConversion.Convert(project);

            string address = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                new byte[] { 2 }
            );

            Assert.AreEqual(preferred + "-2", address);
            CollectionAssert.AreEqual(
                new byte[] { 2 },
                File.ReadAllBytes(Local(project, address + ".png"))
            );
            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(occupied));
        }

        [Test]
        public void Convert_AllocatedDestination_UndoRedoRestoresImagesAndAddresses()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string preferred = "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display";
            string occupied = Local(project, preferred + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(occupied));
            File.WriteAllBytes(occupied, new byte[] { 9 });
            const string previous = "Pack/Assets/image.png";
            WriteLegacy(project, field, previous, new byte[] { 1 });
            ContentEditorArtworkConversion.Convert(project);
            string allocated = Local(project, preferred + "-2.png");

            project.Undo();

            Assert.AreEqual(previous, GetField(project).Value);
            Assert.IsFalse(File.Exists(allocated));
            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(occupied));
            project.Redo();
            Assert.AreEqual(preferred + "-2", GetField(project).Value);
            CollectionAssert.AreEqual(new byte[] { 1 }, File.ReadAllBytes(allocated));
            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(occupied));
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
        public void Convert_ConflictingDestination_ConvertsAllWithoutReplacingExistingImages()
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

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(
                "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display",
                first.Value
            );
            Assert.AreEqual(
                "Pack/Factions/Alliance/Units/Starfighters/SFAL02/display-2",
                second.Value
            );
            CollectionAssert.AreEqual(
                new byte[] { 1 },
                File.ReadAllBytes(Local(project, first.Value + ".png"))
            );
            CollectionAssert.AreEqual(
                new byte[] { 2 },
                File.ReadAllBytes(Local(project, second.Value + ".png"))
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
        public void Convert_DifferentImageFormatOccupiesSlot_PreservesBothFormats()
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

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(
                "Pack/Factions/Alliance/Units/Starfighters/SFAL01/display-2",
                field.Value
            );
            CollectionAssert.AreEqual(
                new byte[] { 1 },
                File.ReadAllBytes(Local(project, field.Value + ".jpg"))
            );
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

        [TestCase(".png", ".jpg")]
        [TestCase(".jpg", ".png")]
        public void Convert_PendingHeadquartersFormats_PreservesIndependentImages(
            string firstExtension,
            string secondExtension
        )
        {
            const string icon = "Pack/Factions/Alliance/Strategy/UI/Shared/galaxy-hq-icon";
            WriteFixtureFile(
                "Data/buildings.xml",
                "<Buildings><Building><TypeID>FIRST</TypeID><BuildingType>Headquarters</BuildingType>"
                    + $"<DisplayImagePath>{icon}</DisplayImagePath></Building>"
                    + "<Building><TypeID>SECOND</TypeID><BuildingType>Headquarters</BuildingType>"
                    + $"<DisplayImagePath>{icon}</DisplayImagePath></Building></Buildings>"
            );
            UpdateBasePackDefinition(pack => pack.BuildingsPath = "Data/buildings.xml");
            ContentPackEditorProject project = CreateWorkingProject();
            ContentPackEditorField[] fields = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind == "Building")
                .Select(record => record.GetField("DisplayImagePath"))
                .ToArray();
            WriteLegacy(project, fields[0], "Pack/Assets/first" + firstExtension, new byte[] { 1 });
            WriteLegacy(
                project,
                fields[1],
                "Pack/Assets/second" + secondExtension,
                new byte[] { 2 }
            );

            ContentEditorArtworkConversion.Convert(project);

            Assert.AreEqual(icon, fields[0].Value);
            Assert.AreEqual(icon + "-2", fields[1].Value);
            CollectionAssert.AreEqual(
                new byte[] { 1 },
                File.ReadAllBytes(Local(project, fields[0].Value + firstExtension))
            );
            CollectionAssert.AreEqual(
                new byte[] { 2 },
                File.ReadAllBytes(Local(project, fields[1].Value + secondExtension))
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PreserveAddress_EquivalentSharedAddresses_RetainsOriginalImage(
            bool explicitFirst
        )
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField first = GetField(project);
            ContentPackEditorField second = GetField(project, "SFAL02");
            const string filename = "Pack/Assets/shared.png";
            const string extensionless = "Pack/Assets/shared";
            WriteLegacy(project, first, filename, new byte[] { 1 });
            project.SetField(first, explicitFirst ? filename : extensionless);
            project.SetField(second, explicitFirst ? extensionless : filename);
            string address = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                first,
                new byte[] { 2 }
            );
            project.SetField(first, address);

            Assert.DoesNotThrow(() => ContentEditorArtworkConversion.Convert(project));

            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            CollectionAssert.AreEqual(
                new byte[] { 1 },
                File.ReadAllBytes(resolver.ResolveFile(filename))
            );
            Assert.AreEqual(
                resolver.ResolveFile(filename),
                resolver.ResolveFile(extensionless, ".png")
            );
            Assert.AreEqual(explicitFirst ? extensionless : filename, second.Value);
        }

        [TestCase(".png", false)]
        [TestCase(".jpg", false)]
        [TestCase(".jpeg", false)]
        [TestCase(".png", true)]
        public void WriteGeneratedPng_ExtensionlessLegacyImage_PreservesSavedFilename(
            string extension,
            bool redirectedFilename
        )
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string previous = "Pack/Assets/portrait";
            WriteLegacy(project, field, previous + extension, new byte[] { 1 });
            project.SetField(field, previous);
            if (redirectedFilename)
                File.WriteAllText(
                    Path.Combine(project.PackRootPath, ContentArtworkAliases.FileName),
                    ContentArtworkAliases.Serialize(
                        new Dictionary<string, string> { [previous + extension] = previous }
                    )
                );

            string address = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                new byte[] { 2 }
            );
            project.SetField(field, address);
            ContentEditorArtworkConversion.Convert(project);

            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            Assert.AreEqual(
                resolver.ResolveFile(address, ".png"),
                resolver.ResolveFile(previous, ".png", ".jpg", ".jpeg")
            );
            Assert.AreEqual(
                resolver.ResolveFile(address, ".png"),
                resolver.ResolveFile(previous + extension)
            );
        }

        [Test]
        public void WriteGeneratedPng_RedirectedLegacyAddress_PreservesExistingChain()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            const string filename = "Pack/Assets/source.png";
            const string previous = "Pack/Assets/portrait";
            WriteLegacy(project, field, filename, new byte[] { 1 });
            File.WriteAllText(
                Path.Combine(project.PackRootPath, ContentArtworkAliases.FileName),
                ContentArtworkAliases.Serialize(
                    new Dictionary<string, string> { [previous] = filename }
                )
            );
            project.SetField(field, previous);

            string address = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                field,
                new byte[] { 2 }
            );

            Assert.AreEqual(filename, ContentArtworkAliases.Load(project.PackRootPath)[previous]);
            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            Assert.AreEqual(
                resolver.ResolveFile(address, ".png"),
                resolver.ResolveFile(previous, ".png")
            );
        }

        [Test]
        public void PreserveAddress_DifferentFormatsWithSameBasename_PreservesIndependentImages()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField first = GetField(project);
            ContentPackEditorField second = GetField(project, "SFAL02");
            WriteLegacy(project, first, "Pack/Assets/shared.png", new byte[] { 1 });
            WriteLegacy(project, second, "Pack/Assets/shared.jpg", new byte[] { 2 });

            string address = ContentPackEditorStorage.WriteGeneratedPng(
                project,
                first,
                new byte[] { 3 }
            );
            project.SetField(first, address);
            ContentEditorArtworkConversion.Convert(project);

            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            CollectionAssert.AreEqual(
                new byte[] { 3 },
                File.ReadAllBytes(resolver.ResolveFile("Pack/Assets/shared.png"))
            );
            CollectionAssert.AreEqual(
                new byte[] { 2 },
                File.ReadAllBytes(resolver.ResolveFile("Pack/Assets/shared.jpg"))
            );
        }

        [Test]
        public void ImportAsset_ConvertedGeneratedArtwork_PreservesRevisionRedirectChain()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            string revision =
                "Pack/Assets/Generated/starfighter/sfal01/display-" + new string('a', 24) + ".png";
            WriteLegacy(project, field, revision, CreateImage(Color.red));
            ContentEditorArtworkConversion.Convert(project);
            string converted = field.Value;
            string source = Path.Combine(Directory.GetParent(contentRoot).FullName, "new.png");
            File.WriteAllBytes(source, CreateImage(Color.blue));

            string imported = ContentPackEditorStorage.ImportAsset(project, source, field);

            Assert.AreEqual(converted + "-2", imported);
            Assert.AreEqual(converted, ContentArtworkAliases.Load(project.PackRootPath)[revision]);
            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            Assert.AreEqual(resolver.ResolveFile(imported, ".png"), resolver.ResolveFile(revision));
        }

        [Test]
        public void WriteGeneratedPng_OmittedArtworkSlot_UsesReversibleUnitFile()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetOmittedField(project);
            string address;
            using (project.BeginEdit("First artwork"))
            {
                address = ContentPackEditorStorage.WriteGeneratedPng(
                    project,
                    field,
                    CreateImage(Color.red)
                );
                project.SetField(field, address);
            }

            Assert.AreEqual("Pack/Factions/Alliance/Units/Starfighters/SFAL01/display", address);
            Assert.IsTrue(File.Exists(Local(project, address + ".png")));
            project.Undo();
            Assert.IsFalse(File.Exists(Local(project, address + ".png")));
        }

        [Test]
        public void ImportAsset_OmittedArtworkSlot_UsesReversiblePngFile()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetOmittedField(project);
            string source = Path.Combine(Directory.GetParent(contentRoot).FullName, "first.jpg");
            File.WriteAllBytes(source, CreateImage(Color.red, jpeg: true));
            string address;
            using (project.BeginEdit("First artwork"))
            {
                address = ContentPackEditorStorage.ImportAsset(project, source, field);
                project.SetField(field, address);
            }

            Assert.AreEqual("Pack/Factions/Alliance/Units/Starfighters/SFAL01/display", address);
            byte[] stored = File.ReadAllBytes(Local(project, address + ".png"));
            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71 }, stored.Take(4).ToArray());
            project.Undo();
            Assert.IsFalse(File.Exists(Local(project, address + ".png")));
        }

        [Test]
        public void ImportAsset_RedirectedMatchingFile_AllocatesAddressForImportedImage()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = GetField(project);
            string source = Path.Combine(Directory.GetParent(contentRoot).FullName, "source.png");
            File.WriteAllBytes(source, CreateImage(Color.red));
            string first = ContentPackEditorStorage.ImportAsset(project, source, field);
            project.SetField(field, first);
            File.WriteAllBytes(source, CreateImage(Color.blue));
            string second = ContentPackEditorStorage.ImportAsset(project, source, field);
            project.SetField(field, second);
            File.WriteAllBytes(source, CreateImage(Color.red));

            string third = ContentPackEditorStorage.ImportAsset(project, source, field);

            var resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            CollectionAssert.AreEqual(
                File.ReadAllBytes(source),
                File.ReadAllBytes(resolver.ResolveFile(third, ".png"))
            );
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
        /// Gets a detached optional display image member with no authored value.
        /// </summary>
        /// <param name="project">The working project.</param>
        /// <returns>The omitted image field.</returns>
        private static ContentPackEditorField GetOmittedField(ContentPackEditorProject project)
        {
            ContentPackEditorField field = GetField(project);
            field.XmlElement.Remove();
            field = field.Record.GetField("DisplayImagePath");
            Assert.IsNull(field.XmlElement.Parent);
            return field;
        }

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
        /// <param name="jpeg">Whether to encode JPEG instead of PNG.</param>
        /// <returns>The complete encoded image data.</returns>
        private static byte[] CreateImage(Color color, bool jpeg = false)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply();
                return jpeg ? texture.EncodeToJPG() : texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
