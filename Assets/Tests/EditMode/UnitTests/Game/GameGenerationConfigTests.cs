using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.GameEvents;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class GameGenerationConfigTests
    {
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
