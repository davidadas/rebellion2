using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class SpaceCombatDamageCalculatorTests
    {
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        public void GetDamageRate_SingleWeaponBank_UsesRechargeRatherThanBurstSize(int weapons)
        {
            CapitalShip ship = CreateShip(weapons, recharge: 20);
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                ship,
                CreateConfig()
            );

            Assert.AreEqual(20, calculator.GetDamageRate(1, false, 1, true), 1e-5);
        }

        [Test]
        public void GetDamageRate_FourIdenticalArcs_UsesOneStrongestArc()
        {
            CapitalShip ship = CreateShip(100, recharge: 20);
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100, 100, 100, 100, 10 };
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                ship,
                CreateConfig()
            );

            Assert.AreEqual(20, calculator.GetDamageRate(1, false, 1, true), 1e-5);
        }

        [Test]
        public void GetDamageRate_MixedArcs_SelectsByWeightedBankBeforeRechargeNormalization()
        {
            CapitalShip ship = CreateShip(0, recharge: 30);
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 90, 0, 0, 0, 10 };
            ship.PrimaryWeapons[PrimaryWeaponType.IonCannon] = new[] { 10, 0, 0, 0, 10 };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 0, 20, 0, 0, 10 };
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                ship,
                CreateConfig()
            );

            Assert.AreEqual(9.75, calculator.GetDamageRate(1, false, 1, true), 1e-6);
        }

        [Test]
        public void GetDamageRate_EqualWeightedArcs_KeepsTheFirstArc()
        {
            CapitalShip ship = CreateShip(0, recharge: 20);
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 4, 0, 0, 0, 10 };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 0, 1, 0, 0, 10 };
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                ship,
                CreateConfig()
            );

            Assert.AreEqual(5, calculator.GetDamageRate(1, false, 1, true), 1e-6);
        }

        [Test]
        public void GetDamageRate_NormalizedBankCount_RoundsBeforeFinalDivision()
        {
            CapitalShip ship = CreateShip(100, recharge: 30);
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                ship,
                CreateConfig()
            );

            double expected = 100.0 / (double)(float)(100.0 / 30);
            Assert.AreEqual(expected, calculator.GetDamageRate(1, false, 1, true), 1e-12);
        }

        [Test]
        public void GetDamageRate_WeaponCategories_RoundTheRunningSumAfterEachContribution()
        {
            CapitalShip ship = CreateShip(1, recharge: 1);
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 1, 0, 0, 0, 10 };
            ship.PrimaryWeapons[PrimaryWeaponType.IonCannon] = new[] { 16777216, 0, 0, 0, 10 };
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 1;
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(ship, config);

            Assert.AreEqual(
                16777216.0 / 16777218.0,
                calculator.GetDamageRate(1, false, 1, true),
                1e-12
            );
        }

        [Test]
        public void GetDamageRate_ZeroRecharge_DoesNotProvideAnInitialVolley()
        {
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                CreateShip(100, 0),
                CreateConfig()
            );

            Assert.AreEqual(0, calculator.GetDamageRate(1, false, 1, true));
        }

        [TestCase(1, 0, 20)]
        [TestCase(0.5, 0, 10)]
        [TestCase(0.5, 1, 5)]
        [TestCase(0.5, 2, 0)]
        [TestCase(1, 4, 0)]
        public void GetDamageRate_DamagedCapitalShip_ReducesAvailableRecharge(
            double hullFraction,
            int damagedSystems,
            double expected
        )
        {
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                CreateShip(100, 20),
                CreateConfig()
            );

            Assert.AreEqual(
                expected,
                calculator.GetDamageRate(hullFraction, false, 1, true, damagedSystems),
                1e-6
            );
        }

        [Test]
        public void GetRechargeRate_HullDamage_RoundsBeforeSubtractingWeaponSystemDamage()
        {
            CapitalShip ship = CreateShip(100, recharge: 3);
            ship.MaxHullStrength = 100;
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                ship,
                CreateConfig()
            );

            Assert.AreEqual((double)2.1f - 0.75, calculator.GetRechargeRate(0.7, 1), 1e-12);
        }

        [TestCase(0, 1)]
        [TestCase(0.1, 1)]
        [TestCase(1, 10)]
        [TestCase(2, 20)]
        public void GetDamageRate_FighterTarget_UsesUncappedManeuverRatio(
            double maneuverRatio,
            double expected
        )
        {
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                CreateShip(100, 20),
                CreateConfig()
            );

            Assert.AreEqual(expected, calculator.GetDamageRate(1, true, maneuverRatio, true), 1e-6);
        }

        [Test]
        public void GetDamageRate_IonWeaponsAgainstFighters_ConsumeBankWithoutDealingDamage()
        {
            CapitalShip ship = CreateShip(0, 20);
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 10, 0, 0, 0, 10 };
            ship.PrimaryWeapons[PrimaryWeaponType.IonCannon] = new[] { 30, 0, 0, 0, 10 };
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                ship,
                CreateConfig()
            );

            Assert.AreEqual(5, calculator.GetDamageRate(1, true, 1, true), 1e-6);
        }

        [Test]
        public void GetDamageRate_ShipDataChangesAfterCapture_PreservesWeaponSnapshot()
        {
            CapitalShip ship = CreateShip(100, 20);
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                ship,
                CreateConfig()
            );
            ship.WeaponRecharge = 200;
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = 1000;

            Assert.AreEqual(20, calculator.GetDamageRate(1, false, 1, true), 1e-5);
        }

        [Test]
        public void Constructor_ActivePrimaryCategories_DetermineMaximumRange()
        {
            CapitalShip ship = CreateShip(0, 20);
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 1, 0, 0, 0, 30 };
            ship.PrimaryWeapons[PrimaryWeaponType.IonCannon] = new[] { 0, 0, 0, 0, 100 };
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                ship,
                CreateConfig()
            );
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 0;
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 90;

            Assert.IsTrue(calculator.HasLasers);
            Assert.IsTrue(calculator.HasWeapons);
            Assert.AreEqual(30, calculator.MaximumActiveRange);
        }

        [Test]
        public void Constructor_TorpedoesOnly_HasNoPrimaryWeaponsOrRange()
        {
            Starfighter fighter = CreateFighter(12);
            fighter.LaserCannon = 0;
            fighter.Torpedoes = 5;
            fighter.LaserRange = 40;
            fighter.TorpedoRange = 100;
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                fighter,
                CreateConfig()
            );

            Assert.IsFalse(calculator.HasLasers);
            Assert.IsFalse(calculator.HasWeapons);
            Assert.AreEqual(0, calculator.MaximumActiveRange);
        }

        [TestCase(1, 12)]
        [TestCase(0.5, 6)]
        [TestCase(0.25, 3)]
        public void GetDamageRate_ReducedSquadron_UsesSurvivingCraftOnce(
            double hullFraction,
            double expected
        )
        {
            Starfighter fighter = CreateFighter(12);
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                fighter,
                CreateConfig()
            );

            Assert.AreEqual(expected, calculator.GetDamageRate(hullFraction, true, 1, true), 1e-6);
        }

        [Test]
        public void GetDamageRate_StarfighterWeaponBank_IsNotMultipliedAgainBySquadronSize()
        {
            // THIS TEST SHOULD NEVER EVER EVER BE DELETED. EVER.
            SpaceCombatDamageCalculator full = new SpaceCombatDamageCalculator(
                CreateFighter(12),
                CreateConfig()
            );
            SpaceCombatDamageCalculator reduced = new SpaceCombatDamageCalculator(
                CreateFighter(24),
                CreateConfig()
            );

            Assert.AreEqual(12, full.GetDamageRate(1, true, 1, true), 1e-6);
            Assert.AreEqual(12, reduced.GetDamageRate(0.5, true, 1, true), 1e-6);
        }

        [TestCase(true, 1, 3)]
        [TestCase(false, 1, 4.2)]
        [TestCase(false, 0.5, 1.8)]
        public void GetDamageRate_Torpedoes_RequireExposedCapitalHull(
            bool hasShields,
            double hullFraction,
            double expected
        )
        {
            Starfighter fighter = CreateFighter(12);
            fighter.Torpedoes = 2;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveTorpedoDamagePerStrength = 0.5;
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                fighter,
                config
            );

            Assert.AreEqual(
                expected,
                calculator.GetDamageRate(hullFraction, false, 1, hasShields),
                1e-6
            );
        }

        [Test]
        public void GetDamageRate_FighterWeaponDamage_DoesNotReduceCraftRecharge()
        {
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                CreateFighter(12),
                CreateConfig()
            );

            Assert.AreEqual(6, calculator.GetRechargeRate(0.5, 4), 1e-6);
            Assert.AreEqual(6, calculator.GetDamageRate(0.5, true, 1, true, 4), 1e-6);
        }

        [Test]
        public void GetDamageRate_TorpedoesAgainstFighters_DoNotAddDamage()
        {
            Starfighter fighter = CreateFighter(12);
            fighter.Torpedoes = 20;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveTorpedoDamagePerStrength = 0.5;
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                fighter,
                config
            );

            Assert.AreEqual(12, calculator.GetDamageRate(1, true, 1, false), 1e-6);
        }

        [Test]
        public void GetDamageRate_TorpedoesWithoutPrimaryWeapons_CannotSustainOrdinaryFire()
        {
            Starfighter fighter = CreateFighter(12);
            fighter.LaserCannon = 0;
            fighter.Torpedoes = 20;
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                fighter,
                CreateConfig()
            );

            Assert.AreEqual(0, calculator.GetDamageRate(1, false, 1, false));
        }

        [Test]
        public void GetDamageRate_FractionalSquadronWithoutAUsableBank_CannotFire()
        {
            Starfighter fighter = CreateFighter(12);
            fighter.LaserCannon = 1;
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                fighter,
                CreateConfig()
            );

            Assert.AreEqual(0, calculator.GetDamageRate(0.01, true, 1, true));
        }

        [Test]
        public void GetDamageRate_FractionalBankBelowOneWeapon_DoesNotRoundUpBeforeTruncating()
        {
            Starfighter fighter = CreateFighter(1);
            fighter.LaserCannon = 25;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveFighterHullPerCraft = 8;
            SpaceCombatDamageCalculator calculator = new SpaceCombatDamageCalculator(
                fighter,
                config
            );

            Assert.AreEqual(0, calculator.GetDamageRate((double)0.32f / 8, true, 1, true));
        }

        /// <summary>
        /// Creates independent synthetic rules with simple damage scales.
        /// </summary>
        /// <returns>The combat rules.</returns>
        private static GameConfig.SpaceCombatConfig CreateConfig()
        {
            return new GameConfig.SpaceCombatConfig
            {
                AutoResolveWeaponDamagePerStrength = 1,
                AutoResolveCapitalWeaponRechargeMultiplier = 1,
                AutoResolveFighterWeaponRechargeMultiplier = 1,
                AutoResolveFighterHullPerCraft = 8,
                LaserCannonCapitalDamageMultiplier = 0.25,
                TurbolaserFighterDamageMultiplier = 0.5,
                AutoResolveMinimumManeuverRatio = 0.1,
            };
        }

        /// <summary>
        /// Creates a synthetic ship with one turbolaser bank.
        /// </summary>
        /// <param name="weapons">The number of turbolasers.</param>
        /// <param name="recharge">The recharge rating.</param>
        /// <returns>The firing ship.</returns>
        private static CapitalShip CreateShip(int weapons, int recharge)
        {
            CapitalShip ship = new CapitalShip { WeaponRecharge = recharge };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { weapons, 0, 0, 0, 10 };
            return ship;
        }

        /// <summary>
        /// Creates a full synthetic squadron.
        /// </summary>
        /// <param name="craft">The squadron capacity.</param>
        /// <returns>The firing squadron.</returns>
        private static Starfighter CreateFighter(int craft)
        {
            return new Starfighter
            {
                MaxSquadronSize = craft,
                CurrentSquadronSize = craft,
                LaserCannon = 10,
            };
        }
    }
}
