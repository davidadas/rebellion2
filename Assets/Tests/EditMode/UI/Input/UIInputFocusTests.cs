using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rebellion.Tests.UI.Input
{
    [TestFixture]
    public class UIInputFocusTests
    {
        private EventSystem _eventSystem;
        private GameObject _eventSystemObject;
        private TMP_InputField _inputField;
        private GameObject _inputObject;
        private GameObject _selectedChild;

        /// <summary>
        /// Creates one selected TMP input hierarchy for each focus test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            _eventSystem = _eventSystemObject.GetComponent<EventSystem>();
            _inputObject = new GameObject("InputField", typeof(RectTransform));
            _inputField = _inputObject.AddComponent<TMP_InputField>();
            _selectedChild = new GameObject("TextArea", typeof(RectTransform));
            _selectedChild.transform.SetParent(_inputObject.transform, false);
            _eventSystem.SetSelectedGameObject(_selectedChild);
        }

        /// <summary>
        /// Destroys the temporary input and event-system hierarchy after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_inputObject);
            Object.DestroyImmediate(_eventSystemObject);
        }

        /// <summary>
        /// Verifies a focused input field owns keyboard input even when its child is selected.
        /// </summary>
        [Test]
        public void IsTextEntryActive_FocusedParentInputField_ReturnsTrue()
        {
            SetInputFocus(true);

            Assert.IsTrue(UIInputFocus.IsTextEntryActive());
        }

        /// <summary>
        /// Verifies selecting an unfocused input hierarchy does not suppress shortcuts.
        /// </summary>
        [Test]
        public void IsTextEntryActive_UnfocusedInputField_ReturnsFalse()
        {
            SetInputFocus(false);

            Assert.IsFalse(UIInputFocus.IsTextEntryActive());
        }

        /// <summary>
        /// Verifies a stale focused flag on a disabled field does not suppress shortcuts.
        /// </summary>
        [Test]
        public void IsTextEntryActive_DisabledInputField_ReturnsFalse()
        {
            SetInputFocus(true);
            _inputField.enabled = false;

            Assert.IsFalse(UIInputFocus.IsTextEntryActive());
        }

        /// <summary>
        /// Sets TMP's runtime focus state without requiring a player-frame update.
        /// </summary>
        /// <param name="focused">Whether the test input field owns text entry.</param>
        private void SetInputFocus(bool focused)
        {
            typeof(TMP_InputField)
                .GetField("m_AllowInput", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(_inputField, focused);
        }
    }
}
