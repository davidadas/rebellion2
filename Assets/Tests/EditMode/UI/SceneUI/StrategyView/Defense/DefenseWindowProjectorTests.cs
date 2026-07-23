using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Defense
{
    [TestFixture]
    public class DefenseWindowProjectorTests
    {
        private const string _ownerId = "FNALL1";

        private GameObject _windowObject;
        private UIWindow _window;
        private Planet _planet;
        private GalaxyMapPlanet _mapPlanet;
        private DefenseWindowSession _session;
        private UIContext _uiContext;
        private DefenseWindowProjector _projector;

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _ownerId });
            game.Summary.PlayerFactionID = _ownerId;
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _windowObject = new GameObject(
                "DefenseWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 18, 29, 100, 100, false, true, false);
            _planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _ownerId,
            };
            _mapPlanet = new GalaxyMapPlanet(new GamePlanetSystem(), _planet, string.Empty);
            _session = new DefenseWindowSession(_mapPlanet, _window);
            _projector = new DefenseWindowProjector(() => _uiContext);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DefenseWindowProjector(null));
        }

        [Test]
        public void Build_NullSession_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _projector.Build(null, _window, true));
        }

        [Test]
        public void Build_NullWindow_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _projector.Build(_session, null, true));
        }

        [Test]
        public void Build_UnavailableContext_ThrowsInvalidOperationException()
        {
            DefenseWindowProjector projector = new DefenseWindowProjector(() => null);

            Assert.Throws<InvalidOperationException>(() =>
                projector.Build(_session, _window, true)
            );
        }

        [Test]
        public void Build_SelectedCapturedOfficer_ReturnsPersonnelCardPresentation()
        {
            Officer definition = ResourceManager
                .GetEntityData<Officer>()
                .First(officer =>
                    officer.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true
                    && !string.IsNullOrEmpty(officer.CapturedOverlayImagePath)
                );
            Officer officer = new Officer
            {
                InstanceID = "officer",
                DisplayName = "Captured Officer",
                OwnerInstanceID = _ownerId,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
                CapturedOverlayImagePath = definition.CapturedOverlayImagePath,
                IsCaptured = true,
            };
            _planet.Officers.Add(officer);
            _session.Reconcile();
            _session.SelectItem(0, 3);

            DefenseWindowRenderData data = _projector.Build(_session, _window, true);

            Assert.AreEqual(18, data.X);
            Assert.AreEqual(29, data.Y);
            Assert.AreEqual("Corellia", data.Caption);
            Assert.AreEqual(DefenseWindowTab.Personnel, data.ActiveTab);
            Assert.AreEqual("Personnel", data.TabTitle);
            Assert.AreEqual(string.Empty, data.GarrisonRequirementText);
            Assert.IsNotNull(data.TitleTexture);
            Assert.AreEqual(DefenseWindowRenderData.TabCount, data.Tabs.Count);
            Assert.IsNotNull(data.Tabs[0].Texture);
            Assert.IsNotNull(data.Tabs[0].PressedTexture);
            Assert.IsNotNull(data.Tabs[1].Texture);
            Assert.IsNull(data.Tabs[1].PressedTexture);
            Assert.AreEqual(1, data.Items.Count);
            StrategyUnitCardRenderData card = data.Items[0];
            Assert.AreEqual("Captured Officer", card.Name);
            Assert.AreEqual(
                (Color32)_uiContext.GetTheme(_ownerId).GetPrimaryColor(),
                card.NameColor
            );
            Assert.IsTrue(card.ShowName);
            Assert.IsFalse(card.UseAlternateNameLayout);
            Assert.IsNotNull(card.BackgroundTexture);
            Assert.IsNotNull(card.EntityTexture);
            Assert.IsNotNull(card.CapturedOverlayTexture);
            Assert.IsNotNull(card.SelectionTexture);
            Assert.IsNull(card.ConstructionOverlayTexture);
            Assert.IsNull(card.EnrouteOverlayTexture);
            Assert.IsNull(card.DamagedOverlayTexture);
            Assert.IsTrue(card.CanDrag);
        }

        [Test]
        public void Build_PlayerOwnedRegimentTab_ReturnsGarrisonRequirement()
        {
            _planet.SetPopularSupport(_ownerId, 35);
            _session.SelectTab(DefenseWindowTab.Regiments);

            DefenseWindowRenderData data = _projector.Build(_session, _window, true);

            Assert.AreEqual("Trooper Regiments", data.TabTitle);
            Assert.AreEqual("Garrison Requirement: 3", data.GarrisonRequirementText);
        }

        [Test]
        public void Build_NonPlayerOwnedRegimentTab_ClearsGarrisonRequirement()
        {
            const string opposingOwnerId = "FNEMP1";
            _uiContext.Game.Factions.Add(new Faction { InstanceID = opposingOwnerId });
            _planet.OwnerInstanceID = opposingOwnerId;
            _session.SelectTab(DefenseWindowTab.Regiments);

            DefenseWindowRenderData data = _projector.Build(_session, _window, true);

            Assert.AreEqual(string.Empty, data.GarrisonRequirementText);
        }

        [Test]
        public void Build_MovingOfficerWithoutTransitArtwork_UsesThemedEnrouteBackground()
        {
            Officer definition = ResourceManager
                .GetEntityData<Officer>()
                .First(officer => officer.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true);
            Officer officer = new Officer
            {
                InstanceID = "officer",
                DisplayName = "Traveling Officer",
                OwnerInstanceID = _ownerId,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
                Movement = new MovementState(),
            };
            _planet.Officers.Add(officer);
            _session.Reconcile();

            DefenseWindowRenderData data = _projector.Build(_session, _window, false);

            StrategyUnitCardRenderData card = data.Items[0];
            Assert.AreSame(
                _uiContext.GetTexture(
                    _uiContext.GetTheme(_ownerId).StrategyWindows.Defense.EnrouteBackgroundImagePath
                ),
                card.BackgroundTexture
            );
            Assert.IsNull(card.EnrouteOverlayTexture);
            Assert.IsFalse(card.CanDrag);
            Assert.IsNotNull(data.TitleTexture);
        }

        [Test]
        public void Build_StarfighterUnderConstruction_ReturnsConstructionOverlayOnly()
        {
            Starfighter starfighter = CreateStarfighter("fighter", "Fighter Squadron");
            starfighter.ManufacturingStatus = ManufacturingStatus.Building;
            starfighter.Movement = new MovementState();
            starfighter.CurrentSquadronSize = 4;
            starfighter.MaxSquadronSize = 12;
            _planet.Starfighters.Add(starfighter);
            _session.Reconcile();
            _session.SelectTab(DefenseWindowTab.Starfighters);

            DefenseWindowRenderData data = _projector.Build(_session, _window, true);

            StrategyUnitCardRenderData card = data.Items[0];
            Assert.IsNotNull(card.ConstructionOverlayTexture);
            Assert.IsNull(card.EnrouteOverlayTexture);
            Assert.IsNull(card.DamagedOverlayTexture);
            Assert.IsNotNull(card.BackgroundTexture);
            Assert.IsFalse(card.CanDrag);
        }

        [Test]
        public void Build_MovingDamagedStarfighter_ReturnsEnrouteAndDamageOverlays()
        {
            Starfighter definition = ResourceManager
                .GetEntityData<Starfighter>()
                .First(fighter =>
                    fighter.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true
                    && !string.IsNullOrEmpty(fighter.InTransitSmallImagePath)
                    && !string.IsNullOrEmpty(fighter.DamagedSmallImagePath)
                );
            Starfighter starfighter = new Starfighter
            {
                InstanceID = "fighter",
                DisplayName = "Damaged Squadron",
                OwnerInstanceID = _ownerId,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
                InTransitSmallImagePath = definition.InTransitSmallImagePath,
                DamagedSmallImagePath = definition.DamagedSmallImagePath,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = new MovementState(),
                CurrentSquadronSize = 6,
                MaxSquadronSize = 12,
            };
            _planet.Starfighters.Add(starfighter);
            _session.Reconcile();
            _session.SelectTab(DefenseWindowTab.Starfighters);

            DefenseWindowRenderData data = _projector.Build(_session, _window, true);

            StrategyUnitCardRenderData card = data.Items[0];
            Assert.IsNull(card.ConstructionOverlayTexture);
            Assert.IsNotNull(card.EnrouteOverlayTexture);
            Assert.IsNotNull(card.DamagedOverlayTexture);
            Assert.IsNotNull(card.BackgroundTexture);
            Assert.IsFalse(card.CanDrag);
        }

        [Test]
        public void Build_ShieldUnderConstruction_ReturnsDefenseBuildingCard()
        {
            Building definition = ResourceManager
                .GetEntityData<Building>()
                .First(building =>
                    building.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true
                    && building.DefenseFacilityClass == DefenseFacilityClass.Shield
                );
            Building shield = new Building
            {
                InstanceID = "shield",
                DisplayName = "Shield Generator",
                OwnerInstanceID = _ownerId,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
                DefenseFacilityClass = DefenseFacilityClass.Shield,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            _planet.Buildings.Add(shield);
            _session.Reconcile();
            _session.SelectTab(DefenseWindowTab.Shields);

            DefenseWindowRenderData data = _projector.Build(_session, _window, true);

            Assert.AreEqual("Planetary Shields", data.TabTitle);
            Assert.AreEqual(1, data.Items.Count);
            Assert.IsNotNull(data.Items[0].EntityTexture);
            Assert.IsNotNull(data.Items[0].ConstructionOverlayTexture);
            Assert.IsFalse(data.Items[0].CanDrag);
        }

        [TestCase(DefenseWindowTab.Personnel, "Personnel")]
        [TestCase(DefenseWindowTab.Regiments, "Trooper Regiments")]
        [TestCase(DefenseWindowTab.Starfighters, "Fighter Squadrons")]
        [TestCase(DefenseWindowTab.Shields, "Planetary Shields")]
        [TestCase(DefenseWindowTab.Batteries, "Planetary Batteries")]
        [TestCase((DefenseWindowTab)99, "")]
        public void GetTabTitle_Tab_ReturnsExpectedTitle(DefenseWindowTab tab, string expected)
        {
            string title = DefenseWindowProjector.GetTabTitle(tab);

            Assert.AreEqual(expected, title);
        }

        private static Starfighter CreateStarfighter(string instanceId, string displayName)
        {
            Starfighter definition = ResourceManager
                .GetEntityData<Starfighter>()
                .First(fighter => fighter.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true);
            return new Starfighter
            {
                InstanceID = instanceId,
                DisplayName = displayName,
                OwnerInstanceID = _ownerId,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
                InTransitSmallImagePath = definition.InTransitSmallImagePath,
                DamagedSmallImagePath = definition.DamagedSmallImagePath,
            };
        }
    }
}
