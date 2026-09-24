using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventTriggerTests
    {
        [Test]
        public void Triggers_AuthoredContracts_RoundTripConcreteTypesAndBindings()
        {
            GameEvent gameEvent = new GameEvent
            {
                Triggers = new List<GameEventTrigger>
                {
                    new PlanetOwnershipChangedTrigger(),
                    new IntelligenceRevealedTrigger(),
                    new MaintenanceRequiredTrigger(),
                    new ResearchAdvancedTrigger(),
                    new MissionStartedTrigger(),
                    new MissionCompletedTrigger(),
                    new OfficerCaptureChangedTrigger(),
                    new ForceDiscoveryChangedTrigger(),
                    new UnitOwnershipChangedTrigger(),
                    new UnitDestroyedTrigger(),
                    new SpaceCombatCompletedTrigger(),
                    new ManufacturingCompletedTrigger
                    {
                        Bindings = new List<GameEventBinding>
                        {
                            new GameEventBinding { Argument = "DeployedObject", As = "unit" },
                        },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            CollectionAssert.AreEqual(
                gameEvent.Triggers.Select(trigger => trigger.GetType()),
                restored.Triggers.Select(trigger => trigger.GetType())
            );
            GameEventBinding binding = restored.Triggers.Last().Bindings.Single();
            Assert.AreEqual("DeployedObject", binding.Argument);
            Assert.AreEqual("unit", binding.As);
            Assert.IsFalse(xml.Contains("Trigger>"));
        }
    }
}
