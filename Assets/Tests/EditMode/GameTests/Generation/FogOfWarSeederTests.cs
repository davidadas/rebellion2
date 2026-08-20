using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Generation;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class FogOfWarSeederTests
    {
        [Test]
        public void Seed_ForeignCorePlanet_CapturesResourceSnapshotForNonOwner()
        {
            var (game, coreSystem, empirePlanet, _, alliance) = BuildScene();

            new FogOfWarSeeder().Seed(Wrap(game));

            Assert.IsTrue(
                alliance.Fog.Snapshots.ContainsKey(coreSystem.InstanceID),
                "Alliance should have a snapshot of the Empire-owned core system."
            );
            PlanetSnapshot snapshot = alliance.Fog.Snapshots[coreSystem.InstanceID].Planets[
                empirePlanet.InstanceID
            ];
            Assert.AreEqual(empirePlanet.EnergyCapacity, snapshot.EnergyCapacity);
            Assert.AreEqual(empirePlanet.NumRawResourceNodes, snapshot.NumRawResourceNodes);
        }

        [Test]
        public void Seed_OwnedCorePlanet_NoSnapshotForOwner()
        {
            var (game, coreSystem, _, empire, _) = BuildScene();

            new FogOfWarSeeder().Seed(Wrap(game));

            Assert.IsFalse(
                empire.Fog.Snapshots.ContainsKey(coreSystem.InstanceID),
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
                InstanceID = "rim_sys",
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
                InstanceID = "rim_sys",
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

        private static (
            GameRoot game,
            PlanetSector coreSystem,
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

            PlanetSector coreSystem = new PlanetSector
            {
                InstanceID = "core_sys",
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
            coreSystem.AddChild(empirePlanet);
            game.Galaxy = new GalaxyMap();
            game.Galaxy.AddChild(coreSystem);

            return (game, coreSystem, empirePlanet, empire, alliance);
        }

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
