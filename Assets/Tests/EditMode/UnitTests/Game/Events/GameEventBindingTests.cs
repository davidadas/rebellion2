using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Events;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameEventBindingTests
    {
        [Test]
        public void RoundTrip_NumericRanges_RestoresConcreteRolls()
        {
            GameEvent gameEvent = new GameEvent
            {
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "count",
                        RollInteger = new RollInteger { Minimum = 1, Maximum = 5 },
                    },
                    new GameEventBinding
                    {
                        As = "probability",
                        RollDouble = new RollDouble { Minimum = 0.1, Maximum = 0.9 },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            StringAssert.Contains("<RollInteger Minimum=\"1\" Maximum=\"5\" />", xml);
            StringAssert.Contains("<RollDouble Minimum=\"0.1\" Maximum=\"0.9\" />", xml);
            Assert.AreEqual(1, restored.Bindings[0].RollInteger.Minimum);
            Assert.AreEqual(5, restored.Bindings[0].RollInteger.Maximum);
            Assert.AreEqual(0.1, restored.Bindings[1].RollDouble.Minimum);
            Assert.AreEqual(0.9, restored.Bindings[1].RollDouble.Maximum);
        }

        [Test]
        public void RoundTrip_TypedSources_RestoresConcreteSources()
        {
            GameEvent gameEvent = new GameEvent
            {
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding
                    {
                        As = "combat",
                        Sources = new List<GameEventBindingSource>
                        {
                            new SkillRatingBindingSource
                            {
                                OfficerInstanceID = "officer",
                                Rating = SkillRating.Combat,
                            },
                        },
                    },
                    new GameEventBinding
                    {
                        As = "unitCount",
                        Sources = new List<GameEventBindingSource>
                        {
                            new SelectionCountBindingSource
                            {
                                Selectors = new List<GameEventSelector>
                                {
                                    new SelectOfficers { OwnerFactionInstanceID = "faction" },
                                },
                            },
                        },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            StringAssert.Contains(
                "<SkillRating OfficerInstanceID=\"officer\" Rating=\"Combat\" />",
                xml
            );
            StringAssert.Contains("<SelectionCount>", xml);
            Assert.IsInstanceOf<SkillRatingBindingSource>(restored.Bindings[0].Sources[0]);
            Assert.IsInstanceOf<SelectionCountBindingSource>(restored.Bindings[1].Sources[0]);
        }
    }
}
