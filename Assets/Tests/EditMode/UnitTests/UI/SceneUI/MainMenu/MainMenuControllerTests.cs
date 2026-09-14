using NUnit.Framework;
using Rebellion.Game;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.MainMenu
{
    [TestFixture]
    public class MainMenuControllerTests
    {
        private GameObject _gameObject;
        private MainMenuController _controller;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameLaunchContext.Reset(TestContent.Pack);
            _gameObject = new GameObject("MainMenuControllerUnderTest");
            _controller = _gameObject.AddComponent<MainMenuController>();
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_gameObject);
            GameLaunchContext.Reset(TestContent.Pack);
        }

        /// <summary>
        /// Verifies select faction configured id updates launch summary.
        /// </summary>
        [Test]
        public void SelectFaction_ConfiguredID_UpdatesLaunchSummary()
        {
            _controller.SelectFaction("faction-2");

            Assert.AreEqual("faction-2", GameLaunchContext.Summary.PlayerFactionID);
        }

        /// <summary>
        /// Verifies select galaxy size value updates launch summary.
        /// </summary>
        [Test]
        public void SelectGalaxySize_Value_UpdatesLaunchSummary()
        {
            _controller.SelectGalaxySize(GameSize.Medium);

            Assert.AreEqual(GameSize.Medium, GameLaunchContext.Summary.GalaxySize);
        }

        /// <summary>
        /// Verifies select difficulty value updates launch summary.
        /// </summary>
        [Test]
        public void SelectDifficulty_Value_UpdatesLaunchSummary()
        {
            _controller.SelectGameDifficulty(GameDifficulty.Hard);

            Assert.AreEqual(GameDifficulty.Hard, GameLaunchContext.Summary.Difficulty);
        }

        /// <summary>
        /// Verifies select victory condition value updates launch summary without view.
        /// </summary>
        [Test]
        public void SelectVictoryCondition_Value_UpdatesLaunchSummaryWithoutView()
        {
            _controller.SelectVictoryCondition(GameVictoryCondition.Headquarters);

            Assert.AreEqual(
                GameVictoryCondition.Headquarters,
                GameLaunchContext.Summary.VictoryCondition
            );
        }
    }
}
