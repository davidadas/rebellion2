using System;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using GamePlanetSector = Rebellion.Game.Galaxy.PlanetSector;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalaxyMapSnapshotTests
    {
        [Test]
        public void Planet_Values_PreservesNormalizedSnapshot()
        {
            GamePlanetSector system = new GamePlanetSector();
            Planet planet = new Planet { OwnerInstanceID = "owner" };

            GalaxyMapPlanet snapshot = new GalaxyMapPlanet(system, planet, null);

            Assert.AreSame(system, snapshot.PlanetSector);
            Assert.AreSame(planet, snapshot.Planet);
            Assert.AreEqual(string.Empty, snapshot.PlanetIconPath);
            Assert.AreEqual("owner", snapshot.OwnerFactionId);
            Assert.IsNull(snapshot.Sector);
        }

        [Test]
        public void Sector_SourceChanges_PreservesPlanetSnapshotAndAttachesSector()
        {
            GamePlanetSector system = new GamePlanetSector();
            GalaxyMapPlanet planet = new GalaxyMapPlanet(system, new Planet(), string.Empty);
            GalaxyMapPlanet replacement = new GalaxyMapPlanet(system, new Planet(), string.Empty);
            GalaxyMapPlanet[] planets = { planet };

            GalaxyMapSector sector = new GalaxyMapSector(system, planets);
            planets[0] = replacement;

            Assert.AreSame(system, sector.PlanetSector);
            Assert.AreSame(planet, sector.Planets[0]);
            Assert.AreSame(sector, planet.Sector);
            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.Generic.IList<GalaxyMapPlanet>)sector.Planets)[0] = replacement
            );
        }

        [Test]
        public void Sector_NullPlanets_ReturnsEmptySnapshot()
        {
            GalaxyMapSector sector = new GalaxyMapSector(new GamePlanetSector(), null);

            Assert.IsEmpty(sector.Planets);
        }

        [Test]
        public void Sector_PlanetAlreadyAttachedToDifferentSector_ThrowsInvalidOperationException()
        {
            GamePlanetSector system = new GamePlanetSector();
            GalaxyMapPlanet planet = new GalaxyMapPlanet(system, new Planet(), string.Empty);
            new GalaxyMapSector(system, new[] { planet });

            Assert.Throws<InvalidOperationException>(() =>
                new GalaxyMapSector(system, new[] { planet })
            );
        }

        [Test]
        public void AttachToSector_NullSector_ThrowsArgumentNullException()
        {
            GalaxyMapPlanet planet = new GalaxyMapPlanet(
                new GamePlanetSector(),
                new Planet(),
                string.Empty
            );

            Assert.Throws<ArgumentNullException>(() => planet.AttachToSector(null));
        }
    }
}
