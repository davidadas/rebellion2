using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class FleetLifecycleTests
    {
        private const string _ownerId = "owner";

        private GameRoot _game;
        private Planet _planet;

        /// <summary>Creates a populated test planet.</summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.GetFactions().Add(new Faction { InstanceID = _ownerId });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _ownerId,
                IsColonized = true,
            };
            _game.AttachNode(sector, _game.Galaxy);
            _game.AttachNode(_planet, sector);
        }

        [Test]
        public void RemoveEmptyFleet_PopulatedFleet_PreservesFleet()
        {
            Fleet fleet = new Fleet(_ownerId, "fleet") { InstanceID = "fleet" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = _ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(fleet, _planet);
            _game.AttachNode(ship, fleet);

            bool removed = FleetLifecycle.RemoveEmptyFleet(_game, fleet);

            Assert.IsFalse(removed);
            Assert.AreSame(_planet, fleet.GetParent());
        }

        [Test]
        public void RemoveEmptyFleet_EmptyFleet_RemovesFleet()
        {
            Fleet fleet = new Fleet(_ownerId, "fleet") { InstanceID = "fleet" };
            _game.AttachNode(fleet, _planet);

            bool removed = FleetLifecycle.RemoveEmptyFleet(_game, fleet);

            Assert.IsTrue(removed);
            Assert.IsNull(fleet.GetParent());
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID));
        }
    }
}
