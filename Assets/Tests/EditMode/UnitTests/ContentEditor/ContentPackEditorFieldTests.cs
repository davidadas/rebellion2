using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentPackEditorFieldTests
    {
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

        /// <summary>
        /// Creates a sector with two planets for coordinate-edit tests.
        /// </summary>
        /// <returns>The editable catalog.</returns>
        private static ContentPackEditorCatalog CreateMap()
        {
            return Create(
                "<PlanetSectors><PlanetSector><TypeID>SECTOR</TypeID><PositionX>100</PositionX><PositionY>50</PositionY><Planets><Planet><TypeID>ONE</TypeID><PositionX>105</PositionX><PositionY>57</PositionY></Planet><Planet><TypeID>TWO</TypeID><PositionX>120</PositionX><PositionY>59</PositionY></Planet></Planets></PlanetSector></PlanetSectors>"
            );
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
    }
}
