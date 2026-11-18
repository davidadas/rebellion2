using NUnit.Framework;
using Rebellion.Gameplay;
using BitConverter = global::System.BitConverter;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatWeaponsTests
    {
        [Test]
        public void GetOpportunityDamage_ThreeLasers_RetainsLiveScoreBeforeTargetComparison()
        {
            TacticalCombatWeapons weapons = new(
                new int[,]
                {
                    { 3, 0, 0 },
                    { 0, 0, 0 },
                    { 0, 0, 0 },
                    { 0, 0, 0 },
                },
                new float[] { 100, 0, 0 },
                0
            );

            double damage = weapons.GetOpportunityDamage(
                TacticalWeaponArc.Forward,
                TacticalWeaponType.LaserCannon
            );

            Assert.AreEqual(0x3fe99999b0000000L, BitConverter.DoubleToInt64Bits(damage));
        }

        [Test]
        public void Advance_LiveFighterRecharge_RestoresInitialDischargeBeforeReachingCapacity()
        {
            TacticalCombatWeapons weapons = new(
                new int[,]
                {
                    { 10, 2, 0 },
                    { 0, 0, 0 },
                    { 0, 0, 0 },
                    { 0, 0, 0 },
                },
                new float[] { 10, 10, 0 },
                0,
                true
            );
            TacticalCombatSystems systems = new(true, 96, 5, 0, 0, 0, 0, 0, 0, 1, 0);
            weapons.Discharge(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);

            weapons.Advance(1, systems.GetWeaponRecharge());

            Assert.That(
                weapons.GetCharge(TacticalWeaponArc.Forward),
                Is.EqualTo(-10 + systems.GetWeaponRecharge()).Within(0.000001)
            );
            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));

            weapons.Advance(9, systems.GetWeaponRecharge());

            Assert.AreEqual(12, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(12, weapons.GetMaximumCharge(TacticalWeaponArc.Forward));
            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Forward));
        }

        [TestCase(0u)]
        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        public void Constructor_OperationalCapitalBattery_StartsFullyChargedAndReady(uint damage)
        {
            TacticalCombatWeapons weapons = CreateWeapons(damage);

            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(60, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(60, weapons.GetMaximumCharge(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Constructor_DisabledWeapons_StartsDischarged()
        {
            TacticalCombatWeapons weapons = CreateWeapons(4);

            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(0, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(60, weapons.GetMaximumCharge(TacticalWeaponArc.Forward));
        }

        [Test]
        public void GetTargetDamage_IonAgainstFighters_DoesNotFire()
        {
            Assert.AreEqual(
                0,
                CreateWeapons()
                    .GetTargetDamage(TacticalWeaponArc.Forward, TacticalWeaponType.IonCannon, true)
            );
        }

        [Test]
        public void GetTargetDamage_LaserAgainstCapitalShip_UsesCapitalDamageFactor()
        {
            Assert.That(
                CreateWeapons()
                    .GetTargetDamage(
                        TacticalWeaponArc.Forward,
                        TacticalWeaponType.LaserCannon,
                        false
                    ),
                Is.EqualTo(0.44533336f).Within(0.000001f)
            );
        }

        [Test]
        public void Discharge_MultipleLanes_ConsumesTheirCombinedStrength()
        {
            TacticalCombatWeapons weapons = CreateWeapons();
            weapons.Discharge(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);
            weapons.Discharge(TacticalWeaponArc.Forward, TacticalWeaponType.IonCannon);

            Assert.AreEqual(30, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Advance_PartialRecharge_ReadiesOnlyAfterRestoringConsumedCharge()
        {
            TacticalCombatWeapons weapons = CreateWeapons();
            weapons.Discharge(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);
            weapons.Advance(0.5f, 10);

            Assert.AreEqual(55, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));

            weapons.Advance(0.5f, 10);

            Assert.AreEqual(60, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Advance_SurplusBudget_CapsBatteryAtItsFullCapacity()
        {
            TacticalCombatWeapons weapons = CreateWeapons();
            weapons.Discharge(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);

            weapons.Advance(10, 10);

            Assert.AreEqual(60, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Advance_PartialRecharge_RetainsBudgetForSubsequentBatteries()
        {
            TacticalCombatWeapons weapons = CreateDirectionalWeapons();
            weapons.ClearBattery(TacticalWeaponArc.Port);
            weapons.ClearBattery(TacticalWeaponArc.Forward);
            weapons.ClearBattery(TacticalWeaponArc.Starboard);
            weapons.ClearBattery(TacticalWeaponArc.Aft);

            weapons.Advance(1, 45);

            Assert.AreEqual(40, weapons.GetCharge(TacticalWeaponArc.Port));
            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Port));
            Assert.AreEqual(5, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(5, weapons.GetCharge(TacticalWeaponArc.Starboard));
            Assert.AreEqual(5, weapons.GetCharge(TacticalWeaponArc.Aft));

            weapons.Advance(1, 30);

            Assert.AreEqual(10, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(20, weapons.GetCharge(TacticalWeaponArc.Starboard));
            Assert.AreEqual(15, weapons.GetCharge(TacticalWeaponArc.Aft));
            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Aft));
        }

        [Test]
        public void Advance_CompletedAdjacentBatteries_RetainsTheirNextDischargePositions()
        {
            TacticalCombatWeapons weapons = CreateDirectionalWeapons();
            for (int arc = 0; arc < 4; arc++)
                weapons.ClearBattery((TacticalWeaponArc)arc);
            weapons.Advance(1, 100);
            for (int arc = 0; arc < 4; arc++)
                Assert.IsTrue(weapons.IsReady((TacticalWeaponArc)arc));
            weapons.Discharge(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);
            weapons.Discharge(TacticalWeaponArc.Starboard, TacticalWeaponType.LaserCannon);

            weapons.Advance(1, 25);

            Assert.AreEqual(5, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(20, weapons.GetCharge(TacticalWeaponArc.Starboard));
        }

        [Test]
        public void Advance_ZeroBudget_PreservesDischargedState()
        {
            TacticalCombatWeapons weapons = CreateWeapons();
            weapons.Discharge(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);
            weapons.Advance(0, 10);

            Assert.AreEqual(50, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Constructor_EmptyBattery_StartsUnready(bool isFighter)
        {
            TacticalCombatWeapons weapons = new(new int[4, 3], new float[3], 0, isFighter);

            for (int arc = 0; arc < 4; arc++)
            {
                Assert.IsFalse(weapons.IsReady((TacticalWeaponArc)arc));
                Assert.AreEqual(0, weapons.GetCharge((TacticalWeaponArc)arc));
                Assert.AreEqual(0, weapons.GetMaximumCharge((TacticalWeaponArc)arc));
            }
        }

        [Test]
        public void Constructor_FighterBattery_StartsReadyWithoutStoredCharge()
        {
            TacticalCombatWeapons weapons = new(
                new[,]
                {
                    { 3, 1, 0 },
                    { 0, 0, 0 },
                    { 0, 0, 0 },
                    { 0, 0, 0 },
                },
                new float[] { 10, 10, 0 },
                4,
                true
            );

            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(0, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(4, weapons.GetMaximumCharge(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Discharge_InitialFighterBattery_RetainsNegativeChargeUntilRecharged()
        {
            TacticalCombatWeapons weapons = new(
                new[,]
                {
                    { 3, 1, 0 },
                    { 0, 0, 0 },
                    { 0, 0, 0 },
                    { 0, 0, 0 },
                },
                new float[] { 10, 10, 0 },
                0,
                true
            );

            weapons.Discharge(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);

            Assert.AreEqual(-3, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));

            weapons.Advance(1, 2);

            Assert.AreEqual(-1, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));

            weapons.Advance(1, 5);

            Assert.AreEqual(4, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Advance_DisabledCapitalBatteryWithRestoredBudget_RequiresFullRecharge()
        {
            TacticalCombatWeapons weapons = CreateWeapons(4);

            weapons.Advance(1, 1);

            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(1, weapons.GetCharge(TacticalWeaponArc.Forward));

            weapons.Advance(59, 1);

            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(60, weapons.GetCharge(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Advance_DisabledWeaponSystem_LeavesBatteryDischarged()
        {
            TacticalCombatWeapons weapons = CreateWeapons(4);
            TacticalCombatSystems systems = new(
                false,
                100,
                100,
                0,
                10,
                0,
                0,
                0,
                0,
                0,
                0,
                new() { Weapons = 4 }
            );

            weapons.Advance(1000, systems.GetWeaponRecharge());

            Assert.AreEqual(0, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void ClearBattery_PartiallyChargedBattery_RemovesItsRemainingCharge()
        {
            TacticalCombatWeapons weapons = CreateWeapons();
            weapons.Discharge(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);
            weapons.ClearBattery(TacticalWeaponArc.Forward);

            Assert.AreEqual(0, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void ClearBattery_RepeatedDisruption_QueuesEachBatteryOnce()
        {
            TacticalCombatWeapons weapons = CreateDirectionalWeapons();
            weapons.ClearBattery(TacticalWeaponArc.Forward);
            weapons.ClearBattery(TacticalWeaponArc.Forward);
            weapons.ClearBattery(TacticalWeaponArc.Starboard);

            weapons.Advance(1, 15);

            Assert.AreEqual(10, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(5, weapons.GetCharge(TacticalWeaponArc.Starboard));
        }

        /// <summary>
        /// Creates distinct laser capacities for observing ordered recharge across all four arcs.
        /// </summary>
        /// <returns>The configured directional batteries.</returns>
        private static TacticalCombatWeapons CreateDirectionalWeapons() =>
            new(
                new[,]
                {
                    { 10, 0, 0 },
                    { 20, 0, 0 },
                    { 30, 0, 0 },
                    { 40, 0, 0 },
                },
                new float[] { 100, 0, 0 },
                0
            );

        /// <summary>
        /// Creates a forward battery with three different lane strengths.
        /// </summary>
        /// <param name="damage">The weapon system's initial damage.</param>
        /// <returns>The configured weapon banks.</returns>
        private static TacticalCombatWeapons CreateWeapons(uint damage = 0)
        {
            return new TacticalCombatWeapons(
                new[,]
                {
                    { 10, 20, 30 },
                    { 0, 0, 0 },
                    { 0, 0, 0 },
                    { 0, 0, 0 },
                },
                new float[] { 12, 18, 50 },
                damage
            );
        }
    }
}
