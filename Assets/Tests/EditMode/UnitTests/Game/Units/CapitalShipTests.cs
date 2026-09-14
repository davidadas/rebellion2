using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public class CapitalShipTests
    {
        private CapitalShip _capitalShip;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            _capitalShip = new CapitalShip
            {
                StarfighterCapacity = 2,
                RegimentCapacity = 3,
                OwnerInstanceID = "FNALL1",
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                DamageControl = 10,
                MaxShieldStrength = 50,
                ShieldRechargeRate = 5,
                Hyperdrive = 2,
                SublightSpeed = 15,
                Maneuverability = 8,
                WeaponRecharge = 12,
                Bombardment = 20,
                TractorBeamPower = 7,
                TractorBeamnRange = 3,
                HasGravityWell = false,
                DetectionRating = 25,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        /// <summary>
        /// Verifies assign name valid name replaces display name and marks assigned.
        /// </summary>
        [Test]
        public void AssignName_ValidName_ReplacesDisplayNameAndMarksAssigned()
        {
            _capitalShip.DisplayName = "Generic Ship";

            _capitalShip.AssignName("Assigned Ship");

            Assert.AreEqual("Assigned Ship", _capitalShip.DisplayName);
            Assert.IsTrue(_capitalShip.HasAssignedName);
        }

        /// <summary>
        /// Verifies assign name whitespace name throws argument exception.
        /// </summary>
        [Test]
        public void AssignName_WhitespaceName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _capitalShip.AssignName(" "));
            Assert.IsFalse(_capitalShip.HasAssignedName);
        }

        /// <summary>
        /// Verifies add starfighter within capacity adds starfighter.
        /// </summary>
        [Test]
        public void AddStarfighter_WithinCapacity_AddsStarfighter()
        {
            Starfighter starfighter = new Starfighter();

            _capitalShip.AddStarfighter(starfighter);

            Assert.Contains(starfighter, _capitalShip.GetChildren<Starfighter>().ToList());
        }

        /// <summary>
        /// Verifies add starfighter exceeds capacity throws exception.
        /// </summary>
        [Test]
        public void AddStarfighter_ExceedsCapacity_ThrowsException()
        {
            _capitalShip.AddStarfighter(new Starfighter());
            _capitalShip.AddStarfighter(new Starfighter());

            Assert.Throws<InvalidOperationException>(() =>
                _capitalShip.AddStarfighter(new Starfighter())
            );
        }

        /// <summary>
        /// Verifies add regiment within capacity adds regiment.
        /// </summary>
        [Test]
        public void AddRegiment_WithinCapacity_AddsRegiment()
        {
            Regiment regiment = new Regiment();

            _capitalShip.AddRegiment(regiment);

            Assert.Contains(regiment, _capitalShip.GetChildren<Regiment>().ToList());
        }

        /// <summary>
        /// Verifies add regiment exceeds capacity throws exception.
        /// </summary>
        [Test]
        public void AddRegiment_ExceedsCapacity_ThrowsException()
        {
            _capitalShip.AddRegiment(new Regiment());
            _capitalShip.AddRegiment(new Regiment());
            _capitalShip.AddRegiment(new Regiment());

            Assert.Throws<InvalidOperationException>(() =>
                _capitalShip.AddRegiment(new Regiment())
            );
        }

        /// <summary>
        /// Verifies add officer valid owner adds officer.
        /// </summary>
        [Test]
        public void AddOfficer_ValidOwner_AddsOfficer()
        {
            Officer officer = new Officer { OwnerInstanceID = "FNALL1" };

            _capitalShip.AddOfficer(officer);

            Assert.Contains(officer, _capitalShip.GetChildren<Officer>().ToList());
        }

        /// <summary>
        /// Verifies add officer invalid owner throws exception.
        /// </summary>
        [Test]
        public void AddOfficer_InvalidOwner_ThrowsException()
        {
            Officer officer = new Officer { OwnerInstanceID = "INVALID", IsCaptured = false };

            Assert.Throws<SceneAccessException>(() => _capitalShip.AddOfficer(officer));
        }

        /// <summary>
        /// Verifies add officer captured enemy adds officer.
        /// </summary>
        [Test]
        public void AddOfficer_CapturedEnemy_AddsOfficer()
        {
            Officer officer = new Officer { OwnerInstanceID = "INVALID", IsCaptured = true };

            _capitalShip.AddOfficer(officer);

            Assert.Contains(officer, _capitalShip.GetChildren<Officer>().ToList());
        }

        /// <summary>
        /// Verifies add special forces valid owner adds special forces.
        /// </summary>
        [Test]
        public void AddSpecialForces_ValidOwner_AddsSpecialForces()
        {
            SpecialForces specialForces = new SpecialForces { OwnerInstanceID = "FNALL1" };

            _capitalShip.AddSpecialForces(specialForces);

            Assert.Contains(specialForces, _capitalShip.GetChildren<SpecialForces>().ToList());
        }

        /// <summary>
        /// Verifies add special forces invalid owner throws exception.
        /// </summary>
        [Test]
        public void AddSpecialForces_InvalidOwner_ThrowsException()
        {
            SpecialForces specialForces = new SpecialForces { OwnerInstanceID = "INVALID" };

            Assert.Throws<SceneAccessException>(() => _capitalShip.AddSpecialForces(specialForces));
        }

        /// <summary>
        /// Verifies can accept child captured enemy officer returns true.
        /// </summary>
        [Test]
        public void CanAcceptChild_CapturedEnemyOfficer_ReturnsTrue()
        {
            Officer officer = new Officer { OwnerInstanceID = "INVALID", IsCaptured = true };

            Assert.IsTrue(_capitalShip.CanAcceptChild(officer));
        }

        /// <summary>
        /// Verifies can accept child uncaptured enemy officer returns false.
        /// </summary>
        [Test]
        public void CanAcceptChild_UncapturedEnemyOfficer_ReturnsFalse()
        {
            Officer officer = new Officer { OwnerInstanceID = "INVALID", IsCaptured = false };

            Assert.IsFalse(_capitalShip.CanAcceptChild(officer));
        }

        /// <summary>
        /// Verifies can accept child friendly special forces returns true.
        /// </summary>
        [Test]
        public void CanAcceptChild_FriendlySpecialForces_ReturnsTrue()
        {
            SpecialForces specialForces = new SpecialForces { OwnerInstanceID = "FNALL1" };

            Assert.IsTrue(_capitalShip.CanAcceptChild(specialForces));
        }

        /// <summary>
        /// Verifies can accept child enemy special forces returns false.
        /// </summary>
        [Test]
        public void CanAcceptChild_EnemySpecialForces_ReturnsFalse()
        {
            SpecialForces specialForces = new SpecialForces { OwnerInstanceID = "INVALID" };

            Assert.IsFalse(_capitalShip.CanAcceptChild(specialForces));
        }

        /// <summary>
        /// Verifies can accept child ship under construction returns false.
        /// </summary>
        [Test]
        public void CanAcceptChild_ShipUnderConstruction_ReturnsFalse()
        {
            _capitalShip.ManufacturingStatus = ManufacturingStatus.Building;

            Assert.IsFalse(_capitalShip.CanAcceptChild(new Officer { OwnerInstanceID = "FNALL1" }));
        }

        /// <summary>
        /// Verifies remove starfighter existing starfighter removes it from fleet.
        /// </summary>
        [Test]
        public void RemoveStarfighter_ExistingStarfighter_RemovesItFromFleet()
        {
            Starfighter starfighter = new Starfighter();
            _capitalShip.AddStarfighter(starfighter);

            _capitalShip.RemoveChild(starfighter);

            Assert.IsFalse(_capitalShip.GetChildren<Starfighter>().Contains(starfighter));
        }

        /// <summary>
        /// Verifies remove regiment existing regiment removes it from fleet.
        /// </summary>
        [Test]
        public void RemoveRegiment_ExistingRegiment_RemovesItFromFleet()
        {
            Regiment regiment = new Regiment();
            _capitalShip.AddRegiment(regiment);

            _capitalShip.RemoveChild(regiment);

            Assert.IsFalse(_capitalShip.GetChildren<Regiment>().Contains(regiment));
        }

        /// <summary>
        /// Verifies remove officer existing officer removes it from fleet.
        /// </summary>
        [Test]
        public void RemoveOfficer_ExistingOfficer_RemovesItFromFleet()
        {
            Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
            _capitalShip.AddOfficer(officer);

            _capitalShip.RemoveChild(officer);

            Assert.IsFalse(_capitalShip.GetChildren<Officer>().Contains(officer));
        }

        /// <summary>
        /// Verifies remove special forces existing special forces removes it from fleet.
        /// </summary>
        [Test]
        public void RemoveSpecialForces_ExistingSpecialForces_RemovesItFromFleet()
        {
            SpecialForces specialForces = new SpecialForces { OwnerInstanceID = "FNALL1" };
            _capitalShip.AddSpecialForces(specialForces);

            _capitalShip.RemoveChild(specialForces);

            Assert.IsFalse(_capitalShip.GetChildren<SpecialForces>().Contains(specialForces));
        }

        /// <summary>
        /// Verifies get children fleet with children returns all child nodes.
        /// </summary>
        [Test]
        public void GetChildren_FleetWithChildren_ReturnsAllChildNodes()
        {
            Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
            Starfighter starfighter = new Starfighter();
            Regiment regiment = new Regiment();
            SpecialForces specialForces = new SpecialForces { OwnerInstanceID = "FNALL1" };

            _capitalShip.AddOfficer(officer);
            _capitalShip.AddStarfighter(starfighter);
            _capitalShip.AddRegiment(regiment);
            _capitalShip.AddSpecialForces(specialForces);

            IEnumerable<ISceneNode> children = _capitalShip.GetChildren();

            CollectionAssert.AreEquivalent(
                new ISceneNode[] { officer, starfighter, regiment, specialForces },
                children,
                "CapitalShip should return correct children."
            );
        }

        /// <summary>
        /// Verifies add child valid starfighter adds to fleet.
        /// </summary>
        [Test]
        public void AddChild_ValidStarfighter_AddsToFleet()
        {
            Starfighter starfighter = new Starfighter();

            _capitalShip.AddChild(starfighter);

            Assert.Contains(starfighter, _capitalShip.GetChildren<Starfighter>().ToList());
        }

        /// <summary>
        /// Verifies add child valid regiment adds to fleet.
        /// </summary>
        [Test]
        public void AddChild_ValidRegiment_AddsToFleet()
        {
            Regiment regiment = new Regiment();

            _capitalShip.AddChild(regiment);

            Assert.Contains(regiment, _capitalShip.GetChildren<Regiment>().ToList());
        }

        /// <summary>
        /// Verifies add child valid officer adds to fleet.
        /// </summary>
        [Test]
        public void AddChild_ValidOfficer_AddsToFleet()
        {
            Officer officer = new Officer { OwnerInstanceID = "FNALL1" };

            _capitalShip.AddChild(officer);

            Assert.Contains(officer, _capitalShip.GetChildren<Officer>().ToList());
        }

        /// <summary>
        /// Verifies add child valid special forces adds to fleet.
        /// </summary>
        [Test]
        public void AddChild_ValidSpecialForces_AddsToFleet()
        {
            SpecialForces specialForces = new SpecialForces { OwnerInstanceID = "FNALL1" };

            _capitalShip.AddChild(specialForces);

            Assert.Contains(specialForces, _capitalShip.GetChildren<SpecialForces>().ToList());
        }

        /// <summary>
        /// Verifies add child invalid owner throws exception.
        /// </summary>
        [Test]
        public void AddChild_InvalidOwner_ThrowsException()
        {
            Officer officer = new Officer { OwnerInstanceID = "INVALID" };

            Assert.Throws<SceneAccessException>(() => _capitalShip.AddChild(officer));
        }

        /// <summary>
        /// Verifies remove child existing starfighter removes it.
        /// </summary>
        [Test]
        public void RemoveChild_ExistingStarfighter_RemovesIt()
        {
            Starfighter starfighter = new Starfighter();
            _capitalShip.AddChild(starfighter);

            _capitalShip.RemoveChild(starfighter);

            Assert.IsFalse(_capitalShip.GetChildren<Starfighter>().Contains(starfighter));
        }

        /// <summary>
        /// Verifies remove child existing regiment removes it.
        /// </summary>
        [Test]
        public void RemoveChild_ExistingRegiment_RemovesIt()
        {
            Regiment regiment = new Regiment();
            _capitalShip.AddChild(regiment);

            _capitalShip.RemoveChild(regiment);

            Assert.IsFalse(_capitalShip.GetChildren<Regiment>().Contains(regiment));
        }

        /// <summary>
        /// Verifies remove child existing officer removes it.
        /// </summary>
        [Test]
        public void RemoveChild_ExistingOfficer_RemovesIt()
        {
            Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
            _capitalShip.AddChild(officer);

            _capitalShip.RemoveChild(officer);

            Assert.IsFalse(_capitalShip.GetChildren<Officer>().Contains(officer));
        }

        /// <summary>
        /// Verifies remove child existing special forces removes it.
        /// </summary>
        [Test]
        public void RemoveChild_ExistingSpecialForces_RemovesIt()
        {
            SpecialForces specialForces = new SpecialForces { OwnerInstanceID = "FNALL1" };
            _capitalShip.AddChild(specialForces);

            _capitalShip.RemoveChild(specialForces);

            Assert.IsFalse(_capitalShip.GetChildren<SpecialForces>().Contains(specialForces));
        }

        /// <summary>
        /// Verifies serialize and deserialize capital ship with children maintains state.
        /// </summary>
        [Test]
        public void SerializeAndDeserialize_CapitalShipWithChildren_MaintainsState()
        {
            _capitalShip.ManufacturingQueueSequence = 7;
            _capitalShip.ShipNamePoolID = "POOL";
            _capitalShip.AssignName("Named Ship");
            Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
            Starfighter starfighter = new Starfighter();
            Regiment regiment = new Regiment();
            SpecialForces specialForces = new SpecialForces { OwnerInstanceID = "FNALL1" };

            _capitalShip.AddOfficer(officer);
            _capitalShip.AddStarfighter(starfighter);
            _capitalShip.AddRegiment(regiment);
            _capitalShip.AddSpecialForces(specialForces);

            string serialized = SerializationHelper.Serialize(_capitalShip);
            CapitalShip deserialized = SerializationHelper.Deserialize<CapitalShip>(serialized);

            Assert.AreEqual(
                _capitalShip.StarfighterCapacity,
                deserialized.StarfighterCapacity,
                "StarfighterCapacity should be correctly deserialized."
            );
            Assert.AreEqual(
                _capitalShip.RegimentCapacity,
                deserialized.RegimentCapacity,
                "RegimentCapacity should be correctly deserialized."
            );
            Assert.AreEqual(
                _capitalShip.OwnerInstanceID,
                deserialized.OwnerInstanceID,
                "OwnerInstanceID should be correctly deserialized."
            );
            Assert.AreEqual(
                _capitalShip.ManufacturingQueueSequence,
                deserialized.ManufacturingQueueSequence,
                "ManufacturingQueueSequence should be correctly deserialized."
            );
            Assert.AreEqual("POOL", deserialized.ShipNamePoolID);
            Assert.AreEqual("Named Ship", deserialized.DisplayName);
            Assert.IsTrue(deserialized.HasAssignedName);
            Assert.AreEqual(
                _capitalShip.GetChildren<Officer>().Count,
                deserialized.GetChildren<Officer>().Count,
                "Officers should be correctly deserialized."
            );
            Assert.AreEqual(
                _capitalShip.GetChildren<SpecialForces>().Count,
                deserialized.GetChildren<SpecialForces>().Count,
                "SpecialForces should be correctly deserialized."
            );
            Assert.AreEqual(
                _capitalShip.GetChildren<Starfighter>().Count,
                deserialized.GetChildren<Starfighter>().Count,
                "Starfighters should be correctly deserialized."
            );
            Assert.AreEqual(
                _capitalShip.GetChildren<Regiment>().Count,
                deserialized.GetChildren<Regiment>().Count,
                "Regiments should be correctly deserialized."
            );
            Assert.AreEqual(
                _capitalShip.GetChildren<SpecialForces>().Count,
                deserialized.GetChildren<SpecialForces>().Count,
                "SpecialForces should be correctly deserialized."
            );
        }

        /// <summary>
        /// Verifies set manufacturing status building to complete updates successfully.
        /// </summary>
        [Test]
        public void SetManufacturingStatus_BuildingToComplete_UpdatesSuccessfully()
        {
            _capitalShip.ManufacturingStatus = ManufacturingStatus.Building;

            ((IManufacturable)_capitalShip).SetManufacturingStatus(ManufacturingStatus.Complete);

            Assert.AreEqual(ManufacturingStatus.Complete, _capitalShip.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies set manufacturing status complete to building throws exception.
        /// </summary>
        [Test]
        public void SetManufacturingStatus_CompleteToBuilding_ThrowsException()
        {
            _capitalShip.ManufacturingStatus = ManufacturingStatus.Complete;

            Assert.Throws<InvalidOperationException>(() =>
                ((IManufacturable)_capitalShip).SetManufacturingStatus(ManufacturingStatus.Building)
            );
        }

        /// <summary>
        /// Verifies get starfighter capacity default capital ship returns expected value.
        /// </summary>
        [Test]
        public void GetStarfighterCapacity_DefaultCapitalShip_ReturnsExpectedValue()
        {
            int capacity = _capitalShip.GetStarfighterCapacity();

            Assert.AreEqual(2, capacity);
        }

        /// <summary>
        /// Verifies get current starfighter count no starfighters returns zero.
        /// </summary>
        [Test]
        public void GetCurrentStarfighterCount_NoStarfighters_ReturnsZero()
        {
            int count = _capitalShip.GetCurrentStarfighterCount();

            Assert.AreEqual(0, count);
        }

        /// <summary>
        /// Verifies get current starfighter count with starfighters returns correct count.
        /// </summary>
        [Test]
        public void GetCurrentStarfighterCount_WithStarfighters_ReturnsCorrectCount()
        {
            _capitalShip.AddStarfighter(new Starfighter());
            _capitalShip.AddStarfighter(new Starfighter());

            int count = _capitalShip.GetCurrentStarfighterCount();

            Assert.AreEqual(2, count);
        }

        /// <summary>
        /// Verifies get regiment capacity default capital ship returns expected value.
        /// </summary>
        [Test]
        public void GetRegimentCapacity_DefaultCapitalShip_ReturnsExpectedValue()
        {
            int capacity = _capitalShip.GetRegimentCapacity();

            Assert.AreEqual(3, capacity);
        }

        /// <summary>
        /// Verifies get current regiment count no regiments returns zero.
        /// </summary>
        [Test]
        public void GetCurrentRegimentCount_NoRegiments_ReturnsZero()
        {
            int count = _capitalShip.GetCurrentRegimentCount();

            Assert.AreEqual(0, count);
        }

        /// <summary>
        /// Verifies get current regiment count with regiments returns correct count.
        /// </summary>
        [Test]
        public void GetCurrentRegimentCount_WithRegiments_ReturnsCorrectCount()
        {
            _capitalShip.AddRegiment(new Regiment());
            _capitalShip.AddRegiment(new Regiment());

            int count = _capitalShip.GetCurrentRegimentCount();

            Assert.AreEqual(2, count);
        }

        /// <summary>
        /// Verifies primary weapons default capital ship has correct types.
        /// </summary>
        [Test]
        public void PrimaryWeapons_DefaultCapitalShip_HasCorrectTypes()
        {
            Assert.IsTrue(_capitalShip.PrimaryWeapons.ContainsKey(PrimaryWeaponType.Turbolaser));
            Assert.IsTrue(_capitalShip.PrimaryWeapons.ContainsKey(PrimaryWeaponType.IonCannon));
            Assert.IsTrue(_capitalShip.PrimaryWeapons.ContainsKey(PrimaryWeaponType.LaserCannon));
        }

        /// <summary>
        /// Verifies primary weapons default capital ship has correct array sizes.
        /// </summary>
        [Test]
        public void PrimaryWeapons_DefaultCapitalShip_HasCorrectArraySizes()
        {
            Assert.AreEqual(5, _capitalShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser].Length);
            Assert.AreEqual(5, _capitalShip.PrimaryWeapons[PrimaryWeaponType.IonCannon].Length);
            Assert.AreEqual(5, _capitalShip.PrimaryWeapons[PrimaryWeaponType.LaserCannon].Length);
        }

        /// <summary>
        /// Verifies get primary weapon strength weapon range values ignores range.
        /// </summary>
        [Test]
        public void GetPrimaryWeaponStrength_WeaponRangeValues_IgnoresRange()
        {
            _capitalShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new int[]
            {
                10,
                20,
                30,
                40,
                900,
            };
            _capitalShip.PrimaryWeapons[PrimaryWeaponType.IonCannon] = new int[]
            {
                1,
                2,
                3,
                4,
                900,
            };
            _capitalShip.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new int[]
            {
                5,
                0,
                0,
                0,
                900,
            };

            int strength = _capitalShip.GetPrimaryWeaponStrength();

            Assert.AreEqual(115, strength);
        }

        /// <summary>
        /// Verifies get combat value imperial star destroyer and corvette values durability.
        /// </summary>
        [Test]
        public void GetCombatValue_ImperialStarDestroyerAndCorvette_ValuesDurability()
        {
            CapitalShip starDestroyer = CreateCombatShip(480, 2750, 300);
            CapitalShip corvette = CreateCombatShip(450, 500, 200);

            Assert.AreEqual(1209, starDestroyer.GetCombatValue());
            Assert.AreEqual(561, corvette.GetCombatValue());
            Assert.Greater(starDestroyer.GetCombatValue(), corvette.GetCombatValue() * 2);
        }

        /// <summary>
        /// Verifies get combat value with shield strength increases value.
        /// </summary>
        [Test]
        public void GetCombatValue_WithShieldStrength_IncreasesValue()
        {
            CapitalShip unshielded = CreateCombatShip(100, 100, 0);
            CapitalShip shielded = CreateCombatShip(100, 100, 100);

            Assert.AreEqual(100, unshielded.GetCombatValue());
            Assert.AreEqual(141, shielded.GetCombatValue());
        }

        /// <summary>
        /// Verifies get combat value with hull damage uses remaining durability.
        /// </summary>
        [Test]
        public void GetCombatValue_WithHullDamage_UsesRemainingDurability()
        {
            CapitalShip ship = CreateCombatShip(100, 100, 0);
            ship.CurrentHullStrength = 25;

            Assert.AreEqual(50, ship.GetCombatValue());
        }

        /// <summary>
        /// Verifies get combat value with no remaining hull returns zero.
        /// </summary>
        [Test]
        public void GetCombatValue_WithNoRemainingHull_ReturnsZero()
        {
            CapitalShip ship = CreateCombatShip(100, 100, 100);
            ship.CurrentHullStrength = 0;

            Assert.Zero(ship.GetCombatValue());
            Assert.Zero(ship.GetProjectedCombatValue());
        }

        /// <summary>
        /// Verifies get projected combat value building ship uses maximum durability.
        /// </summary>
        [Test]
        public void GetProjectedCombatValue_BuildingShip_UsesMaximumDurability()
        {
            CapitalShip ship = CreateCombatShip(100, 100, 0);
            ship.ManufacturingStatus = ManufacturingStatus.Building;
            ship.CurrentHullStrength = 0;

            Assert.Zero(ship.GetCombatValue());
            Assert.AreEqual(100, ship.GetProjectedCombatValue());
        }

        /// <summary>
        /// Verifies configured capital ship preserves combat and movement statistics.
        /// </summary>
        [Test]
        public void ConfiguredCapitalShip_PreservesCombatAndMovementStatistics()
        {
            Assert.AreEqual(12, _capitalShip.WeaponRecharge);
            Assert.AreEqual(20, _capitalShip.Bombardment);
            Assert.AreEqual(100, _capitalShip.CurrentHullStrength);
            Assert.AreEqual(10, _capitalShip.DamageControl);
            Assert.AreEqual(50, _capitalShip.MaxShieldStrength);
            Assert.AreEqual(5, _capitalShip.ShieldRechargeRate);
            Assert.AreEqual(2, _capitalShip.Hyperdrive);
            Assert.AreEqual(15, _capitalShip.SublightSpeed);
            Assert.AreEqual(8, _capitalShip.Maneuverability);
            Assert.AreEqual(7, _capitalShip.TractorBeamPower);
            Assert.AreEqual(3, _capitalShip.TractorBeamnRange);
            Assert.IsFalse(_capitalShip.HasGravityWell);
            Assert.AreEqual(25, _capitalShip.DetectionRating);
        }

        /// <summary>
        /// Creates combat ship.
        /// </summary>
        /// <param name="attackStrength">The attack strength.</param>
        /// <param name="hullStrength">The hull strength.</param>
        /// <param name="shieldStrength">The shield strength.</param>
        /// <returns>The created combat ship.</returns>
        private static CapitalShip CreateCombatShip(
            int attackStrength,
            int hullStrength,
            int shieldStrength
        )
        {
            CapitalShip ship = new CapitalShip
            {
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = hullStrength,
                CurrentHullStrength = hullStrength,
                MaxShieldStrength = shieldStrength,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = attackStrength;
            return ship;
        }
    }
} // namespace Rebellion.Tests.Game.Units
