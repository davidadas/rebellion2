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
              <xs:element name=""DistanceScale"" type=""xs:decimal""/>
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
      </xs:all>
    </xs:complexType>
  </xs:element>
</xs:schema>";
        private const string _fixtureDefaultsXml =
            "<GameConfig><Movement><DistanceScale>12</DistanceScale></Movement></GameConfig>";
        private const string _fixtureCompleteDefaultsXml =
            "<GameConfig><Movement><DistanceScale>12</DistanceScale></Movement>"
            + "<Research><BaseResearchPoints>1</BaseResearchPoints></Research></GameConfig>";

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

            Assert.AreEqual(12, config.Movement.DistanceScale);
            Assert.AreEqual(1, config.Research.BaseResearchPoints);
        }

        [Test]
        public void LoadGameConfig_PackOverrideLeaf_ReplacesDefaultValue()
        {
            GameConfig config = LoadGameConfigFromFixture(
                _fixtureCompleteDefaultsXml,
                "<GameConfig><Movement><DistanceScale>7</DistanceScale></Movement></GameConfig>"
            );

            Assert.AreEqual(7, config.Movement.DistanceScale);
            Assert.AreEqual(1, config.Research.BaseResearchPoints);
        }

        [Test]
        public void LoadGameConfig_PackSuppliesSectionMissingFromDefaults_MergesIntoDefaults()
        {
            GameConfig config = LoadGameConfigFromFixture(
                _fixtureDefaultsXml,
                "<GameConfig><Research><BaseResearchPoints>3</BaseResearchPoints></Research></GameConfig>"
            );

            Assert.AreEqual(12, config.Movement.DistanceScale);
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
