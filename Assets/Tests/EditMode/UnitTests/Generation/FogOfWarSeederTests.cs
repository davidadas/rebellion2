using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Generation;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class FogOfWarSeederTests
    {
        [TestCase(false, false, false)]
        [TestCase(false, false, true)]
        [TestCase(false, true, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(true, true, true)]
        public void Seed_StartingIntelligence_RevealsOnlySelectedCategories(
            bool agents,
            bool fleets,
            bool buildings
        )
        {
            var (game, sector, planet, empire, alliance) = BuildScene();
            game.AttachNode(
                new Officer { InstanceID = "officer", OwnerInstanceID = empire.InstanceID },
                planet
            );
            game.AttachNode(
                new SpecialForces
                {
                    InstanceID = "special",
                    OwnerInstanceID = empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            game.AttachNode(
                new Building
                {
                    InstanceID = "building",
                    OwnerInstanceID = empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            game.AttachNode(
                new Regiment
                {
                    InstanceID = "regiment",
                    OwnerInstanceID = empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            game.AttachNode(
                new Starfighter
                {
                    InstanceID = "fighter",
                    OwnerInstanceID = empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            Fleet fleet = new() { InstanceID = "fleet", OwnerInstanceID = empire.InstanceID };
            CapitalShip ship = new()
            {
                InstanceID = "ship",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(
                new Officer { InstanceID = "passenger", OwnerInstanceID = empire.InstanceID },
                ship
            );
            GenerationContext context = Wrap(game);
            context.Config.StartingIntelligence = new List<StartingIntelligenceProfile>
            {
                new()
                {
                    Difficulty = context.Summary.Difficulty,
                    RevealAgents = agents,
                    RevealFleets = fleets,
                    RevealBuildings = buildings,
                },
            };

            new FogOfWarSeeder().Seed(context);

            PlanetSnapshot snapshot = alliance.Fog.Snapshots[sector.InstanceID].Planets[
                planet.InstanceID
            ];
            Assert.AreEqual(agents ? 1 : 0, snapshot.Officers.Count);
            Assert.AreEqual(agents ? 1 : 0, snapshot.SpecialForces.Count);
            Assert.AreEqual(buildings ? 1 : 0, snapshot.Buildings.Count);
            Assert.AreEqual(fleets ? 1 : 0, snapshot.Fleets.Count);
            Assert.AreEqual(
                agents && fleets ? 1 : 0,
                snapshot.Fleets.Sum(fleet =>
                    fleet.GetChildren<CapitalShip>().Sum(ship => ship.GetChildren<Officer>().Count)
                )
            );
            Assert.AreEqual(1, snapshot.Regiments.Count);
            Assert.AreEqual(1, snapshot.Starfighters.Count);
        }

        [TestCase(GameDifficulty.Easy)]
        [TestCase(GameDifficulty.Medium)]
        [TestCase(GameDifficulty.Hard)]
        public void Seed_UnexploredPlanets_HidesForeignCoreWorldButKeepsOwnedWorld(
            GameDifficulty difficulty
        )
        {
            var (game, sector, planet, empire, alliance) = BuildScene();
            GenerationContext context = Wrap(game);
            context.Summary.Difficulty = difficulty;
            context.Config.StartingIntelligence = new List<StartingIntelligenceProfile>
            {
                new() { Difficulty = difficulty, StartWithUnexploredPlanets = true },
            };

            new FogOfWarSeeder().Seed(context);

            Assert.IsEmpty(alliance.Fog.Snapshots);
            FogOfWarQueries queries = new(game);
            Assert.IsTrue(
                queries
                    .BuildFactionView(alliance)
                    .GetChildren<Planet>(recursive: true)
                    .Single()
                    .IsUnexploredView
            );
            Assert.IsFalse(
                queries
                    .BuildFactionView(empire)
                    .GetChildren<Planet>(recursive: true)
                    .Single()
                    .IsUnexploredView
            );
        }

        [Test]
        public void Seed_UnexploredPlanetsWithVisibilityOverride_RevealsConfiguredPlanet()
        {
            var (game, sector, planet, _, alliance) = BuildScene();
            GenerationContext context = Wrap(game);
            context.Config.StartingIntelligence = new List<StartingIntelligenceProfile>
            {
                new()
                {
                    Difficulty = context.Summary.Difficulty,
                    StartWithUnexploredPlanets = true,
                    RevealAgents = false,
                    RevealFleets = false,
                    RevealBuildings = false,
                },
            };
            context.Config.GalaxyClassification.FactionSetups.Add(
                new FactionSetup
                {
                    FactionID = "FNEMP1",
                    StartingPlanets = new List<StartingPlanet>
                    {
                        new()
                        {
                            PlanetTypeID = planet.TypeID,
                            VisibleToFactionIDs = new List<string> { alliance.InstanceID },
                        },
                    },
                }
            );
            game.AttachNode(
                new Officer { InstanceID = "officer", OwnerInstanceID = "FNEMP1" },
                planet
            );

            new FogOfWarSeeder().Seed(context);

            Assert.AreEqual(
                1,
                alliance.Fog.Snapshots[sector.InstanceID].Planets[planet.InstanceID].Officers.Count
            );
            Assert.IsTrue(planet.WasVisitedBy(alliance.InstanceID));
        }

        [Test]
        public void Seed_ForeignCorePlanet_CapturesResourceSnapshotForNonOwner()
        {
            var (game, coreSector, empirePlanet, _, alliance) = BuildScene();

            new FogOfWarSeeder().Seed(Wrap(game));

            Assert.IsTrue(
                alliance.Fog.Snapshots.ContainsKey(coreSector.InstanceID),
                "Alliance should have a snapshot of the Empire-owned core sector."
            );
            PlanetSnapshot snapshot = alliance.Fog.Snapshots[coreSector.InstanceID].Planets[
                empirePlanet.InstanceID
            ];
            Assert.AreEqual(empirePlanet.EnergyCapacity, snapshot.EnergyCapacity);
            Assert.AreEqual(empirePlanet.NumRawResourceNodes, snapshot.NumRawResourceNodes);
        }

        [Test]
        public void Seed_OwnedCorePlanet_NoSnapshotForOwner()
        {
            var (game, coreSector, _, empire, _) = BuildScene();

            new FogOfWarSeeder().Seed(Wrap(game));

            Assert.IsFalse(
                empire.Fog.Snapshots.ContainsKey(coreSector.InstanceID),
                "Owner should not have a snapshot of their own planet from the seeder."
            );
        }

        [Test]
        public void Seed_RimPlanetWithoutOverride_NoSnapshotForOtherFactions()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary() };
            game.SetConfig(new GameConfig { Planet = new GameConfig.PlanetConfig() });
            Faction empire = new Faction { InstanceID = "FNEMP1" };
            Faction alliance = new Faction { InstanceID = "FNALL1" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector rim = new PlanetSector
            {
                InstanceID = "rim_sector",
                SectorType = PlanetSectorType.OuterRim,
            };
            rim.AddChild(
                new Planet
                {
                    InstanceID = "HOTH",
                    OwnerInstanceID = "FNALL1",
                    IsColonized = true,
                }
            );
            game.Galaxy = new GalaxyMap();
            game.Galaxy.AddChild(rim);

            new FogOfWarSeeder().Seed(Wrap(game));

            Assert.IsFalse(
                empire.Fog.Snapshots.ContainsKey(rim.InstanceID),
                "Foreign rim planets without an explicit override should remain hidden."
            );
        }

        [Test]
        public void Seed_VisibilityOverride_CapturesSnapshotForListedFaction()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary() };
            game.SetConfig(new GameConfig { Planet = new GameConfig.PlanetConfig() });
            Faction empire = new Faction { InstanceID = "FNEMP1" };
            Faction alliance = new Faction { InstanceID = "FNALL1" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector rim = new PlanetSector
            {
                InstanceID = "rim_sector",
                SectorType = PlanetSectorType.OuterRim,
            };
            rim.AddChild(
                new Planet
                {
                    InstanceID = "YAVIN",
                    TypeID = "PLSUM06",
                    OwnerInstanceID = "FNALL1",
                    IsColonized = true,
                }
            );
            game.Galaxy = new GalaxyMap();
            game.Galaxy.AddChild(rim);

            GameGenerationConfig config = new GameGenerationConfig
            {
                GalaxyClassification = new GalaxyClassificationSection
                {
                    FactionSetups = new List<FactionSetup>
                    {
                        new FactionSetup
                        {
                            FactionID = "FNALL1",
                            StartingPlanets = new List<StartingPlanet>
                            {
                                new StartingPlanet
                                {
                                    PlanetTypeID = "PLSUM06",
                                    VisibleToFactionIDs = new List<string> { "FNEMP1" },
                                },
                            },
                        },
                    },
                },
            };

            new FogOfWarSeeder().Seed(Wrap(game, config));

            Assert.IsTrue(
                empire.Fog.Snapshots.ContainsKey(rim.InstanceID),
                "Empire should see Yavin because the override grants visibility."
            );
            Assert.IsTrue(
                rim.GetChildren<Planet>()[0].WasVisitedBy("FNEMP1"),
                "Visibility overrides should mark the planet as known for the listed faction."
            );
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <returns>The constructed scene.</returns>
        private static (
            GameRoot game,
            PlanetSector coreSector,
            Planet empirePlanet,
            Faction empire,
            Faction alliance
        ) BuildScene()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary() };
            game.SetConfig(new GameConfig { Planet = new GameConfig.PlanetConfig() });

            Faction empire = new Faction { InstanceID = "FNEMP1" };
            Faction alliance = new Faction { InstanceID = "FNALL1" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector coreSector = new PlanetSector
            {
                InstanceID = "core_sector",
                SectorType = PlanetSectorType.Core,
            };
            Planet empirePlanet = new Planet
            {
                InstanceID = "CORUSCANT",
                TypeID = "PLSEW05",
                OwnerInstanceID = "FNEMP1",
                IsColonized = true,
                EnergyCapacity = 9,
                NumRawResourceNodes = 6,
            };
            game.Galaxy = new GalaxyMap();
            game.AttachNode(coreSector, game.Galaxy);
            game.AttachNode(empirePlanet, coreSector);

            return (game, coreSector, empirePlanet, empire, alliance);
        }

        /// <summary>
        /// Executes wrap.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="config">The config.</param>
        /// <returns>The result of wrap.</returns>
        private static GenerationContext Wrap(GameRoot game, GameGenerationConfig config = null)
        {
            GenerationContext ctx = GenerationContextFactory.CreateDefault();
            ctx.Game = game;
            if (config != null)
                ctx.Config = config;
            return ctx;
        }
    }
}
