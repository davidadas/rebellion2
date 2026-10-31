using System.Threading.Tasks;
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
                        return Task.FromResult(true);
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
        public async Task QuickLoad_CanceledPersistenceRequest_DoesNotEscapeInputCallbackAsync()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject root = new("AppInputControllerUnderTest");
            InputManager inputManager = root.AddComponent<InputManager>();
            AppInputController controller = root.AddComponent<AppInputController>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            TaskCompletionSource<bool> completion = new();
            int requestCount = 0;

            try
            {
                controller.Initialize(
                    inputManager,
                    new CancelStack(),
                    null,
                    _ =>
                    {
                        requestCount++;
                        return completion.Task;
                    }
                );
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F9));
                InputSystem.Update();
                Assert.AreEqual(1, requestCount);
                Assert.IsFalse(completion.Task.IsCompleted);

                completion.SetCanceled();
                await Task.Yield();

                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally
            {
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

        [Test]
        public void SuppressShortcuts_MenuShortcut_WaitsForLeaseRelease()
        {
            using ShortcutFixture input = new();
            int requests = 0;
            input.Controller.OptionsMenuRequested += () => requests++;
            System.IDisposable lease = input.Controller.SuppressShortcuts();

            input.Press(Key.LeftShift, Key.Escape);
            Assert.AreEqual(1, input.MenuShortcutPerformances);
            Assert.Zero(requests);
            lease.Dispose();
            Assert.IsFalse(input.Controller.ShortcutsSuppressed);
            Assert.IsFalse(LoadingScreenController.IsLoadInProgress);
            input.Press(Key.LeftShift, Key.Escape);

            Assert.AreEqual(2, input.MenuShortcutPerformances);
            Assert.AreEqual(1, requests);
        }

        [Test]
        public void SuppressShortcuts_ConcurrentLeases_ReleasingOnePreservesOtherOwner()
        {
            using ShortcutFixture input = new();
            System.IDisposable first = input.Controller.SuppressShortcuts();
            System.IDisposable second = input.Controller.SuppressShortcuts();
            first.Dispose();
            first.Dispose();

            input.Press(Key.F9);
            Assert.Zero(input.LoadRequests);
            second.Dispose();
            input.Press(Key.F9);

            Assert.AreEqual(1, input.LoadRequests);
        }

        [Test]
        public void SuppressShortcuts_Cancel_StillReachesModalCancellationOwner()
        {
            using ShortcutFixture input = new();
            TestCancelable cancelable = new();
            input.CancelStack.Register(cancelable);
            using System.IDisposable lease = input.Controller.SuppressShortcuts();

            input.Press(Key.Escape);

            Assert.AreEqual(1, cancelable.CancelCount);
        }

        [Test]
        public void SuppressShortcuts_ContextReplaced_DoesNotRestoreObsoleteContext()
        {
            using ShortcutFixture input = new();
            System.IDisposable lease = input.Controller.SuppressShortcuts();
            input.Controller.SetContext(InputContext.Menu);

            lease.Dispose();
            input.Press(Key.F9);

            Assert.Zero(input.LoadRequests);
        }

        private sealed class ShortcutFixture : System.IDisposable
        {
            private readonly InputTestFixture _input = new();
            private readonly GameObject _root;
            private readonly Keyboard _keyboard;

            public AppInputController Controller { get; }
            public CancelStack CancelStack { get; } = new();
            public int LoadRequests { get; private set; }
            public int MenuShortcutPerformances { get; private set; }

            /// <summary>
            /// Creates an isolated keyboard and the application shortcut owner.
            /// </summary>
            public ShortcutFixture()
            {
                _input.Setup();
                _root = new GameObject("Shortcut suppression test");
                InputManager manager = _root.AddComponent<InputManager>();
                Controller = _root.AddComponent<AppInputController>();
                _keyboard = InputSystem.AddDevice<Keyboard>();
                Controller.Initialize(
                    manager,
                    CancelStack,
                    null,
                    _ =>
                    {
                        LoadRequests++;
                        return Task.FromResult(true);
                    }
                );
                manager.Asset.FindAction("Global/OpenGameMenu").performed += _ =>
                    MenuShortcutPerformances++;
            }

            /// <summary>
            /// Sends fresh, ordered key presses through the configured input action asset.
            /// </summary>
            /// <param name="keys">The keys to press together.</param>
            public void Press(params Key[] keys)
            {
                foreach (Key key in keys)
                {
                    _input.currentTime += 0.01;
                    _input.Press(_keyboard[key]);
                }
                for (int index = keys.Length - 1; index >= 0; index--)
                {
                    _input.currentTime += 0.01;
                    _input.Release(_keyboard[keys[index]]);
                }
            }

            /// <summary>
            /// Releases the owned input controller and restores the input test environment.
            /// </summary>
            public void Dispose()
            {
                Object.DestroyImmediate(_root);
                _input.TearDown();
            }
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
