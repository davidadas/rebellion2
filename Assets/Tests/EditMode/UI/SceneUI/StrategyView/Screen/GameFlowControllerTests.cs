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

        [Test]
        public void StartNewGame_MissingSummary_ThrowsInvalidOperationException()
        {
            FieldInfo summaryField = typeof(GameLaunchContext).GetField(
                "<Summary>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            object originalSummary = summaryField.GetValue(null);
            try
            {
                summaryField.SetValue(null, null);

                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                    InvokePrivate("StartNewGame")
                );

                Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
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
        public void PlayFactionIntro_NullFaction_ThrowsInvalidOperationException()
        {
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                InvokePrivate("PlayFactionIntro", new object[] { null })
            );

            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
        }

        [Test]
        public void PreloadBriefingContentAsync_NullBriefing_ReturnsCompletedTask()
        {
            Task task = (Task)
                typeof(GameFlowController)
                    .GetMethod(
                        "PreloadBriefingContentAsync",
                        BindingFlags.Static | BindingFlags.NonPublic
                    )
                    .Invoke(null, new object[] { null });

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
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_controller, arguments);
        }
    }
}
