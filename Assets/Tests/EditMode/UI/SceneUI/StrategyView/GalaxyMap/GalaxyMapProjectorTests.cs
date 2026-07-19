using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using UnityEngine;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

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
            _game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Alliance" }
            );
            _game.Factions.Add(
                new Faction { InstanceID = _opposingFactionId, DisplayName = "Empire" }
            );
            _game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = new UIContext(
                _game,
                new FactionThemeLibrary(),
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
            GamePlanetSystem system = CreateSystem("system", "Corellia", 40, 50);
            Planet planet = CreatePlanet("planet", _opposingFactionId, 47, 61);
            planet.IsHeadquarters = true;
            GalaxyMapSector sector = CreateSector(system, planet);
            FactionTheme opposingTheme = _uiContext.GetTheme(_opposingFactionId);

            GalaxyMapRenderData data = _projector.Project(
                new[] { sector },
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff,
                system.InstanceID
            );

            Assert.AreEqual(1, data.Clusters.Count);
            GalaxyMapClusterRenderData cluster = data.Clusters[0];
            Assert.AreEqual("system", cluster.SystemInstanceId);
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
        public void Project_UnexploredHeadquarters_ReturnsUnknownMarkerWithoutOverlay()
        {
            GamePlanetSystem system = CreateSystem("system", "Corellia", 0, 0);
            Planet planet = CreatePlanet("planet", _opposingFactionId, 1, 2);
            planet.IsHeadquarters = true;
            planet.IsUnexploredView = true;
            GalaxyMapSector sector = CreateSector(system, planet);
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
            GamePlanetSystem system = CreateSystem("system", "Corellia", 0, 0);
            Planet planet = CreatePlanet("planet", _playerFactionId, 1, 2);
            planet.IsInUprising = true;
            GalaxyMapSector sector = CreateSector(system, planet);
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
        public void Project_MixedFactionFleets_ReturnsMixedMarker()
        {
            GamePlanetSystem system = CreateSystem("system", "Corellia", 0, 0);
            Planet planet = CreatePlanet("planet", _playerFactionId, 1, 2);
            planet.Fleets.Add(new GameFleet(_playerFactionId, "Player Fleet"));
            planet.Fleets.Add(new GameFleet(_opposingFactionId, "Opposing Fleet"));
            GalaxyMapSector sector = CreateSector(system, planet);
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
            GamePlanetSystem system = CreateSystem("system", "Corellia", 0, 0);
            GalaxyMapSector sector = new GalaxyMapSector(
                system,
                new GalaxyMapPlanet[]
                {
                    null,
                    new GalaxyMapPlanet(system, null, string.Empty),
                    new GalaxyMapPlanet(
                        system,
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
        public void GetSystemSourcePosition_NullSystem_ReturnsZero()
        {
            Vector2Int position = _projector.GetSystemSourcePosition(null);

            Assert.AreEqual(Vector2Int.zero, position);
        }

        [Test]
        public void GetSystemSourcePosition_System_ReturnsBackgroundAdjustedPosition()
        {
            GamePlanetSystem system = new GamePlanetSystem { PositionX = 12, PositionY = 34 };
            SourcePointLayout backgroundPosition = _uiContext
                .GetPlayerFactionTheme()
                .GalaxyBackground.SourcePosition;

            Vector2Int position = _projector.GetSystemSourcePosition(system);

            Assert.AreEqual(backgroundPosition.X + 12, position.x);
            Assert.AreEqual(backgroundPosition.Y + 34, position.y);
        }

        [Test]
        public void GetSystemSourcePosition_UnavailableContext_ThrowsInvalidOperationException()
        {
            GalaxyMapProjector projector = new GalaxyMapProjector(() => null);

            Assert.Throws<InvalidOperationException>(() =>
                projector.GetSystemSourcePosition(new GamePlanetSystem())
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

        private static GalaxyMapSector CreateSector(GamePlanetSystem system, Planet planet)
        {
            return new GalaxyMapSector(
                system,
                new[] { new GalaxyMapPlanet(system, planet, string.Empty) }
            );
        }

        private static GamePlanetSystem CreateSystem(
            string instanceId,
            string displayName,
            int positionX,
            int positionY
        )
        {
            return new GamePlanetSystem
            {
                InstanceID = instanceId,
                DisplayName = displayName,
                PositionX = positionX,
                PositionY = positionY,
            };
        }
    }
}
