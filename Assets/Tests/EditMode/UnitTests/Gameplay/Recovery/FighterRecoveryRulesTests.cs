using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class FighterRecoveryRulesTests
    {
        [Test]
        public void EstimateCompletionTick_ScheduledLosses_ReturnsFinalPulseDay()
        {
            Starfighter squadron = new Starfighter
            {
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 8,
                NextReplacementTick = 102,
                ReplacementIntervalTicks = 2,
            };

            long? completion = FighterRecoveryRules.EstimateCompletionTick(squadron, 100);

            Assert.AreEqual(108, completion);
        }

        [Test]
        public void EstimateCompletionTick_CompleteSquadron_ReturnsNull()
        {
            Starfighter squadron = new Starfighter
            {
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                NextReplacementTick = 102,
                ReplacementIntervalTicks = 2,
            };

            long? completion = FighterRecoveryRules.EstimateCompletionTick(squadron, 100);

            Assert.IsNull(completion);
        }
    }
}
