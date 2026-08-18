using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class PersonnelSystemTests
    {
        private const string _ownerId = "owner";

        private GameRoot _game;
        private PersonnelSystem _personnelSystem;
        private Planet _planet;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _game.Factions.Add(new Faction { InstanceID = _ownerId });
            PlanetSector system = new PlanetSector { InstanceID = "system" };
            _game.AttachNode(system, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _ownerId,
                IsColonized = true,
            };
            _game.AttachNode(_planet, system);
            _personnelSystem = new PersonnelSystem(_game);
        }

        [Test]
        public void Constructor_WithNullGame_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new PersonnelSystem(null)
            );

            Assert.AreEqual("game", exception.ParamName);
        }

        [Test]
        public void KillOfficer_ActiveOfficer_MarksKilledAndRetainsIdentity()
        {
            Officer officer = CreateOfficer("killed-officer");
            officer.Movement = new MovementState();
            _game.AttachNode(officer, _planet);

            _personnelSystem.KillOfficer(officer);

            Assert.IsTrue(officer.IsKilled);
            Assert.IsNull(officer.Movement);
            Assert.IsNull(officer.GetParent());
            Assert.IsTrue(_game.IsInVoid(officer));
            Assert.AreSame(officer, _game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID));
            Assert.Contains(
                officer,
                _game.GetFactionByOwnerInstanceID(_ownerId).GetOwnedUnitsByType<Officer>()
            );
            CollectionAssert.DoesNotContain(_game.GetSceneNodesByType<Officer>(), officer);
        }

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
            Assert.IsNull(officer.GetParent());
            Assert.IsNull(specialForces.GetParent());
            Assert.IsTrue(_game.IsInVoid(officer));
            Assert.IsTrue(_game.IsInVoid(specialForces));
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

            bool retired = _personnelSystem.Retire(
                new ISceneNode[] { officer, mainOfficer },
                _ownerId
            );

            Assert.IsFalse(retired);
            Assert.AreSame(_planet, officer.GetParent());
            Assert.AreSame(_planet, mainOfficer.GetParent());
        }

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

        private static Officer CreateOfficer(string instanceId)
        {
            return new Officer { InstanceID = instanceId, OwnerInstanceID = _ownerId };
        }

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
