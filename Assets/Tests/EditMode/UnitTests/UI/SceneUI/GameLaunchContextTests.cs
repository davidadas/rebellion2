using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Util.Reflection;

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
            Assert.AreEqual("FNALL1", GameLaunchContext.Summary.PlayerFactionID);
            Assert.IsNull(GameLaunchContext.SaveFileName);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
            Assert.IsFalse(GameLaunchContext.PlayIntroCutscene);
        }

        [Test]
        public void Reset_ExistingSummary_ReplacesSummaryInstance()
        {
            GameSummary original = GameLaunchContext.Summary;

            GameLaunchContext.Reset(TestContent.Pack);

            Assert.AreNotSame(original, GameLaunchContext.Summary);
        }

        [Test]
        public void PrepareSaveLoad_ValidFileName_ConfiguresSavedGameLaunch()
        {
            GameLaunchContext.PlayIntroCutscene = true;

            GameLaunchContext.PrepareSaveLoad("campaign");

            Assert.AreEqual("campaign", GameLaunchContext.SaveFileName);
            Assert.IsTrue(GameLaunchContext.IsLoadGame);
            Assert.IsFalse(GameLaunchContext.PlayIntroCutscene);
        }

        [Test]
        public void ApplyCustomGameSummary_ConfiguredSummary_ReplacesContextWithDetachedCopy()
        {
            GameSummary configured = new GameSummary
            {
                PlayerFactionID = "FNEMP1",
                Difficulty = GameDifficulty.Hard,
                GalaxySize = GameSize.Small,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Abundant,
                StartingFactionIDs = new[] { "FNEMP1", "FNALL1" },
                GenerationConfigOverride = TestContent.Data.GenerationConfig.GetDeepCopy(),
            };
            GameLaunchContext.PrepareSaveLoad("campaign");

            GameLaunchContext.ApplyCustomGameSummary(configured);
            configured.StartingFactionIDs[0] = "changed";
            configured.GenerationConfigOverride.FacilityGeneration.CoreMineMultiplier = 999;

            Assert.AreEqual("FNEMP1", GameLaunchContext.Summary.PlayerFactionID);
            Assert.AreEqual(GameDifficulty.Hard, GameLaunchContext.Summary.Difficulty);
            Assert.AreEqual("FNEMP1", GameLaunchContext.Summary.StartingFactionIDs[0]);
            Assert.AreNotEqual(
                999,
                GameLaunchContext
                    .Summary
                    .GenerationConfigOverride
                    .FacilityGeneration
                    .CoreMineMultiplier
            );
            Assert.AreNotSame(configured, GameLaunchContext.Summary);
            Assert.IsNull(GameLaunchContext.SaveFileName);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
        }

        [Test]
        public void RestoreSaveLoadAfterContentRestart_PreservedRequest_RestoresSavedGameLaunch()
        {
            GameLaunchContext.PrepareSaveLoadAfterContentRestart("campaign");
            GameLaunchContext.Reset(TestContent.Pack);

            GameLaunchContext.RestoreSaveLoadAfterContentRestart();

            Assert.AreEqual("campaign", GameLaunchContext.SaveFileName);
            Assert.IsTrue(GameLaunchContext.IsLoadGame);
            Assert.IsFalse(GameLaunchContext.PlayIntroCutscene);
        }

        [Test]
        public void RestoreSaveLoadAfterContentRestart_ConsumedRequest_DoesNotRestoreTwice()
        {
            GameLaunchContext.PrepareSaveLoadAfterContentRestart("campaign");
            GameLaunchContext.RestoreSaveLoadAfterContentRestart();
            GameLaunchContext.Reset(TestContent.Pack);

            GameLaunchContext.RestoreSaveLoadAfterContentRestart();

            Assert.IsNull(GameLaunchContext.SaveFileName);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
        }

        [Test]
        public void ConsumeContentPackAfterRestart_PreparedPack_ReturnsPackOnce()
        {
            GameLaunchContext.PrepareSaveLoadAfterContentRestart("campaign", TestContent.Pack);

            ContentPack first = GameLaunchContext.ConsumeContentPackAfterRestart();
            ContentPack second = GameLaunchContext.ConsumeContentPackAfterRestart();

            Assert.AreSame(TestContent.Pack, first);
            Assert.IsNull(second);
        }

        [Test]
        public void RestoreCustomGameAfterContentRestart_PreparedSummary_RestoresDetachedLaunch()
        {
            GameSummary summary = GameLaunchContext.CreateDefaultSummary(TestContent.Pack);
            summary.PlayerFactionID = "FNEMP1";
            summary.Difficulty = GameDifficulty.Hard;
            GameLaunchContext.PrepareCustomGameAfterContentRestart(summary, TestContent.Pack);

            ContentPack selected = GameLaunchContext.ConsumeContentPackAfterRestart();
            GameLaunchContext.Reset(selected);
            GameLaunchContext.RestoreCustomGameAfterContentRestart();
            summary.PlayerFactionID = "changed";

            Assert.AreSame(TestContent.Pack, selected);
            Assert.AreEqual("FNEMP1", GameLaunchContext.Summary.PlayerFactionID);
            Assert.AreEqual(GameDifficulty.Hard, GameLaunchContext.Summary.Difficulty);
            Assert.AreNotSame(summary, GameLaunchContext.Summary);
        }
    }
}
