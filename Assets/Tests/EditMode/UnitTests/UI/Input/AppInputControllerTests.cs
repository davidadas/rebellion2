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

        [Test]
        public void CancelOrSettings_ShortcutsSuppressed_DoesNotCancel()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject root = new("AppInputControllerUnderTest");
            InputManager inputManager = root.AddComponent<InputManager>();
            AppInputController controller = root.AddComponent<AppInputController>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            TestCancelable cancelable = new();
            CancelStack cancelStack = new();
            cancelStack.Register(cancelable);

            try
            {
                controller.Initialize(inputManager, cancelStack, null);
                inputManager.SetApplicationShortcutsSuppressed(true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();

                Assert.AreEqual(0, cancelable.CancelCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
                inputFixture.TearDown();
            }
        }

        [Test]
        public void QuickLoad_Performed_RoutesThroughSavedGameTransition()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject root = new("AppInputControllerUnderTest");
            InputManager inputManager = root.AddComponent<InputManager>();
            AppInputController controller = root.AddComponent<AppInputController>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            string requestedSave = null;

            try
            {
                controller.Initialize(
                    inputManager,
                    new CancelStack(),
                    null,
                    fileName =>
                    {
                        requestedSave = fileName;
                        return true;
                    }
                );
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F9));
                InputSystem.Update();

                Assert.AreEqual(SaveGameManager.QuickSaveFileName, requestedSave);
            }
            finally
            {
                Object.DestroyImmediate(root);
                inputFixture.TearDown();
            }
        }

        [Test]
        public void QuickLoad_MenuContext_BlocksLoadingUntilPreviousContextIsRestored()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject root = new("AppInputControllerUnderTest");
            InputManager inputManager = root.AddComponent<InputManager>();
            AppInputController controller = root.AddComponent<AppInputController>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int loads = 0;
            try
            {
                controller.Initialize(
                    inputManager,
                    new CancelStack(),
                    null,
                    _ =>
                    {
                        loads++;
                        return true;
                    }
                );
                controller.SetContext(InputContext.Strategy);
                controller.PushContext(InputContext.Menu);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F9));
                InputSystem.Update();

                Assert.AreEqual(0, loads);
                controller.PopContext();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F9));
                InputSystem.Update();
                Assert.AreEqual(1, loads);
            }
            finally
            {
                Object.DestroyImmediate(root);
                inputFixture.TearDown();
            }
        }

        [Test]
        public void QuickLoad_OverlappingShortcutOwners_BlocksUntilBothRelease()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject root = new("AppInputControllerUnderTest");
            InputManager inputManager = root.AddComponent<InputManager>();
            AppInputController controller = root.AddComponent<AppInputController>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int loads = 0;
            System.IDisposable first = null;
            System.IDisposable second = null;
            try
            {
                controller.Initialize(
                    inputManager,
                    new CancelStack(),
                    null,
                    _ =>
                    {
                        loads++;
                        return true;
                    }
                );
                first = inputManager.SuppressApplicationShortcuts();
                second = inputManager.SuppressApplicationShortcuts();
                first.Dispose();
                first.Dispose();

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F9));
                InputSystem.Update();

                Assert.AreEqual(0, loads);
                second.Dispose();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F9));
                InputSystem.Update();
                Assert.AreEqual(1, loads);
            }
            finally
            {
                first?.Dispose();
                second?.Dispose();
                Object.DestroyImmediate(root);
                inputFixture.TearDown();
            }
        }

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
