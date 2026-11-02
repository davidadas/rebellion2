using NUnit.Framework;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class GameConfigTests
    {
        [TestCase(0, ForceRankLabel.None)]
        [TestCase(9, ForceRankLabel.None)]
        [TestCase(10, ForceRankLabel.Novice)]
        [TestCase(19, ForceRankLabel.Novice)]
        [TestCase(20, ForceRankLabel.Trainee)]
        [TestCase(79, ForceRankLabel.Trainee)]
        [TestCase(80, ForceRankLabel.ForceStudent)]
        [TestCase(99, ForceRankLabel.ForceStudent)]
        [TestCase(100, ForceRankLabel.ForceKnight)]
        [TestCase(119, ForceRankLabel.ForceKnight)]
        [TestCase(120, ForceRankLabel.ForceMaster)]
        [TestCase(-1, ForceRankLabel.ForceMaster)]
        [TestCase(-32768, ForceRankLabel.ForceMaster)]
        [TestCase(65536, ForceRankLabel.None)]
        public void GetRankLabel_StoredRatingBoundary_UsesDisplayRepresentation(
            int forceRank,
            ForceRankLabel expected
        )
        {
            Assert.AreEqual(expected, TestContent.Data.GameConfig.Jedi.GetRankLabel(forceRank));
        }
    }
}
