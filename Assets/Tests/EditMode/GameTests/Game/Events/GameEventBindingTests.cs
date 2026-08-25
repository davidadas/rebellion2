using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameEventBindingTests
    {
        [Test]
        public void Bind_NumericRanges_StoresRolledValues()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            GameEvent gameEvent = new GameEvent();
            GameEventEvaluationContext context = new GameEventEvaluationContext(gameEvent, null);
            GameEventBinding integerBinding = new GameEventBinding
            {
                As = "count",
                RollInteger = new RollInteger { Minimum = 1, Maximum = 5 },
            };
            GameEventBinding doubleBinding = new GameEventBinding
            {
                As = "probability",
                RollDouble = new RollDouble { Minimum = 0.1, Maximum = 0.9 },
            };
            IRandomNumberProvider random = new FixedRandomProvider(new[] { 0.5, 0.5 });

            integerBinding.Bind(game, random, context);
            doubleBinding.Bind(game, random, context);

            Assert.AreEqual(3, context.GetBinding<int>("count"));
            Assert.AreEqual(0.5, context.GetBinding<double>("probability"), 0.0001);
        }

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
    }
}
