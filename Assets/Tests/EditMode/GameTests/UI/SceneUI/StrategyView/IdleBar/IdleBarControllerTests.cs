using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.IdleBar
{
    [TestFixture]
    public class IdleBarControllerTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private TestActions _actions;
        private IdleBarController _controller;
        private readonly List<ISceneNode> _resolvedEntities = new List<ISceneNode>();
        private Officer _officer;
        private GameObject _rootObject;
        private SelectionModifierState _selectionModifiers;
        private IdleBarView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<IdleBarView>(true);
            _officer = new Officer { InstanceID = "officer", DisplayName = "Officer" };
            _resolvedEntities.Clear();
            _resolvedEntities.Add(_officer);
            _selectionModifiers = default;
            _actions = new TestActions();
            _controller = new IdleBarController(
                () => null,
                () => false,
                () => _selectionModifiers,
                () => null,
                () => true,
                ResolveEntity
            );
            _controller.Initialize(_actions);
            _controller.BindView(_view);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void BindView_BeforeInitialize_Throws()
        {
            IdleBarController controller = new IdleBarController(
                () => null,
                () => false,
                () => default,
                () => null,
                () => false,
                _ => null
            );

            Assert.Throws<InvalidOperationException>(() => controller.BindView(_view));
        }

        [Test]
        public void SelectEntry_ResolvesAndRoutesEntity()
        {
            RenderOfficerDirectly();

            _view
                .GetComponentInChildren<IdleBarSlotView>(false)
                .GetComponent<Button>()
                .onClick.Invoke();

            Assert.AreSame(_officer, _actions.OpenedTarget);
        }

        [Test]
        public void ShiftSelectEntry_TogglesSelectionWithoutOpeningEntity()
        {
            _selectionModifiers = new SelectionModifierState(multiSelect: true, rangeSelect: false);
            RenderEntriesDirectly(_officer);

            ClickSlot(0);

            Assert.IsTrue(_controller.IsSelected(_officer));
            Assert.IsNull(_actions.OpenedTarget);

            ClickSlot(0);

            Assert.IsFalse(_controller.IsSelected(_officer));
        }

        [Test]
        public void ShiftSelectEntries_OfficerAndSpecialForcesSharePersonnelSelection()
        {
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "special-forces",
                DisplayName = "Special Forces",
            };
            _resolvedEntities.Add(specialForces);
            _selectionModifiers = new SelectionModifierState(multiSelect: true, rangeSelect: false);
            RenderEntriesDirectly(_officer, specialForces);

            ClickSlot(0);
            ClickSlot(1);

            Assert.IsTrue(_controller.IsSelected(_officer));
            Assert.IsTrue(_controller.IsSelected(specialForces));
        }

        [Test]
        public void EntryDrag_SelectedPersonnel_RoutesCompleteSelection()
        {
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "special-forces",
                DisplayName = "Special Forces",
            };
            _resolvedEntities.Add(specialForces);
            _selectionModifiers = new SelectionModifierState(multiSelect: true, rangeSelect: false);
            RenderEntriesDirectly(_officer, specialForces);
            ClickSlot(0);
            ClickSlot(1);
            _selectionModifiers = default;
            IdleBarSlotView slot = GetEntitySlots().First();

            slot.OnPointerDown(CreatePointerEvent(slot.gameObject));

            CollectionAssert.AreEquivalent(
                new ISceneNode[] { _officer, specialForces },
                _actions.DraggedTargets
            );
        }

        [Test]
        public void EntryDrag_ShiftHeld_DoesNotStartDragCandidate()
        {
            _selectionModifiers = new SelectionModifierState(multiSelect: true, rangeSelect: false);
            RenderEntriesDirectly(_officer);
            IdleBarSlotView slot = GetEntitySlots().Single();

            slot.OnPointerDown(CreatePointerEvent(slot.gameObject));

            Assert.IsNull(_actions.DraggedTargets);
        }

        [Test]
        public void ShiftSelectEntry_IncompatibleTypeReplacesCurrentSelection()
        {
            Planet planet = new Planet { InstanceID = "planet", DisplayName = "Planet" };
            _resolvedEntities.Add(planet);
            _selectionModifiers = new SelectionModifierState(multiSelect: true, rangeSelect: false);
            RenderEntriesDirectly(_officer, planet);

            ClickSlot(0);
            ClickSlot(1);

            Assert.IsFalse(_controller.IsSelected(_officer));
            Assert.IsTrue(_controller.IsSelected(planet));
        }

        [Test]
        public void UnmodifiedSelectEntry_ClearsMultiSelectionAndOpensEntity()
        {
            _selectionModifiers = new SelectionModifierState(multiSelect: true, rangeSelect: false);
            RenderEntriesDirectly(_officer);
            ClickSlot(0);
            _selectionModifiers = default;

            ClickSlot(0);

            Assert.IsFalse(_controller.IsSelected(_officer));
            Assert.AreSame(_officer, _actions.OpenedTarget);
        }

        [Test]
        public void PrimaryPressOutsideIdleBar_ClearsMultiSelection()
        {
            _selectionModifiers = new SelectionModifierState(multiSelect: true, rangeSelect: false);
            RenderEntriesDirectly(_officer);
            ClickSlot(0);

            _controller.ClearSelectionOutside(new Vector2(-10000f, -10000f));

            Assert.IsFalse(_controller.IsSelected(_officer));
        }

        [Test]
        public void ToggleTracking_ChangesStateAndRequestsRender()
        {
            Assert.IsTrue(_controller.IsIdleBarTracked(_officer));

            _controller.ToggleIdleBarTracking(_officer);

            Assert.IsFalse(_controller.IsIdleBarTracked(_officer));
            Assert.AreEqual(1, _actions.RenderRequestCount);

            _controller.ToggleIdleBarTracking(_officer);

            Assert.IsTrue(_controller.IsIdleBarTracked(_officer));
            Assert.AreEqual(2, _actions.RenderRequestCount);
        }

        [Test]
        public void SecondaryClick_ResolvesAndRoutesContextMenuWithoutUntracking()
        {
            RenderOfficerDirectly();
            IdleBarSlotView slot = _view.GetComponentInChildren<IdleBarSlotView>(false);
            PointerEventData rightClick = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
            };
            slot.OnPointerEnter(new PointerEventData(null));

            slot.OnPointerClick(rightClick);

            Assert.IsTrue(_controller.IsIdleBarTracked(_officer));
            Assert.AreSame(_officer, _actions.ContextTarget);
            Assert.AreSame(rightClick, _actions.ContextEventData);
            Assert.AreEqual(0, _actions.RenderRequestCount);
        }

        [Test]
        public void EntryHover_ActiveEntry_HighlightsLocationUntilPointerExits()
        {
            RenderOfficerDirectly();
            IdleBarSlotView slot = _view.GetComponentInChildren<IdleBarSlotView>(false);

            slot.OnPointerEnter(new PointerEventData(null));

            Assert.AreSame(_officer, _actions.HighlightedTarget);

            slot.OnPointerExit(new PointerEventData(null));

            Assert.IsNull(_actions.HighlightedTarget);
        }

        [Test]
        public void EntryDrag_MovableEntity_RoutesCandidateMovementAndCompletion()
        {
            RenderOfficerDirectly();
            IdleBarSlotView slot = _view.GetComponentInChildren<IdleBarSlotView>(false);
            ScrollAreaView scrollArea = _view.GetComponentInChildren<ScrollAreaView>(true);
            PointerEventData eventData = CreatePointerEvent(slot.gameObject);

            slot.OnPointerDown(eventData);
            scrollArea.RelayDrag(eventData);
            scrollArea.RelayDragEnd(eventData);

            CollectionAssert.AreEqual(new ISceneNode[] { _officer }, _actions.DraggedTargets);
            Assert.AreEqual(1, _actions.DragMoveCount);
            Assert.AreEqual(1, _actions.DragEndCount);
        }

        [Test]
        public void EntryDrag_Planet_DoesNotStartItemDrag()
        {
            Planet planet = new Planet { InstanceID = "planet", DisplayName = "Planet" };
            _resolvedEntities.Add(planet);
            _view.Render(
                new IdleBarRenderData(
                    true,
                    new List<IdleBarEntry> { new IdleBarEntry(planet, null) },
                    new RectInt(0, 0, 700, 350)
                )
            );
            IdleBarSlotView slot = _view.GetComponentInChildren<IdleBarSlotView>(false);

            slot.OnPointerDown(CreatePointerEvent(slot.gameObject));

            Assert.IsNull(_actions.DraggedTargets);
        }

        [Test]
        public void ResetSession_RestoresTracking()
        {
            _controller.ToggleIdleBarTracking(_officer);

            _controller.ResetSession();

            Assert.IsTrue(_controller.IsIdleBarTracked(_officer));
        }

        [Test]
        public void Render_DisabledFeature_HidesViewWithoutThemeData()
        {
            IdleBarController controller = new IdleBarController(
                () => null,
                () => false,
                () => default,
                () => null,
                () => false,
                _ => null
            );
            controller.Initialize(_actions);
            controller.BindView(_view);

            controller.Render();

            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        private void RenderOfficerDirectly()
        {
            RenderEntriesDirectly(_officer);
        }

        private void RenderEntriesDirectly(params ISceneNode[] entities)
        {
            _view.Render(
                new IdleBarRenderData(
                    true,
                    entities.Select(entity => new IdleBarEntry(entity, null)).ToList(),
                    new RectInt(0, 0, 700, 350)
                )
            );
        }

        private void ClickSlot(int index)
        {
            GetEntitySlots().ElementAt(index).GetComponent<Button>().onClick.Invoke();
        }

        private IEnumerable<IdleBarSlotView> GetEntitySlots()
        {
            return _view
                .GetComponentsInChildren<IdleBarSlotView>(false)
                .Where(slot => slot.GetComponent<Button>().interactable);
        }

        private ISceneNode ResolveEntity(string instanceId)
        {
            return _resolvedEntities.FirstOrDefault(entity => entity.InstanceID == instanceId);
        }

        private static PointerEventData CreatePointerEvent(GameObject target)
        {
            return new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = -1,
                pressPosition = new Vector2(10, 20),
                pointerCurrentRaycast = new RaycastResult { gameObject = target },
                pointerPressRaycast = new RaycastResult { gameObject = target },
            };
        }

        private sealed class TestActions : IIdleBarActions
        {
            public int RenderRequestCount { get; private set; }

            public ISceneNode OpenedTarget { get; private set; }

            public ISceneNode ContextTarget { get; private set; }

            public PointerEventData ContextEventData { get; private set; }

            public ISceneNode HighlightedTarget { get; private set; }

            public IReadOnlyList<ISceneNode> DraggedTargets { get; private set; }

            public int DragMoveCount { get; private set; }

            public int DragEndCount { get; private set; }

            public void OpenIdleBarTarget(ISceneNode target)
            {
                OpenedTarget = target;
            }

            public void OpenIdleBarContextMenu(ISceneNode target, PointerEventData eventData)
            {
                ContextTarget = target;
                ContextEventData = eventData;
            }

            public void RequestIdleBarRender()
            {
                RenderRequestCount++;
            }

            public void SetIdleBarLocationHighlight(ISceneNode target)
            {
                HighlightedTarget = target;
            }

            public bool TryStartIdleBarItemDrag(
                IReadOnlyList<ISceneNode> targets,
                DragPreview preview,
                PointerEventData eventData
            )
            {
                DraggedTargets = targets;
                return true;
            }

            public void MoveIdleBarItemDrag(PointerEventData eventData)
            {
                DragMoveCount++;
            }

            public void EndIdleBarItemDrag(PointerEventData eventData)
            {
                DragEndCount++;
            }
        }
    }
}
