using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Missions
{
    [TestFixture]
    public class MissionCreateWindowSessionTests
    {
        private List<StrategyMissionChoice> _choices;
        private List<IMissionParticipant> _participants;
        private StrategyMissionTarget _target;
        private UIWindow _window;
        private GameObject _windowObject;

        [SetUp]
        public void SetUp()
        {
            _windowObject = new GameObject(
                "MissionCreateWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 10, 20, 300, 200, false, true, true);
            GamePlanetSystem system = new GamePlanetSystem();
            GalaxyMapPlanet planet = new GalaxyMapPlanet(
                system,
                new Planet { InstanceID = "planet" },
                string.Empty
            );
            _target = new StrategyMissionTarget(planet, null);
            _choices = new List<StrategyMissionChoice>
            {
                CreateChoice(MissionTypeIDs.Diplomacy, "Diplomacy"),
                CreateChoice(MissionTypeIDs.Espionage, "Espionage"),
            };
            _participants = new List<IMissionParticipant>
            {
                CreateOfficer("first", "First"),
                CreateOfficer("second", "Second"),
                CreateOfficer("third", "Third"),
            };
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_MissingRequiredInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MissionCreateWindowSession(null, _target, _choices, _participants)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new MissionCreateWindowSession(_window, null, _choices, _participants)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new MissionCreateWindowSession(_window, _target, null, _participants)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new MissionCreateWindowSession(_window, _target, _choices, null)
            );
        }

        [Test]
        public void Constructor_ChoicesAndParticipants_ReturnsInitialMissionState()
        {
            MissionCreateWindowSession session = CreateSession();

            Assert.AreSame(_window, session.Window);
            Assert.AreSame(_target, session.Target);
            Assert.AreEqual(2, session.Choices.Count);
            Assert.AreEqual(3, session.Agents.Count);
            Assert.IsEmpty(session.Decoys);
            Assert.AreEqual(0, session.SelectedMissionIndex);
            Assert.AreSame(_choices[0], session.SelectedChoice);
            Assert.AreEqual(MissionCreateWindowTab.Mission, session.ActiveTab);
            Assert.IsFalse(session.DropdownOpen);
            Assert.IsEmpty(session.SelectedAgents);
            Assert.IsEmpty(session.SelectedDecoys);
        }

        [Test]
        public void Constructor_EmptyChoices_ReturnsNoSelectedMission()
        {
            MissionCreateWindowSession session = new MissionCreateWindowSession(
                _window,
                _target,
                Array.Empty<StrategyMissionChoice>(),
                _participants
            );

            Assert.AreEqual(-1, session.SelectedMissionIndex);
            Assert.IsNull(session.SelectedChoice);
        }

        [Test]
        public void Dropdown_ToggleAndDismiss_UpdatesVisibility()
        {
            MissionCreateWindowSession session = CreateSession();

            session.ToggleDropdown();
            bool dismissed = session.DismissDropdown();
            bool dismissedAgain = session.DismissDropdown();

            Assert.IsTrue(dismissed);
            Assert.IsFalse(dismissedAgain);
            Assert.IsFalse(session.DropdownOpen);
        }

        [Test]
        public void SelectMission_ValidIndex_SelectsChoiceAndClosesDropdown()
        {
            MissionCreateWindowSession session = CreateSession();
            session.ToggleDropdown();

            bool selected = session.SelectMission(1);

            Assert.IsTrue(selected);
            Assert.AreEqual(1, session.SelectedMissionIndex);
            Assert.AreSame(_choices[1], session.SelectedChoice);
            Assert.IsFalse(session.DropdownOpen);
            Assert.IsTrue(session.IsMissionIndexValid(0));
            Assert.IsFalse(session.IsMissionIndexValid(-1));
            Assert.IsFalse(session.IsMissionIndexValid(2));
        }

        [Test]
        public void SelectMission_InvalidIndex_PreservesSelection()
        {
            MissionCreateWindowSession session = CreateSession();

            bool selected = session.SelectMission(4);

            Assert.IsFalse(selected);
            Assert.AreEqual(0, session.SelectedMissionIndex);
        }

        [Test]
        public void SelectTab_ValidTab_ChangesTabAndClosesDropdown()
        {
            MissionCreateWindowSession session = CreateSession();
            session.ToggleDropdown();

            bool selected = session.SelectTab(MissionCreateWindowTab.Personnel);

            Assert.IsTrue(selected);
            Assert.AreEqual(MissionCreateWindowTab.Personnel, session.ActiveTab);
            Assert.IsFalse(session.DropdownOpen);
            Assert.IsTrue(session.IsTabValid(MissionCreateWindowTab.Mission));
            Assert.IsTrue(session.IsTabValid(MissionCreateWindowTab.Personnel));
            Assert.IsFalse(session.IsTabValid((MissionCreateWindowTab)10));
        }

        [Test]
        public void SelectTab_InvalidTab_PreservesActiveTab()
        {
            MissionCreateWindowSession session = CreateSession();

            bool selected = session.SelectTab((MissionCreateWindowTab)10);

            Assert.IsFalse(selected);
            Assert.AreEqual(MissionCreateWindowTab.Mission, session.ActiveTab);
        }

        [Test]
        public void MoveSelectedParticipants_AgentSelection_MovesParticipantInSourceOrder()
        {
            MissionCreateWindowSession session = CreateSession();
            session.SelectParticipant(MissionParticipantRole.Agent, 1, 1);

            bool moved = session.MoveSelectedParticipants(MissionParticipantRole.Agent);

            Assert.IsTrue(moved);
            Assert.AreEqual(2, session.Agents.Count);
            Assert.AreEqual("first", ((Officer)session.Agents[0]).InstanceID);
            Assert.AreEqual("third", ((Officer)session.Agents[1]).InstanceID);
            Assert.AreEqual(1, session.Decoys.Count);
            Assert.AreEqual("second", ((Officer)session.Decoys[0]).InstanceID);
            Assert.IsEmpty(session.SelectedAgents);
        }

        [Test]
        public void SelectParticipant_DoubleClick_MovesParticipantToOppositeRole()
        {
            MissionCreateWindowSession session = CreateSession();

            bool selected = session.SelectParticipant(MissionParticipantRole.Agent, 0, 2);

            Assert.IsTrue(selected);
            Assert.AreEqual(2, session.Agents.Count);
            Assert.AreEqual(1, session.Decoys.Count);
            Assert.AreEqual("first", ((Officer)session.Decoys[0]).InstanceID);
            Assert.IsEmpty(session.SelectedAgents);
        }

        [Test]
        public void ParticipantOperations_InvalidRoleOrIndex_ReturnFalse()
        {
            MissionCreateWindowSession session = CreateSession();

            bool selected = session.SelectParticipant(MissionParticipantRole.Agent, 5, 1);
            bool unsupportedSelection = session.SelectParticipant((MissionParticipantRole)10, 0, 1);
            bool moved = session.MoveSelectedParticipants(MissionParticipantRole.Agent);
            bool unsupportedMove = session.MoveSelectedParticipants((MissionParticipantRole)10);

            Assert.IsFalse(selected);
            Assert.IsFalse(unsupportedSelection);
            Assert.IsFalse(moved);
            Assert.IsFalse(unsupportedMove);
            Assert.IsTrue(session.IsParticipantIndexValid(MissionParticipantRole.Agent, 0));
            Assert.IsFalse(session.IsParticipantIndexValid(MissionParticipantRole.Agent, 5));
            Assert.IsFalse(session.IsParticipantIndexValid(MissionParticipantRole.Decoy, 0));
            Assert.IsFalse(session.IsParticipantIndexValid((MissionParticipantRole)10, 0));
        }

        private static StrategyMissionChoice CreateChoice(string typeId, string displayName)
        {
            return new StrategyMissionChoice(
                new MissionOption(typeId, displayName, OfficerRating.Diplomacy)
            );
        }

        private static Officer CreateOfficer(string instanceId, string displayName)
        {
            return new Officer { InstanceID = instanceId, DisplayName = displayName };
        }

        private MissionCreateWindowSession CreateSession()
        {
            return new MissionCreateWindowSession(_window, _target, _choices, _participants);
        }
    }
}
