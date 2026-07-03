using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Encyclopedia
{
    [TestFixture]
    public class EncyclopediaCatalogBuilderTests
    {
        [Test]
        public void Build_WithEntityEncyclopediaData_UsesEntitySpecificFields()
        {
            CapitalShip ship = new CapitalShip
            {
                TypeID = "SHIP1",
                DisplayName = "Static Ship",
                DisplayImagePath = "Art/UI/Units/static_ship",
                EncyclopediaImagePath = "Art/HD/UI/Encyclopedia/static_ship",
                EncyclopediaStats = new List<EncyclopediaEntryStat>
                {
                    new EncyclopediaEntryStat { Label = "Maintenance Cost", Value = "4" },
                },
                Description = "Static description.",
                EncyclopediaDescription = "Encyclopedia description.",
            };

            EncyclopediaCatalog catalog = BuildCatalog(
                new EncyclopediaEntries(),
                capitalShips: new[] { ship }
            );

            EncyclopediaEntry entry = catalog.FindEntry("SHIP1", null);

            Assert.IsNotNull(entry);
            Assert.AreEqual("Static Ship", entry.DisplayName);
            Assert.AreEqual(EncyclopediaEntryCategory.Ship, entry.Category);
            Assert.AreEqual("Art/HD/UI/Encyclopedia/static_ship", entry.ImagePath);
            Assert.AreEqual(1, entry.Stats.Count);
            Assert.AreEqual("Maintenance Cost", entry.Stats[0].Label);
            Assert.AreEqual("4", entry.Stats[0].Value);
            Assert.AreEqual("Encyclopedia description.", entry.Description);
        }

        [Test]
        public void Build_WithEntityWithoutEncyclopediaImage_DoesNotUseDisplayImage()
        {
            Building building = new Building
            {
                TypeID = "BUILDING1",
                DisplayName = "Construction Yard",
                DisplayImagePath = "Art/UI/Units/construction_yard",
                Description = "Static building description.",
            };

            EncyclopediaCatalog catalog = BuildCatalog(
                new EncyclopediaEntries(),
                buildings: new[] { building }
            );

            EncyclopediaEntry entry = catalog.FindEntry("BUILDING1", null);

            Assert.IsNotNull(entry);
            Assert.AreEqual("Construction Yard", entry.DisplayName);
            Assert.AreEqual(EncyclopediaEntryCategory.Facility, entry.Category);
            Assert.IsNull(entry.ImagePath);
            Assert.AreEqual("Static building description.", entry.Description);
        }

        [Test]
        public void Build_WithAuthoredEntityDuplicate_KeepsGeneratedEntityEntry()
        {
            EncyclopediaEntries authoredEntries = new EncyclopediaEntries
            {
                new EncyclopediaEntry
                {
                    TypeID = "BUILDING1",
                    DisplayName = "Authored Facility",
                    Category = EncyclopediaEntryCategory.Concept,
                },
            };
            Building building = new Building
            {
                TypeID = "BUILDING1",
                DisplayName = "Construction Yard",
            };

            EncyclopediaCatalog catalog = BuildCatalog(
                authoredEntries,
                buildings: new[] { building }
            );

            EncyclopediaEntry entry = catalog.FindEntry("BUILDING1", null);

            Assert.IsNotNull(entry);
            Assert.AreEqual("Construction Yard", entry.DisplayName);
            Assert.AreEqual(EncyclopediaEntryCategory.Facility, entry.Category);
        }

        [Test]
        public void Build_WithConceptOverlay_KeepsAuthoredEntry()
        {
            EncyclopediaEntries authoredEntries = new EncyclopediaEntries
            {
                new EncyclopediaEntry
                {
                    TypeID = "FLEET",
                    DisplayName = "Fleet",
                    Category = EncyclopediaEntryCategory.Concept,
                    VisibleFactionInstanceID = "FNALL1",
                    ImagePath = "Art/HD/UI/Encyclopedia/fleet",
                    Description = "Concept description.",
                },
            };

            EncyclopediaCatalog catalog = BuildCatalog(authoredEntries);

            EncyclopediaEntry entry = catalog.FindEntry("FLEET", "FNALL1");

            Assert.IsNotNull(entry);
            Assert.AreEqual(EncyclopediaEntryCategory.Concept, entry.Category);
            Assert.AreEqual("Art/HD/UI/Encyclopedia/fleet", entry.ImagePath);
            Assert.AreEqual("Concept description.", entry.Description);
            Assert.IsNull(catalog.FindEntry("FLEET", "FNEMP1"));
        }

        [Test]
        public void Build_WithPlanetSystem_AddsPlanetEntries()
        {
            PlanetSystem system = new PlanetSystem
            {
                Planets = new List<Planet>
                {
                    new Planet
                    {
                        TypeID = "PLANET1",
                        DisplayName = "Balmorra",
                        PlanetIconPath = "Art/UI/StrategyView/balmorra",
                        EncyclopediaImagePath = "Art/HD/UI/Encyclopedia/balmorra",
                        Description = "Planet description.",
                        EncyclopediaDescription = "Planet encyclopedia description.",
                    },
                },
            };

            EncyclopediaCatalog catalog = BuildCatalog(
                new EncyclopediaEntries(),
                planetSystems: new[] { system }
            );

            EncyclopediaEntry entry = catalog.FindEntry("PLANET1", null);

            Assert.IsNotNull(entry);
            Assert.AreEqual("Balmorra", entry.DisplayName);
            Assert.AreEqual(EncyclopediaEntryCategory.System, entry.Category);
            Assert.AreEqual("Art/HD/UI/Encyclopedia/balmorra", entry.ImagePath);
            Assert.AreEqual("Planet encyclopedia description.", entry.Description);
        }

        [Test]
        public void Build_FromResourceData_UsesEncyclopediaImages()
        {
            EncyclopediaCatalog catalog = new EncyclopediaCatalogBuilder().Build();

            List<EncyclopediaEntry> entriesWithWrongImagePath = catalog
                .Where(entry =>
                    string.IsNullOrEmpty(entry.ImagePath)
                    || !entry.ImagePath.StartsWith("Art/HD/UI/Encyclopedia/")
                )
                .ToList();

            Assert.IsEmpty(entriesWithWrongImagePath);
        }

        [Test]
        public void Build_WithSingleAllowedOwner_SetsEntryOwner()
        {
            Starfighter starfighter = new Starfighter
            {
                TypeID = "FIGHTER1",
                DisplayName = "A-Wing Squadron",
                AllowedOwnerInstanceIDs = new List<string> { "FNALL1" },
            };

            EncyclopediaCatalog catalog = BuildCatalog(
                new EncyclopediaEntries(),
                starfighters: new[] { starfighter }
            );

            EncyclopediaEntry entry = catalog.FindEntry("FIGHTER1", null);

            Assert.IsNotNull(entry);
            Assert.AreEqual("FNALL1", entry.OwnerInstanceID);
        }

        [Test]
        public void Build_WithNullStaticEntries_IgnoresNullEntries()
        {
            PlanetSystem system = new PlanetSystem
            {
                Planets = new List<Planet>
                {
                    null,
                    new Planet { TypeID = "PLANET1", DisplayName = "Balmorra" },
                },
            };
            Building building = new Building
            {
                TypeID = "BUILDING1",
                DisplayName = "Construction Yard",
            };

            EncyclopediaCatalog catalog = BuildCatalog(
                new EncyclopediaEntries(),
                planetSystems: new[] { system },
                buildings: new Building[] { null, building }
            );

            Assert.AreEqual(2, catalog.Count);
            Assert.IsNotNull(catalog.FindEntry("PLANET1", null));
            Assert.IsNotNull(catalog.FindEntry("BUILDING1", null));
        }

        private static EncyclopediaCatalog BuildCatalog(
            EncyclopediaEntries authoredEntries,
            IEnumerable<PlanetSystem> planetSystems = null,
            IEnumerable<Building> buildings = null,
            IEnumerable<CapitalShip> capitalShips = null,
            IEnumerable<Starfighter> starfighters = null,
            IEnumerable<Regiment> regiments = null,
            IEnumerable<SpecialForces> specialForces = null,
            IEnumerable<Officer> officers = null
        )
        {
            EncyclopediaCatalogBuilder builder = new EncyclopediaCatalogBuilder();
            return builder.Build(
                authoredEntries,
                planetSystems,
                buildings,
                capitalShips,
                starfighters,
                regiments,
                specialForces,
                officers
            );
        }
    }
}
