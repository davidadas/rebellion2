using NUnit.Framework;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class AIWorkspaceTests
    {
        /// <summary>
        /// NeedsUnitDeployment is true when FlagA bits 0x1 and 0x2 are both clear —
        /// no regiment or starfighter deployment mission is active for this system.
        /// </summary>
        [Test]
        public void NeedsUnitDeployment_TrueWhenNeitherDeploymentBitIsSet()
        {
            var rec = new SystemAnalysisRecord();
            rec.FlagA = 0;

            Assert.IsTrue(rec.NeedsUnitDeployment);
        }

        /// <summary>
        /// FlagA bit 0x1 (regiment deployment in progress) makes NeedsUnitDeployment false.
        /// </summary>
        [Test]
        public void NeedsUnitDeployment_FalseWhenRegimentDeploymentBitSet()
        {
            var rec = new SystemAnalysisRecord();
            rec.FlagA = 0x1;

            Assert.IsFalse(rec.NeedsUnitDeployment);
        }

        /// <summary>
        /// FlagA bit 0x2 (starfighter deployment in progress) makes NeedsUnitDeployment false.
        /// </summary>
        [Test]
        public void NeedsUnitDeployment_FalseWhenStarfighterDeploymentBitSet()
        {
            var rec = new SystemAnalysisRecord();
            rec.FlagA = 0x2;

            Assert.IsFalse(rec.NeedsUnitDeployment);
        }

        /// <summary>
        /// Both deployment bits set also yields false — any active deployment blocks selection.
        /// </summary>
        [Test]
        public void NeedsUnitDeployment_FalseWhenBothDeploymentBitsSet()
        {
            var rec = new SystemAnalysisRecord();
            rec.FlagA = 0x3;

            Assert.IsFalse(rec.NeedsUnitDeployment);
        }

        /// <summary>
        /// Other FlagA bits (not 0x1 or 0x2) do not affect NeedsUnitDeployment.
        /// </summary>
        [Test]
        public void NeedsUnitDeployment_TrueWhenOnlyOtherFlagABitsSet()
        {
            var rec = new SystemAnalysisRecord();
            rec.FlagA = 0x800 | 0x800000; // HasEnemyEconomicTarget + IsShortageCandidate bits

            Assert.IsTrue(rec.NeedsUnitDeployment);
        }
    }
}
