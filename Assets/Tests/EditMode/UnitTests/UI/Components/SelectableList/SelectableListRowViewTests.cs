using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components.SelectableList
{
    [TestFixture]
    public class SelectableListRowViewTests
    {
        private EventSystem _eventSystem;
        private GameObject _eventSystemObject;
        private RawImage _hitArea;
        private GameObject _listObject;
        private TestSelectableListRowView _row;
        private GameObject _rowObject;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            _eventSystem = _eventSystemObject.GetComponent<EventSystem>();
            UIComponentTestHelper.InvokeLifecycle(_eventSystem, "OnEnable");
            _listObject = new GameObject("List", typeof(RectTransform));
            _row = CreateRow("Row");
            _rowObject = _row.gameObject;
            _hitArea = _rowObject.GetComponent<RawImage>();
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_listObject);
            UIComponentTestHelper.InvokeLifecycle(_eventSystem, "OnDisable");
            Object.DestroyImmediate(_eventSystemObject);
        }

        /// <summary>
        /// Verifies configure selectable row disabled row enables row and hit area.
        /// </summary>
        [Test]
        public void ConfigureSelectableRow_DisabledRow_EnablesRowAndHitArea()
        {
            _row.enabled = false;
            _hitArea.enabled = false;
            _hitArea.raycastTarget = false;
            _hitArea.canvasRenderer.cullTransparentMesh = true;

            _row.Configure(7, _hitArea);

            Assert.IsTrue(_row.enabled);
            Assert.AreEqual(7, _row.Index);
            Assert.IsTrue(_hitArea.enabled);
            Assert.IsTrue(_hitArea.raycastTarget);
            Assert.IsFalse(_hitArea.canvasRenderer.cullTransparentMesh);
        }

        /// <summary>
        /// Verifies configure selectable row null hit area throws missing reference exception.
        /// </summary>
        [Test]
        public void ConfigureSelectableRow_NullHitArea_ThrowsMissingReferenceException()
        {
            Assert.Throws<MissingReferenceException>(() => _row.Configure(0, null));
        }

        /// <summary>
        /// Verifies on pointer click left double click raises activated.
        /// </summary>
        [Test]
        public void OnPointerClick_LeftDoubleClick_RaisesActivated()
        {
            PointerEventData eventData = new PointerEventData(_eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 2,
            };
            SelectableListRowView activatedRow = null;
            PointerEventData receivedEvent = null;
            _row.Activated += (row, pointerEvent) =>
            {
                activatedRow = row;
                receivedEvent = pointerEvent;
            };

            _row.OnPointerClick(eventData);

            Assert.AreSame(_row, activatedRow);
            Assert.AreSame(eventData, receivedEvent);
        }

        /// <summary>
        /// Verifies on pointer click nonactivating clicks do not raise activated.
        /// </summary>
        [Test]
        public void OnPointerClick_NonactivatingClicks_DoNotRaiseActivated()
        {
            int activatedCount = 0;
            _row.Activated += (_, _) => activatedCount++;

            _row.OnPointerClick(
                new PointerEventData(_eventSystem)
                {
                    button = PointerEventData.InputButton.Left,
                    clickCount = 1,
                }
            );
            _row.OnPointerClick(
                new PointerEventData(_eventSystem)
                {
                    button = PointerEventData.InputButton.Right,
                    clickCount = 2,
                }
            );

            Assert.AreEqual(0, activatedCount);
        }

        /// <summary>
        /// Verifies on pointer down left button focuses and raises selected.
        /// </summary>
        [Test]
        public void OnPointerDown_LeftButton_FocusesAndRaisesSelected()
        {
            PointerEventData eventData = new PointerEventData(_eventSystem)
            {
                button = PointerEventData.InputButton.Left,
            };
            SelectableListRowView selectedRow = null;
            PointerEventData receivedEvent = null;
            int contextCount = 0;
            _row.Selected += (row, pointerEvent) =>
            {
                selectedRow = row;
                receivedEvent = pointerEvent;
            };
            _row.ContextRequested += (_, _) => contextCount++;

            _row.OnPointerDown(eventData);

            Assert.AreSame(_row, selectedRow);
            Assert.AreSame(eventData, receivedEvent);
            Assert.AreSame(_rowObject, _eventSystem.currentSelectedGameObject);
            Assert.AreEqual(0, contextCount);
        }

        /// <summary>
        /// Verifies on pointer down right button raises context request only.
        /// </summary>
        [Test]
        public void OnPointerDown_RightButton_RaisesContextRequestOnly()
        {
            PointerEventData eventData = new PointerEventData(_eventSystem)
            {
                button = PointerEventData.InputButton.Right,
            };
            SelectableListRowView contextRow = null;
            PointerEventData receivedEvent = null;
            int selectedCount = 0;
            _row.Selected += (_, _) => selectedCount++;
            _row.ContextRequested += (row, pointerEvent) =>
            {
                contextRow = row;
                receivedEvent = pointerEvent;
            };

            _row.OnPointerDown(eventData);

            Assert.AreSame(_row, contextRow);
            Assert.AreSame(eventData, receivedEvent);
            Assert.AreEqual(0, selectedCount);
        }

        /// <summary>
        /// Verifies on move down selects and focuses next active sibling.
        /// </summary>
        [Test]
        public void OnMove_Down_SelectsAndFocusesNextActiveSibling()
        {
            TestSelectableListRowView nextRow = CreateRow("NextRow");
            AxisEventData eventData = new AxisEventData(_eventSystem)
            {
                moveDir = MoveDirection.Down,
            };
            SelectableListRowView selectedRow = null;
            PointerEventData receivedEvent = null;
            nextRow.Selected += (row, pointerEvent) =>
            {
                selectedRow = row;
                receivedEvent = pointerEvent;
            };

            _row.OnMove(eventData);

            Assert.AreSame(nextRow, selectedRow);
            Assert.IsNull(receivedEvent);
            Assert.AreSame(nextRow.gameObject, _eventSystem.currentSelectedGameObject);
            Assert.IsTrue(eventData.used);
        }

        /// <summary>
        /// Verifies on move disabled navigation does not select sibling.
        /// </summary>
        [Test]
        public void OnMove_DisabledNavigation_DoesNotSelectSibling()
        {
            TestSelectableListRowView nextRow = CreateRow("NextRow");
            AxisEventData eventData = new AxisEventData(_eventSystem)
            {
                moveDir = MoveDirection.Down,
            };
            int selectedCount = 0;
            nextRow.Selected += (_, _) => selectedCount++;
            _row.SetNavigationGate(() => false);

            _row.OnMove(eventData);

            Assert.AreEqual(0, selectedCount);
            Assert.IsFalse(eventData.used);
        }

        /// <summary>
        /// Verifies on submit enabled navigation raises activated and uses event.
        /// </summary>
        [Test]
        public void OnSubmit_EnabledNavigation_RaisesActivatedAndUsesEvent()
        {
            BaseEventData eventData = new BaseEventData(_eventSystem);
            SelectableListRowView activatedRow = null;
            PointerEventData receivedEvent = new PointerEventData(_eventSystem);
            _row.Activated += (row, pointerEvent) =>
            {
                activatedRow = row;
                receivedEvent = pointerEvent;
            };

            _row.OnSubmit(eventData);

            Assert.AreSame(_row, activatedRow);
            Assert.IsNull(receivedEvent);
            Assert.IsTrue(eventData.used);
        }

        /// <summary>
        /// Verifies on submit disabled navigation does not raise activated.
        /// </summary>
        [Test]
        public void OnSubmit_DisabledNavigation_DoesNotRaiseActivated()
        {
            BaseEventData eventData = new BaseEventData(_eventSystem);
            int activatedCount = 0;
            _row.Activated += (_, _) => activatedCount++;
            _row.SetNavigationGate(() => false);

            _row.OnSubmit(eventData);

            Assert.AreEqual(0, activatedCount);
            Assert.IsFalse(eventData.used);
        }

        /// <summary>
        /// Verifies focus row for navigation selection outside scope focuses requested row.
        /// </summary>
        [Test]
        public void FocusRowForNavigation_SelectionOutsideScope_FocusesRequestedRow()
        {
            _eventSystem.SetSelectedGameObject(_eventSystemObject);

            SelectableListRowView.FocusRowForNavigation(_listObject.transform, true, _row);

            Assert.AreSame(_rowObject, _eventSystem.currentSelectedGameObject);
        }

        /// <summary>
        /// Verifies focus row for navigation selection inside scope preserves current selection.
        /// </summary>
        [Test]
        public void FocusRowForNavigation_SelectionInsideScope_PreservesCurrentSelection()
        {
            TestSelectableListRowView otherRow = CreateRow("OtherRow");
            _eventSystem.SetSelectedGameObject(otherRow.gameObject);

            SelectableListRowView.FocusRowForNavigation(_listObject.transform, true, _row);

            Assert.AreSame(otherRow.gameObject, _eventSystem.currentSelectedGameObject);
        }

        /// <summary>
        /// Creates row.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The created row.</returns>
        private TestSelectableListRowView CreateRow(string name)
        {
            GameObject rowObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(TestSelectableListRowView)
            );
            rowObject.transform.SetParent(_listObject.transform, false);
            return rowObject.GetComponent<TestSelectableListRowView>();
        }

        private sealed class TestSelectableListRowView : SelectableListRowView
        {
            /// <summary>
            /// Configures the requested operation.
            /// </summary>
            /// <param name="index">The index.</param>
            /// <param name="hitArea">The hit area.</param>
            public void Configure(int index, RawImage hitArea)
            {
                ConfigureSelectableRow(index, hitArea);
            }
        }
    }
}
