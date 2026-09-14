using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.UI.SceneUI
{
    [TestFixture]
    public class GameLaunchContextTests
    {
        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameLaunchContext.Reset(TestContent.Pack);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            GameLaunchContext.Reset(TestContent.Pack);
        }

        /// <summary>
        /// Verifies reset modified context restores new game defaults.
        /// </summary>
        [Test]
        public void Reset_ModifiedContext_RestoresNewGameDefaults()
        {
            GameLaunchContext.Summary.Difficulty = GameDifficulty.Hard;
            GameLaunchContext.Summary.GalaxySize = GameSize.Small;
            GameLaunchContext.Summary.VictoryCondition = GameVictoryCondition.Headquarters;
            GameLaunchContext.SaveFileName = "Campaign";
            GameLaunchContext.IsLoadGame = true;
            GameLaunchContext.PlayIntroCutscene = true;

            GameLaunchContext.Reset(TestContent.Pack);

            Assert.AreEqual(GameDifficulty.Easy, GameLaunchContext.Summary.Difficulty);
            Assert.AreEqual(GameSize.Large, GameLaunchContext.Summary.GalaxySize);
            Assert.AreEqual(
                GameVictoryCondition.Conquest,
                GameLaunchContext.Summary.VictoryCondition
            );
            Assert.AreEqual(
                GameResourceAvailability.Normal,
                GameLaunchContext.Summary.ResourceAvailability
            );
            Assert.AreEqual(0, GameLaunchContext.Summary.StartingResearchLevel);
            Assert.AreEqual("FNALL1", GameLaunchContext.Summary.PlayerFactionID);
            Assert.IsNull(GameLaunchContext.SaveFileName);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
            Assert.IsFalse(GameLaunchContext.PlayIntroCutscene);
        }

        /// <summary>
        /// Verifies reset existing summary replaces summary instance.
        /// </summary>
        [Test]
        public void Reset_ExistingSummary_ReplacesSummaryInstance()
        {
            GameSummary original = GameLaunchContext.Summary;

            GameLaunchContext.Reset(TestContent.Pack);

            Assert.AreNotSame(original, GameLaunchContext.Summary);
        }
    }
}
