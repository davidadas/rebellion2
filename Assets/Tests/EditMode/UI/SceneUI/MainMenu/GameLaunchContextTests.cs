using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.UI.SceneUI.MainMenu
{
    [TestFixture]
    public class GameLaunchContextTests
    {
        [SetUp]
        public void SetUp()
        {
            GameLaunchContext.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GameLaunchContext.Reset();
        }

        [Test]
        public void Reset_ModifiedContext_RestoresNewGameDefaults()
        {
            GameLaunchContext.Summary.Difficulty = GameDifficulty.Hard;
            GameLaunchContext.Summary.GalaxySize = GameSize.Small;
            GameLaunchContext.Summary.VictoryCondition = GameVictoryCondition.Headquarters;
            GameLaunchContext.SaveFileName = "Campaign";
            GameLaunchContext.IsLoadGame = true;
            GameLaunchContext.PlayIntroCutscene = true;

            GameLaunchContext.Reset();

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
            Assert.AreEqual(1, GameLaunchContext.Summary.StartingResearchLevel);
            Assert.IsNull(GameLaunchContext.SaveFileName);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
            Assert.IsFalse(GameLaunchContext.PlayIntroCutscene);
        }

        [Test]
        public void Reset_ExistingSummary_ReplacesSummaryInstance()
        {
            GameSummary original = GameLaunchContext.Summary;

            GameLaunchContext.Reset();

            Assert.AreNotSame(original, GameLaunchContext.Summary);
        }
    }
}
