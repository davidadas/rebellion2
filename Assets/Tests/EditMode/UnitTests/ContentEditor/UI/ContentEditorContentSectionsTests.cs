using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorContentSectionsTests
    {
        private string folder;

        /// <summary>
        /// Creates an isolated folder for authored catalog fixtures.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(
                Path.GetTempPath(),
                "editor-sections-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(folder);
        }

        /// <summary>
        /// Removes the fixture-owned catalog folder.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Directory.Delete(folder, true);
        }

        [Test]
        public void EncyclopediaFields_MissingEmbeddedData_EditsOnlyTheStandaloneFallback()
        {
            ContentPackEditorCatalog units = Catalog(
                "officers",
                "<Officers><Officer><TypeID>OFF1</TypeID></Officer></Officers>"
            );
            ContentPackEditorCatalog articles = Catalog(
                "articles",
                "<EncyclopediaEntries><EncyclopediaEntry><TypeID>OFF1</TypeID><Description>Old article</Description></EncyclopediaEntry></EncyclopediaEntries>"
            );
            ContentEditorContentSections
                .EncyclopediaFields(units.Records[0], new[] { units, articles })
                .Single(field => field.Group == "Description")
                .SetValue("New article");
            Assert.AreEqual("New article", articles.Records[0].GetField("Description").Value);
            Assert.IsFalse(units.Document.IsDirty);
        }

        [Test]
        public void EncyclopediaFields_NewEntity_OffersOptionalFieldsWithoutWritingAnOverride()
        {
            ContentPackEditorCatalog units = Catalog(
                "officers",
                "<Officers><Officer><TypeID>OFF1</TypeID></Officer></Officers>"
            );
            var fields = ContentEditorContentSections.EncyclopediaFields(
                units.Records[0],
                new[] { units }
            );
            Assert.IsTrue(fields.Any(field => field.Group == "EncyclopediaDescription"));
            Assert.IsTrue(fields.Any(field => field.Group == "EncyclopediaImagePath"));
            Assert.IsFalse(units.Document.IsDirty);
            fields
                .Single(field => field.Group == "EncyclopediaDescription")
                .SetValue("New officer article");
            Assert.AreEqual(
                "New officer article",
                units.Records[0].GetField("EncyclopediaDescription").Value
            );
        }

        [TestCase("<EncyclopediaDescription>Embedded article</EncyclopediaDescription>")]
        [TestCase("<EncyclopediaImagePath>Pack/test-image</EncyclopediaImagePath>")]
        [TestCase(
            "<EncyclopediaStats><EncyclopediaEntryStat><Label>Skill</Label><Value>4</Value></EncyclopediaEntryStat></EncyclopediaStats>"
        )]
        public void EncyclopediaFields_AnyEmbeddedData_TakesPriorityOverTheStandaloneArticle(
            string embedded
        )
        {
            ContentPackEditorCatalog units = Catalog(
                "officers",
                "<Officers><Officer><TypeID>OFF1</TypeID>" + embedded + "</Officer></Officers>"
            );
            ContentPackEditorCatalog articles = Catalog(
                "articles",
                "<EncyclopediaEntries><EncyclopediaEntry><TypeID>OFF1</TypeID><Description>Unused fallback</Description></EncyclopediaEntry></EncyclopediaEntries>"
            );
            var fields = ContentEditorContentSections.EncyclopediaFields(
                units.Records[0],
                new[] { units, articles }
            );
            Assert.IsTrue(
                fields.All(field =>
                    field.Group.StartsWith("Encyclopedia", StringComparison.Ordinal)
                )
            );
            fields
                .Single(field => field.Group == "EncyclopediaDescription")
                .SetValue("New embedded article");
            Assert.IsFalse(articles.Document.IsDirty);
            Assert.AreEqual(
                "New embedded article",
                units.Records[0].GetField("EncyclopediaDescription").Value
            );
        }

        [Test]
        public void FactionArtworkFields_ThemeWithoutDefaults_OffersDetachedFieldsUntilEdited()
        {
            ContentPackEditorCatalog themes = Catalog(
                "themes",
                "<FactionThemes><FactionTheme><FactionInstanceID>FNTEST</FactionInstanceID></FactionTheme></FactionThemes>"
            );
            ContentPackEditorRecord theme = themes.Records.Single();

            ContentPackEditorField[] fields = ContentEditorContentSections
                .FactionArtworkFields(theme)
                .ToArray();

            Assert.AreEqual(3, fields.Count(field => field.Group == "ArtworkDefaults"));
            Assert.IsFalse(themes.Document.IsDirty);
            fields
                .Single(field => field.MemberName == "OfficerBackgroundImagePath")
                .SetValue("Pack/Art/officers.png");
            Assert.AreEqual(
                "Pack/Art/officers.png",
                theme.Element.Element("ArtworkDefaults").Element("OfficerBackgroundImagePath").Value
            );
            Assert.IsNotNull(
                fields.Single(field => field.MemberName == "CapitalShipBackgroundImagePath")
            );
            Assert.IsNotNull(
                fields.Single(field => field.MemberName == "StarfighterBackgroundImagePath")
            );
            Assert.IsTrue(themes.Document.IsDirty);
        }

        [Test]
        public void FactionArtworkFields_ConfiguredMedallion_LeavesModelToMainMenuPage()
        {
            ContentPackEditorCatalog themes = Catalog(
                "themes",
                "<FactionThemes><FactionTheme><FactionInstanceID>FNTEST</FactionInstanceID>"
                    + "<MainMenuMedallionModelPath>Pack/Models/custom-medallion</MainMenuMedallionModelPath>"
                    + "</FactionTheme></FactionThemes>"
            );
            ContentPackEditorRecord theme = themes.Records.Single();

            ContentPackEditorField medallion = theme
                .GetFields()
                .Single(field =>
                    field.MemberName == nameof(FactionTheme.MainMenuMedallionModelPath)
                );

            Assert.AreEqual(ContentPackEditorMediaType.Model, medallion.MediaType);
            Assert.IsFalse(ContentEditorContentSections.IsFactionInterfaceField(medallion));
            Assert.IsFalse(
                ContentEditorContentSections
                    .FactionArtworkFields(theme)
                    .Any(field => field.MemberName == medallion.MemberName)
            );
            Assert.IsFalse(themes.Document.IsDirty);
        }

        [TestCase("facility-research", true)]
        [TestCase("diplomacy", false)]
        public void IsFactionInterfaceField_NeutralMissionArtwork_FollowsTheSharedKeys(
            string asset,
            bool shared
        )
        {
            ContentPackEditorCatalog themes = Catalog(
                "neutral",
                "<FactionThemes><FactionTheme><FactionInstanceID>DEFAULT</FactionInstanceID>"
                    + "<MissionIcons><Icons>"
                    + "<MissionIconTheme><Key>ResearchFacilityDesign</Key>"
                    + "<LargeImagePath>Pack/Shared/facility-research</LargeImagePath></MissionIconTheme>"
                    + "<MissionIconTheme><Key>Diplomacy</Key>"
                    + "<LargeImagePath>Pack/Shared/diplomacy</LargeImagePath></MissionIconTheme>"
                    + "</Icons></MissionIcons></FactionTheme></FactionThemes>"
            );
            ContentPackEditorField field = themes
                .Records.Single()
                .GetFields()
                .Single(item => item.Value == "Pack/Shared/" + asset);

            Assert.AreEqual(
                shared,
                ContentEditorContentSections.IsFactionInterfaceField(field, neutral: true)
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IsFactionInterfaceField_ConfirmationDialogArtwork_LeavesArtworkInAdvanced(
            bool neutral
        )
        {
            ContentPackEditorCatalog themes = Catalog(
                "themes",
                "<FactionThemes><FactionTheme><FactionInstanceID>FNTEST</FactionInstanceID>"
                    + "<ConfirmDialogTheme><BackgroundImagePath>Pack/Factions/Alliance/confirm</BackgroundImagePath>"
                    + "</ConfirmDialogTheme></FactionTheme></FactionThemes>"
            );
            ContentPackEditorField field = themes
                .Records.Single()
                .GetFields()
                .Single(item => item.MemberName == nameof(ConfirmDialogTheme.BackgroundImagePath));

            Assert.IsFalse(ContentEditorContentSections.IsFactionInterfaceField(field, neutral));
            Assert.AreEqual(
                "Advanced",
                ContentEditorContentSections.FactionSection(field, neutral)
            );
        }

        [Test]
        public void FactionArtworkFields_InterfaceStateImages_IncludesAndCategorizesEveryState()
        {
            ContentPackEditorCatalog themes = Catalog(
                "themes",
                "<FactionThemes><FactionTheme><FactionInstanceID>FNTEST</FactionInstanceID>"
                    + "<SavedGameSlotIconImagePath>Pack/Factions/Alliance/save-icon</SavedGameSlotIconImagePath>"
                    + "<GalaxyBackground><ImagePath>Pack/Factions/Alliance/galaxy</ImagePath>"
                    + "<DestroyedPlanetIconPath>Pack/Factions/Alliance/destroyed-planet</DestroyedPlanetIconPath>"
                    + "<UnexploredPlanetIconPath>Pack/Shared/Strategy/UI/Windows/PlanetSector/star-unknown</UnexploredPlanetIconPath>"
                    + "<PlanetIcons><Mixed>Pack/Factions/Alliance/asteroid-marker</Mixed></PlanetIcons>"
                    + "</GalaxyBackground>"
                    + "<TacticalHUDLayout><ImagePath>Pack/Factions/Alliance/strategy-frame</ImagePath></TacticalHUDLayout>"
                    + "<StrategyBookmarkIcons>"
                    + "<FacilityImagePath>Pack/Factions/Alliance/facility-bookmark</FacilityImagePath>"
                    + "</StrategyBookmarkIcons>"
                    + "<StrategyContextMenuTheme>"
                    + "<CheckMarkImagePath>Pack/Factions/Alliance/context-check</CheckMarkImagePath>"
                    + "</StrategyContextMenuTheme>"
                    + "<PlanetOverlayTheme>"
                    + "<GalaxyHeadquartersImagePath>Pack/Factions/Alliance/galaxy-hq</GalaxyHeadquartersImagePath>"
                    + "<UnitTileIcons>"
                    + "<FleetListIconImagePath>Pack/Factions/Alliance/fleet-list</FleetListIconImagePath>"
                    + "<MissionTileImagePath>Pack/Factions/Alliance/mission-tile</MissionTileImagePath>"
                    + "</UnitTileIcons><PlanetOverlayIcons><Missions>"
                    + "<NormalImagePath>Pack/Factions/Alliance/mission-normal</NormalImagePath>"
                    + "<HoverImagePath>Pack/Factions/Alliance/mission-hover</HoverImagePath>"
                    + "</Missions></PlanetOverlayIcons></PlanetOverlayTheme>"
                    + "<StrategyWindows><Defense><PersonnelTab>"
                    + "<ActiveImagePath>Pack/Factions/Alliance/personnel-active</ActiveImagePath>"
                    + "<InactiveImagePath>Pack/Factions/Alliance/personnel-inactive</InactiveImagePath>"
                    + "</PersonnelTab></Defense></StrategyWindows>"
                    + "<PlanetWindowTheme><BuildingsPane><BuildingsTabs><Production>"
                    + "<NormalImagePath>Pack/Factions/Alliance/buildings-production</NormalImagePath>"
                    + "</Production></BuildingsTabs></BuildingsPane>"
                    + "<FleetsPane><FleetsImagePath>Pack/Factions/Alliance/fleets-pane</FleetsImagePath>"
                    + "</FleetsPane><GarrisonPanel><Officers>"
                    + "<NormalImagePath>Pack/Factions/Alliance/garrison-officers</NormalImagePath>"
                    + "</Officers></GarrisonPanel><MissionsPane><MissionTabs>"
                    + "<PrimaryParticipants>"
                    + "<NormalImagePath>Pack/Factions/Alliance/mission-primary</NormalImagePath>"
                    + "</PrimaryParticipants></MissionTabs></MissionsPane></PlanetWindowTheme>"
                    + "</FactionTheme></FactionThemes>"
            );
            ContentPackEditorField[] fields = ContentEditorContentSections
                .FactionArtworkFields(themes.Records.Single())
                .ToArray();

            ContentPackEditorField missionHover = fields.Single(field =>
                field.Value == "Pack/Factions/Alliance/mission-hover"
            );
            ContentPackEditorField defenseInactive = fields.Single(field =>
                field.Value == "Pack/Factions/Alliance/personnel-inactive"
            );
            ContentPackEditorField buildingsProduction = fields.Single(field =>
                field.Value == "Pack/Factions/Alliance/buildings-production"
            );
            ContentPackEditorField fleetsPane = fields.Single(field =>
                field.Value == "Pack/Factions/Alliance/fleets-pane"
            );
            ContentPackEditorField garrisonOfficers = fields.Single(field =>
                field.Value == "Pack/Factions/Alliance/garrison-officers"
            );
            ContentPackEditorField missionPrimary = fields.Single(field =>
                field.Value == "Pack/Factions/Alliance/mission-primary"
            );
            Assert.AreEqual(
                "Strategy view",
                ContentEditorContentSections.FactionArtworkCategory(
                    fields.Single(field => field.Value == "Pack/Factions/Alliance/galaxy-hq")
                )
            );
            Assert.AreEqual(
                "Strategy view",
                ContentEditorContentSections.FactionArtworkCategory(
                    fields.Single(field => field.Value == "Pack/Factions/Alliance/galaxy")
                )
            );
            Assert.AreEqual(
                "Planet sector",
                ContentEditorContentSections.FactionArtworkCategory(
                    fields.Single(field => field.Value == "Pack/Factions/Alliance/destroyed-planet")
                )
            );
            Assert.IsFalse(
                fields.Any(field => field.Value == "Pack/Factions/Alliance/asteroid-marker"),
                "The removed mixed marker should not remain as an orphaned interface option."
            );
            Assert.IsFalse(
                ContentEditorContentSections.IsFactionInterfaceField(
                    fields.Single(field =>
                        field.Value == "Pack/Shared/Strategy/UI/Windows/PlanetSector/star-unknown"
                    )
                ),
                "Shared artwork must not be presented as though changing one faction owns it."
            );
            Assert.AreEqual(
                "Planet sector",
                ContentEditorContentSections.FactionArtworkCategory(
                    fields.Single(field =>
                        field.Value == "Pack/Factions/Alliance/facility-bookmark"
                    )
                )
            );
            Assert.AreEqual(
                "Strategy view",
                ContentEditorContentSections.FactionArtworkCategory(
                    fields.Single(field => field.Value == "Pack/Factions/Alliance/context-check")
                )
            );
            Assert.AreEqual(
                "Shell artwork",
                ContentEditorContentSections.FactionArtworkCategory(
                    fields.Single(field => field.Value == "Pack/Factions/Alliance/save-icon")
                )
            );
            Assert.AreEqual(
                "Fleet window",
                ContentEditorContentSections.FactionArtworkCategory(
                    fields.Single(field => field.Value == "Pack/Factions/Alliance/fleet-list")
                )
            );
            Assert.AreEqual(
                "Missions",
                ContentEditorContentSections.FactionArtworkCategory(
                    fields.Single(field => field.Value == "Pack/Factions/Alliance/mission-tile")
                )
            );
            Assert.IsFalse(
                ContentEditorContentSections.IsFactionInterfaceField(
                    fields.Single(field => field.Value == "Pack/Factions/Alliance/mission-tile")
                ),
                "Serialized artwork without an interface consumer should not appear in the browser."
            );
            Assert.AreEqual(
                "Planet sector",
                ContentEditorContentSections.FactionArtworkCategory(missionHover)
            );
            Assert.AreEqual(
                "Strategy view",
                ContentEditorContentSections.FactionArtworkCategory(
                    fields.Single(field => field.Value == "Pack/Factions/Alliance/strategy-frame")
                )
            );
            Assert.AreEqual(
                "Defense window",
                ContentEditorContentSections.FactionArtworkCategory(defenseInactive)
            );
            CollectionAssert.AreEqual(
                new[] { "Missions overlay" },
                ContentEditorContentSections.FactionArtworkSections(missionHover),
                "Mission hover and normal images should share one view heading."
            );
            CollectionAssert.AreEqual(
                new[] { "Personnel tab" },
                ContentEditorContentSections.FactionArtworkSections(defenseInactive),
                "Defense tab states should share one view heading."
            );
            Assert.AreEqual(
                "Facilities and construction",
                ContentEditorContentSections.FactionArtworkCategory(buildingsProduction)
            );
            Assert.AreEqual(
                "Fleet window",
                ContentEditorContentSections.FactionArtworkCategory(fleetsPane)
            );
            Assert.AreEqual(
                "Defense window",
                ContentEditorContentSections.FactionArtworkCategory(garrisonOfficers)
            );
            Assert.AreEqual(
                "Missions",
                ContentEditorContentSections.FactionArtworkCategory(missionPrimary)
            );
            CollectionAssert.DoesNotContain(
                ContentEditorContentSections.FactionArtworkCategories,
                "Planet window"
            );
            CollectionAssert.DoesNotContain(
                ContentEditorContentSections.FactionArtworkCategories,
                "Galaxy"
            );
            CollectionAssert.Contains(
                ContentEditorContentSections.FactionArtworkCategories,
                "Planet sector"
            );
            CollectionAssert.DoesNotContain(
                ContentEditorContentSections.FactionArtworkCategories,
                "Other artwork"
            );
            CollectionAssert.AreEqual(
                ContentEditorContentSections.FactionArtworkCategories.OrderBy(
                    value => value,
                    StringComparer.Ordinal
                ),
                ContentEditorContentSections.FactionArtworkCategories,
                "Interface surfaces should be presented in a stable alphabetical order."
            );
            CollectionAssert.AreEqual(
                new[] { "Buildings pane: Production" },
                ContentEditorContentSections.FactionArtworkSections(buildingsProduction),
                "Planet-window containers should collapse into one view heading."
            );
            Assert.IsFalse(themes.Document.IsDirty);
        }

        [Test]
        public void FactionArtworkFields_AnimationFrameDirectory_ExcludesDirectoryFromArtwork()
        {
            ContentPackEditorCatalog themes = Catalog(
                "themes",
                "<FactionThemes><FactionTheme><FactionInstanceID>FNTEST</FactionInstanceID>"
                    + "<StrategyAdvisor><AnimationImageRoot>Pack/Frames</AnimationImageRoot>"
                    + "</StrategyAdvisor></FactionTheme></FactionThemes>"
            );
            ContentPackEditorRecord theme = themes.Records.Single();
            ContentPackEditorField animationRoot = theme
                .GetFields()
                .Single(field => field.MemberName == "AnimationImageRoot");

            Assert.IsTrue(animationRoot.IsMediaDirectory);
            Assert.IsFalse(ContentEditorContentSections.IsFactionArtworkField(animationRoot));
            Assert.AreEqual("Advanced", ContentEditorContentSections.FactionSection(animationRoot));
            Assert.IsFalse(
                ContentEditorContentSections
                    .FactionArtworkFields(theme)
                    .Any(field => field.MemberName == "AnimationImageRoot")
            );
            Assert.IsFalse(themes.Document.IsDirty);
        }

        /// <summary>
        /// Opens a fixture document as an editable catalog.
        /// </summary>
        /// <param name="name">The fixture filename.</param>
        /// <param name="xml">The catalog contents.</param>
        /// <returns>The editable fixture catalog.</returns>
        private ContentPackEditorCatalog Catalog(string name, string xml)
        {
            string path = Path.Combine(folder, name + ".xml");
            File.WriteAllText(path, xml);
            return new ContentPackEditorCatalog(
                new ContentPackEditorDocument(
                    "Pack/" + name + ".xml",
                    path,
                    path,
                    File.ReadAllText(path)
                )
            );
        }
    }
}
