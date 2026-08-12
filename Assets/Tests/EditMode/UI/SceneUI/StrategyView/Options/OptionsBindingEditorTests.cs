using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Options
{
    [TestFixture]
    public sealed class OptionsBindingEditorTests
    {
        private GameObject _root;
        private InputManager _inputManager;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("InputManager");
            _inputManager = _root.AddComponent<InputManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void CompositeSignatures_UseActualModifierAndButtonParts()
        {
            InputAction decrease = _inputManager.Asset.FindAction(
                "Strategy/DecreaseGameSpeed",
                true
            );
            InputAction increase = _inputManager.Asset.FindAction(
                "Strategy/IncreaseGameSpeed",
                true
            );
            int decreasePrimary = InputBindingStore.GetTopLevelBindingIndex(decrease, 0);
            int increasePrimary = InputBindingStore.GetTopLevelBindingIndex(increase, 0);

            string decreaseSignature = OptionsBindingEditor.GetBindingSignature(
                decrease,
                decreasePrimary
            );
            string increaseSignature = OptionsBindingEditor.GetBindingSignature(
                increase,
                increasePrimary
            );

            StringAssert.Contains("<Keyboard>/minus", decreaseSignature);
            StringAssert.EndsWith($"|{KeyboardChordProcessor.Control}", decreaseSignature);
            Assert.AreNotEqual(decreaseSignature, increaseSignature);
        }

        [Test]
        public void CompositeAndProcessorSignatures_MatchForEquivalentChord()
        {
            InputAction composite = _inputManager.Asset.FindAction(
                "Strategy/DecreaseGameSpeed",
                true
            );
            InputAction processor = _inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            int compositeIndex = InputBindingStore.GetTopLevelBindingIndex(composite, 0);
            int processorIndex = InputBindingStore.GetTopLevelBindingIndex(processor, 0);
            processor.ApplyBindingOverride(
                processorIndex,
                new InputBinding
                {
                    overridePath = "<Keyboard>/minus",
                    overrideProcessors = KeyboardChordProcessor.GetProcessorOverride(
                        KeyboardChordProcessor.Control
                    ),
                }
            );

            Assert.AreEqual(
                OptionsBindingEditor.GetBindingSignature(composite, compositeIndex),
                OptionsBindingEditor.GetBindingSignature(processor, processorIndex)
            );
        }

        [Test]
        public void ProcessorSignatures_DistinguishPlainKeyFromModifiedChord()
        {
            InputAction first = _inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            InputAction second = _inputManager.Asset.FindAction(
                "Strategy/ShowFighterSquadrons",
                true
            );
            int firstIndex = InputBindingStore.GetTopLevelBindingIndex(first, 0);
            int secondIndex = InputBindingStore.GetTopLevelBindingIndex(second, 0);
            first.ApplyBindingOverride(firstIndex, "<Keyboard>/b");
            second.ApplyBindingOverride(
                secondIndex,
                new InputBinding
                {
                    overridePath = "<Keyboard>/b",
                    overrideProcessors = KeyboardChordProcessor.GetProcessorOverride(
                        KeyboardChordProcessor.Control
                    ),
                }
            );

            Assert.AreNotEqual(
                OptionsBindingEditor.GetBindingSignature(first, firstIndex),
                OptionsBindingEditor.GetBindingSignature(second, secondIndex)
            );
        }
    }
}
