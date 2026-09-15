using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameEventTests
    {
        /// <summary>
        /// Verifies can activate maximum activations reached returns false.
        /// </summary>
        [Test]
        public void CanActivate_MaximumActivationsReached_ReturnsFalse()
        {
            GameEvent gameEvent = new GameEvent { MaximumActivations = 3 };
            GameEventState state = new GameEventState { ActivationCount = 3 };

            bool result = gameEvent.CanActivate(state);

            Assert.IsFalse(result);
        }

        /// <summary>
        /// Verifies can activate unlimited event returns true.
        /// </summary>
        [Test]
        public void CanActivate_UnlimitedEvent_ReturnsTrue()
        {
            GameEvent gameEvent = new GameEvent();
            GameEventState state = new GameEventState { ActivationCount = 100 };

            bool result = gameEvent.CanActivate(state);

            Assert.IsTrue(result);
        }

        /// <summary>
        /// Verifies conditionals authored aliases round trip concrete types.
        /// </summary>
        [Test]
        public void Conditionals_AuthoredAliases_RoundTripConcreteTypes()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "EVENT_TEST",
                Conditionals = new List<GameConditional>
                {
                    new TickCountConditional
                    {
                        InstanceID = "TICK_CONDITION",
                        Ticks = 30,
                        Comparison = ComparisonOperator.GreaterThan,
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            StringAssert.Contains("<TickCount Comparison=\"GreaterThan\" Ticks=\"30\">", xml);
            Assert.IsFalse(xml.Contains("<TickCountConditional"));
            TickCountConditional conditional =
                restored.Conditionals.Single() as TickCountConditional;
            Assert.IsNotNull(conditional);
            Assert.AreEqual(30, conditional.Ticks);
            Assert.AreEqual(ComparisonOperator.GreaterThan, conditional.Comparison);
        }

        /// <summary>
        /// Verifies is active authored node instance id round trips.
        /// </summary>
        [Test]
        public void IsActive_AuthoredNodeInstanceID_RoundTrips()
        {
            GameEvent gameEvent = new GameEvent
            {
                Conditionals = new List<GameConditional>
                {
                    new IsActiveConditional { NodeInstanceID = "DARTH_VADER" },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            StringAssert.Contains("<IsActive NodeInstanceID=\"DARTH_VADER\">", xml);
            IsActiveConditional conditional = restored.Conditionals.Single() as IsActiveConditional;
            Assert.IsNotNull(conditional);
            Assert.AreEqual("DARTH_VADER", conditional.NodeInstanceID);
        }

        /// <summary>
        /// Verifies composite conditionals round trip without collection wrappers.
        /// </summary>
        [Test]
        public void CompositeConditionals_RoundTripWithoutCollectionWrappers()
        {
            GameEvent gameEvent = new GameEvent
            {
                Conditionals = new List<GameConditional>
                {
                    new NotConditional
                    {
                        Conditionals = new List<GameConditional>
                        {
                            new AnyConditional
                            {
                                Conditionals = new List<GameConditional>
                                {
                                    new IsCapturedConditional
                                    {
                                        OfficerInstanceID = "LUKE_SKYWALKER",
                                    },
                                    new IsInTransitConditional
                                    {
                                        UnitInstanceID = "LUKE_SKYWALKER",
                                    },
                                },
                            },
                        },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            XElement root = XElement.Parse(xml);
            XElement notElement = root.Element("Conditionals")?.Element("Not");
            XElement anyElement = notElement?.Element("Any");
            Assert.IsNotNull(anyElement?.Element("IsCaptured"));
            Assert.IsNull(notElement?.Element("Conditionals"));
            Assert.IsNull(anyElement.Element("Conditionals"));
            NotConditional not = (NotConditional)restored.Conditionals.Single();
            AnyConditional any = (AnyConditional)not.Conditionals.Single();
            Assert.AreEqual(2, any.Conditionals.Count);
        }

        /// <summary>
        /// Verifies maximum activations authored value round trips attribute.
        /// </summary>
        [Test]
        public void MaximumActivations_AuthoredValue_RoundTripsAttribute()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "LIMITED_EVENT",
                MaximumActivations = 5,
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            Assert.AreEqual(5, restored.MaximumActivations);
        }
    }
}
