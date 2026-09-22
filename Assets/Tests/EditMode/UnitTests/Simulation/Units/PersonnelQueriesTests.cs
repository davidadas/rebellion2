using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class PersonnelQueriesTests
    {
        private const string _ownerId = "owner";

        private GameRoot _game;
        private PersonnelQueries _queries;
        private Planet _planet;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _game.GetFactions().Add(new Faction { InstanceID = _ownerId });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _ownerId,
                IsColonized = true,
            };
            _game.AttachNode(_planet, sector);
            _queries = new PersonnelQueries(_game);
        }

        [Test]
        public void Constructor_WithNullGame_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new PersonnelQueries(null)
            );

            Assert.AreEqual("game", exception.ParamName);
        }

        [Test]
        public void CanRetire_OwnedOfficerAndSpecialForces_ReturnsTrue()
        {
            Officer officer = CreateOfficer("officer");
            SpecialForces specialForces = CreateSpecialForces("special-forces");
            _game.AttachNode(officer, _planet);
            _game.AttachNode(specialForces, _planet);

            bool canRetire = _queries.CanRetire(
                new ISceneNode[] { officer, specialForces },
                _ownerId
            );

            Assert.IsTrue(canRetire);
        }

        [Test]
        public void CanRetire_BlockedPersonnel_ReturnsFalse()
        {
            Officer capturedOfficer = CreateOfficer("captured");
            capturedOfficer.IsCaptured = true;
            Officer movingOfficer = CreateOfficer("moving");
            movingOfficer.Movement = new MovementState();
            SpecialForces buildingForces = CreateSpecialForces("building");
            buildingForces.ManufacturingStatus = ManufacturingStatus.Building;
            _game.AttachNode(capturedOfficer, _planet);
            _game.AttachNode(movingOfficer, _planet);
            _game.AttachNode(buildingForces, _planet);

            Assert.IsFalse(_queries.CanRetire(new ISceneNode[] { capturedOfficer }, _ownerId));
            Assert.IsFalse(_queries.CanRetire(new ISceneNode[] { movingOfficer }, _ownerId));
            Assert.IsFalse(_queries.CanRetire(new ISceneNode[] { buildingForces }, _ownerId));
        }

        [Test]
        public void CanRetire_SnapshotSelection_ResolvesLivePersonnel()
        {
            Officer officer = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);

            bool canRetire = _queries.CanRetire(
                new ISceneNode[] { new Officer { InstanceID = officer.InstanceID } },
                _ownerId
            );

            Assert.IsTrue(canRetire);
        }

        [Test]
        public void CanRetire_EligibleSelection_LeavesPersonnelActive()
        {
            Officer officer = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);

            _queries.CanRetire(new ISceneNode[] { officer }, _ownerId);

            Assert.IsTrue(officer.IsActive());
            Assert.IsFalse(officer.IsRetired);
        }

        [Test]
        public void CanRetire_CapturedLiveOfficerWithEligibleSnapshot_ReturnsFalse()
        {
            Officer officer = CreateOfficer("officer");
            officer.IsCaptured = true;
            _game.AttachNode(officer, _planet);

            bool canRetire = _queries.CanRetire(
                new ISceneNode[] { CreateOfficer(officer.InstanceID) },
                _ownerId
            );

            Assert.IsFalse(canRetire);
        }

        /// <summary>
        /// Creates officer.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The created officer.</returns>
        private static Officer CreateOfficer(string instanceId)
        {
            return new Officer { InstanceID = instanceId, OwnerInstanceID = _ownerId };
        }

        /// <summary>
        /// Creates special forces.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The created special forces.</returns>
        private static SpecialForces CreateSpecialForces(string instanceId)
        {
            return new SpecialForces
            {
                InstanceID = instanceId,
                OwnerInstanceID = _ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }
    }
}
