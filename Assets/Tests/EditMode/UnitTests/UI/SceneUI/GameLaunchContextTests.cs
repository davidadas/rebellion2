using System;
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
        public void Reset_ExistingSetup_ReplacesSetupInstance()
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
        public void Reset_PendingSavedGame_ClearsPreviousLaunchRequest()
        {
            GameLaunchContext.PrepareSaveLoad("old-campaign");

            GameLaunchContext.Reset(TestContent.Pack);

            Assert.IsNull(GameLaunchContext.SaveFileName);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
            Assert.IsFalse(GameLaunchContext.PlayIntroCutscene);
        }

        [Test]
        public void ApplyCustomGameSummary_ConfiguredSetup_ReplacesContextWithDetachedCopy()
        {
            GameSummary configured = new GameSummary
            {
                PlayerFactionID = "FNEMP1",
                Difficulty = GameDifficulty.Hard,
                GalaxySize = GameSize.Small,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Abundant,
                StartingFactionIDs = new[] { "FNEMP1", "FNALL1" },
                CustomSettings = new CustomGameSettings
                {
                    GenerationConfig = TestContent.Data.GenerationConfig.GetDeepCopy(),
                },
            };
            GameLaunchContext.PrepareSaveLoad("campaign");

            GameLaunchContext.ApplyCustomGameSummary(configured);
            configured.StartingFactionIDs[0] = "changed";
            configured.CustomSettings.GenerationConfig.FacilityGeneration.CoreMineMultiplier = 999;

            Assert.AreEqual("FNEMP1", GameLaunchContext.Summary.PlayerFactionID);
            Assert.AreEqual(GameDifficulty.Hard, GameLaunchContext.Summary.Difficulty);
            Assert.AreEqual("FNEMP1", GameLaunchContext.Summary.StartingFactionIDs[0]);
            Assert.AreNotEqual(
                999,
                GameLaunchContext
                    .Summary
                    .CustomSettings
                    .GenerationConfig
                    .FacilityGeneration
                    .CoreMineMultiplier
            );
            Assert.AreNotSame(configured, GameLaunchContext.Summary);
            Assert.IsNull(GameLaunchContext.SaveFileName);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
        }

        [Test]
        public void ApplyCustomGameSummary_NullSetup_PreservesPendingLaunch()
        {
            GameSummary original = GameLaunchContext.Summary;
            GameLaunchContext.PrepareSaveLoad("pending-campaign");

            Assert.Throws<ArgumentNullException>(() =>
                GameLaunchContext.ApplyCustomGameSummary(null)
            );

            Assert.AreSame(original, GameLaunchContext.Summary);
            Assert.AreEqual("pending-campaign", GameLaunchContext.SaveFileName);
            Assert.IsTrue(GameLaunchContext.IsLoadGame);
        }
    }
}
