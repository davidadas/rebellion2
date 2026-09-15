using System;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Status
{
    [TestFixture]
    public class StatusWindowSessionTests
    {
        private GameObject _windowObject;

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_windowObject != null)
                UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        /// <summary>
        /// Verifies constructor null dependencies throw argument null exception.
        /// </summary>
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

        /// <summary>
        /// Verifies reconcile snapshot backed target rebinds planet and item by identity.
        /// </summary>
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
                currentMapPlanet.PlanetSector,
                new[] { currentMapPlanet }
            );
            currentOfficer = new Officer { InstanceID = "officer" };

            bool reconciled = session.Reconcile(new[] { sector });

            Assert.IsTrue(reconciled);
            Assert.AreSame(currentMapPlanet, session.Target.Planet);
            Assert.AreSame(currentOfficer, session.Target.Item);
            Assert.AreEqual(ManufacturingType.Ship, session.Target.ManufacturingType);
        }

        /// <summary>
        /// Verifies reconcile missing snapshot planet returns false and preserves target.
        /// </summary>
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

        /// <summary>
        /// Verifies reconcile missing snapshot item returns false and preserves target.
        /// </summary>
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

        /// <summary>
        /// Verifies reconcile static template target preserves original item.
        /// </summary>
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

        /// <summary>
        /// Verifies reconcile null sectors throws argument null exception.
        /// </summary>
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

        /// <summary>
        /// Creates window.
        /// </summary>
        /// <returns>The created window.</returns>
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

        /// <summary>
        /// Creates map planet.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <returns>The created map planet.</returns>
        private static GalaxyMapPlanet CreateMapPlanet(Planet planet)
        {
            return new GalaxyMapPlanet(new GalaxyPlanetSector(), planet, string.Empty);
        }
    }
}
