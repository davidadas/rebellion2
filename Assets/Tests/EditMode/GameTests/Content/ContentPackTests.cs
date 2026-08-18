using System.IO;
using System.Xml;
using System.Xml.Schema;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentPackTests
    {
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
        public void GameEventSchema_ChangeOfficerRatingWithPlanetSelector_RejectsDocument()
        {
            const string xml =
                @"
<GameEvents>
  <GameEvent>
    <InstanceID>EVENT</InstanceID>
    <Actions>
      <ChangeOfficerRating Rating=""Combat"">
        <Amount>1</Amount>
        <SelectPlanets InstanceID=""PLANET""/>
      </ChangeOfficerRating>
    </Actions>
  </GameEvent>
</GameEvents>";

            Assert.Throws<XmlSchemaValidationException>(() => ValidateGameEventsXml(xml));
        }

        [Test]
        public void GameEventSchema_ChangePlanetStatWithOfficerSelector_RejectsDocument()
        {
            const string xml =
                @"
<GameEvents>
  <GameEvent>
    <InstanceID>EVENT</InstanceID>
    <Actions>
      <ChangePlanetStat Stat=""EnergyCapacity"">
        <Amount>1</Amount>
        <SelectOfficers InstanceID=""OFFICER""/>
      </ChangePlanetStat>
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
