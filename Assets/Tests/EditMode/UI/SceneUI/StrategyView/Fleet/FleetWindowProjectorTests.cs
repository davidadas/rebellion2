using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Fleet
{
    [TestFixture]
    public class FleetWindowProjectorTests
    {
        private const string _ownerId = "FNALL1";

        private CapitalShip _capitalShip;
        private GameFleet _fleet;
        private Officer _officer;
        private Planet _planet;
        private FleetWindowProjector _projector;
        private Regiment _regiment;
        private GameFleet _secondFleet;
        private FleetWindowSession _session;
        private SpecialForces _specialForces;
        private Starfighter _starfighter;
        private UIContext _uiContext;
        private UIWindow _window;
        private GameObject _windowObject;

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _ownerId, DisplayName = "Alliance" });
            game.Summary.PlayerFactionID = _ownerId;
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _ownerId,
            };
            _fleet = CreateCompositeFleet();
            CapitalShip secondShip = CreateCapitalShip("second-ship", "Second Ship", false);
            _secondFleet = new GameFleet(
                _ownerId,
                "Second Fleet",
                new System.Collections.Generic.List<CapitalShip> { secondShip }
            )
            {
                InstanceID = "second-fleet",
                Movement = new MovementState(),
            };
            _planet.Fleets.Add(_fleet);
            _planet.Fleets.Add(_secondFleet);
            AttachFleetGraph(_planet, _fleet);
            AttachFleetGraph(_planet, _secondFleet);
            _windowObject = new GameObject("FleetWindow", typeof(RectTransform), typeof(UIWindow));
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 18, 29, 410, 300, false, true, true);
            _session = new FleetWindowSession(
                new GalaxyMapPlanet(new GamePlanetSystem(), _planet, string.Empty),
                _window
            );
            _projector = new FleetWindowProjector(() => _uiContext);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FleetWindowProjector(null));
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
            FleetWindowProjector projector = new FleetWindowProjector(() => null);

            Assert.Throws<InvalidOperationException>(() =>
                projector.Build(_session, _window, true)
            );
        }

        [Test]
        public void Build_CompositeFleet_ReturnsCompleteCapitalShipPresentation()
        {
            FleetWindowRenderData data = _projector.Build(_session, _window, true);

            Assert.AreEqual(18, data.X);
            Assert.AreEqual(29, data.Y);
            Assert.AreEqual("Corellia", data.Caption);
            Assert.IsNotNull(data.TitleTexture);
            Assert.IsNotNull(data.DetailBackgroundTexture);
            Assert.AreEqual(2, data.FleetRows.Count);
            FleetListRowRenderData firstRow = data.FleetRows[0];
            Assert.AreEqual("First Fleet", firstRow.Name);
            Assert.IsNotNull(firstRow.IconTexture);
            Assert.IsNull(firstRow.EnrouteOverlayTexture);
            Assert.IsNotNull(firstRow.DamagedOverlayTexture);
            Assert.IsNotNull(firstRow.StarfighterBadgeTexture);
            Assert.IsNotNull(firstRow.TroopBadgeTexture);
            Assert.IsNotNull(firstRow.PersonnelBadgeTexture);
            Assert.IsNotNull(firstRow.SelectionTexture);
            Assert.IsNotNull(data.FleetRows[1].EnrouteOverlayTexture);
            Assert.IsNull(data.FleetRows[1].SelectionTexture);
            Assert.AreEqual(FleetWindowTab.CapitalShips, data.ActiveTab);
            Assert.AreEqual(0, data.SelectedFleetIndex);
            Assert.IsTrue(data.HasSelectedFleet);
            Assert.IsNotNull(data.BannerTexture);
            Assert.IsNull(data.BannerEnrouteOverlayTexture);
            Assert.IsNotNull(data.BannerDamagedOverlayTexture);
            Assert.AreEqual("First Fleet", data.FleetName);
            Assert.AreEqual(
                (Color32)_uiContext.GetTheme(_ownerId).GetPrimaryColor(),
                data.FleetNameColor
            );
            Assert.IsFalse(data.ShowCapacity);
            Assert.AreEqual(string.Empty, data.CapacityLeft);
            Assert.AreEqual(string.Empty, data.CapacityRight);
            Assert.AreEqual(FleetWindowRenderData.TabCount, data.Tabs.Count);
            Assert.IsTrue(data.Tabs.All(tab => tab.Texture != null));
            Assert.IsTrue(data.Tabs.All(tab => tab.PressedTexture != null));
            Assert.AreEqual(1, data.DetailItems.Count);
            StrategyUnitCardRenderData card = data.DetailItems[0];
            Assert.AreEqual("Capital Ship", card.Name);
            Assert.IsFalse(card.UseAlternateNameLayout);
            Assert.IsNull(card.BackgroundTexture);
            Assert.IsNull(card.EnrouteOverlayTexture);
            Assert.IsNotNull(card.DamagedOverlayTexture);
            Assert.IsNotNull(card.EntityTexture);
            Assert.IsNotNull(card.SelectionTexture);
            Assert.AreEqual(-1, card.EntityFrameYOffset);
            Assert.IsNotNull(card.StarfighterBadgeTexture);
            Assert.IsNotNull(card.TroopBadgeTexture);
            Assert.IsNotNull(card.PersonnelBadgeTexture);
            Assert.IsTrue(card.CanDrag);
        }

        [Test]
        public void Build_MovingFleet_ReturnsFleetAndDetailTransitPresentation()
        {
            _session.SelectItem(_secondFleet);

            FleetWindowRenderData data = _projector.Build(_session, _window, false);

            Assert.AreEqual(1, data.SelectedFleetIndex);
            Assert.AreEqual("Second Fleet", data.FleetName);
            Assert.IsNotNull(data.BannerEnrouteOverlayTexture);
            Assert.IsNull(data.BannerDamagedOverlayTexture);
            Assert.IsNotNull(data.FleetRows[1].EnrouteOverlayTexture);
            Assert.IsNotNull(data.DetailItems[0].EnrouteOverlayTexture);
            Assert.IsNull(data.DetailItems[0].DamagedOverlayTexture);
            Assert.IsNotNull(data.TitleTexture);
        }

        [Test]
        public void Build_StarfighterTab_ReturnsCapacityLossesAndSelectionPresentation()
        {
            _session.SelectTab(FleetWindowTab.Starfighters);

            FleetWindowRenderData data = _projector.Build(_session, _window, true);

            Assert.IsTrue(data.ShowCapacity);
            Assert.AreEqual("1", data.CapacityLeft);
            Assert.AreEqual("4", data.CapacityRight);
            Assert.AreEqual(1, data.DetailItems.Count);
            StrategyUnitCardRenderData card = data.DetailItems[0];
            Assert.AreEqual("Starfighter", card.Name);
            Assert.IsNull(card.BackgroundTexture);
            Assert.IsNull(card.EnrouteOverlayTexture);
            Assert.IsNotNull(card.DamagedOverlayTexture);
            Assert.IsNotNull(card.SelectionTexture);
            Assert.AreEqual(0, card.EntityFrameYOffset);
        }

        [Test]
        public void Build_StarfighterUnderConstruction_UsesConstructionBackground()
        {
            _starfighter.ManufacturingStatus = ManufacturingStatus.Building;
            _starfighter.Movement = new MovementState();
            _session.SelectTab(FleetWindowTab.Starfighters);

            FleetWindowRenderData data = _projector.Build(_session, _window, true);

            StrategyUnitCardRenderData card = data.DetailItems[0];
            Assert.IsNotNull(card.BackgroundTexture);
            Assert.IsNotNull(card.EntityTexture);
            Assert.IsNull(card.EnrouteOverlayTexture);
            Assert.IsNull(card.DamagedOverlayTexture);
        }

        [Test]
        public void Build_RegimentTab_ReturnsCapacityAndPersonnelBackground()
        {
            _session.SelectTab(FleetWindowTab.Regiments);

            FleetWindowRenderData data = _projector.Build(_session, _window, true);

            Assert.IsTrue(data.ShowCapacity);
            Assert.AreEqual("1", data.CapacityLeft);
            Assert.AreEqual("3", data.CapacityRight);
            Assert.AreEqual(1, data.DetailItems.Count);
            Assert.AreEqual("Regiment", data.DetailItems[0].Name);
            Assert.IsNotNull(data.DetailItems[0].BackgroundTexture);
            Assert.IsNull(data.DetailItems[0].EnrouteOverlayTexture);
        }

        [Test]
        public void Build_PersonnelTab_ReturnsOfficerAndSpecialForcesPresentation()
        {
            _session.SelectTab(FleetWindowTab.Personnel);

            FleetWindowRenderData data = _projector.Build(_session, _window, true);

            Assert.IsFalse(data.ShowCapacity);
            Assert.AreEqual(2, data.DetailItems.Count);
            StrategyUnitCardRenderData officerCard = data.DetailItems[0];
            Assert.AreEqual("Officer", officerCard.Name);
            Assert.IsTrue(officerCard.UseAlternateNameLayout);
            Assert.IsNotNull(officerCard.BackgroundTexture);
            Assert.IsNotNull(officerCard.DamagedOverlayTexture);
            Assert.IsNotNull(officerCard.CapturedOverlayTexture);
            Assert.IsNotNull(officerCard.SelectionTexture);
            StrategyUnitCardRenderData specialForcesCard = data.DetailItems[1];
            Assert.AreEqual("Special Forces", specialForcesCard.Name);
            Assert.IsTrue(specialForcesCard.UseAlternateNameLayout);
            Assert.IsNotNull(specialForcesCard.BackgroundTexture);
            Assert.IsNull(specialForcesCard.SelectionTexture);
        }

        [Test]
        public void Build_RenameTargets_ReturnsCurrentRenamePlacementAndText()
        {
            _session.BeginRename(_secondFleet);

            FleetWindowRenderData fleetRename = _projector.Build(_session, _window, true);

            Assert.AreEqual(1, fleetRename.RenameFleetRowIndex);
            Assert.AreEqual(-1, fleetRename.RenameDetailItemIndex);
            Assert.AreEqual("Second Fleet", fleetRename.RenameText);

            _session.BeginRename(_capitalShip);

            FleetWindowRenderData shipRename = _projector.Build(_session, _window, true);

            Assert.AreEqual(-1, shipRename.RenameFleetRowIndex);
            Assert.AreEqual(0, shipRename.RenameDetailItemIndex);
            Assert.AreEqual("Capital Ship", shipRename.RenameText);
        }

        [Test]
        public void Build_EmptyPlanet_ReturnsEmptyFleetPresentation()
        {
            Planet planet = new Planet
            {
                InstanceID = "empty",
                DisplayName = "Empty",
                OwnerInstanceID = _ownerId,
            };
            FleetWindowSession session = new FleetWindowSession(
                new GalaxyMapPlanet(new GamePlanetSystem(), planet, string.Empty),
                _window
            );

            FleetWindowRenderData data = _projector.Build(session, _window, true);

            Assert.AreEqual("Empty", data.Caption);
            Assert.IsEmpty(data.FleetRows);
            Assert.AreEqual(-1, data.SelectedFleetIndex);
            Assert.IsFalse(data.HasSelectedFleet);
            Assert.IsNull(data.BannerTexture);
            Assert.AreEqual(string.Empty, data.FleetName);
            Assert.IsEmpty(data.Tabs);
            Assert.IsEmpty(data.DetailItems);
        }

        private GameFleet CreateCompositeFleet()
        {
            _capitalShip = CreateCapitalShip("ship", "Capital Ship", true);
            _capitalShip.StarfighterCapacity = 4;
            _capitalShip.RegimentCapacity = 3;
            Starfighter fighterDefinition = ResourceManager
                .GetEntityData<Starfighter>()
                .First(fighter => fighter.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true);
            _starfighter = new Starfighter
            {
                InstanceID = "starfighter",
                DisplayName = "Starfighter",
                OwnerInstanceID = _ownerId,
                DisplayImagePath = fighterDefinition.DisplayImagePath,
                SmallDisplayImagePath = fighterDefinition.SmallDisplayImagePath,
                InTransitImagePath = fighterDefinition.InTransitImagePath,
                InTransitSmallImagePath = fighterDefinition.InTransitSmallImagePath,
                DamagedImagePath = fighterDefinition.DamagedImagePath,
                DamagedSmallImagePath = fighterDefinition.DamagedSmallImagePath,
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentSquadronSize = 6,
                MaxSquadronSize = 12,
            };
            Regiment regimentDefinition = ResourceManager
                .GetEntityData<Regiment>()
                .First(regiment => regiment.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true);
            _regiment = new Regiment
            {
                InstanceID = "regiment",
                DisplayName = "Regiment",
                OwnerInstanceID = _ownerId,
                DisplayImagePath = regimentDefinition.DisplayImagePath,
                SmallDisplayImagePath = regimentDefinition.SmallDisplayImagePath,
                InTransitImagePath = regimentDefinition.InTransitImagePath,
                InTransitSmallImagePath = regimentDefinition.InTransitSmallImagePath,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Officer officerDefinition = ResourceManager
                .GetEntityData<Officer>()
                .First(officer =>
                    officer.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true
                    && !string.IsNullOrEmpty(officer.InjuredImagePath)
                    && !string.IsNullOrEmpty(officer.CapturedOverlayImagePath)
                );
            _officer = new Officer
            {
                InstanceID = "officer",
                DisplayName = "Officer",
                OwnerInstanceID = _ownerId,
                DisplayImagePath = officerDefinition.DisplayImagePath,
                SmallDisplayImagePath = officerDefinition.SmallDisplayImagePath,
                InjuredImagePath = officerDefinition.InjuredImagePath,
                CapturedOverlayImagePath = officerDefinition.CapturedOverlayImagePath,
                InjuryPoints = 1,
                IsCaptured = true,
            };
            SpecialForces specialForcesDefinition = ResourceManager
                .GetEntityData<SpecialForces>()
                .First(unit => unit.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true);
            _specialForces = new SpecialForces
            {
                InstanceID = "special-forces",
                DisplayName = "Special Forces",
                OwnerInstanceID = _ownerId,
                DisplayImagePath = specialForcesDefinition.DisplayImagePath,
                SmallDisplayImagePath = specialForcesDefinition.SmallDisplayImagePath,
                InTransitImagePath = specialForcesDefinition.InTransitImagePath,
                InTransitSmallImagePath = specialForcesDefinition.InTransitSmallImagePath,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _capitalShip.Starfighters.Add(_starfighter);
            _capitalShip.Regiments.Add(_regiment);
            _capitalShip.Officers.Add(_officer);
            _capitalShip.SpecialForces.Add(_specialForces);
            return new GameFleet(
                _ownerId,
                "First Fleet",
                new System.Collections.Generic.List<CapitalShip> { _capitalShip }
            )
            {
                InstanceID = "first-fleet",
            };
        }

        private static CapitalShip CreateCapitalShip(
            string instanceId,
            string displayName,
            bool damaged
        )
        {
            CapitalShip definition = ResourceManager
                .GetEntityData<CapitalShip>()
                .First(ship => ship.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true);
            return new CapitalShip
            {
                InstanceID = instanceId,
                DisplayName = displayName,
                OwnerInstanceID = _ownerId,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
                InTransitImagePath = definition.InTransitImagePath,
                InTransitSmallImagePath = definition.InTransitSmallImagePath,
                DamagedImagePath = definition.DamagedImagePath,
                DamagedSmallImagePath = definition.DamagedSmallImagePath,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = 100,
                CurrentHullStrength = damaged ? 50 : 100,
            };
        }

        private static void AttachFleetGraph(Planet planet, GameFleet fleet)
        {
            fleet.SetParent(planet);
            foreach (CapitalShip ship in fleet.CapitalShips)
            {
                ship.SetParent(fleet);
                foreach (ISceneNode child in ship.GetChildren())
                    child.SetParent(ship);
            }
        }
    }
}
