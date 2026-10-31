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
        public void Move_SectorWouldOverflowAPlanet_RejectsBeforeChangingAnyCoordinates()
        {
            ContentPackEditorCatalog catalog = Create(
                "<PlanetSectors><PlanetSector><TypeID>SECTOR</TypeID><PositionX>0</PositionX><PositionY>0</PositionY><Planets><Planet><TypeID>ONE</TypeID><PositionX>5</PositionX><PositionY>7</PositionY></Planet><Planet><TypeID>TWO</TypeID><PositionX>2147483647</PositionX><PositionY>9</PositionY></Planet></Planets></PlanetSector></PlanetSectors>"
            );
            ContentPackEditorRecord sector = catalog.Records.Single();
            string before = sector.Element.ToString();

            Assert.Throws<ArgumentException>(() => catalog.Move(sector, 1, 1));

            Assert.AreEqual(before, sector.Element.ToString());
            Assert.IsFalse(catalog.Document.IsDirty);
        }

        [Test]
        public void Move_SectorSpansIntegerRange_PreservesValidPlanetOffsets()
        {
            ContentPackEditorCatalog catalog = Create(
                "<PlanetSectors><PlanetSector><TypeID>SECTOR</TypeID><PositionX>-2147483638</PositionX><PositionY>0</PositionY><Planets><Planet><TypeID>ONE</TypeID><PositionX>-2147483633</PositionX><PositionY>7</PositionY></Planet></Planets></PlanetSector></PlanetSectors>"
            );
            ContentPackEditorRecord sector = catalog.Records.Single();

            catalog.Move(sector, 2147483637, 1);

            Assert.AreEqual(2147483642, sector.Planets.Single().X);
            Assert.AreEqual(8, sector.Planets.Single().Y);
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
