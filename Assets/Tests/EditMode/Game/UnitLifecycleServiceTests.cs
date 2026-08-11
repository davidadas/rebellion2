using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public sealed class UnitLifecycleServiceTests
    {
        private GameRoot _game;
        private Faction _alliance;
        private Faction _empire;
        private Planet _planet;
        private Officer _officer;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(new GameSummary(), TestContent.Data.GameConfig);
            _alliance = new Faction { InstanceID = "ALLIANCE" };
            _empire = new Faction { InstanceID = "EMPIRE" };
            _game.Factions.Add(_alliance);
            _game.Factions.Add(_empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "SYSTEM" };
            _planet = new Planet
            {
                InstanceID = "PLANET",
                OwnerInstanceID = _alliance.InstanceID,
                IsColonized = true,
            };
            _officer = new Officer
            {
                InstanceID = "OFFICER",
                OwnerInstanceID = _alliance.InstanceID,
            };
            _game.AttachNode(system, _game.Galaxy);
            _game.AttachNode(_planet, system);
            _game.AttachNode(_officer, _planet);
        }

        [Test]
        public void AddToVoid_OwnedUnit_DetachesAndRetainsRegistration()
        {
            _game.UnitLifecycle.AddToVoid(_officer);

            Assert.IsNull(_officer.GetParent());
            Assert.IsTrue(_alliance.VoidPool.Contains(_officer));
            Assert.AreEqual(_planet.InstanceID, _officer.LastParentInstanceID);
            Assert.AreSame(_officer, _game.GetSceneNodeByInstanceID<Officer>(_officer.InstanceID));
            Assert.IsFalse(_alliance.GetOwnedUnitsByType<Officer>().Contains(_officer));
        }

        [Test]
        public void SetStatus_UnitInVoid_SetsStatus()
        {
            _game.UnitLifecycle.AddToVoid(_officer);

            _game.UnitLifecycle.SetStatus(_officer, VoidStatus.Captured);

            Assert.AreEqual(VoidStatus.Captured, _officer.VoidState.Status);
        }

        [Test]
        public void Activate_UnitInVoid_AttachesAndClearsStatus()
        {
            _game.UnitLifecycle.AddToVoid(_officer);
            _game.UnitLifecycle.SetStatus(_officer, VoidStatus.OnMission, "On Mission (Dagobah)");

            _game.UnitLifecycle.Activate(_officer, _planet);

            Assert.AreSame(_planet, _officer.GetParent());
            Assert.IsFalse(_alliance.VoidPool.Contains(_officer));
            Assert.IsNull(_officer.VoidState);
        }

        [Test]
        public void Activate_DestinationRejectsUnit_PreservesVoidState()
        {
            _game.UnitLifecycle.AddToVoid(_officer);
            _game.UnitLifecycle.SetStatus(_officer, VoidStatus.OnMission);
            PlanetSystem invalidDestination = _planet.GetParent() as PlanetSystem;

            Assert.Throws<InvalidOperationException>(() =>
                _game.UnitLifecycle.Activate(_officer, invalidDestination)
            );
            Assert.IsTrue(_alliance.VoidPool.Contains(_officer));
            Assert.AreEqual(VoidStatus.OnMission, _officer.VoidState.Status);
        }

        [Test]
        public void ChangeOwnership_ActiveNode_MigratesFactionOwnership()
        {
            _game.UnitLifecycle.ChangeOwnership(_planet, _empire.InstanceID);

            Assert.IsFalse(_alliance.GetOwnedUnitsByType<Planet>().Contains(_planet));
            Assert.IsTrue(_empire.GetOwnedUnitsByType<Planet>().Contains(_planet));
            Assert.AreEqual(_empire.InstanceID, _planet.OwnerInstanceID);
        }

        [Test]
        public void ChangeOwnership_VoidUnit_MigratesVoidPool()
        {
            _game.UnitLifecycle.AddToVoid(_officer);

            _game.UnitLifecycle.ChangeOwnership(_officer, _empire.InstanceID);

            Assert.IsFalse(_alliance.VoidPool.Contains(_officer));
            Assert.IsTrue(_empire.VoidPool.Contains(_officer));
            Assert.AreEqual(_empire.InstanceID, _officer.OwnerInstanceID);
        }

        [Test]
        public void ChangeOwnership_UnknownFaction_ThrowsException()
        {
            Assert.Throws<SceneNodeNotFoundException>(() =>
                _game.UnitLifecycle.ChangeOwnership(_planet, "UNKNOWN")
            );
        }
    }
}
