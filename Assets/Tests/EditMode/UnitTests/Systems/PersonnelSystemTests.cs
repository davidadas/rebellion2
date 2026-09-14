using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Sectors
{
    [TestFixture]
    public class PersonnelSystemTests
    {
        private const string _ownerId = "owner";

        private GameRoot _game;
        private PersonnelSystem _personnelSystem;
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
            _personnelSystem = new PersonnelSystem(_game);
        }

        /// <summary>
        /// Verifies constructor with null game throws argument null exception.
        /// </summary>
        [Test]
        public void Constructor_WithNullGame_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new PersonnelSystem(null)
            );

            Assert.AreEqual("game", exception.ParamName);
        }

        /// <summary>
        /// Verifies kill officer active officer marks killed and retains identity.
        /// </summary>
        [Test]
        public void KillOfficer_ActiveOfficer_MarksKilledAndRetainsIdentity()
        {
            Officer officer = CreateOfficer("killed-officer");
            officer.Movement = new MovementState();
            _game.AttachNode(officer, _planet);

            _personnelSystem.KillOfficer(officer);

            Assert.IsTrue(officer.IsKilled);
            Assert.IsNull(officer.Movement);
            Assert.AreSame(_planet, officer.GetParent());
            Assert.IsFalse(officer.IsActive());
            Assert.AreSame(
                officer,
                _game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID, includeDisabled: true)
            );
            CollectionAssert.DoesNotContain(_game.GetSceneNodesByType<Officer>(), officer);
        }

        /// <summary>
        /// Verifies can retire owned officer and special forces returns true.
        /// </summary>
        [Test]
        public void CanRetire_OwnedOfficerAndSpecialForces_ReturnsTrue()
        {
            Officer officer = CreateOfficer("officer");
            SpecialForces specialForces = CreateSpecialForces("special-forces");
            _game.AttachNode(officer, _planet);
            _game.AttachNode(specialForces, _planet);

            bool canRetire = _personnelSystem.CanRetire(
                new ISceneNode[] { officer, specialForces },
                _ownerId
            );

            Assert.IsTrue(canRetire);
        }

        /// <summary>
        /// Verifies can retire blocked personnel returns false.
        /// </summary>
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

            Assert.IsFalse(
                _personnelSystem.CanRetire(new ISceneNode[] { capturedOfficer }, _ownerId)
            );
            Assert.IsFalse(
                _personnelSystem.CanRetire(new ISceneNode[] { movingOfficer }, _ownerId)
            );
            Assert.IsFalse(
                _personnelSystem.CanRetire(new ISceneNode[] { buildingForces }, _ownerId)
            );
        }

        /// <summary>
        /// Verifies can retire snapshot selection resolves live personnel.
        /// </summary>
        [Test]
        public void CanRetire_SnapshotSelection_ResolvesLivePersonnel()
        {
            Officer officer = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);

            bool canRetire = _personnelSystem.CanRetire(
                new ISceneNode[] { new Officer { InstanceID = officer.InstanceID } },
                _ownerId
            );

            Assert.IsTrue(canRetire);
        }

        /// <summary>
        /// Verifies retire owned personnel removes complete selection.
        /// </summary>
        [Test]
        public void Retire_OwnedPersonnel_RemovesCompleteSelection()
        {
            Officer officer = CreateOfficer("officer");
            SpecialForces specialForces = CreateSpecialForces("special-forces");
            _game.AttachNode(officer, _planet);
            _game.AttachNode(specialForces, _planet);

            bool retired = _personnelSystem.Retire(
                new ISceneNode[] { officer, specialForces },
                _ownerId
            );

            Assert.IsTrue(retired);
            Assert.AreSame(_planet, officer.GetParent());
            Assert.AreSame(_planet, specialForces.GetParent());
            Assert.IsFalse(officer.IsActive());
            Assert.IsFalse(specialForces.IsActive());
            Assert.IsTrue(officer.IsRetired);
            Assert.IsTrue(specialForces.IsRetired);
        }

        /// <summary>
        /// Verifies retire invalid member preserves complete selection.
        /// </summary>
        [Test]
        public void Retire_InvalidMember_PreservesCompleteSelection()
        {
            Officer officer = CreateOfficer("officer");
            Officer mainOfficer = CreateOfficer("main-officer");
            mainOfficer.IsMain = true;
            _game.AttachNode(officer, _planet);
            _game.AttachNode(mainOfficer, _planet);

            bool retired = _personnelSystem.Retire(
                new ISceneNode[] { officer, mainOfficer },
                _ownerId
            );

            Assert.IsFalse(retired);
            Assert.AreSame(_planet, officer.GetParent());
            Assert.AreSame(_planet, mainOfficer.GetParent());
        }

        /// <summary>
        /// Verifies retire unauthorized owner preserves personnel.
        /// </summary>
        [Test]
        public void Retire_UnauthorizedOwner_PreservesPersonnel()
        {
            Officer officer = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);

            bool retired = _personnelSystem.Retire(new ISceneNode[] { officer }, "other-owner");

            Assert.IsFalse(retired);
            Assert.AreSame(_planet, officer.GetParent());
            Assert.AreSame(officer, _game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID));
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
