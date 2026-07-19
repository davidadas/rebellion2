using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Missions
{
    [TestFixture]
    public class MissionsWindowSessionTests
    {
        private TestMission _firstMission;
        private GalaxyMapPlanet _galaxyMapPlanet;
        private Planet _planet;
        private TestMission _secondMission;
        private UIWindow _window;
        private GameObject _windowObject;

        [SetUp]
        public void SetUp()
        {
            _windowObject = new GameObject(
                "MissionsWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 10, 20, 300, 200, false, true, true);
            _firstMission = CreateMission("first-mission", "First Mission");
            _secondMission = CreateMission("second-mission", "Second Mission");
            _planet = new Planet { InstanceID = "planet", DisplayName = "Corellia" };
            _planet.Missions.Add(_firstMission);
            _planet.Missions.Add(_secondMission);
            _galaxyMapPlanet = new GalaxyMapPlanet(new GamePlanetSystem(), _planet, string.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_MissingRequiredInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new MissionsWindowSession(null, _window));
            Assert.Throws<ArgumentNullException>(() =>
                new MissionsWindowSession(_galaxyMapPlanet, null)
            );
        }

        [Test]
        public void Constructor_Missions_SelectsFirstMissionAndAgentRole()
        {
            MissionsWindowSession session = CreateSession();

            Assert.AreSame(_galaxyMapPlanet, session.Planet);
            Assert.AreSame(_window, session.Window);
            Assert.AreEqual(0, session.SelectedMissionIndex);
            Assert.AreSame(_firstMission, session.SelectedMission);
            Assert.AreEqual(MissionParticipantRole.Agent, session.ActiveRole);
            Assert.AreSame(_firstMission.MainParticipants, session.ActiveParticipants);
            Assert.AreEqual(-1, session.ContextParticipantIndex);
            Assert.IsNull(session.ContextParticipant);
        }

        [Test]
        public void Constructor_EmptyMissionList_ReturnsNoSelection()
        {
            _planet.Missions.Clear();

            MissionsWindowSession session = CreateSession();

            Assert.AreEqual(-1, session.SelectedMissionIndex);
            Assert.IsNull(session.SelectedMission);
            Assert.IsEmpty(session.ActiveParticipants);
        }

        [Test]
        public void SelectMission_ValidMission_UpdatesSelectionAndClearsParticipantContext()
        {
            MissionsWindowSession session = CreateSession();
            session.CaptureParticipant(0);

            bool selected = session.SelectMission(1);

            Assert.IsTrue(selected);
            Assert.AreEqual(1, session.SelectedMissionIndex);
            Assert.AreSame(_secondMission, session.SelectedMission);
            Assert.AreEqual(-1, session.ContextParticipantIndex);
            Assert.IsNull(session.ContextParticipant);
            Assert.AreSame(_secondMission, session.GetMission(1));
            Assert.IsNull(session.GetMission(-1));
            Assert.IsNull(session.GetMission(2));
        }

        [Test]
        public void SelectMission_UninitializedIdentifier_GeneratesIdentityAndSelectsMission()
        {
            _secondMission.InstanceID = null;
            MissionsWindowSession session = CreateSession();

            bool selected = session.SelectMission(1);

            Assert.IsTrue(selected);
            Assert.IsNotEmpty(_secondMission.InstanceID);
            Assert.AreSame(_secondMission, session.SelectedMission);
        }

        [Test]
        public void SelectTarget_ValidMission_UpdatesMissionAndParticipantRole()
        {
            MissionsWindowSession session = CreateSession();

            bool selected = session.SelectTarget(1, MissionParticipantRole.Decoy);

            Assert.IsTrue(selected);
            Assert.AreSame(_secondMission, session.SelectedMission);
            Assert.AreEqual(MissionParticipantRole.Decoy, session.ActiveRole);
            Assert.AreSame(_secondMission.DecoyParticipants, session.ActiveParticipants);
        }

        [Test]
        public void SelectRole_DifferentRole_ClearsParticipantContext()
        {
            MissionsWindowSession session = CreateSession();
            session.CaptureParticipant(0);

            bool selected = session.SelectRole(MissionParticipantRole.Decoy);
            bool selectedAgain = session.SelectRole(MissionParticipantRole.Decoy);

            Assert.IsTrue(selected);
            Assert.IsFalse(selectedAgain);
            Assert.AreEqual(MissionParticipantRole.Decoy, session.ActiveRole);
            Assert.AreEqual(-1, session.ContextParticipantIndex);
            Assert.IsNull(session.ContextParticipant);
        }

        [Test]
        public void CaptureParticipant_ValidIndex_TracksParticipantByIdentifier()
        {
            MissionsWindowSession session = CreateSession();

            session.CaptureParticipant(0);
            _firstMission.MainParticipants.Insert(
                0,
                new Officer { InstanceID = "inserted", DisplayName = "Inserted" }
            );

            Assert.AreEqual(1, session.ContextParticipantIndex);
            Assert.AreEqual("first-agent", session.ContextParticipant.InstanceID);
            Assert.IsTrue(session.IsParticipantIndexValid(0));
            Assert.IsFalse(session.IsParticipantIndexValid(5));
        }

        [Test]
        public void CaptureParticipant_InvalidIndex_ClearsParticipantContext()
        {
            MissionsWindowSession session = CreateSession();
            session.CaptureParticipant(0);

            session.CaptureParticipant(8);

            Assert.AreEqual(-1, session.ContextParticipantIndex);
            Assert.IsNull(session.ContextParticipant);
        }

        [Test]
        public void ReconcileSelection_ReorderedMissions_PreservesSelectedMissionIdentity()
        {
            MissionsWindowSession session = CreateSession();
            session.SelectMission(1);
            _planet.Missions.Reverse();

            session.ReconcileSelection();

            Assert.AreEqual(0, session.SelectedMissionIndex);
            Assert.AreSame(_secondMission, session.SelectedMission);
        }

        [Test]
        public void ReconcileSelection_RemovedMission_SelectsNearestFallback()
        {
            MissionsWindowSession session = CreateSession();
            session.SelectMission(1);
            _planet.Missions.Remove(_secondMission);

            session.ReconcileSelection();

            Assert.AreEqual(0, session.SelectedMissionIndex);
            Assert.AreSame(_firstMission, session.SelectedMission);
        }

        [Test]
        public void RebindPlanet_RefreshedProjection_PreservesMissionSelection()
        {
            MissionsWindowSession session = CreateSession();
            session.SelectMission(1);
            Planet refreshedPlanet = new Planet { InstanceID = "planet" };
            refreshedPlanet.Missions.Add(CreateMission("first-mission", "First Mission"));
            TestMission refreshedSelection = CreateMission("second-mission", "Second Mission");
            refreshedPlanet.Missions.Add(refreshedSelection);
            GalaxyMapPlanet refreshed = new GalaxyMapPlanet(
                new GamePlanetSystem(),
                refreshedPlanet,
                string.Empty
            );

            session.RebindPlanet(refreshed);

            Assert.AreSame(refreshed, session.Planet);
            Assert.AreEqual(1, session.SelectedMissionIndex);
            Assert.AreSame(refreshedSelection, session.SelectedMission);
            Assert.Throws<ArgumentNullException>(() => session.RebindPlanet(null));
        }

        private static TestMission CreateMission(string instanceId, string displayName)
        {
            TestMission mission = new TestMission
            {
                InstanceID = instanceId,
                ConfigKey = MissionTypeIDs.Diplomacy,
                DisplayName = displayName,
            };
            mission.MainParticipants.Add(
                new Officer { InstanceID = instanceId.Replace("mission", "agent") }
            );
            mission.DecoyParticipants.Add(
                new Officer { InstanceID = instanceId.Replace("mission", "decoy") }
            );
            return mission;
        }

        private MissionsWindowSession CreateSession()
        {
            return new MissionsWindowSession(_galaxyMapPlanet, _window);
        }

        private sealed class TestMission : Mission
        {
            public override bool ShouldRepeatAfterCompletion(GameRoot game)
            {
                return false;
            }
        }
    }
}
