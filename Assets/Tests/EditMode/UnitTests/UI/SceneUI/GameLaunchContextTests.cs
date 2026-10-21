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
            GameLaunchContext.Setup.Difficulty = GameDifficulty.Hard;
            GameLaunchContext.Setup.GalaxySize = GameSize.Small;
            GameLaunchContext.Setup.VictoryCondition = GameVictoryCondition.Headquarters;
            GameLaunchContext.SaveFileName = "Campaign";
            GameLaunchContext.IsLoadGame = true;
            GameLaunchContext.PlayIntroCutscene = true;

            GameLaunchContext.Reset(TestContent.Pack);

            Assert.AreEqual(GameDifficulty.Easy, GameLaunchContext.Setup.Difficulty);
            Assert.AreEqual(GameSize.Large, GameLaunchContext.Setup.GalaxySize);
            Assert.AreEqual(
                GameVictoryCondition.Conquest,
                GameLaunchContext.Setup.VictoryCondition
            );
            Assert.AreEqual(
                GameResourceAvailability.Normal,
                GameLaunchContext.Setup.ResourceAvailability
            );
            Assert.AreEqual("FNALL1", GameLaunchContext.Setup.PlayerFactionID);
            Assert.IsNull(GameLaunchContext.SaveFileName);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
            Assert.IsFalse(GameLaunchContext.PlayIntroCutscene);
        }

        [Test]
        public void Reset_ExistingSetup_ReplacesSetupInstance()
        {
            GameSetup original = GameLaunchContext.Setup;

            GameLaunchContext.Reset(TestContent.Pack);

            Assert.AreNotSame(original, GameLaunchContext.Setup);
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
        public void ApplyCustomGameSetup_ConfiguredSetup_ReplacesContextWithDetachedCopy()
        {
            GameSetup configured = new GameSetup
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

            GameLaunchContext.ApplyCustomGameSetup(configured);
            configured.StartingFactionIDs[0] = "changed";
            configured.CustomSettings.GenerationConfig.FacilityGeneration.CoreMineMultiplier = 999;

            Assert.AreEqual("FNEMP1", GameLaunchContext.Setup.PlayerFactionID);
            Assert.AreEqual(GameDifficulty.Hard, GameLaunchContext.Setup.Difficulty);
            Assert.AreEqual("FNEMP1", GameLaunchContext.Setup.StartingFactionIDs[0]);
            Assert.AreNotEqual(
                999,
                GameLaunchContext
                    .Setup
                    .CustomSettings
                    .GenerationConfig
                    .FacilityGeneration
                    .CoreMineMultiplier
            );
            Assert.AreNotSame(configured, GameLaunchContext.Setup);
            Assert.IsNull(GameLaunchContext.SaveFileName);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
        }

        [Test]
        public void ApplyCustomGameSetup_NullSetup_PreservesPendingLaunch()
        {
            GameSetup original = GameLaunchContext.Setup;
            GameLaunchContext.PrepareSaveLoad("pending-campaign");

            Assert.Throws<ArgumentNullException>(() =>
                GameLaunchContext.ApplyCustomGameSetup(null)
            );

            Assert.AreSame(original, GameLaunchContext.Setup);
            Assert.AreEqual("pending-campaign", GameLaunchContext.SaveFileName);
            Assert.IsTrue(GameLaunchContext.IsLoadGame);
        }
    }
}
