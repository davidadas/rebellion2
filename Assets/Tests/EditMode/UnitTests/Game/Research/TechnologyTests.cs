using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Research;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Research
{
    [TestFixture]
    public sealed class TechnologyTests
    {
        /// <summary>
        /// Verifies reference copy creates an independent, unidentified manufacturing item.
        /// </summary>
        [Test]
        public void GetReferenceCopy_StarfighterTemplate_CreatesIndependentManufacturingItem()
        {
            Starfighter template = new Starfighter
            {
                InstanceID = "template",
                TypeID = "fighter",
                OwnerInstanceID = "owner",
                ManufacturingStatus = ManufacturingStatus.Complete,
                ManufacturingProgress = 12,
                ManufacturingFactionInstanceIDs = new List<string> { "owner" },
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                LaserCannon = 7,
            };
            Technology technology = new Technology(template);

            Starfighter copy = (Starfighter)technology.GetReferenceCopy();

            Assert.AreNotSame(template, copy);
            Assert.AreNotEqual(template.InstanceID, copy.InstanceID);
            Assert.IsNull(copy.OwnerInstanceID);
            Assert.IsNull(copy.GetParent());
            Assert.IsNull(copy.Movement);
            Assert.AreEqual(ManufacturingStatus.Building, copy.ManufacturingStatus);
            Assert.AreEqual(12, copy.ManufacturingProgress);
            Assert.AreEqual(template.TypeID, copy.TypeID);
            Assert.AreEqual(template.MaxSquadronSize, copy.MaxSquadronSize);
            Assert.AreEqual(template.LaserCannon, copy.LaserCannon);
            CollectionAssert.AreEqual(
                template.ManufacturingFactionInstanceIDs,
                copy.ManufacturingFactionInstanceIDs
            );
            Assert.AreNotSame(
                template.ManufacturingFactionInstanceIDs,
                copy.ManufacturingFactionInstanceIDs
            );
        }
    }
}
