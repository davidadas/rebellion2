using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Simulation;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Screen
{
    [TestFixture]
    public class GameFlowControllerTests
    {
        private GameFlowController _controller;
        private GameObject _gameObject;
        private StrategyController _strategyController;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("GameFlowControllerUnderTest");
            _gameObject.SetActive(false);
            _strategyController = _gameObject.AddComponent<StrategyController>();
            _controller = _gameObject.AddComponent<GameFlowController>();
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
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
        public void AdvanceActiveTick_WithRemainingStep_RetainsTickUntilFollowingFrame()
        {
            IEnumerator tick = new object[] { null }.GetEnumerator();
            SetField("activeTick", tick);

            InvokePrivate("AdvanceActiveTick");

            Assert.AreSame(tick, GetField<IEnumerator>("activeTick"));

            InvokePrivate("AdvanceActiveTick");

            Assert.IsNull(GetField<IEnumerator>("activeTick"));
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

        [Test]
        public void GetHeadquartersDestroyedCutscenePath_HeadquartersLost_ReturnsDefenderMovie()
        {
            Faction defender = new Faction { InstanceID = "defender" };
            FactionThemeLibrary themes = new FactionThemeLibrary(
                new FactionThemes
                {
                    new FactionTheme { FactionInstanceID = "DEFAULT" },
                    new FactionTheme
                    {
                        FactionInstanceID = defender.InstanceID,
                        HeadquartersDestroyedCutscenePath = "defender-headquarters-destroyed",
                    },
                }
            );

            string path = GameFlowController.GetHeadquartersDestroyedCutscenePath(
                themes,
                new HeadquartersCapturedResult { Defender = defender }
            );

            Assert.AreEqual("defender-headquarters-destroyed", path);
        }

        [Test]
        public void GetHeadquartersDestroyedCutscenePath_MissingDefenderTheme_ReturnsNull()
        {
            FactionThemeLibrary themes = CreateDefaultOnlyThemeLibrary();

            string path = GameFlowController.GetHeadquartersDestroyedCutscenePath(
                themes,
                new HeadquartersCapturedResult
                {
                    Defender = new Faction { InstanceID = "missing-faction" },
                }
            );

            Assert.IsNull(path);
        }

        [Test]
        public void HandleVictoryDeclared_MissingPlayerTheme_StillSchedulesCampaignFinish()
        {
            GameConfig config = new GameConfig();
            config.Smuggling.LossPercentByMinimumSupport[0] = 0;
            GameRoot game = new GameRoot(config);
            Faction player = new Faction { InstanceID = "missing-player-theme" };
            Faction opponent = new Faction { InstanceID = "opponent" };
            game.GetFactions().Add(player);
            game.GetFactions().Add(opponent);
            game.Summary.PlayerFactionID = player.InstanceID;
            game.SetFactionController(player.InstanceID, "PLAYER1", PlayerControllerType.Human);
            GameSession manager = GameSessionFactory.Create(game, TestGameData.Create(config));
            SetField("activeGameSession", manager);
            SetField("themeLibrary", CreateDefaultOnlyThemeLibrary());
            SetField("cutscenePlaying", true);

            Assert.DoesNotThrow(() =>
                InvokePrivate(
                    "HandleVictoryDeclared",
                    new VictoryResult { Winner = player, Loser = opponent }
                )
            );

            Assert.AreEqual(TickSpeed.Paused, game.GetGameSpeed());
            Assert.IsTrue(GetField<bool>("campaignEnding"));
            Assert.IsTrue(GetField<bool>("finishCampaignAfterCutscenes"));
            Assert.IsEmpty(GetField<Queue<string>>("cutsceneQueue"));
        }

        [Test]
        public void Update_NoActiveGame_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => InvokePrivate("Update"));
        }

        [Test]
        public void ShouldPlayOpeningBriefing_BriefingsDisabled_ReturnsFalse()
        {
            bool shouldPlay = GameFlowController.ShouldPlayOpeningBriefing(
                requested: true,
                completed: false,
                disabled: true
            );

            Assert.IsFalse(shouldPlay);
        }

        [Test]
        public void ShouldPlayOpeningBriefing_BriefingsEnabledAndUncompleted_ReturnsTrue()
        {
            bool shouldPlay = GameFlowController.ShouldPlayOpeningBriefing(
                requested: true,
                completed: false,
                disabled: false
            );

            Assert.IsTrue(shouldPlay);
        }

        [Test]
        public void StartNewGameAsync_MissingSummary_ThrowsInvalidOperationException()
        {
            FieldInfo summaryField = typeof(GameLaunchContext).GetField(
                "<Summary>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            object originalSummary = summaryField.GetValue(null);
            try
            {
                summaryField.SetValue(null, null);

                Task startup = (Task)InvokePrivate("StartNewGameAsync");

                Assert.ThrowsAsync<InvalidOperationException>(async () => await startup);
            }
            finally
            {
                summaryField.SetValue(null, originalSummary);
            }
        }

        [Test]
        public void LoadGame_MissingFileName_ThrowsInvalidOperationException()
        {
            string originalFileName = GameLaunchContext.SaveFileName;
            try
            {
                GameLaunchContext.SaveFileName = null;

                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                    InvokePrivate("LoadGame")
                );

                Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            }
            finally
            {
                GameLaunchContext.SaveFileName = originalFileName;
            }
        }

        [Test]
        public void PlayFactionIntroAsync_NullFaction_ThrowsInvalidOperationException()
        {
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                InvokePrivate("PlayFactionIntroAsync", new object[] { null })
            );

            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
        }

        [Test]
        public void PlayFactionIntroAsync_IntroDisabled_ReturnsCompletedTask()
        {
            bool original = GameLaunchContext.PlayIntroCutscene;
            GameLaunchContext.PlayIntroCutscene = false;
            Task task;
            try
            {
                task = (Task)InvokePrivate(
                    "PlayFactionIntroAsync",
                    new object[] { new Rebellion.Game.Factions.Faction() }
                );
            }
            finally
            {
                GameLaunchContext.PlayIntroCutscene = original;
            }

            Assert.IsTrue(task.IsCompletedSuccessfully);
        }

        /// <summary>
        /// Gets field.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The requested field.</returns>
        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(GameFlowController)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_controller);
        }

        /// <summary>
        /// Sets field.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <param name="value">The value.</param>
        private void SetField(string fieldName, object value)
        {
            typeof(GameFlowController)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_controller, value);
        }

        /// <summary>
        /// Executes invoke private.
        /// </summary>
        /// <param name="methodName">The method name.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The result of invoke private.</returns>
        private object InvokePrivate(string methodName, params object[] arguments)
        {
            return typeof(GameFlowController)
                .GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic
                )
                .Invoke(_controller, arguments);
        }

        /// <summary>
        /// Creates default only theme library.
        /// </summary>
        /// <returns>The created default only theme library.</returns>
        private static FactionThemeLibrary CreateDefaultOnlyThemeLibrary()
        {
            return new FactionThemeLibrary(
                new FactionThemes { new FactionTheme { FactionInstanceID = "DEFAULT" } }
            );
        }
    }
}
