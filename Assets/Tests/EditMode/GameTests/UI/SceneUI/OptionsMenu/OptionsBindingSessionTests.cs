using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Rebellion.Tests.UI.SceneUI.OptionsMenu
{
    [TestFixture]
    public sealed class OptionsBindingSessionTests
    {
        private GameObject _root;
        private InputManager _inputManager;

        /// <summary>
        /// Creates the generated input asset used by each binding-schema test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("InputManager");
            _inputManager = _root.AddComponent<InputManager>();
        }

        /// <summary>
        /// Destroys the generated input asset after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// Verifies that authored modifier composites include their actual modifier and key paths.
        /// </summary>
        [Test]
        public void CompositeSignatures_UseAuthoredModifierAndBindingParts()
        {
            InputAction decrease = _inputManager.Asset.FindAction(
                "Strategy/DecreaseGameSpeed",
                true
            );
            InputAction increase = _inputManager.Asset.FindAction(
                "Strategy/IncreaseGameSpeed",
                true
            );

            string decreaseSignature = OptionsBindingSession.GetBindingSignature(
                decrease,
                FindBinding(decrease, "PrimaryChord")
            );
            string increaseSignature = OptionsBindingSession.GetBindingSignature(
                increase,
                FindBinding(increase, "PrimaryChord")
            );

            StringAssert.Contains("<Keyboard>/ctrl", decreaseSignature);
            StringAssert.Contains("<Keyboard>/minus", decreaseSignature);
            Assert.AreNotEqual(decreaseSignature, increaseSignature);
        }

        /// <summary>
        /// Verifies that an overridden authored composite matches an equivalent default chord.
        /// </summary>
        [Test]
        public void CompositeSignatures_EquivalentChordOverridesMatch()
        {
            InputAction expected = _inputManager.Asset.FindAction(
                "Strategy/DecreaseGameSpeed",
                true
            );
            InputAction rebound = _inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            int expectedChord = FindBinding(expected, "PrimaryChord");
            int reboundChord = FindBinding(rebound, "PrimaryChord");
            rebound.ApplyBindingOverride(
                FindPart(rebound, reboundChord, "Modifier"),
                "<Keyboard>/ctrl"
            );
            rebound.ApplyBindingOverride(
                FindPart(rebound, reboundChord, "Binding"),
                "<Keyboard>/minus"
            );

            Assert.AreEqual(
                OptionsBindingSession.GetBindingSignature(expected, expectedChord),
                OptionsBindingSession.GetBindingSignature(rebound, reboundChord)
            );
        }

        /// <summary>
        /// Verifies that a plain key does not conflict with a modifier chord using the same base key.
        /// </summary>
        [Test]
        public void BindingSignatures_DistinguishPlainKeyFromModifiedChord()
        {
            InputAction plain = _inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            InputAction chord = _inputManager.Asset.FindAction(
                "Strategy/ShowFighterSquadrons",
                true
            );
            int plainIndex = FindBinding(plain, "Primary");
            int chordIndex = FindBinding(chord, "PrimaryChord");
            plain.ApplyBindingOverride(plainIndex, "<Keyboard>/b");
            chord.ApplyBindingOverride(FindPart(chord, chordIndex, "Modifier"), "<Keyboard>/ctrl");
            chord.ApplyBindingOverride(FindPart(chord, chordIndex, "Binding"), "<Keyboard>/b");

            Assert.AreNotEqual(
                OptionsBindingSession.GetBindingSignature(plain, plainIndex),
                OptionsBindingSession.GetBindingSignature(chord, chordIndex)
            );
        }

        /// <summary>
        /// Verifies the project enables Unity's shortcut consumption required by authored chords.
        /// </summary>
        [Test]
        public void ProjectInputSettings_ShortcutConsumption_IsEnabled()
        {
            Assert.IsTrue(InputSystem.settings.shortcutKeysConsumeInput);
        }

        /// <summary>
        /// Verifies the dedicated game-menu chord is exposed with its player-facing label.
        /// </summary>
        [Test]
        public void Rebuild_GlobalBindings_IncludesOpenGameMenu()
        {
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);
            InputAction openGameMenu = _inputManager.Asset.FindAction("Global/OpenGameMenu", true);

            session.Rebuild();

            OptionsBindingRow row = session.Rows.Single(row => row.Action == "Open Game Menu");
            Assert.IsFalse(row.IsHeader);
            Assert.AreEqual("SHIFT+ESC", row.Primary);
            Assert.IsFalse(row.PrimaryEditable);
            int chord = FindBinding(openGameMenu, "PrimaryChord");
            Assert.AreEqual("<Keyboard>/shift", openGameMenu.bindings[chord + 1].effectivePath);
            Assert.AreEqual("<Keyboard>/escape", openGameMenu.bindings[chord + 2].effectivePath);
        }

        /// <summary>
        /// Verifies selection controls expose only the two configured selection behaviors.
        /// </summary>
        [Test]
        public void Rebuild_SelectionModifiers_ExposeToggleAndRangeOnly()
        {
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);
            session.Rebuild();

            string[] labels = session.Rows.Select(row => row.Action).ToArray();

            CollectionAssert.Contains(labels, "Toggle Selection Modifier");
            CollectionAssert.Contains(labels, "Range Selection Modifier");
            CollectionAssert.DoesNotContain(labels, "Alternate Select Modifier");
        }

        /// <summary>
        /// Verifies the reserved Escape slot cannot enter interactive capture.
        /// </summary>
        [Test]
        public void BeginRebind_OpenGameMenuPrimary_DoesNotStartCapture()
        {
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);
            session.Rebuild();
            int row = session
                .Rows.Select((binding, index) => (binding, index))
                .Single(item => item.binding.Action == "Open Game Menu")
                .index;

            session.BeginRebind(row, false);

            Assert.AreEqual(-1, session.ListeningRow);
        }

        /// <summary>
        /// Verifies restoring one row leaves overrides on other actions intact.
        /// </summary>
        [Test]
        public void RestoreDefault_OneBinding_RestoresOnlySelectedAction()
        {
            InputAction troopers = _inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            InputAction fighters = _inputManager.Asset.FindAction(
                "Strategy/ShowFighterSquadrons",
                true
            );
            troopers.ApplyBindingOverride(FindBinding(troopers, "Primary"), "<Keyboard>/n");
            fighters.ApplyBindingOverride(FindBinding(fighters, "Primary"), "<Keyboard>/m");
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);
            session.Rebuild();
            int row = session
                .Rows.Select((binding, index) => (binding, index))
                .Single(item => item.binding.Action == "Show Troopers")
                .index;

            session.RestoreDefault(row);

            Assert.IsNull(troopers.bindings[FindBinding(troopers, "Primary")].overridePath);
            Assert.AreEqual(
                "<Keyboard>/m",
                fighters.bindings[FindBinding(fighters, "Primary")].overridePath
            );
        }

        /// <summary>
        /// Verifies restoring all bindings removes overrides throughout the bindable maps.
        /// </summary>
        [Test]
        public void RestoreAllDefaults_MultipleBindings_RemovesEveryOverride()
        {
            InputAction troopers = _inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            InputAction quickSave = _inputManager.Asset.FindAction("Global/QuickSave", true);
            troopers.ApplyBindingOverride(FindBinding(troopers, "Primary"), "<Keyboard>/n");
            quickSave.ApplyBindingOverride(FindBinding(quickSave, "Primary"), "<Keyboard>/m");
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);
            session.Rebuild();

            session.RestoreAllDefaults();

            Assert.IsFalse(
                _inputManager
                    .Asset.actionMaps.SelectMany(map => map.actions)
                    .SelectMany(action => action.bindings)
                    .Any(binding => binding.hasOverrides)
            );
        }

        /// <summary>
        /// Verifies an unbound secondary slot can begin capture through the temporary listening action.
        /// </summary>
        [Test]
        public void BeginRebind_UnboundSecondarySlot_StartsInteractiveCapture()
        {
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);
            session.Rebuild();
            int row = session
                .Rows.Select((binding, index) => (binding, index))
                .First(item => item.binding.Action == "Show Troopers")
                .index;

            Assert.AreEqual("UNBOUND", session.Rows[row].Secondary);
            Assert.DoesNotThrow(() => session.BeginRebind(row, true));

            Assert.AreEqual(row, session.ListeningRow);
            Assert.IsTrue(session.ListeningSecondary);
            session.CancelRebind();
            Assert.AreEqual(-1, session.ListeningRow);
        }

        /// <summary>
        /// Verifies Escape cancels binding capture without leaking into the global menu command.
        /// </summary>
        [Test]
        public void BeginRebind_Escape_CancelsCaptureWithoutPerformingGlobalShortcut()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject inputRoot = new("IsolatedInputManager");
            InputManager inputManager = inputRoot.AddComponent<InputManager>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            InputAction cancel = inputManager.Asset.FindAction("Global/CancelOrSettings", true);
            int cancelCount = 0;
            cancel.performed += _ => cancelCount++;
            cancel.actionMap.Enable();

            try
            {
                using OptionsBindingSession session = new OptionsBindingSession(inputManager);
                session.Rebuild();
                int row = session
                    .Rows.Select((binding, index) => (binding, index))
                    .First(item => item.binding.Action == "Show Troopers")
                    .index;
                session.BeginRebind(row, false);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();

                Assert.AreEqual(-1, session.ListeningRow);
                Assert.AreEqual(0, cancelCount);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();

                Assert.AreEqual(1, cancelCount);
            }
            finally
            {
                cancel.actionMap.Disable();
                Object.DestroyImmediate(inputRoot);
                inputFixture.TearDown();
            }
        }

        /// <summary>
        /// Verifies a modifier chord consumes its base key instead of firing the plain shortcut.
        /// </summary>
        [Test]
        public void AuthoredChord_ModifierHeld_ConsumesPlainBaseKeyAction()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject inputRoot = new("IsolatedInputManager");
            InputManager inputManager = inputRoot.AddComponent<InputManager>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            InputAction chord = inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            InputAction plain = inputManager.Asset.FindAction(
                "Strategy/ShowFighterSquadrons",
                true
            );
            InputActionMap strategy = chord.actionMap;
            int chordComposite = FindBinding(chord, "PrimaryChord");
            chord.ApplyBindingOverride(FindBinding(chord, "Primary"), string.Empty);
            chord.ApplyBindingOverride(
                FindPart(chord, chordComposite, "Modifier"),
                "<Keyboard>/ctrl"
            );
            chord.ApplyBindingOverride(FindPart(chord, chordComposite, "Binding"), "<Keyboard>/b");
            plain.ApplyBindingOverride(FindBinding(plain, "Primary"), "<Keyboard>/b");
            int chordCount = 0;
            int plainCount = 0;
            chord.performed += _ => chordCount++;
            plain.performed += _ => plainCount++;

            try
            {
                InputSystem.settings.shortcutKeysConsumeInput = true;
                strategy.Enable();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.B));
                InputSystem.Update();

                Assert.AreEqual(1, chordCount);
                Assert.AreEqual(0, plainCount);
            }
            finally
            {
                strategy.Disable();
                Object.DestroyImmediate(inputRoot);
                inputFixture.TearDown();
            }
        }

        /// <summary>
        /// Finds a top-level authored binding by name.
        /// </summary>
        private static int FindBinding(InputAction action, string name)
        {
            for (int index = 0; index < action.bindings.Count; index++)
            {
                InputBinding binding = action.bindings[index];
                if (!binding.isPartOfComposite && binding.name == name)
                    return index;
            }
            Assert.Fail($"Binding '{name}' was not found on {action}.");
            return -1;
        }

        /// <summary>
        /// Finds a named part belonging to one authored composite.
        /// </summary>
        private static int FindPart(InputAction action, int compositeIndex, string name)
        {
            for (int index = compositeIndex + 1; index < action.bindings.Count; index++)
            {
                InputBinding binding = action.bindings[index];
                if (!binding.isPartOfComposite)
                    break;
                if (binding.name == name)
                    return index;
            }
            Assert.Fail($"Composite part '{name}' was not found on {action}.");
            return -1;
        }
    }
}
