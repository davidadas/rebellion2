using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
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
        public void Update_NoActiveGame_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => InvokePrivate("Update"));
        }

        /// <summary>
        /// Verifies that new-game startup rejects a missing launch summary.
        /// </summary>
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

        /// <summary>
        /// Verifies that faction introduction playback rejects a missing faction.
        /// </summary>
        [Test]
        public void PlayFactionIntroAsync_NullFaction_ThrowsInvalidOperationException()
        {
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                InvokePrivate("PlayFactionIntroAsync", new object[] { null })
            );

            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
        }

        /// <summary>
        /// Verifies disabled introductions have no asynchronous presentation gate.
        /// </summary>
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

        private object InvokePrivate(string methodName, params object[] arguments)
        {
            return typeof(GameFlowController)
                .GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic
                )
                .Invoke(_controller, arguments);
        }
    }
}
