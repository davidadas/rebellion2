using NUnit.Framework;
using Rebellion.Gameplay;

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

            Assert.AreEqual(0x3fe99999b0000000L, System.BitConverter.DoubleToInt64Bits(damage));
        }

        [Test]
        public void Advance_LiveFighterRecharge_ReachesStoredZeroTarget()
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

            Assert.AreEqual(0, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Constructor_ArmedCapitalBattery_StoresChargeWithoutReadyFlag()
        {
            TacticalCombatWeapons weapons = CreateWeapons();

            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(60, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(0, weapons.GetMaximumCharge(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Constructor_DisabledWeapons_StartsDischarged()
        {
            TacticalCombatWeapons weapons = CreateWeapons(4);

            Assert.IsFalse(weapons.IsReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(0, weapons.GetCharge(TacticalWeaponArc.Forward));
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
        public void Advance_ChargeAboveTarget_ResetsChargeAndReadiesBattery()
        {
            TacticalCombatWeapons weapons = CreateWeapons();
            weapons.Discharge(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);
            weapons.Advance(0.5f, 10);

            Assert.AreEqual(0, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Forward));
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

        [Test]
        public void Constructor_EmptyCapitalBattery_PreservesDefaultReadiness()
        {
            TacticalCombatWeapons weapons = CreateWeapons();

            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Aft));
            Assert.AreEqual(0, weapons.GetCharge(TacticalWeaponArc.Aft));
            Assert.AreEqual(0, weapons.GetMaximumCharge(TacticalWeaponArc.Aft));
        }

        [Test]
        public void Constructor_FighterBattery_PreservesReadinessAndZeroRechargeTarget()
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
            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Aft));
            Assert.AreEqual(4, weapons.GetCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(0, weapons.GetMaximumCharge(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Advance_DisabledCapitalBatteryWithPositiveBudget_ReadiesAtZeroCharge()
        {
            TacticalCombatWeapons weapons = CreateWeapons(4);

            weapons.Advance(1, 1);

            Assert.IsTrue(weapons.IsReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(0, weapons.GetCharge(TacticalWeaponArc.Forward));
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
