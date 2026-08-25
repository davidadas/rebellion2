using System;
using System.IO;
using System.Xml;
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

        [Test]
        public void GameEventSchema_TriggerArgumentBinding_AcceptsDocument()
        {
            const string xml =
                @"
<GameEvents>
  <GameEvent>
    <InstanceID>EVENT</InstanceID>
    <Triggers>
      <ManufacturingCompleted>
        <Bindings>
          <Bind Argument=""DeployedObject"" As=""unit""/>
          <Bind Argument=""Location"" As=""location""/>
        </Bindings>
      </ManufacturingCompleted>
    </Triggers>
  </GameEvent>
</GameEvents>";

            Assert.DoesNotThrow(() => ValidateGameEventsXml(xml));
        }

        [Test]
        public void GameEventSchema_TriggerResultBinding_RejectsDocument()
        {
            const string xml =
                @"
<GameEvents>
  <GameEvent>
    <InstanceID>EVENT</InstanceID>
    <Triggers>
      <ManufacturingCompleted As=""production""/>
    </Triggers>
  </GameEvent>
</GameEvents>";

            Assert.Throws<XmlSchemaValidationException>(() => ValidateGameEventsXml(xml));
        }

        [Test]
        public void GameEventSchema_DottedBindingReference_RejectsDocument()
        {
            const string xml =
                @"
<GameEvents>
  <GameEvent>
    <InstanceID>EVENT</InstanceID>
    <Conditionals>
      <EvaluateBinding Binding=""$production.Tick"" Comparison=""Equal"" CompareTo=""1""/>
    </Conditionals>
  </GameEvent>
</GameEvents>";

            Assert.Throws<XmlSchemaValidationException>(() => ValidateGameEventsXml(xml));
        }

        [Test]
        public void GenerationConfigSchema_ValidStartingOfficer_AcceptsDocument()
        {
            const string officer =
                @"
      <StartingOfficerRule>
        <OfficerInstanceID>OFFICER</OfficerInstanceID>
        <GalaxySizes>
          <GameSize>Large</GameSize>
        </GalaxySizes>
        <DestinationTypeID>PLANET_TYPE</DestinationTypeID>
      </StartingOfficerRule>";

            Assert.DoesNotThrow(() => ValidateGenerationConfigXml(CreateGenerationXml(officer)));
        }

        [Test]
        public void GenerationConfigSchema_StartingOfficerWithoutInstanceID_RejectsDocument()
        {
            const string officer =
                @"
      <StartingOfficerRule>
        <DestinationTypeID>PLANET_TYPE</DestinationTypeID>
      </StartingOfficerRule>";

            Assert.Throws<XmlSchemaValidationException>(() =>
                ValidateGenerationConfigXml(CreateGenerationXml(officer))
            );
        }

        [Test]
        public void GenerationConfigSchema_StartingOfficerWithBlankInstanceID_RejectsDocument()
        {
            const string officer =
                @"
      <StartingOfficerRule>
        <OfficerInstanceID>   </OfficerInstanceID>
      </StartingOfficerRule>";

            Assert.Throws<XmlSchemaValidationException>(() =>
                ValidateGenerationConfigXml(CreateGenerationXml(officer))
            );
        }

        [Test]
        public void GenerationConfigSchema_StartingOfficerWithTwoDestinations_RejectsDocument()
        {
            const string officer =
                @"
      <StartingOfficerRule>
        <OfficerInstanceID>OFFICER</OfficerInstanceID>
        <DestinationTypeID>PLANET_TYPE</DestinationTypeID>
        <DestinationInstanceID>PLANET</DestinationInstanceID>
      </StartingOfficerRule>";

            Assert.Throws<XmlSchemaValidationException>(() =>
                ValidateGenerationConfigXml(CreateGenerationXml(officer))
            );
        }

        [Test]
        public void GenerationConfigSchema_DuplicateStartingOfficer_RejectsDocument()
        {
            const string officers =
                @"
      <StartingOfficerRule>
        <OfficerInstanceID>OFFICER</OfficerInstanceID>
      </StartingOfficerRule>
      <StartingOfficerRule>
        <OfficerInstanceID>OFFICER</OfficerInstanceID>
      </StartingOfficerRule>";

            Assert.Throws<XmlSchemaValidationException>(() =>
                ValidateGenerationConfigXml(CreateGenerationXml(officers))
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

        private static void ValidateGameEventsXml(string xml)
        {
            ValidateXml(xml, "game-events.xsd");
        }

        private static void ValidateGenerationConfigXml(string xml)
        {
            ValidateXml(xml, "generation-config.xsd");
        }

        private static void ValidateXml(string xml, string schemaFileName)
        {
            string schemaPath = Path.Combine(
                Application.dataPath,
                "Content",
                "Application",
                "Schemas",
                schemaFileName
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

        private static string CreateGenerationXml(string startingOfficerRules)
        {
            return $@"
<GameGenerationConfig>
  <Officers>
    <NumStartingOfficers>
      <Small>0</Small>
      <Medium>0</Medium>
      <Large>0</Large>
    </NumStartingOfficers>
    <StartingOfficers>{startingOfficerRules}
    </StartingOfficers>
  </Officers>
  <GalaxyClassification>
    <FactionSetups>
      <FactionSetup>
        <FactionID>FACTION</FactionID>
        <GarrisonTroopTypeID>REGIMENT_TYPE</GarrisonTroopTypeID>
        <StartingPlanets>
          <StartingPlanet>
            <PlanetTypeID>PLANET_TYPE</PlanetTypeID>
            <IsHeadquarters>true</IsHeadquarters>
            <Loyalty>100</Loyalty>
            <PickFromRim>false</PickFromRim>
          </StartingPlanet>
        </StartingPlanets>
      </FactionSetup>
    </FactionSetups>
    <Profiles>
      <DifficultyProfile>
        <Name>Default</Name>
        <Difficulty>-1</Difficulty>
        <FactionBuckets>
          <FactionBucketConfig>
            <FactionID>FACTION</FactionID>
            <StrongPct>0</StrongPct>
            <WeakPct>0</WeakPct>
          </FactionBucketConfig>
        </FactionBuckets>
      </DifficultyProfile>
    </Profiles>
  </GalaxyClassification>
  <PlanetResources>
    <Profiles>
      <PlanetResourceProfile>
        <Availability>Normal</Availability>
        <CoreEnergy><Base>0</Base><Random1>0</Random1><Random2>0</Random2></CoreEnergy>
        <RimEnergy><Base>0</Base><Random1>0</Random1><Random2>0</Random2></RimEnergy>
        <CoreRawMaterials><Base>0</Base><Random1>0</Random1><Random2>0</Random2></CoreRawMaterials>
        <RimRawMaterials><Base>0</Base><Random1>0</Random1><Random2>0</Random2></RimRawMaterials>
        <EnergyMin>0</EnergyMin>
        <EnergyMax>0</EnergyMax>
        <RawMaterialsMin>0</RawMaterialsMin>
        <RawMaterialsMax>0</RawMaterialsMax>
        <RimColonizationPct>0</RimColonizationPct>
      </PlanetResourceProfile>
    </Profiles>
  </PlanetResources>
  <PlanetSupport>
    <Strong><Base>0</Base><Random>0</Random></Strong>
    <Weak><Base>0</Base><Random>0</Random></Weak>
    <Neutral><Base>0</Base><Random>0</Random></Neutral>
    <RimSupportRandom>0</RimSupportRandom>
  </PlanetSupport>
  <FacilityGeneration>
    <CoreMineMultiplier>0</CoreMineMultiplier>
    <RimMineMultiplier>0</RimMineMultiplier>
    <MineTypeID>MINE_TYPE</MineTypeID>
    <FacilityTableRollMin>0</FacilityTableRollMin>
    <FacilityTableRollMaxExclusive>1</FacilityTableRollMaxExclusive>
    <CoreFacilityTable><WeightedFacilityEntry><CumulativeWeight>0</CumulativeWeight></WeightedFacilityEntry></CoreFacilityTable>
    <RimFacilityTable><WeightedFacilityEntry><CumulativeWeight>0</CumulativeWeight></WeightedFacilityEntry></RimFacilityTable>
    <HQLoadouts>
      <HQFacilityLoadout>
        <PlanetTypeID>PLANET_TYPE</PlanetTypeID>
        <FacilityTypeIDs><string>FACILITY_TYPE</string></FacilityTypeIDs>
      </HQFacilityLoadout>
    </HQLoadouts>
  </FacilityGeneration>
  <UnitDeployment>
    <UprisingPreventionThreshold>0</UprisingPreventionThreshold>
    <SupportDeficitPerGarrisonTroop>1</SupportDeficitPerGarrisonTroop>
    <BudgetDifficultyMappings/>
    <FixedGarrisons/>
    <FixedFleets/>
    <FactionBudgets>
      <FactionBudget>
        <FactionID>FACTION</FactionID>
        <BudgetLevels>
          <BudgetLevel>
            <GalaxySize>0</GalaxySize>
            <Difficulty>-1</Difficulty>
            <Percentage>0</Percentage>
          </BudgetLevel>
        </BudgetLevels>
        <UnitTable>
          <WeightedUnitEntry>
            <CumulativeWeight>0</CumulativeWeight>
            <Units><UnitEntry><TypeID>UNIT_TYPE</TypeID><Count>1</Count></UnitEntry></Units>
          </WeightedUnitEntry>
        </UnitTable>
      </FactionBudget>
    </FactionBudgets>
  </UnitDeployment>
  <Balance>
    <SupportBoostPerUnit>0</SupportBoostPerUnit>
    <MaxMilitaryPresenceBoost>0</MaxMilitaryPresenceBoost>
  </Balance>
</GameGenerationConfig>";
        }
    }
}
