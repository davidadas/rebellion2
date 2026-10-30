using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentPackEditorCatalogTests
    {
        private string directory;

        /// <summary>
        /// Creates an isolated authoring folder.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(
                Path.GetTempPath(),
                "content-catalog-test-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(directory);
        }

        /// <summary>
        /// Removes the fixture-owned folder.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Directory.Delete(directory, true);
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
        public void SetValue_InvalidNumber_DoesNotChangeWorkingDocument()
        {
            ContentPackEditorCatalog catalog = Create(
                "<SpecialForces><SpecialForce><TypeID>SF1</TypeID><ConstructionCost>4</ConstructionCost></SpecialForce></SpecialForces>"
            );
            ContentPackEditorField cost = catalog
                .Records[0]
                .GetFields()
                .Single(field => field.Name == "Construction cost");

            Assert.Throws<ArgumentException>(() => cost.SetValue("not a number"));
            Assert.IsFalse(catalog.Document.IsDirty);
            Assert.AreEqual("4", cost.Value);
        }

        [Test]
        public void SetValue_DuplicateIdentifier_DoesNotChangeRecord()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Officers><Officer><TypeID>OF1</TypeID></Officer><Officer><TypeID>OF2</TypeID></Officer></Officers>"
            );
            ContentPackEditorField id = catalog.Records[0].GetFields().Single();

            Assert.Throws<ArgumentException>(() => id.SetValue("OF2"));
            Assert.AreEqual("OF1", catalog.Records[0].ID);
        }

        [Test]
        public void SetValue_DuplicateAttributeIdentifier_DoesNotChangeTheWorkingDocument()
        {
            ContentPackEditorCatalog catalog = Create(
                "<GameEvents><GameEvent ID='one' /><GameEvent ID='two' /></GameEvents>"
            );
            ContentPackEditorField id = catalog.Records[0].GetFields().Single();

            Assert.Throws<ArgumentException>(() => id.SetValue("two"));
            Assert.IsFalse(catalog.Document.IsDirty);
            Assert.AreEqual("one", catalog.Records[0].ID);
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
        public void SetValue_OneWeaponArc_PreservesOtherArcsAndCatalogEntries()
        {
            ContentPackEditorCatalog catalog = Create(
                "<CapitalShips><CapitalShip><TypeID>CS1</TypeID><PrimaryWeapons><Entry><Key><PrimaryWeaponType>Turbolaser</PrimaryWeaponType></Key><Value><ArrayOfInt><int>10</int><int>20</int><int>30</int><int>40</int><int>50</int></ArrayOfInt></Value></Entry></PrimaryWeapons></CapitalShip><CapitalShip><TypeID>CS2</TypeID><DisplayName>Other ship</DisplayName></CapitalShip></CapitalShips>"
            );
            XElement other = new XElement(catalog.Records[1].Element);
            ContentPackEditorField port = catalog
                .Records[0]
                .GetFields()
                .Single(field => field.Name == "Turbolaser / Port");
            Assert.AreEqual("Port", port.Caption);
            CollectionAssert.AreEqual(new[] { "Primary weapons", "Turbolaser" }, port.SectionPath);

            catalog
                .Records[0]
                .GetFields()
                .Single(field => field.Name == "Turbolaser / Port")
                .SetValue("75");

            CollectionAssert.AreEqual(
                new[] { "10", "20", "75", "40", "50" },
                XDocument
                    .Parse(catalog.Document.Text)
                    .Descendants("int")
                    .Select(element => element.Value)
            );
            Assert.IsTrue(XNode.DeepEquals(other, catalog.Records[1].Element));
        }

        [Test]
        public void Duplicate_Officer_AssignsUniqueTypeAndInstanceIdentifiers()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Officers><Officer><TypeID>OF1</TypeID><InstanceID>PERSON</InstanceID><DisplayName>Officer</DisplayName><Ratings><Entry><Key>Combat</Key><Value>50</Value></Entry></Ratings></Officer></Officers>"
            );

            ContentPackEditorRecord first = catalog.Duplicate(catalog.Records[0]);
            ContentPackEditorRecord second = catalog.Duplicate(catalog.Records[0]);

            Assert.AreNotEqual(first.ID, second.ID);
            Assert.AreNotEqual(
                first.Element.Element("InstanceID").Value,
                second.Element.Element("InstanceID").Value
            );
            Assert.AreEqual("50", second.GetFields().Single(field => field.Name == "Combat").Value);
        }

        [Test]
        public void Move_Sector_PreservesPlanetOffsets()
        {
            ContentPackEditorCatalog catalog = CreateMap();
            ContentPackEditorRecord sector = catalog.Records[0];

            catalog.Move(sector, 120, 60);

            Assert.AreEqual(125, sector.Planets[0].X);
            Assert.AreEqual(67, sector.Planets[0].Y);
            Assert.AreEqual(140, sector.Planets[1].X);
            Assert.AreEqual(69, sector.Planets[1].Y);
        }

        [Test]
        public void Duplicate_AttributeIdentity_AssignsANewIdentityWithoutChangingThePayload()
        {
            ContentPackEditorCatalog catalog = Create(
                "<GameEvents><GameEvent ID='arrival'><TickRange Min='2' Max='3' /></GameEvent></GameEvents>"
            );
            XElement payload = new XElement(catalog.Records[0].Element.Element("TickRange"));

            ContentPackEditorRecord copy = catalog.Duplicate(catalog.Records[0]);

            Assert.AreNotEqual("arrival", copy.ID);
            Assert.IsTrue(XNode.DeepEquals(payload, copy.Element.Element("TickRange")));
            Assert.AreEqual("arrival", catalog.Records[0].ID);
        }

        [Test]
        public void Duplicate_Sector_AssignsUniquePlanetIdentitiesAndPreservesTheirOffsets()
        {
            ContentPackEditorCatalog catalog = CreateMap();
            ContentPackEditorRecord source = catalog.Records[0];

            ContentPackEditorRecord copy = catalog.Duplicate(source);

            string[] ids = XDocument
                .Parse(catalog.Document.Text)
                .Descendants("TypeID")
                .Select(id => id.Value)
                .ToArray();
            Assert.AreEqual(ids.Length, ids.Distinct().Count());
            Assert.AreEqual(2, copy.Planets.Count);
            for (int i = 0; i < copy.Planets.Count; i++)
            {
                Assert.AreEqual(source.Planets[i].X - source.X, copy.Planets[i].X - copy.X);
                Assert.AreEqual(source.Planets[i].Y - source.Y, copy.Planets[i].Y - copy.Y);
            }
        }

        [Test]
        public void Duplicate_Planet_AddsTheCopyToTheSameSector()
        {
            ContentPackEditorCatalog catalog = CreateMap();
            ContentPackEditorRecord sector = catalog.Records[0];

            ContentPackEditorRecord copy = catalog.Duplicate(sector.Planets[0]);

            Assert.AreEqual(1, catalog.Records.Count);
            Assert.AreEqual(3, sector.Planets.Count);
            Assert.AreSame(sector.Element.Element("Planets"), copy.Element.Parent);
            Assert.AreNotEqual(sector.Planets[0].ID, copy.ID);
        }

        [Test]
        public void Remove_Planet_PreservesItsSectorAndSibling()
        {
            ContentPackEditorCatalog catalog = CreateMap();
            ContentPackEditorRecord sector = catalog.Records[0];
            XElement sibling = new XElement(sector.Planets[1].Element);

            catalog.Remove(sector.Planets[0]);

            Assert.AreEqual(1, catalog.Records.Count);
            Assert.IsTrue(XNode.DeepEquals(sibling, catalog.Records[0].Planets.Single().Element));
        }

        [Test]
        public void Move_Planet_DoesNotMoveSectorOrOtherPlanets()
        {
            ContentPackEditorCatalog catalog = CreateMap();
            ContentPackEditorRecord sector = catalog.Records[0];

            catalog.Move(sector.Planets[0], 111, 55);

            Assert.AreEqual(100, sector.X);
            Assert.AreEqual(50, sector.Y);
            Assert.AreEqual(120, sector.Planets[1].X);
            Assert.AreEqual(59, sector.Planets[1].Y);
        }

        [Test]
        public void SetValue_SectorCoordinate_AlsoMovesPlanets()
        {
            ContentPackEditorCatalog catalog = CreateMap();

            catalog
                .Records[0]
                .GetFields()
                .Single(field => field.Name == "Position X")
                .SetValue("130");

            Assert.AreEqual(135, catalog.Records[0].Planets[0].X);
            Assert.AreEqual(57, catalog.Records[0].Planets[0].Y);
        }

        [Test]
        public void SetValue_EventAttribute_PreservesConditionsAndActions()
        {
            ContentPackEditorCatalog catalog = Create(
                "<GameEvents><GameEvent Category='Story'><InstanceID>EVENT</InstanceID><Schedule><RandomDelay MinimumTicks='200' MaximumTicks='300'/></Schedule><Conditionals><IsActive NodeInstanceID='PERSON'/></Conditionals></GameEvent></GameEvents>"
            );

            catalog
                .Records[0]
                .GetFields()
                .Single(field => field.Name.EndsWith("Minimum ticks", StringComparison.Ordinal))
                .SetValue("150");

            XDocument xml = XDocument.Parse(catalog.Document.Text);
            Assert.AreEqual(
                "150",
                xml.Descendants("RandomDelay").Single().Attribute("MinimumTicks").Value
            );
            Assert.AreEqual(
                "300",
                xml.Descendants("RandomDelay").Single().Attribute("MaximumTicks").Value
            );
            Assert.AreEqual(
                "PERSON",
                xml.Descendants("IsActive").Single().Attribute("NodeInstanceID").Value
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
        public void SetValue_IntegerMember_RejectsFractionalValues()
        {
            ContentPackEditorCatalog catalog = Create(
                "<CapitalShips><CapitalShip><TypeID>CS1</TypeID><ConstructionCost>4</ConstructionCost></CapitalShip></CapitalShips>"
            );
            ContentPackEditorField field = catalog
                .Records[0]
                .GetFields()
                .Single(candidate => candidate.MemberName == "ConstructionCost");

            Assert.Throws<ArgumentException>(() => field.SetValue("1.5"));
            Assert.AreEqual("4", field.Value);
            Assert.IsFalse(catalog.Document.IsDirty);
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
        public void SetValue_EnumMember_RejectsUndefinedNamesAndNumericValues()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Officers><Officer><TypeID>OF1</TypeID><CurrentRank>None</CurrentRank></Officer></Officers>"
            );
            ContentPackEditorField field = catalog.Records[0].GetField("CurrentRank");

            Assert.Throws<ArgumentException>(() => field.SetValue("UnknownRank"));
            Assert.Throws<ArgumentException>(() => field.SetValue("999"));
            Assert.AreEqual("None", field.Value);
            field.SetValue("Admiral");
            Assert.AreEqual("Admiral", field.Value);
        }

        [Test]
        public void SetImportedAsset_EmptyVoiceList_PreservesPathItemNameAndPunctuation()
        {
            ContentPackEditorCatalog catalog = Create(
                "<Officers><Officer><TypeID>OF1</TypeID><VoiceSet><Order/></VoiceSet></Officer></Officers>"
            );
            ContentPackEditorField field = catalog
                .Records[0]
                .GetFields()
                .Single(candidate => candidate.Group == "VoiceSet");

            field.SetImportedAsset("Pack/Assets/yes, commander.wav");

            Assert.AreEqual(ContentPackEditorMediaType.Audio, field.MediaType);
            XElement list = catalog.Records[0].Element.Element("VoiceSet").Element("Order");
            Assert.AreEqual(1, list.Elements().Count());
            Assert.AreEqual("Path", list.Elements().Single().Name.LocalName);
            Assert.AreEqual("Pack/Assets/yes, commander.wav", list.Elements().Single().Value);
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
        /// Creates a sector with two planets for coordinate-edit tests.
        /// </summary>
        /// <returns>The editable catalog.</returns>
        private ContentPackEditorCatalog CreateMap()
        {
            return Create(
                "<PlanetSectors><PlanetSector><TypeID>SECTOR</TypeID><PositionX>100</PositionX><PositionY>50</PositionY><Planets><Planet><TypeID>ONE</TypeID><PositionX>105</PositionX><PositionY>57</PositionY></Planet><Planet><TypeID>TWO</TypeID><PositionX>120</PositionX><PositionY>59</PositionY></Planet></Planets></PlanetSector></PlanetSectors>"
            );
        }

        /// <summary>
        /// Creates an editable catalog over the supplied XML.
        /// </summary>
        /// <param name="xml">The fixture data.</param>
        /// <returns>The editable catalog.</returns>
        private ContentPackEditorCatalog Create(string xml)
        {
            string source = Path.Combine(directory, "source.xml");
            File.WriteAllText(source, xml);
            return new ContentPackEditorCatalog(
                new ContentPackEditorDocument(
                    "Pack/Data/units.xml",
                    source,
                    Path.Combine(directory, "target.xml")
                )
            );
        }
    }
}
