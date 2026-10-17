using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Generation;
using Rebellion.Util.Reflection;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public sealed class GameGenerationConfigCodecTests
    {
        [Test]
        public void Serialize_AuthoredConfig_RoundTripsNestedNumericValues()
        {
            GameGenerationConfig source = TestContent.Data.GenerationConfig;

            string xml = GameGenerationConfigCodec.Serialize(source);
            GameGenerationConfig restored = GameGenerationConfigCodec.Deserialize(xml);

            Assert.AreEqual(
                source.GalaxyClassification.FactionSetups[0].NumStartingOfficers.Large,
                restored.GalaxyClassification.FactionSetups[0].NumStartingOfficers.Large
            );
            Assert.AreEqual(
                source.GalaxyClassification.FactionSetups[0].StartingResearchLevel,
                restored.GalaxyClassification.FactionSetups[0].StartingResearchLevel
            );
            Assert.AreEqual(
                source.PlanetResources.Profiles[1].CoreEnergy.Random1,
                restored.PlanetResources.Profiles[1].CoreEnergy.Random1
            );
            Assert.AreEqual(
                source.UnitDeployment.FactionBudgets[0].UnitTable[0].Units[0].Count,
                restored.UnitDeployment.FactionBudgets[0].UnitTable[0].Units[0].Count
            );
        }

        [Test]
        public void Deserialize_MissingXml_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                GameGenerationConfigCodec.Deserialize(null)
            );
        }

        [Test]
        public void Serialize_FactionModifier_RoundTripsNestedSettings()
        {
            GameGenerationConfig source = TestContent.Data.GenerationConfig.GetDeepCopy();
            source.FactionModifiers.Add(
                new FactionModifierConfig
                {
                    FactionID = "CUSTOM",
                    Settings = new FactionSettings { GarrisonEfficiency = 3 },
                }
            );

            string xml = GameGenerationConfigCodec.Serialize(source);
            GameGenerationConfig restored = GameGenerationConfigCodec.Deserialize(xml);

            Assert.AreEqual("CUSTOM", restored.FactionModifiers[0].FactionID);
            Assert.AreEqual(3, restored.FactionModifiers[0].Settings.GarrisonEfficiency);
        }
    }
}
