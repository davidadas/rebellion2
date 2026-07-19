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
    public class MissionsWindowViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/MissionsWindow.prefab";

        private Texture2D _texture;
        private MissionsWindowView _view;
        private GameObject _viewObject;

        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<MissionsWindowView>();
            _texture = new Texture2D(84, 42);
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
        public void Render_SelectedMission_AppliesWindowMissionRowsTargetTabsAndParticipants()
        {
            MissionsWindowRenderData data = CreateRenderData(
                true,
                new[] { CreateMission("Diplomacy", true), CreateMission("Espionage", false) },
                new[] { CreateParticipant("Leia"), CreateParticipant("Han") }
            );

            _view.Render(data);

            RectInt rect = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(18, rect.x);
            Assert.AreEqual(26, rect.y);
            Assert.AreSame(_texture, FindComponent<RawImage>("TitleImage").texture);
            Assert.AreEqual("Corellia", FindText("CaptionTextField").text);
            MissionListRowView[] missions = FindMissionRows();
            Assert.AreEqual(2, missions.Length);
            Assert.AreEqual("Diplomacy", FindMissionText(missions[0]).text);
            Assert.AreSame(_texture, FindMissionImage(missions[0], "IconImage").texture);
            Assert.AreSame(_texture, FindMissionImage(missions[0], "SelectionImage").texture);
            Assert.IsFalse(FindMissionObject(missions[1], "SelectionImage").activeSelf);
            Assert.IsTrue(FindObject("TargetTitleTextField").activeSelf);
            Assert.AreSame(_texture, FindComponent<RawImage>("TargetImage").texture);
            Assert.AreEqual("Coruscant", FindText("TargetNameTextField").text);
            Assert.IsTrue(FindObject("Tabs").activeSelf);
            Assert.IsTrue(FindObject("AgentTabButtonImage").activeSelf);
            Assert.IsTrue(FindObject("DecoyTabButtonImage").activeSelf);
            MissionParticipantRowView[] participants = FindParticipantRows();
            Assert.AreEqual(2, participants.Length);
            Assert.AreEqual(MissionParticipantRole.Agent, participants[0].Role);
            Assert.AreEqual("Leia", FindParticipantText(participants[0]).text);
            Assert.AreEqual("Han", FindParticipantText(participants[1]).text);
        }

        [Test]
        public void Render_NoSelectedMission_HidesDetailsTabsAndCachedParticipants()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateMission("Diplomacy", true) },
                    new[] { CreateParticipant("Leia") }
                )
            );
            MissionParticipantRowView participant = FindParticipantRows().Single();

            _view.Render(
                CreateRenderData(
                    false,
                    new[] { CreateMission("Diplomacy", false) },
                    Array.Empty<MissionParticipantRowRenderData>()
                )
            );

            Assert.IsFalse(FindObject("TargetTitleTextField").activeSelf);
            Assert.IsFalse(FindObject("TargetImage").activeSelf);
            Assert.IsFalse(FindObject("TargetNameTextField").activeSelf);
            Assert.IsFalse(FindObject("Tabs").activeSelf);
            Assert.IsFalse(FindObject("AgentTabButtonImage").activeSelf);
            Assert.IsFalse(FindObject("DecoyTabButtonImage").activeSelf);
            Assert.IsFalse(FindObject("ParticipantsScrollArea").activeSelf);
            Assert.IsFalse(participant.gameObject.activeSelf);
        }

        [Test]
        public void Render_SelectedMissionWithoutTargetImage_HidesOnlyTargetImage()
        {
            MissionsWindowRenderData data = CreateRenderData(
                true,
                new[] { CreateMission("Diplomacy", true) },
                Array.Empty<MissionParticipantRowRenderData>(),
                targetTexture: null,
                showTargetImage: false
            );

            _view.Render(data);

            Assert.IsTrue(FindObject("TargetTitleTextField").activeSelf);
            Assert.IsFalse(FindObject("TargetImage").activeSelf);
            Assert.IsTrue(FindObject("TargetNameTextField").activeSelf);
            Assert.AreEqual("Coruscant", FindText("TargetNameTextField").text);
        }

        [Test]
        public void Render_ShorterCollections_HideUnusedCachedRows()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateMission("Diplomacy", true), CreateMission("Espionage", false) },
                    new[] { CreateParticipant("Leia"), CreateParticipant("Han") }
                )
            );
            MissionListRowView secondMission = FindMissionRows()[1];
            MissionParticipantRowView secondParticipant = FindParticipantRows()[1];

            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateMission("Recruitment", true) },
                    new[] { CreateParticipant("Luke") }
                )
            );

            Assert.IsFalse(secondMission.gameObject.activeSelf);
            Assert.IsFalse(secondParticipant.gameObject.activeSelf);
            Assert.AreEqual("Recruitment", FindMissionText(FindMissionRows()[0]).text);
            Assert.AreEqual("Luke", FindParticipantText(FindParticipantRows()[0]).text);
        }

        [Test]
        public void Render_InvalidTabCount_ThrowsArgumentException()
        {
            MissionsWindowRenderData data = CreateRenderData(
                true,
                new[] { CreateMission("Diplomacy", true) },
                Array.Empty<MissionParticipantRowRenderData>(),
                _texture,
                Array.Empty<MissionsWindowTabRenderData>()
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
        }

        [Test]
        public void Render_InvalidTabOrder_ThrowsArgumentException()
        {
            MissionsWindowTabRenderData[] tabs = CreateTabs();
            tabs[0] = new MissionsWindowTabRenderData(
                MissionParticipantRole.Decoy,
                _texture,
                _texture
            );
            MissionsWindowRenderData data = CreateRenderData(
                true,
                new[] { CreateMission("Diplomacy", true) },
                Array.Empty<MissionParticipantRowRenderData>(),
                _texture,
                tabs
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
        }

        [Test]
        public void OnPointerClick_PrimaryAndSecondary_RaisesOnlyPrimarySurfaceRequest()
        {
            int clickCount = 0;
            PointerEventData received = null;
            _view.SurfaceClicked += (_, eventData) =>
            {
                clickCount++;
                received = eventData;
            };
            PointerEventData primary = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };
            PointerEventData secondary = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
            };

            _view.OnPointerClick(primary);
            _view.OnPointerClick(secondary);
            _view.OnPointerClick(null);

            Assert.AreEqual(1, clickCount);
            Assert.AreSame(primary, received);
        }

        [Test]
        public void TabButton_Click_RaisesAuthoredParticipantRole()
        {
            MissionParticipantRole? requested = null;
            _view.TabRequested += (_, role) => requested = role;

            FindComponent<Button>("DecoyTabButtonImage").onClick.Invoke();

            Assert.AreEqual(MissionParticipantRole.Decoy, requested);
        }

        [Test]
        public void MissionRowGestures_RenderedRow_RaiseStableIndexAndOriginalEvent()
        {
            int pressedIndex = -1;
            int releasedIndex = -1;
            int droppedIndex = -1;
            int doubleClickedIndex = -1;
            PointerEventData pressedEvent = null;
            _view.MissionPressed += (_, index, eventData) =>
            {
                pressedIndex = index;
                pressedEvent = eventData;
            };
            _view.MissionReleased += (_, index, _) => releasedIndex = index;
            _view.MissionDropped += (_, index, _) => droppedIndex = index;
            _view.MissionDoubleClicked += (_, index, _) => doubleClickedIndex = index;
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateMission("Diplomacy", true), CreateMission("Espionage", false) },
                    Array.Empty<MissionParticipantRowRenderData>()
                )
            );
            MissionListRowView row = FindMissionRows()[1];
            UIComponentTestHelper.InvokeLifecycle(row, "Awake");
            UIPointerGestureRelay relay = row.GetComponent<UIPointerGestureRelay>();
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 2,
            };

            relay.OnPointerDown(eventData);
            relay.OnPointerClick(eventData);
            relay.OnDrop(eventData);

            Assert.AreEqual(1, pressedIndex);
            Assert.AreSame(eventData, pressedEvent);
            Assert.AreEqual(1, releasedIndex);
            Assert.AreEqual(1, droppedIndex);
            Assert.AreEqual(1, doubleClickedIndex);
        }

        [Test]
        public void ParticipantPress_RenderedRow_RaisesStableIndexAndOriginalEvent()
        {
            int pressedIndex = -1;
            PointerEventData received = null;
            _view.ParticipantPressed += (_, index, eventData) =>
            {
                pressedIndex = index;
                received = eventData;
            };
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateMission("Diplomacy", true) },
                    new[] { CreateParticipant("Leia"), CreateParticipant("Han") }
                )
            );
            MissionParticipantRowView row = FindParticipantRows()[1];
            UIComponentTestHelper.InvokeLifecycle(row, "Awake");
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            row.GetComponent<UIPointerGestureRelay>().OnPointerDown(eventData);

            Assert.AreEqual(1, pressedIndex);
            Assert.AreSame(eventData, received);
        }

        [Test]
        public void GetParticipantIndex_ActiveParticipantAndMissingTarget_ReturnExpectedIndexes()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateMission("Diplomacy", true) },
                    new[] { CreateParticipant("Leia") }
                )
            );
            MissionParticipantRowView participant = FindParticipantRows().Single();
            PointerEventData rowEvent = new PointerEventData(null)
            {
                pointerCurrentRaycast = new RaycastResult
                {
                    gameObject = FindParticipantText(participant).gameObject,
                },
            };

            int rowIndex = _view.GetParticipantIndex(rowEvent);
            int missingIndex = _view.GetParticipantIndex(null);

            Assert.AreEqual(0, rowIndex);
            Assert.AreEqual(-1, missingIndex);
        }

        [Test]
        public void ScrollMetrics_AuthoredTemplates_ReturnConsistentRowGeometry()
        {
            int missionStep = _view.GetMissionListScrollStep();
            int participantStep = _view.GetParticipantScrollStep();

            Assert.Greater(missionStep, 0);
            Assert.Greater(participantStep, 0);
            Assert.AreEqual(
                missionStep,
                _view.GetMissionListScrollContentHeight(2)
                    - _view.GetMissionListScrollContentHeight(1)
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
            MissionListRowView missionTemplate = _viewObject
                .GetComponentsInChildren<MissionListRowView>(true)
                .Single(row => row.name == "MissionListRowTemplate");
            MissionParticipantRowView participantTemplate = _viewObject
                .GetComponentsInChildren<MissionParticipantRowView>(true)
                .Single(row => row.name == "ParticipantRowTemplate");

            Assert.Throws<ArgumentNullException>(() => missionTemplate.Render(null));
            Assert.Throws<ArgumentNullException>(() => participantTemplate.Render(null));
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsControlsRowsAndRaisesDestroyedEvent()
        {
            MissionsWindowView destroyed = null;
            int tabCount = 0;
            int missionCount = 0;
            int participantCount = 0;
            _view.Destroyed += view => destroyed = view;
            _view.TabRequested += (_, _) => tabCount++;
            _view.MissionPressed += (_, _, _) => missionCount++;
            _view.ParticipantPressed += (_, _, _) => participantCount++;
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateMission("Diplomacy", true) },
                    new[] { CreateParticipant("Leia") }
                )
            );
            MissionListRowView mission = FindMissionRows().Single();
            MissionParticipantRowView participant = FindParticipantRows().Single();
            UIComponentTestHelper.InvokeLifecycle(mission, "Awake");
            UIComponentTestHelper.InvokeLifecycle(participant, "Awake");
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            FindComponent<Button>("AgentTabButtonImage").onClick.Invoke();
            mission.GetComponent<UIPointerGestureRelay>().OnPointerDown(eventData);
            participant.GetComponent<UIPointerGestureRelay>().OnPointerDown(eventData);

            Assert.AreSame(_view, destroyed);
            Assert.AreEqual(0, tabCount);
            Assert.AreEqual(0, missionCount);
            Assert.AreEqual(0, participantCount);
        }

        private MissionsWindowRenderData CreateRenderData(
            bool hasSelectedMission,
            MissionListRowRenderData[] missions,
            MissionParticipantRowRenderData[] participants,
            Texture targetTexture = null,
            MissionsWindowTabRenderData[] tabs = null,
            bool showTargetImage = true
        )
        {
            return new MissionsWindowRenderData(
                18,
                26,
                _texture,
                "Corellia",
                MissionParticipantRole.Agent,
                hasSelectedMission ? 0 : -1,
                hasSelectedMission,
                "Coruscant",
                showTargetImage && hasSelectedMission ? targetTexture ?? _texture : null,
                missions,
                tabs ?? CreateTabs(),
                participants
            );
        }

        private MissionsWindowTabRenderData[] CreateTabs()
        {
            return MissionsWindowRenderData
                .OrderedRoles.Select(role => new MissionsWindowTabRenderData(
                    role,
                    _texture,
                    _texture
                ))
                .ToArray();
        }

        private MissionListRowRenderData CreateMission(string name, bool selected)
        {
            return new MissionListRowRenderData(name, _texture, selected ? _texture : null);
        }

        private MissionParticipantRowRenderData CreateParticipant(string name)
        {
            return new MissionParticipantRowRenderData(name, Color.white, null, _texture);
        }

        private MissionListRowView[] FindMissionRows()
        {
            return _viewObject
                .GetComponentsInChildren<MissionListRowView>(true)
                .Where(row =>
                    row.name.StartsWith("MissionListRow", StringComparison.Ordinal)
                    && row.name != "MissionListRowTemplate"
                )
                .OrderBy(row => row.Index)
                .ToArray();
        }

        private MissionParticipantRowView[] FindParticipantRows()
        {
            return _viewObject
                .GetComponentsInChildren<MissionParticipantRowView>(true)
                .Where(row =>
                    row.name.StartsWith("MissionParticipantRow", StringComparison.Ordinal)
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

        private static RawImage FindMissionImage(MissionListRowView row, string objectName)
        {
            return row.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == objectName);
        }

        private static GameObject FindMissionObject(MissionListRowView row, string objectName)
        {
            return row.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        private static TextMeshProUGUI FindMissionText(MissionListRowView row)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "NameTextField");
        }

        private static TextMeshProUGUI FindParticipantText(MissionParticipantRowView row)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "NameTextField");
        }
    }
}
