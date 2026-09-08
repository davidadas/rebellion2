using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.IdleBar
{
    [TestFixture]
    public class IdleBarViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private GameObject _rootObject;
        private IdleBarView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<IdleBarView>(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void PrefabProperties_AuthoredView_ContainsCompactShelfAndCircularSlotTemplate()
        {
            Image hitArea = GetField<Image>("shelfHitArea");
            ScrollAreaView scrollArea = GetField<ScrollAreaView>("entriesScrollArea");
            IdleBarSlotView template = GetField<IdleBarSlotView>("slotTemplate");
            Assert.IsNotNull(_view);
            Assert.IsNotNull(hitArea);
            Assert.AreEqual(new Color(0.08f, 0.09f, 0.11f, 0.9f), hitArea.color);
            Assert.IsTrue(hitArea.raycastTarget);
            Assert.IsNotNull(scrollArea);
            Assert.IsFalse(scrollArea.gameObject.activeSelf);
            Assert.IsNotNull(template);
            Assert.IsFalse(template.gameObject.activeSelf);
            Assert.AreSame(scrollArea.ContentRoot, template.transform.parent);
            Button button = template.GetComponent<Button>();
            RectTransform frame = template.transform.Find("CircleFrame") as RectTransform;
            RectTransform mask =
                template.GetComponentInChildren<Mask>(true).transform as RectTransform;
            TextMeshProUGUI overflowText = template.GetComponentInChildren<TextMeshProUGUI>(true);
            Assert.IsNotNull(button);
            Assert.AreEqual(Selectable.Transition.None, button.transition);
            Assert.AreEqual(Color.black, frame.GetComponent<Image>().color);
            Assert.AreEqual(new Vector2(28f, 28f), frame.sizeDelta);
            Assert.AreEqual(new Vector2(24f, 24f), mask.sizeDelta);
            Assert.IsNotNull(overflowText);
            Assert.AreEqual(FontStyles.Bold, overflowText.fontStyle);
            Assert.IsFalse(overflowText.gameObject.activeSelf);
        }

        [Test]
        public void PrefabProperties_IdleBarIsTopLevelNonWindowFeature()
        {
            Transform windows = _view.transform.parent.Find("Windows");

            Assert.AreEqual("StrategyView", _view.transform.parent.name);
            Assert.IsFalse(_view.gameObject.activeSelf);
            Assert.IsNull(_view.GetComponent<UIWindow>());
            Assert.IsNotNull(windows);
            Assert.Less(_view.transform.GetSiblingIndex(), windows.GetSiblingIndex());
        }

        [Test]
        public void Render_FewEntries_CreatesSmallRightAlignedSingleRow()
        {
            RectInt bounds = new RectInt(50, 30, 700, 350);
            _view.Render(
                new IdleBarRenderData(
                    true,
                    new List<IdleBarEntry>
                    {
                        new IdleBarEntry(CreateOfficer("Officer"), null),
                        new IdleBarEntry(CreateSpecialForces("Spec Ops"), null),
                        new IdleBarEntry(CreatePlanet("Planet"), null),
                    },
                    bounds
                )
            );
            List<IdleBarSlotView> slots = GetVisibleSlots();
            RectTransform first = slots[0].transform as RectTransform;
            RectTransform second = slots[1].transform as RectTransform;
            RectTransform third = slots[2].transform as RectTransform;
            Image hitArea = GetField<Image>("shelfHitArea");
            Assert.AreEqual(3, slots.Count);
            Assert.AreEqual("Officer", slots[0].name);
            Assert.AreEqual("Spec Ops", slots[1].name);
            Assert.AreEqual("Planet", slots[2].name);
            Assert.Less(first.anchoredPosition.x, second.anchoredPosition.x);
            Assert.Less(second.anchoredPosition.x, third.anchoredPosition.x);
            Assert.AreEqual(first.anchoredPosition.y, third.anchoredPosition.y);
            ScrollAreaView scrollArea = GetField<ScrollAreaView>("entriesScrollArea");
            Assert.AreEqual(
                741f,
                scrollArea.GetComponent<RectTransform>().anchoredPosition.x
                    + scrollArea.ScrollRoot.anchoredPosition.x
                    + third.anchoredPosition.x
                    + third.sizeDelta.x
            );
            Assert.AreEqual(28f, third.sizeDelta.x);
            Assert.AreEqual(28f, third.sizeDelta.y);
            Assert.LessOrEqual(hitArea.rectTransform.sizeDelta.x, bounds.width / 2f);
            Assert.AreEqual(34f, hitArea.rectTransform.sizeDelta.y);
            Assert.AreEqual(new Color(0.08f, 0.09f, 0.11f, 0.9f), hitArea.color);
        }

        [Test]
        public void PointerHover_ExpandsPortraitWithinFixedSlot()
        {
            _view.Render(
                new IdleBarRenderData(
                    true,
                    new[] { new IdleBarEntry(CreateOfficer("Officer"), null) },
                    new RectInt(0, 0, 800, 480)
                )
            );
            IdleBarSlotView slot = GetVisibleSlots().Single();
            RectTransform slotRect = slot.transform as RectTransform;
            Image frame = slot.transform.Find("CircleFrame").GetComponent<Image>();
            RectTransform mask = slot.GetComponentInChildren<Mask>().transform as RectTransform;
            RawImage portraitBackground = slot
                .transform.Find("PortraitMask/PortraitBackground")
                .GetComponent<RawImage>();
            Assert.AreEqual(new Vector2(28f, 28f), slotRect.sizeDelta);
            Assert.AreEqual(new Vector2(24f, 24f), mask.sizeDelta);
            Assert.AreEqual(Color.black, frame.color);
            RawImage portraitImage = slot
                .transform.Find("PortraitMask/PortraitImage")
                .GetComponent<RawImage>();
            Assert.AreEqual(Color.black, portraitBackground.color);
            Assert.AreEqual(Vector3.one, portraitImage.rectTransform.localScale);

            slot.OnPointerEnter(null);

            Assert.AreEqual(new Vector2(28f, 28f), slotRect.sizeDelta);
            Assert.AreEqual(new Vector2(26f, 26f), mask.sizeDelta);
            Assert.AreEqual(Color.black, frame.color);
            Assert.AreEqual(Color.black, portraitBackground.color);
            Assert.AreEqual(Vector3.one * 1.08f, portraitImage.rectTransform.localScale);

            slot.OnPointerExit(null);

            Assert.AreEqual(new Vector2(24f, 24f), mask.sizeDelta);
            Assert.AreEqual(Color.black, portraitBackground.color);
            Assert.AreEqual(Vector3.one, portraitImage.rectTransform.localScale);
        }

        [Test]
        public void Render_SelectedEntry_UsesWhiteCircleOutline()
        {
            _view.Render(
                new IdleBarRenderData(
                    true,
                    new[] { new IdleBarEntry(CreateOfficer("Officer"), null, true) },
                    new RectInt(0, 0, 800, 480)
                )
            );

            Image frame = GetVisibleSlots()
                .Single()
                .transform.Find("CircleFrame")
                .GetComponent<Image>();

            Assert.AreEqual(Color.white, frame.color);
        }

        [Test]
        public void PointerHover_OverflowingEntries_RevealsThreeRowsUntilPointerLeaves()
        {
            _view.Render(
                new IdleBarRenderData(true, CreateEntries(20), new RectInt(50, 30, 400, 350))
            );

            List<IdleBarSlotView> slots = GetVisibleSlots();
            Image hitArea = GetField<Image>("shelfHitArea");
            Assert.AreEqual(5, slots.Count);
            Assert.AreEqual("+16", slots[4].name);
            Assert.AreEqual(1, CountRows(slots));

            _view.OnPointerEnter(new PointerEventData(null));

            slots = GetVisibleSlots();
            Assert.AreEqual(20, slots.Count);
            Assert.AreEqual(210f, hitArea.rectTransform.sizeDelta.x);
            Assert.AreEqual(3, CountRows(slots));
            Assert.Less(
                slots[0].GetComponent<RectTransform>().anchoredPosition.x,
                slots[1].GetComponent<RectTransform>().anchoredPosition.x
            );
            Assert.Less(
                slots[7].GetComponent<RectTransform>().anchoredPosition.x,
                slots[8].GetComponent<RectTransform>().anchoredPosition.x
            );
            Assert.AreEqual(
                slots[0].GetComponent<RectTransform>().anchoredPosition.x,
                slots[7].GetComponent<RectTransform>().anchoredPosition.x
            );
            ScrollAreaView scrollArea = GetField<ScrollAreaView>("entriesScrollArea");
            Assert.AreEqual(86f, scrollArea.ViewportHeight);
            Assert.IsFalse(
                scrollArea.GetComponentInChildren<Scrollbar>(true).gameObject.activeSelf
            );
            foreach (
                IGrouping<float, IdleBarSlotView> row in slots.GroupBy(slot =>
                    slot.GetComponent<RectTransform>().anchoredPosition.y
                )
            )
            {
                RectTransform[] ordered = row.Select(slot => slot.GetComponent<RectTransform>())
                    .OrderBy(rect => rect.anchoredPosition.x)
                    .ToArray();
                for (int index = 1; index < ordered.Length; index++)
                {
                    Assert.GreaterOrEqual(
                        ordered[index].anchoredPosition.x,
                        ordered[index - 1].anchoredPosition.x + ordered[index - 1].sizeDelta.x
                    );
                }
            }

            slots[0].OnPointerExit(new PointerEventData(null));
            InvokeViewMethod("LateUpdate");

            Assert.AreEqual(20, GetVisibleSlots().Count);
            Assert.AreEqual(3, CountRows(GetVisibleSlots()));

            _view.OnPointerExit(new PointerEventData(null));
            InvokeViewMethod("LateUpdate");

            Assert.AreEqual(5, GetVisibleSlots().Count);
            Assert.AreEqual("+16", GetVisibleSlots()[4].name);
            Assert.AreEqual(1, CountRows(GetVisibleSlots()));
        }

        [Test]
        public void Scroll_OverflowingEntries_MovesSharedScrollAreaByOneRow()
        {
            _view.Render(
                new IdleBarRenderData(true, CreateEntries(29), new RectInt(50, 30, 400, 350))
            );
            Assert.AreEqual("Officer 0", GetVisibleSlots()[0].name);
            Assert.AreEqual("+25", GetVisibleSlots()[4].name);

            _view.OnPointerEnter(new PointerEventData(null));
            PointerEventData scroll = new PointerEventData(null)
            {
                scrollDelta = new Vector2(0f, -1f),
            };
            ScrollAreaView scrollArea = GetField<ScrollAreaView>("entriesScrollArea");
            scrollArea.RelayScroll(scroll);

            Assert.AreEqual(29f, scrollArea.ContentRoot.anchoredPosition.y, 0.001f);
            Scrollbar scrollbar = scrollArea.GetComponentInChildren<Scrollbar>(true);
            Assert.IsTrue(scrollbar.gameObject.activeSelf);
            Assert.Greater(
                scrollbar.GetComponent<RectTransform>().anchoredPosition.x,
                scrollArea.ScrollRoot.anchoredPosition.x
            );
            Assert.Greater(
                scrollArea.GetComponent<RectTransform>().anchoredPosition.x
                    + scrollbar.GetComponent<RectTransform>().anchoredPosition.x,
                250f
            );
        }

        [Test]
        public void DragCandidate_CenteredPortrait_PreservesCrop()
        {
            Texture2D texture = new Texture2D(40, 20);
            DragPreview capturedPreview = null;
            _view.EntryDragCandidateRequested += (_, preview, _) => capturedPreview = preview;
            _view.Render(
                new IdleBarRenderData(
                    true,
                    new[] { new IdleBarEntry(CreateOfficer("Officer"), texture) },
                    new RectInt(0, 0, 700, 350)
                )
            );
            IdleBarSlotView slot = GetVisibleSlots().Single();

            slot.OnPointerDown(
                new PointerEventData(null) { button = PointerEventData.InputButton.Left }
            );

            Assert.IsNotNull(capturedPreview);
            Assert.AreEqual(new Rect(0.25f, 0f, 0.5f, 1f), capturedPreview.Images[0].UvRect);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void Render_ThenClick_RaisesSelectedEntityIdentity()
        {
            string selectedInstanceId = null;
            _view.EntrySelected += instanceId => selectedInstanceId = instanceId;
            _view.Render(
                new IdleBarRenderData(
                    true,
                    new[] { new IdleBarEntry(CreateOfficer("Officer"), null) },
                    new RectInt(0, 0, 700, 350)
                )
            );
            GetVisibleSlots()[0].GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual("Officer", selectedInstanceId);
        }

        [Test]
        public void Render_ThenRightClick_RaisesContextRequestWithoutSelecting()
        {
            string selectedInstanceId = null;
            string contextInstanceId = null;
            PointerEventData contextEventData = null;
            _view.EntrySelected += instanceId => selectedInstanceId = instanceId;
            _view.EntryContextRequested += (instanceId, eventData) =>
            {
                contextInstanceId = instanceId;
                contextEventData = eventData;
            };
            _view.Render(
                new IdleBarRenderData(
                    true,
                    new[] { new IdleBarEntry(CreateOfficer("Officer"), null) },
                    new RectInt(0, 0, 700, 350)
                )
            );
            PointerEventData rightClick = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
            };

            GetVisibleSlots()[0].OnPointerClick(rightClick);

            Assert.IsNull(selectedInstanceId);
            Assert.AreEqual("Officer", contextInstanceId);
            Assert.AreSame(rightClick, contextEventData);
            Assert.IsTrue(rightClick.used);
        }

        [Test]
        public void RightClick_OpenContextMenu_KeepsExpandedUntilMenuCloses()
        {
            bool contextMenuOpen = false;
            _view.SetContextMenuOpenProvider(() => contextMenuOpen);
            _view.EntryContextRequested += (_, _) => contextMenuOpen = true;
            _view.Render(
                new IdleBarRenderData(true, CreateEntries(8), new RectInt(50, 30, 400, 350))
            );
            _view.OnPointerEnter(new PointerEventData(null));
            PointerEventData rightClick = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
            };

            GetVisibleSlots()[0].OnPointerClick(rightClick);
            _view.OnPointerExit(new PointerEventData(null));
            InvokeViewMethod("LateUpdate");

            Assert.AreEqual(8, GetVisibleSlots().Count);

            contextMenuOpen = false;
            InvokeViewMethod("LateUpdate");

            Assert.AreEqual(5, GetVisibleSlots().Count);
            Assert.AreEqual("+4", GetVisibleSlots()[4].name);
        }

        [Test]
        public void Render_EmptyEntries_HidesShelfAndExistingSlots()
        {
            _view.Render(
                new IdleBarRenderData(true, CreateEntries(1), new RectInt(0, 0, 700, 350))
            );

            _view.Render(
                new IdleBarRenderData(true, new List<IdleBarEntry>(), new RectInt(0, 0, 700, 350))
            );

            Assert.IsFalse(GetField<Image>("shelfHitArea").gameObject.activeSelf);
            Assert.IsEmpty(GetVisibleSlots());
        }

        [Test]
        public void Render_HiddenPresentation_DeactivatesFeatureRoot()
        {
            _view.Render(new IdleBarRenderData(false, CreateEntries(1), new RectInt()));

            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(IdleBarView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }

        private List<IdleBarSlotView> GetVisibleSlots()
        {
            List<IdleBarSlotView> visible = GetField<List<IdleBarSlotView>>("slots")
                .Where(slot => slot.gameObject.activeSelf)
                .ToList();
            IdleBarSlotView overflow = GetField<IdleBarSlotView>("overflowSlot");
            if (overflow?.gameObject.activeSelf == true)
                visible.Add(overflow);
            return visible;
        }

        private void InvokeViewMethod(string methodName)
        {
            typeof(IdleBarView)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_view, null);
        }

        private static int CountRows(IEnumerable<IdleBarSlotView> slots)
        {
            return slots
                .Select(slot => slot.GetComponent<RectTransform>().anchoredPosition.y)
                .Distinct()
                .Count();
        }

        private static List<IdleBarEntry> CreateEntries(int count)
        {
            List<IdleBarEntry> entries = new List<IdleBarEntry>();
            for (int index = 0; index < count; index++)
                entries.Add(new IdleBarEntry(CreateOfficer($"Officer {index}"), null));
            return entries;
        }

        private static Officer CreateOfficer(string displayName)
        {
            return new Officer { InstanceID = displayName, DisplayName = displayName };
        }

        private static SpecialForces CreateSpecialForces(string displayName)
        {
            return new SpecialForces { InstanceID = displayName, DisplayName = displayName };
        }

        private static Planet CreatePlanet(string displayName)
        {
            return new Planet { InstanceID = displayName, DisplayName = displayName };
        }
    }
}
