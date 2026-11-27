using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using BitConverter = global::System.BitConverter;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class CombatBatteryTests
    {
        [Test]
        public void GetOpportunityDamage_ThreeLasers_RetainsLiveScoreBeforeTargetComparison()
        {
            CombatUnit weapons = CreateBatteryUnit(
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

            double damage = TacticalCombatFireControl.GetOpportunityDamage(
                weapons,
                TacticalWeaponArc.Forward,
                TacticalWeaponType.LaserCannon
            );

            Assert.AreEqual(0x3fe99999b0000000L, BitConverter.DoubleToInt64Bits(damage));
        }

        [Test]
        public void Advance_LiveFighterRecharge_RestoresInitialDischargeBeforeReachingCapacity()
        {
            CombatUnit weapons = CreateBatteryUnit(
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
            CombatUnit systems = CombatDamageTests.CreateUnitWithPools(
                true,
                96,
                5,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                0
            );
            weapons.DischargeBattery(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);

            TacticalCombatFireControl.RechargeBatteries(
                weapons,
                1,
                TacticalCombatFireControl.GetWeaponRecharge(systems)
            );

            Assert.That(
                weapons.GetBatteryCharge(TacticalWeaponArc.Forward),
                Is.EqualTo(-10 + TacticalCombatFireControl.GetWeaponRecharge(systems))
                    .Within(0.000001)
            );
            Assert.IsFalse(weapons.IsBatteryReady(TacticalWeaponArc.Forward));

            TacticalCombatFireControl.RechargeBatteries(
                weapons,
                9,
                TacticalCombatFireControl.GetWeaponRecharge(systems)
            );

            Assert.AreEqual(12, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(12, weapons.GetBatteryCapacity(TacticalWeaponArc.Forward));
            Assert.IsTrue(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
        }

        [TestCase(0u)]
        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        public void Constructor_OperationalCapitalBattery_StartsFullyChargedAndReady(uint damage)
        {
            CombatUnit weapons = CreateWeapons(damage);

            Assert.IsTrue(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(60, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(60, weapons.GetBatteryCapacity(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Constructor_DisabledWeapons_StartsDischarged()
        {
            CombatUnit weapons = CreateWeapons(4);

            Assert.IsFalse(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(0, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(60, weapons.GetBatteryCapacity(TacticalWeaponArc.Forward));
        }

        [Test]
        public void GetTargetDamage_IonAgainstFighters_DoesNotFire()
        {
            Assert.AreEqual(
                0,
                TacticalCombatFireControl.GetTargetDamage(
                    CreateWeapons(),
                    TacticalWeaponArc.Forward,
                    TacticalWeaponType.IonCannon,
                    true
                )
            );
        }

        [Test]
        public void GetTargetDamage_LaserAgainstCapitalShip_UsesCapitalDamageFactor()
        {
            Assert.That(
                TacticalCombatFireControl.GetTargetDamage(
                    CreateWeapons(),
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
            CombatUnit weapons = CreateWeapons();
            weapons.DischargeBattery(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);
            weapons.DischargeBattery(TacticalWeaponArc.Forward, TacticalWeaponType.IonCannon);

            Assert.AreEqual(30, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Advance_PartialRecharge_ReadiesOnlyAfterRestoringConsumedCharge()
        {
            CombatUnit weapons = CreateWeapons();
            weapons.DischargeBattery(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);
            TacticalCombatFireControl.RechargeBatteries(weapons, 0.5f, 10);

            Assert.AreEqual(55, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsBatteryReady(TacticalWeaponArc.Forward));

            TacticalCombatFireControl.RechargeBatteries(weapons, 0.5f, 10);

            Assert.AreEqual(60, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsTrue(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Advance_SurplusBudget_CapsBatteryAtItsFullCapacity()
        {
            CombatUnit weapons = CreateWeapons();
            weapons.DischargeBattery(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);

            TacticalCombatFireControl.RechargeBatteries(weapons, 10, 10);

            Assert.AreEqual(60, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsTrue(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Advance_PartialRecharge_RetainsBudgetForSubsequentBatteries()
        {
            CombatUnit weapons = CreateDirectionalWeapons();
            weapons.ClearBattery(TacticalWeaponArc.Port);
            weapons.ClearBattery(TacticalWeaponArc.Forward);
            weapons.ClearBattery(TacticalWeaponArc.Starboard);
            weapons.ClearBattery(TacticalWeaponArc.Aft);

            TacticalCombatFireControl.RechargeBatteries(weapons, 1, 45);

            Assert.AreEqual(40, weapons.GetBatteryCharge(TacticalWeaponArc.Port));
            Assert.IsTrue(weapons.IsBatteryReady(TacticalWeaponArc.Port));
            Assert.AreEqual(5, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(5, weapons.GetBatteryCharge(TacticalWeaponArc.Starboard));
            Assert.AreEqual(5, weapons.GetBatteryCharge(TacticalWeaponArc.Aft));

            TacticalCombatFireControl.RechargeBatteries(weapons, 1, 30);

            Assert.AreEqual(10, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(20, weapons.GetBatteryCharge(TacticalWeaponArc.Starboard));
            Assert.AreEqual(15, weapons.GetBatteryCharge(TacticalWeaponArc.Aft));
            Assert.IsFalse(weapons.IsBatteryReady(TacticalWeaponArc.Aft));
        }

        [Test]
        public void Advance_CompletedAdjacentBatteries_RetainsTheirNextDischargePositions()
        {
            CombatUnit weapons = CreateDirectionalWeapons();
            for (int arc = 0; arc < 4; arc++)
                weapons.ClearBattery((TacticalWeaponArc)arc);
            TacticalCombatFireControl.RechargeBatteries(weapons, 1, 100);
            for (int arc = 0; arc < 4; arc++)
                Assert.IsTrue(weapons.IsBatteryReady((TacticalWeaponArc)arc));
            weapons.DischargeBattery(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);
            weapons.DischargeBattery(TacticalWeaponArc.Starboard, TacticalWeaponType.LaserCannon);

            TacticalCombatFireControl.RechargeBatteries(weapons, 1, 25);

            Assert.AreEqual(5, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(20, weapons.GetBatteryCharge(TacticalWeaponArc.Starboard));
        }

        [Test]
        public void Advance_ZeroBudget_PreservesDischargedState()
        {
            CombatUnit weapons = CreateWeapons();
            weapons.DischargeBattery(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);
            TacticalCombatFireControl.RechargeBatteries(weapons, 0, 10);

            Assert.AreEqual(50, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Constructor_EmptyBattery_StartsUnready(bool isFighter)
        {
            CombatUnit weapons = CreateBatteryUnit(new int[4, 3], new float[3], 0, isFighter);

            for (int arc = 0; arc < 4; arc++)
            {
                Assert.IsFalse(weapons.IsBatteryReady((TacticalWeaponArc)arc));
                Assert.AreEqual(0, weapons.GetBatteryCharge((TacticalWeaponArc)arc));
                Assert.AreEqual(0, weapons.GetBatteryCapacity((TacticalWeaponArc)arc));
            }
        }

        [Test]
        public void Constructor_FighterBattery_StartsReadyWithoutStoredCharge()
        {
            CombatUnit weapons = CreateBatteryUnit(
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

            Assert.IsTrue(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(0, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(4, weapons.GetBatteryCapacity(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Discharge_InitialFighterBattery_RetainsNegativeChargeUntilRecharged()
        {
            CombatUnit weapons = CreateBatteryUnit(
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

            weapons.DischargeBattery(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);

            Assert.AreEqual(-3, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsBatteryReady(TacticalWeaponArc.Forward));

            TacticalCombatFireControl.RechargeBatteries(weapons, 1, 2);

            Assert.AreEqual(-1, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsBatteryReady(TacticalWeaponArc.Forward));

            TacticalCombatFireControl.RechargeBatteries(weapons, 1, 5);

            Assert.AreEqual(4, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsTrue(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Advance_DisabledCapitalBatteryWithRestoredBudget_RequiresFullRecharge()
        {
            CombatUnit weapons = CreateWeapons(4);

            TacticalCombatFireControl.RechargeBatteries(weapons, 1, 1);

            Assert.IsFalse(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(1, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));

            TacticalCombatFireControl.RechargeBatteries(weapons, 59, 1);

            Assert.IsTrue(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
            Assert.AreEqual(60, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Advance_DisabledWeaponSystem_LeavesBatteryDischarged()
        {
            CombatUnit weapons = CreateWeapons(4);
            CombatUnit systems = CombatDamageTests.CreateUnitWithPools(
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

            TacticalCombatFireControl.RechargeBatteries(
                weapons,
                1000,
                TacticalCombatFireControl.GetWeaponRecharge(systems)
            );

            Assert.AreEqual(0, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void ClearBattery_PartiallyChargedBattery_RemovesItsRemainingCharge()
        {
            CombatUnit weapons = CreateWeapons();
            weapons.DischargeBattery(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon);
            weapons.ClearBattery(TacticalWeaponArc.Forward);

            Assert.AreEqual(0, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(weapons.IsBatteryReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void ClearBattery_RepeatedDisruption_QueuesEachBatteryOnce()
        {
            CombatUnit weapons = CreateDirectionalWeapons();
            weapons.ClearBattery(TacticalWeaponArc.Forward);
            weapons.ClearBattery(TacticalWeaponArc.Forward);
            weapons.ClearBattery(TacticalWeaponArc.Starboard);

            TacticalCombatFireControl.RechargeBatteries(weapons, 1, 15);

            Assert.AreEqual(10, weapons.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.AreEqual(5, weapons.GetBatteryCharge(TacticalWeaponArc.Starboard));
        }

        /// <summary>
        /// Creates distinct laser capacities for observing ordered recharge across all four arcs.
        /// </summary>
        /// <returns>The configured directional batteries.</returns>
        private static CombatUnit CreateDirectionalWeapons() =>
            CreateBatteryUnit(
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
        private static CombatUnit CreateWeapons(uint damage = 0)
        {
            return CreateBatteryUnit(
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

        /// <summary>
        /// Creates a combatant with the fixture's weapon ratings and initial damage.
        /// </summary>
        /// <param name="strengths">The authored weapon strengths in arc order.</param>
        /// <param name="ranges">The weapon ranges for the fixture.</param>
        /// <param name="damage">The initial number of disabled weapon systems.</param>
        /// <param name="fighter">Whether to use the fighter battery rules.</param>
        /// <returns>The detached battle participant used to test battery behavior.</returns>
        private static CombatUnit CreateBatteryUnit(
            int[,] strengths,
            float[] ranges,
            uint damage,
            bool fighter = false
        )
        {
            if (fighter)
                return new CombatUnit(
                    new Starfighter
                    {
                        MaxSquadronSize = 1,
                        CurrentSquadronSize = 1,
                        LaserCannon = strengths[0, 0],
                        IonCannon = strengths[0, 1],
                        LaserRange = (int)ranges[0],
                        IonRange = (int)ranges[1],
                    },
                    CombatSide.Attacker,
                    0
                );
            CapitalShip ship = new()
            {
                SystemDamage = new CapitalShipSystemDamage { Weapons = (int)damage },
            };
            for (int arc = 0; arc < 4; arc++)
            {
                HardpointGroup group = new() { Arc = (TacticalWeaponArc)arc };
                HardpointWeaponType[] weapons =
                {
                    HardpointWeaponType.LaserCannon,
                    HardpointWeaponType.IonCannon,
                    HardpointWeaponType.Turbolaser,
                };
                for (int weapon = 0; weapon < weapons.Length; weapon++)
                    group
                        .GetHardpoints()
                        .Add(
                            new Hardpoint
                            {
                                Weapon = new WeaponData
                                {
                                    WeaponType = weapons[weapon],
                                    Damage = strengths[arc, weapon],
                                    Range = ranges[weapon],
                                },
                            }
                        );
                ship.HardpointGroups.Add(group);
            }
            return new CombatUnit(ship, CombatSide.Attacker, 0);
        }
    }
}
