using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Screen
{
    [TestFixture]
    public sealed class StrategyShortcutBindingsTests
    {
        private InputTestFixture _inputFixture;
        private InputActionAsset _asset;
        private Keyboard _keyboard;
        private StrategyShortcutBindings _bindings;
        private bool _canExecute;
        private int _commandCount;

        /// <summary>
        /// Creates an isolated keyboard and strategy shortcut asset for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _inputFixture = new InputTestFixture();
            _inputFixture.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = _asset.AddActionMap("Strategy");
            map.AddAction("TestCommand", InputActionType.Button, "<Keyboard>/space");
            map.AddAction("BookmarkSlot3", InputActionType.Button, "<Keyboard>/3");
            map.AddAction("OpenPlanetFinder", InputActionType.Button, "<Keyboard>/f3");
            map.AddAction("ShowMines", InputActionType.Button, "<Keyboard>/m");
            _canExecute = true;
            _commandCount = 0;
            _bindings = new StrategyShortcutBindings(_asset, () => _canExecute);
        }

        /// <summary>
        /// Releases owned subscriptions and restores the global input system.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _bindings.Dispose();
            Object.DestroyImmediate(_asset);
            _inputFixture.TearDown();
        }

        [Test]
        public void Bind_PerformedInput_InvokesCommandOnce()
        {
            _bindings.Bind("Strategy/TestCommand", () => _commandCount++);

            PressShortcut();

            Assert.AreEqual(1, _commandCount);
        }

        [Test]
        public void Bind_BlockedInput_DoesNotInvokeCommand()
        {
            _bindings.Bind("Strategy/TestCommand", () => _commandCount++);
            _canExecute = false;

            PressShortcut();

            Assert.AreEqual(0, _commandCount);
        }

        [Test]
        public void Bind_ExecutionPermissionChanges_UsesCurrentPermission()
        {
            _bindings.Bind("Strategy/TestCommand", () => _commandCount++);
            _canExecute = false;
            PressShortcut();
            _canExecute = true;

            PressShortcut();

            Assert.AreEqual(1, _commandCount);
        }

        [Test]
        public void Bind_MissingAction_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bindings.Bind("Strategy/Missing", () => _commandCount++));
        }

        [Test]
        public void Dispose_BoundAction_DetachesCommand()
        {
            _bindings.Bind("Strategy/TestCommand", () => _commandCount++);
            _bindings.Dispose();

            PressShortcut();

            Assert.AreEqual(0, _commandCount);
        }

        [Test]
        public void Dispose_SharedAction_PreservesOtherSubscribers()
        {
            _bindings.Bind("Strategy/TestCommand", () => _commandCount++);
            int otherCount = 0;
            _asset.FindAction("Strategy/TestCommand").performed += _ => otherCount++;
            _bindings.Dispose();

            PressShortcut();

            Assert.AreEqual(1, otherCount);
        }

        [Test]
        public void BindCommands_BookmarkShortcut_UsesZeroBasedSlot()
        {
            int slot = -1;
            _bindings.BindCommands(
                value => slot = value,
                _ => { },
                () => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { }
            );

            PressShortcut(Key.Digit3);

            Assert.AreEqual(2, slot);
        }

        [Test]
        public void BindCommands_PlanetFinderShortcut_OpensTroopFinder()
        {
            FinderMode? mode = null;
            _bindings.BindCommands(
                _ => { },
                value => mode = value,
                () => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { }
            );

            PressShortcut(Key.F3);

            Assert.AreEqual(FinderMode.Troops, mode);
        }

        [Test]
        public void BindCommands_FilterShortcut_SelectsConfiguredFilter()
        {
            GalacticInformationFilterMode? filter = null;
            _bindings.BindCommands(
                _ => { },
                _ => { },
                () => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                value => filter = value
            );

            PressShortcut(Key.M);

            Assert.AreEqual(GalacticInformationFilterMode.Mines, filter);
        }

        /// <summary>
        /// Sends one complete keyboard press and release through the input system.
        /// </summary>
        /// <param name="key">The shortcut key to press and release.</param>
        private void PressShortcut(Key key = Key.Space)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            InputSystem.Update();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
        }
    }
}
