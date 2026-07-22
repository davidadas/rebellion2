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
using Rebellion.SceneGraph;
using UnityEngine;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Missions
{
    [TestFixture]
    public class MissionsWindowProjectorTests
    {
        private const string _opposingFactionId = "FNEMP1";
        private const string _playerFactionId = "FNALL1";

        private string _entityImagePath;
        private TestMission _mission;
        private GalaxyMapPlanet _planet;
        private MissionsWindowProjector _projector;
        private Officer _target;
        private UIContext _uiContext;
        private Dictionary<string, ISceneNode> _visibleNodes;
        private UIWindow _window;
        private GameObject _windowObject;

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Alliance" }
            );
            game.Factions.Add(
                new Faction { InstanceID = _opposingFactionId, DisplayName = "Empire" }
            );
            game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _entityImagePath = _uiContext.GetPlayerFactionTheme().GalaxyBackground.ImagePath;
            _windowObject = new GameObject(
                "MissionsWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 12, 24, 300, 200, false, true, true);
            Planet planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _opposingFactionId,
            };
            _target = CreateOfficer("target", "Target Officer", false);
            _mission = new TestMission
            {
                InstanceID = "mission",
                ConfigKey = MissionTypeIDs.Diplomacy,
                DisplayName = "Diplomacy Mission",
                OwnerInstanceID = _playerFactionId,
                LocationInstanceID = _target.InstanceID,
            };
            _mission.MainParticipants.Add(CreateOfficer("agent", "Agent", true));
            _mission.DecoyParticipants.Add(CreateOfficer("decoy", "Decoy", false));
            planet.Missions.Add(_mission);
            _planet = new GalaxyMapPlanet(new GamePlanetSystem(), planet, string.Empty);
            _visibleNodes = new Dictionary<string, ISceneNode> { [_target.InstanceID] = _target };
            _projector = new MissionsWindowProjector(
                () => _uiContext,
                instanceId =>
                    _visibleNodes.TryGetValue(instanceId, out ISceneNode node) ? node : null
            );
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_MissingProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MissionsWindowProjector(null, _ => null)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new MissionsWindowProjector(() => _uiContext, null)
            );
        }

        [Test]
        public void Build_MissingSessionOrWindow_ThrowsArgumentNullException()
        {
            MissionsWindowSession session = new MissionsWindowSession(_planet, _window);

            Assert.Throws<ArgumentNullException>(() => _projector.Build(null, _window, true));
            Assert.Throws<ArgumentNullException>(() => _projector.Build(session, null, true));
        }

        [Test]
        public void Build_UnavailableContext_ThrowsInvalidOperationException()
        {
            MissionsWindowProjector projector = new MissionsWindowProjector(() => null, _ => null);
            MissionsWindowSession session = new MissionsWindowSession(_planet, _window);

            Assert.Throws<InvalidOperationException>(() => projector.Build(session, _window, true));
        }

        [Test]
        public void Build_SelectedMission_ReturnsCompleteActivePresentation()
        {
            MissionsWindowSession session = new MissionsWindowSession(_planet, _window);
            FactionTheme ownerTheme = _uiContext.GetTheme(_opposingFactionId);
            FactionTheme playerTheme = _uiContext.GetTheme(_playerFactionId);
            MissionsWindowTheme theme = playerTheme.StrategyWindows.Missions;

            MissionsWindowRenderData data = _projector.Build(session, _window, true);

            Assert.AreEqual(12, data.X);
            Assert.AreEqual(24, data.Y);
            Assert.AreSame(
                _uiContext.GetTexture(ownerTheme.WindowTitleTheme.ActiveImagePath),
                data.TitleTexture
            );
            Assert.AreEqual("Corellia", data.Caption);
            Assert.AreEqual(MissionParticipantRole.Agent, data.ActiveRole);
            Assert.AreEqual(0, data.SelectedMissionIndex);
            Assert.IsTrue(data.HasSelectedMission);
            Assert.AreEqual("Target Officer", data.TargetName);
            Assert.AreSame(_uiContext.GetTexture(_entityImagePath), data.TargetTexture);
            Assert.AreEqual(1, data.Missions.Count);
            Assert.AreEqual("Diplomacy Mission", data.Missions[0].Name);
            Assert.AreSame(
                _uiContext.GetTexture(
                    playerTheme.MissionIcons.GetImagePath(MissionIconKeys.Diplomacy, true)
                ),
                data.Missions[0].IconTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(theme.SelectionImagePath),
                data.Missions[0].SelectionTexture
            );
            Assert.AreEqual(2, data.Tabs.Count);
            Assert.AreEqual(MissionParticipantRole.Agent, data.Tabs[0].Role);
            Assert.AreSame(
                _uiContext.GetTexture(theme.AgentsTab.GetImagePath(0)),
                data.Tabs[0].Texture
            );
            Assert.AreEqual(MissionParticipantRole.Decoy, data.Tabs[1].Role);
            Assert.AreSame(
                _uiContext.GetTexture(theme.DecoysTab.GetImagePath(1)),
                data.Tabs[1].Texture
            );
            Assert.AreEqual(1, data.Participants.Count);
            Assert.AreEqual("Agent", data.Participants[0].Name);
            Assert.AreEqual((Color32)Color.white, data.Participants[0].NameColor);
            Assert.AreSame(
                _uiContext.GetTexture(_entityImagePath),
                data.Participants[0].BackgroundTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(_entityImagePath),
                data.Participants[0].EntityTexture
            );
            Assert.IsTrue(data.Participants[0].UseInTransitBackground);
        }

        [Test]
        public void Build_InactiveWindowAndDecoyRole_ReturnsInactivePresentation()
        {
            MissionsWindowSession session = new MissionsWindowSession(_planet, _window);
            session.SelectRole(MissionParticipantRole.Decoy);
            FactionTheme ownerTheme = _uiContext.GetTheme(_opposingFactionId);

            MissionsWindowRenderData data = _projector.Build(session, _window, false);

            Assert.AreSame(
                _uiContext.GetTexture(ownerTheme.WindowTitleTheme.InactiveImagePath),
                data.TitleTexture
            );
            Assert.AreEqual(MissionParticipantRole.Decoy, data.ActiveRole);
            Assert.AreEqual(1, data.Participants.Count);
            Assert.AreEqual("Decoy", data.Participants[0].Name);
            Assert.IsNull(data.Participants[0].BackgroundTexture);
            Assert.IsFalse(data.Participants[0].UseInTransitBackground);
        }

        [Test]
        public void Build_ParticipantCarriedByMovingFleet_UsesTransitPresentation()
        {
            Officer participant = (Officer)_mission.MainParticipants[0];
            participant.Movement = null;
            CapitalShip ship = new CapitalShip();
            GameFleet fleet = new GameFleet { Movement = new MovementState() };
            ship.SetParent(fleet);
            participant.SetParent(ship);
            MissionsWindowSession session = new MissionsWindowSession(_planet, _window);

            MissionsWindowRenderData data = _projector.Build(session, _window, true);

            Assert.AreSame(
                _uiContext.GetTexture(_entityImagePath),
                data.Participants[0].BackgroundTexture
            );
            Assert.IsTrue(data.Participants[0].UseInTransitBackground);
        }

        [Test]
        public void Build_NoMissions_ReturnsEmptySelectionPresentation()
        {
            _planet.Planet.Missions.Clear();
            MissionsWindowSession session = new MissionsWindowSession(_planet, _window);

            MissionsWindowRenderData data = _projector.Build(session, _window, true);

            Assert.AreEqual(-1, data.SelectedMissionIndex);
            Assert.IsFalse(data.HasSelectedMission);
            Assert.AreEqual(string.Empty, data.TargetName);
            Assert.IsNull(data.TargetTexture);
            Assert.IsEmpty(data.Missions);
            Assert.IsEmpty(data.Participants);
        }

        [Test]
        public void Build_MissingVisibleLocation_FallsBackToPlanetPresentation()
        {
            _visibleNodes.Clear();
            MissionsWindowSession session = new MissionsWindowSession(_planet, _window);

            MissionsWindowRenderData data = _projector.Build(session, _window, true);

            Assert.AreEqual("Corellia", data.TargetName);
            Assert.IsNull(data.TargetTexture);
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

        private sealed class TestMission : Mission
        {
            public override bool ShouldRepeatAfterCompletion(GameRoot game)
            {
                return false;
            }
        }
    }
}
