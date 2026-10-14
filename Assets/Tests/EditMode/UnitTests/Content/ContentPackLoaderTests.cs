using System;
using System.IO;
using System.Linq;
using System.Xml.Schema;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Units;
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
              <xs:element name=""DistanceDivisor"" type=""xs:decimal""/>
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

        [Test]
        public void OpenActive_DetectionBlockingFacilities_LoadExpectedFlags()
        {
            Building ionCannon = TestContent.Pack.GameData.Buildings.Single(building =>
                building.TypeID == "BDDF03"
            );
            Building[] turbolasers = TestContent
                .Pack.GameData.Buildings.Where(building => building.TypeID is "BDDF01" or "BDDF05")
                .ToArray();

            Assert.IsTrue(ionCannon.IsDetectionBlocker);
            Assert.AreEqual(2, turbolasers.Length);
            Assert.IsTrue(turbolasers.All(building => !building.IsDetectionBlocker));
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
        public void GetDefaultPackID_CatalogSelection_ReturnsConfiguredPack()
        {
            string contentRoot = Path.Combine(
                Path.GetTempPath(),
                "content-catalog-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(contentRoot);
            File.WriteAllText(
                Path.Combine(contentRoot, "catalog.xml"),
                "<ContentCatalog><ActivePackID>default-pack</ActivePackID>"
                    + "<ActiveScenarioID>standard</ActiveScenarioID></ContentCatalog>"
            );
            try
            {
                Assert.AreEqual("default-pack", ContentPackLoader.GetDefaultPackID(contentRoot));
            }
            finally
            {
                Directory.Delete(contentRoot, true);
            }
        }

        [Test]
        public void DiscoverModResolver_DerivedPackMissingFile_UsesBasePackFile()
        {
            string contentRoot = CreateDerivedPackFixture(
                out string basePackRoot,
                out string derivedPackRoot
            );
            try
            {
                string inheritedFile = WriteFile(basePackRoot, "Data/ships.xml", "base");

                ContentFileResolver resolver = ContentPackLoader.DiscoverModResolver(
                    contentRoot,
                    "derived-pack",
                    Array.Empty<string>()
                );

                Assert.AreEqual(inheritedFile, resolver.ResolveFile("Pack/Data/ships.xml"));
                Assert.AreEqual(derivedPackRoot, resolver.PackRootPath);
            }
            finally
            {
                Directory.Delete(contentRoot, true);
            }
        }

        [Test]
        public void DiscoverModResolver_PackInheritanceCycle_ThrowsInvalidDataException()
        {
            string contentRoot = Path.Combine(
                Path.GetTempPath(),
                "content-pack-inheritance-" + Guid.NewGuid().ToString("N")
            );
            WritePackDefinition(contentRoot, "First", "first-pack", "second-pack");
            WritePackDefinition(contentRoot, "Second", "second-pack", "first-pack");
            try
            {
                Assert.Throws<InvalidDataException>(() =>
                    ContentPackLoader.DiscoverModResolver(
                        contentRoot,
                        "first-pack",
                        Array.Empty<string>()
                    )
                );
            }
            finally
            {
                Directory.Delete(contentRoot, true);
            }
        }

        [Test]
        public void DiscoverModResolver_BasePackVersionMismatch_ThrowsInvalidDataException()
        {
            string contentRoot = Path.Combine(
                Path.GetTempPath(),
                "content-pack-version-" + Guid.NewGuid().ToString("N")
            );
            WritePackDefinition(contentRoot, "Base", "base-pack", null);
            WritePackDefinition(
                contentRoot,
                "Derived",
                "derived-pack",
                "base-pack",
                basePackVersion: "2.0.0"
            );
            try
            {
                Assert.Throws<InvalidDataException>(() =>
                    ContentPackLoader.DiscoverModResolver(
                        contentRoot,
                        "derived-pack",
                        Array.Empty<string>()
                    )
                );
            }
            finally
            {
                Directory.Delete(contentRoot, true);
            }
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
                "<GameConfig><Movement><DistanceDivisor>2.5</DistanceDivisor></Movement></GameConfig>"
            );

            Assert.AreEqual(2.5, config.Movement.DistanceDivisor);
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

        /// <summary>
        /// Creates a content root containing one base pack and one derived pack.
        /// </summary>
        /// <param name="basePackRoot">Receives the base pack root.</param>
        /// <param name="derivedPackRoot">Receives the derived pack root.</param>
        /// <returns>The temporary content root.</returns>
        private static string CreateDerivedPackFixture(
            out string basePackRoot,
            out string derivedPackRoot
        )
        {
            string contentRoot = Path.Combine(
                Path.GetTempPath(),
                "content-pack-inheritance-" + Guid.NewGuid().ToString("N")
            );
            basePackRoot = WritePackDefinition(contentRoot, "Base", "base-pack", null);
            derivedPackRoot = WritePackDefinition(
                contentRoot,
                "Derived",
                "derived-pack",
                "base-pack"
            );
            return contentRoot;
        }

        /// <summary>
        /// Writes a minimal content-pack definition beneath a temporary content root.
        /// </summary>
        /// <param name="contentRoot">The temporary content root.</param>
        /// <param name="directoryName">The pack directory name.</param>
        /// <param name="packID">The declared pack identifier.</param>
        /// <param name="basePackID">The optional inherited pack identifier.</param>
        /// <param name="version">The declared pack version.</param>
        /// <param name="basePackVersion">The required inherited pack version.</param>
        /// <returns>The created pack root.</returns>
        private static string WritePackDefinition(
            string contentRoot,
            string directoryName,
            string packID,
            string basePackID,
            string version = "1.0.0",
            string basePackVersion = "1.0.0"
        )
        {
            string packRoot = Path.Combine(contentRoot, "Packs", directoryName);
            Directory.CreateDirectory(packRoot);
            string baseElement = string.IsNullOrWhiteSpace(basePackID)
                ? string.Empty
                : $"<BasePackID>{basePackID}</BasePackID>"
                    + $"<BasePackVersion>{basePackVersion}</BasePackVersion>";
            File.WriteAllText(
                Path.Combine(packRoot, "pack.xml"),
                $"<ContentPack><ID>{packID}</ID>{baseElement}<Version>{version}</Version></ContentPack>"
            );
            return packRoot;
        }

        /// <summary>
        /// Writes a file beneath a temporary pack root.
        /// </summary>
        /// <param name="packRoot">The pack root.</param>
        /// <param name="relativePath">The pack-relative file path.</param>
        /// <param name="contents">The file contents.</param>
        /// <returns>The absolute file path.</returns>
        private static string WriteFile(string packRoot, string relativePath, string contents)
        {
            string path = Path.Combine(
                packRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)
            );
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
            return path;
        }
    }
}
