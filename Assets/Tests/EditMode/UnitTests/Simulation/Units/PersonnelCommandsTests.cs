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
    public class PersonnelCommandsTests
    {
        private const string _ownerId = "owner";

        private GameRoot _game;
        private PersonnelCommands _commands;
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
            _commands = new PersonnelCommands(new PersonnelQueries(_game));
        }

        [Test]
        public void Constructor_NullQueries_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new PersonnelCommands(null)
            );

            Assert.AreEqual("queries", exception.ParamName);
        }

        [Test]
        public void KillOfficer_ActiveOfficer_MarksKilledAndRetainsIdentity()
        {
            Officer officer = CreateOfficer("killed-officer");
            officer.Movement = new MovementState();
            _game.AttachNode(officer, _planet);

            _commands.KillOfficer(officer);

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

        [Test]
        public void Retire_OwnedPersonnel_RemovesCompleteSelection()
        {
            Officer officer = CreateOfficer("officer");
            SpecialForces specialForces = CreateSpecialForces("special-forces");
            _game.AttachNode(officer, _planet);
            _game.AttachNode(specialForces, _planet);

            bool retired = _commands.Retire(new ISceneNode[] { officer, specialForces }, _ownerId);

            Assert.IsTrue(retired);
            Assert.AreSame(_planet, officer.GetParent());
            Assert.AreSame(_planet, specialForces.GetParent());
            Assert.IsFalse(officer.IsActive());
            Assert.IsFalse(specialForces.IsActive());
            Assert.IsTrue(officer.IsRetired);
            Assert.IsTrue(specialForces.IsRetired);
        }

        [Test]
        public void Retire_InvalidMember_PreservesCompleteSelection()
        {
            Officer officer = CreateOfficer("officer");
            Officer mainOfficer = CreateOfficer("main-officer");
            mainOfficer.IsMain = true;
            _game.AttachNode(officer, _planet);
            _game.AttachNode(mainOfficer, _planet);

            bool retired = _commands.Retire(new ISceneNode[] { officer, mainOfficer }, _ownerId);

            Assert.IsFalse(retired);
            Assert.AreSame(_planet, officer.GetParent());
            Assert.AreSame(_planet, mainOfficer.GetParent());
        }

        [Test]
        public void Retire_UnauthorizedOwner_PreservesPersonnel()
        {
            Officer officer = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);

            bool retired = _commands.Retire(new ISceneNode[] { officer }, "other-owner");

            Assert.IsFalse(retired);
            Assert.AreSame(_planet, officer.GetParent());
            Assert.AreSame(officer, _game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID));
        }

        [Test]
        public void KillOfficer_NullOfficer_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                _commands.KillOfficer(null)
            );

            Assert.AreEqual("officer", exception.ParamName);
        }

        [Test]
        public void Retire_DuplicateIdentity_PreservesPersonnel()
        {
            Officer officer = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);

            bool retired = _commands.Retire(
                new ISceneNode[] { officer, CreateOfficer(officer.InstanceID) },
                _ownerId
            );

            Assert.IsFalse(retired);
            Assert.IsTrue(officer.IsActive());
            Assert.IsFalse(officer.IsRetired);
        }

        [Test]
        public void Retire_UnregisteredMember_PreservesCompleteSelection()
        {
            Officer officer = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);

            bool retired = _commands.Retire(
                new ISceneNode[] { officer, CreateOfficer("missing") },
                _ownerId
            );

            Assert.IsFalse(retired);
            Assert.IsTrue(officer.IsActive());
            Assert.IsFalse(officer.IsRetired);
        }

        [Test]
        public void Retire_SnapshotSelection_RetiresOnlyLivePersonnel()
        {
            Officer officer = CreateOfficer("officer");
            Officer snapshot = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);

            bool retired = _commands.Retire(new ISceneNode[] { snapshot }, _ownerId);

            Assert.IsTrue(retired);
            Assert.IsTrue(officer.IsRetired);
            Assert.IsFalse(snapshot.IsRetired);
        }

        [Test]
        public void Retire_OfficerCapturedAfterEligibilityCheck_PreservesPersonnel()
        {
            Officer officer = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);
            ISceneNode[] selection = { officer };
            Assert.IsTrue(new PersonnelQueries(_game).CanRetire(selection, _ownerId));
            officer.IsCaptured = true;

            bool retired = _commands.Retire(selection, _ownerId);

            Assert.IsFalse(retired);
            Assert.IsTrue(officer.IsActive());
            Assert.IsFalse(officer.IsRetired);
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
