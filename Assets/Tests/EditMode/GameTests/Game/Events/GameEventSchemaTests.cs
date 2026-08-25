using System.IO;
using System.Xml;
using System.Xml.Schema;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameEventSchemaTests
    {
        [Test]
        public void Validate_ChangeEnergyCapacityWithOfficerSelector_RejectsDocument()
        {
            const string xml =
                @"
<GameEvents>
  <GameEvent>
    <InstanceID>EVENT</InstanceID>
    <Actions>
      <ChangeEnergyCapacity>
        <Amount>1</Amount>
        <SelectOfficers InstanceID=""OFFICER""/>
      </ChangeEnergyCapacity>
    </Actions>
  </GameEvent>
</GameEvents>";

            Assert.Throws<XmlSchemaValidationException>(() => Validate(xml));
        }

        [Test]
        public void Validate_ChangeOfficerRatingWithPlanetSelector_RejectsDocument()
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

            Assert.Throws<XmlSchemaValidationException>(() => Validate(xml));
        }

        [Test]
        public void Validate_DottedBindingReference_RejectsDocument()
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

            Assert.Throws<XmlSchemaValidationException>(() => Validate(xml));
        }

        [Test]
        public void Validate_RandomAndSupportActions_AcceptsDocument()
        {
            const string xml =
                @"
<GameEvents>
  <GameEvent>
    <InstanceID>EVENT</InstanceID>
    <Bindings>
      <Bind As=""damage""><RollInteger Minimum=""1"" Maximum=""5""/></Bind>
      <Bind As=""chance""><RollDouble Minimum=""0.1"" Maximum=""0.9""/></Bind>
    </Bindings>
    <Schedule><At Tick=""1""/></Schedule>
    <Actions>
      <RollChance ProbabilityBinding=""$chance"">
        <Actions>
          <ChangeRawResourceNodes PlanetInstanceID=""NABOO"">
            <AmountBinding>$damage</AmountBinding>
          </ChangeRawResourceNodes>
          <SetPopularSupport PlanetInstanceID=""NABOO"" FactionInstanceID=""FNALL1"">
            <Support>20</Support>
          </SetPopularSupport>
          <ChangePopularSupport PlanetInstanceID=""NABOO"" FactionInstanceID=""FNEMP1"">
            <RollInteger Minimum=""-5"" Maximum=""-1""/>
          </ChangePopularSupport>
        </Actions>
      </RollChance>
      <RollOutcome>
        <Outcomes>
          <Outcome Weight=""3""><Actions/></Outcome>
          <Outcome Weight=""1""><Actions/></Outcome>
        </Outcomes>
      </RollOutcome>
    </Actions>
  </GameEvent>
</GameEvents>";

            Assert.DoesNotThrow(() => Validate(xml));
        }

        [Test]
        public void Validate_TriggerArgumentBinding_AcceptsDocument()
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

            Assert.DoesNotThrow(() => Validate(xml));
        }

        [Test]
        public void Validate_TriggerResultBinding_RejectsDocument()
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

            Assert.Throws<XmlSchemaValidationException>(() => Validate(xml));
        }

        private static void Validate(string xml)
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
