using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.GameEvents;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class GameGenerationConfigTests
    {
        [TestCase(GameDifficulty.Easy)]
        [TestCase(GameDifficulty.Medium)]
        [TestCase(GameDifficulty.Hard)]
        public void GetStartingIntelligence_OmittedProfile_PreservesDefaultVisibility(
            GameDifficulty difficulty
        )
        {
            GameGenerationConfig config = SerializationHelper.Deserialize<GameGenerationConfig>(
                "<GameGenerationConfig/>"
            );

            StartingIntelligenceProfile profile = config.GetStartingIntelligence(difficulty);

            Assert.IsTrue(profile.RevealAgents);
            Assert.IsTrue(profile.RevealFleets);
            Assert.IsTrue(profile.RevealBuildings);
            Assert.IsFalse(profile.StartWithUnexploredPlanets);
        }

        [Test]
        public void CreateCopy_StartingIntelligence_PreservesIndependentSerializableProfiles()
        {
            GameGenerationConfig source = new()
            {
                StartingIntelligence = new List<StartingIntelligenceProfile>
                {
                    new()
                    {
                        Difficulty = GameDifficulty.Hard,
                        RevealAgents = false,
                        RevealFleets = false,
                        RevealBuildings = false,
                        StartWithUnexploredPlanets = true,
                    },
                },
            };
            string serialized = SerializationHelper.Serialize(source);
            GameGenerationConfig restored = SerializationHelper.Deserialize<GameGenerationConfig>(
                serialized
            );
            GameGenerationConfig copy = source.CreateCopy();

            Assert.AreEqual(serialized, SerializationHelper.Serialize(restored));
            Assert.AreEqual(serialized, SerializationHelper.Serialize(copy));
            copy.GetStartingIntelligence(GameDifficulty.Hard).RevealAgents = true;
            copy.StartingIntelligence.Clear();
            Assert.AreEqual(serialized, SerializationHelper.Serialize(source));
            Assert.IsTrue(source.GetStartingIntelligence(GameDifficulty.Medium).RevealAgents);
        }

        [Test]
        public void CreateCopy_ConfiguredGeneration_PreservesAllValues()
        {
            GameGenerationConfig source = TestPackFixture.CreateGenerationConfig();

            GameGenerationConfig copy = source.CreateCopy();

            Assert.AreEqual(
                SerializationHelper.Serialize(source),
                SerializationHelper.Serialize(copy)
            );
        }

        [Test]
        public void CreateCopy_EditingDeploymentCargo_DoesNotChangeSource()
        {
            GameGenerationConfig source = new GameGenerationConfig
            {
                UnitDeployment = new UnitDeploymentSection
                {
                    FixedFleets = new List<FixedFleet>
                    {
                        new FixedFleet
                        {
                            TargetPlanets = new List<string> { "planet" },
                            ShipEntries = new List<FixedFleetShip>
                            {
                                new FixedFleetShip
                                {
                                    TypeID = "ship",
                                    Cargo = new List<UnitEntry>
                                    {
                                        new UnitEntry { TypeID = "troop", Count = 2 },
                                    },
                                },
                            },
                        },
                    },
                },
            };
            string before = SerializationHelper.Serialize(source);
            GameGenerationConfig copy = source.CreateCopy();

            copy.UnitDeployment.FixedFleets[0].TargetPlanets.Clear();
            copy.UnitDeployment.FixedFleets[0].ShipEntries[0].Cargo[0].Count = 10;

            Assert.AreEqual(before, SerializationHelper.Serialize(source));
        }

        [Test]
        public void CreateCopy_EditingFactionOverrides_DoesNotChangeSource()
        {
            GameGenerationConfig source = TestPackFixture.CreateGenerationConfig();
            source.FactionModifiers.Add(new FactionModifierConfig { FactionID = "faction" });
            string before = SerializationHelper.Serialize(source);
            GameGenerationConfig copy = source.CreateCopy();

            copy.FactionModifiers[0].Settings.Headquarters.FacilityTypeID = "replacement";
            copy.DisabledEventCategories.Add(GameEventCategory.Story);
            copy.GalaxyClassification.FactionSetups[0].NumStartingOfficers.Small += 1;

            Assert.AreEqual(before, SerializationHelper.Serialize(source));
        }

        [Test]
        public void IsEventCategoryEnabled_NoDisabledCategories_EnablesEveryCategory()
        {
            GameGenerationConfig config = new GameGenerationConfig();

            Assert.IsTrue(config.IsEventCategoryEnabled(GameEventCategory.None));
            Assert.IsTrue(config.IsEventCategoryEnabled(GameEventCategory.Story));
            Assert.IsTrue(config.IsEventCategoryEnabled(GameEventCategory.Disaster));
        }

        [Test]
        public void SerializeAndDeserialize_DisabledEventCategories_PreservesSelections()
        {
            GameGenerationConfig config = new GameGenerationConfig
            {
                DisabledEventCategories = new List<GameEventCategory>
                {
                    GameEventCategory.Story,
                    GameEventCategory.Disaster,
                },
            };

            GameGenerationConfig restored = SerializationHelper.Deserialize<GameGenerationConfig>(
                SerializationHelper.Serialize(config)
            );

            CollectionAssert.AreEquivalent(
                config.DisabledEventCategories,
                restored.DisabledEventCategories
            );
            Assert.IsFalse(restored.IsEventCategoryEnabled(GameEventCategory.Story));
            Assert.IsFalse(restored.IsEventCategoryEnabled(GameEventCategory.Disaster));
            Assert.IsTrue(restored.IsEventCategoryEnabled(GameEventCategory.None));
        }

        [Test]
        public void SerializeAndDeserialize_OmittedEventSettings_EnablesEveryCategory()
        {
            GameGenerationConfig restored = SerializationHelper.Deserialize<GameGenerationConfig>(
                "<GameGenerationConfig/>"
            );

            Assert.IsTrue(restored.IsEventCategoryEnabled(GameEventCategory.Story));
            Assert.IsTrue(restored.IsEventCategoryEnabled(GameEventCategory.Disaster));
        }
    }
}
