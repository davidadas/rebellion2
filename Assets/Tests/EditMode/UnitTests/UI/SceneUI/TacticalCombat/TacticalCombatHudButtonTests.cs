using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatHudButtonTests
    {
        private GameObject _root;
        private EventSystem _events;
        private TacticalCombatHudButton _button;
        private Texture2D _artwork;
        private Sprite _up;
        private Sprite _down;

        /// <summary>
        /// Creates one active image button and its pointer event owner.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Tactical control test");
            GameObject events = new("Event system", typeof(EventSystem));
            events.transform.SetParent(_root.transform);
            _events = events.GetComponent<EventSystem>();
            GameObject image = new(
                "Button",
                typeof(RectTransform),
                typeof(Image),
                typeof(TacticalCombatHudButton)
            );
            image.transform.SetParent(_root.transform);
            _button = image.GetComponent<TacticalCombatHudButton>();
            _button.targetGraphic = image.GetComponent<Image>();
            _button.navigation = new Navigation { mode = Navigation.Mode.None };
            _artwork = new Texture2D(2, 1);
            _up = Sprite.Create(_artwork, new Rect(0, 0, 1, 1), Vector2.zero);
            _down = Sprite.Create(_artwork, new Rect(1, 0, 1, 1), Vector2.zero);
            image.GetComponent<Image>().sprite = _up;
            _button.transition = Selectable.Transition.SpriteSwap;
            _button.spriteState = new SpriteState
            {
                pressedSprite = _down,
                highlightedSprite = _up,
                selectedSprite = _up,
                disabledSprite = _up,
            };
        }

        /// <summary>
        /// Releases the authored image and pointer event owner.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_up);
            Object.DestroyImmediate(_down);
            Object.DestroyImmediate(_artwork);
        }

        [Test]
        public void ApplyKeyboardActivation_Click_DispatchesWithReleasedArtwork()
        {
            int presses = 0;
            _button.onClick.AddListener(() =>
            {
                Assert.AreSame(_up, ((Image)_button.targetGraphic).overrideSprite);
                presses++;
            });

            _button.ApplyKeyboardActivation(true);

            Assert.AreEqual(1, presses);
        }

        [Test]
        public void ApplyKeyboardActivation_KeyRelease_DoesNotDispatchAnotherClick()
        {
            int presses = 0;
            _button.onClick.AddListener(() => presses++);
            _button.ApplyKeyboardActivation(true);

            _button.ApplyKeyboardActivation(false);

            Assert.AreEqual(1, presses);
        }

        [Test]
        public void ApplyKeyboardActivation_DisabledControl_DoesNotDispatchClick()
        {
            int presses = 0;
            _button.onClick.AddListener(() => presses++);
            _button.interactable = false;

            _button.ApplyKeyboardActivation(true);

            Assert.AreEqual(0, presses);
        }

        [Test]
        public void ApplyKeyboardActivation_DisabledDuringPointerPress_CancelsClickAfterReenabling()
        {
            int presses = 0;
            _button.onClick.AddListener(() => presses++);
            PointerEventData pointer = new(_events) { button = PointerEventData.InputButton.Left };
            _button.OnPointerDown(pointer);
            _button.interactable = false;

            _button.ApplyKeyboardActivation(true);
            _button.interactable = true;
            _button.OnPointerUp(pointer);
            _button.OnPointerClick(pointer);

            Assert.AreEqual(0, presses);
        }

        [Test]
        public void ApplyKeyboardActivation_KeyReleaseDuringPointerPress_CancelsItsLaterClick()
        {
            int presses = 0;
            _button.onClick.AddListener(() => presses++);
            PointerEventData pointer = new(_events) { button = PointerEventData.InputButton.Left };
            _button.OnPointerDown(pointer);
            Assert.AreSame(_down, ((Image)_button.targetGraphic).overrideSprite);

            _button.ApplyKeyboardActivation(false);
            _button.OnPointerUp(pointer);
            _button.OnPointerClick(pointer);

            Assert.AreSame(_up, ((Image)_button.targetGraphic).overrideSprite);
            Assert.AreEqual(0, presses);
        }

        [Test]
        public void ApplyKeyboardActivation_ClickDuringPointerPress_PreservesLaterPointerRelease()
        {
            int presses = 0;
            _button.onClick.AddListener(() => presses++);
            PointerEventData pointer = new(_events) { button = PointerEventData.InputButton.Left };
            _button.OnPointerDown(pointer);

            _button.ApplyKeyboardActivation(true);
            _button.OnPointerUp(pointer);
            _button.OnPointerClick(pointer);

            Assert.AreEqual(2, presses);
        }

        [Test]
        public void ApplyKeyboardActivation_KeyReleaseAfterSyntheticClick_CancelsRemainingPointerClick()
        {
            int presses = 0;
            _button.onClick.AddListener(() => presses++);
            PointerEventData pointer = new(_events) { button = PointerEventData.InputButton.Left };
            _button.OnPointerDown(pointer);
            _button.ApplyKeyboardActivation(true);

            _button.ApplyKeyboardActivation(false);
            _button.OnPointerUp(pointer);
            _button.OnPointerClick(pointer);

            Assert.AreEqual(1, presses);
        }

        [Test]
        public void OnPointerDown_AfterKeyboardPressClear_AdmitsTheNewPhysicalClick()
        {
            int presses = 0;
            _button.onClick.AddListener(() => presses++);
            _button.ApplyKeyboardActivation(false);
            PointerEventData pointer = new(_events) { button = PointerEventData.InputButton.Left };

            _button.OnPointerDown(pointer);
            _button.OnPointerUp(pointer);
            _button.OnPointerClick(pointer);

            Assert.AreEqual(1, presses);
        }

        [Test]
        public void OnPointerClick_PrimaryButtonRelease_DispatchesOnceAfterPress()
        {
            int presses = 0;
            _button.onClick.AddListener(() => presses++);
            PointerEventData pointer = new(_events) { button = PointerEventData.InputButton.Left };

            _button.OnPointerDown(pointer);
            Assert.AreEqual(0, presses);
            _button.OnPointerUp(pointer);
            _button.OnPointerClick(pointer);

            Assert.AreEqual(1, presses);
        }

        [Test]
        public void OnPointerDown_SecondaryButton_DoesNotDispatchControl()
        {
            int presses = 0;
            _button.onClick.AddListener(() => presses++);

            PointerEventData pointer = new(_events) { button = PointerEventData.InputButton.Right };
            _button.OnPointerDown(pointer);
            _button.OnPointerUp(pointer);
            _button.OnPointerClick(pointer);

            Assert.AreEqual(0, presses);
        }

        [Test]
        public void OnPointerDown_DisabledControl_DoesNotDispatchControl()
        {
            int presses = 0;
            _button.onClick.AddListener(() => presses++);
            _button.interactable = false;

            PointerEventData pointer = new(_events) { button = PointerEventData.InputButton.Left };
            _button.OnPointerDown(pointer);
            _button.OnPointerUp(pointer);
            _button.OnPointerClick(pointer);

            Assert.AreEqual(0, presses);
        }
    }
}
