using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class UIPointerGestureRelayTests
    {
        private GameObject _object;
        private UIPointerGestureRelay _relay;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("PointerGestureRelay", typeof(UIPointerGestureRelay));
            _relay = _object.GetComponent<UIPointerGestureRelay>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_object);
        }

        [TestCase(PointerEventData.InputButton.Left)]
        [TestCase(PointerEventData.InputButton.Right)]
        public void OnPointerDown_SupportedButton_RaisesPressed(PointerEventData.InputButton button)
        {
            PointerEventData eventData = new PointerEventData(null) { button = button };
            PointerEventData received = null;
            _relay.Pressed += value => received = value;

            _relay.OnPointerDown(eventData);

            Assert.AreSame(eventData, received);
        }

        [Test]
        public void OnPointerDown_UnsupportedOrNullEvent_DoesNotRaisePressed()
        {
            int pressedCount = 0;
            _relay.Pressed += _ => pressedCount++;

            _relay.OnPointerDown(null);
            _relay.OnPointerDown(
                new PointerEventData(null) { button = PointerEventData.InputButton.Middle }
            );

            Assert.AreEqual(0, pressedCount);
        }

        [Test]
        public void OnPointerClick_LeftDoubleClick_RaisesReleaseAndDoubleClick()
        {
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 2,
            };
            PointerEventData released = null;
            PointerEventData doubleClicked = null;
            _relay.Released += value => released = value;
            _relay.DoubleClicked += value => doubleClicked = value;

            _relay.OnPointerClick(eventData);

            Assert.AreSame(eventData, released);
            Assert.AreSame(eventData, doubleClicked);
        }

        [Test]
        public void OnPointerClick_LeftSingleClick_RaisesReleaseOnly()
        {
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
            };
            int releasedCount = 0;
            int doubleClickedCount = 0;
            _relay.Released += _ => releasedCount++;
            _relay.DoubleClicked += _ => doubleClickedCount++;

            _relay.OnPointerClick(eventData);

            Assert.AreEqual(1, releasedCount);
            Assert.AreEqual(0, doubleClickedCount);
        }

        [Test]
        public void OnPointerClick_NonprimaryOrNullEvent_DoesNotRaiseClickEvents()
        {
            int releasedCount = 0;
            int doubleClickedCount = 0;
            _relay.Released += _ => releasedCount++;
            _relay.DoubleClicked += _ => doubleClickedCount++;

            _relay.OnPointerClick(null);
            _relay.OnPointerClick(
                new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Right,
                    clickCount = 2,
                }
            );

            Assert.AreEqual(0, releasedCount);
            Assert.AreEqual(0, doubleClickedCount);
        }

        [Test]
        public void OnDrop_ValidThenNullEvent_RaisesOnlyValidDrop()
        {
            PointerEventData eventData = new PointerEventData(null);
            PointerEventData received = null;
            int droppedCount = 0;
            _relay.Dropped += value =>
            {
                droppedCount++;
                received = value;
            };

            _relay.OnDrop(eventData);
            _relay.OnDrop(null);

            Assert.AreEqual(1, droppedCount);
            Assert.AreSame(eventData, received);
        }
    }
}
