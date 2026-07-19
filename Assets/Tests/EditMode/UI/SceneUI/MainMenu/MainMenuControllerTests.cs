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

        [SetUp]
        public void SetUp()
        {
            GameLaunchContext.Reset();
            _gameObject = new GameObject("MainMenuControllerUnderTest");
            _controller = _gameObject.AddComponent<MainMenuController>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_gameObject);
            GameLaunchContext.Reset();
        }

        [Test]
        public void SelectFaction_ConfiguredId_UpdatesLaunchSummary()
        {
            _controller.SelectFaction("faction-2");

            Assert.AreEqual("faction-2", GameLaunchContext.Summary.PlayerFactionID);
        }

        [Test]
        public void SelectGalaxySize_Value_UpdatesLaunchSummary()
        {
            _controller.SelectGalaxySize(GameSize.Medium);

            Assert.AreEqual(GameSize.Medium, GameLaunchContext.Summary.GalaxySize);
        }

        [Test]
        public void SelectDifficulty_Value_UpdatesLaunchSummary()
        {
            _controller.SelectGameDifficulty(GameDifficulty.Hard);

            Assert.AreEqual(GameDifficulty.Hard, GameLaunchContext.Summary.Difficulty);
        }

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
