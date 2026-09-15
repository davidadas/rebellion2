using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public class FleetTests
    {
        private Fleet _fleet;
        private CapitalShip _capitalShip1;
        private CapitalShip _capitalShip2;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _fleet = new Fleet
            {
                InstanceID = "FLEET1",
                OwnerInstanceID = "FACTION1",
                Movement = null,
            };

            _capitalShip1 = new CapitalShip
            {
                InstanceID = "SHIP1",
                OwnerInstanceID = "FACTION1",
                StarfighterCapacity = 5,
                RegimentCapacity = 3,
            };

            _capitalShip2 = new CapitalShip
            {
                InstanceID = "SHIP2",
                OwnerInstanceID = "FACTION1",
                StarfighterCapacity = 8,
                RegimentCapacity = 4,
            };
        }

        /// <summary>
        /// Verifies add child with capital ship adds capital ship.
        /// </summary>
        [Test]
        public void AddChild_WithCapitalShip_AddsCapitalShip()
        {
            _fleet.AddChild(_capitalShip1);

            Assert.Contains(_capitalShip1, _fleet.GetChildren<CapitalShip>().ToList());
        }

        /// <summary>
        /// Verifies add child with invalid owner throws exception.
        /// </summary>
        [Test]
        public void AddChild_WithInvalidOwner_ThrowsException()
        {
            CapitalShip invalidShip = new CapitalShip { OwnerInstanceID = "INVALID" };

            Assert.Throws<SceneAccessException>(() => _fleet.AddChild(invalidShip));
        }

        /// <summary>
        /// Verifies add child with officer throws scene access exception.
        /// </summary>
        [Test]
        public void AddChild_WithOfficer_ThrowsSceneAccessException()
        {
            Officer officer = new Officer { OwnerInstanceID = "FACTION1" };

            _fleet.AddChild(_capitalShip1);

            Assert.Throws<SceneAccessException>(() => _fleet.AddChild(officer));
        }

        /// <summary>
        /// Verifies add child with starfighter throws scene access exception.
        /// </summary>
        [Test]
        public void AddChild_WithStarfighter_ThrowsSceneAccessException()
        {
            Starfighter sf = new Starfighter { OwnerInstanceID = "FACTION1" };

            _fleet.AddChild(_capitalShip1);

            Assert.Throws<SceneAccessException>(() => _fleet.AddChild(sf));
        }

        /// <summary>
        /// Verifies add child with regiment throws scene access exception.
        /// </summary>
        [Test]
        public void AddChild_WithRegiment_ThrowsSceneAccessException()
        {
            Regiment reg = new Regiment { OwnerInstanceID = "FACTION1" };

            _fleet.AddChild(_capitalShip1);

            Assert.Throws<SceneAccessException>(() => _fleet.AddChild(reg));
        }

        /// <summary>
        /// Verifies remove child existing capital ship removes it.
        /// </summary>
        [Test]
        public void RemoveChild_ExistingCapitalShip_RemovesIt()
        {
            _fleet.AddChild(_capitalShip1);

            _fleet.RemoveChild(_capitalShip1);

            Assert.IsFalse(_fleet.GetChildren<CapitalShip>().Contains(_capitalShip1));
        }

        /// <summary>
        /// Verifies get children fleet with capital ships returns all capital ships.
        /// </summary>
        [Test]
        public void GetChildren_FleetWithCapitalShips_ReturnsAllCapitalShips()
        {
            _fleet.AddChild(_capitalShip1);
            _fleet.AddChild(_capitalShip2);

            IEnumerable<ISceneNode> children = _fleet.GetChildren();

            CollectionAssert.AreEquivalent(
                new ISceneNode[] { _capitalShip1, _capitalShip2 },
                children,
                "Fleet should return correct children."
            );
        }

        /// <summary>
        /// Verifies get starfighter capacity multiple capital ships returns total sum.
        /// </summary>
        [Test]
        public void GetStarfighterCapacity_MultipleCapitalShips_ReturnsTotalSum()
        {
            _fleet.AddChild(_capitalShip1);
            _fleet.AddChild(_capitalShip2);

            int capacity = _fleet.GetStarfighterCapacity();

            Assert.AreEqual(
                13,
                capacity,
                "Should return sum of all capital ship starfighter capacities"
            );
        }

        /// <summary>
        /// Verifies get regiment capacity multiple capital ships returns total sum.
        /// </summary>
        [Test]
        public void GetRegimentCapacity_MultipleCapitalShips_ReturnsTotalSum()
        {
            _fleet.AddChild(_capitalShip1);
            _fleet.AddChild(_capitalShip2);

            int capacity = _fleet.GetRegimentCapacity();

            Assert.AreEqual(
                7,
                capacity,
                "Should return sum of all capital ship regiment capacities"
            );
        }

        /// <summary>
        /// Verifies get current starfighter count multiple capital ships returns total sum.
        /// </summary>
        [Test]
        public void GetCurrentStarfighterCount_MultipleCapitalShips_ReturnsTotalSum()
        {
            Starfighter starfighter1 = new Starfighter();
            Starfighter starfighter2 = new Starfighter();

            _fleet.AddChild(_capitalShip1);
            _capitalShip1.AddStarfighter(starfighter1);
            _capitalShip1.AddStarfighter(starfighter2);

            int count = _fleet.GetCurrentStarfighterCount();

            Assert.AreEqual(2, count, "Should return total starfighters across all capital ships");
        }

        /// <summary>
        /// Verifies get excess starfighter capacity partially filled fleet returns remaining capacity.
        /// </summary>
        [Test]
        public void GetExcessStarfighterCapacity_PartiallyFilledFleet_ReturnsRemainingCapacity()
        {
            Starfighter starfighter = new Starfighter();

            _fleet.AddChild(_capitalShip1);
            _capitalShip1.AddStarfighter(starfighter);

            int excess = _fleet.GetExcessStarfighterCapacity();

            Assert.AreEqual(4, excess, "Should return excess capacity (5 - 1 = 4)");
        }

        /// <summary>
        /// Verifies get current regiment count multiple capital ships returns total sum.
        /// </summary>
        [Test]
        public void GetCurrentRegimentCount_MultipleCapitalShips_ReturnsTotalSum()
        {
            Regiment regiment1 = new Regiment();
            Regiment regiment2 = new Regiment();

            _fleet.AddChild(_capitalShip1);
            _capitalShip1.AddRegiment(regiment1);
            _capitalShip1.AddRegiment(regiment2);

            int count = _fleet.GetCurrentRegimentCount();

            Assert.AreEqual(2, count, "Should return total regiments across all capital ships");
        }

        /// <summary>
        /// Verifies get excess regiment capacity partially filled fleet returns remaining capacity.
        /// </summary>
        [Test]
        public void GetExcessRegimentCapacity_PartiallyFilledFleet_ReturnsRemainingCapacity()
        {
            Regiment regiment = new Regiment();

            _fleet.AddChild(_capitalShip1);
            _capitalShip1.AddRegiment(regiment);

            int excess = _fleet.GetExcessRegimentCapacity();

            Assert.AreEqual(2, excess, "Should return excess capacity (3 - 1 = 2)");
        }

        /// <summary>
        /// Verifies find ship for starfighter skips unavailable ships.
        /// </summary>
        [Test]
        public void FindShipForStarfighter_SkipsUnavailableShips()
        {
            _capitalShip1.ManufacturingStatus = ManufacturingStatus.Building;
            _capitalShip2.ManufacturingStatus = ManufacturingStatus.Complete;
            _fleet.AddChild(_capitalShip1);
            _fleet.AddChild(_capitalShip2);

            CapitalShip result = _fleet.FindShipForStarfighter();

            Assert.AreSame(_capitalShip2, result);
        }

        /// <summary>
        /// Verifies find ship for starfighter fleet in transit returns null.
        /// </summary>
        [Test]
        public void FindShipForStarfighter_FleetInTransit_ReturnsNull()
        {
            _capitalShip1.ManufacturingStatus = ManufacturingStatus.Complete;
            _fleet.AddChild(_capitalShip1);
            _fleet.Movement = new MovementState();

            CapitalShip result = _fleet.FindShipForStarfighter();

            Assert.IsNull(result);
        }

        /// <summary>
        /// Verifies find ship for regiment skips unavailable ships.
        /// </summary>
        [Test]
        public void FindShipForRegiment_SkipsUnavailableShips()
        {
            _capitalShip1.ManufacturingStatus = ManufacturingStatus.Complete;
            _capitalShip1.Movement = new MovementState();
            _capitalShip2.ManufacturingStatus = ManufacturingStatus.Complete;
            _fleet.AddChild(_capitalShip1);
            _fleet.AddChild(_capitalShip2);

            CapitalShip result = _fleet.FindShipForRegiment();

            Assert.AreSame(_capitalShip2, result);
        }

        /// <summary>
        /// Verifies is movable when idle returns true.
        /// </summary>
        [Test]
        public void IsMovable_WhenIdle_ReturnsTrue()
        {
            _fleet.Movement = null;

            bool isMovable = _fleet.IsMovable();

            Assert.IsTrue(isMovable, "Fleet should be movable when idle");
        }

        /// <summary>
        /// Verifies is movable when in transit returns false.
        /// </summary>
        [Test]
        public void IsMovable_WhenInTransit_ReturnsFalse()
        {
            _fleet.Movement = new MovementState();

            bool isMovable = _fleet.IsMovable();

            Assert.IsFalse(isMovable, "Fleet should not be movable when in transit");
        }

        /// <summary>
        /// Verifies serialize and deserialize fleet with capital ship maintains state.
        /// </summary>
        [Test]
        public void SerializeAndDeserialize_FleetWithCapitalShip_MaintainsState()
        {
            _fleet.AddChild(_capitalShip1);
            _fleet.Waypoints.Add("PLANET1");
            _fleet.Waypoints.Add("PLANET2");
            string serialized = SerializationHelper.Serialize(_fleet);
            Fleet deserialized = SerializationHelper.Deserialize<Fleet>(serialized);

            Assert.AreEqual(
                _fleet.InstanceID,
                deserialized.InstanceID,
                "InstanceID should be correctly deserialized."
            );
            Assert.AreEqual(
                _fleet.OwnerInstanceID,
                deserialized.OwnerInstanceID,
                "OwnerInstanceID should be correctly deserialized."
            );
            Assert.AreEqual(
                _fleet.Movement,
                deserialized.Movement,
                "MovementStatus should be correctly deserialized."
            );
            Assert.AreEqual(
                _fleet.GetPosition().X,
                deserialized.GetPosition().X,
                "PositionX should be correctly deserialized."
            );
            Assert.AreEqual(
                _fleet.GetPosition().Y,
                deserialized.GetPosition().Y,
                "PositionY should be correctly deserialized."
            );
            Assert.AreEqual(
                _fleet.GetChildren<CapitalShip>().Count,
                deserialized.GetChildren<CapitalShip>().Count,
                "CapitalShips count should be correctly deserialized."
            );
            CollectionAssert.AreEqual(_fleet.Waypoints, deserialized.Waypoints);
        }

        /// <summary>
        /// Verifies create copy fleet with waypoints copies independent route.
        /// </summary>
        [Test]
        public void CreateCopy_FleetWithWaypoints_CopiesIndependentRoute()
        {
            _fleet.Waypoints.Add("PLANET1");

            Fleet copy = (Fleet)_fleet.CreateCopy();
            copy.Waypoints.Add("PLANET2");

            CollectionAssert.AreEqual(new[] { "PLANET1" }, _fleet.Waypoints);
            CollectionAssert.AreEqual(new[] { "PLANET1", "PLANET2" }, copy.Waypoints);
        }

        /// <summary>
        /// Verifies has waypoints waypoint added returns true.
        /// </summary>
        [Test]
        public void HasWaypoints_WaypointAdded_ReturnsTrue()
        {
            _fleet.Waypoints.Add("PLANET1");

            bool hasWaypoints = _fleet.HasWaypoints();

            Assert.IsTrue(hasWaypoints);
        }

        /// <summary>
        /// Verifies has waypoints no waypoints returns false.
        /// </summary>
        [Test]
        public void HasWaypoints_NoWaypoints_ReturnsFalse()
        {
            bool hasWaypoints = _fleet.HasWaypoints();

            Assert.IsFalse(hasWaypoints);
        }

        /// <summary>
        /// Verifies set combat state entering combat sets combat state and clears route.
        /// </summary>
        [Test]
        public void SetCombatState_EnteringCombat_SetsCombatStateAndClearsRoute()
        {
            _fleet.Waypoints.Add("PLANET1");

            _fleet.SetCombatState(true);

            Assert.IsTrue(_fleet.IsInCombat);
            Assert.IsEmpty(_fleet.Waypoints);
        }

        /// <summary>
        /// Verifies set combat state leaving combat clears combat state.
        /// </summary>
        [Test]
        public void SetCombatState_LeavingCombat_ClearsCombatState()
        {
            _fleet.SetCombatState(true);

            _fleet.SetCombatState(false);

            Assert.IsFalse(_fleet.IsInCombat);
        }

        /// <summary>
        /// Verifies get starfighters fleet with starfighters returns all starfighters across fleet.
        /// </summary>
        [Test]
        public void GetStarfighters_FleetWithStarfighters_ReturnsAllStarfightersAcrossFleet()
        {
            Starfighter starfighter1 = new Starfighter();
            Starfighter starfighter2 = new Starfighter();
            Starfighter starfighter3 = new Starfighter();

            _fleet.AddChild(_capitalShip1);
            _fleet.AddChild(_capitalShip2);
            _capitalShip1.AddStarfighter(starfighter1);
            _capitalShip1.AddStarfighter(starfighter2);
            _capitalShip2.AddStarfighter(starfighter3);

            IEnumerable<Starfighter> starfighters = _fleet.GetStarfighters();

            CollectionAssert.AreEquivalent(
                new Starfighter[] { starfighter1, starfighter2, starfighter3 },
                starfighters,
                "Should return all starfighters from all capital ships"
            );
        }

        /// <summary>
        /// Verifies get starfighters when no starfighters returns empty.
        /// </summary>
        [Test]
        public void GetStarfighters_WhenNoStarfighters_ReturnsEmpty()
        {
            _fleet.AddChild(_capitalShip1);

            IEnumerable<Starfighter> starfighters = _fleet.GetStarfighters();

            Assert.IsEmpty(starfighters, "Should return empty collection when no starfighters");
        }

        /// <summary>
        /// Verifies get regiments fleet with regiments returns all regiments across fleet.
        /// </summary>
        [Test]
        public void GetRegiments_FleetWithRegiments_ReturnsAllRegimentsAcrossFleet()
        {
            Regiment regiment1 = new Regiment();
            Regiment regiment2 = new Regiment();
            Regiment regiment3 = new Regiment();

            _fleet.AddChild(_capitalShip1);
            _fleet.AddChild(_capitalShip2);
            _capitalShip1.AddRegiment(regiment1);
            _capitalShip1.AddRegiment(regiment2);
            _capitalShip2.AddRegiment(regiment3);

            IEnumerable<Regiment> regiments = _fleet.GetRegiments();

            CollectionAssert.AreEquivalent(
                new Regiment[] { regiment1, regiment2, regiment3 },
                regiments,
                "Should return all regiments from all capital ships"
            );
        }

        /// <summary>
        /// Verifies get regiments when no regiments returns empty.
        /// </summary>
        [Test]
        public void GetRegiments_WhenNoRegiments_ReturnsEmpty()
        {
            _fleet.AddChild(_capitalShip1);

            IEnumerable<Regiment> regiments = _fleet.GetRegiments();

            Assert.IsEmpty(regiments, "Should return empty collection when no regiments");
        }

        /// <summary>
        /// Verifies get special forces fleet with special forces returns all special forces across fleet.
        /// </summary>
        [Test]
        public void GetSpecialForces_FleetWithSpecialForces_ReturnsAllSpecialForcesAcrossFleet()
        {
            SpecialForces specialForces1 = new SpecialForces { OwnerInstanceID = "FACTION1" };
            SpecialForces specialForces2 = new SpecialForces { OwnerInstanceID = "FACTION1" };
            SpecialForces specialForces3 = new SpecialForces { OwnerInstanceID = "FACTION1" };

            _fleet.AddChild(_capitalShip1);
            _fleet.AddChild(_capitalShip2);
            _capitalShip1.AddSpecialForces(specialForces1);
            _capitalShip1.AddSpecialForces(specialForces2);
            _capitalShip2.AddSpecialForces(specialForces3);

            IEnumerable<SpecialForces> specialForces = _fleet.GetSpecialForces();

            CollectionAssert.AreEquivalent(
                new SpecialForces[] { specialForces1, specialForces2, specialForces3 },
                specialForces,
                "Should return all special forces from all capital ships"
            );
        }

        /// <summary>
        /// Verifies get special forces when no special forces returns empty.
        /// </summary>
        [Test]
        public void GetSpecialForces_WhenNoSpecialForces_ReturnsEmpty()
        {
            _fleet.AddChild(_capitalShip1);

            IEnumerable<SpecialForces> specialForces = _fleet.GetSpecialForces();

            Assert.IsEmpty(specialForces, "Should return empty collection when no special forces");
        }

        /// <summary>
        /// Verifies get officers fleet with officers returns all officers across fleet.
        /// </summary>
        [Test]
        public void GetOfficers_FleetWithOfficers_ReturnsAllOfficersAcrossFleet()
        {
            Officer officer1 = new Officer { OwnerInstanceID = "FACTION1" };
            Officer officer2 = new Officer { OwnerInstanceID = "FACTION1" };
            Officer officer3 = new Officer { OwnerInstanceID = "FACTION1" };

            _fleet.AddChild(_capitalShip1);
            _fleet.AddChild(_capitalShip2);
            _capitalShip1.AddOfficer(officer1);
            _capitalShip1.AddOfficer(officer2);
            _capitalShip2.AddOfficer(officer3);

            IEnumerable<Officer> officers = _fleet.GetOfficers();

            CollectionAssert.AreEquivalent(
                new Officer[] { officer1, officer2, officer3 },
                officers,
                "Should return all officers from all capital ships"
            );
        }

        /// <summary>
        /// Verifies get officers when no officers returns empty.
        /// </summary>
        [Test]
        public void GetOfficers_WhenNoOfficers_ReturnsEmpty()
        {
            _fleet.AddChild(_capitalShip1);

            IEnumerable<Officer> officers = _fleet.GetOfficers();

            Assert.IsEmpty(officers, "Should return empty collection when no officers");
        }

        /// <summary>
        /// Verifies get assault strength general commander with leadership applies personnel modifier.
        /// </summary>
        [Test]
        public void GetAssaultStrength_GeneralCommanderWithLeadership_AppliesPersonnelModifier()
        {
            Fleet fleet = new Fleet { InstanceID = "F1", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "CS1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100 };
            fleet.AddChild(ship);

            Officer general = new Officer
            {
                InstanceID = "O1",
                OwnerInstanceID = "empire",
                CurrentRank = OfficerRank.General,
            };
            general.SetBaseRating(OfficerRating.Leadership, 50);
            ship.AddChild(general);

            // (50 / 10 + 1) * 100 = 6 * 100 = 600
            Assert.AreEqual(600, fleet.GetAssaultStrength(10));
        }

        /// <summary>
        /// Verifies get assault strength admiral commander only uses base multiplier.
        /// </summary>
        [Test]
        public void GetAssaultStrength_AdmiralCommanderOnly_UsesBaseMultiplier()
        {
            Fleet fleet = new Fleet { InstanceID = "F1", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "CS1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100 };
            fleet.AddChild(ship);

            Officer admiral = new Officer
            {
                InstanceID = "O1",
                OwnerInstanceID = "empire",
                CurrentRank = OfficerRank.Admiral,
            };
            admiral.SetBaseRating(OfficerRating.Leadership, 50);
            ship.AddChild(admiral);

            // Admiral's Leadership does not count — only Generals contribute assault personnel.
            // (0 / 10 + 1) * 100 = 1 * 100 = 100
            Assert.AreEqual(100, fleet.GetAssaultStrength(10));
        }

        /// <summary>
        /// Verifies get assault strength no commander uses base multiplier.
        /// </summary>
        [Test]
        public void GetAssaultStrength_NoCommander_UsesBaseMultiplier()
        {
            Fleet fleet = new Fleet { InstanceID = "F1", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "CS1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100 };
            fleet.AddChild(ship);

            // (0 / 10 + 1) * 100 = 100
            Assert.AreEqual(100, fleet.GetAssaultStrength(10));
        }
    }
} // namespace Rebellion.Tests.Game.Units
