using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Factions;

namespace Rebellion.Tests.Game.Factions
{
    [TestFixture]
    public class FactionSettingsTests
    {
        [Test]
        public void CreateCopy_VictoryRules_CopiesValuesWithoutSharingOfficerList()
        {
            FactionSettings settings = new FactionSettings
            {
                ConquestOfficerInstanceIDs = new List<string> { "leader" },
                SimultaneousVictoryPriority = 4,
            };

            FactionSettings copy = settings.CreateCopy();
            copy.ConquestOfficerInstanceIDs.Add("second-leader");

            CollectionAssert.AreEqual(new[] { "leader" }, settings.ConquestOfficerInstanceIDs);
            CollectionAssert.AreEqual(
                new[] { "leader", "second-leader" },
                copy.ConquestOfficerInstanceIDs
            );
            Assert.AreEqual(4, copy.SimultaneousVictoryPriority);
        }
    }
}
