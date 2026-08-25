using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using UnityEngine;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalaxyMapProjectorTests
    {
        private const string _opposingFactionId = "FNEMP1";
        private const string _playerFactionId = "FNALL1";

        private GameRoot _game;
        private GalaxyMapProjector _projector;
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
            _projector = new GalaxyMapProjector(() => _uiContext);
        }

        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GalaxyMapProjector(null));
        }

        [Test]
        public void ProjectWaypointRoutes_PlayerAndOpposingRoutes_ReturnsOnlyPlayerRoute()
        {
            GalaxyPlanetSector sector = CreateSector("sector", "Sector", 0, 0);
            Planet origin = CreatePlanet("origin", _playerFactionId, 10, 20);
            Planet destination = CreatePlanet("destination", _playerFactionId, 40, 50);
            _game.AttachNode(sector, _game.GetGalaxyMap());
            _game.AttachNode(origin, sector);
            _game.AttachNode(destination, sector);
            GameFleet playerFleet = new GameFleet(_playerFactionId, "Player Fleet")
            {
                Movement = new MovementState { OriginPosition = new System.Drawing.Point(12, 14) },
            };
            playerFleet.Waypoints.Add(destination.InstanceID);
            _game.AttachNode(playerFleet, origin);
            GameFleet opposingFleet = new GameFleet(_opposingFactionId, "Opposing Fleet");
            opposingFleet.Waypoints.Add(destination.InstanceID);
            _game.AttachNode(opposingFleet, origin);

            List<GalaxyMapWaypointRouteRenderData> routes =
                GalaxyMapProjector.ProjectWaypointRoutes(
                    _game,
                    _playerFactionId,
                    selectedFleetInstanceIds: new[] { playerFleet.InstanceID }
                );

            Assert.AreEqual(1, routes.Count);
            Assert.AreEqual(playerFleet.InstanceID, routes[0].FleetInstanceId);
            Assert.AreEqual(new Vector2Int(20, 22), routes[0].Origin);
            Assert.AreEqual(1, routes[0].Waypoints.Count);
            Assert.AreEqual(1, routes[0].Waypoints[0].Order);
            Assert.AreEqual(new Vector2Int(48, 58), routes[0].Waypoints[0].Position);
        }

        [Test]
        public void ProjectWaypointRoutes_UnselectedRoute_ReturnsRouteOnlyWhenAllRoutesEnabled()
        {
            GalaxyPlanetSector sector = CreateSector("sector", "Sector", 0, 0);
            Planet origin = CreatePlanet("origin", _playerFactionId, 10, 20);
            Planet destination = CreatePlanet("destination", _playerFactionId, 40, 50);
            _game.AttachNode(sector, _game.GetGalaxyMap());
            _game.AttachNode(origin, sector);
            _game.AttachNode(destination, sector);
            GameFleet fleet = new GameFleet(_playerFactionId, "Player Fleet");
            fleet.Waypoints.Add(destination.InstanceID);
            _game.AttachNode(fleet, origin);

            List<GalaxyMapWaypointRouteRenderData> defaultRoutes =
                GalaxyMapProjector.ProjectWaypointRoutes(_game, _playerFactionId);
            List<GalaxyMapWaypointRouteRenderData> allRoutes =
                GalaxyMapProjector.ProjectWaypointRoutes(
                    _game,
                    _playerFactionId,
                    showAllRoutes: true
                );

            Assert.IsEmpty(defaultRoutes);
            Assert.AreEqual(1, allRoutes.Count);
            Assert.AreEqual(fleet.InstanceID, allRoutes[0].FleetInstanceId);
        }

        [Test]
        public void ProjectWaypointRoutes_UncommittedPlan_ReturnsPreviewWithoutMutatingFleet()
        {
            GalaxyPlanetSector sector = CreateSector("sector", "Sector", 0, 0);
            Planet origin = CreatePlanet("origin", _playerFactionId, 10, 20);
            Planet destination = CreatePlanet("destination", _playerFactionId, 40, 50);
            _game.AttachNode(sector, _game.GetGalaxyMap());
            _game.AttachNode(origin, sector);
            _game.AttachNode(destination, sector);
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

            List<GalaxyMapWaypointRouteRenderData> routes =
                GalaxyMapProjector.ProjectWaypointRoutes(_game, _playerFactionId, plan);

            Assert.AreEqual(1, routes.Count);
            Assert.AreEqual(new Vector2Int(18, 28), routes[0].Origin);
            Assert.AreEqual(new Vector2Int(48, 58), routes[0].Waypoints[0].Position);
            Assert.IsEmpty(fleet.Waypoints);
            Assert.IsNull(fleet.Movement);
        }

        [Test]
        public void Project_UnavailableContext_ThrowsInvalidOperationException()
        {
            GalaxyMapProjector projector = new GalaxyMapProjector(() => null);

            Assert.Throws<InvalidOperationException>(() =>
                projector.Project(
                    null,
                    _playerFactionId,
                    GalacticInformationFilterMode.DisplayOff,
                    null
                )
            );
        }

        [Test]
        public void Project_NoSectors_ReturnsConfiguredBackgroundWithoutClusters()
        {
            FactionTheme theme = _uiContext.GetPlayerFactionTheme();

            GalaxyMapRenderData data = _projector.Project(
                null,
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff,
                null
            );

            Assert.AreSame(
                _uiContext.GetTexture(theme.GalaxyBackground.ImagePath),
                data.BackgroundTexture
            );
            Assert.AreEqual(theme.GalaxyBackground.SourcePosition.X, data.BackgroundBounds.Value.x);
            Assert.AreEqual(theme.GalaxyBackground.SourcePosition.Y, data.BackgroundBounds.Value.y);
            Assert.AreEqual(
                UILayout.ToSourceUnits(data.BackgroundTexture.width),
                data.BackgroundBounds.Value.width
            );
            Assert.AreEqual(
                UILayout.ToSourceUnits(data.BackgroundTexture.height),
                data.BackgroundBounds.Value.height
            );
            Assert.IsFalse(data.ActiveFilterLabel.Visible);
            Assert.IsEmpty(data.Clusters);
        }

        [Test]
        public void Project_DisplayOff_ReturnsFactionMarkerOffsetsAndHeadquartersOverlay()
        {
            GalaxyPlanetSector planetSector = CreateSector("sector", "Corellia", 40, 50);
            Planet planet = CreatePlanet("planet", _opposingFactionId, 47, 61);
            planet.IsHeadquarters = true;
            GalaxyMapSector sector = CreateSector(planetSector, planet);
            FactionTheme opposingTheme = _uiContext.GetTheme(_opposingFactionId);

            GalaxyMapRenderData data = _projector.Project(
                new[] { sector },
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff,
                planetSector.InstanceID
            );

            Assert.AreEqual(1, data.Clusters.Count);
            GalaxyMapClusterRenderData cluster = data.Clusters[0];
            Assert.AreEqual("sector", cluster.SectorInstanceId);
            Assert.AreEqual(40, cluster.SourceX);
            Assert.AreEqual(50, cluster.SourceY);
            Assert.AreEqual("Corellia", cluster.Label);
            Assert.IsTrue(cluster.ShowLabel);
            Assert.AreEqual(1, cluster.Stars.Count);
            GalaxyMapStarRenderData star = cluster.Stars[0];
            Assert.AreEqual("planet", star.PlanetInstanceId);
            Assert.AreEqual(7, star.SourceX);
            Assert.AreEqual(11, star.SourceY);
            Assert.AreSame(
                _uiContext.GetTexture(opposingTheme.GalaxyBackground.PlanetIcons.Small),
                star.StarTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(opposingTheme.PlanetOverlayTheme.GalaxyHeadquartersImagePath),
                star.HeadquartersTexture
            );
        }

        [Test]
        public void Project_CorellianPlanets_ReturnsCorrectedSeloniaAndDurosOffsets()
        {
            GalaxyPlanetSector planetSector = TestContent.Data.PlanetSectors.Single(candidate =>
                candidate.TypeID == "PSCOR"
            );
            Planet selonia = planetSector
                .GetChildren<Planet>()
                .Single(planet => planet.InstanceID == "SELONIA");
            Planet duros = planetSector
                .GetChildren<Planet>()
                .Single(planet => planet.InstanceID == "DUROS");
            GalaxyMapSector sector = new GalaxyMapSector(
                planetSector,
                new[]
                {
                    new GalaxyMapPlanet(planetSector, selonia, string.Empty),
                    new GalaxyMapPlanet(planetSector, duros, string.Empty),
                }
            );

            GalaxyMapRenderData data = _projector.Project(
                new[] { sector },
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff,
                null
            );

            GalaxyMapStarRenderData seloniaStar = data.Clusters[0]
                .Stars.Single(star => star.PlanetInstanceId == "SELONIA");
            GalaxyMapStarRenderData durosStar = data.Clusters[0]
                .Stars.Single(star => star.PlanetInstanceId == "DUROS");
            Assert.AreEqual(224, selonia.PositionX);
            Assert.AreEqual(237, duros.PositionX);
            Assert.AreEqual(18, seloniaStar.SourceX);
            Assert.AreEqual(31, durosStar.SourceX);
        }

        [Test]
        public void Project_UnexploredHeadquarters_ReturnsUnknownMarkerWithoutOverlay()
        {
            GalaxyPlanetSector planetSector = CreateSector("sector", "Corellia", 0, 0);
            Planet planet = CreatePlanet("planet", _opposingFactionId, 1, 2);
            planet.IsHeadquarters = true;
            planet.IsUnexploredView = true;
            GalaxyMapSector sector = CreateSector(planetSector, planet);
            FactionTheme playerTheme = _uiContext.GetPlayerFactionTheme();

            GalaxyMapRenderData data = _projector.Project(
                new[] { sector },
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff,
                null
            );

            GalaxyMapStarRenderData star = data.Clusters[0].Stars[0];
            Assert.AreSame(
                _uiContext.GetTexture(playerTheme.GalaxyBackground.UnexploredPlanetIconPath),
                star.StarTexture
            );
            Assert.IsNull(star.HeadquartersTexture);
            Assert.IsFalse(data.Clusters[0].ShowLabel);
        }

        [Test]
        public void Project_HighestFilterValue_ReturnsExtraLargeMarkerAndActiveLabel()
        {
            GalaxyPlanetSector planetSector = CreateSector("sector", "Corellia", 0, 0);
            Planet planet = CreatePlanet("planet", _playerFactionId, 1, 2);
            planet.IsInUprising = true;
            GalaxyMapSector sector = CreateSector(planetSector, planet);
            FactionTheme playerTheme = _uiContext.GetPlayerFactionTheme();
            GalacticInformationFilterTheme filter =
                playerTheme.GalacticInformationDisplay.GetFilter(
                    GalacticInformationFilterMode.Uprisings
                );

            GalaxyMapRenderData data = _projector.Project(
                new[] { sector },
                _playerFactionId,
                GalacticInformationFilterMode.Uprisings,
                null
            );

            Assert.AreSame(
                _uiContext.GetTexture(playerTheme.GalaxyBackground.PlanetIcons.XL),
                data.Clusters[0].Stars[0].StarTexture
            );
            Assert.AreEqual(filter.Label, data.ActiveFilterLabel.Text);
            Assert.AreEqual(
                playerTheme.GalacticInformationDisplay.GetActiveFilterLabelColor(),
                data.ActiveFilterLabel.Color
            );
            Assert.AreEqual(
                new RectInt(
                    playerTheme.GalacticInformationDisplay.ActiveFilterLabelSourceLayout.X,
                    playerTheme.GalacticInformationDisplay.ActiveFilterLabelSourceLayout.Y,
                    playerTheme.GalacticInformationDisplay.ActiveFilterLabelSourceLayout.Width,
                    playerTheme.GalacticInformationDisplay.ActiveFilterLabelSourceLayout.Height
                ),
                data.ActiveFilterLabel.Bounds
            );
            Assert.AreEqual(
                playerTheme.GalacticInformationDisplay.ActiveFilterLabelFontSize,
                data.ActiveFilterLabel.FontSize
            );
        }

        [Test]
        public void Project_OpponentLoyaltyBriefing_HighlightsOnlyOpponentAndUsesCueLabel()
        {
            GalaxyPlanetSector planetSector = CreateSector("sector", "Corellia", 0, 0);
            Planet playerPlanet = CreatePlanet("player", _playerFactionId, 1, 2);
            Planet opposingPlanet = CreatePlanet("opposing", _opposingFactionId, 3, 4);
            playerPlanet.SetFullPopularSupport(_playerFactionId);
            opposingPlanet.SetFullPopularSupport(_opposingFactionId);
            GalaxyMapSector sector = CreateSector(planetSector, playerPlanet, opposingPlanet);
            FactionTheme playerTheme = _uiContext.GetPlayerFactionTheme();
            FactionTheme opposingTheme = _uiContext.GetTheme(_opposingFactionId);
            StrategyBriefingMapPresentation briefing = new StrategyBriefingMapPresentation(
                StrategyBriefingMapMode.OpponentLoyalty,
                "Systems Loyal to the Empire",
                null,
                null,
                _playerFactionId,
                _opposingFactionId
            );

            GalaxyMapRenderData data = _projector.Project(
                new[] { sector },
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff,
                null,
                briefing
            );

            Assert.AreEqual("Systems Loyal to the Empire", data.ActiveFilterLabel.Text);
            Assert.AreSame(
                _uiContext.GetTexture(playerTheme.GalaxyBackground.PlanetIcons.Small),
                data.Clusters[0].Stars[0].StarTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(opposingTheme.GalaxyBackground.PlanetIcons.XL),
                data.Clusters[0].Stars[1].StarTexture
            );
        }

        [Test]
        public void Project_UnexploredSystemsBriefing_UsesBlueHighlightForUnexploredPlanets()
        {
            GalaxyPlanetSector planetSector = CreateSector("sector", "Outer Rim", 0, 0);
            Planet exploredPlanet = CreatePlanet("explored", _playerFactionId, 1, 2);
            Planet unexploredPlanet = CreatePlanet("unexplored", null, 3, 4);
            unexploredPlanet.IsUnexploredView = true;
            GalaxyMapSector sector = CreateSector(planetSector, exploredPlanet, unexploredPlanet);
            FactionTheme playerTheme = _uiContext.GetPlayerFactionTheme();
            FactionTheme defaultTheme = _uiContext.GetTheme(null);
            StrategyBriefingMapPresentation briefing = new StrategyBriefingMapPresentation(
                StrategyBriefingMapMode.UnexploredSystems,
                "Unexplored Systems",
                null,
                null,
                _playerFactionId,
                _opposingFactionId
            );

            GalaxyMapRenderData data = _projector.Project(
                new[] { sector },
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff,
                null,
                briefing
            );

            Assert.AreSame(
                _uiContext.GetTexture(playerTheme.GalaxyBackground.PlanetIcons.Small),
                data.Clusters[0].Stars[0].StarTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(defaultTheme.GalaxyBackground.PlanetIcons.XL),
                data.Clusters[0].Stars[1].StarTexture
            );
        }

        [Test]
        public void Project_DimmedBriefing_DimsOnlyGalaxyBackground()
        {
            GalaxyPlanetSector planetSector = CreateSector("sector", "Corellia", 0, 0);
            Planet planet = CreatePlanet("planet", _playerFactionId, 1, 2);
            GalaxyMapSector sector = CreateSector(planetSector, planet);
            StrategyBriefingMapPresentation briefing = new StrategyBriefingMapPresentation(
                StrategyBriefingMapMode.Default,
                null,
                null,
                null,
                _playerFactionId,
                _opposingFactionId,
                true
            );

            GalaxyMapRenderData data = _projector.Project(
                new[] { sector },
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff,
                null,
                briefing
            );

            Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f, 1f), data.BackgroundColor);
            Assert.AreEqual("Briefing", data.ActiveFilterLabel.Text);
            Assert.IsNotNull(data.Clusters[0].Stars[0].StarTexture);
        }

        [Test]
        public void Project_TargetBriefing_RevealsTargetSystemAndOverridesLabel()
        {
            GalaxyPlanetSector planetSector = CreateSector("sector", "Corellia", 0, 0);
            Planet targetPlanet = CreatePlanet("planet", _playerFactionId, 1, 2);
            GalaxyMapSector sector = CreateSector(planetSector, targetPlanet);
            StrategyBriefingMapPresentation briefing = new StrategyBriefingMapPresentation(
                StrategyBriefingMapMode.Spotlight,
                "Mon Mothma",
                planetSector.InstanceID,
                targetPlanet.InstanceID,
                _playerFactionId,
                _opposingFactionId
            );

            GalaxyMapRenderData data = _projector.Project(
                new[] { sector },
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff,
                null,
                briefing
            );

            Assert.AreEqual("Mon Mothma", data.ActiveFilterLabel.Text);
            Assert.IsTrue(data.Clusters[0].ShowLabel);
            Assert.AreSame(
                _uiContext.GetTexture(
                    _uiContext.GetPlayerFactionTheme().GalaxyBackground.PlanetIcons.XL
                ),
                data.Clusters[0].Stars[0].StarTexture
            );
        }

        [Test]
        public void Project_UnsupportedBriefingMode_ThrowsArgumentOutOfRangeException()
        {
            GalaxyPlanetSector planetSector = CreateSector("sector", "Corellia", 0, 0);
            Planet planet = CreatePlanet("planet", _playerFactionId, 1, 2);
            GalaxyMapSector sector = CreateSector(planetSector, planet);
            StrategyBriefingMapPresentation briefing = new StrategyBriefingMapPresentation(
                (StrategyBriefingMapMode)int.MaxValue,
                "Invalid",
                null,
                null,
                _playerFactionId,
                _opposingFactionId
            );

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _projector.Project(
                    new[] { sector },
                    _playerFactionId,
                    GalacticInformationFilterMode.DisplayOff,
                    null,
                    briefing
                )
            );
        }

        [Test]
        public void Project_MixedFactionFleets_ReturnsMixedMarker()
        {
            GalaxyPlanetSector planetSector = CreateSector("sector", "Corellia", 0, 0);
            Planet planet = CreatePlanet("planet", _playerFactionId, 1, 2);
            planet.AddChild(new GameFleet(_playerFactionId, "Player Fleet"));
            planet.AddChild(new GameFleet(_opposingFactionId, "Opposing Fleet"));
            GalaxyMapSector sector = CreateSector(planetSector, planet);
            FactionTheme playerTheme = _uiContext.GetPlayerFactionTheme();

            GalaxyMapRenderData data = _projector.Project(
                new[] { sector },
                _playerFactionId,
                GalacticInformationFilterMode.IdleFleets,
                null
            );

            Assert.AreSame(
                _uiContext.GetTexture(playerTheme.GalaxyBackground.PlanetIcons.Mixed),
                data.Clusters[0].Stars[0].StarTexture
            );
        }

        [Test]
        public void Project_NullEntries_SkipsInvalidSectorsAndPlanets()
        {
            GalaxyPlanetSector planetSector = CreateSector("sector", "Corellia", 0, 0);
            GalaxyMapSector sector = new GalaxyMapSector(
                planetSector,
                new GalaxyMapPlanet[]
                {
                    null,
                    new GalaxyMapPlanet(planetSector, null, string.Empty),
                    new GalaxyMapPlanet(
                        planetSector,
                        CreatePlanet("planet", _playerFactionId, 1, 2),
                        string.Empty
                    ),
                }
            );
            GalaxyMapSector[] sectors = { null, new GalaxyMapSector(null, null), sector };

            GalaxyMapRenderData data = _projector.Project(
                sectors,
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff,
                null
            );

            Assert.AreEqual(1, data.Clusters.Count);
            Assert.AreEqual(1, data.Clusters[0].Stars.Count);
            Assert.AreEqual("planet", data.Clusters[0].Stars[0].PlanetInstanceId);
        }

        [Test]
        public void GetSectorSourcePosition_NullSector_ReturnsZero()
        {
            Vector2Int position = _projector.GetSectorSourcePosition(null);

            Assert.AreEqual(Vector2Int.zero, position);
        }

        [Test]
        public void GetSectorSourcePosition_Sector_ReturnsBackgroundAdjustedPosition()
        {
            GalaxyPlanetSector sector = new GalaxyPlanetSector { PositionX = 12, PositionY = 34 };
            SourcePointLayout backgroundPosition = _uiContext
                .GetPlayerFactionTheme()
                .GalaxyBackground.SourcePosition;

            Vector2Int position = _projector.GetSectorSourcePosition(sector);

            Assert.AreEqual(backgroundPosition.X + 12, position.x);
            Assert.AreEqual(backgroundPosition.Y + 34, position.y);
        }

        [Test]
        public void GetSectorSourcePosition_UnavailableContext_ThrowsInvalidOperationException()
        {
            GalaxyMapProjector projector = new GalaxyMapProjector(() => null);

            Assert.Throws<InvalidOperationException>(() =>
                projector.GetSectorSourcePosition(new GalaxyPlanetSector())
            );
        }

        [TestCase(0, "small")]
        [TestCase(1, "medium")]
        [TestCase(2, "large")]
        [TestCase(3, "xl")]
        [TestCase(8, "xl")]
        public void GetPlanetIconPath_ConfiguredMarker_ReturnsRequestedSize(
            int markerIndex,
            string expected
        )
        {
            PlanetIcons icons = new PlanetIcons
            {
                Small = "small",
                Medium = "medium",
                Large = "large",
                XL = "xl",
            };

            string path = GalaxyMapProjector.GetPlanetIconPath(icons, markerIndex);

            Assert.AreEqual(expected, path);
        }

        [Test]
        public void GetPlanetIconPath_MissingLargerMarkers_ReturnsNearestConfiguredSize()
        {
            PlanetIcons icons = new PlanetIcons { Small = "small" };

            string medium = GalaxyMapProjector.GetPlanetIconPath(icons, 1);
            string large = GalaxyMapProjector.GetPlanetIconPath(icons, 2);
            string extraLarge = GalaxyMapProjector.GetPlanetIconPath(icons, 3);

            Assert.AreEqual("small", medium);
            Assert.AreEqual("small", large);
            Assert.AreEqual("small", extraLarge);
            Assert.IsNull(GalaxyMapProjector.GetPlanetIconPath(null, 0));
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
                OwnerInstanceID = ownerInstanceId,
                PositionX = positionX,
                PositionY = positionY,
            };
        }

        private static GalaxyMapSector CreateSector(
            GalaxyPlanetSector sector,
            params Planet[] planets
        )
        {
            return new GalaxyMapSector(
                sector,
                planets
                    .Select(planet => new GalaxyMapPlanet(sector, planet, string.Empty))
                    .ToArray()
            );
        }

        private static GalaxyPlanetSector CreateSector(
            string instanceId,
            string displayName,
            int positionX,
            int positionY
        )
        {
            return new GalaxyPlanetSector
            {
                InstanceID = instanceId,
                DisplayName = displayName,
                PositionX = positionX,
                PositionY = positionY,
            };
        }
    }
}
