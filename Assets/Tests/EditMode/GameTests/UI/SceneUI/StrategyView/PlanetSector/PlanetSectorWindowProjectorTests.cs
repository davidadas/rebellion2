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
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.PlanetSector
{
    [TestFixture]
    public class PlanetSectorWindowProjectorTests
    {
        private const string _opposingFactionId = "FNEMP1";
        private const string _playerFactionId = "FNALL1";

        private GameRoot _game;
        private GalaxyPlanetSector _planetSector;
        private PlanetSectorWindowProjector _projector;
        private UIContext _uiContext;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _game
                .GetFactions()
                .Add(new Faction { InstanceID = _playerFactionId, DisplayName = "Alliance" });
            _game
                .GetFactions()
                .Add(new Faction { InstanceID = _opposingFactionId, DisplayName = "Empire" });
            _game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = TestContent.CreateUIContext(
                _game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _planetSector = new GalaxyPlanetSector
            {
                InstanceID = "sector",
                DisplayName = "Corellian",
                PositionX = 10,
                PositionY = 20,
            };
            _projector = new PlanetSectorWindowProjector(() => _uiContext);
        }

        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new PlanetSectorWindowProjector(null));
        }

        [Test]
        public void ProjectWaypointRoutes_PlayerAndOpposingRoutes_ReturnsVisiblePlayerRoute()
        {
            Planet origin = CreatePlanet("origin", _playerFactionId, 10, 20);
            Planet firstDestination = CreatePlanet("first", _playerFactionId, 13, 24);
            Planet secondDestination = CreatePlanet("second", _playerFactionId, 16, 28);
            _game.AttachNode(_planetSector, _game.GetGalaxyMap());
            _game.AttachNode(origin, _planetSector);
            _game.AttachNode(firstDestination, _planetSector);
            _game.AttachNode(secondDestination, _planetSector);
            GameFleet playerFleet = new GameFleet(_playerFactionId, "Player Fleet")
            {
                Movement = new MovementState { OriginPosition = origin.GetPosition() },
            };
            playerFleet.Waypoints.Add(firstDestination.InstanceID);
            playerFleet.Waypoints.Add(secondDestination.InstanceID);
            _game.AttachNode(playerFleet, origin);
            GameFleet opposingFleet = new GameFleet(_opposingFactionId, "Opposing Fleet");
            opposingFleet.Waypoints.Add(firstDestination.InstanceID);
            _game.AttachNode(opposingFleet, origin);
            GalaxyMapPlanet[] visiblePlanets =
            {
                new GalaxyMapPlanet(_planetSector, origin, string.Empty),
                new GalaxyMapPlanet(_planetSector, firstDestination, string.Empty),
                new GalaxyMapPlanet(_planetSector, secondDestination, string.Empty),
            };

            _projector.ProjectWaypointRoutes(
                new GalaxyMapSector(_planetSector, visiblePlanets),
                null,
                out List<PlanetSectorWaypointSegmentRenderData> segments,
                out List<PlanetSectorWaypointRenderData> waypoints,
                new[] { playerFleet.InstanceID, opposingFleet.InstanceID }
            );

            Assert.AreEqual(2, segments.Count);
            Assert.AreEqual(0, segments[0].StartPlanetIndex);
            Assert.AreEqual(1, segments[0].EndPlanetIndex);
            Assert.AreEqual(1, segments[1].StartPlanetIndex);
            Assert.AreEqual(2, segments[1].EndPlanetIndex);
            Assert.AreEqual(2, waypoints.Count);
            Assert.AreEqual(1, waypoints[0].Order);
            Assert.AreEqual(1, waypoints[0].PlanetIndex);
            Assert.AreEqual(2, waypoints[1].Order);
            Assert.AreEqual(2, waypoints[1].PlanetIndex);
        }

        [Test]
        public void ProjectWaypointRoutes_UnselectedRoute_ReturnsRouteOnlyWhenAllRoutesEnabled()
        {
            Planet origin = CreatePlanet("origin", _playerFactionId, 10, 20);
            Planet destination = CreatePlanet("destination", _playerFactionId, 13, 24);
            _game.AttachNode(_planetSector, _game.GetGalaxyMap());
            _game.AttachNode(origin, _planetSector);
            _game.AttachNode(destination, _planetSector);
            GameFleet fleet = new GameFleet(_playerFactionId, "Player Fleet");
            fleet.Waypoints.Add(destination.InstanceID);
            _game.AttachNode(fleet, origin);
            GalaxyMapPlanet[] visiblePlanets =
            {
                new GalaxyMapPlanet(_planetSector, origin, string.Empty),
                new GalaxyMapPlanet(_planetSector, destination, string.Empty),
            };
            GalaxyMapSector mapSector = new GalaxyMapSector(_planetSector, visiblePlanets);

            _projector.ProjectWaypointRoutes(
                mapSector,
                null,
                out List<PlanetSectorWaypointSegmentRenderData> defaultSegments,
                out List<PlanetSectorWaypointRenderData> defaultWaypoints
            );
            _projector.ProjectWaypointRoutes(
                mapSector,
                null,
                out List<PlanetSectorWaypointSegmentRenderData> allSegments,
                out List<PlanetSectorWaypointRenderData> allWaypoints,
                showAllRoutes: true
            );

            Assert.IsEmpty(defaultSegments);
            Assert.IsEmpty(defaultWaypoints);
            Assert.AreEqual(1, allSegments.Count);
            Assert.AreEqual(1, allWaypoints.Count);
        }

        [Test]
        public void ProjectWaypointRoutes_UncommittedPlan_ReturnsVisiblePreview()
        {
            Planet origin = CreatePlanet("origin", _playerFactionId, 10, 20);
            Planet destination = CreatePlanet("destination", _playerFactionId, 13, 24);
            _game.AttachNode(_planetSector, _game.GetGalaxyMap());
            _game.AttachNode(origin, _planetSector);
            _game.AttachNode(destination, _planetSector);
            GameFleet fleet = new GameFleet(_playerFactionId, "Player Fleet");
            _game.AttachNode(fleet, origin);
            StrategyWindowTargetingSource plan = new StrategyWindowTargetingSource(
                null,
                StrategyMenuAction.WaypointMove,
                0,
                0,
                new[] { fleet }
            );
            plan.TryAppendWaypoint(destination.InstanceID);
            GalaxyMapPlanet[] visiblePlanets =
            {
                new GalaxyMapPlanet(_planetSector, origin, string.Empty),
                new GalaxyMapPlanet(_planetSector, destination, string.Empty),
            };

            _projector.ProjectWaypointRoutes(
                new GalaxyMapSector(_planetSector, visiblePlanets),
                plan,
                out List<PlanetSectorWaypointSegmentRenderData> segments,
                out List<PlanetSectorWaypointRenderData> waypoints
            );

            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual(0, segments[0].StartPlanetIndex);
            Assert.AreEqual(1, segments[0].EndPlanetIndex);
            Assert.AreEqual(1, waypoints.Count);
            Assert.AreEqual(1, waypoints[0].Order);
            Assert.AreEqual(1, waypoints[0].PlanetIndex);
            Assert.IsEmpty(fleet.Waypoints);
            Assert.IsNull(fleet.Movement);
        }

        [Test]
        public void CreateRenderData_UnavailableContext_ThrowsInvalidOperationException()
        {
            PlanetSectorWindowProjector projector = new PlanetSectorWindowProjector(() => null);

            Assert.Throws<InvalidOperationException>(() =>
                projector.CreateRenderData(null, null, PlanetIcon.None, null, PlanetIcon.None)
            );
        }

        [Test]
        public void CreateRenderData_NullSector_ReturnsEmptyPresentation()
        {
            PlanetSectorWindowRenderData data = _projector.CreateRenderData(
                null,
                null,
                PlanetIcon.None,
                null,
                PlanetIcon.None
            );

            Assert.AreEqual(string.Empty, data.Title);
            Assert.IsEmpty(data.Planets);
        }

        [Test]
        public void CreateRenderData_CompositePlanet_ReturnsCompletePresentation()
        {
            Planet planet = CreatePlanet("planet", _playerFactionId, 13, 25);
            planet.IsHeadquarters = true;
            planet.EnergyCapacity = 3;
            planet.NumRawResourceNodes = 4;
            planet.PopularSupport[_playerFactionId] = 75;
            planet.PopularSupport[_opposingFactionId] = 25;
            planet.AddTestChild(CreateBuilding(BuildingType.Mine));
            planet.AddTestChild(CreateBuilding(BuildingType.Defense));
            planet.AddTestChild(new GameFleet(_playerFactionId, "Player Fleet"));
            planet.AddTestChild(new GameFleet(_opposingFactionId, "Opposing Fleet"));
            planet.AddTestChild(new TestMission(_playerFactionId));
            planet.AddTestChild(new TestMission(_opposingFactionId));
            string planetTexturePath = _uiContext
                .GetPlayerFactionTheme()
                .GalaxyBackground.ImagePath;
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSector, planet, planetTexturePath)
            );
            FactionTheme playerTheme = _uiContext.GetTheme(_playerFactionId);
            FactionTheme opposingTheme = _uiContext.GetTheme(_opposingFactionId);

            PlanetSectorWindowRenderData data = _projector.CreateRenderData(
                sector,
                planet.InstanceID,
                PlanetIcon.Fleet,
                planet.InstanceID,
                PlanetIcon.Mission
            );

            Assert.AreEqual("Corellian", data.Title);
            Assert.AreEqual(1, data.Planets.Count);
            PlanetSectorPlanetRenderData presentation = data.Planets[0];
            Assert.AreEqual(0, presentation.PlanetIndex);
            Assert.AreEqual(new Vector2Int(3, 5), presentation.GalaxyOffset);
            Assert.AreSame(_uiContext.GetTexture(planetTexturePath), presentation.PlanetTexture);
            Assert.AreSame(
                _uiContext.GetTexture(
                    playerTheme.PlanetOverlayTheme.PlanetOverlayIcons.Buildings.NormalImagePath
                ),
                presentation.FacilityTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    playerTheme.PlanetOverlayTheme.PlanetOverlayIcons.Buildings.HoverImagePath
                ),
                presentation.FacilityPressedTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    playerTheme.PlanetOverlayTheme.PlanetOverlayIcons.Defenses.NormalImagePath
                ),
                presentation.DefenseTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    playerTheme.PlanetOverlayTheme.PlanetOverlayIcons.Defenses.HoverImagePath
                ),
                presentation.DefensePressedTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    opposingTheme.PlanetOverlayTheme.PlanetOverlayIcons.Fleets.NormalImagePath
                ),
                presentation.FleetTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    opposingTheme.PlanetOverlayTheme.PlanetOverlayIcons.Fleets.HoverImagePath
                ),
                presentation.FleetPressedTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    opposingTheme.PlanetOverlayTheme.PlanetOverlayIcons.Missions.NormalImagePath
                ),
                presentation.MissionTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    opposingTheme.PlanetOverlayTheme.PlanetOverlayIcons.Missions.HoverImagePath
                ),
                presentation.MissionPressedTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    playerTheme.PlanetOverlayTheme.PlanetSectorHeadquartersImagePath
                ),
                presentation.HeadquartersTexture
            );
            Assert.AreEqual("Corellia", presentation.Name);
            Assert.AreEqual((Color32)playerTheme.GetPrimaryColor(), presentation.NameColor);
            Assert.AreEqual(PlanetIcon.Fleet, presentation.SelectedIcon);
            Assert.AreEqual(PlanetIcon.Mission, presentation.HoveredIcon);
            Assert.IsTrue(presentation.EnergyBar.Visible);
            Assert.AreEqual(3, presentation.EnergyBar.CellCount);
            Assert.AreEqual(2, presentation.EnergyBar.LitCells);
            Assert.AreEqual(new Color32(255, 255, 255, 255), presentation.EnergyBar.FillColor);
            Assert.AreEqual(new Color32(64, 132, 255, 255), presentation.EnergyBar.EmptyColor);
            Assert.AreEqual(
                new Color32(160, 160, 160, 255),
                presentation.EnergyBar.BackgroundColor
            );
            Assert.AreEqual(4, presentation.RawResourceBar.CellCount);
            Assert.AreEqual(1, presentation.RawResourceBar.LitCells);
            Assert.AreEqual(new Color32(255, 255, 84, 255), presentation.RawResourceBar.FillColor);
            Assert.AreEqual(new Color32(236, 106, 46, 255), presentation.RawResourceBar.EmptyColor);
            Assert.IsTrue(presentation.SupportBar.Visible);
            Assert.AreEqual(0.75f, presentation.SupportBar.FillRatio);
            Assert.AreEqual(
                (Color32)playerTheme.GetPrimaryColor(),
                presentation.SupportBar.FillColor
            );
            Assert.AreEqual(
                (Color32)opposingTheme.GetPrimaryColor(),
                presentation.SupportBar.BackgroundColor
            );
        }

        [Test]
        public void CreateRenderData_PopularSupport_ReturnsSupportedOpposingFactionColor()
        {
            _game
                .GetFactions()
                .Insert(1, new Faction { InstanceID = "SPECTATOR", DisplayName = "Spectator" });
            Planet planet = CreatePlanet("planet", _playerFactionId, 13, 25);
            planet.PopularSupport[_playerFactionId] = 75;
            planet.PopularSupport[_opposingFactionId] = 25;
            FactionTheme opposingTheme = _uiContext.GetTheme(_opposingFactionId);

            PlanetSectorPlanetRenderData presentation = _projector
                .CreateRenderData(
                    CreateSector(new GalaxyMapPlanet(_planetSector, planet, string.Empty)),
                    null,
                    PlanetIcon.None,
                    null,
                    PlanetIcon.None
                )
                .Planets[0];

            Assert.AreEqual(
                (Color32)opposingTheme.GetPrimaryColor(),
                presentation.SupportBar.BackgroundColor
            );
        }

        [Test]
        public void CreateRenderData_UnselectedPlanet_ReturnsNoInteractionState()
        {
            Planet planet = CreatePlanet("planet", _playerFactionId, 10, 20);
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSector, planet, string.Empty)
            );

            PlanetSectorWindowRenderData data = _projector.CreateRenderData(
                sector,
                "other",
                PlanetIcon.Facility,
                "other",
                PlanetIcon.Defense
            );

            Assert.AreEqual(PlanetIcon.None, data.Planets[0].SelectedIcon);
            Assert.AreEqual(PlanetIcon.None, data.Planets[0].HoveredIcon);
        }

        [Test]
        public void CreateRenderData_StationedOfficer_ReturnsDefenseOverlay()
        {
            Planet planet = CreatePlanet("planet", _playerFactionId, 10, 20);
            planet.AddTestChild(
                new Officer { OwnerInstanceID = _playerFactionId, Movement = null }
            );
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSector, planet, string.Empty)
            );

            PlanetSectorWindowRenderData data = _projector.CreateRenderData(
                sector,
                null,
                PlanetIcon.None,
                null,
                PlanetIcon.None
            );

            Assert.IsNotNull(data.Planets[0].DefenseTexture);
            Assert.IsNotNull(data.Planets[0].DefensePressedTexture);
        }

        [Test]
        public void CreateRenderData_UprisingPlanet_ReturnsUprisingAndMissionOverlays()
        {
            Planet planet = CreatePlanet("planet", _opposingFactionId, 10, 20);
            planet.IsInUprising = true;
            planet.AddTestChild(new TestMission(_opposingFactionId));
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSector, planet, string.Empty)
            );
            FactionTheme playerTheme = _uiContext.GetPlayerFactionTheme();
            FactionTheme opposingTheme = _uiContext.GetTheme(_opposingFactionId);

            PlanetSectorWindowRenderData data = _projector.CreateRenderData(
                sector,
                null,
                PlanetIcon.None,
                null,
                PlanetIcon.None
            );

            Assert.AreSame(
                _uiContext.GetTexture(playerTheme.PlanetOverlayTheme.PlanetSectorUprisingImagePath),
                data.Planets[0].UprisingTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    opposingTheme.PlanetOverlayTheme.PlanetOverlayIcons.Missions.NormalImagePath
                ),
                data.Planets[0].MissionTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    opposingTheme.PlanetOverlayTheme.PlanetOverlayIcons.Missions.HoverImagePath
                ),
                data.Planets[0].MissionPressedTexture
            );
        }

        [Test]
        public void CreateRenderData_NeutralPlanet_ReturnsNeutralFacilityAndDefenseTextures()
        {
            Planet planet = CreatePlanet("planet", null, 10, 20);
            planet.AddTestChild(CreateBuilding(BuildingType.Mine));
            planet.AddTestChild(CreateBuilding(BuildingType.Defense));
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSector, planet, string.Empty)
            );
            FactionTheme neutralTheme = _uiContext.GetTheme(null);

            PlanetSectorWindowRenderData data = _projector.CreateRenderData(
                sector,
                null,
                PlanetIcon.None,
                null,
                PlanetIcon.None
            );

            PlanetSectorPlanetRenderData presentation = data.Planets[0];
            Assert.AreSame(
                _uiContext.GetTexture(
                    neutralTheme.PlanetOverlayTheme.PlanetOverlayIcons.Buildings.NormalImagePath
                ),
                presentation.FacilityTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    neutralTheme.PlanetOverlayTheme.PlanetOverlayIcons.Buildings.HoverImagePath
                ),
                presentation.FacilityPressedTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    neutralTheme.PlanetOverlayTheme.PlanetOverlayIcons.Defenses.NormalImagePath
                ),
                presentation.DefenseTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(
                    neutralTheme.PlanetOverlayTheme.PlanetOverlayIcons.Defenses.HoverImagePath
                ),
                presentation.DefensePressedTexture
            );
            Assert.IsNull(presentation.FleetTexture);
            Assert.IsNull(presentation.FleetPressedTexture);
            Assert.IsNull(presentation.MissionTexture);
            Assert.IsNull(presentation.MissionPressedTexture);
        }

        [Test]
        public void CreateRenderData_UnexploredPlanet_ReturnsHiddenDetails()
        {
            Planet planet = CreatePlanet("planet", _playerFactionId, 10, 20);
            planet.IsUnexploredView = true;
            planet.IsInUprising = true;
            planet.IsHeadquarters = true;
            planet.EnergyCapacity = 3;
            planet.NumRawResourceNodes = 4;
            planet.PopularSupport[_playerFactionId] = 100;
            planet.AddTestChild(CreateBuilding(BuildingType.Mine));
            planet.AddTestChild(new Regiment());
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSector, planet, string.Empty)
            );

            PlanetSectorWindowRenderData data = _projector.CreateRenderData(
                sector,
                null,
                PlanetIcon.None,
                null,
                PlanetIcon.None
            );

            PlanetSectorPlanetRenderData presentation = data.Planets[0];
            Assert.IsNull(presentation.FacilityTexture);
            Assert.IsNull(presentation.FacilityPressedTexture);
            Assert.IsNull(presentation.DefenseTexture);
            Assert.IsNull(presentation.DefensePressedTexture);
            Assert.IsNull(presentation.HeadquartersTexture);
            Assert.IsNull(presentation.UprisingTexture);
            Assert.IsFalse(presentation.EnergyBar.Visible);
            Assert.IsFalse(presentation.RawResourceBar.Visible);
            Assert.IsFalse(presentation.SupportBar.Visible);
        }

        [Test]
        public void CreateRenderData_EmptyCapacities_ReturnsContinuousEmptyBars()
        {
            Planet planet = CreatePlanet("planet", _playerFactionId, 10, 20);
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSector, planet, string.Empty)
            );

            PlanetSectorWindowRenderData data = _projector.CreateRenderData(
                sector,
                null,
                PlanetIcon.None,
                null,
                PlanetIcon.None
            );

            PlanetSectorPlanetRenderData presentation = data.Planets[0];
            Assert.IsTrue(presentation.EnergyBar.Visible);
            Assert.AreEqual(0, presentation.EnergyBar.CellCount);
            Assert.AreEqual(1f, presentation.EnergyBar.FillRatio);
            Assert.AreEqual(new Color32(0, 0, 255, 255), presentation.EnergyBar.FillColor);
            Assert.IsTrue(presentation.RawResourceBar.Visible);
            Assert.AreEqual(0, presentation.RawResourceBar.CellCount);
            Assert.AreEqual(1f, presentation.RawResourceBar.FillRatio);
            Assert.AreEqual(new Color32(236, 106, 46, 255), presentation.RawResourceBar.FillColor);
            Assert.IsFalse(presentation.SupportBar.Visible);
        }

        [Test]
        public void CreateRenderData_DestroyedPlanet_ReturnsDestroyedPlanetTexture()
        {
            Planet planet = CreatePlanet("planet", _playerFactionId, 10, 20);
            planet.IsDestroyed = true;
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(
                    _planetSector,
                    planet,
                    _uiContext.GetPlayerFactionTheme().GalaxyBackground.ImagePath
                )
            );

            PlanetSectorWindowRenderData data = _projector.CreateRenderData(
                sector,
                null,
                PlanetIcon.None,
                null,
                PlanetIcon.None
            );

            Assert.AreSame(
                _uiContext.GetTexture(
                    _uiContext.GetPlayerFactionTheme().GalaxyBackground.DestroyedPlanetIconPath
                ),
                data.Planets[0].PlanetTexture
            );
        }

        [Test]
        public void CreateRenderData_NullPlanet_ReturnsSectorRelativePlaceholder()
        {
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSector, null, string.Empty)
            );

            PlanetSectorWindowRenderData data = _projector.CreateRenderData(
                sector,
                null,
                PlanetIcon.None,
                null,
                PlanetIcon.None
            );

            Assert.AreEqual(1, data.Planets.Count);
            Assert.AreEqual(string.Empty, data.Planets[0].Name);
            Assert.AreEqual(new Vector2Int(-10, -20), data.Planets[0].GalaxyOffset);
            Assert.IsNull(data.Planets[0].PlanetTexture);
            Assert.IsNull(data.Planets[0].UprisingTexture);
            Assert.IsNull(data.Planets[0].FacilityTexture);
            Assert.IsNull(data.Planets[0].DefenseTexture);
            Assert.IsNull(data.Planets[0].FleetTexture);
            Assert.IsNull(data.Planets[0].MissionTexture);
        }

        private static Building CreateBuilding(BuildingType type)
        {
            return new Building
            {
                BuildingType = type,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private static Planet CreatePlanet(
            string instanceId,
            string ownerInstanceId,
            int positionX,
            int positionY
        )
        {
            return new Planet
            {
                InstanceID = instanceId,
                DisplayName = "Corellia",
                OwnerInstanceID = ownerInstanceId,
                PositionX = positionX,
                PositionY = positionY,
            };
        }

        private GalaxyMapSector CreateSector(GalaxyMapPlanet planet)
        {
            return new GalaxyMapSector(_planetSector, new[] { planet });
        }

        private sealed class TestMission : Mission
        {
            /// <summary>Creates an empty test mission copy.</summary>
            /// <returns>An empty test mission.</returns>
            protected override Rebellion.SceneGraph.BaseSceneNode CreateNodeCopy() =>
                new TestMission(null);

            public TestMission(string ownerInstanceId)
            {
                OwnerInstanceID = ownerInstanceId;
            }

            public override bool ShouldRepeatAfterCompletion(GameRoot game)
            {
                return false;
            }
        }
    }
}
