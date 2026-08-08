using System.Reflection;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Screen
{
    [TestFixture]
    public class GameFlowControllerTests
    {
        private GameFlowController _controller;
        private GameObject _gameObject;
        private StrategyController _strategyController;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("GameFlowControllerUnderTest");
            _gameObject.SetActive(false);
            _strategyController = _gameObject.AddComponent<StrategyController>();
            _controller = _gameObject.AddComponent<GameFlowController>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void Awake_MissingSerializedStrategyController_ThrowsMissingReferenceException()
        {
            SetField("strategyController", null);

            Assert.Throws<MissingReferenceException>(() =>
                UIComponentTestHelper.InvokeLifecycle(_controller, "Awake")
            );
        }

        [Test]
        public void Awake_ComposedStrategyController_DoesNotThrow()
        {
            UIComponentTestHelper.InvokeLifecycle(_controller, "Reset");

            Assert.DoesNotThrow(() => UIComponentTestHelper.InvokeLifecycle(_controller, "Awake"));
        }

        [Test]
        public void Reset_ComposedGameObject_AssignsStrategyControllerReference()
        {
            UIComponentTestHelper.InvokeLifecycle(_controller, "Reset");

            Assert.AreSame(_strategyController, GetField<StrategyController>("strategyController"));
        }

        [Test]
        public void GetCampaignEndingCutscenePath_PlayerWon_ReturnsConfiguredVictoryMovie()
        {
            Faction player = new Faction { InstanceID = "alliance" };
            Faction opponent = new Faction { InstanceID = "empire" };
            FactionTheme theme = new FactionTheme
            {
                VictoryCutscenePath = "alliance-victory",
                DefeatCutscenePath = "alliance-defeat",
            };

            string path = GameFlowController.GetCampaignEndingCutscenePath(
                theme,
                player,
                new VictoryResult { Winner = player, Loser = opponent }
            );

            Assert.AreEqual("alliance-victory", path);
        }

        [Test]
        public void GetCampaignEndingCutscenePath_PlayerLost_ReturnsConfiguredDefeatMovie()
        {
            Faction player = new Faction { InstanceID = "alliance" };
            Faction opponent = new Faction { InstanceID = "empire" };
            FactionTheme theme = new FactionTheme
            {
                VictoryCutscenePath = "alliance-victory",
                DefeatCutscenePath = "alliance-defeat",
            };

            string path = GameFlowController.GetCampaignEndingCutscenePath(
                theme,
                player,
                new VictoryResult { Winner = opponent, Loser = player }
            );

            Assert.AreEqual("alliance-defeat", path);
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(GameFlowController)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_controller);
        }

        private void SetField(string fieldName, object value)
        {
            typeof(GameFlowController)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_controller, value);
        }
    }
}
