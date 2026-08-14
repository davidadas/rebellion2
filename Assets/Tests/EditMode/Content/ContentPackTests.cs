using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentPackTests
    {
        [Test]
        public void OpenActive_ConfiguredCatalog_ComposesSelectedPackAndScenario()
        {
            ContentPack pack = ContentPackLoader.OpenActive();

            Assert.AreEqual("classic-galactic-civil-war", pack.Definition.ID);
            Assert.AreEqual("standard", pack.Scenario.ID);
            CollectionAssert.AreEquivalent(
                pack.Scenario.PlayableFactionIDs,
                pack.Factions.Select(faction => faction.ID)
            );
            Assert.IsNotEmpty(pack.GameData.Factions);
            Assert.IsNotEmpty(pack.GameData.PlanetSystems);
            Assert.IsNotEmpty(pack.GameData.Officers);
        }

        [TestCase("main-menu")]
        [TestCase("strategy")]
        public void PreloadManifests_ConfiguredScope_MatchesContentOwner(string preloadID)
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            ContentPreloadManifest applicationManifest =
                ContentPackLoader.LoadApplicationPreloadManifest(pack.ContentRootPath, preloadID);
            ContentPreloadManifest packManifest = pack.GetPreloadManifest(preloadID);
            string[] applicationAddresses = applicationManifest
                .Textures.Concat(applicationManifest.TextureDirectories)
                .Concat(applicationManifest.Audio)
                .Concat(applicationManifest.Models)
                .ToArray();
            string[] packAddresses = packManifest
                .Textures.Concat(packManifest.TextureDirectories)
                .Concat(packManifest.Audio)
                .Concat(packManifest.Models)
                .ToArray();

            Assert.IsNotEmpty(applicationAddresses);
            Assert.IsTrue(
                applicationAddresses.All(address =>
                    address.StartsWith("Application/", System.StringComparison.Ordinal)
                )
            );
            Assert.IsTrue(
                packAddresses.All(address =>
                    address.StartsWith("Pack/", System.StringComparison.Ordinal)
                )
            );
        }
        [TestCase(RuntimePlatform.OSXPlayer, "Game.app/Contents/Resources/Data")]
        [TestCase(RuntimePlatform.OSXPlayer, "Game.app/Contents")]
        [TestCase(RuntimePlatform.LinuxPlayer, "Game_Data")]
        [TestCase(RuntimePlatform.WindowsPlayer, "Game_Data")]
        public void ResolvePlayerContentRootPath_DesktopPlayer_ReturnsDirectoryBesideArtifact(
            RuntimePlatform platform,
            string relativeDataPath
        )
        {
            string playerDirectory = Path.Combine(Path.GetTempPath(), "content-pack-player-layout");
            string dataPath = Path.Combine(playerDirectory, relativeDataPath);

            string contentRoot = ContentPackLoader.ResolvePlayerContentRootPath(dataPath, platform);

            Assert.AreEqual(Path.Combine(playerDirectory, "Content"), contentRoot);
        }

        [Test]
        public void ResolvePlayerContentRootPath_MacBundleLayout_DoesNotDependOnPlatformEnum()
        {
            string playerDirectory = Path.Combine(Path.GetTempPath(), "content-pack-mac-layout");
            string dataPath = Path.Combine(
                playerDirectory,
                "Game.app",
                "Contents",
                "Resources",
                "Data"
            );

            string contentRoot = ContentPackLoader.ResolvePlayerContentRootPath(
                dataPath,
                RuntimePlatform.LinuxPlayer
            );

            Assert.AreEqual(Path.Combine(playerDirectory, "Content"), contentRoot);
        }

        [Test]
        public void GameEventSchema_AdjustOfficerStatWithPlanetSelector_RejectsDocument()
        {
            const string xml =
                @"
<GameEvents>
  <GameEvent>
    <InstanceID>EVENT</InstanceID>
    <Actions>
      <AdjustOfficerStat Stat=""Combat"">
        <Amount>1</Amount>
        <SelectPlanets InstanceID=""PLANET""/>
      </AdjustOfficerStat>
    </Actions>
  </GameEvent>
</GameEvents>";

            Assert.Throws<XmlSchemaValidationException>(() => ValidateGameEventsXml(xml));
        }

        [Test]
        public void GameEventSchema_AdjustPlanetStatWithOfficerSelector_RejectsDocument()
        {
            const string xml =
                @"
<GameEvents>
  <GameEvent>
    <InstanceID>EVENT</InstanceID>
    <Actions>
      <AdjustPlanetStat Stat=""EnergyCapacity"">
        <Amount>1</Amount>
        <SelectOfficers InstanceID=""OFFICER""/>
      </AdjustPlanetStat>
    </Actions>
  </GameEvent>
</GameEvents>";

            Assert.Throws<XmlSchemaValidationException>(() => ValidateGameEventsXml(xml));
        }

        private static void ValidateGameEventsXml(string xml)
        {
            string schemaPath = Path.Combine(
                Application.dataPath,
                "Content",
                "Application",
                "Schemas",
                "game-events.xsd"
            );
            XmlReaderSettings settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
            };
            settings.Schemas.Add(null, schemaPath);
            using StringReader stringReader = new StringReader(xml);
            using XmlReader reader = XmlReader.Create(stringReader, settings);
            while (reader.Read()) { }
        }
    }
}
