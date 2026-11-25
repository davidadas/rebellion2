using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class ShipRecoveryRulesTests
    {
        [Test]
        public void EstimateCompletionTick_ScheduledHullDamage_ReturnsFinalPulseDay()
        {
            CapitalShip ship = new CapitalShip
            {
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = 360,
                CurrentHullStrength = 300,
                NextRepairTick = 102,
                RepairIntervalTicks = 2,
            };

            long? completion = ShipRecoveryRules.EstimateCompletionTick(ship, 100);

            Assert.AreEqual(104, completion);
        }

        [Test]
        public void EstimateCompletionTick_EnrouteShip_ReturnsNull()
        {
            CapitalShip ship = new CapitalShip
            {
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = 360,
                CurrentHullStrength = 300,
                NextRepairTick = 102,
                RepairIntervalTicks = 2,
                Movement = TestMovement.Create(transitTicks: 10, ticksElapsed: 2),
            };

            long? completion = ShipRecoveryRules.EstimateCompletionTick(ship, 100);

            Assert.IsNull(completion);
        }
    }
}
