using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Results
{
    [TestFixture]
    public class CombatUnitSnapshotTests
    {
        [Test]
        public void Constructor_DestroyedBuilding_MarksBuildingNonOperational()
        {
            Building rubble = new Building
            {
                IsDestroyed = true,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            CombatUnitSnapshot snapshot = new CombatUnitSnapshot(rubble);

            Assert.IsFalse(snapshot.WasOperational);
        }
    }
}
