using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentPreloadBuilderTests
    {
        [Test]
        public void CreateStrategy_CurrentShipReferences_UsesOverridesAndDeduplicatesOptionalExtras()
        {
            GameDataCatalog data = CreateData(
                new[]
                {
                    new CapitalShip
                    {
                        TypeID = "ONE",
                        SmallDisplayImagePath = "Pack/Ships/current",
                    },
                    new CapitalShip
                    {
                        TypeID = "TWO",
                        SmallDisplayImagePath = "Pack/Ships/current",
                    },
                    new CapitalShip { TypeID = "THREE", SmallDisplayImagePath = string.Empty },
                },
                new FactionThemes()
            );
            ContentPreloadManifest extras = new()
            {
                TexturesPerFrame = 7,
                Textures = new() { "Pack/Extra", "Pack/Ships/current" },
            };
            ContentPreloadManifest first = ContentPreloadBuilder.CreateStrategy(data, extras);
            CollectionAssert.AreEqual(new[] { "Pack/Extra", "Pack/Ships/current" }, first.Textures);
            Assert.AreEqual(7, first.TexturesPerFrame);
            data.CapitalShips[0].SmallDisplayImagePath = "Pack/Ships/replaced";
            CollectionAssert.Contains(
                ContentPreloadBuilder.CreateStrategy(data, null).Textures,
                "Pack/Ships/replaced"
            );
            CollectionAssert.DoesNotContain(first.Textures, "Pack/Ships/replaced");
            CollectionAssert.AreEqual(
                new[] { "Pack/Extra", "Pack/Ships/current" },
                extras.Textures
            );
        }

        [Test]
        public void CreateStrategy_InterfaceFeedback_ResolvesThemeAudioWithoutLoadingUnrelatedNotifications()
        {
            FactionTheme theme = new()
            {
                StrategyWindowSounds = new() { PlanetWindowMinimizeSoundPath = "Pack/UI/minimize" },
                StrategyAdvisor = new()
                {
                    AudioRoot = "Pack/Advisor",
                    PlanetaryGarrisonManagementEnabled = new()
                    {
                        AudioOptions = new() { "garrison-a", "garrison-b" },
                    },
                    Notifications = new()
                    {
                        new()
                        {
                            NotificationType = AdvisorNotificationType.PlanetaryAssault,
                            Droid = new() { AudioPath = "Pack/Advisor/assault" },
                        },
                        new()
                        {
                            NotificationType = AdvisorNotificationType.FleetArrived,
                            Droid = new() { AudioPath = "Pack/Advisor/unrelated" },
                        },
                    },
                },
            };
            ContentPreloadManifest manifest = ContentPreloadBuilder.CreateStrategy(
                CreateData(Array.Empty<CapitalShip>(), new() { theme }),
                null
            );
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "Pack/UI/minimize",
                    "Pack/Advisor/garrison-a",
                    "Pack/Advisor/garrison-b",
                    "Pack/Advisor/assault",
                },
                manifest.Audio
            );
        }

        [Test]
        public void CreateStrategy_InstalledPacks_EveryWarmupAddressResolves()
        {
            string contentRoot = ContentPackLoader.ResolveContentRootPath();
            foreach (
                ContentPackDefinition definition in ContentPackLoader.DiscoverPacks(contentRoot)
            )
            {
                ContentPack pack = ContentPackLoader.OpenExact(
                    contentRoot,
                    definition.ID,
                    definition.DefaultScenarioID,
                    Array.Empty<string>()
                );
                ContentPreloadManifest manifest = ContentPreloadBuilder.CreateStrategy(
                    pack.GameData,
                    null
                );
                foreach (string address in manifest.Textures)
                    Assert.IsNotNull(
                        pack.FileResolver.ResolveFile(address, ".png", ".jpg", ".jpeg"),
                        $"{definition.ID}: missing texture {address}"
                    );
                foreach (string address in manifest.Audio)
                    Assert.IsNotNull(
                        pack.FileResolver.ResolveFile(address, ".wav", ".ogg", ".mp3"),
                        $"{definition.ID}: missing audio {address}"
                    );
            }
        }

        [Test]
        public void GetPreloadManifest_StandardPhasesWithoutAuthoredLists_ProvidesDerivedDefaults()
        {
            GameDataCatalog data = CreateData(
                new[]
                {
                    new CapitalShip { TypeID = "SHIP", SmallDisplayImagePath = "Pack/Ship" },
                },
                new()
            );
            string root = Path.GetTempPath();
            ContentPack pack = new(
                root,
                root,
                new ContentFileResolver(root, root),
                new(),
                new(),
                Array.Empty<ContentFactionDefinition>(),
                data,
                new Dictionary<string, ContentPreloadManifest>()
            );
            CollectionAssert.AreEqual(
                new[] { "Pack/Ship" },
                pack.GetPreloadManifest("strategy").Textures
            );
            Assert.Greater(pack.GetPreloadManifest("main-menu").TexturesPerFrame, 0);
            Assert.Throws<InvalidDataException>(() => pack.GetPreloadManifest("unknown-phase"));
        }

        /// <summary>
        /// Creates an isolated typed catalog without depending on installed pack measurements.
        /// </summary>
        /// <param name="ships">The ship artwork references.</param>
        /// <param name="themes">The current interface themes.</param>
        /// <returns>The fixture-owned catalog.</returns>
        private static GameDataCatalog CreateData(CapitalShip[] ships, FactionThemes themes) =>
            new(
                new GameConfig(),
                new GameGenerationConfig(),
                Array.Empty<Faction>(),
                Array.Empty<PlanetSector>(),
                Array.Empty<Building>(),
                ships,
                Array.Empty<Starfighter>(),
                Array.Empty<Regiment>(),
                Array.Empty<SpecialForces>(),
                Array.Empty<Officer>(),
                Array.Empty<GameEvent>(),
                Array.Empty<MessageDefinition>(),
                new EncyclopediaEntries(),
                themes
            );
    }
}
