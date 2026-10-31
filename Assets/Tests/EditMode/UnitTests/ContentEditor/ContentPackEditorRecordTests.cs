using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentPackEditorRecordTests
    {
        [Test]
        public void GetFields_AbsentOptionalMembers_ExposesTypedFieldsWithoutChangingTheDocument()
        {
            var record = CreateGenerationRecord(
                "<Officers><StartingOfficers><StartingOfficerRule><OfficerInstanceID>OFFICER</OfficerInstanceID><DestinationTypeID>PLANET</DestinationTypeID></StartingOfficerRule></StartingOfficers></Officers>"
            );
            string before = record.Catalog.Document.Text;
            XElement owner = record.Element.Descendants("StartingOfficerRule").Single();
            var fields = record.GetFields(owner);

            CollectionAssert.AreEqual(
                new[]
                {
                    "OfficerInstanceID",
                    "GalaxySizes",
                    "DestinationTypeID",
                    "DestinationInstanceID",
                },
                fields.Select(field => field.MemberName)
            );
            Assert.IsTrue(fields.Single(field => field.MemberName == "GalaxySizes").IsList);
            CollectionAssert.AreEquivalent(
                new[] { "Small", "Medium", "Large" },
                fields.Single(field => field.MemberName == "GalaxySizes").Choices
            );
            Assert.AreEqual(before, record.Catalog.Document.Text);
            Assert.IsFalse(record.Catalog.Document.IsDirty);
        }

        [Test]
        public void GetFields_OptionalDestinationEdit_ReplacesTheOtherDestinationInDeclarationOrder()
        {
            var record = CreateGenerationRecord(
                "<Officers><StartingOfficers><StartingOfficerRule><OfficerInstanceID>OFFICER</OfficerInstanceID><DestinationTypeID>PLANET</DestinationTypeID></StartingOfficerRule></StartingOfficers></Officers>"
            );
            XElement owner = record.Element.Descendants("StartingOfficerRule").Single();
            var fields = record.GetFields(owner);
            fields
                .Single(field => field.MemberName == "DestinationInstanceID")
                .SetValue("PLANET_INSTANCE");
            fields
                .Single(field => field.MemberName == "GalaxySizes")
                .SetListValues(new[] { "Small" });

            CollectionAssert.AreEqual(
                new[] { "OfficerInstanceID", "GalaxySizes", "DestinationInstanceID" },
                owner.Elements().Select(element => element.Name.LocalName)
            );
            Assert.AreEqual("PLANET_INSTANCE", owner.Element("DestinationInstanceID").Value);
            Assert.AreEqual(
                "GameSize",
                owner.Element("GalaxySizes").Elements().Single().Name.LocalName
            );
        }

        [Test]
        public void GetFields_ClearingOptionalFactionVisibility_OmitsTheMember()
        {
            var record = CreateGenerationRecord(
                "<GalaxyClassification><FactionSetups><FactionSetup><StartingPlanets><StartingPlanet><PlanetTypeID>PLANET</PlanetTypeID><VisibleToFactionIDs><FactionID>FACTION</FactionID></VisibleToFactionIDs></StartingPlanet></StartingPlanets></FactionSetup></FactionSetups></GalaxyClassification>"
            );
            XElement owner = record.Element.Descendants("StartingPlanet").Single();
            record
                .GetFields(owner)
                .Single(field => field.MemberName == "VisibleToFactionIDs")
                .SetListValues(Array.Empty<string>());

            Assert.IsNull(owner.Element("VisibleToFactionIDs"));
            Assert.AreEqual("PLANET", owner.Element("PlanetTypeID").Value);
        }

        [Test]
        public void GetFields_ForeignObject_RejectsWithoutChangingTheDocument()
        {
            var record = CreateGenerationRecord("<Officers/>");
            Assert.Throws<ArgumentException>(() =>
                record.GetFields(new XElement("StartingOfficerRule"))
            );
            Assert.IsFalse(record.Catalog.Document.IsDirty);
        }

        [Test]
        public void GetTableField_NewThreshold_InsertsInScoreOrderAndRemovesOnlyTheClearedEntry()
        {
            var record = CreateRules();
            XElement table = record
                .Element.Element("ProbabilityTables")
                .Element("Mission")
                .Element("Foil");
            string before = record.Catalog.Document.Text;
            var field = record.GetTableField(table, 5, "Foil");
            Assert.IsTrue(field.IsNumber);
            Assert.AreEqual(string.Empty, field.Value);
            Assert.AreEqual(before, record.Catalog.Document.Text);

            field.SetValue("42");
            CollectionAssert.AreEqual(
                new[] { 0, 5, 10 },
                table.Elements().Select(entry => (int)entry.Element("Key"))
            );
            field.SetValue(string.Empty);
            CollectionAssert.AreEqual(
                new[] { 0, 10 },
                table.Elements().Select(entry => (int)entry.Element("Key"))
            );
        }

        [Test]
        public void GetTableField_ForeignTable_RejectsWithoutChangingTheDocument()
        {
            var record = CreateRules();
            Assert.Throws<ArgumentException>(() =>
                record.GetTableField(new XElement("Foil"), 5, "Foil")
            );
            Assert.IsFalse(record.Catalog.Document.IsDirty);
        }

        [Test]
        public void GetFields_SpecialForcesDefinition_UsesTypedFactionAndNumericFields()
        {
            ContentPackEditorRecord unit = Create(
                "<SpecialForces><SpecialForce><TypeID>TEST</TypeID><ManufacturingFactionInstanceIDs/><ConstructionCost>0</ConstructionCost></SpecialForce></SpecialForces>"
            )
                .Records.Single();
            ContentPackEditorField factions = unit.GetField("ManufacturingFactionInstanceIDs");
            Assert.IsTrue(factions.IsList);
            Assert.IsTrue(factions.IsFactionReference);
            Assert.IsEmpty(factions.Values);
            factions.SetListValues(new[] { "FACTION" });
            Assert.AreEqual(
                "FACTION",
                unit.Element.Element("ManufacturingFactionInstanceIDs").Elements().Single().Value
            );

            ContentPackEditorField cost = unit.GetField("ConstructionCost");
            Assert.IsTrue(cost.IsNumber);
            Assert.Throws<ArgumentException>(() => cost.SetValue("not a number"));
        }

        [Test]
        public void GetFields_FactionControls_UsesReadableLabelsAndKnownMissionChoices()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Factions><Faction><InstanceID>TEST</InstanceID><HQInstanceID>HOME</HQInstanceID><DisallowedMissionTypeIDs><string>Diplomacy</string></DisallowedMissionTypeIDs></Faction></Factions>"
            );
            ContentPackEditorField headquarters = catalog.Records[0].GetField("HQInstanceID");
            Assert.AreEqual("HQ Instance ID", headquarters.Caption);
            StringAssert.Contains("headquarters", headquarters.HelpText);
            ContentPackEditorField missions = catalog
                .Records[0]
                .GetField("DisallowedMissionTypeIDs");
            Assert.AreEqual("Disabled missions", missions.Caption);
            Assert.AreEqual("Disallowed mission type IDs", missions.Name);
            Assert.IsTrue(missions.IsList);
            CollectionAssert.Contains(missions.Choices, "Diplomacy");
            CollectionAssert.Contains(missions.Choices, "Research");
            Assert.IsNotEmpty(missions.HelpText);
        }

        [Test]
        public void GetFields_FactionMessages_UsesTitlesAndMultilineBodiesWithoutSerializerHeadings()
        {
            ContentPackEditorRecord record = Create(
                "<Factions><Faction><Messages><Entry><Key>Advice</Key><Value><ArrayOfMessage>"
                    + "<Message><Title>Build defenses</Title><Body>Protect your worlds.</Body></Message>"
                    + "</ArrayOfMessage></Value></Entry></Messages></Faction></Factions>"
            )
                .Records.Single();
            ContentPackEditorField body = record
                .GetFields()
                .Single(field => field.MemberName == "Body");
            CollectionAssert.AreEqual(
                new[] { "Messages", "Advice", "Build defenses" },
                body.SectionPath
            );
            Assert.IsTrue(body.IsMultiline);
            body.SetValue("Changed advice.");
            Assert.AreEqual("Changed advice.", record.Element.Descendants("Body").Single().Value);
        }

        [Test]
        public void GetFields_ShipNames_IdentifiesPoolsByIdAndExplainsTheirPurpose()
        {
            ContentPackEditorRecord record = Create(
                "<Factions><Faction><ShipNamePools><NamePool><NamePoolID>cruisers</NamePoolID>"
                    + "<FallbackNamePoolID>common</FallbackNamePoolID><Names><Name>Example</Name></Names>"
                    + "</NamePool></ShipNamePools></Faction></Factions>"
            )
                .Records.Single();
            ContentPackEditorField names = record
                .GetFields()
                .Single(field => field.MemberName == "Names");
            CollectionAssert.AreEqual(
                new[] { "Ship name pools", "Pool cruisers" },
                names.SectionPath
            );
            Assert.AreEqual("Ship names", names.Caption);
            Assert.IsNotEmpty(
                record.GetFields().Single(field => field.MemberName == "NamePoolID").HelpText
            );
            Assert.IsNotEmpty(
                record
                    .GetFields()
                    .Single(field => field.MemberName == "FallbackNamePoolID")
                    .HelpText
            );
        }

        [Test]
        public void GetFields_OfficerRatings_UsesNamedEditableRatings()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Officers><Officer><TypeID>OF1</TypeID><DisplayName>Officer one</DisplayName><Ratings><Entry><Key>Diplomacy</Key><Value>42</Value></Entry><Entry><Key>Combat</Key><Value>17</Value></Entry></Ratings><CanBetray>false</CanBetray></Officer></Officers>"
            );
            ContentPackEditorField diplomacy = catalog
                .Records[0]
                .GetFields()
                .Single(field => field.Name == "Diplomacy");

            diplomacy.SetValue("65");

            XDocument saved = XDocument.Parse(catalog.Document.Text);
            Assert.AreEqual("65", saved.Descendants("Entry").First().Element("Value").Value);
            Assert.AreEqual("17", saved.Descendants("Entry").Last().Element("Value").Value);
            Assert.IsTrue(
                catalog.Records[0].GetFields().Single(field => field.Name == "Can betray").IsBoolean
            );
        }

        [Test]
        public void GetFields_MessageImageKey_DoesNotTreatAThemeLookupKeyAsAnAssetPath()
        {
            ContentPackEditorCatalog catalog = Create(
                "<MessageDefinitions><MessageDefinition ID='report'><BackgroundImageKey>advice</BackgroundImageKey></MessageDefinition></MessageDefinitions>"
            );
            ContentPackEditorField key = catalog
                .Records[0]
                .GetFields()
                .Single(field => field.MemberName == "BackgroundImageKey");

            Assert.IsFalse(key.IsMedia);
        }

        [Test]
        public void GetFields_RepeatedNestedEntries_ExposesDistinctSectionsAndShortCaptions()
        {
            ContentPackEditorCatalog catalog = Create(
                "<FactionThemes><FactionTheme><StrategyAdvisor><Notifications>"
                    + "<StrategyAdvisorNotificationTheme><NotificationType>Bombardment</NotificationType>"
                    + "<Protocol><FrameCount>61</FrameCount></Protocol></StrategyAdvisorNotificationTheme>"
                    + "<StrategyAdvisorNotificationTheme><NotificationType>PlanetaryAssault</NotificationType>"
                    + "<Protocol><FrameCount>42</FrameCount></Protocol></StrategyAdvisorNotificationTheme>"
                    + "</Notifications></StrategyAdvisor></FactionTheme></FactionThemes>"
            );
            ContentPackEditorField[] fields = catalog
                .Records.Single()
                .GetFields()
                .Where(field => field.MemberName == "FrameCount")
                .ToArray();

            Assert.IsTrue(fields.All(field => field.Caption == "Frame count"));
            CollectionAssert.AreEqual(
                new[]
                {
                    "Strategy advisor",
                    "Notifications",
                    "Strategy advisor notification theme 1",
                    "Protocol",
                },
                fields[0].SectionPath
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    "Strategy advisor",
                    "Notifications",
                    "Strategy advisor notification theme 2",
                    "Protocol",
                },
                fields[1].SectionPath
            );
            Assert.IsFalse(catalog.Document.IsDirty);

            fields[1].SetValue("75");

            CollectionAssert.AreEqual(
                new[] { "61", "75" },
                XDocument
                    .Parse(catalog.Document.Text)
                    .Descendants("FrameCount")
                    .Select(item => item.Value)
            );
        }

        [Test]
        public void GetFields_ArtworkAndDescription_AppearInSeparateSections()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Regiments><Regiment><TypeID>RE1</TypeID><DisplayImagePath>Pack/unit</DisplayImagePath><EncyclopediaDescription>Unit description.</EncyclopediaDescription></Regiment></Regiments>"
            );
            ContentPackEditorField[] fields = catalog.Records[0].GetFields().ToArray();

            Assert.AreEqual(
                "Artwork and audio",
                fields.Single(field => field.Value == "Pack/unit").Section
            );
            Assert.AreEqual(
                "Descriptions",
                fields.Single(field => field.Value == "Unit description.").Section
            );
            Assert.AreEqual("Properties", fields.Single(field => field.Value == "RE1").Section);
        }

        [Test]
        public void GetFields_NumericDisplayName_RemainsText()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Officers><Officer><TypeID>OF1</TypeID><DisplayName>123</DisplayName></Officer></Officers>"
            );
            ContentPackEditorField field = catalog
                .Records[0]
                .GetFields()
                .Single(candidate => candidate.MemberName == "DisplayName");

            Assert.IsFalse(field.IsNumber);
            field.SetValue("Named officer");
            Assert.AreEqual("Named officer", catalog.Records[0].Name);
        }

        [Test]
        public void GetField_MissingEligibilityList_DoesNotChangeDocumentUntilEdited()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Officers><Officer><TypeID>OF1</TypeID></Officer></Officers>"
            );
            ContentPackEditorField field = catalog.Records[0].GetField("AllowedRanks");

            Assert.IsTrue(field.IsList);
            CollectionAssert.Contains(field.Choices, "Admiral");
            Assert.IsFalse(catalog.Document.IsDirty);
            Assert.IsNull(catalog.Records[0].Element.Element("AllowedRanks"));
            field.SetListValues(new[] { "Admiral", "Commander" });
            CollectionAssert.AreEqual(
                new[] { "Admiral", "Commander" },
                catalog
                    .Records[0]
                    .Element.Element("AllowedRanks")
                    .Elements("OfficerRank")
                    .Select(element => element.Value)
            );
        }

        [Test]
        public void GetField_MissingScalarMember_OnlyAttachesOnChange()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Officers><Officer><TypeID>OF1</TypeID></Officer></Officers>"
            );
            ContentPackEditorField field = catalog.Records[0].GetField("IsRecruitable");

            Assert.IsTrue(field.IsBoolean);
            Assert.AreEqual("false", field.Value);
            field.SetValue("false");
            Assert.IsFalse(catalog.Document.IsDirty);
            field.SetValue("true");
            Assert.AreEqual("true", catalog.Records[0].Element.Element("IsRecruitable").Value);
        }

        [Test]
        public void GetRatingField_MissingEntry_IsTypedAndDoesNotModifyDocumentUntilEdited()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Officers><Officer><TypeID>OF1</TypeID><Ratings /></Officer></Officers>"
            );
            ContentPackEditorField field = catalog.Records[0].GetRatingField("Diplomacy");

            Assert.IsTrue(field.IsNumber);
            Assert.AreEqual("Ratings", field.Group);
            Assert.IsFalse(catalog.Document.IsDirty);
            Assert.Throws<ArgumentException>(() => field.SetValue("1.5"));
            field.SetValue("27");

            XElement entry = XDocument.Parse(catalog.Document.Text).Descendants("Entry").Single();
            Assert.AreEqual("Diplomacy", entry.Element("Key").Value);
            Assert.AreEqual("27", entry.Element("Value").Value);
        }

        [Test]
        public void GetRatingField_MissingRatings_AttachesOnlyTheEditedDictionaryEntry()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Officers><Officer><TypeID>OF1</TypeID></Officer></Officers>"
            );
            catalog.Records[0].GetRatingField("Combat").SetValue("41");

            XElement ratings = XDocument
                .Parse(catalog.Document.Text)
                .Descendants("Ratings")
                .Single();
            Assert.AreEqual(1, ratings.Elements().Count());
            Assert.AreEqual("Combat", ratings.Element("Entry").Element("Key").Value);
        }

        [Test]
        public void GetRatingField_MultiplePendingSkills_UsesOneRatingsDictionary()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Officers><Officer><TypeID>OF1</TypeID></Officer></Officers>"
            );
            ContentPackEditorField combat = catalog.Records[0].GetRatingField("Combat");
            ContentPackEditorField diplomacy = catalog.Records[0].GetRatingField("Diplomacy");
            combat.SetValue("40");
            diplomacy.SetValue("50");

            XElement ratings = XDocument
                .Parse(catalog.Document.Text)
                .Descendants("Ratings")
                .Single();
            Assert.AreEqual(2, ratings.Elements("Entry").Count());
            Assert.AreSame(combat, catalog.Records[0].GetRatingField("Combat"));
        }

        [Test]
        public void GetFields_EmptyComplexCollectionWrapper_DoesNotExposeItAsAStringList()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Factions><Faction><InstanceID>SIDE1</InstanceID><Messages><Entry><Key>Advice</Key><Value><ArrayOfMessage /></Value></Entry></Messages></Faction></Factions>"
            );

            Assert.IsFalse(catalog.Records[0].GetFields().Any(field => field.Group == "Messages"));
            Assert.IsFalse(catalog.Document.IsDirty);
        }

        [Test]
        public void GetFields_EmptyDefinitionPathList_WritesTheDeclaredPathElementName()
        {
            ContentPackEditorCatalog catalog = Create(
                "<ContentPack><ID>test-pack</ID><FactionPaths /></ContentPack>"
            );
            ContentPackEditorField paths = catalog
                .Records[0]
                .GetFields()
                .Single(field => field.MemberName == "FactionPaths");
            paths.SetListValues(new[] { "Factions/Example/faction.xml" });

            XElement item = XDocument
                .Parse(catalog.Document.Text)
                .Root.Element("FactionPaths")
                .Elements()
                .Single();
            Assert.AreEqual("Path", item.Name.LocalName);
            Assert.AreEqual("Factions/Example/faction.xml", item.Value);
        }

        /// <summary>
        /// Creates an in-memory catalog without touching the filesystem.
        /// </summary>
        /// <param name="xml">The authored fixture document.</param>
        /// <returns>The editable catalog.</returns>
        private static ContentPackEditorCatalog Create(string xml) =>
            new(
                new ContentPackEditorDocument(
                    "Pack/Data/units.xml",
                    Path.Combine(Path.GetTempPath(), "source.xml"),
                    Path.Combine(Path.GetTempPath(), "target.xml"),
                    xml
                )
            );

        /// <summary>
        /// Creates an in-memory starting-setup definition.
        /// </summary>
        /// <param name="contents">The authored fixture XML.</param>
        /// <returns>The editable record.</returns>
        private static ContentPackEditorRecord CreateGenerationRecord(string contents) =>
            CreateRecord("<GameGenerationConfig>" + contents + "</GameGenerationConfig>");

        /// <summary>
        /// Creates an in-memory rules definition with a sparse numeric lookup table.
        /// </summary>
        /// <returns>The editable record.</returns>
        private static ContentPackEditorRecord CreateRules() =>
            CreateRecord(
                "<GameConfig><ProbabilityTables><Mission><Foil><Entry><Key>0</Key><Value>10</Value></Entry><Entry><Key>10</Key><Value>20</Value></Entry></Foil></Mission></ProbabilityTables></GameConfig>"
            );

        /// <summary>
        /// Creates an in-memory record over an isolated document.
        /// </summary>
        /// <param name="xml">The authored fixture XML.</param>
        /// <returns>The editable record.</returns>
        private static ContentPackEditorRecord CreateRecord(string xml)
        {
            string path = Path.Combine(Path.GetTempPath(), "record-tests.xml");
            return new ContentPackEditorCatalog(
                new ContentPackEditorDocument("Pack/Data/record.xml", path, path, xml)
            ).Records.Single();
        }
    }
}
