using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public sealed class OfficerTraitDefinitionTests
    {
        [Test]
        public void Serialize_SharedTriggerAndAction_RoundTripsDefinition()
        {
            OfficerTraitDefinition trait = new()
            {
                ID = "fixture-trait",
                Triggers = new List<GameTrigger>
                {
                    new UnitArrivedTrigger { UnitInstanceID = "fixture-officer" },
                },
                Actions = new List<GameAction>
                {
                    new ReserveOfficerAction { OfficerInstanceID = "fixture-officer" },
                },
            };

            OfficerTraitDefinition restored =
                SerializationHelper.Deserialize<OfficerTraitDefinition>(
                    SerializationHelper.Serialize(trait)
                );

            Assert.AreEqual(trait.ID, restored.ID);
            Assert.AreEqual(
                "fixture-officer",
                ((UnitArrivedTrigger)restored.Triggers.Single()).UnitInstanceID
            );
            Assert.AreEqual(
                "fixture-officer",
                ((ReserveOfficerAction)restored.Actions.Single()).OfficerInstanceID
            );
        }
    }
}
