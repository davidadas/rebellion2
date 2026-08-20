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
            GamePlanetSector planetSector = new GamePlanetSector();
            Planet planet = new Planet { OwnerInstanceID = "owner" };

            GalaxyMapPlanet snapshot = new GalaxyMapPlanet(planetSector, planet, null);

            Assert.AreSame(planetSector, snapshot.PlanetSector);
            Assert.AreSame(planet, snapshot.Planet);
            Assert.AreEqual(string.Empty, snapshot.PlanetIconPath);
            Assert.AreEqual("owner", snapshot.OwnerFactionId);
            Assert.IsNull(snapshot.Sector);
        }

        [Test]
        public void Sector_SourceChanges_PreservesPlanetSnapshotAndAttachesSector()
        {
            GamePlanetSector planetSector = new GamePlanetSector();
            GalaxyMapPlanet planet = new GalaxyMapPlanet(planetSector, new Planet(), string.Empty);
            GalaxyMapPlanet replacement = new GalaxyMapPlanet(planetSector, new Planet(), string.Empty);
            GalaxyMapPlanet[] planets = { planet };

            GalaxyMapSector sector = new GalaxyMapSector(planetSector, planets);
            planets[0] = replacement;

            Assert.AreSame(planetSector, sector.PlanetSector);
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
            GamePlanetSector planetSector = new GamePlanetSector();
            GalaxyMapPlanet planet = new GalaxyMapPlanet(planetSector, new Planet(), string.Empty);
            new GalaxyMapSector(planetSector, new[] { planet });

            Assert.Throws<InvalidOperationException>(() =>
                new GalaxyMapSector(planetSector, new[] { planet })
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
