using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using UnityEngine;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;
using GameFleet = Rebellion.Game.Units.Fleet;

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
            game.GetFactions()
                .Add(new Faction { InstanceID = _playerFactionId, DisplayName = "Alliance" });
            game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
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
                new GalaxyPlanetSector(),
                new Planet
                {
                    InstanceID = "planet",
                    DisplayName = "Corellia",
                    PlanetIconPath = _entityImagePath,
                },
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
                new StrategyMissionTarget(_planet, _planet.Planet),
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
                new StrategyMissionTarget(_planet, _planet.Planet),
                Array.Empty<IMissionParticipant>()
            );

            Assert.Throws<InvalidOperationException>(() => projector.Build(session, _window));
        }

        [Test]
        public void Build_MissionTabWithOpenDropdown_ReturnsMissionWorkflowPresentation()
        {
            MissionCreateWindowSession session = CreateSession(
                new StrategyMissionTarget(_planet, _planet.Planet),
                Array.Empty<IMissionParticipant>()
            );
            session.ToggleDropdown();
            FactionTheme playerTheme = _uiContext.GetPlayerFactionTheme();
            MissionCreateWindowTheme theme = playerTheme.StrategyWindows.MissionCreate;
            StrategyCheckboxTheme checkboxTheme = playerTheme.StrategyCheckboxTheme;

            MissionCreateWindowRenderData data = _projector.Build(session, _window);

            Assert.AreEqual(15, data.X);
            Assert.AreEqual(25, data.Y);
            Assert.AreEqual(MissionCreateWindowTab.Mission, data.ActiveTab);
            Assert.IsTrue(data.DropdownOpen);
            Assert.IsFalse(data.CanConfirm);
            Assert.AreSame(_uiContext.GetTexture(theme.TitleImagePath), data.TitleTexture);
            Assert.AreEqual("Diplomacy", data.MissionName);
            Assert.AreSame(
                _uiContext.GetTexture(
                    playerTheme.MissionIcons.GetImagePath(MissionIconKeys.Diplomacy, false)
                ),
                data.SelectedMissionTexture
            );
            Assert.AreEqual("Corellia", data.TargetName);
            Assert.AreSame(_uiContext.GetTexture(_entityImagePath), data.TargetTexture);
            Assert.IsTrue(data.UsePlanetTargetPreview);
            Assert.AreSame(
                _uiContext.GetTexture(theme.AgentsHeaderImagePath),
                data.AgentsHeaderTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(theme.DecoysHeaderImagePath),
                data.DecoysHeaderTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(checkboxTheme.FrameImagePath),
                data.CheckboxFrameTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(checkboxTheme.CheckMarkImagePath),
                data.CheckboxCheckMarkTexture
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
        public void Build_MissionOdds_UsesCurrentAgentAndDecoySplitForEveryIcon()
        {
            Officer primary = CreateOfficer("primary", "Primary", false);
            Officer decoy = CreateOfficer("decoy", "Decoy", false);
            List<MissionStartRequest> requests = new List<MissionStartRequest>();
            MissionCreateWindowProjector projector = new MissionCreateWindowProjector(
                () => _uiContext,
                request =>
                {
                    requests.Add(request);
                    return request.MissionTypeID == MissionTypeIDs.Diplomacy
                        ? new MissionOdds(80, 25)
                        : new MissionOdds(50, 10);
                }
            );
            MissionCreateWindowSession session = CreateSession(
                new StrategyMissionTarget(_planet, _planet.Planet),
                new IMissionParticipant[] { primary, decoy }
            );
            session.SelectParticipant(MissionParticipantRole.Agent, 1, 1);
            session.MoveSelectedParticipants(MissionParticipantRole.Agent);
            session.ToggleDropdown();

            MissionCreateWindowRenderData data = projector.Build(session, _window);

            Assert.AreEqual(60, data.SelectedMissionOdds.OverallSuccessPercent);
            Assert.AreEqual(25, data.SelectedMissionOdds.FoilPercent);
            Assert.AreEqual(60, data.DropdownItems[0].MissionOdds.OverallSuccessPercent);
            Assert.AreEqual(25, data.DropdownItems[0].MissionOdds.FoilPercent);
            Assert.AreEqual(45, data.DropdownItems[1].MissionOdds.OverallSuccessPercent);
            Assert.AreEqual(10, data.DropdownItems[1].MissionOdds.FoilPercent);
            Assert.AreEqual(2, requests.Count);
            Assert.IsTrue(
                requests.All(request => request.MainParticipants.SequenceEqual(new[] { primary }))
            );
            Assert.IsTrue(
                requests.All(request => request.DecoyParticipants.SequenceEqual(new[] { decoy }))
            );
        }

        [Test]
        public void Build_MissionOdds_UsesLatestObservedPlanetFleetState()
        {
            Planet latestPlanet = new Planet
            {
                InstanceID = _planet.Planet.InstanceID,
                DisplayName = _planet.Planet.DisplayName,
            };
            GameFleet fleet = new GameFleet
            {
                InstanceID = "latest-visible-fleet",
                OwnerInstanceID = "FNEMP1",
            };
            latestPlanet.AddChild(fleet);
            MissionStartRequest capturedRequest = null;
            MissionCreateWindowProjector projector = new MissionCreateWindowProjector(
                () => _uiContext,
                request =>
                {
                    capturedRequest = request;
                    return new MissionOdds(50, 20);
                },
                planetInstanceId =>
                    planetInstanceId == latestPlanet.InstanceID ? latestPlanet : null
            );
            MissionCreateWindowSession session = CreateSession(
                new StrategyMissionTarget(_planet, _planet.Planet),
                new[] { CreateOfficer("primary", "Primary", false) }
            );

            projector.Build(session, _window);

            Assert.AreSame(latestPlanet, capturedRequest.Location);
            Assert.AreSame(latestPlanet, capturedRequest.SelectedTarget);
            Assert.AreSame(
                fleet,
                ((Planet)capturedRequest.Location).GetChildren<GameFleet>().Single()
            );
        }

        [Test]
        public void Build_MissionOdds_MissingFromLatestObservedPlanetOmitsTargetEstimate()
        {
            Officer primary = CreateOfficer("primary", "Primary", false);
            Officer staleTarget = CreateOfficer("stale-target", "Stale Target", false);
            Planet latestPlanet = new Planet { InstanceID = _planet.Planet.InstanceID };
            int estimateCount = 0;
            MissionCreateWindowProjector projector = new MissionCreateWindowProjector(
                () => _uiContext,
                _ =>
                {
                    estimateCount++;
                    return new MissionOdds(50, 20);
                },
                _ => latestPlanet
            );
            MissionCreateWindowSession session = new MissionCreateWindowSession(
                _window,
                new StrategyMissionTarget(_planet, staleTarget),
                new[]
                {
                    CreateChoice(
                        MissionTypeIDs.Assassination,
                        "Assassination",
                        MissionTargetKind.Officer
                    ),
                },
                new[] { primary }
            );

            MissionCreateWindowRenderData data = projector.Build(session, _window);

            Assert.IsNull(data.SelectedMissionOdds);
            Assert.AreEqual(0, estimateCount);
        }

        [Test]
        public void Build_MissionOddsDisabled_OmitsEveryEstimate()
        {
            int estimateCount = 0;
            MissionCreateWindowProjector projector = new MissionCreateWindowProjector(
                () => _uiContext,
                _ =>
                {
                    estimateCount++;
                    return new MissionOdds(80, 25);
                }
            );
            MissionCreateWindowSession session = CreateSession(
                new StrategyMissionTarget(_planet, _planet.Planet),
                new[] { CreateOfficer("primary", "Primary", false) }
            );
            session.ToggleDropdown();
            session.SetShowMissionOdds(false);

            MissionCreateWindowRenderData data = projector.Build(session, _window);

            Assert.IsFalse(data.ShowMissionOdds);
            Assert.IsNull(data.SelectedMissionOdds);
            Assert.IsTrue(data.DropdownItems.All(item => item.MissionOdds == null));
            Assert.AreEqual(0, estimateCount);
        }

        [Test]
        public void Build_PlanetTargetWithoutArtwork_UsesPlanetPreviewFallback()
        {
            _planet.Planet.PlanetIconPath = null;
            MissionCreateWindowSession session = CreateSession(
                new StrategyMissionTarget(_planet, _planet.Planet),
                Array.Empty<IMissionParticipant>()
            );

            MissionCreateWindowRenderData data = _projector.Build(session, _window);

            Assert.IsNull(data.TargetTexture);
            Assert.IsTrue(data.UsePlanetTargetPreview);
        }

        [Test]
        public void Build_PersonnelTab_ReturnsSelectionAndTransitPresentation()
        {
            Officer selected = CreateOfficer("selected", "Selected", false);
            Officer inTransit = CreateOfficer("transit", "In Transit", true);
            MissionCreateWindowSession session = CreateSession(
                new StrategyMissionTarget(_planet, _planet.Planet),
                new IMissionParticipant[] { selected, inTransit }
            );
            session.SelectTab(MissionCreateWindowTab.Personnel);
            session.SelectParticipant(MissionParticipantRole.Agent, 0, 1);

            MissionCreateWindowRenderData data = _projector.Build(session, _window);

            Assert.AreEqual(MissionCreateWindowTab.Personnel, data.ActiveTab);
            Assert.IsTrue(data.CanConfirm);
            Assert.IsFalse(data.DropdownOpen);
            Assert.IsEmpty(data.DropdownItems);
            Assert.AreEqual(2, data.AgentRows.Count);
            Assert.AreEqual("Selected", data.AgentRows[0].Name);
            Assert.AreEqual((Color32)Color.white, data.AgentRows[0].NameColor);
            Assert.AreSame(
                _uiContext.GetTexture(
                    _uiContext
                        .GetPlayerFactionTheme()
                        .StrategyWindows.Defense.PersonnelBackgroundImagePath
                ),
                data.AgentRows[0].BackgroundTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(_entityImagePath),
                data.AgentRows[0].EntityTexture
            );
            Assert.AreEqual("In Transit", data.AgentRows[1].Name);
            Assert.AreEqual((Color32)Color.gray, data.AgentRows[1].NameColor);
            Assert.AreSame(
                _uiContext.GetTexture(_entityImagePath),
                data.AgentRows[1].BackgroundTexture
            );
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
                new StrategyMissionTarget(_planet, _planet.Planet),
                new IMissionParticipant[] { officer }
            );
            session.SelectTab(MissionCreateWindowTab.Personnel);

            MissionCreateWindowRenderData data = _projector.Build(session, _window);

            Assert.AreSame(
                _uiContext.GetTexture(_entityImagePath),
                data.AgentRows[0].BackgroundTexture
            );
        }

        [Test]
        public void Build_EntityTargetedMission_ReturnsEntityPreview()
        {
            Officer target = CreateOfficer("target", "Target Officer", false);
            MissionCreateWindowSession session = new MissionCreateWindowSession(
                _window,
                new StrategyMissionTarget(_planet, target),
                new[]
                {
                    CreateChoice(
                        MissionTypeIDs.Assassination,
                        "Assassination",
                        MissionTargetKind.Officer
                    ),
                },
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
                new StrategyMissionTarget(_planet, _planet.Planet),
                Array.Empty<StrategyMissionChoice>(),
                Array.Empty<IMissionParticipant>()
            );

            MissionCreateWindowRenderData data = _projector.Build(session, _window);

            Assert.AreEqual(string.Empty, data.MissionName);
            Assert.IsNull(data.SelectedMissionTexture);
        }

        private StrategyMissionChoice CreateChoice(
            string missionTypeId,
            string name,
            MissionTargetKind targetKind = MissionTargetKind.Planet
        )
        {
            return new StrategyMissionChoice(
                new MissionOption(missionTypeId, name, OfficerRating.Diplomacy, targetKind)
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
