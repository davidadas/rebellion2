using System;
using System.IO;
using System.Xml.Schema;
using NUnit.Framework;
using Rebellion.Game;
using UnityEngine;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentPackLoaderTests
    {
        private const string _fixtureSchemaXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<xs:schema xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
  <xs:element name=""GameConfig"">
    <xs:complexType>
      <xs:all>
        <xs:element name=""Movement"">
          <xs:complexType>
            <xs:all>
              <xs:element name=""DistanceDivisor"" type=""xs:positiveInteger""/>
            </xs:all>
          </xs:complexType>
        </xs:element>
        <xs:element name=""Research"">
          <xs:complexType>
            <xs:all>
              <xs:element name=""BaseResearchPoints"" type=""xs:integer""/>
            </xs:all>
          </xs:complexType>
        </xs:element>
        <xs:element name=""DifficultyModifiers"" minOccurs=""0"">
          <xs:complexType>
            <xs:sequence>
              <xs:element name=""Entry"" minOccurs=""0"" maxOccurs=""unbounded"">
                <xs:complexType>
                  <xs:sequence>
                    <xs:element name=""Key"" type=""xs:string""/>
                    <xs:element name=""Value"">
                      <xs:complexType>
                        <xs:sequence>
                          <xs:element name=""GameModifier"">
                            <xs:complexType>
                              <xs:all>
                                <xs:element name=""MissionExecutionSpeedIncreasePercent"" type=""xs:nonNegativeInteger""/>
                                <xs:element name=""DetectionRatingMultiplier"" type=""xs:decimal""/>
                                <xs:element name=""MaintenanceCapacityPercent"" type=""xs:nonNegativeInteger""/>
                              </xs:all>
                            </xs:complexType>
                          </xs:element>
                        </xs:sequence>
                      </xs:complexType>
                    </xs:element>
                  </xs:sequence>
                </xs:complexType>
              </xs:element>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
      </xs:all>
    </xs:complexType>
  </xs:element>
</xs:schema>";
        private const string _fixtureDefaultsXml =
            "<GameConfig><Movement><DistanceDivisor>5</DistanceDivisor></Movement></GameConfig>";
        private const string _fixtureCompleteDefaultsXml =
            "<GameConfig><Movement><DistanceDivisor>5</DistanceDivisor></Movement>"
            + "<Research><BaseResearchPoints>1</BaseResearchPoints></Research></GameConfig>";
        private const string _fixtureDifficultyDefaultsXml =
            "<GameConfig><Movement><DistanceDivisor>5</DistanceDivisor></Movement>"
            + "<Research><BaseResearchPoints>1</BaseResearchPoints></Research>"
            + "<DifficultyModifiers><Entry><Key>Hard</Key><Value><GameModifier>"
            + "<MissionExecutionSpeedIncreasePercent>37</MissionExecutionSpeedIncreasePercent>"
            + "<DetectionRatingMultiplier>1.375</DetectionRatingMultiplier>"
            + "<MaintenanceCapacityPercent>150</MaintenanceCapacityPercent>"
            + "</GameModifier></Value></Entry></DifficultyModifiers></GameConfig>";

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
        public void LoadGameConfig_NoPackOverridePath_UsesApplicationDefaults()
        {
            GameConfig config = LoadGameConfigFromFixture(
                _fixtureCompleteDefaultsXml,
                packOverrideXml: null
            );

            Assert.AreEqual(5, config.Movement.DistanceDivisor);
            Assert.AreEqual(1, config.Research.BaseResearchPoints);
        }

        [Test]
        public void LoadGameConfig_PackOverrideLeaf_ReplacesDefaultValue()
        {
            GameConfig config = LoadGameConfigFromFixture(
                _fixtureCompleteDefaultsXml,
                "<GameConfig><Movement><DistanceDivisor>7</DistanceDivisor></Movement></GameConfig>"
            );

            Assert.AreEqual(7, config.Movement.DistanceDivisor);
            Assert.AreEqual(1, config.Research.BaseResearchPoints);
        }

        [Test]
        public void LoadGameConfig_PackSuppliesSectionMissingFromDefaults_MergesIntoDefaults()
        {
            GameConfig config = LoadGameConfigFromFixture(
                _fixtureDefaultsXml,
                "<GameConfig><Research><BaseResearchPoints>3</BaseResearchPoints></Research></GameConfig>"
            );

            Assert.AreEqual(5, config.Movement.DistanceDivisor);
            Assert.AreEqual(3, config.Research.BaseResearchPoints);
        }

        [Test]
        public void LoadGameConfig_MergedDocumentMissingRequiredElement_RejectsDocument()
        {
            Assert.Throws<XmlSchemaValidationException>(() =>
                LoadGameConfigFromFixture(_fixtureDefaultsXml, packOverrideXml: null)
            );
        }

        [Test]
        public void LoadGameConfig_UnknownOverrideElement_RejectsDocument()
        {
            Assert.Throws<XmlSchemaValidationException>(() =>
                LoadGameConfigFromFixture(
                    _fixtureCompleteDefaultsXml,
                    "<GameConfig><Bogus>1</Bogus></GameConfig>"
                )
            );
        }

        [Test]
        public void LoadGameConfig_FixtureMissionExecutionSpeedIncrease_DeserializesValue()
        {
            GameConfig config = LoadGameConfigFromFixture(
                _fixtureDifficultyDefaultsXml,
                packOverrideXml: null
            );

            Assert.AreEqual(
                37,
                config.DifficultyModifiers[GameDifficulty.Hard].MissionExecutionSpeedIncreasePercent
            );
        }

        [Test]
        public void LoadGameConfig_FixtureDetectionRatingMultiplier_DeserializesValue()
        {
            GameConfig config = LoadGameConfigFromFixture(
                _fixtureDifficultyDefaultsXml,
                packOverrideXml: null
            );

            Assert.AreEqual(
                1.375,
                config.DifficultyModifiers[GameDifficulty.Hard].DetectionRatingMultiplier
            );
        }

        [Test]
        public void LoadGameConfig_FixtureMaintenanceCapacityPercent_DeserializesValue()
        {
            GameConfig config = LoadGameConfigFromFixture(
                _fixtureDifficultyDefaultsXml,
                packOverrideXml: null
            );

            Assert.AreEqual(
                150,
                config.DifficultyModifiers[GameDifficulty.Hard].MaintenanceCapacityPercent
            );
        }

        /// <summary>
        /// Loads game config from fixture.
        /// </summary>
        /// <param name="applicationDefaultsXml">The application defaults xml.</param>
        /// <param name="packOverrideXml">The pack override xml.</param>
        /// <returns>The loaded game config from fixture.</returns>
        private static GameConfig LoadGameConfigFromFixture(
            string applicationDefaultsXml,
            string packOverrideXml
        )
        {
            string contentRoot = Path.Combine(
                Path.GetTempPath(),
                "content-pack-loader-config-" + Guid.NewGuid().ToString("N")
            );
            try
            {
                string rulesRoot = Path.Combine(contentRoot, "Application", "Rules");
                string schemasRoot = Path.Combine(contentRoot, "Application", "Schemas");
                string packRoot = Path.Combine(contentRoot, "Packs", "Fixture");
                Directory.CreateDirectory(rulesRoot);
                Directory.CreateDirectory(schemasRoot);
                Directory.CreateDirectory(Path.Combine(packRoot, "Rules"));
                File.WriteAllText(Path.Combine(rulesRoot, "game.xml"), applicationDefaultsXml);
                File.WriteAllText(Path.Combine(schemasRoot, "game-config.xsd"), _fixtureSchemaXml);
                string packOverridePath = null;
                if (packOverrideXml != null)
                {
                    packOverridePath = "Rules/game.xml";
                    File.WriteAllText(Path.Combine(packRoot, "Rules", "game.xml"), packOverrideXml);
                }

                return ContentPackLoader.LoadGameConfig(contentRoot, packRoot, packOverridePath);
            }
            finally
            {
                if (Directory.Exists(contentRoot))
                    Directory.Delete(contentRoot, true);
            }
        }
    }
}
