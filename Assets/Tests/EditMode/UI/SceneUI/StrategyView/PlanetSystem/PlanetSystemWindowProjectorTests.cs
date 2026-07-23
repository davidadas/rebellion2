using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using UnityEngine;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.PlanetSystem
{
    [TestFixture]
    public class PlanetSystemWindowProjectorTests
    {
        private const string _opposingFactionId = "FNEMP1";
        private const string _playerFactionId = "FNALL1";

        private GamePlanetSystem _planetSystem;
        private PlanetSystemWindowProjector _projector;
        private UIContext _uiContext;

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
            _planetSystem = new GamePlanetSystem
            {
                InstanceID = "system",
                DisplayName = "Corellia System",
                PositionX = 10,
                PositionY = 20,
            };
            _projector = new PlanetSystemWindowProjector(() => _uiContext);
        }

        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new PlanetSystemWindowProjector(null));
        }

        [Test]
        public void CreateRenderData_UnavailableContext_ThrowsInvalidOperationException()
        {
            PlanetSystemWindowProjector projector = new PlanetSystemWindowProjector(() => null);

            Assert.Throws<InvalidOperationException>(() =>
                projector.CreateRenderData(null, null, PlanetIcon.None, null, PlanetIcon.None)
            );
        }

        [Test]
        public void CreateRenderData_NullSector_ReturnsEmptyPresentation()
        {
            PlanetSystemWindowRenderData data = _projector.CreateRenderData(
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
            planet.Buildings.Add(CreateBuilding(BuildingType.Mine));
            planet.Buildings.Add(CreateBuilding(BuildingType.Defense));
            planet.Fleets.Add(new GameFleet(_playerFactionId, "Player Fleet"));
            planet.Fleets.Add(new GameFleet(_opposingFactionId, "Opposing Fleet"));
            planet.Missions.Add(new TestMission(_playerFactionId));
            planet.Missions.Add(new TestMission(_opposingFactionId));
            string planetTexturePath = _uiContext
                .GetPlayerFactionTheme()
                .GalaxyBackground.ImagePath;
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSystem, planet, planetTexturePath)
            );
            FactionTheme playerTheme = _uiContext.GetTheme(_playerFactionId);
            FactionTheme opposingTheme = _uiContext.GetTheme(_opposingFactionId);

            PlanetSystemWindowRenderData data = _projector.CreateRenderData(
                sector,
                planet.InstanceID,
                PlanetIcon.Fleet,
                planet.InstanceID,
                PlanetIcon.Mission
            );

            Assert.AreEqual("Corellia System", data.Title);
            Assert.AreEqual(1, data.Planets.Count);
            PlanetSystemPlanetRenderData presentation = data.Planets[0];
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
                    playerTheme.PlanetOverlayTheme.PlanetSystemHeadquartersImagePath
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
        public void CreateRenderData_UnselectedPlanet_ReturnsNoInteractionState()
        {
            Planet planet = CreatePlanet("planet", _playerFactionId, 10, 20);
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSystem, planet, string.Empty)
            );

            PlanetSystemWindowRenderData data = _projector.CreateRenderData(
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
        public void CreateRenderData_UprisingPlanet_ReturnsUprisingAndMissionOverlays()
        {
            Planet planet = CreatePlanet("planet", _opposingFactionId, 10, 20);
            planet.IsInUprising = true;
            planet.Missions.Add(new TestMission(_opposingFactionId));
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSystem, planet, string.Empty)
            );
            FactionTheme playerTheme = _uiContext.GetPlayerFactionTheme();
            FactionTheme opposingTheme = _uiContext.GetTheme(_opposingFactionId);

            PlanetSystemWindowRenderData data = _projector.CreateRenderData(
                sector,
                null,
                PlanetIcon.None,
                null,
                PlanetIcon.None
            );

            Assert.AreSame(
                _uiContext.GetTexture(playerTheme.PlanetOverlayTheme.PlanetSystemUprisingImagePath),
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
            planet.Buildings.Add(CreateBuilding(BuildingType.Mine));
            planet.Buildings.Add(CreateBuilding(BuildingType.Defense));
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSystem, planet, string.Empty)
            );
            FactionTheme neutralTheme = _uiContext.GetTheme(null);

            PlanetSystemWindowRenderData data = _projector.CreateRenderData(
                sector,
                null,
                PlanetIcon.None,
                null,
                PlanetIcon.None
            );

            PlanetSystemPlanetRenderData presentation = data.Planets[0];
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
            planet.Buildings.Add(CreateBuilding(BuildingType.Mine));
            planet.Regiments.Add(new Regiment());
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSystem, planet, string.Empty)
            );

            PlanetSystemWindowRenderData data = _projector.CreateRenderData(
                sector,
                null,
                PlanetIcon.None,
                null,
                PlanetIcon.None
            );

            PlanetSystemPlanetRenderData presentation = data.Planets[0];
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
                new GalaxyMapPlanet(_planetSystem, planet, string.Empty)
            );

            PlanetSystemWindowRenderData data = _projector.CreateRenderData(
                sector,
                null,
                PlanetIcon.None,
                null,
                PlanetIcon.None
            );

            PlanetSystemPlanetRenderData presentation = data.Planets[0];
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
                    _planetSystem,
                    planet,
                    _uiContext.GetPlayerFactionTheme().GalaxyBackground.ImagePath
                )
            );

            PlanetSystemWindowRenderData data = _projector.CreateRenderData(
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
        public void CreateRenderData_NullPlanet_ReturnsSystemRelativePlaceholder()
        {
            GalaxyMapSector sector = CreateSector(
                new GalaxyMapPlanet(_planetSystem, null, string.Empty)
            );

            PlanetSystemWindowRenderData data = _projector.CreateRenderData(
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
            return new GalaxyMapSector(_planetSystem, new[] { planet });
        }

        private sealed class TestMission : Mission
        {
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
