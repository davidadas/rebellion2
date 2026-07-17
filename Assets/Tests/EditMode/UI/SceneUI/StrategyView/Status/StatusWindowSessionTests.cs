using System;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Status
{
    [TestFixture]
    public class StatusWindowSessionTests
    {
        private GameObject _windowObject;

        [TearDown]
        public void TearDown()
        {
            if (_windowObject != null)
                UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_NullDependencies_ThrowArgumentNullException()
        {
            UIWindow window = CreateWindow();
            StrategyStatusTarget target = new StrategyStatusTarget(null, null);

            Assert.Throws<ArgumentNullException>(() =>
                new StatusWindowSession(null, target, false, _ => null)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StatusWindowSession(window, null, false, _ => null)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StatusWindowSession(window, target, false, null)
            );
        }

        [Test]
        public void Constructor_CompleteTarget_StoresWindowTargetAndAvailability()
        {
            UIWindow window = CreateWindow();
            Officer officer = new Officer { InstanceID = "officer" };
            StrategyStatusTarget target = new StrategyStatusTarget(
                null,
                officer,
                ManufacturingType.Troop
            );

            StatusWindowSession session = new StatusWindowSession(window, target, true, _ => null);

            Assert.AreSame(window, session.Window);
            Assert.AreSame(target, session.Target);
            Assert.IsTrue(session.InfoDisabled);
        }

        [Test]
        public void Reconcile_SnapshotBackedTarget_RebindsPlanetAndItemByIdentity()
        {
            UIWindow window = CreateWindow();
            Planet originalPlanet = new Planet { InstanceID = "planet" };
            GalaxyMapPlanet originalMapPlanet = CreateMapPlanet(originalPlanet);
            Officer originalOfficer = new Officer { InstanceID = "officer" };
            Officer currentOfficer = originalOfficer;
            StatusWindowSession session = new StatusWindowSession(
                window,
                new StrategyStatusTarget(
                    originalMapPlanet,
                    originalOfficer,
                    ManufacturingType.Ship
                ),
                false,
                _ => currentOfficer
            );
            Planet currentPlanet = new Planet { InstanceID = "planet" };
            GalaxyMapPlanet currentMapPlanet = CreateMapPlanet(currentPlanet);
            GalaxyMapSector sector = new GalaxyMapSector(
                currentMapPlanet.SectorSystem,
                new[] { currentMapPlanet }
            );
            currentOfficer = new Officer { InstanceID = "officer" };

            bool reconciled = session.Reconcile(new[] { sector });

            Assert.IsTrue(reconciled);
            Assert.AreSame(currentMapPlanet, session.Target.Planet);
            Assert.AreSame(currentOfficer, session.Target.Item);
            Assert.AreEqual(ManufacturingType.Ship, session.Target.ManufacturingType);
        }

        [Test]
        public void Reconcile_MissingSnapshotPlanet_ReturnsFalseAndPreservesTarget()
        {
            UIWindow window = CreateWindow();
            Planet planet = new Planet { InstanceID = "planet" };
            StrategyStatusTarget target = new StrategyStatusTarget(CreateMapPlanet(planet), null);
            StatusWindowSession session = new StatusWindowSession(window, target, false, _ => null);

            bool reconciled = session.Reconcile(Array.Empty<GalaxyMapSector>());

            Assert.IsFalse(reconciled);
            Assert.AreSame(target, session.Target);
        }

        [Test]
        public void Reconcile_MissingSnapshotItem_ReturnsFalseAndPreservesTarget()
        {
            UIWindow window = CreateWindow();
            Officer officer = new Officer { InstanceID = "officer" };
            ISceneNode currentItem = officer;
            StrategyStatusTarget target = new StrategyStatusTarget(null, officer);
            StatusWindowSession session = new StatusWindowSession(
                window,
                target,
                false,
                _ => currentItem
            );
            currentItem = null;

            bool reconciled = session.Reconcile(Array.Empty<GalaxyMapSector>());

            Assert.IsFalse(reconciled);
            Assert.AreSame(target, session.Target);
        }

        [Test]
        public void Reconcile_StaticTemplateTarget_PreservesOriginalItem()
        {
            UIWindow window = CreateWindow();
            Building template = new Building { InstanceID = "template" };
            StatusWindowSession session = new StatusWindowSession(
                window,
                new StrategyStatusTarget(null, template, ManufacturingType.Building),
                false,
                _ => null
            );

            bool reconciled = session.Reconcile(new GalaxyMapSector[] { null });

            Assert.IsTrue(reconciled);
            Assert.IsNull(session.Target.Planet);
            Assert.AreSame(template, session.Target.Item);
            Assert.AreEqual(ManufacturingType.Building, session.Target.ManufacturingType);
        }

        [Test]
        public void Reconcile_NullSectors_ThrowsArgumentNullException()
        {
            StatusWindowSession session = new StatusWindowSession(
                CreateWindow(),
                new StrategyStatusTarget(null, null),
                false,
                _ => null
            );

            Assert.Throws<ArgumentNullException>(() => session.Reconcile(null));
        }

        private UIWindow CreateWindow()
        {
            _windowObject = new GameObject(
                "StatusWindow",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(UIWindow)
            );
            return _windowObject.GetComponent<UIWindow>();
        }

        private static GalaxyMapPlanet CreateMapPlanet(Planet planet)
        {
            return new GalaxyMapPlanet(new GamePlanetSystem(), planet, string.Empty);
        }
    }
}
