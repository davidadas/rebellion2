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

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<MissionsWindowView>();
            _texture = new Texture2D(84, 42);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_viewObject);
        }

        /// <summary>
        /// Verifies render null data throws argument null exception.
        /// </summary>
        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        /// <summary>
        /// Verifies render selected mission applies window mission rows target tabs and participants.
        /// </summary>
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

        /// <summary>
        /// Verifies render no selected mission hides details tabs and cached participants.
        /// </summary>
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

        /// <summary>
        /// Verifies render selected mission without target image hides only target image.
        /// </summary>
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

        /// <summary>
        /// Verifies render shorter collections hide unused cached rows.
        /// </summary>
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

        /// <summary>
        /// Verifies render invalid tab count throws argument exception.
        /// </summary>
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

        /// <summary>
        /// Verifies render invalid tab order throws argument exception.
        /// </summary>
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

        /// <summary>
        /// Verifies on pointer click primary and secondary raises only primary surface request.
        /// </summary>
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

        /// <summary>
        /// Verifies tab button click raises authored participant role.
        /// </summary>
        [Test]
        public void TabButton_Click_RaisesAuthoredParticipantRole()
        {
            MissionParticipantRole? requested = null;
            _view.TabRequested += (_, role) => requested = role;

            FindComponent<Button>("DecoyTabButtonImage").onClick.Invoke();

            Assert.AreEqual(MissionParticipantRole.Decoy, requested);
        }

        /// <summary>
        /// Verifies mission row gestures rendered row raise stable index and original event.
        /// </summary>
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

        /// <summary>
        /// Verifies participant gestures rendered row raise stable index and original event.
        /// </summary>
        [Test]
        public void ParticipantGestures_RenderedRow_RaiseStableIndexAndOriginalEvent()
        {
            int pressedIndex = -1;
            int releasedIndex = -1;
            PointerEventData pressedEvent = null;
            PointerEventData releasedEvent = null;
            _view.ParticipantPressed += (_, index, eventData) =>
            {
                pressedIndex = index;
                pressedEvent = eventData;
            };
            _view.ParticipantReleased += (_, index, eventData) =>
            {
                releasedIndex = index;
                releasedEvent = eventData;
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

            UIPointerGestureRelay relay = row.GetComponent<UIPointerGestureRelay>();
            relay.OnPointerDown(eventData);
            relay.OnPointerClick(eventData);

            Assert.AreEqual(1, pressedIndex);
            Assert.AreSame(eventData, pressedEvent);
            Assert.AreEqual(1, releasedIndex);
            Assert.AreSame(eventData, releasedEvent);
        }

        /// <summary>
        /// Verifies get participant index active participant and missing target return expected indexes.
        /// </summary>
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

        /// <summary>
        /// Verifies scroll metrics authored templates return consistent row geometry.
        /// </summary>
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

        /// <summary>
        /// Verifies child views null render data throw argument null exception.
        /// </summary>
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

        /// <summary>
        /// Verifies on destroy initialized view unbinds controls rows and raises destroyed event.
        /// </summary>
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
            _view.ParticipantReleased += (_, _, _) => participantCount++;
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
            participant.GetComponent<UIPointerGestureRelay>().OnPointerClick(eventData);

            Assert.AreSame(_view, destroyed);
            Assert.AreEqual(0, tabCount);
            Assert.AreEqual(0, missionCount);
            Assert.AreEqual(0, participantCount);
        }

        /// <summary>
        /// Creates render data.
        /// </summary>
        /// <param name="hasSelectedMission">Whether has selected mission.</param>
        /// <param name="missions">The missions.</param>
        /// <param name="participants">The participants.</param>
        /// <param name="targetTexture">The target texture.</param>
        /// <param name="tabs">The tabs.</param>
        /// <param name="showTargetImage">Whether show target image.</param>
        /// <returns>The created render data.</returns>
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

        /// <summary>
        /// Creates tabs.
        /// </summary>
        /// <returns>The created tabs.</returns>
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

        /// <summary>
        /// Creates mission.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="selected">Whether selected.</param>
        /// <returns>The created mission.</returns>
        private MissionListRowRenderData CreateMission(string name, bool selected)
        {
            return new MissionListRowRenderData(name, _texture, selected ? _texture : null);
        }

        /// <summary>
        /// Creates participant.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The created participant.</returns>
        private MissionParticipantRowRenderData CreateParticipant(string name)
        {
            return new MissionParticipantRowRenderData(name, Color.white, null, _texture);
        }

        /// <summary>
        /// Finds mission rows.
        /// </summary>
        /// <returns>The matching mission rows.</returns>
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

        /// <summary>
        /// Finds participant rows.
        /// </summary>
        /// <returns>The matching participant rows.</returns>
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

        /// <summary>
        /// Finds component.
        /// </summary>
        /// <param name="objectName">The object name.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The matching component.</returns>
        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _viewObject
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        /// <summary>
        /// Finds object.
        /// </summary>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching object.</returns>
        private GameObject FindObject(string objectName)
        {
            return _viewObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        /// <summary>
        /// Finds text.
        /// </summary>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching text.</returns>
        private TextMeshProUGUI FindText(string objectName)
        {
            return FindComponent<TextMeshProUGUI>(objectName);
        }

        /// <summary>
        /// Finds mission image.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching mission image.</returns>
        private static RawImage FindMissionImage(MissionListRowView row, string objectName)
        {
            return row.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == objectName);
        }

        /// <summary>
        /// Finds mission object.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching mission object.</returns>
        private static GameObject FindMissionObject(MissionListRowView row, string objectName)
        {
            return row.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        /// <summary>
        /// Finds mission text.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns>The matching mission text.</returns>
        private static TextMeshProUGUI FindMissionText(MissionListRowView row)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "NameTextField");
        }

        /// <summary>
        /// Finds participant text.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns>The matching participant text.</returns>
        private static TextMeshProUGUI FindParticipantText(MissionParticipantRowView row)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "NameTextField");
        }
    }
}
