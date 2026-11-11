using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Units;
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
                created is Officer officer
                    ? officer.RecruitingFactionInstanceIDs
                    : ((IManufacturable)created).ManufacturingFactionInstanceIDs
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

        [Test]
        public void Save_ApplicationRulesOverride_ReopensWithResolvedDefaults()
        {
            ContentPackEditorProject project = CreateApplicationRulesProject();
            project.SetField(RulesField(project), "9");
            ContentPackEditorStorage.Save(project);

            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );

            Assert.AreEqual("9", RulesField(reopened).Value);
            Assert.IsFalse(RulesField(reopened).IsInherited);
        }

        [Test]
        public async Task WriteSnapshotAsync_ResetLastApplicationRulesOverride_OmitsTheManifestLinkAsync()
        {
            ContentPackEditorProject project = CreateApplicationRulesProject();
            project.SetField(RulesField(project), "9");
            ContentPackEditorStorage.Save(project);
            project = ContentPackEditorStorage.OpenFolder(contentRoot, project.PackRootPath);
            project.ResetFieldToInherited(RulesField(project));
            string snapshot = Path.Combine(contentRoot, "RulesSnapshot");
            Directory.CreateDirectory(snapshot);

            await ContentPackEditorExporter.WriteSnapshotAsync(
                project,
                snapshot,
                CancellationToken.None
            );

            ContentPackEditorProject exported = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                snapshot
            );
            Assert.IsTrue(string.IsNullOrWhiteSpace(exported.Definition.GameConfigPath));
            Assert.AreEqual("5", RulesField(exported).Value);
            Assert.IsTrue(project.Undo());
            Assert.AreEqual("9", RulesField(project).Value);
            Assert.AreEqual("Shared/Data/game-config.xml", project.Definition.GameConfigPath);
        }

        [Test]
        public void Save_ResetLastApplicationRulesOverride_RemovesTheManifestLink()
        {
            ContentPackEditorProject project = CreateApplicationRulesProject();
            project.SetField(RulesField(project), "9");
            ContentPackEditorStorage.Save(project);
            project.ResetFieldToInherited(RulesField(project));

            ContentPackEditorStorage.Save(project);

            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );
            Assert.IsTrue(string.IsNullOrWhiteSpace(reopened.Definition.GameConfigPath));
            Assert.AreEqual("5", RulesField(reopened).Value);
        }

        [Test]
        public void OpenFolder_LowestRulesLayerInheritsApplicationDefaults_ResolvesTheLayer()
        {
            CreateApplicationRulesProject();
            WriteFixtureFile(
                "Data/rules.xml",
                "<GameConfig Inherit='true'><Movement><DistanceDivisor>7</DistanceDivisor></Movement></GameConfig>"
            );
            string manifest = Path.Combine(contentRoot, "Packs/BasePack/pack.xml");
            ContentPackDefinition definition =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(manifest);
            definition.GameConfigPath = "Data/rules.xml";
            ContentDefinitionSerializer.Serialize(manifest, definition);
            ContentPackEditorProject derived = ContentPackEditorStorage.Create(
                contentRoot,
                "base-pack",
                "second-pack",
                "Second pack",
                "1.0.0"
            );
            Assert.AreEqual("7", RulesField(derived).Value);
        }

        [Test]
        public void Save_ChangedApplicationRules_RejectsTheStaleRulesOverride()
        {
            ContentPackEditorProject project = CreateApplicationRulesProject();
            project.SetField(RulesField(project), "9");
            ContentPackEditorStorage.Save(project);
            project = ContentPackEditorStorage.OpenFolder(contentRoot, project.PackRootPath);
            project.SetField(RulesField(project), "11");
            File.WriteAllText(
                Path.Combine(contentRoot, "Application/Rules/game.xml"),
                "<GameConfig/>"
            );
            Assert.Throws<IOException>(() => ContentPackEditorStorage.Save(project));
        }

        [Test]
        public Task Save_ResetLegacySparseRules_PreservesUnrelatedResolvedSettingsAsync() =>
            VerifyLegacyRuleResetAsync(false);

        [Test]
        public Task WriteSnapshotAsync_ResetLegacySparseRules_PreservesUnrelatedResolvedSettingsAsync() =>
            VerifyLegacyRuleResetAsync(true);

        [Test]
        public void ResetFieldToInherited_LegacySparseRules_PreservesApplicationDefaultsWhenBasePackDiffers()
        {
            ContentPackEditorProject project = CreateLegacyRulesProject(false, 21);
            GameConfig before = ContentPackLoader.LoadGameConfig(
                contentRoot,
                project.PackRootPath,
                project.Definition.GameConfigPath
            );

            project.ResetFieldToInherited(RulesField(project));
            ContentPackEditorStorage.Save(project);

            GameConfig after = ContentPackLoader.LoadGameConfig(
                contentRoot,
                project.PackRootPath,
                project.Definition.GameConfigPath
            );
            Assert.AreEqual(before.Research.BaseResearchPoints, after.Research.BaseResearchPoints);
            Assert.AreNotEqual(21, after.Research.BaseResearchPoints);
        }

        [Test]
        public void Save_EditingLegacySparseRules_KeepsUneditedDefaultsUnauthored()
        {
            ContentPackEditorProject project = CreateLegacyRulesProject();
            project.SetField(RulesField(project), "11");
            ContentPackEditorStorage.Save(project);
            XElement saved = XElement.Load(
                Path.Combine(project.PackRootPath, "Shared/Data/game-config.xml")
            );
            Assert.IsNull(saved.Attribute("Inherit"));
            Assert.IsNull(saved.Element("DifficultyModifiers"));
            Assert.AreEqual(2, saved.Elements().Count());
        }

        [Test]
        public void ResetFieldToInherited_LegacySparseRules_UndoRestoresTheOriginalAuthoredText()
        {
            ContentPackEditorProject project = CreateLegacyRulesProject();
            string authored = RulesField(project).Document.Text;
            project.ResetFieldToInherited(RulesField(project));
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(authored, RulesField(project).Document.Text);
            Assert.IsTrue(project.Redo());
            Assert.IsNotNull(
                XElement.Parse(RulesField(project).Document.Text).Element("DifficultyModifiers")
            );
        }

        /// <summary>
        /// Verifies that resetting a legacy rule preserves resolved settings through persistence.
        /// </summary>
        /// <param name="snapshot">Whether to verify a snapshot export.</param>
        /// <returns>The completed save or snapshot verification.</returns>
        private async Task VerifyLegacyRuleResetAsync(bool snapshot)
        {
            ContentPackEditorProject project = CreateLegacyRulesProject();
            ContentFileResolver resolver = ContentPackLoader.CreateFolderResolver(
                contentRoot,
                project.PackRootPath,
                project.Definition
            );
            string defaults = ContentXmlOverrides.Load(resolver, "Application/Rules/game.xml");
            XElement expected = XElement.Parse(
                ContentXmlOverrides.Load(resolver, "Pack/Shared/Data/game-config.xml", defaults)
            );
            string inherited = ContentXmlOverrides.Load(
                ContentPackLoader.CreatePackResolver(contentRoot, "base-pack"),
                "Pack/Shared/Data/game-config.xml",
                defaults
            );
            expected
                .Element("Movement")
                .SetElementValue(
                    "DistanceDivisor",
                    XElement.Parse(inherited).Element("Movement").Element("DistanceDivisor").Value
                );

            project.ResetFieldToInherited(RulesField(project));
            string destination = project.PackRootPath;
            if (snapshot)
            {
                destination = Path.Combine(contentRoot, "LegacySnapshot");
                Directory.CreateDirectory(destination);
                await ContentPackEditorExporter.WriteSnapshotAsync(
                    project,
                    destination,
                    CancellationToken.None
                );
            }
            else
                ContentPackEditorStorage.Save(project);

            ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                destination
            );
            resolver = ContentPackLoader.CreateFolderResolver(
                contentRoot,
                destination,
                reopened.Definition
            );
            XElement actual = XElement.Parse(
                ContentXmlOverrides.Load(resolver, "Pack/Shared/Data/game-config.xml", defaults)
            );
            ContentPack loaded = ContentPackLoader.LoadPack(
                resolver,
                reopened.Definition,
                reopened.Definition.DefaultScenarioID
            );
            Assert.IsTrue(
                XNode.DeepEquals(ComparableRules(expected), ComparableRules(actual)),
                "Reset must change only the selected rule."
            );
            Assert.AreEqual(3, loaded.GameData.GameConfig.Research.BaseResearchPoints);
        }

        /// <summary>
        /// Compares dictionary entries by key while preserving ordered values and ignoring XML comments.
        /// </summary>
        /// <param name="rules">The resolved rules to compare.</param>
        /// <returns>An independent rules tree with canonical dictionary entry order.</returns>
        private static XElement ComparableRules(XElement rules)
        {
            XElement result = new XElement(rules);
            result.DescendantNodes().OfType<XComment>().Remove();
            foreach (XElement parent in result.DescendantsAndSelf().Reverse().ToArray())
                if (
                    parent.HasElements
                    && parent
                        .Elements()
                        .All(child => child.Name == "Entry" && child.Element("Key") != null)
                )
                    parent.ReplaceNodes(
                        parent
                            .Elements()
                            .OrderBy(
                                child =>
                                    child.Element("Key").ToString(SaveOptions.DisableFormatting),
                                System.StringComparer.Ordinal
                            )
                            .Select(child => new XElement(child))
                            .ToArray()
                    );
            return result;
        }

        /// <summary>
        /// Opens a legacy sparse local rules file over independently configured application and pack defaults.
        /// </summary>
        /// <param name="localResearch">Whether the local file explicitly changes research.</param>
        /// <param name="baseResearch">An optional inherited pack value distinct from the application default.</param>
        /// <returns>The isolated authored project.</returns>
        private ContentPackEditorProject CreateLegacyRulesProject(
            bool localResearch = true,
            int? baseResearch = null
        )
        {
            ContentPackEditorProject project = CreateLoadableProject();
            if (baseResearch.HasValue)
            {
                string path = Path.Combine(
                    contentRoot,
                    "Packs/BasePack/Shared/Data/game-config.xml"
                );
                XElement xml = XElement.Load(path);
                xml.Element("Research").SetElementValue("BaseResearchPoints", baseResearch.Value);
                xml.Save(path);
            }
            string manifest = Path.Combine(project.PackRootPath, "pack.xml");
            ContentPackDefinition definition =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(manifest);
            definition.GameConfigPath = "Shared/Data/game-config.xml";
            ContentDefinitionSerializer.Serialize(manifest, definition);
            string target = Path.Combine(project.PackRootPath, definition.GameConfigPath);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(
                target,
                "<GameConfig><Movement><DistanceDivisor>9</DistanceDivisor></Movement>"
                    + (
                        localResearch
                            ? "<Research><BaseResearchPoints>3</BaseResearchPoints></Research>"
                            : string.Empty
                    )
                    + "</GameConfig>"
            );
            return ContentPackEditorStorage.OpenFolder(contentRoot, project.PackRootPath);
        }

        /// <summary>
        /// Creates editable rules whose only inherited source is the application document.
        /// </summary>
        /// <returns>The temporary derived project.</returns>
        private ContentPackEditorProject CreateApplicationRulesProject()
        {
            string path = Path.Combine(contentRoot, "Application/Rules/game.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(
                path,
                "<GameConfig><Movement><DistanceDivisor>5</DistanceDivisor></Movement></GameConfig>"
            );
            return CreateWorkingProject();
        }

        /// <summary>
        /// Gets the editable distance divisor from the public catalog contract.
        /// </summary>
        /// <param name="project">The project owning the rules.</param>
        /// <returns>The distance-divisor setting.</returns>
        private static ContentPackEditorField RulesField(ContentPackEditorProject project) =>
            project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Rules")
                .Records.Single()
                .GetFields()
                .Single(field => field.MemberName == "DistanceDivisor");

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
