using NUnit.Framework;
using Rebellion.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Rebellion.Tests.UI.Input
{
    [TestFixture]
    public class AppInputControllerTests
    {
        /// <summary>
        /// Verifies cancel or settings unhandled does not raise options menu request.
        /// </summary>
        [Test]
        public void CancelOrSettings_Unhandled_DoesNotRaiseOptionsMenuRequest()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject root = new("AppInputControllerUnderTest");
            InputManager inputManager = root.AddComponent<InputManager>();
            AppInputController controller = root.AddComponent<AppInputController>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int requestCount = 0;
            TestCancelable cancelable = new();
            CancelStack cancelStack = new();
            cancelStack.Register(cancelable);
            controller.OptionsMenuRequested += () => requestCount++;

            try
            {
                controller.Initialize(inputManager, cancelStack, null);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();

                Assert.AreEqual(0, requestCount);
                Assert.AreEqual(1, cancelable.CancelCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
                inputFixture.TearDown();
            }
        }

        /// <summary>
        /// Verifies open game menu shift escape raises options menu request.
        /// </summary>
        [Test]
        public void OpenGameMenu_ShiftEscape_RaisesOptionsMenuRequest()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject root = new("AppInputControllerUnderTest");
            InputManager inputManager = root.AddComponent<InputManager>();
            AppInputController controller = root.AddComponent<AppInputController>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int requestCount = 0;
            TestCancelable cancelable = new();
            CancelStack cancelStack = new();
            cancelStack.Register(cancelable);
            controller.OptionsMenuRequested += () => requestCount++;

            try
            {
                controller.Initialize(inputManager, cancelStack, null);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.Escape));
                InputSystem.Update();

                Assert.AreEqual(1, requestCount);
                Assert.AreEqual(0, cancelable.CancelCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
                inputFixture.TearDown();
            }
        }

        /// <summary>
        /// Verifies get slower game speed supported speeds steps toward paused.
        /// </summary>
        [Test]
        public void GetSlowerGameSpeed_SupportedSpeeds_StepsTowardPaused()
        {
            Assert.AreEqual(
                TickSpeed.Medium,
                AppInputController.GetSlowerGameSpeed(TickSpeed.Fast)
            );
            Assert.AreEqual(
                TickSpeed.Slow,
                AppInputController.GetSlowerGameSpeed(TickSpeed.Medium)
            );
            Assert.AreEqual(
                TickSpeed.VerySlow,
                AppInputController.GetSlowerGameSpeed(TickSpeed.Slow)
            );
            Assert.AreEqual(
                TickSpeed.Paused,
                AppInputController.GetSlowerGameSpeed(TickSpeed.VerySlow)
            );
            Assert.AreEqual(
                TickSpeed.Paused,
                AppInputController.GetSlowerGameSpeed(TickSpeed.Paused)
            );
        }

        /// <summary>
        /// Verifies get faster game speed supported speeds steps toward fast.
        /// </summary>
        [Test]
        public void GetFasterGameSpeed_SupportedSpeeds_StepsTowardFast()
        {
            Assert.AreEqual(
                TickSpeed.VerySlow,
                AppInputController.GetFasterGameSpeed(TickSpeed.Paused)
            );
            Assert.AreEqual(
                TickSpeed.Slow,
                AppInputController.GetFasterGameSpeed(TickSpeed.VerySlow)
            );
            Assert.AreEqual(
                TickSpeed.Medium,
                AppInputController.GetFasterGameSpeed(TickSpeed.Slow)
            );
            Assert.AreEqual(
                TickSpeed.Fast,
                AppInputController.GetFasterGameSpeed(TickSpeed.Medium)
            );
            Assert.AreEqual(TickSpeed.Fast, AppInputController.GetFasterGameSpeed(TickSpeed.Fast));
        }

        private sealed class TestCancelable : ICancelable
        {
            public int CancelCount { get; private set; }

            /// <summary>
            /// Attempts cancel.
            /// </summary>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool TryCancel()
            {
                CancelCount++;
                return true;
            }
        }
    }
}
