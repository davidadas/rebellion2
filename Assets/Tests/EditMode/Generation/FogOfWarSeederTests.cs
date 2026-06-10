using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Generation;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class FogOfWarSeederTests
    {
        private static (
            GameRoot game,
            PlanetSystem coreSystem,
            Planet empirePlanet,
            Faction empire,
            Faction alliance
        ) BuildScene()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary() };
            game.SetConfig(new GameConfig { Planet = new GameConfig.PlanetConfig() });

            Faction empire = new Faction { InstanceID = "FNEMP1" };
            Faction alliance = new Faction { InstanceID = "FNALL1" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem coreSystem = new PlanetSystem
            {
                InstanceID = "core_sys",
                SystemType = PlanetSystemType.CoreSystem,
            };
            Planet empirePlanet = new Planet
            {
                InstanceID = "CORUSCANT",
                TypeID = "PLSEW05",
                OwnerInstanceID = "FNEMP1",
                IsColonized = true,
            };
            coreSystem.Planets.Add(empirePlanet);
            game.Galaxy = new GalaxyMap { PlanetSystems = new List<PlanetSystem> { coreSystem } };

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

        [Test]
        public void Seed_ForeignCorePlanet_CapturesSnapshotForNonOwner()
        {
            var (game, coreSystem, _, _, alliance) = BuildScene();

            new FogOfWarSeeder().Seed(Wrap(game));

            Assert.IsTrue(
                alliance.Fog.Snapshots.ContainsKey(coreSystem.InstanceID),
                "Alliance should have a snapshot of the Empire-owned core system."
            );
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
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem rim = new PlanetSystem
            {
                InstanceID = "rim_sys",
                SystemType = PlanetSystemType.OuterRim,
            };
            rim.Planets.Add(
                new Planet
                {
                    InstanceID = "HOTH",
                    OwnerInstanceID = "FNALL1",
                    IsColonized = true,
                }
            );
            game.Galaxy = new GalaxyMap { PlanetSystems = new List<PlanetSystem> { rim } };

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
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem rim = new PlanetSystem
            {
                InstanceID = "rim_sys",
                SystemType = PlanetSystemType.OuterRim,
            };
            rim.Planets.Add(
                new Planet
                {
                    InstanceID = "YAVIN",
                    TypeID = "PLSUM06",
                    OwnerInstanceID = "FNALL1",
                    IsColonized = true,
                }
            );
            game.Galaxy = new GalaxyMap { PlanetSystems = new List<PlanetSystem> { rim } };

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
        }
    }
}
