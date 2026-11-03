using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorNavigationTests
    {
        private string directory;

        /// <summary>
        /// Creates an isolated catalog folder.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(
                Path.GetTempPath(),
                "content-navigation-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(directory);
        }

        /// <summary>
        /// Removes the fixture-owned catalog folder.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Directory.Delete(directory, true);
        }

        [Test]
        public void Render_RulesAndMissionPages_KeepAllNonAiRulesAccessible()
        {
            ContentPackEditorProject project = ContentEditorTestData.CreateProject(directory);
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(project.GetCatalogs());
            navigation.SetExpanded(true);
            ContentPackEditorField[] original = project
                .GetCatalogs()
                .Where(catalog => catalog.Name == "Rules")
                .SelectMany(catalog => catalog.Records)
                .SelectMany(record => record.GetFields())
                .Where(field => field.Group != "AI")
                .ToArray();
            ContentPackEditorField[] projected = navigation
                .GetRows(string.Empty)
                .Where(node => node.Fields != null)
                .SelectMany(node => node.Fields)
                .Where(field => field.Record.Kind == "GameConfig")
                .ToArray();
            CollectionAssert.AreEqual(
                original.Select(FieldIdentity).OrderBy(value => value, StringComparer.Ordinal),
                projected
                    .Select(FieldIdentity)
                    .Distinct()
                    .OrderBy(value => value, StringComparer.Ordinal)
            );
        }

        [Test]
        public void Reveal_Record_ExpandsOnlyItsUnitsCategoryAndFactionAncestors()
        {
            ContentPackEditorCatalog officers = Catalog(
                "Officers",
                "Officer",
                "Pack/Factions/SideA/Data/officers.xml",
                "OF1",
                "Named officer"
            );
            ContentPackEditorCatalog ships = Catalog(
                "CapitalShips",
                "CapitalShip",
                "Pack/Factions/SideA/Data/ships.xml",
                "SHIP1",
                "Named ship"
            );
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(new[] { officers, ships });

            navigation.Reveal(officers.Records[0]);

            ContentEditorTreeNode[] visible = navigation.GetRows(string.Empty).ToArray();
            CollectionAssert.AreEqual(
                new[]
                {
                    "Units",
                    "Officers",
                    "SideA",
                    "Named officer",
                    "+ Add officer",
                    "+ Add officer",
                    "Capital ships",
                },
                visible.Select(node => node.Caption)
            );
            Assert.AreEqual(3, visible.Single(node => node.Record == officers.Records[0]).Depth);
        }

        [Test]
        public void Render_PackWithoutFactions_KeepsStartingSetupSecondToLast()
        {
            string pack = Path.Combine(directory, "pack.xml");
            string setup = Path.Combine(directory, "generation.xml");
            File.WriteAllText(pack, "<ContentPack><ID>test-pack</ID></ContentPack>");
            File.WriteAllText(setup, "<GameGenerationConfig />");
            ContentEditorNavigation navigation = new();
            navigation.Render(
                new[]
                {
                    new ContentPackEditorCatalog(
                        new ContentPackEditorDocument(
                            "Pack/pack.xml",
                            pack,
                            pack,
                            File.ReadAllText(pack)
                        )
                    ),
                    new ContentPackEditorCatalog(
                        new ContentPackEditorDocument(
                            "Pack/generation.xml",
                            setup,
                            setup,
                            File.ReadAllText(setup)
                        )
                    ),
                }
            );

            CollectionAssert.AreEqual(
                new[] { "Factions", "Starting setup", "Pack settings" },
                navigation.GetRows(string.Empty).Select(node => node.Caption)
            );
        }

        [Test]
        public void GetCreationCatalogs_SidebarOrder_DeduplicatesFactionActionsAndIgnoresExpansion()
        {
            string manifest = Path.Combine(directory, "pack.xml");
            File.WriteAllText(manifest, "<ContentPack><ID>test-pack</ID></ContentPack>");
            ContentPackEditorCatalog pack = new(
                new ContentPackEditorDocument(
                    "Pack/pack.xml",
                    manifest,
                    manifest,
                    File.ReadAllText(manifest)
                )
            );
            ContentPackEditorCatalog[] catalogs =
            {
                Catalog("Buildings", "Building", "Pack/Data/buildings.xml", "BUILDING", "Building"),
                Catalog("GameEvents", "GameEvent", "Pack/Data/events.xml", "EVENT", "Event"),
                Catalog(
                    "CapitalShips",
                    "CapitalShip",
                    "Pack/Factions/A/Data/ships.xml",
                    "SHIP",
                    "Ship"
                ),
                Catalog(
                    "Officers",
                    "Officer",
                    "Pack/Factions/A/Data/officers.xml",
                    "A",
                    "Officer A"
                ),
                Catalog(
                    "Officers",
                    "Officer",
                    "Pack/Factions/B/Data/officers.xml",
                    "B",
                    "Officer B"
                ),
                Catalog("OfficerTraits", "Trait", "Pack/Data/traits.xml", "TRAIT", "Trait"),
                Catalog(
                    "PlanetSectors",
                    "PlanetSector",
                    "Pack/Data/galaxy.xml",
                    "SECTOR",
                    "Sector"
                ),
                pack,
            };
            ContentEditorNavigation navigation = new();
            navigation.Render(catalogs);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Factions",
                    "Galaxy",
                    "Units",
                    "Officer traits",
                    "Buildings",
                    "Game events",
                    "Pack settings",
                },
                navigation.GetRows(string.Empty).Select(node => node.Caption)
            );
            string[] expected =
            {
                "Pack settings",
                "Galaxy map",
                "Officers",
                "Capital ships",
                "Officer traits",
                "Buildings",
                "Game events",
            };
            CollectionAssert.AreEqual(
                expected,
                navigation.GetCreationCatalogs().Select(catalog => catalog.Name)
            );
            navigation.SetExpanded(true);
            CollectionAssert.AreEqual(
                expected,
                navigation.GetCreationCatalogs().Select(catalog => catalog.Name)
            );
        }

        [Test]
        public void GetRows_SearchingIdentifier_RevealsMatchingRecordsWithoutChangingExpansionState()
        {
            ContentPackEditorCatalog officers = Catalog(
                "Officers",
                "Officer",
                "Pack/Factions/SideA/Data/officers.xml",
                "OF1",
                "Named officer"
            );
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(new[] { officers });

            Assert.AreEqual("Named officer", navigation.GetRows("of1").Last().Caption);
            CollectionAssert.AreEqual(
                new[] { "Units" },
                navigation.GetRows(string.Empty).Select(node => node.Caption)
            );
        }

        [Test]
        public void Render_SameTypeIdentifiersInDifferentFactions_ProducesDistinctTreeKeys()
        {
            ContentPackEditorCatalog first = Catalog(
                "Officers",
                "Officer",
                "Pack/Factions/SideA/Data/officers.xml",
                "OF1",
                "First officer"
            );
            ContentPackEditorCatalog second = Catalog(
                "Officers",
                "Officer",
                "Pack/Factions/SideB/Data/officers.xml",
                "OF1",
                "Second officer"
            );
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(new[] { first, second });

            ContentEditorTreeNode[] records = navigation
                .GetRows("OF1")
                .Where(node => node.Record != null)
                .ToArray();

            Assert.AreEqual(2, records.Select(node => node.Key).Distinct().Count());
        }

        [Test]
        public void SetExpanded_AllBranches_ExpandsAndCollapsesWithoutChangingRecords()
        {
            ContentPackEditorCatalog officers = Catalog(
                "Officers",
                "Officer",
                "Pack/Factions/SideA/Data/officers.xml",
                "OFF1",
                "Officer"
            );
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(new[] { officers });
            navigation.SetExpanded(true);
            Assert.AreEqual(6, navigation.GetRows(string.Empty).Count);
            navigation.Render(new[] { officers });
            Assert.AreEqual(6, navigation.GetRows(string.Empty).Count);
            navigation.SetExpanded(false);
            Assert.AreEqual(1, navigation.GetRows(string.Empty).Count);
            Assert.IsFalse(officers.Document.IsDirty);
        }

        [Test]
        public void Render_FactionManifestAndGameplayData_UsesOneCombinedFactionPage()
        {
            ContentPackEditorCatalog settings = Catalog(
                "Factions",
                "Faction",
                "Pack/Factions/SideA/Data/faction.xml",
                "FA",
                "Side A"
            );
            string manifest = Path.Combine(directory, "faction.xml");
            File.WriteAllText(
                manifest,
                "<Faction><ID>side-a</ID><FactionDataPath>Factions/SideA/Data/faction.xml</FactionDataPath></Faction>"
            );
            ContentPackEditorCatalog files = new ContentPackEditorCatalog(
                new ContentPackEditorDocument(
                    "Pack/Factions/SideA/faction.xml",
                    manifest,
                    manifest,
                    File.ReadAllText(manifest)
                )
            );
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(new[] { settings, files });
            navigation.SetExpanded(true);
            CollectionAssert.AreEqual(
                new[] { "Factions", "Side A" },
                navigation.GetRows(string.Empty).Select(node => node.Caption)
            );
            Assert.AreEqual(2, navigation.GetRows(string.Empty).Last().RelatedRecords.Count);
        }

        [Test]
        public void Render_FactionReferenceArticles_BelongToTheFactionPageAndRemainSearchable()
        {
            ContentPackEditorCatalog faction = Catalog(
                "Factions",
                "Faction",
                "Pack/Factions/SideA/Data/faction.xml",
                "FA",
                "Side A"
            );
            ContentPackEditorCatalog articles = Catalog(
                "EncyclopediaEntries",
                "EncyclopediaEntry",
                "Pack/Factions/SideA/Data/encyclopedia-entries.xml",
                "FLEET",
                "Fleet"
            );
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(new[] { faction, articles });
            navigation.SetExpanded(true);
            CollectionAssert.AreEqual(
                new[] { "Factions", "Side A" },
                navigation.GetRows(string.Empty).Select(node => node.Caption)
            );
            ContentEditorTreeNode page = navigation.GetRows("Fleet").Last();
            Assert.AreEqual("factions/SideA", page.Key);
            CollectionAssert.Contains(page.RelatedRecords, articles.Records[0]);
            Assert.IsEmpty(page.Children);
            navigation.SetExpanded(false);
            navigation.Reveal(articles.Records[0]);
            Assert.AreEqual("factions/SideA", navigation.GetRows(string.Empty).Last().Key);
        }

        [Test]
        public void Render_SharedReferenceArticle_RemainsAccessibleWithoutAFactionOwner()
        {
            ContentPackEditorCatalog articles = Catalog(
                "EncyclopediaEntries",
                "EncyclopediaEntry",
                "Pack/Shared/Data/encyclopedia-entries.xml",
                "CONCEPT",
                "Shared concept"
            );
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(new[] { articles });
            Assert.AreEqual(
                articles.Records[0],
                navigation.GetRows("Shared concept").Last().Record
            );
        }

        [Test]
        public void Render_RecordsWithoutIds_AssignsDistinctKeysThatSurviveReopening()
        {
            string file = Path.Combine(directory, "messages.xml");
            File.WriteAllText(
                file,
                "<MessageDefinitions><MessageDefinition><DisplayName>First</DisplayName><ResultType>MissionReport</ResultType></MessageDefinition><MessageDefinition><DisplayName>Second</DisplayName><ResultType>MissionReport</ResultType></MessageDefinition></MessageDefinitions>"
            );
            ContentPackEditorDocument document = new ContentPackEditorDocument(
                "Pack/messages.xml",
                file,
                file,
                File.ReadAllText(file)
            );
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(new[] { new ContentPackEditorCatalog(document) });
            navigation.SetExpanded(true);
            string[] keys = navigation
                .GetRows(string.Empty)
                .Where(row => row.Record != null)
                .Select(row => row.Key)
                .ToArray();
            Assert.AreEqual(2, keys.Distinct().Count());
            navigation.Render(new[] { new ContentPackEditorCatalog(document) });
            CollectionAssert.AreEqual(
                keys,
                navigation
                    .GetRows(string.Empty)
                    .Where(row => row.Record != null)
                    .Select(row => row.Key)
            );
        }

        [Test]
        public void GetRows_EmptyCatalog_ExposesItsDocumentForAdvancedEditing()
        {
            ContentPackEditorCatalog catalog = Catalog(
                "Officers",
                "Officer",
                "Pack/Factions/SideA/Data/officers.xml",
                "OFF1",
                "Officer"
            );
            catalog.Remove(catalog.Records[0]);
            ContentEditorNavigation navigation = new ContentEditorNavigation();
            navigation.Render(new[] { catalog });

            ContentEditorTreeNode empty = navigation
                .GetRows("Officers")
                .Single(node => node.Record != null);

            Assert.AreSame(catalog.Document, empty.Record.Catalog.Document);
            Assert.AreEqual("Officers", empty.Record.Kind);
            Assert.AreEqual("No entries", empty.Caption);
        }

        [TestCase("Officers", "Officer", "RecruitingFactionInstanceIDs")]
        [TestCase("CapitalShips", "CapitalShip", "ManufacturingFactionInstanceIDs")]
        public void Render_MultiFactionUnit_ShowsOneSharedRecordAndRetainsItsOwningCatalog(
            string root,
            string kind,
            string eligibility
        )
        {
            ContentPackEditorCatalog catalog = Catalog(
                root,
                kind,
                "Pack/Factions/SideA/Data/units.xml",
                "UNIT1",
                "Shared unit"
            );
            ContentPackEditorRecord record = catalog.Records.Single();
            record.Element.Add(
                new XElement(eligibility, new XElement("string", "A"), new XElement("string", "B"))
            );
            ContentEditorNavigation navigation = new();
            navigation.Render(new[] { catalog });
            navigation.SetExpanded(true);

            ContentEditorTreeNode[] rows = navigation.GetRows(string.Empty).ToArray();
            ContentEditorTreeNode shared = rows.Single(row => row.Caption == "Shared");
            Assert.AreSame(record, shared.Children.Single(child => child.Record != null).Record);
            Assert.AreEqual(1, rows.Count(row => row.Record == record));
            Assert.AreSame(catalog, record.Catalog);
            Assert.AreSame(catalog, shared.Children.Last().CreationCatalog);
        }

        [TestCase("Officers", "Officer", "+ Add officer")]
        [TestCase("SpecialForces", "SpecialForce", "+ Add special forces")]
        [TestCase("Regiments", "Regiment", "+ Add regiment")]
        [TestCase("CapitalShips", "CapitalShip", "+ Add capital ship")]
        [TestCase("Starfighters", "Starfighter", "+ Add starfighter")]
        [TestCase("Buildings", "Building", "+ Add building")]
        public void Render_MultipleFactionCatalogs_OffersAnAddRowAtTheEndOfEachFactionAndCategory(
            string collection,
            string kind,
            string caption
        )
        {
            ContentPackEditorCatalog first = Catalog(
                collection,
                kind,
                "Pack/Factions/SideA/Data/units.xml",
                "UNIT1",
                "First unit"
            );
            ContentPackEditorCatalog second = Catalog(
                collection,
                kind,
                "Pack/Factions/SideB/Data/units.xml",
                "UNIT2",
                "Second unit"
            );
            ContentEditorNavigation navigation = new();
            navigation.Render(new[] { first, second });
            navigation.SetExpanded(true);

            ContentEditorTreeNode[] rows = navigation.GetRows(string.Empty).ToArray();
            ContentEditorTreeNode add = rows.Single(row =>
                row.CreationCatalog != null && row.Key.EndsWith("#add", StringComparison.Ordinal)
            );
            ContentEditorTreeNode category = rows.Single(row => row.Children.Contains(add));
            Assert.AreEqual(first.Name, category.Caption);
            Assert.AreEqual(caption, add.Caption);
            Assert.AreEqual(category.Depth + 1, add.Depth);
            Assert.AreEqual(add, category.Children.Last());
            ContentEditorTreeNode[] factions = category
                .Children.Where(node => node.CreationCatalog == null)
                .ToArray();
            Assert.AreEqual(2, factions.Length);
            foreach (ContentEditorTreeNode faction in factions)
            {
                ContentEditorTreeNode create = faction.Children.Last();
                Assert.AreEqual(caption, create.Caption);
                Assert.AreEqual(faction.Depth + 1, create.Depth);
                Assert.IsNotNull(create.CreationCatalog);
                Assert.AreEqual(1, faction.Children.Count(node => node.CreationCatalog != null));
            }
        }

        [Test]
        public void Render_FactionsWithTheSameName_KeepsCreationActionsScopedByIdentity()
        {
            ContentPackEditorCatalog first = Catalog(
                "Factions",
                "Faction",
                "Pack/Factions/SideA/Data/faction.xml",
                "A",
                "Same name"
            );
            first.Records[0].Element.Add(new XElement("InstanceID", "A"));
            ContentPackEditorCatalog second = Catalog(
                "Factions",
                "Faction",
                "Pack/Factions/SideB/Data/faction.xml",
                "Shared",
                "Same name"
            );
            second.Records[0].Element.Add(new XElement("InstanceID", "Shared"));
            ContentPackEditorCatalog firstUnits = Catalog(
                "Officers",
                "Officer",
                "Pack/Factions/SideA/Data/units.xml",
                "FIRST",
                "First"
            );
            firstUnits
                .Records[0]
                .Element.Add(
                    new XElement("RecruitingFactionInstanceIDs", new XElement("String", "A"))
                );
            ContentPackEditorCatalog secondUnits = Catalog(
                "Officers",
                "Officer",
                "Pack/Factions/SideB/Data/units.xml",
                "SECOND",
                "Second"
            );
            secondUnits
                .Records[0]
                .Element.Add(
                    new XElement("RecruitingFactionInstanceIDs", new XElement("String", "Shared"))
                );
            ContentPackEditorRecord shared = firstUnits.Duplicate(firstUnits.Records[0]);
            shared
                .Element.Element("RecruitingFactionInstanceIDs")
                .Add(new XElement("String", "Shared"));
            ContentEditorNavigation navigation = new();
            navigation.Render(new[] { first, second, secondUnits, firstUnits });
            navigation.SetExpanded(true);

            ContentEditorTreeNode[] rows = navigation.GetRows(string.Empty).ToArray();
            ContentEditorTreeNode[] actions = rows.Where(row =>
                    row.CreationCatalog?.Name == "Officers"
                )
                .ToArray();
            Assert.AreEqual(
                actions.Length,
                actions.Select(action => action.Key).Distinct().Count()
            );
            Assert.AreSame(
                firstUnits,
                actions.Single(action => action.CreationFactionInstanceID == "A").CreationCatalog
            );
            Assert.AreSame(
                secondUnits,
                actions
                    .Single(action => action.CreationFactionInstanceID == "Shared")
                    .CreationCatalog
            );
            Assert.AreEqual(2, actions.Count(action => action.CreationFactionInstanceID == null));
            Assert.AreSame(
                secondUnits,
                navigation.GetCreationCatalogs().Single(catalog => catalog.Name == "Officers")
            );
            Assert.AreEqual(
                2,
                rows.Count(row =>
                    row.Caption == "Same name"
                    && row.Children.Any(child => child.CreationCatalog?.Name == "Officers")
                )
            );
        }

        [TestCase("Officers", "Officer", "RecruitingFactionInstanceIDs")]
        [TestCase("CapitalShips", "CapitalShip", "ManufacturingFactionInstanceIDs")]
        public void Render_ChangingFactionSelection_GroupsTheSameDefinitionByItsSelections(
            string collection,
            string kind,
            string eligibility
        )
        {
            ContentPackEditorCatalog units = Catalog(
                collection,
                kind,
                "Pack/Factions/SideA/Data/units.xml",
                "UNIT1",
                "New unit"
            );
            ContentPackEditorCatalog first = Catalog(
                "Factions",
                "Faction",
                "Pack/Factions/SideA/Data/faction.xml",
                "A",
                "Side A"
            );
            first.Records[0].Element.Add(new XElement("InstanceID", "A"));
            ContentPackEditorCatalog second = Catalog(
                "Factions",
                "Faction",
                "Pack/Factions/SideB/Data/faction.xml",
                "B",
                "Side B"
            );
            second.Records[0].Element.Add(new XElement("InstanceID", "B"));
            ContentPackEditorRecord record = units.Records[0];
            XElement selected = new(eligibility);
            record.Element.Add(selected);
            ContentEditorNavigation navigation = new();
            ContentPackEditorCatalog[] catalogs = { units, first, second };
            navigation.Render(catalogs);
            navigation.SetExpanded(true);

            Assert.AreSame(
                record,
                navigation
                    .GetRows(string.Empty)
                    .Single(row => row.Caption == "Unassigned")
                    .Children.Single(child => child.Record != null)
                    .Record
            );
            selected.Add(new XElement("String", "B"));
            navigation.Render(catalogs);
            navigation.SetExpanded(true);
            Assert.AreSame(
                record,
                navigation
                    .GetRows(string.Empty)
                    .Single(row =>
                        row.Caption == "Side B" && row.Children.Any(child => child.Record == record)
                    )
                    .Children.Single(child => child.Record != null)
                    .Record
            );
            Assert.IsFalse(
                navigation.GetRows(string.Empty).Any(row => row.Caption == "Unassigned")
            );

            selected.Add(new XElement("String", "A"));
            navigation.Render(catalogs);
            navigation.SetExpanded(true);
            Assert.AreSame(
                record,
                navigation
                    .GetRows(string.Empty)
                    .Single(row => row.Caption == "Shared")
                    .Children.Single(child => child.Record != null)
                    .Record
            );
            Assert.AreSame(units, record.Catalog);
            Assert.AreEqual(1, navigation.GetRows(string.Empty).Count(row => row.Record == record));
        }

        [Test]
        public void Render_OfficerWithOwnerAndNoRecruitingFactions_UsesItsOwnerLabel()
        {
            ContentPackEditorCatalog units = Catalog(
                "Officers",
                "Officer",
                "Pack/Factions/SideA/Data/officers.xml",
                "UNIT1",
                "Main officer"
            );
            ContentPackEditorCatalog faction = Catalog(
                "Factions",
                "Faction",
                "Pack/Factions/SideB/Data/faction.xml",
                "B",
                "Side B"
            );
            faction.Records[0].Element.Add(new XElement("InstanceID", "B"));
            units
                .Records[0]
                .Element.Add(
                    new XElement("OwnerInstanceID", "B"),
                    new XElement("RecruitingFactionInstanceIDs")
                );
            ContentEditorNavigation navigation = new();
            navigation.Render(new[] { units, faction });
            navigation.SetExpanded(true);

            Assert.AreEqual("Side B", navigation.GetUnitGroup(units.Records[0]));
            Assert.IsFalse(
                navigation.GetRows(string.Empty).Any(row => row.Caption == "Unassigned")
            );
        }

        /// <summary>
        /// Identifies an authored field independently of its navigation page.
        /// </summary>
        /// <param name="field">The projected member.</param>
        /// <returns>Its group, entry path and member identity.</returns>
        private static string FieldIdentity(ContentPackEditorField field) =>
            field.Group + "|" + string.Join("/", field.SectionPath) + "|" + field.Name;

        /// <summary>
        /// Creates a named-record catalog at a chosen scoped address.
        /// </summary>
        /// <param name="root">The collection root.</param>
        /// <param name="item">The record element name.</param>
        /// <param name="address">The scoped address.</param>
        /// <param name="id">The record identity.</param>
        /// <param name="name">The record name.</param>
        /// <returns>The editable catalog.</returns>
        private ContentPackEditorCatalog Catalog(
            string root,
            string item,
            string address,
            string id,
            string name
        )
        {
            string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(
                path,
                "<"
                    + root
                    + "><"
                    + item
                    + "><TypeID>"
                    + id
                    + "</TypeID><DisplayName>"
                    + name
                    + "</DisplayName></"
                    + item
                    + "></"
                    + root
                    + ">"
            );
            return new ContentPackEditorCatalog(
                new ContentPackEditorDocument(address, path, path, File.ReadAllText(path))
            );
        }
    }
}
