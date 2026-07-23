using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using UnityEngine;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Missions
{
    [TestFixture]
    public class MissionCreateWindowProjectorTests
    {
        private const string _playerFactionId = "FNALL1";

        private string _entityImagePath;
        private List<StrategyMissionChoice> _missionChoices;
        private GalaxyMapPlanet _planet;
        private MissionCreateWindowProjector _projector;
        private UIContext _uiContext;
        private UIWindow _window;
        private GameObject _windowObject;

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Alliance" }
            );
            game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _entityImagePath = _uiContext.GetPlayerFactionTheme().GalaxyBackground.ImagePath;
            _windowObject = new GameObject(
                "MissionCreateWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 15, 25, 300, 200, false, true, true);
            _planet = new GalaxyMapPlanet(
                new GamePlanetSystem(),
                new Planet { InstanceID = "planet", DisplayName = "Corellia" },
                string.Empty
            );
            _missionChoices = new List<StrategyMissionChoice>
            {
                CreateChoice(MissionTypeIDs.Diplomacy, "Diplomacy"),
                CreateChoice(MissionTypeIDs.Espionage, "Espionage"),
            };
            _projector = new MissionCreateWindowProjector(() => _uiContext);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new MissionCreateWindowProjector(null));
        }

        [Test]
        public void Build_MissingSessionOrWindow_ThrowsArgumentNullException()
        {
            MissionCreateWindowSession session = CreateSession(
                new StrategyMissionTarget(_planet, null),
                Array.Empty<IMissionParticipant>()
            );

            Assert.Throws<ArgumentNullException>(() => _projector.Build(null, _window));
            Assert.Throws<ArgumentNullException>(() => _projector.Build(session, null));
        }

        [Test]
        public void Build_UnavailableContext_ThrowsInvalidOperationException()
        {
            MissionCreateWindowProjector projector = new MissionCreateWindowProjector(() => null);
            MissionCreateWindowSession session = CreateSession(
                new StrategyMissionTarget(_planet, null),
                Array.Empty<IMissionParticipant>()
            );

            Assert.Throws<InvalidOperationException>(() => projector.Build(session, _window));
        }

        [Test]
        public void Build_MissionTabWithOpenDropdown_ReturnsMissionWorkflowPresentation()
        {
            MissionCreateWindowSession session = CreateSession(
                new StrategyMissionTarget(_planet, null),
                Array.Empty<IMissionParticipant>()
            );
            session.ToggleDropdown();
            FactionTheme playerTheme = _uiContext.GetPlayerFactionTheme();
            MissionCreateWindowTheme theme = playerTheme.StrategyWindows.MissionCreate;

            MissionCreateWindowRenderData data = _projector.Build(session, _window);

            Assert.AreEqual(15, data.X);
            Assert.AreEqual(25, data.Y);
            Assert.AreEqual(MissionCreateWindowTab.Mission, data.ActiveTab);
            Assert.IsTrue(data.DropdownOpen);
            Assert.AreSame(_uiContext.GetTexture(theme.TitleImagePath), data.TitleTexture);
            Assert.AreEqual("Diplomacy", data.MissionName);
            Assert.AreSame(
                _uiContext.GetTexture(
                    playerTheme.MissionIcons.GetImagePath(MissionIconKeys.Diplomacy, false)
                ),
                data.SelectedMissionTexture
            );
            Assert.AreEqual("Corellia", data.TargetName);
            Assert.IsNull(data.TargetTexture);
            Assert.IsTrue(data.UsePlanetTargetPreview);
            Assert.AreSame(
                _uiContext.GetTexture(theme.AgentsHeaderImagePath),
                data.AgentsHeaderTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(theme.DecoysHeaderImagePath),
                data.DecoysHeaderTexture
            );
            Assert.AreEqual(2, data.Tabs.Count);
            Assert.AreEqual(MissionCreateWindowTab.Mission, data.Tabs[0].Tab);
            Assert.AreSame(
                _uiContext.GetTexture(theme.MissionTab.GetImagePath(0)),
                data.Tabs[0].Texture
            );
            Assert.AreSame(
                _uiContext.GetTexture(theme.MissionTab.GetImagePath(0)),
                data.Tabs[0].PressedTexture
            );
            Assert.AreEqual(MissionCreateWindowTab.Personnel, data.Tabs[1].Tab);
            Assert.AreSame(
                _uiContext.GetTexture(theme.PersonnelTab.GetImagePath(1)),
                data.Tabs[1].Texture
            );
            Assert.AreEqual(2, data.DropdownItems.Count);
            Assert.AreEqual("Diplomacy", data.DropdownItems[0].Label);
            Assert.AreEqual((Color32)Color.white, data.DropdownItems[0].LabelColor);
            Assert.AreEqual("Espionage", data.DropdownItems[1].Label);
            Assert.AreEqual((Color32)Color.gray, data.DropdownItems[1].LabelColor);
            Assert.IsEmpty(data.AgentRows);
            Assert.IsEmpty(data.DecoyRows);
        }

        [Test]
        public void Build_PersonnelTab_ReturnsSelectionAndTransitPresentation()
        {
            Officer selected = CreateOfficer("selected", "Selected", false);
            Officer inTransit = CreateOfficer("transit", "In Transit", true);
            MissionCreateWindowSession session = CreateSession(
                new StrategyMissionTarget(_planet, null),
                new IMissionParticipant[] { selected, inTransit }
            );
            session.SelectTab(MissionCreateWindowTab.Personnel);
            session.SelectParticipant(MissionParticipantRole.Agent, 0, 1);

            MissionCreateWindowRenderData data = _projector.Build(session, _window);

            Assert.AreEqual(MissionCreateWindowTab.Personnel, data.ActiveTab);
            Assert.IsFalse(data.DropdownOpen);
            Assert.IsEmpty(data.DropdownItems);
            Assert.AreEqual(2, data.AgentRows.Count);
            Assert.AreEqual("Selected", data.AgentRows[0].Name);
            Assert.AreEqual((Color32)Color.white, data.AgentRows[0].NameColor);
            Assert.IsNull(data.AgentRows[0].BackgroundTexture);
            Assert.AreSame(
                _uiContext.GetTexture(_entityImagePath),
                data.AgentRows[0].EntityTexture
            );
            Assert.IsFalse(data.AgentRows[0].UseInTransitBackground);
            Assert.AreEqual("In Transit", data.AgentRows[1].Name);
            Assert.AreEqual((Color32)Color.gray, data.AgentRows[1].NameColor);
            Assert.AreSame(
                _uiContext.GetTexture(_entityImagePath),
                data.AgentRows[1].BackgroundTexture
            );
            Assert.IsTrue(data.AgentRows[1].UseInTransitBackground);
            Assert.IsEmpty(data.DecoyRows);
        }

        [Test]
        public void Build_PersonnelCarriedByMovingFleet_UsesTransitPresentation()
        {
            Officer officer = CreateOfficer("officer", "Officer", false);
            CapitalShip ship = new CapitalShip();
            GameFleet fleet = new GameFleet { Movement = new MovementState() };
            ship.SetParent(fleet);
            officer.SetParent(ship);
            MissionCreateWindowSession session = CreateSession(
                new StrategyMissionTarget(_planet, null),
                new IMissionParticipant[] { officer }
            );
            session.SelectTab(MissionCreateWindowTab.Personnel);

            MissionCreateWindowRenderData data = _projector.Build(session, _window);

            Assert.AreSame(
                _uiContext.GetTexture(_entityImagePath),
                data.AgentRows[0].BackgroundTexture
            );
            Assert.IsTrue(data.AgentRows[0].UseInTransitBackground);
        }

        [Test]
        public void Build_EntityTarget_ReturnsEntityPreview()
        {
            Officer target = CreateOfficer("target", "Target Officer", false);
            MissionCreateWindowSession session = CreateSession(
                new StrategyMissionTarget(_planet, target),
                Array.Empty<IMissionParticipant>()
            );

            MissionCreateWindowRenderData data = _projector.Build(session, _window);

            Assert.AreEqual("Target Officer", data.TargetName);
            Assert.AreSame(_uiContext.GetTexture(_entityImagePath), data.TargetTexture);
            Assert.IsFalse(data.UsePlanetTargetPreview);
        }

        [Test]
        public void Build_EmptyChoices_ReturnsEmptyMissionSelection()
        {
            MissionCreateWindowSession session = new MissionCreateWindowSession(
                _window,
                new StrategyMissionTarget(_planet, null),
                Array.Empty<StrategyMissionChoice>(),
                Array.Empty<IMissionParticipant>()
            );

            MissionCreateWindowRenderData data = _projector.Build(session, _window);

            Assert.AreEqual(string.Empty, data.MissionName);
            Assert.IsNull(data.SelectedMissionTexture);
        }

        private StrategyMissionChoice CreateChoice(string missionTypeId, string name)
        {
            return new StrategyMissionChoice(
                new MissionOption(missionTypeId, name, OfficerRating.Diplomacy)
            );
        }

        private Officer CreateOfficer(string instanceId, string displayName, bool inTransit)
        {
            return new Officer
            {
                InstanceID = instanceId,
                DisplayName = displayName,
                DisplayImagePath = _entityImagePath,
                SmallDisplayImagePath = _entityImagePath,
                InTransitSmallImagePath = _entityImagePath,
                Movement = inTransit ? new MovementState() : null,
            };
        }

        private MissionCreateWindowSession CreateSession(
            StrategyMissionTarget target,
            IEnumerable<IMissionParticipant> participants
        )
        {
            return new MissionCreateWindowSession(_window, target, _missionChoices, participants);
        }
    }
}
