using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.ContentEditor.Integration
{
    [TestFixture]
    [Category("Integration")]
    public sealed class ContentPackEditorStorageTests : ContentPackEditorTestBase
    {
        [TestCase("Officers")]
        [TestCase("Starfighters")]
        [TestCase("Capital ships")]
        [TestCase("Troops")]
        [TestCase("Special forces")]
        [TestCase("Buildings")]
        public void Save_AddedUnit_LoadsInTheRuntimeCatalog(string category)
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == category);
            ContentPackEditorRecord unit = project.AddUnit(catalog);

            string member =
                category == "Officers"
                    ? "RecruitingFactionInstanceIDs"
                    : "ManufacturingFactionInstanceIDs";
            string faction = project.GetFactionOptions().Last().Key;
            project.SetFieldValues(unit.GetField(member), new[] { faction });

            ContentPackEditorStorage.Save(project);

            ContentFileResolver source = ContentPackLoader.CreatePackResolver(
                project.ContentRootPath,
                project.Definition.BasePackID
            );
            ContentFileResolver resolver = new ContentFileResolver(
                project.ContentRootPath,
                project.PackRootPath,
                fallbackPackRootPaths: new[] { source.PackRootPath }
            );
            ContentPack loaded = ContentPackLoader.LoadPack(
                resolver,
                project.Definition,
                project.Definition.DefaultScenarioID
            );
            var definitions = loaded
                .GameData.Buildings.Cast<ISceneNode>()
                .Concat(loaded.GameData.CapitalShips)
                .Concat(loaded.GameData.Starfighters)
                .Concat(loaded.GameData.Regiments)
                .Concat(loaded.GameData.SpecialForces)
                .Concat(loaded.GameData.Officers);
            ISceneNode created = definitions.Single(definition =>
                definition.TypeID == unit.ID && definition.DisplayName == unit.Name
            );
            CollectionAssert.AreEqual(
                new[] { faction },
                created is Rebellion.Game.Units.Officer officer
                    ? officer.RecruitingFactionInstanceIDs
                    : (
                        (Rebellion.Game.Units.IManufacturable)created
                    ).ManufacturingFactionInstanceIDs
            );
        }

        [Test]
        public void Save_InheritedSettingEdit_WritesOnlyTheAuthoredValue()
        {
            ContentPackEditorProject project = CreateThemeProject();
            project.SetField(ThemeField(project, "FactionPrimaryColorHex"), "blue");

            ContentPackEditorStorage.Save(project);

            XElement saved = XElement.Load(ThemePath(project));
            Assert.AreEqual("true", (string)saved.Attribute("Inherit"));
            Assert.IsNull(saved.Element("FactionTheme").Element("UseUpperButtonLayout"));
            Assert.AreEqual(
                "blue",
                (string)saved.Element("FactionTheme").Element("FactionPrimaryColorHex")
            );
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );
            Assert.AreEqual("true", ThemeField(reopened, "UseUpperButtonLayout").Value);
            Assert.IsTrue(ThemeField(reopened, "UseUpperButtonLayout").IsInherited);
        }

        [Test]
        public void Save_UneditedInheritedSettings_DoesNotCreateAnOverride()
        {
            ContentPackEditorProject project = CreateThemeProject();
            Assert.AreEqual("red", ThemeField(project, "FactionPrimaryColorHex").Value);

            ContentPackEditorStorage.Save(project);

            Assert.IsFalse(File.Exists(ThemePath(project)));
        }

        [Test]
        public void OpenFolder_UpdatedBaseSetting_UsesNewValueWithoutChangingAuthoredValue()
        {
            ContentPackEditorProject project = CreateThemeProject();
            project.SetField(ThemeField(project, "FactionPrimaryColorHex"), "blue");
            ContentPackEditorStorage.Save(project);
            WriteFixtureFile("Data/theme.xml", ThemeXml(false));

            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );

            Assert.AreEqual("false", ThemeField(reopened, "UseUpperButtonLayout").Value);
            Assert.AreEqual("blue", ThemeField(reopened, "FactionPrimaryColorHex").Value);
        }

        [Test]
        public void ResetFieldToInherited_SavedOverride_RemovesFileAndPreservesUndo()
        {
            ContentPackEditorProject project = CreateThemeProject();
            project.SetField(ThemeField(project, "FactionPrimaryColorHex"), "blue");
            ContentPackEditorStorage.Save(project);

            project.ResetFieldToInherited(ThemeField(project, "FactionPrimaryColorHex"));
            ContentPackEditorStorage.Save(project);

            Assert.IsFalse(File.Exists(ThemePath(project)));
            Assert.AreEqual("red", ThemeField(project, "FactionPrimaryColorHex").Value);
            Assert.IsTrue(project.Undo());
            Assert.AreEqual("blue", ThemeField(project, "FactionPrimaryColorHex").Value);
            Assert.IsTrue(project.IsDirty);
            Assert.IsTrue(project.Redo());
            Assert.AreEqual("red", ThemeField(project, "FactionPrimaryColorHex").Value);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void Save_ExplicitFalseMatchingUpdatedBase_KeepsItsOverride()
        {
            ContentPackEditorProject project = CreateThemeProject();
            project.SetField(ThemeField(project, "UseUpperButtonLayout"), "false");
            ContentPackEditorStorage.Save(project);
            WriteFixtureFile("Data/theme.xml", ThemeXml(false));
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );
            reopened.SetField(ThemeField(reopened, "FactionPrimaryColorHex"), "blue");

            ContentPackEditorStorage.Save(reopened);

            Assert.AreEqual(
                "false",
                (string)
                    XElement
                        .Load(ThemePath(reopened))
                        .Element("FactionTheme")
                        .Element("UseUpperButtonLayout")
            );
            Assert.IsFalse(ThemeField(reopened, "UseUpperButtonLayout").IsInherited);
        }

        [Test]
        public void Save_ExternallyChangedInheritedSetting_RejectsWritingAnOverride()
        {
            ContentPackEditorProject project = CreateThemeProject();
            project.SetField(ThemeField(project, "FactionPrimaryColorHex"), "blue");
            WriteFixtureFile("Data/theme.xml", ThemeXml(false));

            Assert.Throws<IOException>(() => ContentPackEditorStorage.Save(project));
            Assert.IsFalse(File.Exists(ThemePath(project)));
        }

        [Test]
        public async Task WriteSnapshotAsync_InheritedSettingEdit_ExportsOnlyTheOverrideAsync()
        {
            ContentPackEditorProject project = CreateThemeProject();
            project.SetField(ThemeField(project, "FactionPrimaryColorHex"), "blue");
            string snapshot = Path.Combine(contentRoot, "Snapshot");
            Directory.CreateDirectory(snapshot);

            await ContentPackEditorExporter.WriteSnapshotAsync(
                project,
                snapshot,
                CancellationToken.None
            );

            XElement saved = XElement.Load(Path.Combine(snapshot, "Data/theme.xml"));
            Assert.IsNull(saved.Element("FactionTheme").Element("UseUpperButtonLayout"));
            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                snapshot
            );
            Assert.AreEqual("true", ThemeField(reopened, "UseUpperButtonLayout").Value);
            Assert.IsFalse(File.Exists(ThemePath(project)));
        }

        [Test]
        public async Task WriteSnapshotAsync_UnsavedReset_OmitsTheSavedOverrideAsync()
        {
            ContentPackEditorProject project = CreateThemeProject();
            project.SetField(ThemeField(project, "FactionPrimaryColorHex"), "blue");
            ContentPackEditorStorage.Save(project);
            project.ResetFieldToInherited(ThemeField(project, "FactionPrimaryColorHex"));
            string snapshot = Path.Combine(contentRoot, "Snapshot");
            Directory.CreateDirectory(snapshot);

            await ContentPackEditorExporter.WriteSnapshotAsync(
                project,
                snapshot,
                CancellationToken.None
            );

            Assert.IsFalse(File.Exists(Path.Combine(snapshot, "Data/theme.xml")));
            Assert.IsTrue(File.Exists(ThemePath(project)));
            Assert.AreEqual(
                "red",
                ThemeField(
                    ContentPackEditorStorage.OpenFolder(contentRoot, snapshot),
                    "FactionPrimaryColorHex"
                ).Value
            );
        }

        /// <summary>
        /// Opens an inherited theme with two independently editable settings.
        /// </summary>
        /// <returns>The isolated derived project.</returns>
        private ContentPackEditorProject CreateThemeProject()
        {
            WriteFixtureFile("Data/theme.xml", ThemeXml(true));
            return CreateWorkingProject();
        }

        /// <summary>
        /// Creates the base theme with a selectable inherited Boolean.
        /// </summary>
        /// <param name="upper">The inherited layout value.</param>
        /// <returns>The base fixture XML.</returns>
        private static string ThemeXml(bool upper) =>
            "<FactionThemes><FactionTheme><FactionInstanceID>FNEMP1</FactionInstanceID><FactionPrimaryColorHex>red</FactionPrimaryColorHex><UseUpperButtonLayout>"
            + upper.ToString().ToLowerInvariant()
            + "</UseUpperButtonLayout></FactionTheme></FactionThemes>";

        /// <summary>
        /// Finds one field through the editable public project contract.
        /// </summary>
        /// <param name="project">The project owning the theme.</param>
        /// <param name="name">The serialized setting name.</param>
        /// <returns>The editable field.</returns>
        private static ContentPackEditorField ThemeField(
            ContentPackEditorProject project,
            string name
        ) =>
            project
                .GetCatalogs()
                .Single(catalog => catalog.Document.Address == "Pack/Data/theme.xml")
                .Records.Single()
                .GetField(name);

        /// <summary>
        /// Gets the local theme override destination.
        /// </summary>
        /// <param name="project">The project owning the override.</param>
        /// <returns>The theme file path.</returns>
        private static string ThemePath(ContentPackEditorProject project) =>
            Path.Combine(project.PackRootPath, "Data/theme.xml");
    }
}
