using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Rebellion.Tests.UI.SceneUI.OptionsMenu
{
    [TestFixture]
    public sealed class OptionsBindingEditorTests
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

            string decreaseSignature = OptionsBindingEditor.GetBindingSignature(
                decrease,
                FindBinding(decrease, "PrimaryChord")
            );
            string increaseSignature = OptionsBindingEditor.GetBindingSignature(
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
                OptionsBindingEditor.GetBindingSignature(expected, expectedChord),
                OptionsBindingEditor.GetBindingSignature(rebound, reboundChord)
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
                OptionsBindingEditor.GetBindingSignature(plain, plainIndex),
                OptionsBindingEditor.GetBindingSignature(chord, chordIndex)
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
