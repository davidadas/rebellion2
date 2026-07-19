using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Missions
{
    [TestFixture]
    public class MissionCreateWindowViewTests
    {
        private const string _prefabPath =
            "Assets/Prefabs/UI/StrategyView/MissionCreateWindow.prefab";

        private Texture2D _texture;
        private MissionCreateWindowView _view;
        private GameObject _viewObject;

        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<MissionCreateWindowView>();
            _texture = new Texture2D(80, 40);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_viewObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_MissionTab_AppliesTitleSelectionTargetTabsAndDropdown()
        {
            MissionCreateWindowRenderData data = CreateRenderData(
                MissionCreateWindowTab.Mission,
                true,
                new[] { CreateDropdownItem("Diplomacy"), CreateDropdownItem("Espionage") },
                Array.Empty<MissionParticipantRowRenderData>(),
                Array.Empty<MissionParticipantRowRenderData>(),
                _texture,
                true
            );

            _view.Render(data);

            RectInt rect = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(23, rect.x);
            Assert.AreEqual(31, rect.y);
            Assert.AreSame(_texture, FindComponent<RawImage>("LeftTitleImage").texture);
            Assert.AreSame(_texture, FindComponent<RawImage>("RightTitleImage").texture);
            Assert.AreEqual(Color.black, FindText("TitleTextField").color);
            Assert.IsTrue(FindObject("MissionSelection").activeSelf);
            Assert.IsFalse(FindObject("Personnel").activeSelf);
            Assert.AreSame(_texture, FindComponent<RawImage>("SelectedMissionImage").texture);
            Assert.AreEqual("Diplomacy", FindText("SelectedMissionNameTextField").text);
            Assert.AreSame(_texture, FindComponent<RawImage>("TargetPreviewImage").texture);
            Assert.AreEqual("Corellia", FindText("TargetPreviewNameTextField").text);
            Assert.IsTrue(FindObject("Dropdown").activeSelf);
            Assert.AreSame(_texture, FindComponent<RawImage>("MissionTabButtonImage").texture);
            StrategyDropdownItemView[] items = FindDropdownItems();
            Assert.AreEqual(2, items.Length);
            Assert.AreEqual("Diplomacy", FindDropdownText(items[0]).text);
            Assert.AreEqual("Espionage", FindDropdownText(items[1]).text);
            Assert.AreSame(_texture, FindDropdownImage(items[0]).texture);
        }

        [Test]
        public void Render_MissionTabWithoutSelection_HidesOptionalSelectionFields()
        {
            MissionCreateWindowRenderData data = CreateRenderData(
                MissionCreateWindowTab.Mission,
                false,
                Array.Empty<StrategyDropdownItemRenderData>(),
                Array.Empty<MissionParticipantRowRenderData>(),
                Array.Empty<MissionParticipantRowRenderData>(),
                null,
                false,
                string.Empty,
                null,
                string.Empty,
                null,
                false
            );

            _view.Render(data);

            Assert.IsFalse(FindObject("SelectedMissionImage").activeSelf);
            Assert.IsFalse(FindObject("SelectedMissionNameTextField").activeSelf);
            Assert.IsFalse(FindObject("TargetPreviewImage").activeSelf);
            Assert.IsFalse(FindObject("TargetPreviewNameTextField").activeSelf);
            Assert.IsFalse(FindObject("Dropdown").activeSelf);
        }

        [Test]
        public void Render_PlanetTargetPreview_UsesAuthoredPreviewTexture()
        {
            Texture authoredPlanetTexture = FindComponent<RawImage>("TargetPreviewImage").texture;
            MissionCreateWindowRenderData data = CreateRenderData(
                MissionCreateWindowTab.Mission,
                false,
                Array.Empty<StrategyDropdownItemRenderData>(),
                Array.Empty<MissionParticipantRowRenderData>(),
                Array.Empty<MissionParticipantRowRenderData>(),
                null,
                true
            );

            _view.Render(data);

            Assert.IsNotNull(authoredPlanetTexture);
            Assert.AreSame(
                authoredPlanetTexture,
                FindComponent<RawImage>("TargetPreviewImage").texture
            );
            Assert.IsTrue(FindObject("TargetPreviewImage").activeSelf);
        }

        [Test]
        public void Render_ClosedDropdown_HidesPreviouslyRenderedItems()
        {
            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Mission,
                    true,
                    new[] { CreateDropdownItem("Diplomacy") },
                    Array.Empty<MissionParticipantRowRenderData>(),
                    Array.Empty<MissionParticipantRowRenderData>()
                )
            );
            StrategyDropdownItemView item = FindDropdownItems().Single();

            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Mission,
                    false,
                    new[] { CreateDropdownItem("Diplomacy") },
                    Array.Empty<MissionParticipantRowRenderData>(),
                    Array.Empty<MissionParticipantRowRenderData>()
                )
            );

            Assert.IsFalse(FindObject("Dropdown").activeSelf);
            Assert.IsFalse(item.gameObject.activeSelf);
        }

        [Test]
        public void Render_ShorterDropdownCollection_HidesUnusedCachedItems()
        {
            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Mission,
                    true,
                    new[] { CreateDropdownItem("Diplomacy"), CreateDropdownItem("Espionage") },
                    Array.Empty<MissionParticipantRowRenderData>(),
                    Array.Empty<MissionParticipantRowRenderData>()
                )
            );
            StrategyDropdownItemView secondItem = FindDropdownItems()[1];

            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Mission,
                    true,
                    new[] { CreateDropdownItem("Recruitment") },
                    Array.Empty<MissionParticipantRowRenderData>(),
                    Array.Empty<MissionParticipantRowRenderData>()
                )
            );

            Assert.IsFalse(secondItem.gameObject.activeSelf);
            Assert.AreEqual("Recruitment", FindDropdownText(FindDropdownItems()[0]).text);
        }

        [Test]
        public void Render_PersonnelTab_AppliesHeadersAndBothParticipantLists()
        {
            MissionCreateWindowRenderData data = CreateRenderData(
                MissionCreateWindowTab.Personnel,
                false,
                Array.Empty<StrategyDropdownItemRenderData>(),
                new[] { CreateParticipant("Leia", false), CreateParticipant("Han", true) },
                new[] { CreateParticipant("Chewbacca", false) }
            );

            _view.Render(data);

            Assert.AreEqual(Color.white, FindText("TitleTextField").color);
            Assert.IsFalse(FindObject("MissionSelection").activeSelf);
            Assert.IsTrue(FindObject("Personnel").activeSelf);
            Assert.AreSame(_texture, FindComponent<RawImage>("AgentsHeaderImage").texture);
            Assert.AreSame(_texture, FindComponent<RawImage>("DecoysHeaderImage").texture);
            MissionParticipantRowView[] agents = FindParticipantRows(MissionParticipantRole.Agent);
            MissionParticipantRowView[] decoys = FindParticipantRows(MissionParticipantRole.Decoy);
            Assert.AreEqual(2, agents.Length);
            Assert.AreEqual(1, decoys.Length);
            Assert.AreEqual(0, agents[0].Index);
            Assert.AreEqual(1, agents[1].Index);
            Assert.AreEqual("Leia", FindParticipantText(agents[0]).text);
            Assert.AreEqual("Han", FindParticipantText(agents[1]).text);
            Assert.AreEqual("Chewbacca", FindParticipantText(decoys[0]).text);
            Assert.AreSame(_texture, FindParticipantImage(agents[0], "EntityImage").texture);
        }

        [Test]
        public void Render_ShorterParticipantCollections_HideUnusedCachedRows()
        {
            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Personnel,
                    false,
                    Array.Empty<StrategyDropdownItemRenderData>(),
                    new[] { CreateParticipant("Leia", false), CreateParticipant("Han", false) },
                    new[]
                    {
                        CreateParticipant("Chewbacca", false),
                        CreateParticipant("Luke", false),
                    }
                )
            );
            MissionParticipantRowView secondAgent = FindParticipantRows(
                MissionParticipantRole.Agent
            )[1];
            MissionParticipantRowView secondDecoy = FindParticipantRows(
                MissionParticipantRole.Decoy
            )[1];

            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Personnel,
                    false,
                    Array.Empty<StrategyDropdownItemRenderData>(),
                    new[] { CreateParticipant("Replacement Agent", false) },
                    new[] { CreateParticipant("Replacement Decoy", false) }
                )
            );

            Assert.IsFalse(secondAgent.gameObject.activeSelf);
            Assert.IsFalse(secondDecoy.gameObject.activeSelf);
            Assert.AreEqual(
                "Replacement Agent",
                FindParticipantText(FindParticipantRows(MissionParticipantRole.Agent)[0]).text
            );
            Assert.AreEqual(
                "Replacement Decoy",
                FindParticipantText(FindParticipantRows(MissionParticipantRole.Decoy)[0]).text
            );
        }

        [Test]
        public void Render_SwitchingToMissionTab_HidesParticipantRows()
        {
            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Personnel,
                    false,
                    Array.Empty<StrategyDropdownItemRenderData>(),
                    new[] { CreateParticipant("Leia", false) },
                    new[] { CreateParticipant("Han", false) }
                )
            );
            MissionParticipantRowView agent = FindParticipantRows(MissionParticipantRole.Agent)
                .Single();
            MissionParticipantRowView decoy = FindParticipantRows(MissionParticipantRole.Decoy)
                .Single();

            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Mission,
                    false,
                    Array.Empty<StrategyDropdownItemRenderData>(),
                    Array.Empty<MissionParticipantRowRenderData>(),
                    Array.Empty<MissionParticipantRowRenderData>()
                )
            );

            Assert.IsFalse(agent.gameObject.activeSelf);
            Assert.IsFalse(decoy.gameObject.activeSelf);
            Assert.IsFalse(FindObject("Personnel").activeSelf);
        }

        [Test]
        public void Render_InvalidTabCount_ThrowsArgumentException()
        {
            MissionCreateWindowRenderData data = CreateRenderData(
                MissionCreateWindowTab.Mission,
                false,
                Array.Empty<StrategyDropdownItemRenderData>(),
                Array.Empty<MissionParticipantRowRenderData>(),
                Array.Empty<MissionParticipantRowRenderData>(),
                null,
                false,
                "Diplomacy",
                _texture,
                "Corellia",
                Array.Empty<MissionCreateTabRenderData>()
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
        }

        [Test]
        public void Render_InvalidTabOrder_ThrowsArgumentException()
        {
            MissionCreateTabRenderData[] tabs = CreateTabs();
            tabs[0] = new MissionCreateTabRenderData(
                MissionCreateWindowTab.Personnel,
                _texture,
                _texture
            );
            MissionCreateWindowRenderData data = CreateRenderData(
                MissionCreateWindowTab.Mission,
                false,
                Array.Empty<StrategyDropdownItemRenderData>(),
                Array.Empty<MissionParticipantRowRenderData>(),
                Array.Empty<MissionParticipantRowRenderData>(),
                null,
                false,
                "Diplomacy",
                _texture,
                "Corellia",
                tabs
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
        }

        [Test]
        public void AuthoredControls_Click_RaiseTabsDropdownActionsAndParticipantMoves()
        {
            MissionCreateWindowTab? tab = null;
            int dropdownCount = 0;
            MissionParticipantRole? firstMove = null;
            MissionParticipantRole? secondMove = null;
            int moveCount = 0;
            _view.TabRequested += (_, requested) => tab = requested;
            _view.DropdownToggleRequested += _ => dropdownCount++;
            _view.MoveParticipantsRequested += (_, role) =>
            {
                moveCount++;
                if (moveCount == 1)
                    firstMove = role;
                else
                    secondMove = role;
            };

            FindComponent<Button>("PersonnelTabButtonImage").onClick.Invoke();
            FindComponent<Button>("DropdownButtonImage").onClick.Invoke();
            FindComponent<Button>("MoveRightButtonImage").onClick.Invoke();
            FindComponent<Button>("MoveLeftButtonImage").onClick.Invoke();

            Assert.AreEqual(MissionCreateWindowTab.Personnel, tab);
            Assert.AreEqual(1, dropdownCount);
            Assert.AreEqual(MissionParticipantRole.Agent, firstMove);
            Assert.AreEqual(MissionParticipantRole.Decoy, secondMove);
        }

        [Test]
        public void ActionButtons_Click_RaiseInfoConfirmAndCancelRequests()
        {
            int infoCount = 0;
            int confirmCount = 0;
            int cancelCount = 0;
            _view.InfoRequested += _ => infoCount++;
            _view.ConfirmRequested += _ => confirmCount++;
            _view.CancelRequested += _ => cancelCount++;

            FindComponent<Button>("InfoButtonImage").onClick.Invoke();
            FindComponent<Button>("OkButtonImage").onClick.Invoke();
            FindComponent<Button>("CancelButtonImage").onClick.Invoke();

            Assert.AreEqual(1, infoCount);
            Assert.AreEqual(1, confirmCount);
            Assert.AreEqual(1, cancelCount);
        }

        [Test]
        public void DropdownItem_Click_RaisesStableVisualIndex()
        {
            int requestedIndex = -1;
            _view.DropdownItemRequested += (_, index) => requestedIndex = index;
            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Mission,
                    true,
                    new[] { CreateDropdownItem("Diplomacy"), CreateDropdownItem("Espionage") },
                    Array.Empty<MissionParticipantRowRenderData>(),
                    Array.Empty<MissionParticipantRowRenderData>()
                )
            );
            StrategyDropdownItemView item = FindDropdownItems()[1];
            UIComponentTestHelper.InvokeLifecycle(item, "Awake");

            item.GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(1, requestedIndex);
        }

        [Test]
        public void ParticipantGestures_RenderedRows_RaiseRoleIndexAndOriginalEvent()
        {
            MissionParticipantRole? pressedRole = null;
            MissionParticipantRole? clickedRole = null;
            int pressedIndex = -1;
            int clickedIndex = -1;
            PointerEventData pressedEvent = null;
            PointerEventData clickedEvent = null;
            _view.ParticipantPressed += (_, role, index, eventData) =>
            {
                pressedRole = role;
                pressedIndex = index;
                pressedEvent = eventData;
            };
            _view.ParticipantClicked += (_, role, index, eventData) =>
            {
                clickedRole = role;
                clickedIndex = index;
                clickedEvent = eventData;
            };
            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Personnel,
                    false,
                    Array.Empty<StrategyDropdownItemRenderData>(),
                    Array.Empty<MissionParticipantRowRenderData>(),
                    new[] { CreateParticipant("Han", false) }
                )
            );
            MissionParticipantRowView row = FindParticipantRows(MissionParticipantRole.Decoy)
                .Single();
            UIComponentTestHelper.InvokeLifecycle(row, "Awake");
            UIPointerGestureRelay relay = row.GetComponent<UIPointerGestureRelay>();
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            relay.OnPointerDown(eventData);
            relay.OnPointerClick(eventData);

            Assert.AreEqual(MissionParticipantRole.Decoy, pressedRole);
            Assert.AreEqual(0, pressedIndex);
            Assert.AreSame(eventData, pressedEvent);
            Assert.AreEqual(MissionParticipantRole.Decoy, clickedRole);
            Assert.AreEqual(0, clickedIndex);
            Assert.AreSame(eventData, clickedEvent);
        }

        [Test]
        public void OnPointerClick_OpenDropdownOutsidePrimaryClick_RaisesDismissRequest()
        {
            int dismissCount = 0;
            _view.DropdownDismissRequested += _ => dismissCount++;
            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Mission,
                    true,
                    new[] { CreateDropdownItem("Diplomacy") },
                    Array.Empty<MissionParticipantRowRenderData>(),
                    Array.Empty<MissionParticipantRowRenderData>()
                )
            );
            PointerEventData outside = CreateRaycastEvent(
                PointerEventData.InputButton.Left,
                FindObject("TitleTextField")
            );
            PointerEventData inside = CreateRaycastEvent(
                PointerEventData.InputButton.Left,
                FindObject("DropdownButtonImage")
            );
            PointerEventData secondary = CreateRaycastEvent(
                PointerEventData.InputButton.Right,
                FindObject("TitleTextField")
            );

            _view.OnPointerClick(inside);
            _view.OnPointerClick(secondary);
            _view.OnPointerClick(outside);

            Assert.AreEqual(1, dismissCount);
        }

        [Test]
        public void ScrollMetrics_AuthoredTemplates_ReturnConsistentRowGeometry()
        {
            int dropdownStep = _view.GetDropdownScrollStep();
            int participantStep = _view.GetParticipantScrollStep();

            Assert.Greater(dropdownStep, 0);
            Assert.Greater(participantStep, 0);
            Assert.AreEqual(
                dropdownStep,
                _view.GetDropdownScrollContentHeight(2) - _view.GetDropdownScrollContentHeight(1)
            );
            Assert.AreEqual(
                participantStep,
                _view.GetParticipantScrollContentHeight(2)
                    - _view.GetParticipantScrollContentHeight(1)
            );
        }

        [Test]
        public void ChildViews_NullRenderData_ThrowArgumentNullException()
        {
            StrategyDropdownItemView dropdownTemplate = _viewObject
                .GetComponentsInChildren<StrategyDropdownItemView>(true)
                .Single(item => item.name == "DropdownItemRowTemplate");
            MissionParticipantRowView[] participantTemplates = _viewObject
                .GetComponentsInChildren<MissionParticipantRowView>(true)
                .Where(row => row.name.EndsWith("RowTemplate", StringComparison.Ordinal))
                .ToArray();

            Assert.Throws<ArgumentNullException>(() => dropdownTemplate.Render(null));
            Assert.AreEqual(2, participantTemplates.Length);
            Assert.Throws<ArgumentNullException>(() => participantTemplates[0].Render(null));
            Assert.Throws<ArgumentNullException>(() => participantTemplates[1].Render(null));
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsControlsRowsAndRaisesDestroyedEvent()
        {
            MissionCreateWindowView destroyed = null;
            int confirmCount = 0;
            int dropdownItemCount = 0;
            int participantCount = 0;
            _view.Destroyed += view => destroyed = view;
            _view.ConfirmRequested += _ => confirmCount++;
            _view.DropdownItemRequested += (_, _) => dropdownItemCount++;
            _view.ParticipantClicked += (_, _, _, _) => participantCount++;
            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Mission,
                    true,
                    new[] { CreateDropdownItem("Diplomacy") },
                    Array.Empty<MissionParticipantRowRenderData>(),
                    Array.Empty<MissionParticipantRowRenderData>()
                )
            );
            StrategyDropdownItemView dropdownItem = FindDropdownItems().Single();
            UIComponentTestHelper.InvokeLifecycle(dropdownItem, "Awake");
            _view.Render(
                CreateRenderData(
                    MissionCreateWindowTab.Personnel,
                    false,
                    Array.Empty<StrategyDropdownItemRenderData>(),
                    new[] { CreateParticipant("Leia", false) },
                    Array.Empty<MissionParticipantRowRenderData>()
                )
            );
            MissionParticipantRowView participant = FindParticipantRows(
                    MissionParticipantRole.Agent
                )
                .Single();
            UIComponentTestHelper.InvokeLifecycle(participant, "Awake");

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            FindComponent<Button>("OkButtonImage").onClick.Invoke();
            dropdownItem.GetComponent<Button>().onClick.Invoke();
            participant
                .GetComponent<UIPointerGestureRelay>()
                .OnPointerClick(new PointerEventData(null));

            Assert.AreSame(_view, destroyed);
            Assert.AreEqual(0, confirmCount);
            Assert.AreEqual(0, dropdownItemCount);
            Assert.AreEqual(0, participantCount);
        }

        private MissionCreateWindowRenderData CreateRenderData(
            MissionCreateWindowTab activeTab,
            bool dropdownOpen,
            StrategyDropdownItemRenderData[] dropdownItems,
            MissionParticipantRowRenderData[] agents,
            MissionParticipantRowRenderData[] decoys,
            Texture targetTexture = null,
            bool usePlanetTargetPreview = false,
            string missionName = "Diplomacy",
            Texture selectedMissionTexture = null,
            string targetName = "Corellia",
            MissionCreateTabRenderData[] tabs = null,
            bool showSelectedMission = true
        )
        {
            return new MissionCreateWindowRenderData(
                23,
                31,
                activeTab,
                dropdownOpen,
                _texture,
                missionName,
                showSelectedMission ? selectedMissionTexture ?? _texture : null,
                targetName,
                targetTexture,
                usePlanetTargetPreview,
                _texture,
                _texture,
                tabs ?? CreateTabs(),
                dropdownItems,
                agents,
                decoys
            );
        }

        private MissionCreateTabRenderData[] CreateTabs()
        {
            return MissionCreateWindowRenderData
                .OrderedTabs.Select(tab => new MissionCreateTabRenderData(tab, _texture, _texture))
                .ToArray();
        }

        private StrategyDropdownItemRenderData CreateDropdownItem(string label)
        {
            return new StrategyDropdownItemRenderData(_texture, label, Color.white);
        }

        private MissionParticipantRowRenderData CreateParticipant(string name, bool inTransit)
        {
            return new MissionParticipantRowRenderData(
                name,
                Color.white,
                null,
                _texture,
                inTransit
            );
        }

        private StrategyDropdownItemView[] FindDropdownItems()
        {
            return _viewObject
                .GetComponentsInChildren<StrategyDropdownItemView>(true)
                .Where(item =>
                    item.name.StartsWith("DropdownItemRow", StringComparison.Ordinal)
                    && item.name != "DropdownItemRowTemplate"
                )
                .OrderBy(item => item.Index)
                .ToArray();
        }

        private MissionParticipantRowView[] FindParticipantRows(MissionParticipantRole role)
        {
            return _viewObject
                .GetComponentsInChildren<MissionParticipantRowView>(true)
                .Where(row =>
                    row.name.StartsWith("MissionParticipantRow", StringComparison.Ordinal)
                    && row.Role == role
                )
                .OrderBy(row => row.Index)
                .ToArray();
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _viewObject
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        private GameObject FindObject(string objectName)
        {
            return _viewObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        private TextMeshProUGUI FindText(string objectName)
        {
            return FindComponent<TextMeshProUGUI>(objectName);
        }

        private static RawImage FindDropdownImage(StrategyDropdownItemView item)
        {
            return item.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "ItemImage");
        }

        private static TextMeshProUGUI FindDropdownText(StrategyDropdownItemView item)
        {
            return item.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "ItemTextField");
        }

        private static RawImage FindParticipantImage(
            MissionParticipantRowView row,
            string objectName
        )
        {
            return row.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == objectName);
        }

        private static TextMeshProUGUI FindParticipantText(MissionParticipantRowView row)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "NameTextField");
        }

        private static PointerEventData CreateRaycastEvent(
            PointerEventData.InputButton button,
            GameObject target
        )
        {
            return new PointerEventData(null)
            {
                button = button,
                pointerCurrentRaycast = new RaycastResult { gameObject = target },
            };
        }
    }
}
